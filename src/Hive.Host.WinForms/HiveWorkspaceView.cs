using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;

namespace Hive.Host.WinForms;

public sealed class HiveWorkspaceView : UserControl
{
    private readonly IHiveManagementFacade _management;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveThemeManager _themeManager;

    private readonly HiveListView _workItems;
    private readonly Label _statusLabel;
    private readonly Label _attachmentLabel;
    private readonly Label _executionLabel;
    private readonly ListBox _activityList;
    private readonly HiveButton _refreshButton;
    private readonly HiveButton _addImageButton;
    private readonly HiveButton _requestApprovalButton;
    private readonly HiveButton _approveButton;
    private readonly HiveButton _rejectButton;

    private WorkItem? _selectedWorkItem;
    private CancellationTokenSource? _operationCts;
    private bool _suppressSelectionChanged;

    public HiveWorkspaceView(
        IHiveManagementFacade management,
        ResourceAccessContext accessContext,
        IHiveThemeManager themeManager)
    {
        _management = management ?? throw new ArgumentNullException(nameof(management));
        _accessContext = accessContext ?? throw new ArgumentNullException(nameof(accessContext));
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = new Padding(16);
        MinimumSize = new Size(760, 520);

        _refreshButton = CreateButton("Refresh", HiveButtonStyle.Secondary);
        _addImageButton = CreateButton("Add image", HiveButtonStyle.Primary);
        _requestApprovalButton = CreateButton("Request approval", HiveButtonStyle.Secondary);
        _approveButton = CreateButton("Approve", HiveButtonStyle.Primary);
        _rejectButton = CreateButton("Reject", HiveButtonStyle.Danger);

        _refreshButton.Click += async (_, _) => await RefreshAsync();
        _addImageButton.Click += async (_, _) => await AddImageAsync();
        _requestApprovalButton.Click += async (_, _) => await RequestApprovalAsync();
        _approveButton.Click += async (_, _) => await ApproveAsync();
        _rejectButton.Click += async (_, _) => await RejectAsync();

        _workItems = new HiveListView
        {
            Dock = DockStyle.Fill,
            AccessibleName = "Workspace WorkItems",
            AccessibleRole = AccessibleRole.Table
        };
        _workItems.Columns.Add("WorkItem", 250);
        _workItems.Columns.Add("Status", 130);
        _workItems.Columns.Add("Version", 80);
        _workItems.Columns.Add("Attachment", 180);
        _workItems.SelectedIndexChanged += async (_, _) => await SelectCurrentWorkItemAsync();

        _statusLabel = CreateValueLabel();
        _attachmentLabel = CreateValueLabel();
        _executionLabel = CreateValueLabel();

        _activityList = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            BorderStyle = BorderStyle.None,
            HorizontalScrollbar = false
        };

        Controls.Add(BuildLayout());

