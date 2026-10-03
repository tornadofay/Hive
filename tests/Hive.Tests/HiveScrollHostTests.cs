using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Xunit;

namespace Hive.Tests;

public sealed class HiveScrollHostTests
{
    private const int ObjIdHScroll = -6;
    private const int ObjIdVScroll = -5;
    private const uint StateSystemInvisible = 0x00008000;
    private const uint StateSystemOffscreen = 0x00010000;

    [Fact]
    public void Synchronize_ExposesNormalizedHorizontalAndVerticalState()
    {
        using var host = new HiveScrollHost
        {
            Size = new Size(200, 200)
        };
        using var content = new Panel
        {
            Size = new Size(500, 600)
        };

        host.Attach(content);

        Assert.Equal(300, host.HorizontalScrollState.EffectiveMaximum);
        Assert.Equal(400, host.VerticalScrollState.EffectiveMaximum);
        Assert.True(host.HorizontalScrollState.CanScroll);
        Assert.True(host.VerticalScrollState.CanScroll);
        Assert.True(host.HorizontalScrollBarForTesting.Visible);
        Assert.True(host.VerticalScrollBarForTesting.Visible);
    }

    [Fact]
    public void ZeroSizedHost_DoesNotThrowDuringSynchronization()
    {
        using var host = new HiveScrollHost
        {
            Size = new Size(1, 1)
        };
        using var content = new Panel
        {
            Size = new Size(500, 600)
        };

        host.Attach(content);

        host.SetBounds(0, 0, 0, 0);
        host.Synchronize();

        Assert.Equal(0, host.HorizontalScrollState.ViewportSize);
        Assert.Equal(0, host.VerticalScrollState.ViewportSize);
    }

    [Fact]
    public void NonScrollableContent_HasNoActiveScrollbar()
    {
        using var host = new HiveScrollHost
        {
            Size = new Size(300, 240)
        };
        using var content = new Panel
        {
            Size = new Size(100, 120)
        };

        host.Attach(content);

        Assert.False(host.HorizontalScrollState.CanScroll);
        Assert.False(host.VerticalScrollState.CanScroll);
        Assert.False(host.HorizontalScrollBarForTesting.Visible);
        Assert.False(host.VerticalScrollBarForTesting.Visible);
        Assert.Equal(Point.Empty, content.Location);
    }

