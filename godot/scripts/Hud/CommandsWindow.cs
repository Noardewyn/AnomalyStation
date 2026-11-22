using System.Linq;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Hud.Tgui;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Protocol;

namespace Anomaly.Client.Hud;

/// <summary>
/// Protocol version 78 (silicons SI4): BYOND's stat panel tabs of the own mob's command verbs (the AI's "AI Commands"): one tab a category,
/// one button a verb; a press runs the verb on the server (<see cref="ActionKind.Verb"/>). tg's stat panel is always on screen; the port's is a
/// window the "Commands" button at the screen's top left opens, which is there only while the mob has command verbs.
/// </summary>
public partial class CommandsWindow : PanelContainer
{
    private ReplicaWorld _replica;
    private IReadOnlyList<VerbEntry> _shown;
    private Action<string> _run;
    private WindowChrome _chrome;
    private TabContainer _tabs;
    private Button _toggle;

    /// <summary>Binds the window to the world's verbs; <paramref name="run"/> sends a verb's path; the toggle button goes on <paramref name="layer"/>.</summary>
    private Func<float> _rowRight;

    /// <param name="rowRight">the right edge of the top-left HUD row (the action buttons), which the toggle goes beside</param>
    public void Bind(ReplicaWorld replica, Action<string> run, CanvasLayer layer, PlayOptions options, Action saved, Func<float> rowRight)
    {
        _rowRight = rowRight;
        _replica = replica;
        _run = run;
        Name = "CommandsWindow";
        Visible = false;
        AddThemeStyleboxOverride("panel", TguiRender.Flat(TguiTheme.WindowBg, 1));
        var column = new VBoxContainer();
        AddChild(column);
        _chrome = new WindowChrome(this, "commands", options, saved) { MinSize = new Vector2(220, 160) };
        column.AddChild(_chrome.TitleBar("Commands", () => Visible = false));
        _tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddChild(_tabs);
        column.AddChild(_chrome.Grip());
        layer.AddChild(this);
        _chrome.Place(new Vector2(16, 120), new Vector2(300, 360));
        _toggle = new Button { Text = "Commands", FocusMode = FocusModeEnum.None, Visible = false, Position = new Vector2(96, 8), TooltipText = "The command verbs (tg's stat panel tabs)" };
        _toggle.Pressed += () => Visible = !Visible;
        layer.AddChild(_toggle);
    }

    public override void _Process(double delta)
    {
        if (_toggle.Visible) // beside the action buttons, not over them (the owner, 2026-11-14: the AI's second button was covered)
            _toggle.Position = new Vector2(Mathf.Max(16f, (_rowRight?.Invoke() ?? 0f) + 8f), 8);
        if (_replica == null || ReferenceEquals(_replica.Verbs, _shown))
            return;
        _shown = _replica.Verbs;
        foreach (Node child in _tabs.GetChildren())
        {
            _tabs.RemoveChild(child);
            child.QueueFree();
        }
        foreach (IGrouping<string, VerbEntry> category in _shown.GroupBy(v => v.Category))
        {
            var scroll = new ScrollContainer { Name = category.Key, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            scroll.AddChild(list);
            foreach (VerbEntry verb in category)
            {
                var button = new Button { Text = verb.Name, FocusMode = FocusModeEnum.None, Alignment = HorizontalAlignment.Left };
                string path = verb.Path;
                button.Pressed += () => _run?.Invoke(path);
                list.AddChild(button);
            }
            _tabs.AddChild(scroll);
        }
        bool any = _shown.Count > 0;
        _toggle.Visible = any;
        if (!any)
            Visible = false;
    }
}
