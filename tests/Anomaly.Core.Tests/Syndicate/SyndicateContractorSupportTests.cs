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
/// Syndicate stage X1b (D316): the contractor's support unit and the fulton kit. The support beacon polls the ghosts and drops the
/// chosen one in a pod as the Contractor Support Unit (tg's outfit with its lit syndicate cigarette, the one "follow orders"
/// objective, no uplink of its own). The fulton kit's beacon unfolds from its core and takes a name; the pack, linked to it, lifts a
/// person away on a balloon and sets them down next to the beacon.
/// </summary>
public class SyndicateContractorSupportTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed, int width = 30)
    {
        var r = new AudioRoom(width, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.In(() => Waiting.NoSleep(bob.equipOutfit(typeof(datum_outfit_job_assistant))));
        r.Step(Second);
        // CentCom's holding area for pods in transit, which the toy room lacks: its corner turf
        r.In(() =>
        {
            var lane = GLOB.areas_by_type[typeof(area_centcom_central_command_areas_supplypod_supplypod_temp_holding)] as area
                ?? Dm.@new<area_centcom_central_command_areas_supplypod_supplypod_temp_holding>();
            set_turf_to_area(locate(1, 1, 1), lane);
        });
        return (r, feed, bob);
    }

    private static T Held<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item => r.In(() =>
    {
        T item = Dm.@new<T>(who.loc);
        Waiting.NoSleep(who.put_in_active_hand(item));
        return item;
    });

    // an act in the newest window of that interface the player has open (tg's input windows)
    private static void Answer(AudioRoom r, mob who, string iface, object value)
    {
        r.Step(2);
        var open = r.In(() => Dm.each_cast<datum_tgui>(who.tgui_open_uis).Select(u => Dm.str(u.@interface)).ToList());
        Assert.True(open.Contains(iface), "no " + iface + " open; open: " + string.Join(", ", open));
        string window = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(who.tgui_open_uis).Last(u => Dm.str(u.@interface) == iface).window.id));
        r.Command(() => ClientSeam.TguiMessage(who.client, window, "act/submit", Dm.list(Dm.kv("entry", value))));
        r.Step(4);
    }

    [Fact]
    public void TheTypesAreLiveAndTheHubsEntriesSell()
    {
        foreach (Type type in new[]
        {
            typeof(datum_antagonist_traitor_contractor_support), typeof(datum_outfit_contractor_partner), typeof(obj_item_extraction_pack),
            typeof(obj_item_extraction_pack_syndicate), typeof(obj_item_fulton_core_syndicate), typeof(obj_structure_extraction_point_syndicate),
            typeof(obj_effect_extraction_holder), typeof(obj_item_storage_box_contractor_fulton_extraction),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
        var r = new AudioRoom(5, 5, seed: 3);
        foreach (Type entry in new[] { typeof(datum_uplink_item_contractor_extraction_kit), typeof(datum_uplink_item_contractor_partner), typeof(datum_uplink_item_contractor_pinpointer) })
            Assert.False(r.In(() => uplink_item_hidden((datum_uplink_item)Dm.@new(entry))), entry.Name + " is hidden");
    }

    [Fact]
    public void TheSupportBeaconDropsAGhostAsTheContractorSupportUnit()
    {
        var (r, feed, bob) = Room(151);
        mob_living_carbon_human body = r.Join(20, 2, "Gina");
        client gina = body.client;
        r.In(() => body.ghostize(TRUE));
        r.Step(2);
        mob_dead_observer ghost = Assert.IsType<mob_dead_observer>(gina.mob);
        var beacon = Held<obj_item_antag_spawner_loadout_contractor>(r, bob);
        r.In(() => Waiting.Detach(beacon.attack_self(bob)));
        r.Step(2);
        atom_movable_screen_alert_poll_alert alert = r.In(() => Dm.each(ghost.alerts).Select(k => ghost.alerts[k]).OfType<atom_movable_screen_alert_poll_alert>().SingleOrDefault());
        Assert.NotNull(alert);
        Click(r, ghost, alert);

        // the poll's 10 seconds: Gina is the support unit in a pod over Bob's turf
        r.Step(11 * Second);
        mob_living_carbon_human unit = r.In(() => gina.mob as mob_living_carbon_human);
        Assert.NotNull(unit);
        Assert.True(r.In(() => QDELETED(beacon)));
        var antag = r.In(() => unit.mind.has_antag_datum(typeof(datum_antagonist_traitor_contractor_support)) as datum_antagonist_traitor_contractor_support);
        Assert.NotNull(antag);
        var objectives = r.In(() => Dm.each<datum_objective>(antag.objectives).ToList());
        var order = Assert.Single(objectives);
        Assert.Equal("Follow Contractor's Orders", r.In(() => order.name));
        Assert.Equal(1f, r.In(() => order.completed));
        Assert.Null(r.In(() => antag.uplink_handler)); // no uplink of its own
        Assert.True(feed.Any("is your superior"));

        // tg's outfit: the chameleon kit, the syndicate toolbox, the lit cigarette
        Assert.IsType<obj_item_clothing_under_chameleon>(r.In(() => unit.w_uniform));
        Assert.IsType<obj_item_clothing_shoes_chameleon_noslip>(r.In(() => unit.shoes));
        var cig = Assert.IsType<obj_item_cigarette_syndicate>(r.In(() => unit.wear_mask));
        Assert.True(r.In(() => Dm.truthy(cig.lit)), "the cigarette is not lit");
        Assert.Contains(r.In(() => Dm.each(unit.held_items).ToList()), o => o is obj_item_storage_toolbox_syndicate);
        r.Step(9 * Second);
        Assert.Same(r.In(() => get_turf(bob)), r.In(() => unit.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheFultonPackLiftsAPersonToItsBeacon()
    {
        var (r, feed, bob) = Room(152);
        var box = Held<obj_item_storage_box_contractor_fulton_extraction>(r, bob);
        var contents = r.In(() => Dm.each(box.contents).ToList());
        Assert.Contains(contents, o => o is obj_item_extraction_pack_syndicate);
        Assert.Contains(contents, o => o is obj_item_fulton_core_syndicate);
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(box)));

        // the core unfolds into the beacon, which takes a name from its maker
        var core = Held<obj_item_fulton_core_syndicate>(r, bob);
        r.In(() => Waiting.Detach(core.attack_self(bob)));
        r.Step(2 * Second);
        Assert.True(r.In(() => QDELETED(core)));
        var beacon = r.In(() => Dm.each<obj_structure_extraction_point_syndicate>(get_turf(bob).contents).Single());
        Assert.StartsWith("fulton recovery beacon (", r.In(() => beacon.name));
        Answer(r, bob, "TextInputModal", "Dropoff");
        Assert.Equal("Dropoff", r.In(() => beacon.name));
        Assert.Contains(r.In(() => Dm.each(GLOB.total_extraction_beacons).ToList()), w => r.In(() => (w as datum_weakref).resolve()) == beacon);

        // away from the beacon, the pack is linked to it
        mob_living_carbon_human carol = r.Join(21, 4, "Carol");
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(20, 4, 1))));
        var pack = Held<obj_item_extraction_pack_syndicate>(r, bob);
        r.In(() => Waiting.Detach(pack.attack_self(bob)));
        Answer(r, bob, "ListInputWindow", "Dropoff");
        Assert.Same(beacon, r.In(() => pack.beacon_ref.resolve()));
        Assert.True(feed.Any("balloon: linked!"));

        // the pack on Carol: five seconds of attaching, then the balloon
        r.In(() => Waiting.Detach(pack.interact_with_atom(carol, bob, null)));
        r.Step(2);
        Assert.True(feed.Any("You are being extracted!"));
        r.Step(5 * Second + 2);
        var holder = r.In(() => carol.loc as obj_effect_extraction_holder);
        Assert.NotNull(holder);
        Assert.Equal(2f, r.In(() => pack.uses_left));
        Assert.True(r.In(() => HAS_TRAIT(carol, TRAIT_FORCED_STANDING)));
        Assert.True(r.In(() => Dm.truthy(carol.IsParalyzed())));
        r.Step(Second); // the balloon inflates after 0.4 s
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/fulton/fultext_deploy.ogg");

        // the lift (6 s), the flight (3 s), the descent next to the beacon (7 s) and the landing
        r.Step(17 * Second);
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/fulton/fultext_launch.ogg");
        Assert.True(r.In(() => QDELETED(holder)));
        var landed = r.In(() => get_turf(carol) as turf);
        Assert.True(r.In(() => get_dist(landed, beacon)) <= 1, "Carol is not next to the beacon");
        Assert.False(r.In(() => HAS_TRAIT(carol, TRAIT_FORCED_STANDING)));
        r.AssertNoRuntimes();
    }
}
