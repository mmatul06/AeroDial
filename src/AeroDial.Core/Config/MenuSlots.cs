// AeroDial — MenuSlots.cs
// Slot arithmetic for the ring editor: a menu is a list of items where an empty
// placeholder (ActionType.None, no label) may sit at any position so users control
// where the gaps are. Trailing placeholders are never persisted.

namespace AeroDial.Config;

public static class MenuSlots
{
    public static MenuItemConfig NewEmpty() => new() { Label = "", Icon = "", ActionType = ActionType.None };

    public static bool SlotFilled(RadialMenuConfig? menu, int slot)
        => menu is not null && slot >= 0 && slot < menu.Items.Count && !menu.Items[slot].IsEmptySlot;

    /// <summary>Places item at slot, padding with empties so the index exists.</summary>
    public static void PutAt(List<MenuItemConfig> items, int slot, MenuItemConfig item)
    {
        if (slot < 0) return;
        while (items.Count <= slot) items.Add(NewEmpty());
        items[slot] = item;
    }

    /// <summary>Removes empty placeholders from the end of the list.</summary>
    public static void TrimTrailingEmpties(List<MenuItemConfig> items)
    {
        while (items.Count > 0 && items[^1].IsEmptySlot) items.RemoveAt(items.Count - 1);
    }

    /// <summary>Swaps the items at src and dst (padding with empties so both exist), then
    /// trims trailing empties. Moving into an empty slot therefore leaves an empty placeholder
    /// behind, so every other slot keeps its position.</summary>
    public static void MoveOrSwap(List<MenuItemConfig> items, int src, int dst)
    {
        if (src == dst || src < 0 || dst < 0) return;
        int max = Math.Max(src, dst);
        while (items.Count <= max) items.Add(NewEmpty());
        (items[src], items[dst]) = (items[dst], items[src]);
        TrimTrailingEmpties(items);
    }
}
