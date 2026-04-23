// src/Orbital.Core/Ordering/TodoOrdering.cs
namespace Orbital.Core.Ordering;

using Orbital.Core.Models;

public static class TodoOrdering
{
    public static int NextTopOrder(IReadOnlyCollection<Todo> existing)
    {
        if (existing.Count == 0) return 0;
        return existing.Min(t => t.Order) - 1;
    }

    public static void ReorderActive(List<Todo> items, int fromIndex, int toIndex)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (fromIndex == toIndex) return;
        var item = items[fromIndex];
        items.RemoveAt(fromIndex);
        items.Insert(toIndex, item);
        // Reassign sequential orders so drag history doesn't accumulate gaps that confuse later sorts.
        for (int i = 0; i < items.Count; i++)
            items[i].Order = i;
    }
}
