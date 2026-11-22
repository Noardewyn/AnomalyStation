namespace Anomaly.Client.Shared.Dev;

/// <summary>One row of a list of types: a type of the list, or a branch of the tree of their paths that holds some of them.</summary>
/// <param name="Path">The row's path (<c>/obj/machinery/power</c>).</param>
/// <param name="Entry">The list's type of that path; null for a branch that is no type of the list (a base class the list leaves out).</param>
/// <param name="Depth">How deep the row stands in the tree (0: a top row).</param>
/// <param name="Branch">The row has rows under it.</param>
/// <param name="Open">A branch that shows them.</param>
/// <param name="Types">How many types of the list stand under a branch.</param>
public readonly record struct SpawnRow(string Path, SpawnEntry Entry, int Depth, bool Branch, bool Open, int Types)
{
    /// <summary>The last part of the path: what a row of the tree is called by.</summary>
    public string Leaf => Path.Substring(Path.LastIndexOf('/') + 1);
}

/// <summary>
/// A list of types as a tree by their paths (the map editor's palette and the dev menu's spawn panel): <c>/obj/machinery/power/rtg</c> stands
/// under power, which stands under machinery. The part of the path that every type of the list shares is no level of the tree; a type that is
/// that very path stands first.
/// </summary>
public sealed class SpawnTree
{
    private sealed class Node
    {
        public string Path;
        public SpawnEntry Entry;
        public readonly List<Node> Children = new();
        public int Types;
    }

    private readonly Node _root = new() { Path = string.Empty };
    private readonly Dictionary<string, Node> _nodes = new(StringComparer.Ordinal);

    /// <summary>The tree of a list of types (a path listed twice counts once).</summary>
    public SpawnTree(IReadOnlyList<SpawnEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
            return;
        // the part every path shares, in whole steps of the path
        string shared = entries[0].Path;
        foreach (SpawnEntry e in entries)
        {
            while (!(e.Path == shared || (e.Path.Length > shared.Length && e.Path.StartsWith(shared, StringComparison.Ordinal) && e.Path[shared.Length] == '/')))
                shared = shared.Substring(0, Math.Max(0, shared.LastIndexOf('/')));
        }
        _root.Path = shared;
        _nodes[shared] = _root;
        foreach (SpawnEntry e in entries)
        {
            Node node = _root;
            for (int at = shared.Length; at < e.Path.Length;)
            {
                int next = e.Path.IndexOf('/', at + 1);
                string path = next < 0 ? e.Path : e.Path.Substring(0, next);
                if (!_nodes.TryGetValue(path, out Node child))
                {
                    _nodes[path] = child = new Node { Path = path };
                    node.Children.Add(child);
                }
                node = child;
                at = path.Length;
            }
            if (node.Entry != null)
                continue;
            node.Entry = e;
            for (Node over = Parent(node); over != null; over = Parent(over))
                over.Types++;
        }
        foreach (Node node in _nodes.Values)
            node.Children.Sort(static (a, b) => string.CompareOrdinal(a.Path, b.Path));
    }

    private Node Parent(Node node)
        => ReferenceEquals(node, _root) ? null : _nodes[node.Path.Substring(0, Math.Max(_root.Path.Length, node.Path.LastIndexOf('/')))];

    /// <summary>The rows to show, into <paramref name="into"/> (cleared first): the top rows, and under every open branch its own.</summary>
    public void Rows(IReadOnlySet<string> open, List<SpawnRow> into)
    {
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(into);
        into.Clear();
        if (_root.Entry != null)
            into.Add(new SpawnRow(_root.Path, _root.Entry, 0, false, false, 0));
        foreach (Node top in _root.Children)
            Add(top, 0, open, into);
    }

    private static void Add(Node node, int depth, IReadOnlySet<string> open, List<SpawnRow> into)
    {
        bool branch = node.Children.Count > 0, shown = branch && open.Contains(node.Path);
        into.Add(new SpawnRow(node.Path, node.Entry, depth, branch, shown, node.Types));
        if (!shown)
            return;
        foreach (Node child in node.Children)
            Add(child, depth + 1, open, into);
    }

    /// <summary>The branches a type stands under, the top one first: open, they bring its row into the list. None for a path the tree has not.</summary>
    public IReadOnlyList<string> Over(string path)
    {
        var over = new List<string>();
        if (path == null || !_nodes.TryGetValue(path, out Node node))
            return over;
        for (Node up = Parent(node); up != null && !ReferenceEquals(up, _root); up = Parent(up))
            over.Add(up.Path);
        over.Reverse();
        return over;
    }

    /// <summary>The same list with no tree, a row a type: a search's matches, the favourites.</summary>
    public static void Flat(IReadOnlyList<SpawnEntry> entries, List<SpawnRow> into)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(into);
        into.Clear();
        foreach (SpawnEntry e in entries)
            into.Add(new SpawnRow(e.Path, e, 0, false, false, 0));
    }
}
