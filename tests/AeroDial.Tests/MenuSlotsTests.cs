using AeroDial.Config;

namespace AeroDial.Tests;

public class MenuSlotsTests
{
    private static MenuItemConfig Item(string label) => new() { Label = label, ActionType = ActionType.OpenSettings };

    [Fact]
    public void PutAt_pads_with_empty_slots()
    {
        var items = new List<MenuItemConfig>();
        MenuSlots.PutAt(items, 3, Item("D"));
        Assert.Equal(4, items.Count);
        Assert.True(items[0].IsEmptySlot);
        Assert.True(items[2].IsEmptySlot);
        Assert.Equal("D", items[3].Label);
    }

    [Fact]
    public void TrimTrailingEmpties_keeps_interior_gaps()
    {
        var items = new List<MenuItemConfig> { Item("A"), MenuSlots.NewEmpty(), Item("C"), MenuSlots.NewEmpty(), MenuSlots.NewEmpty() };
        MenuSlots.TrimTrailingEmpties(items);
        Assert.Equal(3, items.Count);
        Assert.True(items[1].IsEmptySlot);
    }

    [Fact]
    public void MoveOrSwap_into_empty_slot_leaves_a_gap_behind()
    {
        var items = new List<MenuItemConfig> { Item("A"), Item("B") };
        MenuSlots.MoveOrSwap(items, 0, 5);
        Assert.Equal(6, items.Count);
        Assert.True(items[0].IsEmptySlot);
        Assert.Equal("B", items[1].Label);
        Assert.Equal("A", items[5].Label);
    }

    [Fact]
    public void MoveOrSwap_between_filled_slots_swaps()
    {
        var items = new List<MenuItemConfig> { Item("A"), Item("B"), Item("C") };
        MenuSlots.MoveOrSwap(items, 0, 2);
        Assert.Equal(["C", "B", "A"], items.Select(i => i.Label));
    }

    [Fact]
    public void MoveOrSwap_to_the_end_then_back_trims_the_tail()
    {
        var items = new List<MenuItemConfig> { Item("A"), Item("B") };
        MenuSlots.MoveOrSwap(items, 1, 4);
        MenuSlots.MoveOrSwap(items, 4, 1);
        Assert.Equal(2, items.Count);
        Assert.Equal("B", items[1].Label);
    }

    [Fact]
    public void SlotFilled_reports_only_real_items()
    {
        var menu = new RadialMenuConfig { Items = [Item("A"), MenuSlots.NewEmpty()] };
        Assert.True(MenuSlots.SlotFilled(menu, 0));
        Assert.False(MenuSlots.SlotFilled(menu, 1));
        Assert.False(MenuSlots.SlotFilled(menu, 2));
        Assert.False(MenuSlots.SlotFilled(null, 0));
    }
}
