using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Host.WinForms;

public sealed class HiveDirectLlmConversationView : UserControl
{
    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveThemeManager _themeManager;
    private readonly HiveComboBox _executionTargetSelector;
    private readonly ListBox _conversationList;
    private readonly TextBox _transcriptTextBox;
    private readonly HiveScrollHost _transcriptScrollHost;
    private readonly TextBox _messageTextBox;
    private readonly HiveScrollHost _messageScrollHost;
    private readonly Label _conversationDetailsLabel;
    private readonly Label _statusLabel;
    private readonly HiveButton _refreshButton;
    private readonly HiveButton _newConversationButton;
    private readonly HiveButton _sendButton;
    private readonly HiveButton _cancelButton;
    private readonly CancellationTokenSource _lifetimeCts = new();

    private readonly List<TargetOption> _targets = [];
    private DirectLlmConversationSummary? _selectedConversation;
    private CancellationTokenSource? _operationCts;
    private bool _suppressConversationSelection;
    private bool _disposed;

    public HiveDirectLlmConversationView(
        IHiveManagementFacade management,
        ResourceAccessContext accessContext,
        IHiveThemeManager themeManager)
    {
        _management = management ?? throw new ArgumentNullException(nameof(management));
        _accessContext = accessContext ?? throw new ArgumentNullException(nameof(accessContext));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = new Padding(12);
        AccessibleName = "Direct LLM conversation workspace";

        _refreshButton = CreateButton("Refresh", HiveButtonStyle.Secondary);
        _newConversationButton = CreateButton("New conversation", HiveButtonStyle.Secondary);
        _sendButton = CreateButton("Send", HiveButtonStyle.Primary);
        _cancelButton = CreateButton("Cancel request", HiveButtonStyle.Danger);

        _executionTargetSelector = new HiveComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            AccessibleName = "Direct LLM ExecutionTarget",
            AccessibleDescription = "Choose the exact configured ExecutionTarget for the next direct LLM request."
        };
        _executionTargetSelector.DisplayMember = nameof(TargetOption.DisplayLabel);