        _themeManager.Apply(this);
        UpdateActionState();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _operationCts?.Cancel();
            _operationCts?.Dispose();
        }

        base.Dispose(disposing);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await RunOperationAsync(
            RefreshCoreAsync,
            cancellationToken);
    }

    private async Task RefreshCoreAsync(CancellationToken token)
    {
        var result = await _management.ListWorkItemsAsync(
            _accessContext,
            cancellationToken: token).ConfigureAwait(true);

        if (result.IsFailure)
        {
            ShowError(result.Error!);
            return;
        }

        _suppressSelectionChanged = true;
        _workItems.BeginUpdate();
        try
        {
            _workItems.Items.Clear();

            foreach (var item in result.Value!)
            {
                var attachment = item.Attachment is null
                    ? "None"
                    : $"{item.Attachment.FileName} ({FormatSize(item.Attachment.ContentLength)})";

                var row = new ListViewItem(item.Id.ToString());
                row.SubItems.Add(item.Status.ToString());
                row.SubItems.Add(item.Resource.Version.ToString());
                row.SubItems.Add(attachment);
                row.Tag = item;
                _workItems.Items.Add(row);
            }
        }
        finally
        {
            _workItems.EndUpdate();
            _suppressSelectionChanged = false;
        }

        _selectedWorkItem = FindPreviouslySelected(
            result.Value!,
            _selectedWorkItem?.Id);

        if (_selectedWorkItem is null)
        {
            ClearDetails();
        }
        else
        {
            await LoadActivityAsync(
                _selectedWorkItem,
                token).ConfigureAwait(true);
        }

        UpdateActionState();
    }

    private async Task AddImageAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select image",
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All files|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
            return;

        await RunOperationAsync(
            async token =>
            {
                var mediaType = GetMediaType(dialog.FileName);
                if (mediaType is null)
                {
                    ShowError(
                        Error.Validation(
                            "hive.workspace.image-type-unsupported",
                            "The selected file type is not supported as a V1 image."));
                    return;
                }

                var content = await File.ReadAllBytesAsync(
                    dialog.FileName,
                    token).ConfigureAwait(true);

                var result = await _management.CreateImageWorkItemAsync(
                    new WorkItemImageSubmission(
                        Path.GetFileName(dialog.FileName),
                        mediaType,
                        content),
                    _accessContext,
                    token).ConfigureAwait(true);

                if (result.IsFailure)
                {
                    ShowError(result.Error!);
                    return;
                }

                await RefreshCoreAsync(token).ConfigureAwait(true);
                SelectWorkItem(result.Value!.Id);

                if (_selectedWorkItem is not null)
                {
                    await LoadActivityAsync(
                        _selectedWorkItem,
                        token).ConfigureAwait(true);
                }
            });
    }

    private async Task RequestApprovalAsync()
    {
        if (_selectedWorkItem is null)
            return;

        await RunOperationAsync(
            async token =>
            {
                var result = await _management.RequestWorkItemApprovalAsync(
                    _selectedWorkItem.Id,
                    _selectedWorkItem.Resource.Version,
                    _accessContext,
                    token).ConfigureAwait(true);

                await HandleTransitionResultAsync(
                    result,
                    "Approval requested",
                    token).ConfigureAwait(true);
            });
    }

    private async Task ApproveAsync()
    {
        if (_selectedWorkItem is null)
            return;

        var question = HiveMessageBox.ShowQuestion(
            FindForm(),
            "Approve this WorkItem?",
            "Approve WorkItem",
            MessageBoxButtons.YesNo,
            _themeManager);

        if (question != DialogResult.Yes)
            return;

        await RunOperationAsync(
            async token =>
            {
                var result = await _management.ApproveWorkItemAsync(
                    _selectedWorkItem.Id,
                    _selectedWorkItem.Resource.Version,
                    _accessContext,
                    token).ConfigureAwait(true);

                await HandleTransitionResultAsync(
                    result,
                    "WorkItem approved",
                    token).ConfigureAwait(true);
            });
    }

    private async Task RejectAsync()
    {
        if (_selectedWorkItem is null)
            return;

        var reason = PromptRejectionReason();
        if (reason is null)
            return;

        await RunOperationAsync(
            async token =>
            {
                var result = await _management.RejectWorkItemAsync(
                    _selectedWorkItem.Id,
                    _selectedWorkItem.Resource.Version,
                    _accessContext,
                    reason,
                    token).ConfigureAwait(true);

                await HandleTransitionResultAsync(
                    result,
                    "WorkItem rejected",
                    token).ConfigureAwait(true);
            });
    }

    private async Task HandleTransitionResultAsync(
        Result<WorkItem> result,
        string successTitle,
        CancellationToken cancellationToken)
    {
        if (result.IsFailure)
        {
            ShowError(result.Error!);
            await RefreshCoreAsync(cancellationToken).ConfigureAwait(true);
            return;
        }

        await RefreshCoreAsync(cancellationToken).ConfigureAwait(true);
        HiveMessageBox.ShowSuccess(
            FindForm(),
            $"{successTitle}.",
            successTitle,
            _themeManager);
    }

    private async Task SelectCurrentWorkItemAsync()
    {
        if (_suppressSelectionChanged)
            return;

        var item = _workItems.SelectedItems.Count == 0
            ? null
            : _workItems.SelectedItems[0].Tag as WorkItem;

        _selectedWorkItem = item;
        UpdateDetails();

        if (item is null)
        {
            _activityList.Items.Clear();
            UpdateActionState();
            return;
        }

        await RunOperationAsync(
            token => LoadActivityAsync(item, token));
    }

    private async Task LoadActivityAsync(
        WorkItem workItem,
        CancellationToken cancellationToken)
    {
        _statusLabel.Text = workItem.Status.ToString();
        ApplyStatusVisual();
        _attachmentLabel.Text = workItem.Attachment is null
            ? "No attachment"
            : $"{workItem.Attachment.FileName} · {FormatSize(workItem.Attachment.ContentLength)} · {workItem.Attachment.MediaType}";
        _executionLabel.Text =
            "No execution/provider activity has been created for this WorkItem yet.";

        var activity = await _management.GetWorkItemActivityAsync(
            workItem.Id,
            _accessContext,
            cancellationToken).ConfigureAwait(true);

        if (activity.IsFailure)
        {
            ShowError(activity.Error!);
            return;
        }

        _activityList.BeginUpdate();
        try
        {
            _activityList.Items.Clear();

            foreach (var entry in activity.Value!)
            {
                _activityList.Items.Add(
                    $"{entry.OccurredAtUtc:yyyy-MM-dd HH:mm:ss}  ·  v{entry.Version}  ·  {entry.Message}");
            }
        }
        finally
        {
            _activityList.EndUpdate();
        }

        UpdateActionState();
    }

    private void UpdateDetails()
    {
        if (_selectedWorkItem is null)
        {
            ClearDetails();
            return;
        }

        _statusLabel.Text = $"{_selectedWorkItem.Status} · version {_selectedWorkItem.Resource.Version}";
        _attachmentLabel.Text = _selectedWorkItem.Attachment is null
            ? "No attachment"
            : $"{_selectedWorkItem.Attachment.FileName} · {FormatSize(_selectedWorkItem.Attachment.ContentLength)} · {_selectedWorkItem.Attachment.MediaType}";
        _executionLabel.Text =
            "No execution/provider activity has been created for this WorkItem yet.";
    }

    private void ClearDetails()
    {
        _statusLabel.Text = "No WorkItem selected";
        _attachmentLabel.Text = "—";
        _executionLabel.Text = "—";
        _activityList.Items.Clear();
    }

    private void ApplyStatusVisual()
    {
        var status = _selectedWorkItem?.Status;

        _statusLabel.ForeColor = status switch
        {
            WorkItemStatus.Completed => _themeManager.Theme.VisualStates.Success,
            WorkItemStatus.PendingApproval => _themeManager.Theme.VisualStates.Warning,
            WorkItemStatus.Rejected => _themeManager.Theme.VisualStates.Error,
            WorkItemStatus.Failed => _themeManager.Theme.VisualStates.Error,
            WorkItemStatus.Cancelled => _themeManager.Theme.VisualStates.Warning,
            WorkItemStatus.Created or
            WorkItemStatus.Queued or
            WorkItemStatus.Running => _themeManager.Theme.VisualStates.Information,
            _ => _themeManager.Theme.Palette.MutedText
        };
    }

    private void UpdateActionState()
    {
        var status = _selectedWorkItem?.Status;

        _requestApprovalButton.Enabled =
            status is WorkItemStatus.Created
                or WorkItemStatus.Queued
                or WorkItemStatus.Running;

        _approveButton.Enabled = status == WorkItemStatus.PendingApproval;
        _rejectButton.Enabled = status == WorkItemStatus.PendingApproval;
    }

    private async Task RunOperationAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken externalToken = default)
    {
        _operationCts?.Cancel();
        _operationCts?.Dispose();
        _operationCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);

        SetBusy(true);

        try
        {
            await operation(_operationCts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            ShowError(
                new Error(
                    "hive.workspace.operation-failed",
                    ErrorCategory.Internal,
                    exception.Message));
        }
        finally
        {
            if (!IsDisposed)
                SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _refreshButton.Enabled = !busy;
        _addImageButton.Enabled = !busy;
        _requestApprovalButton.Enabled = !busy && _requestApprovalButton.Enabled;
        _approveButton.Enabled = !busy && _approveButton.Enabled;
        _rejectButton.Enabled = !busy && _rejectButton.Enabled;

        if (busy)
            Cursor = Cursors.WaitCursor;
        else
            Cursor = Cursors.Default;

        if (!busy)
            UpdateActionState();
    }

    private void ShowError(Error error)
    {
        HiveMessageBox.ShowError(
            FindForm(),
            $"{error.Code} [{error.Category}]\r\n{error.Message}",
            "Workspace operation failed",
            _themeManager);
    }

    private void SelectWorkItem(WorkItemId id)
    {
        _suppressSelectionChanged = true;
        try
        {
            foreach (ListViewItem item in _workItems.Items)
            {
                if (item.Tag is WorkItem workItem &&
                    workItem.Id == id)
                {
                    item.Selected = true;
                    item.Focused = true;
                    item.EnsureVisible();
                    _selectedWorkItem = workItem;
                    UpdateDetails();
                    break;
                }
            }
        }
        finally
        {
            _suppressSelectionChanged = false;
        }
    }

    private WorkItem? FindPreviouslySelected(
        IReadOnlyList<WorkItem> items,
        WorkItemId? id) =>
        id is null
            ? null
            : items.FirstOrDefault(item => item.Id == id.Value);

    private HiveButton CreateButton(
        string text,
        HiveButtonStyle style) =>
        new()
        {
            Text = text,
            Style = style,
            Margin = new Padding(0, 0, 8, 0)
        };

    private static Label CreateValueLabel() =>
        new()
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 4, 0)
        };

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 42));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(0, 0, 0, 8)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var title = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Workspace",
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
        header.Controls.Add(title, 0, 0);
        header.Controls.Add(_refreshButton, 1, 0);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        actions.Controls.Add(_addImageButton);
        actions.Controls.Add(_requestApprovalButton);
        actions.Controls.Add(_approveButton);
        actions.Controls.Add(_rejectButton);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            FixedPanel = FixedPanel.Panel2,
            Panel2MinSize = 0
        };
        split.Panel1.Controls.Add(_workItems);
        split.Panel2.Controls.Add(BuildDetailsPanel());

        split.SizeChanged += (_, _) =>
        {
            var availableWidth = split.ClientSize.Width;
            if (availableWidth <= 0)
                return;

            var desiredPanel2MinSize = Math.Min(
                300,
                Math.Max(0, availableWidth - split.Panel1MinSize - split.SplitterWidth));

            var desiredSplitterDistance = Math.Max(
                split.Panel1MinSize,
                availableWidth - desiredPanel2MinSize - split.SplitterWidth);

            if (split.Panel2MinSize < desiredPanel2MinSize
                && split.SplitterDistance > desiredSplitterDistance)
            {
                split.SplitterDistance = desiredSplitterDistance;
            }

            split.Panel2MinSize = desiredPanel2MinSize;
            split.SplitterDistance = desiredSplitterDistance;
        };

        var activityHeader = new Label
        {
            Dock = DockStyle.Top,
            Height = 32,
            Text = "Activity / notifications",
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 0, 0)
        };

        var activityPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 8, 0, 0)
        };
        activityPanel.Controls.Add(_activityList);
        activityPanel.Controls.Add(activityHeader);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(actions, 0, 1);
        root.Controls.Add(split, 0, 2);
        root.Controls.Add(activityPanel, 0, 3);

        return root;
    }

    private Control BuildDetailsPanel()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(12, 0, 0, 0)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(CreateSectionLabel("Status"), 0, 0);
        root.Controls.Add(_statusLabel, 0, 1);
        root.Controls.Add(CreateSectionLabel("Attachment"), 0, 2);
        root.Controls.Add(_attachmentLabel, 0, 3);
        root.Controls.Add(CreateSectionLabel("Execution / provider"), 0, 4);
        root.Controls.Add(_executionLabel, 0, 5);

        var hint = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            Text = "Phase 1.11 owns WorkItem operations and approval state. Actual execution/provider processing is introduced by later V1 pipeline slices.",
            Padding = new Padding(4, 10, 4, 4)
        };
        root.Controls.Add(hint, 0, 6);

        return root;
    }

    private Label CreateSectionLabel(string text) =>
        new()
        {
            Dock = DockStyle.Fill,
            Text = text,
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.BottomLeft
        };

    private string? PromptRejectionReason()
    {
        using var dialog = new HiveRejectionReasonDialog(_themeManager);

        return dialog.ShowDialog(FindForm()) == DialogResult.OK
            ? dialog.Reason
            : null;
    }

    private sealed class HiveRejectionReasonDialog : HiveForm
    {
        private readonly TextBox _reasonTextBox;

        public HiveRejectionReasonDialog(IHiveThemeManager themeManager)
            : base(
                "Reject WorkItem",
                "Provide the reason recorded with this rejection.",
                new Size(620, 430),
                new Size(520, 360),
                themeManager)
        {
            ConfigureHeader(
                allowMove: true,
                allowClose: true,
                allowMinimize: false,
                allowMaximize: false,
                allowHelp: false,
                allowThemeToggle: true);

            SetBodyPadding(new Padding(20));

            var editor = new HiveEditorLayout();
            _reasonTextBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                MaxLength = 2000,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                AcceptsReturn = true
            };

            editor.AddField(
                "Reason",
                "This text is stored as the rejection reason. Leaving it empty preserves the existing behavior and submits an empty reason.",
                _reasonTextBox,
                150);

            var rejectButton = editor.AddActionButton(
                "Reject",
                HiveButtonStyle.Danger,
                96);
            var cancelButton = editor.AddActionButton(
                "Cancel",
                HiveButtonStyle.Secondary,
                96);

            rejectButton.Click += (_, _) =>
            {
                Reason = _reasonTextBox.Text.Trim();
                DialogResult = DialogResult.OK;
                Close();
            };

            cancelButton.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            AcceptButton = rejectButton;
            CancelButton = cancelButton;

            BodyPanel.Controls.Add(editor);
            ThemeManager.Apply(BodyPanel);

            _reasonTextBox.Select();
        }

        public string Reason { get; private set; } = string.Empty;
    }

    private static string? GetMediaType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".bmp" => "image/bmp",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => null
        };

    private static string FormatSize(long bytes) =>
        bytes switch
        {
            >= 1024 * 1024 => $"{bytes / (1024d * 1024d):0.0} MB",
            >= 1024 => $"{bytes / 1024d:0.0} KB",
            _ => $"{bytes} B"
        };
}
