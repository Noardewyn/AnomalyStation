using System.Collections.Generic;
using Godot;
using Anomaly.Client.Shared.Tgui;

namespace Anomaly.Client.Hud.Tgui;

// Gaps CI1 (D339, DV571): the kit's NodeGraph, IntegratedCircuit's plane of components, drawn with Godot's node graph: each GraphNode child
// is a Godot GraphNode at its place with its title row and its rows of ports, the wires are the graph's connections in their output port's
// colour. A wire dragged between two ports asks for a connection (the server checks the datatypes and the window shows it once made); a
// wire dragged off its input asks for the input's wires to go; a right click on a port does tg's remove_connection; a dragged node reports
// its new place; a pan reports the plane's new offset once it settles. The zoom and the scroll outlive the window's rebuilds.

/// <summary>The kit's <see cref="UiKind.NodeGraph"/>.</summary>
public partial class TguiNodeGraph : GraphEdit
{
    /// <summary>How long the plane must stay still after a pan before its new offset is reported (seconds).</summary>
    private const double PanSettle = 0.4;

    public UiNode Node;
    private TguiRenderEnv _env;
    private readonly List<GraphNode> _nodes = new();
    private readonly Dictionary<GraphNode, Vector2> _placed = new();
    private bool _ready;
    private int _panSerial;

    private string StateKey => "graph:" + (Node.Key ?? "plane");

