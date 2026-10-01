using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Hive.Core;
using Hive.Host.WinForms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Xunit;

namespace Hive.Tests;

public sealed class HiveComboBoxTests
{
    [Fact]
    public void Contract_IsHiveOwnedControlAndPreservesIntegrationMetadata()
    {
        using var combo = new HiveComboBox();

        Assert.True(typeof(Control).IsAssignableFrom(typeof(HiveComboBox)));
        Assert.False(typeof(ComboBox).IsAssignableFrom(typeof(HiveComboBox)));
        Assert.NotNull(combo.HiveIntegration);
        Assert.NotNull(combo.HiveField);
        Assert.Equal(AccessibleRole.ComboBox, combo.AccessibleRole);
    }

    [Fact]
    public void ConstructionAndResize_KeepFieldLayoutUsable()
    {
        using var combo = new HiveComboBox
        {
            Size = new Size(320, 42)
        };

        combo.PerformLayout();
        Assert.Single(combo.Controls);
        Assert.True(combo.Controls[0].Width > 0);
        Assert.True(combo.Controls[0].Height > 0);
    }

    [Fact]
    public void Items_SelectionAndNotificationsRemainDeterministic()
    {
        using var combo = new HiveComboBox
        {
            DisplayMember = nameof(Option.Name),
            ValueMember = nameof(Option.Id)
        };

        var first = new Option(1, "First");
        var second = new Option(2, "Second");

        combo.Items.Add(first);
        combo.Items.Add(second);

        var selectedIndexChanged = 0;
        var selectedItemChanged = 0;
        var selectedValueChanged = 0;

        combo.SelectedIndexChanged += (_, _) => selectedIndexChanged++;
        combo.SelectedItemChanged += (_, _) => selectedItemChanged++;
        combo.SelectedValueChanged += (_, _) => selectedValueChanged++;

        combo.SelectedIndex = 1;
        combo.SelectedIndex = 1;

        Assert.Equal(1, combo.SelectedIndex);
        Assert.Same(second, combo.SelectedItem);
        Assert.Equal(2, combo.SelectedValue);
        Assert.Equal("Second", combo.Text);
        Assert.Equal(1, selectedIndexChanged);
        Assert.Equal(1, selectedItemChanged);
        Assert.Equal(1, selectedValueChanged);
    }

    [Fact]
    public void DisplayMemberAndValueMemberResolveBoundSelection()
    {
        using var combo = new HiveComboBox
        {
            DisplayMember = nameof(Option.Name),
            ValueMember = nameof(Option.Id)
        };

        var items = new BindingList<Option>
        {
            new(10, "Alpha"),
            new(20, "Beta"),
            new(30, "Gamma")
        };

        combo.DataSource = items;
        combo.SelectedValue = 20;

        Assert.Equal(1, combo.SelectedIndex);
        Assert.Equal(20, combo.SelectedValue);
        Assert.Equal("Beta", combo.Text);
        Assert.Equal("Beta", ((Option)combo.SelectedItem!).Name);
    }

    [Fact]
    public void DataSource_TracksCurrentPositionAndKeepsSelectedItemWhenListChanges()
    {
        using var combo = new HiveComboBox
        {
            DisplayMember = nameof(Option.Name),
            ValueMember = nameof(Option.Id)
        };

        var selected = new Option(20, "Beta");
        var items = new BindingList<Option>
        {
            new(10, "Alpha"),
            selected,
            new(30, "Gamma")
        };

        combo.DataSource = items;
        combo.SelectedIndex = 1;

        var refreshed = new BindingList<Option>
        {
            new(10, "Alpha"),
            selected,
            new(40, "Delta")
        };

        combo.DataSource = refreshed;

        Assert.Equal(1, combo.SelectedIndex);
        Assert.Same(selected, combo.SelectedItem);
        Assert.Equal(20, combo.SelectedValue);
        Assert.Equal("Beta", combo.Text);
    }

    [Fact]
    public void Filtering_IsOrdinalCaseInsensitiveAndPreservesSourceOrder()
    {
        using var combo = new HiveComboBox
        {
            DisplayMember = nameof(Option.Name)
        };

        combo.Items.AddRange(
        [
            new Option(1, "Alpha"),
            new Option(2, "Beta"),
            new Option(3, "alphabet"),
            new Option(4, "BETA tools"),
            new Option(5, "Gamma")
        ]);

        combo.ApplyFilterForTesting("BeTa");

        Assert.Equal(2, combo.FilteredCountForTesting);
        Assert.Equal(
            new[] { "Beta", "BETA tools" },
            combo.FilteredDisplayValuesForTesting);

        combo.ApplyFilterForTesting(string.Empty);

        Assert.Equal(5, combo.FilteredCountForTesting);
    }

