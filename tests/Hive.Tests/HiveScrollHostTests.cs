using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Xunit;

namespace Hive.Tests;

public sealed class HiveScrollHostTests
{
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

        Assert.NotNull(host.HorizontalScrollState);
        Assert.NotNull(host.VerticalScrollState);
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