    public static Control Build(UiNode node, TguiRenderEnv env)
    {
        var graph = new TguiNodeGraph
        {
            Node = node,
            _env = env,
            RightDisconnects = true,
            SnappingEnabled = node.Selected,
            SnappingDistance = 10,
            ShowGrid = true,
            MinimapEnabled = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, UiSize.Of(node.Height) is UiSize height ? (float)height.Pixels : 300),
        };
        int index = 0;
        foreach (UiNode child in node.Children)
        {
            GraphNode built = graph.BuildNode(child, index++);
            graph._nodes.Add(built);
            graph.AddChild(built);
        }
        graph.ConnectionRequest += graph.OnConnectionRequest;
        graph.DisconnectionRequest += graph.OnDisconnectionRequest;
        graph.EndNodeMove += graph.OnEndNodeMove;
        graph.ScrollOffsetChanged += graph.OnScrollOffsetChanged;
        return graph;
    }

    public override void _Ready()
    {
        if (Node.GraphLinks != null)
        {
            foreach ((int fromNode, int fromPort, int toNode, int toPort, string _) in Node.GraphLinks)
            {
                if (fromNode < 0 || fromNode >= _nodes.Count || toNode < 0 || toNode >= _nodes.Count)
                    continue;
                ConnectNode(_nodes[fromNode].Name, fromPort, _nodes[toNode].Name, toPort);
            }
        }
        // the plane's offset: what this window last scrolled to, else the node's (tg's screen_x, screen_y: the plane's left and top)
        Vector2 scroll = _env?.Local != null ? _env.Local.Get(StateKey + ":scroll", new Vector2(-(float)Node.PosX, -(float)Node.PosY)) : new Vector2(-(float)Node.PosX, -(float)Node.PosY);
        float zoom = _env?.Local != null ? _env.Local.Get(StateKey + ":zoom", 1f) : 1f;
        Callable.From(() =>
        {
            Zoom = zoom;
            ScrollOffset = scroll;
            _ready = true;
        }).CallDeferred();
    }

    // tg: index.jsx:330  handleComponentDropped and :305 handleVarDropped: a component or variable card of a menu dragged onto the plane
    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        Node.OnDropAt != null && data.VariantType == Variant.Type.String && data.AsString() == "tgui";

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        Vector2 at = (atPosition + ScrollOffset) / Zoom;
        Node.OnDropAt?.Invoke(at.X, at.Y);
    }

    private GraphNode BuildNode(UiNode node, int index)
    {
        var graphNode = new TguiGraphNode
        {
            Node = node,
            Name = "node" + index,
            Title = node.Text ?? "",
            PositionOffset = new Vector2((float)node.PosX, (float)node.PosY),
        };
        _placed[graphNode] = graphNode.PositionOffset;
        if (TguiTheme.Named(node.Background) is Color bg)
        {
            graphNode.AddThemeStyleboxOverride("titlebar", TitleBar(bg, false));
            graphNode.AddThemeStyleboxOverride("titlebar_selected", TitleBar(bg, true));
        }
        HBoxContainer titlebar = graphNode.GetTitlebarHBox();
        if (TguiTheme.Named(node.Color) is Color contrast)
        {
            foreach (Node child in titlebar.GetChildren())
                if (child is Label label)
                    label.AddThemeColorOverride("font_color", contrast);
        }
        if (node.Buttons != null && TguiRender.Build(node.Buttons, _env, true) is Control buttons)
            titlebar.AddChild(buttons);

        int slot = 0;
        foreach (UiNode row in node.Children)
        {
            var box = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            box.AddThemeConstantOverride("separation", 16);
            foreach (UiNode cell in row.Children)
            {
                if (TguiRender.Build(cell, _env, true) is Control built)
                    box.AddChild(built);
            }
            graphNode.AddChild(box);
            Color left = TguiTheme.Named(row.PortIn) ?? Colors.SkyBlue;
            Color right = TguiTheme.Named(row.PortOut) ?? Colors.SkyBlue;
            graphNode.SetSlot(slot++, row.PortIn != null, 0, left, row.PortOut != null, 0, right);
        }
        return graphNode;
    }

    private static StyleBoxFlat TitleBar(Color bg, bool selected)
    {
        // tg: IntegratedCircuit.scss .ObjectComponent__Titlebar: the category's colour, rounded at the top
        var box = new StyleBoxFlat { BgColor = bg, ContentMarginLeft = 6, ContentMarginRight = 6, ContentMarginTop = 4, ContentMarginBottom = 4 };
        box.SetCornerRadiusAll(0);
        box.CornerRadiusTopLeft = box.CornerRadiusTopRight = 8;
        if (selected)
        {
            box.BorderColor = Colors.White;
            box.SetBorderWidthAll(1);
        }
        return box;
    }

    private int IndexOf(StringName name)
    {
        for (int i = 0; i < _nodes.Count; i++)
            if (_nodes[i].Name == name)
                return i;
        return -1;
    }

    private void OnConnectionRequest(StringName fromNode, long fromPort, StringName toNode, long toPort)
    {
        int from = IndexOf(fromNode), to = IndexOf(toNode);
        if (from < 0 || to < 0)
            return;
        Node.OnConnect?.Invoke(from, (int)fromPort, to, (int)toPort);
    }

    private void OnDisconnectionRequest(StringName fromNode, long fromPort, StringName toNode, long toPort)
    {
        int from = IndexOf(fromNode), to = IndexOf(toNode);
        if (from < 0 || to < 0)
            return;
        Node.OnDisconnect?.Invoke(from, (int)fromPort, to, (int)toPort);
    }

    private void OnEndNodeMove()
    {
        foreach (GraphNode graphNode in _nodes)
        {
            if (_placed.TryGetValue(graphNode, out Vector2 was) && was != graphNode.PositionOffset && graphNode is TguiGraphNode { Node: UiNode node })
            {
                _placed[graphNode] = graphNode.PositionOffset;
                node.OnMoved?.Invoke(graphNode.PositionOffset.X, graphNode.PositionOffset.Y);
            }
        }
    }

    private void OnScrollOffsetChanged(Vector2 offset)
    {
        if (!_ready)
            return;
        _env?.Local?.SetQuiet(StateKey + ":scroll", offset);
        _env?.Local?.SetQuiet(StateKey + ":zoom", Zoom);
        int serial = ++_panSerial;
        GetTree().CreateTimer(PanSettle).Timeout += () =>
        {
            if (serial == _panSerial && IsInstanceValid(this) && IsInsideTree())
                Node.OnMoved?.Invoke(-ScrollOffset.X, -ScrollOffset.Y);
        };
    }
}

/// <summary>A node of <see cref="TguiNodeGraph"/>: a right click near one of its ports is tg's right click on the port.</summary>
public partial class TguiGraphNode : GraphNode
{
    private const float PortReach = 10;

    public UiNode Node;

    public override void _GuiInput(InputEvent input)
    {
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } button || Node?.OnPortRightClick == null)
            return;
        for (int i = 0; i < GetInputPortCount(); i++)
        {
            if (button.Position.DistanceTo(GetInputPortPosition(i)) <= PortReach)
            {
                Node.OnPortRightClick(true, i);
                AcceptEvent();
                return;
            }
        }
        for (int i = 0; i < GetOutputPortCount(); i++)
        {
            if (button.Position.DistanceTo(GetOutputPortPosition(i)) <= PortReach)
            {
                Node.OnPortRightClick(false, i);
                AcceptEvent();
                return;
            }
        }
    }
}