    [Fact]
    public void Filtering_DoesNotMutateUnderlyingItemsOrSelection()
    {
        using var combo = new HiveComboBox
        {
            DisplayMember = nameof(Option.Name)
        };

        var selected = new Option(2, "Beta");

        combo.Items.Add(new Option(1, "Alpha"));
        combo.Items.Add(selected);
        combo.Items.Add(new Option(3, "Gamma"));
        combo.SelectedItem = selected;

        combo.ApplyFilterForTesting("Gamma");

        Assert.Equal(3, combo.Items.Count);
        Assert.Same(selected, combo.SelectedItem);
        Assert.Equal(1, combo.SelectedIndex);
        Assert.Equal(new[] { "Gamma" }, combo.FilteredDisplayValuesForTesting);
    }

    [Fact]
    public void SelectedValue_IgnoresNullItemsWhenResolvingValueMember()
    {
        using var combo = new HiveComboBox
        {
            DisplayMember = nameof(Option.Name),
            ValueMember = nameof(Option.Id)
        };

        combo.Items.Add(null);
        combo.Items.Add(new Option(42, "Answer"));

        combo.SelectedValue = 42;

        Assert.Equal(1, combo.SelectedIndex);
        Assert.Equal(42, combo.SelectedValue);
        Assert.Equal("Answer", combo.Text);
    }

    [Fact]
    public void SelectedValue_UsesStableItemIdentityWhenDisplayTextDuplicates()
    {
        using var combo = new HiveComboBox
        {
            DisplayMember = nameof(Option.Name),
            ValueMember = nameof(Option.Id)
        };

        var first = new Option(11, "Same");
        var second = new Option(22, "Same");

        combo.Items.Add(first);
        combo.Items.Add(second);
        combo.SelectedValue = 22;

        Assert.Equal(1, combo.SelectedIndex);
        Assert.Same(second, combo.SelectedItem);
        Assert.Equal(22, combo.SelectedValue);
    }

