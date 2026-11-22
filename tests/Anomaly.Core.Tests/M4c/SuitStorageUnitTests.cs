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
/// M4c slice 4: tg's suit storage unit (game/machinery/suit_storage_unit.dm) through its radial (interact, show_radial_menu, DV40).
/// A standard unit starts with the EVA suit, helmet and breath mask behind its closed door (tg's overlays: lights closed, ready);
/// a click shows open, disinfect and lock; open shows close and the five slots (the empty ones as red silhouettes), a slot's pick
/// puts its item in the hand and the unit's overlays follow; an item in hand goes back in by its slot. Lock shows only unlock. The
/// UV cycle locks the unit and runs six 5-second cycles (lights red, UV on top), burns an occupant, washes the contents and opens
/// with airlockclose.ogg; a hacked cycle (uv_super) destroys the contents with airlock_alien_prying.ogg.
/// </summary>
public class SuitStorageUnitTests
{
    private const int Second = 20;
    private const string Close = "sound/machines/airlock/airlockclose.ogg";
    private const string Prying = "sound/machines/airlock/airlock_alien_prying.ogg";

    private static (AudioRoom r, mob_living_carbon_human bob, obj_machinery_suit_storage_unit unit) Room(ulong seed)
    {
        var r = new AudioRoom(9, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        var unit = r.In(() => Dm.@new<obj_machinery_suit_storage_unit_standard_unit>(locate(5, 4, 1)));
        r.Step(Second);
        return (r, bob, unit);
    }

    private static List<string> OverlayStates(AudioRoom r, atom a)
        => r.In(() => Dm.each_cast<object>(a.overlays).Select(o => o as string ?? ((IDmAppearance)o).icon_state).ToList());

    private static datum_radial_menu Shown(AudioRoom r, mob who) => r.In(() => Dm.each_cast<datum_radial_menu>(who.client.dm_radial_menus).LastOrDefault());

    private static List<string> Names(AudioRoom r, datum_radial_menu menu) => r.In(() => menu.dm_elements.Select(e => e.Name).ToList());

    private static void ClickUnit(AudioRoom r, mob who, atom unit)
    {
        r.Command(() => ClientSeam.Click(who.client, unit, "icon-x=16;icon-y=16;left=1;button=left"));
        r.Step(4);
    }

    // The player picks the slice named name of the menu shown to them.
    private static void Pick(AudioRoom r, mob who, string name)
    {
        datum_radial_menu menu = Shown(r, who);
        Assert.NotNull(menu);
        int element = Names(r, menu).IndexOf(name) + 1;
        Assert.True(element > 0, name + " is not in " + string.Join(", ", Names(r, menu)));
        r.Command(() => ClientSeam.RadialPick(who.client, menu.dm_id, element));
        r.Step(6);
    }

    [Fact]
    public void AStandardUnitStartsShutWithTheEvaSuitHelmetAndMask()
    {
        var (r, _, unit) = Room(7901);
        r.In(() =>
        {
            Assert.IsType<obj_item_clothing_suit_space_eva>(unit.suit_value);
            Assert.IsType<obj_item_clothing_head_helmet_space_eva>(unit.helmet_value);
            Assert.IsType<obj_item_clothing_mask_breath>(unit.mask_value);
            Assert.Null(unit.mod_value);
            Assert.Null(unit.storage);
            Assert.Same(unit, unit.suit_value.loc);
            Assert.Equal(1f, unit.density);
        });
        List<string> states = OverlayStates(r, unit);
        Assert.Contains("classic_lights_closed", states);
        Assert.Contains("classic_ready", states);
        Assert.DoesNotContain("classic_open", states);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRadialOpensTheUnitAndDispensesEachSlotIntoTheHand()
    {
        var (r, bob, unit) = Room(7902);
        ClickUnit(r, bob, unit);
        datum_radial_menu menu = Shown(r, bob);
        Assert.NotNull(menu);
        Assert.Equal(new[] { "open", "disinfect", "lock" }, Names(r, menu));
        Assert.Equal("radial_open", r.In(() => ((DmShim.icon)menu.dm_elements[0].Icon.icon).dm_state));

        Pick(r, bob, "open");
        Assert.Equal(1f, r.In(() => unit.state_open));
        Assert.Equal(0f, r.In(() => unit.density));
        List<string> open = OverlayStates(r, unit);
        Assert.Contains("classic_open", open);
        Assert.Contains("classic_suit", open);
        Assert.Contains("classic_helm", open);
        Assert.Contains("classic_lights_open", open);
        // interact(user) again: close and the five slots
        datum_radial_menu slots = Shown(r, bob);
        Assert.NotSame(menu, slots);
        Assert.Equal(new[] { "close", "suit", "helmet", "mask", "mod", "storage" }, Names(r, slots));
        r.In(() =>
        {
            mutable_appearance mod = slots.dm_elements[4].Icon; // no MOD: create_silhouette_of(/obj/item/mod/control)
            Assert.Equal(128f, mod.alpha);
            Assert.Equal(COLOR_RED, mod.color);
            Assert.Equal(unit.suit_value.icon_state, slots.dm_elements[1].Icon.icon_state); // the suit itself
        });

        obj_item suit = r.In(() => unit.suit_value);
        Pick(r, bob, "suit");
        Assert.Same(suit, r.In(() => bob.get_active_held_item()));
        Assert.Null(r.In(() => unit.suit_value));
        Assert.DoesNotContain("classic_suit", OverlayStates(r, unit));
        Assert.Equal(128f, r.In(() => Shown(r, bob).dm_elements[1].Icon.alpha)); // the suit's slot is a silhouette now

        r.In(() => bob.swap_hand());
        obj_item helmet = r.In(() => unit.helmet_value);
        Pick(r, bob, "helmet");
        Assert.Same(helmet, r.In(() => bob.get_active_held_item()));
        Assert.DoesNotContain("classic_helm", OverlayStates(r, unit));

        // An item in the active hand goes back in by its slot (try_insert_item).
        Pick(r, bob, "helmet");
        Assert.Same(helmet, r.In(() => unit.helmet_value));
        Assert.Same(unit, r.In(() => helmet.loc));
        Assert.Contains("classic_helm", OverlayStates(r, unit));

        Pick(r, bob, "close");
        Assert.Equal(0f, r.In(() => unit.state_open));
        Assert.Equal(1f, r.In(() => unit.density));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void LockLeavesOnlyUnlock()
    {
        var (r, bob, unit) = Room(7903);
        ClickUnit(r, bob, unit);
        Pick(r, bob, "lock");
        Assert.Equal(1f, r.In(() => unit.locked));
        Assert.Contains("classic_locked", OverlayStates(r, unit));
        Assert.Equal(new[] { "unlock" }, Names(r, Shown(r, bob))); // autopick_single_option is off
        Pick(r, bob, "unlock");
        Assert.Equal(0f, r.In(() => unit.locked));
        Assert.Contains("classic_ready", OverlayStates(r, unit));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheUvCycleLocksTheUnitForSixCyclesThenOpensWithItsSound()
    {
        var (r, bob, unit) = Room(7904);
        ClickUnit(r, bob, unit);
        r.Sounds.Heard.Clear();
        Pick(r, bob, "disinfect");
        Assert.Equal(1f, r.In(() => unit.uv));
        Assert.Equal(1f, r.In(() => unit.locked));
        List<string> cooking = OverlayStates(r, unit);
        Assert.Contains("classic_lights_red", cooking);
        Assert.Contains("classic_uv", cooking);
        Assert.Null(Shown(r, bob)); // check_interactable is FALSE while the UV runs: no menu

        ClickUnit(r, bob, unit); // nothing to do while it cooks
        Assert.Null(Shown(r, bob));

        r.Step(5 * 5 * Second); // five more cycles of 5 seconds
        Assert.Equal(1f, r.In(() => unit.uv));
        Assert.Equal(0f, r.In(() => unit.state_open));
        Assert.Empty(r.Sounds.Of(bob, Close));
        r.Step(5 * Second + 2); // the last call: the door slides open
        Assert.Equal(0f, r.In(() => unit.uv));
        Assert.Equal(0f, r.In(() => unit.locked));
        Assert.Equal(1f, r.In(() => unit.state_open));
        Assert.Equal(6f, r.In(() => unit.uv_cycles));
        HeardSound sound = Assert.Single(r.Sounds.Of(bob, Close));
        Assert.True(sound.Volume > 0);
        List<string> done = OverlayStates(r, unit);
        Assert.Contains("classic_lights_open", done);
        Assert.Contains("classic_suit", done); // the contents stay, washed
        Assert.NotNull(r.In(() => unit.suit_value));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheUvBurnsAnOccupantEachCycleWithTheSafetiesOff()
    {
        var (r, bob, unit) = Room(7905);
        mob_living_carbon_human dummy = r.Dummy(5, 4);
        r.In(() =>
        {
            unit.safeties = FALSE;
            unit.suit_value = null; // close_machine takes a mob only when it fits: the unit dumps nothing here, so make room
        });
        r.In(() => Waiting.NoSleep(unit.close_machine(dummy)));
        Assert.Same(dummy, r.In(() => unit.occupant));
        r.In(() => Waiting.NoSleep(unit.cook()));
        float burn = r.In(() => Dm.num(dummy.get_fire_loss()));
        Assert.InRange(burn, 10f, 16f); // rand(10, 16) a cycle
        r.Step(6 * 5 * Second + 2);
        Assert.InRange(r.In(() => Dm.num(dummy.get_fire_loss())), 60f, 96f);
        Assert.Null(r.In(() => unit.occupant)); // dumped when the door opens
        Assert.Same(r.In(() => unit.loc), r.In(() => dummy.loc));
    }

    [Fact]
    public void AHackedCycleDestroysTheContentsWithTheProyingSound()
    {
        var (r, bob, unit) = Room(7906);
        obj_item suit = r.In(() => unit.suit_value);
        r.In(() => unit.uv_super = TRUE);
        r.Sounds.Heard.Clear();
        r.In(() => Waiting.NoSleep(unit.cook()));
        Assert.Contains("classic_uvstrong", OverlayStates(r, unit));
        Assert.Contains("classic_super", OverlayStates(r, unit));
        r.Step(6 * 5 * Second + 2);
        Assert.Single(r.Sounds.Of(bob, Prying));
        Assert.Null(r.In(() => unit.suit_value));
        Assert.NotNull(r.In(() => suit.gc_destroyed));
        Assert.Equal(1f, r.In(() => unit.state_open));
    }
}
