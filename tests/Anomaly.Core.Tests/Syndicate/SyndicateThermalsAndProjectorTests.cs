using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Syndicate;

/// <summary>
/// Syndicate stage G1d (D316): the chameleon thermals and the chameleon projector sell; the thermals give the wearer SEE_MOBS and an EMP
/// overloads them (a flash, blindness and eye damage); the projector scans an object's look, hides its holder in a dummy that wears it
/// (its name too), creeps a tile at a time, and breaks when touched, then needs five seconds before it works again.
/// </summary>
public class SyndicateThermalsAndProjectorTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed = 111)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.In(() => Waiting.NoSleep(bob.equipOutfit(typeof(datum_outfit_job_assistant))));
        r.Step(Second);
        return (r, feed, bob);
    }

    [Fact]
    public void TheThermalsAndTheProjectorAreLiveAndSell()
    {
        foreach (Type type in new[]
        {
            typeof(obj_item_clothing_glasses_thermal), typeof(obj_item_clothing_glasses_thermal_syndi), typeof(obj_item_clothing_glasses_thermal_monocle),
            typeof(obj_item_clothing_glasses_thermal_eyepatch), typeof(obj_item_clothing_glasses_thermal_xray), typeof(obj_item_chameleon), typeof(obj_effect_dummy_chameleon),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
        var r = new AudioRoom(5, 5, seed: 3);
        foreach (Type entry in new[] { typeof(datum_uplink_item_device_tools_thermal), typeof(datum_uplink_item_stealthy_tools_chameleon_proj) })
            Assert.False(r.In(() => uplink_item_hidden((datum_uplink_item)Dm.@new(entry))), entry.Name + " is hidden");
    }

    [Fact]
    public void TheThermalsSeeMobsAndAnEmpOverloadsThem()
    {
        var (r, feed, bob) = Room(111);
        var thermals = r.In(() =>
        {
            var item = Dm.@new<obj_item_clothing_glasses_thermal_syndi>(bob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_or_del(item, ITEM_SLOT_EYES))));
            return item;
        });
        r.Step(2);
        Assert.NotEqual(0, (int)r.In(() => Dm.num(bob.sight)) & (int)SEE_MOBS);
        Assert.True(r.In(() => Dm.locate<datum_action_item_action_chameleon_change>(thermals.actions) != null), "no chameleon action");

        var eyes = r.In(() => bob.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes);
        float before = r.In(() => Dm.num(eyes.damage));
        r.In(() => Waiting.Detach(thermals.emp_act(EMP_HEAVY)));
        r.Step(2);
        Assert.True(feed.Any("overloads and blinds you!"));
        // the overload's 5 damage; one Life tick of the eyes' own healing (STANDARD_ORGAN_HEALING * maxHealth * 2 s = 0.05,
        // organ.dm on_life) may fall inside the two steps since gaps MS1's SSmood shares the MC's ticks
        Assert.InRange(r.In(() => Dm.num(eyes.damage)) - before, 4.95f - 1e-3f, 5f + 1e-3f);
        Assert.True(r.In(() => Dm.truthy(bob.has_status_effect(typeof(datum_status_effect_temporary_blindness)))), "not blinded");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheProjectorHidesItsHolderInAScannedLookThatBreaksWhenTouched()
    {
        var (r, feed, bob) = Room(112);
        mob_living_carbon_human alice = r.Join(6, 4, "Alice");
        var projector = r.In(() =>
        {
            var item = Dm.@new<obj_item_chameleon>(bob.loc);
            Waiting.NoSleep(bob.put_in_active_hand(item));
            return item;
        });
        var toolbox = r.In(() => Dm.@new<obj_item_storage_toolbox_mechanical>(locate(4, 4, 1)));
        r.Step(2);
        // nothing that is not an object's look is scanned
        Assert.Equal(NONE, r.In(() => Waiting.NoSleep(projector.interact_with_atom(alice, bob))));
        Assert.Equal(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(projector.interact_with_atom(toolbox, bob))));
        Assert.True(feed.Any("Scanned"));

        r.In(() => Waiting.NoSleep(projector.attack_self(bob)));
        Assert.True(feed.Any("You activate the chameleon projector."));
        var dummy = r.In(() => projector.active_dummy);
        Assert.NotNull(dummy);
        Assert.Same(dummy, r.In(() => bob.loc));
        Assert.Equal(r.In(() => toolbox.name), r.In(() => dummy.name));
        Assert.Equal(r.In(() => toolbox.icon_state), r.In(() => dummy.icon_state));

        // it creeps a tile at a time: a second step waits for its delay (a second at a warm body's temperature)
        var start = r.In(() => (turf)dummy.loc);
        r.In(() => Waiting.NoSleep(dummy.relaymove(bob, NORTH)));
        Assert.Equal(Dm.num(start.y) + 1, r.In(() => Dm.num(((turf)dummy.loc).y)));
        r.In(() => Waiting.NoSleep(dummy.relaymove(bob, NORTH)));
        Assert.Equal(Dm.num(start.y) + 1, r.In(() => Dm.num(((turf)dummy.loc).y)));
        r.Step(Second + 1);
        r.In(() => Waiting.NoSleep(dummy.relaymove(bob, SOUTH)));
        Assert.Equal(Dm.num(start.y), r.In(() => Dm.num(((turf)dummy.loc).y)));

        // a touch breaks it: the holder is put out on its tile and the projector rests for five seconds
        r.In(() => Waiting.NoSleep(dummy.attack_hand(alice)));
        Assert.True(feed.Any("Your chameleon projector deactivates."));
        Assert.True(r.In(() => isturf(bob.loc)), "Bob is still inside");
        Assert.Null(r.In(() => projector.active_dummy));
        Assert.True(r.In(() => QDELETED(dummy)));
        Assert.Equal(0f, r.In(() => projector.can_use));
        r.Step(6 * Second);
        Assert.Equal(1f, r.In(() => projector.can_use));

        // on and off again by hand
        r.In(() => Waiting.NoSleep(projector.attack_self(bob)));
        Assert.NotNull(r.In(() => projector.active_dummy));
        r.In(() => Waiting.NoSleep(projector.attack_self(bob)));
        Assert.True(feed.Any("You deactivate the chameleon projector."));
        Assert.True(r.In(() => isturf(bob.loc)), "Bob is still inside");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APlayersHeldMoveKeyCreepsTheDummy()
    {
        var room = new Anomaly.Core.Tests.Tg.M2Locomotion.P4.CapsuleRoom(seed: 113);
        mob_living_carbon_human bob = room.Join(4, 8, "Bob");
        var projector = room.In(() =>
        {
            var item = Dm.@new<obj_item_chameleon>(bob.loc);
            Waiting.NoSleep(bob.put_in_active_hand(item));
            Waiting.NoSleep(projector_toggle(item, bob));
            return item;
        });
        var dummy = room.In(() => projector.active_dummy);
        Assert.Same(dummy, room.In(() => bob.loc));
        room.Walk(bob, 127, 0, 40, 16384); // east, two seconds
        int x = room.In(() => (int)Dm.num(((turf)dummy.loc).x));
        Assert.InRange(x, 5, 7);
        Assert.Same(dummy, room.In(() => bob.loc));
        room.AssertNoRuntimes();
    }

    private static DmTask projector_toggle(obj_item_chameleon item, mob user) => item.toggle(user);
}
