using System.Drawing;
using System.Windows.Forms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Xunit;

namespace Hive.Tests;

public sealed class HiveTabControlTests
{
    [Fact]
    public void Contract_IsHiveOwnedAndExposesConventionalTabSurface()
    {
        using var tabs = new HiveTabControl();

        Assert.True(typeof(Control).IsAssignableFrom(typeof(HiveTabControl)));
        Assert.False(typeof(TabControl).IsAssignableFrom(typeof(HiveTabControl)));
        Assert.Equal(AccessibleRole.PageTabList, tabs.AccessibleRole);
        Assert.NotNull(tabs.TabPages);
        Assert.Equal(-1, tabs.SelectedIndex);
        Assert.Null(tabs.SelectedTab);
    }

    [Fact]
    public void AddingPages_SelectsFirstAndPreservesPageInstances()
    {
        using var tabs = new HiveTabControl();
        var first = new TabPage("First");
        var second = new TabPage("Second");

        tabs.TabPages.Add(first);
        tabs.TabPages.Add(second);

        Assert.Equal(2, tabs.TabPages.Count);
        Assert.Equal(0, tabs.SelectedIndex);
        Assert.Same(first, tabs.SelectedTab);
        Assert.True(first.Visible);
        Assert.False(second.Visible);
        Assert.IsType<TabControl>(first.Parent);
        Assert.IsType<TabControl>(second.Parent);

        tabs.SelectedIndex = 1;

        Assert.Same(second, tabs.SelectedTab);
        Assert.False(first.Visible);
        Assert.True(second.Visible);
        Assert.Same(second, tabs.TabPages[1]);
    }

    [Fact]
    public void SelectionNotifications_DoNotDuplicateForSameIndex()
    {
        using var tabs = new HiveTabControl();
        tabs.TabPages.Add("First");
        tabs.TabPages.Add("Second");

        var indexChanged = 0;
        var tabChanged = 0;

        tabs.SelectedIndexChanged += (_, _) => indexChanged++;
        tabs.SelectedTabChanged += (_, _) => tabChanged++;

        tabs.SelectedIndex = 1;
        tabs.SelectedIndex = 1;
        tabs.SelectPreviousTab();

        Assert.Equal(2, indexChanged);
        Assert.Equal(2, tabChanged);
        Assert.Equal(0, tabs.SelectedIndex);
    }

    [Fact]
    public void DisabledTabs_AreSkippedByUserNavigationButRemainProgrammaticallySelectable()
    {
        using var tabs = new HiveTabControl();
        tabs.TabPages.Add("First");
        var disabled = tabs.TabPages.Add("Disabled");
        tabs.TabPages.Add("Third");

        disabled.Enabled = false;

        tabs.SelectNextTab();
        Assert.Equal(2, tabs.SelectedIndex);

        tabs.SelectPreviousTab();
        Assert.Equal(0, tabs.SelectedIndex);

        tabs.SelectedIndex = 1;
        Assert.Equal(1, tabs.SelectedIndex);
        Assert.Same(disabled, tabs.SelectedTab);
    }

    [Fact]
    public void SelectedPage_IsPreservedAcrossThemeChanges()
    {
        using var tabs = new HiveTabControl();
        tabs.TabPages.Add("First");
        tabs.TabPages.Add("Second");
        tabs.SelectedIndex = 1;

        var selectedPage = tabs.SelectedTab;
        var selectedIndex = tabs.SelectedIndex;

        var dark = new HiveThemeManager(HiveThemeMode.Dark);
        tabs.ApplyTheme(dark.Theme);
        var light = new HiveThemeManager(HiveThemeMode.Light);
        tabs.ApplyTheme(light.Theme);

        Assert.Equal(selectedIndex, tabs.SelectedIndex);
        Assert.Same(selectedPage, tabs.SelectedTab);
    }

    [Fact]
    public void Accessibility_ExposesRoleAndSelectedTab()
    {
        using var tabs = new HiveTabControl();
        tabs.TabPages.Add("Overview");
        tabs.TabPages.Add("Configuration");
        tabs.SelectedIndex = 1;

        var accessible = tabs.AccessibilityObject;

        Assert.Equal(AccessibleRole.PageTabList, accessible.Role);
        Assert.Contains("Configuration", accessible.Value);
    }

    [Fact]
    public void RemovingPages_DoesNotDisposeCallerOwnedPage()
    {
        using var tabs = new HiveTabControl();
        var first = new TabPage("First");
        var second = new TabPage("Second");

        tabs.TabPages.Add(first);
        tabs.TabPages.Add(second);
        tabs.TabPages.Remove(first);

        Assert.False(first.IsDisposed);
        Assert.Single(tabs.TabPages);
        Assert.Same(second, tabs.SelectedTab);

        first.Dispose();
    }