    [Fact]
    public void KeyboardSelection_CommitsHighlightedItemAndClosesPopup()
    {
        using var form = new Form
        {
            Size = new Size(500, 400)
        };
        using var combo = new HiveComboBox
        {
            Location = new Point(20, 20),
            Size = new Size(240, 34),
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        combo.Items.AddRange(
        [
            "First",
            "Second",
            "Third"
        ]);

        form.Controls.Add(combo);
        form.CreateControl();
        combo.CreateControl();

        combo.ShowDropDown();
        Assert.True(combo.DroppedDown);

        combo.ProcessKeyForTesting(Keys.Down);
        combo.ProcessKeyForTesting(Keys.Enter);

        Assert.Equal(1, combo.SelectedIndex);
        Assert.Equal("Second", combo.Text);
        Assert.False(combo.DroppedDown);
    }

    [Fact]
    public void LongPopupList_UsesHiveScrollHost()
    {
        using var form = new Form
        {
            Size = new Size(500, 400)
        };
        using var combo = new HiveComboBox
        {
            Location = new Point(20, 20),
            Size = new Size(240, 34),
            MaxDropDownItems = 4
        };

        for (var index = 0; index < 40; index++)
            combo.Items.Add($"Option {index + 1:00}");

        form.Controls.Add(combo);
        form.CreateControl();
        combo.CreateControl();

        combo.ShowDropDown();

        Assert.True(combo.DroppedDown);
        Assert.True(combo.PopupVerticalScrollStateForTesting.CanScroll);
        Assert.Equal(0, combo.PopupVerticalScrollStateForTesting.Value);

        combo.HideDropDown();
        Assert.False(combo.DroppedDown);
    }

    [Fact]
    public void PopupHighlightScrollOnlyMovesWhenHighlightIsOutsideViewport()
    {
        Assert.Equal(
            200,
            HiveComboBox.CalculatePopupHighlightScrollForTesting(
                currentScroll: 200,
                rowTop: 260,
                rowHeight: 34,
                viewportSize: 240));

        Assert.Equal(
            220,
            HiveComboBox.CalculatePopupHighlightScrollForTesting(
                currentScroll: 200,
                rowTop: 420,
                rowHeight: 40,
                viewportSize: 240));

        Assert.Equal(
            150,
            HiveComboBox.CalculatePopupHighlightScrollForTesting(
                currentScroll: 200,
                rowTop: 150,
                rowHeight: 40,
                viewportSize: 240));
    }

    [Fact]
    public void PopupBounds_FlipAboveOwnerWhenThereIsNoRoomBelow()
    {
        var ownerBounds = new Rectangle(100, 700, 240, 34);
        var workArea = new Rectangle(0, 0, 1000, 768);

        var bounds = HiveComboBox.CalculatePopupBoundsForTesting(
            ownerBounds,
            workArea,
            popupWidth: 280,
            popupHeight: 220);

        Assert.Equal(100, bounds.X);
        Assert.Equal(480, bounds.Y);
        Assert.Equal(280, bounds.Width);
        Assert.Equal(220, bounds.Height);
    }

    [Fact]
    public void PopupBounds_ClampToWorkAreaWhenOwnerIsNearAnEdge()
    {
        var ownerBounds = new Rectangle(960, 700, 50, 34);
        var workArea = new Rectangle(0, 0, 1000, 768);

        var bounds = HiveComboBox.CalculatePopupBoundsForTesting(
            ownerBounds,
            workArea,
            popupWidth: 280,
            popupHeight: 300);

        Assert.Equal(720, bounds.X);
        Assert.Equal(400, bounds.Y);
        Assert.Equal(280, bounds.Width);
        Assert.Equal(300, bounds.Height);
    }

    [Fact]
    public void Accessibility_ExposesValueAndExpandedState()
    {
        using var combo = new HiveComboBox
        {
            Text = "Selected value"
        };

        var accessible = combo.AccessibilityObject;

        Assert.Equal(AccessibleRole.ComboBox, accessible.Role);
        Assert.True(
            accessible.State.HasFlag(AccessibleStates.Collapsed));
        Assert.Equal("Selected value", accessible.Value);
    }

    [Fact]
    public void HostValueAdapter_HandlesHiveComboBoxExplicitly()
    {
        using var combo = new HiveComboBox();
        combo.Text = "Adapter value";

        Assert.True(
            WinFormsControlValueAdapters.TryGet(
                combo,
                out var adapter));
        Assert.IsType<HiveComboBoxValueAdapter>(adapter);
        Assert.Equal(
            typeof(string).FullName,
            adapter.ValueTypeName);

        var request = new HiveHostInteractionRequest(
            Guid.NewGuid(),
            HiveHostInteractionKind.SetControlValue,
            CorrelationId.New(),
            controlId: "combo",
            value: HiveHostValue.FromString("Updated"),
            captureId: Guid.NewGuid());

        var result = WinFormsControlValueAdapters.SetControlValue(
            combo,
            request);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("Updated", combo.Text);
    }

    [Fact]
    public void Filtering_ReportsNoMatchWithoutMutatingItems()
    {
        using var combo = new HiveComboBox
        {
            DisplayMember = nameof(Option.Name)
        };

        combo.Items.AddRange(
        [
            new Option(1, "Alpha"),
            new Option(2, "Beta")
        ]);

        combo.ApplyFilterForTesting("missing");

        Assert.Equal(0, combo.FilteredCountForTesting);
        Assert.Empty(combo.FilteredDisplayValuesForTesting);
        Assert.Equal(2, combo.Items.Count);
    }

    [Fact]
    public void ThemeApplication_PreservesSelectionAndCanUpdateOpenPopup()
    {
        using var form = new Form
        {
            Size = new Size(500, 400)
        };
        using var combo = new HiveComboBox
        {
            Location = new Point(20, 20),
            Size = new Size(240, 34)
        };

        combo.Items.AddRange(["Alpha", "Beta", "Gamma"]);
        combo.SelectedIndex = 1;

        form.Controls.Add(combo);
        form.CreateControl();
        combo.CreateControl();

        combo.ShowDropDown();
        Assert.True(combo.DroppedDown);

        var themeManager = new HiveThemeManager(HiveThemeMode.Dark);
        themeManager.Apply(combo);

        Assert.Equal(1, combo.SelectedIndex);
        Assert.Equal("Beta", combo.Text);
        Assert.True(combo.DroppedDown);

        combo.HideDropDown();
    }

    private sealed record Option(int Id, string? Name);
}