    [Fact]
    public void SetScrollPosition_MovesAttachedContentAndReportsOneLogicalChange()
    {
        using var host = new HiveScrollHost
        {
            Size = new Size(200, 200)
        };
        using var content = new Panel
        {
            Size = new Size(500, 600)
        };

        host.Attach(content);

        var changes = 0;
        host.ScrollPositionChanged += (_, _) => changes++;

        host.SetScrollPosition(
            horizontal: 125,
            vertical: 250);

        Assert.Equal(
            new Point(-125, -250),
            content.Location);
        Assert.Equal(125, host.HorizontalScrollPosition);
        Assert.Equal(250, host.VerticalScrollPosition);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void NativeContent_RecoversAfterTransientZeroViewportBeforeScrollStateAcquisition()
    {
        using var form = new Form
        {
            Size = new Size(500, 400)
        };
        using var host = new HiveScrollHost
        {
            Size = new Size(500, 400)
        };
        using var textBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical
        };

        textBox.Text = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, 160)
                .Select(index => $"Line {index:000}"));

        form.Controls.Add(host);
        host.Attach(textBox);
        form.Show();
        Application.DoEvents();

        host.SetBounds(0, 0, 0, 0);
        host.Synchronize();

        host.SetBounds(0, 0, 500, 400);
        Application.DoEvents();
        host.Synchronize();

        Assert.True(host.VerticalScrollState.CanScroll);
        Assert.True(host.VerticalScrollBarForTesting.Visible);
    }

    [Fact]
    public void NativeTextBoxContent_UsesHiveScrollBars()
    {
        using var form = new Form
        {
            Size = new Size(500, 400)
        };
        using var host = new HiveScrollHost
        {
            Size = new Size(500, 400)
        };
        using var textBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false
        };

        textBox.Text = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, 160)
                .Select(index => $"Line {index:000} " + new string('x', 180)));

        form.Controls.Add(host);
        host.Attach(textBox);
        form.Show();
        Application.DoEvents();
        host.Synchronize();

        Assert.Same(textBox, host.Content);
        Assert.True(host.VerticalScrollState.CanScroll);
        Assert.True(host.HorizontalScrollState.CanScroll);
        Assert.True(host.VerticalScrollBarForTesting.Visible);
        Assert.True(host.HorizontalScrollBarForTesting.Visible);
        Assert.Equal(Point.Empty, textBox.Location);

        host.SetScrollPosition(40, 80);

        Assert.True(host.VerticalScrollPosition > 0);
        Assert.True(host.HorizontalScrollPosition > 0);
    }

    [Fact]
    public void NativeTextBox_MouseWheelScrollsHivePosition()
    {
        using var form = new Form
        {
            Size = new Size(500, 400)
        };
        using var host = new HiveScrollHost
        {
            Size = new Size(500, 400)
        };
        using var textBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical
        };

        textBox.Text = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, 160)
                .Select(index => $"Line {index:000}"));

        form.Controls.Add(host);
        host.Attach(textBox);
        form.Show();
        Application.DoEvents();
        host.Synchronize();

        Assert.True(host.VerticalScrollState.CanScroll);
        Assert.Equal(0, host.VerticalScrollPosition);

        SendMouseWheel(textBox.Handle, -120);
        Application.DoEvents();
        host.Synchronize();

        Assert.True(host.VerticalScrollPosition > 0);
    }

    [Fact]
    public void NativeTreeViewContent_UsesHiveScrollBars()
    {
        using var form = new Form
        {
            Size = new Size(500, 400)
        };
        using var host = new HiveScrollHost
        {
            Dock = DockStyle.Fill
        };
        using var tree = new TreeView
        {
            Dock = DockStyle.Fill,
            HideSelection = false
        };

        var root = new TreeNode("Root");
        for (var index = 1; index <= 160; index++)
            root.Nodes.Add($"Child {index:000}");
        tree.Nodes.Add(root);
        root.Expand();

        form.Controls.Add(host);
        host.Attach(tree);
        form.Show();
        Application.DoEvents();
        host.Synchronize();

        Assert.Same(tree, host.Content);
        Assert.True(host.VerticalScrollState.CanScroll);
        Assert.True(host.VerticalScrollBarForTesting.Visible);
        Assert.Equal(Point.Empty, tree.Location);

        var maximum = host.VerticalScrollState.EffectiveMaximum;
        Assert.True(maximum > 0);

        host.SetScrollPosition(0, maximum);

        Assert.True(host.VerticalScrollPosition > 0);
    }

    [Fact]
    public void NativeTreeView_MouseWheelScrollsHivePosition()
    {
        using var form = new Form
        {
            Size = new Size(500, 400)
        };
        using var host = new HiveScrollHost
        {
            Size = new Size(500, 400)
        };
        using var tree = new TreeView();

        var root = new TreeNode("Root");
        for (var index = 1; index <= 160; index++)
            root.Nodes.Add($"Child {index:000}");
        tree.Nodes.Add(root);
        root.Expand();

        form.Controls.Add(host);
        host.Attach(tree);
        form.Show();
        Application.DoEvents();
        host.Synchronize();

        Assert.True(host.VerticalScrollState.CanScroll);
        Assert.Equal(0, host.VerticalScrollPosition);

        SendMouseWheel(tree.Handle, -120);
        Application.DoEvents();
        host.Synchronize();

        Assert.True(host.VerticalScrollPosition > 0);
    }

    [Fact]
    public void NativeListViewContent_UsesHiveScrollBars()
    {
        using var form = new Form
        {
            Size = new Size(500, 400)
        };
        using var host = new HiveScrollHost
        {
            Dock = DockStyle.Fill
        };
        using var list = new HiveListView
        {
            View = View.Details,
            HideSelection = false
        };

        list.Columns.Add("Name", 800);
        for (var index = 1; index <= 160; index++)
            list.Items.Add($"Item {index:000}");

        form.Controls.Add(host);
        host.Attach(list);
        form.Show();
        Application.DoEvents();
        host.Synchronize();

        Assert.Same(list, host.Content);
        Assert.True(host.VerticalScrollState.CanScroll);
        Assert.True(host.HorizontalScrollState.CanScroll);
        Assert.True(host.VerticalScrollBarForTesting.Visible);
        Assert.True(host.HorizontalScrollBarForTesting.Visible);
        Assert.Equal(Point.Empty, list.Location);

        var lineHeight = Math.Max(1, list.GetItemRect(0).Height);
        var targetVertical = Math.Min(
            host.VerticalScrollState.EffectiveMaximum,
            lineHeight * 2);

        host.SetScrollPosition(
            host.HorizontalScrollState.EffectiveMaximum,
            targetVertical);

        Assert.True(host.VerticalScrollPosition > 0);
        Assert.True(host.VerticalScrollPosition % lineHeight == 0);
        Assert.True(host.HorizontalScrollPosition > 0);
        Assert.True(list.TopItem?.Index > 0);
    }

    [WinFormsFact]
    public void NativeListView_HidesNativeScrollBarsAcrossMaximizeAndScrolling()
    {
        using var form = new Form
        {
            Size = new Size(900, 620)
        };
        using var host = new HiveScrollHost
        {
            Dock = DockStyle.Fill
        };
        using var list = new HiveListView
        {
            View = View.Details,
            HideSelection = false
        };

        list.Columns.Add("Name", 800);
        for (var index = 1; index <= 160; index++)
            list.Items.Add($"Item {index:000}");

        form.Controls.Add(host);
        host.Attach(list);
        form.Show();

        // The ListView handle is created during showing. Suppression must already
        // be armed before this lifecycle so native scrollbars never become the
        // first-paint presentation.
        AssertNativeListViewScrollBarsHidden(list);

        Application.DoEvents();
        host.Synchronize();

        AssertNativeListViewScrollBarsHidden(list);

        form.WindowState = FormWindowState.Maximized;
        Application.DoEvents();
        host.Synchronize();

        AssertNativeListViewScrollBarsHidden(list);

        var lineHeight = Math.Max(1, list.GetItemRect(0).Height);
        for (var row = 1; row <= 8; row++)
        {
            host.SetScrollPosition(
                host.HorizontalScrollPosition,
                Math.Min(
                    host.VerticalScrollState.EffectiveMaximum,
                    row * lineHeight));
        }

        Application.DoEvents();

        Assert.True(host.VerticalScrollPosition > 0);
        Assert.True(list.TopItem?.Index > 0);
        AssertNativeListViewScrollBarsHidden(list);
    }

    [Fact]
    public void HiveCrudPage_UsesHiveScrollHostForList()
    {
        using var page = new HiveCrudPage<object>();

        Assert.Same(page.ListView, page.ListScrollHostForTesting.Content);
        Assert.IsType<HiveScrollHost>(page.ListScrollHostForTesting);
    }

    private static void AssertNativeListViewScrollBarsHidden(
        Control control)
    {
        AssertScrollBarHidden(control, ObjIdHScroll);
        AssertScrollBarHidden(control, ObjIdVScroll);
    }

    private static void AssertScrollBarHidden(
        Control control,
        int objectId)
    {
        var info = new NativeScrollBarInfo
        {
            cbSize = Marshal.SizeOf<NativeScrollBarInfo>(),
            States = new uint[6]
        };

        var available = GetScrollBarInfo(
            control.Handle,
            objectId,
            ref info);

        Assert.True(
            available,
            $"GetScrollBarInfo failed for native scrollbar object {objectId}.");

        var state = info.States[0];
        Assert.True(
            (state & StateSystemInvisible) != 0 ||
            (state & StateSystemOffscreen) != 0,
            $"Native scrollbar object {objectId} is still exposed; state=0x{state:X8}.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeScrollBarInfo
    {
        public int cbSize;
        public NativeRect ScrollBar;
        public int LineButton;
        public int ThumbTop;
        public int ThumbBottom;
        public int Reserved;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public uint[] States;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetScrollBarInfo(
        IntPtr handle,
        int objectId,
        ref NativeScrollBarInfo info);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(
        IntPtr hWnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam);

    private static void SendMouseWheel(
        IntPtr handle,
        int delta)
    {
        var wParam = new IntPtr(
            (long)(ushort)delta << 16);

        SendMessage(
            handle,
            0x020A,
            wParam,
            IntPtr.Zero);
    }

    [Fact]
    public void Synchronize_AfterResizeClampsExistingPositionToNewBounds()
    {
        using var host = new HiveScrollHost
        {
            Size = new Size(200, 200)
        };
        using var content = new Panel
        {
            Size = new Size(500, 600)
        };

        host.Attach(content);
        host.SetScrollPosition(250, 350);

        host.Size = new Size(400, 500);
        host.Synchronize();

        Assert.Equal(100, host.HorizontalScrollPosition);
        Assert.Equal(100, host.VerticalScrollPosition);
        Assert.Equal(new Point(-100, -100), content.Location);
    }

    [Fact]
    public void Synchronize_AfterContentResizeUpdatesBothScrollStates()
    {
        using var host = new HiveScrollHost
        {
            Size = new Size(200, 200)
        };
        using var content = new Panel
        {
            Size = new Size(250, 250)
        };

        host.Attach(content);

        content.Size = new Size(700, 800);
        host.Synchronize();

        Assert.Equal(500, host.HorizontalScrollState.EffectiveMaximum);
        Assert.Equal(600, host.VerticalScrollState.EffectiveMaximum);
        Assert.True(host.HorizontalScrollState.CanScroll);
        Assert.True(host.VerticalScrollState.CanScroll);
    }

    [Fact]
    public void ThemeApplication_PreservesCurrentScrollPosition()
    {
        using var host = new HiveScrollHost
        {
            Size = new Size(200, 200)
        };
        using var content = new Panel
        {
            Size = new Size(500, 600)
        };

        host.Attach(content);
        host.SetScrollPosition(80, 140);

        var themeManager = new HiveThemeManager(HiveThemeMode.Dark);

        themeManager.Apply(host);

        Assert.Equal(80, host.HorizontalScrollPosition);
        Assert.Equal(140, host.VerticalScrollPosition);
        Assert.Equal(new Point(-80, -140), content.Location);
    }

    [Fact]
    public void Attach_RejectsContentThatAlreadyBelongsToAnotherParent()
    {
        using var parent = new Panel();
        using var content = new Panel();
        using var host = new HiveScrollHost();

        parent.Controls.Add(content);

        var exception = Assert.Throws<InvalidOperationException>(
            () => host.Attach(content));

        Assert.Contains(
            "must not already have a parent",
            exception.Message,
            StringComparison.Ordinal);
        Assert.Same(parent, content.Parent);
    }

    [Fact]
    public void Attach_DisablesNativeAutoScrollAndDetachRestoresIt()
    {
        using var host = new HiveScrollHost
        {
            Size = new Size(200, 200)
        };
        using var content = new Panel
        {
            Size = new Size(500, 600),
            AutoScroll = true
        };

        host.Attach(content);

        Assert.False(content.AutoScroll);

        host.Detach();

        Assert.True(content.AutoScroll);
    }

    [Fact]
    public void Detach_RestoresCallerLayoutAndLeavesContentOwnedByCaller()
    {
        using var host = new HiveScrollHost
        {
            Size = new Size(200, 200)
        };
        using var content = new Panel
        {
            Dock = DockStyle.Fill,
            Anchor = AnchorStyles.Left |
                     AnchorStyles.Right |
                     AnchorStyles.Top |
                     AnchorStyles.Bottom,
            Location = new Point(12, 14),
            Size = new Size(500, 600),
            AutoSize = false
        };

        var autoScroll = content.AutoScroll;
        var originalDock = content.Dock;
        var originalAnchor = content.Anchor;

        host.Attach(content);
        var detached = host.Detach();

        Assert.Same(content, detached);
        Assert.Null(content.Parent);
        Assert.Equal(originalDock, content.Dock);
        Assert.Equal(originalAnchor, content.Anchor);
        Assert.Equal(new Point(12, 14), content.Location);
        Assert.Equal(new Size(500, 600), content.Size);
        Assert.False(content.AutoSize);
        Assert.Equal(autoScroll, content.AutoScroll);
        Assert.Null(host.Content);
    }


    [Fact]
    public void Detach_RestoresCallerDockLayout()
    {
        using var host = new HiveScrollHost
        {
            Size = new Size(200, 200)
        };
        using var content = new Panel
        {
            Dock = DockStyle.Fill,
            Location = new Point(12, 14),
            Size = new Size(500, 600),
            AutoSize = false
        };

        var originalDock = content.Dock;

        host.Attach(content);
        host.Detach();

        Assert.Equal(originalDock, content.Dock);
        Assert.Null(content.Parent);
    }

    [Fact]
    public void DisposingHost_WithAttachedContentUsesNormalParentDisposal()
    {
        var host = new HiveScrollHost
        {
            Size = new Size(200, 200)
        };
        var content = new Panel
        {
            Size = new Size(500, 600)
        };

        host.Attach(content);
        host.Dispose();

        Assert.True(content.IsDisposed);
    }

    [Fact]
    public void DetachingBeforeHostDisposalKeepsContentAlive()
    {
        var host = new HiveScrollHost
        {
            Size = new Size(200, 200)
        };
        var content = new Panel
        {
            Size = new Size(500, 600)
        };

        host.Attach(content);
        var detached = host.Detach();
        host.Dispose();

        Assert.NotNull(detached);
        Assert.False(content.IsDisposed);

        content.Dispose();
    }

    [Fact]
    public void HiveEditorLayout_UsesCustomScrollHostForFieldRegion()
    {
        using var layout = new HiveEditorLayout();

        Assert.NotEmpty(layout.Controls);

        var root = Assert.IsType<TableLayoutPanel>(
            layout.Controls[0]);

        Assert.IsType<HiveScrollHost>(root.Controls[0]);
        Assert.False(layout.FieldsPanel.AutoScroll);
    }

    [Fact]
    public void ScrollbarChangesDoNotReenterHostSynchronizationLoop()
    {
        using var host = new HiveScrollHost
        {
            Size = new Size(200, 200)
        };
        using var content = new Panel
        {
            Size = new Size(500, 600)
        };

        host.Attach(content);
        var changes = 0;
        host.ScrollPositionChanged += (_, _) => changes++;

        host.SetScrollPosition(20, 30);

        Assert.Equal(1, changes);
        Assert.Equal(new Point(-20, -30), content.Location);
    }
}
