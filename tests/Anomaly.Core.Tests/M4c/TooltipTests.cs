using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M4c;

/// <summary>
/// Protocol version 19 (DV47): tg's tooltips (code/modules/tooltip/tooltip.dm). client/New makes the client's /datum/tooltip; a HUD
/// object's MouseEntered (an alert's, an action button's, an item's after its delay) calls openToolTip with its name and description and
/// the datum writes tg's HTML text and theme into the client (dm_tooltip_output, the page's update); MouseExited's closeToolTip writes
/// the hide (the window's winshow). The datum's own rules: no params or no text show nothing, a title alone is a paragraph, the name's
/// \proper and \improper go, a hide while showing waits 0.1 s, a deleted target hides.
/// </summary>
public class TooltipTests
{
    private const int Second = 20;

    private static (AudioRoom r, mob_living_carbon_human bob) Room(ulong seed)
    {
        var r = new AudioRoom(9, 9, seed: seed);
        mob_living_carbon_human bob;
        try { bob = r.Join(4, 4, "Bob"); }
        catch (Exception e) { throw new Exception(string.Join("\n", r.Room.RuntimeText), e); }
        r.Step(Second);
        r.In(() => bob.client.dm_tooltip_output.Clear());
        return (r, bob);
    }

    private static List<TooltipOutput> Taken(AudioRoom r, mob who)
        => r.In(() =>
        {
            var taken = who.client.dm_tooltip_output.ToList();
            who.client.dm_tooltip_output.Clear();
            return taken;
        });