        _conversationList = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            BorderStyle = BorderStyle.None,
            AccessibleName = "Direct LLM conversations",
            AccessibleDescription = "Select a saved conversation to open its history."
        };
        _conversationList.SelectedIndexChanged += ConversationSelectionChanged;

        _transcriptTextBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            WordWrap = true,
            ScrollBars = ScrollBars.None,
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill,
            TabStop = true,
            AccessibleName = "Conversation transcript",
            AccessibleDescription = "Saved user messages and assistant replies for the selected conversation."
        };
        _transcriptScrollHost = new HiveScrollHost
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            AccessibleName = "Conversation transcript scroll area"
        };
        _transcriptScrollHost.Attach(_transcriptTextBox);

        _messageTextBox = new TextBox
        {
            Multiline = true,
            AcceptsReturn = true,
            WordWrap = true,
            ScrollBars = ScrollBars.None,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Fill,
            MaxLength = 64 * 1024,
            AccessibleName = "Message to send",
            AccessibleDescription = "Enter a message to send to the selected ExecutionTarget."
        };
        _messageScrollHost = new HiveScrollHost
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            AccessibleName = "Message editor scroll area"
        };
        _messageScrollHost.Attach(_messageTextBox);

        _conversationDetailsLabel = CreateValueLabel();
        _conversationDetailsLabel.AccessibleName = "Direct LLM execution details";
        _statusLabel = CreateValueLabel();
        _statusLabel.AccessibleRole = AccessibleRole.StatusBar;
        _statusLabel.Text = "Select Refresh to load conversations and active ExecutionTargets.";

        _refreshButton.Click += async (_, _) =>
            await RunOperationAsync(RefreshAsync);
        _newConversationButton.Click += async (_, _) =>
            await RunOperationAsync(CreateConversationAsync);
        _sendButton.Click += async (_, _) =>
            await RunOperationAsync(SendMessageAsync);
        _cancelButton.Click += (_, _) => CancelCurrentOperation();

        Controls.Add(BuildLayout());

        _themeManager.ThemeChanged += ThemeManagerOnChanged;
        _themeManager.Apply(this);
        ApplyStatusVisual();
        UpdateActionState();
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var tokens = cancellationToken.CanBeCanceled
            ? cancellationToken
            : CancellationToken.None;

        await RefreshTargetsAsync(tokens).ConfigureAwait(true);
        if (IsUnavailable())
            return;

        await RefreshConversationsAsync(
            preferredConversationId: _selectedConversation?.Id,
            tokens).ConfigureAwait(true);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _disposed = true;
            _themeManager.ThemeChanged -= ThemeManagerOnChanged;

            _lifetimeCts.Cancel();
            var operationCts = Interlocked.Exchange(ref _operationCts, null);
            operationCts?.Cancel();
            operationCts?.Dispose();
            _lifetimeCts.Dispose();

            _conversationList.SelectedIndexChanged -= ConversationSelectionChanged;
        }

        base.Dispose(disposing);
    }

    private async Task RefreshTargetsAsync(CancellationToken cancellationToken)
    {
        _statusLabel.Text = "Loading active ExecutionTargets…";

        var providersResult = await _management.ListProvidersAsync(
            _accessContext,
            includeRetired: false,
            cancellationToken).ConfigureAwait(true);

        if (providersResult.IsFailure)
        {
            ReportError(providersResult.Error!, "ExecutionTargets could not be loaded.");
            return;
        }

        var targets = new List<TargetOption>();
        foreach (var provider in providersResult.Value!)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (provider.Resource.Lifecycle.Status != ResourceLifecycleStatus.Active ||
                BuiltInProviderCatalog.Find(provider.Key)?.RequiresNativeIntegration == true ||
                !string.Equals(provider.TransportKind, "openai-compatible", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var accountsResult = await _management.ListProviderAccountsAsync(
                provider.Id,
                _accessContext,
                includeRetired: false,
                cancellationToken).ConfigureAwait(true);

            if (accountsResult.IsFailure)
            {
                ReportError(accountsResult.Error!, "Provider accounts could not be loaded.");
                return;
            }

            foreach (var account in accountsResult.Value!)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (account.Resource.Lifecycle.Status != ResourceLifecycleStatus.Active)
                    continue;

                var targetResult = await _management.ListExecutionTargetsAsync(
                    account.Id,
                    _accessContext,
                    includeRetired: false,
                    cancellationToken).ConfigureAwait(true);

                if (targetResult.IsFailure)
                {
                    ReportError(targetResult.Error!, "ExecutionTargets could not be loaded.");
                    return;
                }

                foreach (var target in targetResult.Value!)
                {
                    if (target.Resource.Lifecycle.Status != ResourceLifecycleStatus.Active ||
                        target.Capabilities.Any(static capability =>
                            capability.Capability == HiveCapabilityKeys.TextGeneration &&
                            capability.State == CapabilityState.Unsupported))
                    {
                        continue;
                    }

                    targets.Add(new TargetOption(
                        target,
                        $"{provider.DisplayName} / {account.DisplayName} / " +
                        $"{target.DisplayName} ({target.Model ?? target.Deployment})"));
                }
            }
        }

        if (IsUnavailable() || cancellationToken.IsCancellationRequested)
            return;

        var previouslySelectedTargetId =
            (_executionTargetSelector.SelectedItem as TargetOption)?.Target.Id;

        _targets.Clear();
        _targets.AddRange(
            targets.OrderBy(static target => target.DisplayLabel, StringComparer.OrdinalIgnoreCase));

        _executionTargetSelector.DataSource = null;
        _executionTargetSelector.DataSource = _targets;

        var selectedIndex = previouslySelectedTargetId is { } oldId
            ? _targets.FindIndex(target => target.Target.Id == oldId)
            : -1;

        _executionTargetSelector.SelectedIndex = selectedIndex >= 0
            ? selectedIndex
            : _targets.Count > 0 ? 0 : -1;

        if (_targets.Count == 0)
        {
            _statusLabel.Text =
                "No active OpenAI-compatible ExecutionTargets are available. Configure one in Settings first.";
        }
        else
        {
            _statusLabel.Text = $"{_targets.Count} active ExecutionTarget(s) available.";
        }

        UpdateActionState();
    }

    private async Task RefreshConversationsAsync(
        ConversationId? preferredConversationId,
        CancellationToken cancellationToken)
    {
        var conversationsResult = await _management.ListDirectLlmConversationsAsync(
            _accessContext,
            pageSize: 50,
            cancellationToken).ConfigureAwait(true);

        if (conversationsResult.IsFailure)
        {
            ReportError(conversationsResult.Error!, "Conversation history could not be loaded.");
            return;
        }

        if (IsUnavailable() || cancellationToken.IsCancellationRequested)
            return;

        var options = conversationsResult.Value!
            .Select(static conversation => new ConversationOption(conversation))
            .ToArray();

        _suppressConversationSelection = true;
        try
        {
            _conversationList.BeginUpdate();
            _conversationList.Items.Clear();
            _conversationList.Items.AddRange(options);

            var preferredIndex = preferredConversationId is { } id
                ? Array.FindIndex(options, option => option.Conversation.Id == id)
                : -1;

            if (preferredIndex < 0 && options.Length > 0)
                preferredIndex = 0;

            _conversationList.SelectedIndex = preferredIndex;
        }
        finally
        {
            _conversationList.EndUpdate();
            _suppressConversationSelection = false;
        }

        if (preferredConversationId is { } selectedId &&
            options.Any(option => option.Conversation.Id == selectedId))
        {
            await LoadConversationAsync(selectedId, cancellationToken).ConfigureAwait(true);
        }
        else if (options.Length > 0)
        {
            await LoadConversationAsync(
                options[Math.Max(0, _conversationList.SelectedIndex)].Conversation.Id,
                cancellationToken).ConfigureAwait(true);
        }
        else
        {
            _selectedConversation = null;
            _transcriptTextBox.Clear();
            _conversationDetailsLabel.Text = "No conversation selected.";
        }

        UpdateActionState();
    }

    private async Task CreateConversationAsync(CancellationToken cancellationToken)
    {
        var result = await _management.CreateDirectLlmConversationAsync(
            _accessContext,
            cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            ReportError(result.Error!, "A new conversation could not be created.");
            return;
        }

        await RefreshConversationsAsync(
            result.Value!.Id,
            cancellationToken).ConfigureAwait(true);

        _statusLabel.Text = "Conversation created.";
    }

    private async Task SendMessageAsync(CancellationToken cancellationToken)
    {
        var target = _executionTargetSelector.SelectedItem as TargetOption;
        if (target is null)
        {
            HiveMessageBox.ShowInformation(
                FindForm(),
                "Select an active ExecutionTarget before sending a message.",
                "ExecutionTarget required",
                _themeManager);
            return;
        }

        if (_selectedConversation is null)
        {
            HiveMessageBox.ShowInformation(
                FindForm(),
                "Create or select a conversation before sending a message.",
                "Conversation required",
                _themeManager);
            return;
        }

        var message = _messageTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            HiveMessageBox.ShowInformation(
                FindForm(),
                "Enter a message before sending it.",
                "Message required",
                _themeManager);
            return;
        }

        var conversationId = _selectedConversation.Id;
        Result<DirectLlmConversation> result;
        try
        {
            result = await _management.SendDirectLlmMessageAsync(
                conversationId,
                target.Target.Id,
                message,
                _accessContext,
                cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!IsUnavailable())
            {
                await RefreshConversationsAsync(
                    conversationId,
                    CancellationToken.None).ConfigureAwait(true);
                _statusLabel.Text = "Request cancelled; conversation state refreshed.";
            }

            return;
        }

        if (IsUnavailable())
            return;

        if (result.IsSuccess)
        {
            _messageTextBox.Clear();
            _selectedConversation = result.Value!.Summary;
            await RefreshConversationsAsync(
                conversationId,
                CancellationToken.None).ConfigureAwait(true);
            _statusLabel.Text = "Response received and saved.";
            return;
        }

        // A provider failure may still have a durable user-message/failure event.
        // Reload the conversation so the view reflects the persisted outcome.
        await RefreshConversationsAsync(
            conversationId,
            CancellationToken.None).ConfigureAwait(true);

        ReportError(result.Error!, "The direct LLM request could not be completed.");
    }

    private async Task LoadConversationAsync(
        ConversationId conversationId,
        CancellationToken cancellationToken)
    {
        var result = await _management.GetDirectLlmConversationAsync(
            conversationId,
            _accessContext,
            cancellationToken).ConfigureAwait(true);

        if (IsUnavailable() || cancellationToken.IsCancellationRequested)
            return;

        if (result.IsFailure)
        {
            ReportError(result.Error!, "The selected conversation could not be opened.");
            return;
        }

        var conversation = result.Value!;
        _selectedConversation = conversation.Summary;
        _conversationDetailsLabel.Text = FormatConversationDetails(conversation.Summary);

        var lines = new List<string>();
        foreach (var message in conversation.Messages)
        {
            lines.Add(message.Role == DirectLlmConversationMessageRole.User
                ? $"You  ·  {message.CreatedAtUtc.ToLocalTime():g}"
                : $"Assistant  ·  {message.CreatedAtUtc.ToLocalTime():g}");
            lines.Add(message.Content);
            lines.Add(string.Empty);
        }

        _transcriptTextBox.Text = lines.Count == 0
            ? "No messages yet. Choose an ExecutionTarget and send a message."
            : string.Join(Environment.NewLine, lines);

        _transcriptScrollHost.VerticalScrollPosition = int.MaxValue;
        UpdateActionState();
    }

    private async Task ConversationSelectionChangedAsync(CancellationToken cancellationToken)
    {
        if (_suppressConversationSelection ||
            _conversationList.SelectedItem is not ConversationOption option)
        {
            return;
        }

        await LoadConversationAsync(
            option.Conversation.Id,
            cancellationToken).ConfigureAwait(true);
    }

    private async void ConversationSelectionChanged(object? sender, EventArgs e)
    {
        await RunOperationAsync(ConversationSelectionChangedAsync);
    }

    private async Task RunOperationAsync(
        Func<CancellationToken, Task> operation)
    {
        if (_disposed || IsDisposed || Disposing ||
            Interlocked.CompareExchange(ref _operationCts, null, null) is not null)
        {
            return;
        }

        var operationCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
        if (Interlocked.CompareExchange(ref _operationCts, operationCts, null) is not null)
        {
            operationCts.Dispose();
            return;
        }

        SetBusy(true);
        try
        {
            await operation(operationCts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (operationCts.IsCancellationRequested)
        {
            // Cancellation is an expected Workspace lifecycle event.
        }
        catch (Exception exception)
        {
            if (!operationCts.IsCancellationRequested && !IsUnavailable())
            {
                HiveUiErrorReporter.Report(
                    FindForm(),
                    exception,
                    "Direct LLM operation failed",
                    "The direct LLM workspace operation could not be completed.",
                    null,
                    _themeManager);
            }
        }
        finally
        {
            Interlocked.CompareExchange(ref _operationCts, null, operationCts);
            operationCts.Dispose();

            if (!IsUnavailable())
                SetBusy(false);
        }
    }

    private void CancelCurrentOperation()
    {
        var current = Interlocked.CompareExchange(ref _operationCts, null, null);
        current?.Cancel();
    }

    private void SetBusy(bool busy)
    {
        _refreshButton.Enabled = !busy;
        _newConversationButton.Enabled = !busy;
        _executionTargetSelector.Enabled = !busy;
        _conversationList.Enabled = !busy;
        _messageTextBox.Enabled = !busy;
        _sendButton.Enabled = !busy;
        _cancelButton.Enabled = busy;

        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        if (busy)
            _statusLabel.Text = "Working…";

        if (!busy)
            UpdateActionState();
    }

    private void UpdateActionState()
    {
        if (_disposed || IsDisposed || Disposing)
            return;

        var busy = Interlocked.CompareExchange(ref _operationCts, null, null) is not null;
        _newConversationButton.Enabled = !busy;
        _executionTargetSelector.Enabled = !busy && _targets.Count > 0;
        _conversationList.Enabled = !busy && _conversationList.Items.Count > 0;
        _messageTextBox.Enabled = !busy && _selectedConversation is not null;
        _sendButton.Enabled = !busy &&
                              _selectedConversation is not null &&
                              _executionTargetSelector.SelectedItem is TargetOption;
        _cancelButton.Enabled = busy;
    }

    private void ReportError(Error error, string heading)
    {
        if (IsUnavailable())
            return;

        _statusLabel.Text = $"{error.Code} — {error.Message}";
        ApplyStatusVisual();
        HiveMessageBox.ShowError(
            FindForm(),
            $"{heading}{Environment.NewLine}{error.Message}",
            "Direct LLM operation failed",
            _themeManager);
    }

    private void ThemeManagerOnChanged(object? sender, EventArgs e) =>
        ApplyStatusVisual();

    private void ApplyStatusVisual()
    {
        _statusLabel.ForeColor = _themeManager.Theme.Palette.MutedText;
        _statusLabel.BackColor = _themeManager.Theme.Palette.Surface;
        _conversationDetailsLabel.ForeColor = _themeManager.Theme.Palette.Text;
        _conversationDetailsLabel.BackColor = _themeManager.Theme.Palette.Surface;
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var title = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            Font = new Font(Font, FontStyle.Bold),
            Text = "Direct LLM",
            TextAlign = ContentAlignment.MiddleLeft,
            AccessibleRole = AccessibleRole.StaticText
        };

        var headerActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        headerActions.Controls.Add(_refreshButton);
        headerActions.Controls.Add(_newConversationButton);
        header.Controls.Add(title, 0, 0);
        header.Controls.Add(headerActions, 1, 0);

        var targetRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0, 2, 0, 8),
            Padding = Padding.Empty
        };
        targetRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
        targetRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        targetRow.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        targetRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var targetLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "ExecutionTarget",
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        targetRow.Controls.Add(targetLabel, 0, 0);
        targetRow.Controls.Add(_executionTargetSelector, 1, 0);
        targetRow.SetColumnSpan(targetLabel, 1);
        targetRow.SetRow(_executionTargetSelector, 1);
        targetRow.SetColumn(_executionTargetSelector, 0);
        targetRow.SetColumnSpan(_executionTargetSelector, 2);

        var workspace = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            FixedPanel = FixedPanel.Panel1
        };
        workspace.SizeChanged += (_, _) =>
        {
            var availableWidth = workspace.ClientSize.Width - workspace.SplitterWidth;
            if (availableWidth >= 50)
                workspace.SplitterDistance = Math.Clamp(250, 25, availableWidth - 25);
        };

        var historyPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 8, 0)
        };
        historyPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        historyPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        historyPanel.Controls.Add(CreateSectionLabel("Conversations"), 0, 0);
        historyPanel.Controls.Add(_conversationList, 0, 1);
        workspace.Panel1.Controls.Add(historyPanel);

        var conversationPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        conversationPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        conversationPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        conversationPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
        conversationPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        conversationPanel.Controls.Add(_conversationDetailsLabel, 0, 0);
        conversationPanel.Controls.Add(_transcriptScrollHost, 0, 1);
        conversationPanel.Controls.Add(_messageScrollHost, 0, 2);

        var composerActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0, 4, 0, 0),
            Padding = Padding.Empty
        };
        composerActions.Controls.Add(_sendButton);
        composerActions.Controls.Add(_cancelButton);
        conversationPanel.Controls.Add(composerActions, 0, 3);
        workspace.Panel2.Controls.Add(conversationPanel);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(targetRow, 0, 1);
        root.Controls.Add(workspace, 0, 2);
        root.Controls.Add(_statusLabel, 0, 3);

        return root;
    }

    private Label CreateSectionLabel(string text) =>
        new()
        {
            Dock = DockStyle.Fill,
            Font = new Font(Font, FontStyle.Bold),
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
        };

    private static Label CreateValueLabel() =>
        new()
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = false,
            Padding = new Padding(6, 4, 6, 4),
            TextAlign = ContentAlignment.MiddleLeft
        };

    private HiveButton CreateButton(string text, HiveButtonStyle style) =>
        new()
        {
            Text = text,
            Style = style,
            Margin = new Padding(0, 0, 6, 0),
            AutoSize = true
        };

    private bool IsUnavailable() =>
        _disposed || IsDisposed || Disposing;

    private static string FormatConversationDetails(DirectLlmConversationSummary summary)
    {
        var lastTarget = summary.LastExecutionTargetId?.ToString() ?? "not selected";
        var correlation = summary.ActiveCorrelationId?.ToString() ?? "none";
        var status = summary.LastErrorCode is { Length: > 0 } code
            ? $"{summary.Status} · {code}"
            : summary.Status.ToString();

        return $"{summary.Title}{Environment.NewLine}" +
               $"Status: {status} · Messages: {summary.MessageCount} · " +
               $"Last target: {lastTarget} · Active request: {correlation}";
    }

    private sealed record TargetOption(ExecutionTarget Target, string DisplayLabel)
    {
        public override string ToString() => DisplayLabel;
    }

    private sealed record ConversationOption(DirectLlmConversationSummary Conversation)
    {
        public override string ToString() =>
            $"{Conversation.Title}  ·  {Conversation.Status}  ·  " +
            $"{Conversation.UpdatedAtUtc.ToLocalTime():g}";
    }
}