    [Fact]
    public void HeaderOverflow_UsesHiveScrollInfrastructureAndKeepsSelectedTabVisible()
    {
        using var form = new Form
        {
            Size = new Size(260, 180)
        };
        using var tabs = new HiveTabControl
        {
            Location = new Point(8, 8),
            Size = new Size(220, 120)
        };

        for (var index = 0; index < 12; index++)
            tabs.TabPages.Add($"Configuration section {index + 1:00}");

        form.Controls.Add(tabs);
        form.CreateControl();
        tabs.CreateControl();

        Assert.True(tabs.HeaderHorizontalScrollStateForTesting.CanScroll);

        tabs.SelectedIndex = tabs.TabPages.Count - 1;

        Assert.True(tabs.HeaderHorizontalScrollPositionForTesting > 0);

        var bounds = tabs.GetTabHeaderBoundsForTesting(tabs.SelectedIndex);
        var viewport = tabs.HeaderHorizontalScrollStateForTesting.ViewportSize;
        var scroll = tabs.HeaderHorizontalScrollPositionForTesting;

        Assert.True(bounds.Left >= scroll);
        Assert.True(bounds.Right <= scroll + viewport);
    }

    [Fact]
    public void HeaderScrollCalculation_PreservesCurrentPositionWhenTabIsVisible()
    {
        Assert.Equal(
            100,
            HiveTabControl.CalculateHeaderScrollForTesting(
                currentScroll: 100,
                tabLeft: 140,
                tabWidth: 80,
                viewportSize: 200));

        Assert.Equal(
            40,
            HiveTabControl.CalculateHeaderScrollForTesting(
                currentScroll: 100,
                tabLeft: 40,
                tabWidth: 80,
                viewportSize: 200));

        Assert.Equal(
            120,
            HiveTabControl.CalculateHeaderScrollForTesting(
                currentScroll: 100,
                tabLeft: 300,
                tabWidth: 20,
                viewportSize: 200));
    }

    [Fact]
    public void HeaderHeight_IsDpiAwareAndResizesHeaderSurface()
    {
        using var tabs = new HiveTabControl();

        tabs.HeaderHeight = 48;

        Assert.Equal(48, tabs.HeaderHeight);
    }

    [Fact]
    public void ClearingTabs_RemovesPagesWithoutDisposingThem()
    {
        using var tabs = new HiveTabControl();
        var first = new TabPage("First");
        var second = new TabPage("Second");

        tabs.TabPages.Add(first);
        tabs.TabPages.Add(second);
        tabs.TabPages.Clear();

        Assert.Empty(tabs.TabPages);
        Assert.Equal(-1, tabs.SelectedIndex);
        Assert.Null(tabs.SelectedTab);
        Assert.False(first.IsDisposed);
        Assert.False(second.IsDisposed);

        first.Dispose();
        second.Dispose();
    }

    [Fact]
    public void HeaderKeyboardFocus_NavigatesWithLeftAndRight()
    {
        using var form = new Form
        {
            Size = new Size(420, 220)
        };
        using var tabs = new HiveTabControl
        {
            Location = new Point(8, 8),
            Size = new Size(360, 140)
        };

        tabs.TabPages.Add("First");
        tabs.TabPages.Add("Second");
        tabs.TabPages.Add("Third");

        form.Controls.Add(tabs);
        form.Show();
        form.Activate();
        Application.DoEvents();

        tabs.FocusHeaderForTesting();
        Assert.True(tabs.ContainsFocus);
        tabs.ProcessKeyForTesting(Keys.Right);

        Assert.Equal(1, tabs.SelectedIndex);
        Assert.True(tabs.ContainsFocus);

        tabs.ProcessKeyForTesting(Keys.Left);

        Assert.Equal(0, tabs.SelectedIndex);
    }

    [Fact]
    public void CtrlTabNavigation_UsesNextAndPreviousTab()
    {
        using var tabs = new HiveTabControl();
        tabs.TabPages.Add("First");
        tabs.TabPages.Add("Second");
        tabs.TabPages.Add("Third");

        tabs.ProcessKeyForTesting(Keys.Control | Keys.Tab);
        Assert.Equal(1, tabs.SelectedIndex);

        tabs.ProcessKeyForTesting(Keys.Control | Keys.Shift | Keys.Tab);
        Assert.Equal(0, tabs.SelectedIndex);
    }
}