    [Fact]
    public void TheClientHasItsTooltipDatum()
    {
        var (r, bob) = Room(1901);
        r.In(() =>
        {
            Assert.NotNull(bob.client.tooltips);
            Assert.Same(bob.client, bob.client.tooltips.owner);
            Assert.Equal(SKIN_MAINWINDOW_TOOLTIP, bob.client.tooltips.control);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void HoveringAnAlertShowsItsNameAndDescriptionAndLeavingItHides()
    {
        var (r, bob) = Room(1902);
        atom_movable_screen_alert alert = r.In(() => bob.throw_alert(ALERT_NOT_ENOUGH_OXYGEN, typeof(atom_movable_screen_alert_not_enough_oxy)) as atom_movable_screen_alert);
        Assert.NotNull(alert);
        r.Step(1);
        r.In(() => Assert.True(Dm.@in(alert, bob.client.screen)));

        r.Command(() => ClientSeam.Hover(bob.client, alert));
        r.Step(1);
        TooltipOutput shown = Assert.Single(Taken(r, bob));
        Assert.Equal(TooltipOutputKind.Show, shown.Kind);
        Assert.Equal("<h1>Choking (No O2)</h1><p>You're not getting enough oxygen. Find some good air before you pass out! The box in your backpack has an oxygen tank and breath mask in it.</p>", shown.Text);
        Assert.Equal("midnight", shown.Theme); // alerttooltipstyle "": the ui_style preference's, Midnight by default (tooltip.dm:113)
        Assert.Equal("none", shown.Special);
        Assert.Contains("\"screenLoc\": \"" + r.In(() => alert.screen_loc) + "\"", shown.Params);

        r.Command(() => ClientSeam.Unhover(bob.client));
        r.Step(1);
        Assert.Equal(TooltipOutputKind.Hide, Assert.Single(Taken(r, bob)).Kind);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void HoveringAnActionButtonShowsItsActionAndMovingToAnotherHidesFirst()
    {
        var (r, bob) = Room(1903);
        obj_item_tank_internals_oxygen tank = r.In(() =>
        {
            var t = Dm.@new<obj_item_tank_internals_oxygen>(bob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(t))));
            return t;
        });
        r.Step(1);
        atom_movable_screen_movable_action_button button = r.In(() =>
        {
            datum_action action = Assert.Single(Dm.each_cast<datum_action>(tank.actions));
            return action.viewers[bob.hud_used] as atom_movable_screen_movable_action_button;
        });
        Assert.NotNull(button);

        r.Command(() => ClientSeam.Hover(bob.client, button));
        r.Step(1);
        TooltipOutput shown = Assert.Single(Taken(r, bob));
        (string name, object desc) = r.In(() => (button.name, button.desc));
        Assert.Equal("Set Internals", name);
        Assert.Equal(Dm.truthy(desc) ? "<h1>Set Internals</h1><p>" + desc + "</p>" : "<p>Set Internals</p>", shown.Text);
        Assert.Equal("midnight", shown.Theme);

        // Onto the alert of the HUD's palette: BYOND exits the button first (its hide), then enters the palette (its show).
        atom_movable_screen palette = r.In(() => bob.hud_used.screen_objects[HUD_MOB_TOGGLE_PALETTE] as atom_movable_screen);
        r.Command(() => ClientSeam.Hover(bob.client, palette));
        r.Step(1);
        List<TooltipOutput> moved = Taken(r, bob);
        Assert.Equal(new[] { TooltipOutputKind.Hide, TooltipOutputKind.Show }, moved.Select(o => o.Kind).ToArray());
        Assert.Contains(r.In(() => Dm.str(palette.name)), moved[1].Text);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnItemInAHandShowsItsTooltipAfterTheDelayWithItsForce()
    {
        var (r, bob) = Room(1904);
        obj_item_tank_internals_oxygen tank = r.In(() =>
        {
            var t = Dm.@new<obj_item_tank_internals_oxygen>(bob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(t))));
            return t;
        });
        r.Step(1);
        r.In(() => Assert.True(ClientSeam.IsScreenTarget(bob.client, tank)));

        // Left before the tooltip delay (500 ms, the preference's default): nothing shows, the hide still runs.
        r.Command(() => ClientSeam.Hover(bob.client, tank));
        r.Step(2);
        r.Command(() => ClientSeam.Unhover(bob.client));
        r.Step(Second);
        Assert.Equal(new[] { TooltipOutputKind.Hide }, Taken(r, bob).Select(o => o.Kind).ToArray());

        r.Command(() => ClientSeam.Hover(bob.client, tank));
        r.Step(5);
        Assert.Empty(Taken(r, bob)); // the timer is 5 deciseconds
        r.Step(Second);
        TooltipOutput shown = Assert.Single(Taken(r, bob));
        (object desc, object forceString) = r.In(() => (tank.desc, tank.force_string));
        Assert.Equal("<h1>oxygen tank</h1><p>" + desc + "<br><b>Force:</b> " + forceString + "</p>", shown.Text);
        Assert.Equal("medium", forceString); // tg's set_force_string: the oxygen tank's force 10 is 7 to 10 (tank_types.dm:33)
        Assert.Equal("midnight", shown.Theme);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDatumFollowsTgsRules()
    {
        var (r, bob) = Room(1905);
        r.In(() =>
        {
            datum_tooltip tip = bob.client.tooltips;
            var thing = Dm.@new<atom_movable_screen>(null);
            thing.screen_loc = "WEST,NORTH";

            // No params, no title nor content: nothing.
            Assert.Equal(Dm.FALSE, tip.show(thing, null, "Title", "Text"));
            Assert.Equal(Dm.FALSE, tip.show(thing, "screen-loc=WEST,NORTH", "", ""));
            Assert.Empty(bob.client.dm_tooltip_output);

            // A title alone is a paragraph; its \improper goes.
            Assert.Equal(Dm.TRUE, tip.show(thing, "screen-loc=WEST,NORTH", Dm.text("\\improper Oxygen"), null, "midnight"));
            TooltipOutput only = Assert.Single(bob.client.dm_tooltip_output);
            Assert.Equal("<p>Oxygen</p>", only.Text);
            Assert.Equal("midnight", only.Theme);
            Assert.Same(thing, tip.last_target);
            Assert.Equal(1f, tip.init);
            Assert.Equal(0f, tip.showing);
            bob.client.dm_tooltip_output.Clear();

            // A content alone is a paragraph.
            tip.show(thing, "screen-loc=WEST,NORTH", null, "Just text");
            Assert.Equal("<p>Just text</p>", bob.client.dm_tooltip_output[^1].Text);
            bob.client.dm_tooltip_output.Clear();

            // openToolTip's theme: the caller's, else the ui_style preference's (Midnight by default), else default.
            openToolTip(bob, thing, "screen-loc=WEST,NORTH", "A", "B", "alien");
            Assert.Equal("alien", bob.client.dm_tooltip_output[^1].Theme);
            openToolTip(bob, thing, "screen-loc=WEST,NORTH", "A", "B");
            Assert.Equal("midnight", bob.client.dm_tooltip_output[^1].Theme);
            bob.client.dm_tooltip_output.Clear();

            // A hide while a show is under way waits 0.1 s (the timer); a hide otherwise is at once.
            tip.showing = 1;
            tip.hide();
            Assert.Equal(Dm.TRUE, tip.queueHide);
            Assert.Empty(bob.client.dm_tooltip_output);
            tip.showing = 0;
        });
        r.Step(3);
        r.In(() =>
        {
            Assert.Equal(TooltipOutputKind.Hide, Assert.Single(bob.client.dm_tooltip_output).Kind);
            bob.client.dm_tooltip_output.Clear();

            // The shown target deleted: the tooltip hides and forgets it.
            datum_tooltip tip = bob.client.tooltips;
            var target = Dm.@new<atom_movable_screen>(null);
            tip.show(target, "screen-loc=1,1", "T", "C");
            bob.client.dm_tooltip_output.Clear();
            qdel(target);
            Assert.Null(tip.last_target);
        });
        r.Step(1);
        r.In(() => Assert.Equal(TooltipOutputKind.Hide, Assert.Single(bob.client.dm_tooltip_output).Kind));
        r.AssertNoRuntimes();
    }
}
