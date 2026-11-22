using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Maptext;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Protocol version 19 (tg's tooltips, DV47) through the real server on MetaStation: a player hovers an action button (the oxygen tank's
/// Set Internals) and an alert (Choking (No O2)) in cursor mode, and the client receives tg's tooltip (openToolTip's title and content as
/// /datum/tooltip/show wraps them, the theme) in a Tooltip message; the unhover (protocol 14, MouseExited's closeToolTip) hides it. The
/// client's layout reads the text.
/// </summary>
public class TooltipGameplayTests
{
    private const int Second = 20;

    private readonly ITestOutputHelper _output;

    public TooltipGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    // 6 pixels a character, lines 14 tall: enough for the layout's shape.
    private sealed class FixedMetrics : IMaptextMetrics
    {
        public float Width(in MaptextStyle style, string text) => text.Length * 6f;

        public float LineHeight(in MaptextStyle style) => 14f;

        public float Ascent(in MaptextStyle style) => 11f;
    }

    private static void Unhover(RealHeadlessWorld w, RealHeadlessWorld.Player p)
    {
        p.Session.Hover(ClickTarget.None, w.Now);
        w.Run(Second);
        p.Session.Hover(ClickTarget.None, w.Now);
        w.Run(Second);
    }

    [MetaServerFact]
    public void Hovering_an_action_button_or_an_alert_shows_tgs_tooltip_and_unhovering_hides_it()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 19, TimeoutSteps = 4000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var changes = new List<TooltipMessage>();
        replica.TooltipChanged += t => changes.Add(t);

        // The action button of an oxygen tank in the hand.
        w.Host.EnqueueConsoleLine("give Assistant /obj/item/tank/internals/oxygen");
        w.Run(Second);
        var tank = In(w, () => me.get_active_held_item()) as obj_item_tank_internals_oxygen;
        Assert.NotNull(tank);
        var button = In(w, () => Assert.Single(Dm.each_cast<datum_action>(tank.actions)).viewers[me.hud_used] as atom_movable_screen_movable_action_button);
        Assert.NotNull(button);
        Assert.Contains(replica.Screen.Values, e => e.Id == button.dm_id);
        Assert.Null(replica.Tooltip);

        Assert.True(p.Session.Hover(new ClickTarget(TargetKind.Screen, button.dm_id, 0, 0, 0, 16, 16), w.Now));
        w.Run(Second);
        TooltipMessage shown = replica.Tooltip;
        Assert.NotNull(shown);
        (string name, object desc) = In(w, () => (button.name, button.desc));
        _output.WriteLine("button tooltip: " + shown.Text + " [" + shown.Theme + "]");
        Assert.Equal("Set Internals", name);
        Assert.Equal(Dm.truthy(desc) ? "<h1>Set Internals</h1><p>" + desc + "</p>" : "<p>Set Internals</p>", shown.Text);
        Assert.Equal("midnight", shown.Theme); // tg's openToolTip: the UI style's theme, the Midnight default (M10 slice F: prefs)
        TooltipBox box = TooltipLayout.Place(shown.Text, shown.Theme, new FixedMetrics());
        Assert.Equal("Set Internals", box.Text.Items[0].Text);

        Unhover(w, p);
        Assert.Null(replica.Tooltip);
        Assert.Null(changes[^1]);

        // An alert: tg's lungs throw it when the air has no oxygen; thrown here directly, it joins the HUD's alerts.
        var alert = In(w, () => me.throw_alert(Defines.ALERT_NOT_ENOUGH_OXYGEN, typeof(atom_movable_screen_alert_not_enough_oxy)) as atom_movable_screen_alert);
        Assert.NotNull(alert);
        w.Run(Second);
        Assert.Contains(replica.Screen.Values, e => e.Id == alert.dm_id);
        Assert.True(p.Session.Hover(new ClickTarget(TargetKind.Screen, alert.dm_id, 0, 0, 0, 16, 16), w.Now));
        w.Run(Second);
        Assert.NotNull(replica.Tooltip);
        _output.WriteLine("alert tooltip: " + replica.Tooltip.Text + " [" + replica.Tooltip.Theme + "]");
        Assert.Equal("<h1>Choking (No O2)</h1><p>You're not getting enough oxygen. Find some good air before you pass out! The box in your backpack has an oxygen tank and breath mask in it.</p>", replica.Tooltip.Text);
        Assert.Equal("midnight", replica.Tooltip.Theme);

        // From the alert straight onto the button: the hide and the show of one tick reach the client as the button's tooltip.
        int before = changes.Count;
        Assert.True(p.Session.Hover(new ClickTarget(TargetKind.Screen, button.dm_id, 0, 0, 0, 16, 16), w.Now));
        w.Run(Second);
        Assert.Contains("Set Internals", replica.Tooltip.Text);
        Assert.Equal(before + 1, changes.Count);

        Unhover(w, p);
        Assert.Null(replica.Tooltip);
        Assert.Empty(In(w, () => me.client.dm_tooltip_output.ToList())); // the replicator took every record
    }
}
