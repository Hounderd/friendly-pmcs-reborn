namespace FriendlyPMC.CoreFollowers.Services;

public sealed record FollowerInventoryTreeEntry(
    FollowerInventoryTreeNode Node,
    int Depth);

public static class FollowerInventoryTreeTraversal
{
    public static IReadOnlyList<FollowerInventoryTreeEntry> Flatten(IReadOnlyList<FollowerInventoryTreeNode> nodes)
    {
        if (nodes is null || nodes.Count == 0)
        {
            return Array.Empty<FollowerInventoryTreeEntry>();
        }

        var entries = new List<FollowerInventoryTreeEntry>(nodes.Count);
        foreach (var node in nodes)
        {
            AddNode(entries, node, 0);
        }

        return entries;
    }

    private static void AddNode(List<FollowerInventoryTreeEntry> entries, FollowerInventoryTreeNode node, int depth)
    {
        entries.Add(new FollowerInventoryTreeEntry(node, depth));
        foreach (var child in node.Children)
        {
            AddNode(entries, child, depth + 1);
        }
    }
}
