using Hive.Core;
using System.Drawing;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Host.WinForms;

internal sealed class DirectLlmConversationView : UserControl, IHiveWorkspaceRefreshable
{
    private readonly HiveWorkspaceContext _context;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveThemeManager _themeManager;
    private readonly HiveComboBox _executionTargetSelector;
    private readonly ListBox _conversationList;
    private readonly ChatTranscriptPanel _transcriptPanel;
    private readonly HiveScrollHost _transcriptScrollHost;
    private readonly TextBox _messageTextBox;
    private readonly HiveScrollHost _messageScrollHost;
    private readonly Label _conversationDetailsLabel;
    private readonly Label _statusLabel;
    private readonly HiveButton _refreshButton;
    private readonly HiveButton _newConversationButton;
    private readonly HiveButton _sendButton;
    private readonly HiveButton _cancelButton;
    private HiveCardPanel _historyCard = null!;
    private HiveCardPanel _transcriptCard = null!;
    private HiveCardPanel _composerCard = null!;
    private readonly CancellationTokenSource _lifetimeCts = new();

    private readonly List<DirectLlmTargetOption> _targets = [];
    private DirectLlmConversationSummary? _selectedConversation;
    private CancellationTokenSource? _operationCts;
    private bool _suppressConversationSelection;
    private bool _disposed;

    internal DirectLlmConversationView(HiveWorkspaceContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _accessContext = context.AccessContext;
        _themeManager = context.ThemeManager;

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = new Padding(8);
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
        _executionTargetSelector.DisplayMember = nameof(DirectLlmTargetOption.DisplayLabel);

        _conversationList = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            BorderStyle = BorderStyle.None,
            AccessibleName = "Direct LLM conversations",
            AccessibleDescription = "Select a saved conversation to open its history."
        };
        _conversationList.SelectedIndexChanged += ConversationSelectionChanged;

