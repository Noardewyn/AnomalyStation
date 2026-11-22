using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M4c;

/// <summary>
/// M4c slice 4 (D146, DV40): tg's show_radial_menu (_onclick/hud/screen_objects/radial.dm) as a waiting call the client answers.
/// The menu is listed on the player's client (the port's show_to) with the current page's slices (tg's SetElement: names, the
/// choices' pictures, the slice backgrounds), the call waits (stoplag(1), radial.dm:377) until the client's pick (the slice's Click,
/// ClientSeam.RadialPick) and returns the picked choice; the centre button closes it with no answer; moving away from a require_near
/// menu, a custom check that fails at its check delay, a repeat of the same menu and the client leaving end it with no answer (tg's
/// menu has no time limit of its own). More choices than a page takes add tg's next-page slice; its pick turns the page.
/// </summary>
public class RadialTests
{
    private const int Second = 20;

    private sealed class Pending
    {
        public bool Done;
        public object Answer = "unanswered";
    }

    private static (AudioRoom r, mob_living_carbon_human bob) Room(ulong seed)
    {
        var r = new AudioRoom(9, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        return (r, bob);
    }

    // show_radial_menu run as a proc of its own (tg's callers are procs that wait for it).
    private static Pending Show(AudioRoom r, mob user, atom anchor, DmList choices, float? require_near = null, datum_callback custom_check = null,
        float? autopick = null, object uniqueid = null, float? close_button = null)
    {
        var pending = new Pending();
        r.In(() => spawn(-1, async () =>
        {
            pending.Answer = await show_radial_menu(user, anchor, choices, uniqueid: uniqueid, custom_check: custom_check, require_near: require_near,
                autopick_single_option: autopick, display_close_button: close_button);
            pending.Done = true;
        }));
        return pending;
    }

    private static datum_radial_menu Shown(AudioRoom r, mob who) => r.In(() => Dm.each_cast<datum_radial_menu>(who.client.dm_radial_menus).LastOrDefault());

    private static void Pick(AudioRoom r, mob who, datum_radial_menu menu, int element)
    {
        r.Command(() => ClientSeam.RadialPick(who.client, menu.dm_id, element));
        r.Step(4); // the pick, then the wait's stoplag(1) (two ticks) wakes and returns
    }

    private static DmList Choices(AudioRoom r, params (string Key, object Picture)[] items)
        => r.In(() =>
        {
            DmList list = Dm.list();
            foreach (var (key, picture) in items)
                list[key] = picture;
            return list;
        });

    private static object RadialIcon(string state) => Dm.icon(Dm.resource("icons/hud/radial.dmi"), state);

    [Fact]
    public void TheMenuWaitsUntilTheClientPicksAndReturnsThePickedChoice()
    {
        var (r, bob) = Room(7801);
        obj_item_clothing_mask_breath mask = r.In(() => Dm.@new<obj_item_clothing_mask_breath>(locate(5, 4, 1)));
        DmList choices = Choices(r, ("open", RadialIcon("radial_open")), ("mask", mask));
        Pending pending = Show(r, bob, mask, choices);
        r.Step(10);
        Assert.False(pending.Done, "the menu waits for the player");

        datum_radial_menu menu = Shown(r, bob);
        Assert.NotNull(menu);
        r.In(() =>
        {
            Assert.Equal(2, menu.dm_elements.Count);
            RadialElement open = menu.dm_elements[0], picture = menu.dm_elements[1];
            Assert.Equal("open", open.Name);
            Assert.Equal("c_0", open.Choice);
            Assert.Equal("radial_open", ((DmShim.icon)open.Icon.icon).dm_state); // icon('icons/hud/radial.dmi', "radial_open") in the slice
            Assert.Equal(RADIAL_CONTENT_LAYER, open.Icon.layer);
            Assert.Equal("radial_slice", open.Background.icon_state);
            Assert.Equal("radial_slice_focus", open.BackgroundFocus.icon_state);
            Assert.Equal("mask", picture.Name); // a text key names its slice; the atom is its picture (new /mutable_appearance(atom))
            Assert.Equal(mask.icon_state, picture.Icon.icon_state);
            Assert.NotNull(menu.dm_close_button); // display_close_button: radial_center
            Assert.Equal("radial_center", menu.dm_close_button.icon_state);
            Assert.Equal(1f, menu.pages);
        });

        Pick(r, bob, menu, 2);
        Assert.True(pending.Done);
        Assert.Equal("mask", pending.Answer);
        Assert.Empty(r.In(() => Dm.each_cast<datum_radial_menu>(bob.client.dm_radial_menus).ToList()));
        r.Step(Second);
        Assert.NotNull(r.In(() => menu.gc_destroyed)); // remove_menu: qdel
        r.AssertNoRuntimes();
    }

    [Fact]
    public void OneChoiceIsPickedAtOnceUnlessAutopickIsOff()
    {
        var (r, bob) = Room(7802);
        DmList one = Choices(r, ("open", RadialIcon("radial_open")));
        Pending at_once = Show(r, bob, bob, one);
        Assert.True(at_once.Done);
        Assert.Equal("open", at_once.Answer);
        Assert.Null(Shown(r, bob));

        Pending waits = Show(r, bob, bob, one, autopick: FALSE);
        r.Step(5);
        Assert.False(waits.Done);
        Pick(r, bob, Shown(r, bob), 1);
        Assert.Equal("open", waits.Answer);

        Pending none = Show(r, bob, bob, Choices(r));
        Assert.True(none.Done);
        Assert.Null(none.Answer);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCentreButtonClosesTheMenuWithNoAnswer()
    {
        var (r, bob) = Room(7803);
        DmList choices = Choices(r, ("a", RadialIcon("radial_open")), ("b", RadialIcon("radial_lock")));
        Pending pending = Show(r, bob, bob, choices);
        r.Step(2);
        Pick(r, bob, Shown(r, bob), 0);
        Assert.True(pending.Done);
        Assert.Null(pending.Answer);
        Assert.Null(Shown(r, bob));

        // Without a centre button a centre pick is a click on nothing.
        Pending no_centre = Show(r, bob, bob, choices, close_button: FALSE);
        r.Step(2);
        datum_radial_menu menu = Shown(r, bob);
        Assert.Null(r.In(() => menu.dm_close_button));
        Pick(r, bob, menu, 0);
        Assert.False(no_centre.Done);
        Pick(r, bob, menu, 1);
        Assert.Equal("a", no_centre.Answer);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void MovingAwayFromARequireNearMenuEndsItWithNoAnswer()
    {
        var (r, bob) = Room(7804);
        obj_item_clothing_mask_breath anchor = r.In(() => Dm.@new<obj_item_clothing_mask_breath>(locate(5, 4, 1)));
        DmList choices = Choices(r, ("a", RadialIcon("radial_open")), ("b", RadialIcon("radial_lock")));
        Pending pending = Show(r, bob, anchor, choices, require_near: TRUE);
        r.Step(5);
        Assert.False(pending.Done);
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(8, 8, 1))));
        r.Step(2);
        Assert.True(pending.Done);
        Assert.Null(pending.Answer);
        Assert.Null(Shown(r, bob));

        // A pick that arrives while the player is out of reach is refused too (radial.dm:440).
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(4, 4, 1))));
        Pending again = Show(r, bob, anchor, choices, require_near: TRUE);
        r.Step(2);
        datum_radial_menu menu = Shown(r, bob);
        r.Command(() =>
        {
            ClientSeam.RadialPick(bob.client, menu.dm_id, 1);
            Waiting.NoSleep(bob.forceMove(locate(8, 8, 1)));
        });
        r.Step(2);
        Assert.True(again.Done);
        Assert.Null(again.Answer);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AFailedCustomCheckEndsTheMenuAtItsCheckDelay()
    {
        var (r, bob) = Room(7805);
        var unit = r.In(() => Dm.@new<obj_machinery_suit_storage_unit>(locate(5, 4, 1)));
        datum_callback check = r.In(() => Dm.@new<datum_callback>(unit, nameof(obj_machinery_suit_storage_unit.check_interactable), bob));
        DmList choices = Choices(r, ("a", RadialIcon("radial_open")), ("b", RadialIcon("radial_lock")));
        Pending pending = Show(r, bob, unit, choices, custom_check: check);
        r.Step(Second);
        Assert.False(pending.Done);
        r.In(() => unit.panel_open = TRUE); // check_interactable is FALSE with the panel open
        r.Step(Second); // DEFAULT_CHECK_DELAY is 2 seconds: not yet checked again
        Assert.False(pending.Done);
        r.Step(Second + 2);
        Assert.True(pending.Done);
        Assert.Null(pending.Answer);
        Assert.Null(Shown(r, bob));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void MoreChoicesThanAPageAddTheNextPageSliceAndItsPickTurnsThePage()
    {
        var (r, bob) = Room(7806);
        var items = Enumerable.Range(1, 10).Select(i => ("choice" + i, RadialIcon("radial_open"))).ToArray();
        Pending pending = Show(r, bob, bob, Choices(r, items));
        r.Step(2);
        datum_radial_menu menu = Shown(r, bob);
        int serial = r.In(() => menu.dm_serial);
        r.In(() =>
        {
            Assert.Equal(2f, menu.pages); // max_elements 8 (360 / 45): 7 choices and the next-page slice, then 3 and the slice
            Assert.Equal(8, menu.dm_elements.Count);
            Assert.Equal("choice7", menu.dm_elements[6].Name);
            Assert.True(menu.dm_elements[7].NextPage);
            Assert.Equal("Next Page", menu.dm_elements[7].Name);
            Assert.Equal("radial_next", menu.dm_elements[7].Icon.icon_state);
        });
        Pick(r, bob, menu, 8);
        Assert.False(pending.Done);
        r.In(() =>
        {
            Assert.Equal(2f, menu.current_page);
            Assert.True(menu.dm_serial > serial, "the page's records changed");
            Assert.Equal(4, menu.dm_elements.Count);
            Assert.Equal("choice8", menu.dm_elements[0].Name);
        });
        Pick(r, bob, menu, 3);
        Assert.Equal("choice10", pending.Answer);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ARepeatOfTheSameMenuClosesItAndAnotherPlayersPickIsIgnored()
    {
        var (r, bob) = Room(7807);
        mob_living_carbon_human alice = r.Join(5, 5, "Alice");
        r.Step(Second);
        DmList choices = Choices(r, ("a", RadialIcon("radial_open")), ("b", RadialIcon("radial_lock")));
        Pending first = Show(r, bob, bob, choices);
        r.Step(2);
        datum_radial_menu menu = Shown(r, bob);

        Pick(r, alice, menu, 1); // not shown to Alice: a click on nothing
        Assert.False(first.Done);

        Pending repeat = Show(r, bob, bob, choices); // the same uniqueid (defmenu_[REF(user)]_[REF(anchor)]): finished
        Assert.True(repeat.Done);
        Assert.Null(repeat.Answer);
        r.Step(2);
        Assert.True(first.Done);
        Assert.Null(first.Answer);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheClientLeavingEndsTheMenu()
    {
        var (r, bob) = Room(7808);
        DmList choices = Choices(r, ("a", RadialIcon("radial_open")), ("b", RadialIcon("radial_lock")));
        Pending pending = Show(r, bob, bob, choices);
        r.Step(2);
        client player = r.In(() => bob.client);
        r.Command(() => ClientSeam.Disconnect(player));
        r.Step(Second);
        Assert.True(pending.Done);
        Assert.Null(pending.Answer);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheToolboxsQuickToolUseShowsItsToolsAndUsesThePickedOne()
    {
        var (r, bob) = Room(7809);
        var toolbox = r.In(() =>
        {
            var box = Dm.@new<obj_item_storage_toolbox_mechanical>(bob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(box))));
            return box;
        });
        r.Step(2);
        r.Sounds.Heard.Clear();
        atom floor = r.In(() => (atom)locate(5, 4, 1));
        r.Command(() => ClientSeam.Click(bob.client, floor, "icon-x=16;icon-y=16;left=1;button=left"));
        r.Step(4);
        datum_radial_menu menu = Shown(r, bob);
        Assert.NotNull(menu); // tg's _toolbox.dm:78: the radial of the tools inside
        List<string> names = r.In(() => menu.dm_elements.Select(e => e.Name).ToList());
        Assert.Contains("screwdriver", names);
        Assert.Single(r.Sounds.Of(bob, "sound/items/handling/toolbox/toolbox_open.ogg"));
        obj_item screwdriver = r.In(() => Dm.each_cast<atom>(toolbox.contents).OfType<obj_item_screwdriver>().First());
        Pick(r, bob, menu, names.IndexOf("screwdriver") + 1);
        // tg's quick tool use: the tool comes out into the other hand, is used on the target (melee_attack_chain) and goes back into
        // the toolbox (use_tool_on, _toolbox.dm:114); the rustle of the take and of the put back.
        Assert.Equal(2, r.Sounds.Of(bob, "sound/items/handling/toolbox/toolbox_rustle.ogg").Count);
        Assert.Same(toolbox, r.In(() => screwdriver.loc));
        Assert.Null(Shown(r, bob));
        r.AssertNoRuntimes();
    }
}