        _transcriptPanel = new ChatTranscriptPanel
        {
            AccessibleName = "Conversation transcript",
            AccessibleDescription = "Saved user messages and assistant replies for the selected conversation."
        };
        _transcriptScrollHost = new HiveScrollHost
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            AccessibleName = "Conversation transcript scroll area"
        };
        _transcriptScrollHost.Attach(_transcriptPanel);

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
        _statusLabel.Text = "Loading conversations and ExecutionTargets…";

        _refreshButton.Click += async (_, _) => await RefreshAsync();
        _newConversationButton.Click += async (_, _) =>
            await RunOperationAsync(CreateConversationAsync);
        _sendButton.Click += async (_, _) =>
            await RunOperationAsync(SendMessageAsync);
        _cancelButton.Click += (_, _) => CancelCurrentOperation();

        Controls.Add(BuildLayout());

        _themeManager.ThemeChanged += ThemeManagerOnChanged;
        _themeManager.Apply(this);
        _historyCard.ApplyTheme(_themeManager.Theme);
        _transcriptCard.ApplyTheme(_themeManager.Theme);
        _composerCard.ApplyTheme(_themeManager.Theme);
        _transcriptPanel.ApplyTheme(_themeManager);
        ApplyStatusVisual();
        UpdateActionState();
    }

    private IHiveManagementFacade Management => _context.Management;

    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        RunOperationAsync(RefreshCoreAsync, cancellationToken);

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
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

    private async Task RefreshCoreAsync(CancellationToken cancellationToken)
    {
        await RefreshTargetsAsync(cancellationToken).ConfigureAwait(true);
        if (IsUnavailable() || cancellationToken.IsCancellationRequested)
            return;

        await RefreshConversationsAsync(
            _selectedConversation?.Id,
            cancellationToken).ConfigureAwait(true);
    }

    private async Task RefreshTargetsAsync(CancellationToken cancellationToken)
    {
        _statusLabel.Text = "Loading ExecutionTargets…";

        var catalog = await DirectLlmTargetCatalog.LoadAsync(
            Management,
            _accessContext,
            cancellationToken).ConfigureAwait(true);

        if (IsUnavailable() || cancellationToken.IsCancellationRequested)
            return;

        if (catalog.IsFailure)
        {
            // Fail closed: never show an unfiltered list when favorites could not be read.
            ApplyTargets(Array.Empty<DirectLlmTargetOption>());
            ReportError(catalog.Error!, "ExecutionTargets could not be loaded.");
            return;
        }

        var value = catalog.Value!;
        ApplyTargets(value.Targets);
        _statusLabel.Text = value.DescribeStatus();
        UpdateActionState();
    }

    private void ApplyTargets(IReadOnlyList<DirectLlmTargetOption> targets)
    {
        var previouslySelectedTargetId =
            (_executionTargetSelector.SelectedItem as DirectLlmTargetOption)?.Target.Id;

        _targets.Clear();
        _targets.AddRange(targets);

        _executionTargetSelector.DataSource = null;
        _executionTargetSelector.DataSource = _targets;

        var selectedIndex = previouslySelectedTargetId is { } oldId
            ? _targets.FindIndex(target => target.Target.Id == oldId)
            : -1;

        _executionTargetSelector.SelectedIndex = selectedIndex >= 0
            ? selectedIndex
            : _targets.Count > 0 ? 0 : -1;
    }

    private async Task RefreshConversationsAsync(
        ConversationId? preferredConversationId,
        CancellationToken cancellationToken)
    {
        var conversationsResult = await Management.ListDirectLlmConversationsAsync(
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
            .Select(static conversation => new DirectLlmConversationOption(conversation))
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
            _transcriptPanel.SuspendLayout();
            _transcriptPanel.ClearMessages();
            _transcriptPanel.Size = Size.Empty;
            _transcriptPanel.ResumeLayout(true);
            _conversationDetailsLabel.Text = "No conversation selected.";
        }

        UpdateActionState();
    }

    private async Task CreateConversationAsync(CancellationToken cancellationToken)
    {
        var result = await Management.CreateDirectLlmConversationAsync(
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
        var target = _executionTargetSelector.SelectedItem as DirectLlmTargetOption;
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
            result = await Management.SendDirectLlmMessageAsync(
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
        var result = await Management.GetDirectLlmConversationAsync(
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
        _conversationDetailsLabel.Text =
            DirectLlmConversationPresentation.FormatDetails(conversation.Summary);

        _transcriptPanel.SuspendLayout();
        _transcriptPanel.ClearMessages();
        foreach (var message in conversation.Messages)
        {
            var isUser = message.Role == DirectLlmConversationMessageRole.User;
            var header = isUser
                ? $"You  �  {message.CreatedAtUtc.ToLocalTime():g}"
                : $"Assistant  �  {message.CreatedAtUtc.ToLocalTime():g}";
            _transcriptPanel.AddMessage(isUser, header, message.Content);
        }
        _transcriptPanel.ResumeLayout(true);
        _transcriptPanel.Size = Size.Empty;

        _transcriptPanel.ApplyTheme(_themeManager);
        _transcriptScrollHost.Synchronize();
        _transcriptScrollHost.VerticalScrollPosition = int.MaxValue;
        UpdateActionState();
    }

    private async Task ConversationSelectionChangedAsync(CancellationToken cancellationToken)
    {
        if (_suppressConversationSelection ||
            _conversationList.SelectedItem is not DirectLlmConversationOption option)
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
        Func<CancellationToken, Task> operation,
        CancellationToken externalToken = default)
    {
        if (_disposed || IsDisposed || Disposing ||
            Interlocked.CompareExchange(ref _operationCts, null, null) is not null)
        {
            return;
        }

        var operationCts = CancellationTokenSource.CreateLinkedTokenSource(
            _lifetimeCts.Token,
            externalToken);
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
                    _context.Output,
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
                              _executionTargetSelector.SelectedItem is DirectLlmTargetOption;
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

    private void ThemeManagerOnChanged(object? sender, EventArgs e)
    {
        _historyCard.ApplyTheme(_themeManager.Theme);
        _transcriptCard.ApplyTheme(_themeManager.Theme);
        _composerCard.ApplyTheme(_themeManager.Theme);
        _transcriptPanel.ApplyTheme(_themeManager);
        ApplyStatusVisual();
    }

    private void ApplyStatusVisual()
    {
        var palette = _themeManager.Theme.Palette;
        BackColor = palette.Surface;
        _statusLabel.ForeColor = palette.MutedText;
        _statusLabel.BackColor = palette.Surface;
        _conversationDetailsLabel.ForeColor = palette.Text;
        _conversationDetailsLabel.BackColor = palette.Surface;
        _conversationList.BackColor = palette.Surface;
        _conversationList.ForeColor = palette.Text;
        _messageTextBox.BackColor = palette.Surface;
        _messageTextBox.ForeColor = palette.Text;
        _transcriptPanel.ApplyTheme(_themeManager);
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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

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
            Font = new Font(_themeManager.Theme.Typography.FontFamily, 11.5f, FontStyle.Bold),
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
            Margin = new Padding(0, 2, 0, 4),
            Padding = Padding.Empty
        };
        targetRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
        targetRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        targetRow.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        targetRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var targetLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Execution target",
            Font = new Font(_themeManager.Theme.Typography.FontFamily, _themeManager.Theme.Typography.BodySize, FontStyle.Bold),
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
        _historyCard = new HiveCardPanel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        _historyCard.Controls.Add(_conversationList);
        historyPanel.Controls.Add(_historyCard, 0, 1);
        workspace.Panel1.Controls.Add(historyPanel);

        var conversationPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        conversationPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        conversationPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        conversationPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        conversationPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        conversationPanel.Controls.Add(_conversationDetailsLabel, 0, 0);
        _transcriptCard = new HiveCardPanel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        _transcriptCard.Controls.Add(_transcriptScrollHost);
        conversationPanel.Controls.Add(_transcriptCard, 0, 1);

        _composerCard = new HiveCardPanel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        _composerCard.Controls.Add(_messageScrollHost);
        conversationPanel.Controls.Add(_composerCard, 0, 2);

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
            Font = new Font(_themeManager.Theme.Typography.FontFamily, _themeManager.Theme.Typography.BodySize, FontStyle.Bold),
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
    private sealed class ChatTranscriptPanel : Panel
    {
        private readonly List<ChatBubble> _bubbles = new();
        private readonly Label _placeholder;

        public ChatTranscriptPanel()
        {
            Dock = DockStyle.None;
            AutoSize = false;
            Margin = Padding.Empty;
            Padding = new Padding(8);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = System.Drawing.Color.Transparent;

            _placeholder = new Label
            {
                Text = "No messages yet. Choose an ExecutionTarget and send a message.",
                AutoSize = true,
                Visible = true
            };
            Controls.Add(_placeholder);
        }

        public void ApplyTheme(IHiveThemeManager themeManager)
        {
            if (themeManager is null || _placeholder is null)
                return;

            BackColor = themeManager.Theme.Palette.WindowBackground;
            _placeholder.ForeColor = themeManager.Theme.Palette.MutedText;
            foreach (var bubble in _bubbles)
                bubble.ApplyTheme(themeManager);
        }

        public void ClearMessages()
        {
            foreach (var bubble in _bubbles)
                bubble.Dispose();
            _bubbles.Clear();
            Controls.Clear();
            Controls.Add(_placeholder);
            _placeholder.Visible = true;
            PerformLayout();
        }

        public void AddMessage(bool isUser, string header, string content)
        {
            _placeholder.Visible = false;
            var bubble = new ChatBubble(isUser, header, content);
            _bubbles.Add(bubble);
            Controls.Add(bubble);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (ClientSize.Width > 0)
                PerformLayout();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (ClientSize.Width <= 0)
                return;
            if (_placeholder is null)
                return;

            int y = Padding.Top;
            int bubbleMaxWidth = Math.Max(120, (int)(ClientSize.Width * 0.82));
            foreach (var bubble in _bubbles)
            {
                bubble.SetMaximumBubbleWidth(bubbleMaxWidth);
                bubble.PerformLayout();
                int x = bubble.IsUser
                    ? ClientSize.Width - bubble.Width - Padding.Right
                    : Padding.Left;
                if (x < Padding.Left)
                    x = Padding.Left;
                bubble.Location = new Point(x, y);
                y += bubble.Height + 6;
            }

            if (_bubbles.Count == 0)
            {
                _placeholder.MaximumSize = new Size(Math.Max(1, ClientSize.Width - Padding.Horizontal), 0);
                _placeholder.Location = new Point(Padding.Left, Padding.Top);
            }
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            if (_placeholder is null)
                return proposedSize.Width > 0 ? new Size(proposedSize.Width, 0) : new Size(320, 0);

            int width = proposedSize.Width > 0
                ? proposedSize.Width
                : (Width > 0 ? Width : 320);

            int y = Padding.Top;
            int bubbleMaxWidth = Math.Max(120, (int)(width * 0.82));
            foreach (var bubble in _bubbles)
            {
                bubble.SetMaximumBubbleWidth(bubbleMaxWidth);
                var size = bubble.GetPreferredSize(new Size(bubbleMaxWidth, 0));
                y += size.Height + 6;
            }

            if (_bubbles.Count == 0)
            {
                _placeholder.MaximumSize = new Size(Math.Max(1, width - Padding.Horizontal), 0);
                y += _placeholder.GetPreferredSize(new Size(Math.Max(1, width - Padding.Horizontal), 0)).Height + 10;
            }

            y += 4;
            return new Size(width, Math.Max(Padding.Vertical, y));
        }
    }

    private sealed class ChatBubble : TableLayoutPanel
    {
        public bool IsUser { get; }

        private readonly Label _headerLabel;
        private readonly Label _contentLabel;

        public ChatBubble(bool isUser, string header, string content)
        {
            IsUser = isUser;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            ColumnCount = 1;
            RowCount = 2;
            Margin = Padding.Empty;
            Padding = new Padding(10, 6, 10, 6);

            RowStyles.Add(new RowStyle(SizeType.AutoSize));
            RowStyles.Add(new RowStyle(SizeType.AutoSize));
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            _headerLabel = new Label
            {
                Text = header,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 2)
            };
            _contentLabel = new Label
            {
                Text = content,
                AutoSize = true
            };

            Controls.Add(_headerLabel, 0, 0);
            Controls.Add(_contentLabel, 0, 1);
        }

        public void SetMaximumBubbleWidth(int maxWidth)
        {
            MaximumSize = new Size(Math.Max(1, maxWidth), 0);
            int innerWidth = Math.Max(1, maxWidth - Padding.Horizontal);
            _headerLabel.MaximumSize = new Size(innerWidth, 0);
            _contentLabel.MaximumSize = new Size(innerWidth, 0);
        }

        public void ApplyTheme(IHiveThemeManager themeManager)
        {
            if (themeManager is null)
                return;

            var palette = themeManager.Theme.Palette;
            BackColor = IsUser ? palette.Accent : palette.ElevatedSurface;
            _headerLabel.ForeColor = IsUser ? palette.AccentForeground : palette.MutedText;
            _contentLabel.ForeColor = IsUser ? palette.AccentForeground : palette.Text;
        }
    }
}



