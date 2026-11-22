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
/// Syndicate stage G4b (D316), the uplink's job-locked guns: the meat hook reels its victim in and recharges; the reagent gun shoots
/// its reagents into a target and synthesises a new dart; the self-charging pie cannon fills with cream pies and creams a face, which
/// a wash cleans; the tank transfer valve merges and splits its tanks, from its window and from a signaler on its fuse; the blast
/// cannon turns the bomb's explosion into a blast wave that breaks a window down the line and spares its holder; the syndicate
/// rebar crossbow comes boxed with its quiver, shoots, draws its string and reloads from the quiver; a sweep spawns and deletes each
/// newly live item.
/// </summary>
public class SyndicateGunsTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(int x = 3, int y = 5, ulong seed = 47)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(x, y, "Bob");
        r.In(() => Waiting.NoSleep(bob.equipOutfit(typeof(datum_outfit_job_assistant))));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static mob_living_carbon_human Alice(AudioRoom r, int x, int y = 5)
    {
        mob_living_carbon_human alice = r.Join(x, y, "Alice");
        r.In(() => Waiting.NoSleep(alice.equipOutfit(typeof(datum_outfit_job_assistant))));
        r.Step(Second);
        return alice;
    }

    private static T Held<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item => r.In(() =>
    {
        T item = Dm.@new<T>(who.loc);
        Waiting.NoSleep(who.put_in_active_hand(item));
        return item;
    });

    private static T Offhand<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item => r.In(() =>
    {
        T item = Dm.@new<T>(who.loc);
        Waiting.NoSleep(who.put_in_inactive_hand(item));
        return item;
    });

    [Fact]
    public void TheTypesAreLiveAndTheirEntriesSell()
    {
        foreach (Type type in new[]
        {
            typeof(obj_item_gun_magic), typeof(obj_item_gun_magic_hook), typeof(obj_projectile_hook), typeof(datum_hook_and_move),
            typeof(obj_item_gun_chem), typeof(obj_item_ammo_casing_chemgun), typeof(obj_item_pneumatic_cannon_pie_selfcharge),
            typeof(obj_item_food_pie_cream), typeof(datum_component_splat), typeof(datum_component_face_decal_splat), typeof(obj_item_gun_blastcannon),
            typeof(obj_projectile_blastwave), typeof(obj_item_transfer_valve), typeof(obj_item_gun_ballistic_rifle_rebarxbow_syndie),
            typeof(obj_item_storage_bag_rebar_quiver_syndicate), typeof(obj_item_ammo_casing_rebar_syndie), typeof(obj_projectile_bullet_rebar_syndie),
            typeof(datum_element_caseless), typeof(datum_element_projectile_drop), typeof(obj_item_storage_box_syndie_kit_rebarxbowsyndie),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
        var r = new AudioRoom(5, 5, seed: 3);
        foreach (Type entry in new[]
        {
            typeof(datum_uplink_item_role_restricted_meathook), typeof(datum_uplink_item_role_restricted_chemical_gun),
            typeof(datum_uplink_item_role_restricted_pie_cannon), typeof(datum_uplink_item_role_restricted_blastcannon),
            typeof(datum_uplink_item_role_restricted_rebarxbowsyndie),
        })
            Assert.False(r.In(() => uplink_item_hidden(Dm.@new(entry) as datum_uplink_item)), entry.Name + " is hidden");
    }

    [Fact]
    public void TheMeatHookReelsItsVictimInAndRecharges()
    {
        var (r, feed, bob) = Room(3, 5);
        mob_living_carbon_human alice = Alice(r, 8);
        var hook = Held<obj_item_gun_magic_hook>(r, bob);
        Assert.Equal(1f, r.In(() => hook.charges));
        r.In(() => Waiting.Detach(hook.process_fire(alice, bob)));
        bool held = false;
        for (int i = 0; i < 2 * Second; i++)
        {
            held |= r.In(() => HAS_TRAIT(bob, TRAIT_IMMOBILIZED));
            r.Step(1);
        }
        Assert.True(held, "the firer was not held in place while reeling");
        Assert.True(feed.Any("'s hook!"));
        Assert.True(r.In(() => get_dist(alice, bob) <= 1), "Alice was not reeled in to Bob"); // the last step bumps (and may swap with) Bob
        Assert.True(r.In(() => Dm.num(alice.get_brute_loss()) > 0));
        Assert.False(r.In(() => HAS_TRAIT(bob, TRAIT_IMMOBILIZED)));
        Assert.False(r.In(() => HAS_TRAIT(alice, obj_projectile_hook.TRAIT_HOOKED)));
        Assert.Equal(0f, r.In(() => hook.charges));

        r.In(() => Waiting.Detach(hook.shoot_with_empty_chamber(bob)));
        Assert.True(feed.Any("balloon: not ready yet!"));
        r.Step(9 * Second);
        Assert.Equal(1f, r.In(() => hook.charges));
        Assert.NotNull(r.In(() => hook.chambered.loaded_projectile));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheReagentGunShootsItsReagentsAndSynthesisesADart()
    {
        var (r, feed, bob) = Room(3, 5);
        mob_living_carbon_human alice = Alice(r, 7);
        var gun = Held<obj_item_gun_chem>(r, bob);
        r.In(() => Waiting.NoSleep(((datum_reagents)gun.reagents).add_reagent(typeof(datum_reagent_water), 30)));
        Assert.Equal(4f, r.In(() => gun.syringes_left));
        r.In(() => Waiting.Detach(gun.process_fire(alice, bob)));
        r.Step(Second);
        Assert.Equal(3f, r.In(() => gun.syringes_left));
        Assert.Equal(15f, r.In(() => Dm.num(((datum_reagents)gun.reagents).total_volume)));
        Assert.True(r.In(() => Dm.truthy(alice.has_reagent(typeof(datum_reagent_water)))), "the dart did not inject its water");

        r.Step(26 * Second);
        Assert.True(feed.Any("synthesizes a new dart."));
        Assert.Equal(4f, r.In(() => gun.syringes_left));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePieCannonFillsWithPiesAndCreamsAFace()
    {
        var (r, feed, bob) = Room(3, 5);
        mob_living_carbon_human alice = Alice(r, 7);
        var cannon = Held<obj_item_pneumatic_cannon_pie_selfcharge>(r, bob);
        Assert.Equal(TRUE, r.In(() => cannon.CanItemAutoclick()));
        r.Step(5 * Second);
        Assert.True(r.In(() => cannon.loadedItems.len) >= 1, "the cannon did not charge a pie");
        Assert.All(r.In(() => Dm.each<obj_item>(cannon.loadedItems).ToList()), pie => Assert.IsType<obj_item_food_pie_cream>(pie));
        float loaded = r.In(() => cannon.loadedItems.len);

        r.In(() => Waiting.Detach(cannon.ranged_interact_with_atom(alice, bob)));
        r.Step(3 * Second);
        Assert.True(feed.Any("Bob fires the pie cannon!") || feed.Any("You fire the pie cannon!"));
        Assert.True(feed.Any("is creamed by"), "the pie did not splat");
        var decal = r.In(() => alice.GetComponent(typeof(datum_component_face_decal_splat)) as datum_component_face_decal_splat);
        Assert.NotNull(decal);
        Assert.NotNull(r.In(() => decal.bodypart_overlay));
        Assert.Equal("creampie_human", r.In(() => Dm.str(decal.bodypart_overlay.icon_state)));
        Assert.NotEmpty(r.In(() => Dm.each<obj_effect_decal_cleanable_food_pie_smudge>(alice.loc).ToList()));

        r.In(() => Waiting.NoSleep(alice.wash(CLEAN_TYPE_BLOOD)));
        r.Step(2);
        Assert.Null(r.In(() => alice.GetComponent(typeof(datum_component_face_decal_splat))));
        r.AssertNoRuntimes();
    }

    // an act in the newest window of that interface the player has open
    private static void Act(AudioRoom r, mob who, string iface, string action)
    {
        r.Step(2);
        string window = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(who.tgui_open_uis).Last(u => Dm.str(u.@interface) == iface).window.id));
        r.Command(() => ClientSeam.TguiMessage(who.client, window, "act/" + action, Dm.list()));
        r.Step(4);
    }

    private static obj_item_transfer_valve Valve(AudioRoom r, mob_living_carbon_human bob)
    {
        var valve = Offhand<obj_item_transfer_valve>(r, bob);
        foreach (Type tank in new[] { typeof(obj_item_tank_internals_plasma), typeof(obj_item_tank_internals_oxygen) })
        {
            var t = r.In(() => Dm.@new(tank, bob.loc) as obj_item_tank);
            r.In(() => Waiting.NoSleep(valve.item_interaction(bob, t)));
        }
        Assert.NotNull(r.In(() => valve.tank_one));
        Assert.NotNull(r.In(() => valve.tank_two));
        return valve;
    }

    [Fact]
    public void TheTransferValveMergesAndSplitsItsTanks()
    {
        var (r, feed, bob) = Room(3, 5);
        var valve = Valve(r, bob);
        Assert.True(feed.Any("to the transfer valve's primary port."));
        Assert.True(feed.Any("to the transfer valve's secondary port."));
        Assert.Equal("valve", r.In(() => Dm.str(valve.icon_state)));
        float one = r.In(() => Dm.num((Waiting.NoSleep(valve.tank_one.return_air()) as datum_gas_mixture).volume));
        float two = r.In(() => Dm.num((Waiting.NoSleep(valve.tank_two.return_air()) as datum_gas_mixture).volume));

        // the valve's window
        var data = r.In(() => Waiting.NoSleep(valve.ui_data(bob)) as DmList);
        Assert.Equal("plasma tank", r.In(() => Dm.str(data["tank_one"])));
        r.In(() => Waiting.NoSleep(valve.ui_interact(bob)));
        Act(r, bob, "TransferValve", "toggle");
        Assert.Equal(TRUE, r.In(() => valve.valve_open));
        Assert.Equal(one + two, r.In(() => Dm.num((Waiting.NoSleep(valve.tank_two.return_air()) as datum_gas_mixture).volume)));
        Act(r, bob, "TransferValve", "toggle");
        Assert.Equal(FALSE, r.In(() => valve.valve_open));
        Assert.Equal(two, r.In(() => Dm.num((Waiting.NoSleep(valve.tank_two.return_air()) as datum_gas_mixture).volume)), 3);

        // a signaler on the fuse opens it
        var signaler = r.In(() => Dm.@new<obj_item_assembly_signaler>(bob.loc));
        r.In(() => Waiting.NoSleep(signaler.toggle_secure()));
        r.In(() => Waiting.NoSleep(valve.item_interaction(bob, signaler)));
        Assert.True(feed.Any("to the valve controls and secure it."));
        Assert.Same(valve, r.In(() => signaler.holder));
        Assert.Equal(TRUE, r.In(() => signaler.secured));
        r.In(() => Waiting.NoSleep(signaler.pulse()));
        Assert.Equal(TRUE, r.In(() => valve.valve_open));
        Assert.Equal(3, r.In(() => GLOB.bombers.len)); // the signaler's attachment (log_bomber, transfer_valve.dm) and each opening are logged

        // a tank ejected from the window splits the gas back
        Act(r, bob, "TransferValve", "tankone");
        Assert.Null(r.In(() => valve.tank_one));
        Assert.Equal(FALSE, r.In(() => valve.valve_open));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBlastCannonTurnsTheBombIntoABlastWave()
    {
        var (r, feed, bob) = Room(2, 5);
        var cannon = Held<obj_item_gun_blastcannon>(r, bob);
        var valve = Valve(r, bob);
        r.In(() => Waiting.NoSleep(cannon.item_interaction(bob, valve)));
        Assert.Same(valve, r.In(() => cannon.bomb));
        Assert.Equal("blastcannon_loaded", r.In(() => Dm.str(cannon.icon_state)));
        var window = r.In(() => Dm.@new<obj_structure_window_fulltile>(locate(7, 5, 1)));

        r.In(() => Waiting.NoSleep(cannon.try_fire_gun(locate(12, 5, 1), bob, "")));
        Assert.True(feed.Any("and point it at"));
        Assert.Equal(TRUE, r.In(() => valve.valve_open));

        // the bomb goes off inside the cannon: a blast wave instead of an explosion around Bob
        r.In(() => Waiting.Detach(explosion(valve.tank_two, 1, 2, 4)));
        r.Step(3 * Second);
        Assert.True(feed.Any("You fire a blast wave at"));
        Assert.True(r.In(() => QDELETED(window) || Dm.num(window.atom_integrity) < Dm.num(window.max_integrity)), "the blast wave did not reach the window");
        Assert.Equal(0f, r.In(() => Dm.num(bob.get_brute_loss())));
        Assert.Same(bob, r.In(() => cannon.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRebarCrossbowShootsDrawsAndReloadsFromItsQuiver()
    {
        var (r, feed, bob) = Room(3, 5);
        mob_living_carbon_human alice = Alice(r, 7);
        var kit = r.In(() => Dm.@new<obj_item_storage_box_syndie_kit_rebarxbowsyndie>(bob.loc));
        r.Step(2);
        var crossbow = r.In(() => Dm.each<obj_item_gun_ballistic_rifle_rebarxbow_syndie>(kit).Single());
        var quiver = r.In(() => Dm.each<obj_item_storage_bag_rebar_quiver_syndicate>(kit).Single());
        Assert.Single(r.In(() => Dm.each<obj_item_book_granter_crafting_recipe_dusting_rebarxbowsyndie_ammo>(kit).ToList()));
        Assert.Equal(20f, r.In(() => quiver.contents.len));
        // tg's quiver redraws when a rod goes in or out; the rods made inside it at its Initialize do not (item_init)
        Assert.Equal("syndie_quiver_0", r.In(() => Dm.str(quiver.icon_state)));
        Assert.NotNull(r.In(() => crossbow.GetComponent(typeof(datum_component_scope))));

        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(crossbow)));
        r.In(() => Waiting.NoSleep(bob.put_in_inactive_hand(quiver)));
        var rod = r.In(() => crossbow.chambered);
        Assert.IsType<obj_item_ammo_casing_rebar_syndie>(rod);
        r.In(() => Waiting.Detach(crossbow.process_fire(alice, bob)));
        r.Step(2 * Second);
        Assert.True(r.In(() => Dm.num(alice.get_brute_loss()) > 0), "the rod did not hit");
        Assert.True(r.In(() => QDELETED(rod)), "the rod's casing stayed (caseless)");
        Assert.Equal(1f, r.In(() => crossbow.bolt_locked));
        Assert.Equal(FALSE, r.In(() => crossbow.can_shoot()));

        // drawing the string chambers the second rod
        r.In(() => Waiting.Detach(crossbow.attack_self(bob)));
        r.Step(Second);
        Assert.True(feed.Any("balloon: bowstring drawn"));
        Assert.Equal(0f, r.In(() => crossbow.bolt_locked));
        Assert.NotNull(r.In(() => crossbow.chambered));

        // the quiver reloads the held crossbow
        float before = r.In(() => crossbow.magazine.contents.len);
        r.In(() => Waiting.Detach(quiver.reload_held_rebar(bob)));
        r.Step(2 * Second);
        Assert.Equal(before + 1, r.In(() => crossbow.magazine.contents.len));
        Assert.Equal(19f, r.In(() => quiver.contents.len));
        Assert.Equal("syndie_quiver_3", r.In(() => Dm.str(quiver.icon_state)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EveryNewItemSpawnsAndDeletesCleanly()
    {
        var (r, _, _) = Room();
        IEnumerable<Type> Live(Type root) => root.Assembly.GetTypes()
            .Where(t => (t == root || t.IsSubclassOf(root)) && !Attribute.IsDefined(t, typeof(DmInertAttribute), inherit: false));
        var types = Live(typeof(obj_item_gun_magic)).Concat(Live(typeof(obj_item_gun_chem))).Concat(Live(typeof(obj_item_pneumatic_cannon)))
            .Concat(Live(typeof(obj_item_food_pie_cream))).Concat(Live(typeof(obj_item_gun_blastcannon))).Concat(Live(typeof(obj_item_transfer_valve)))
            .Concat(Live(typeof(obj_item_gun_ballistic_rifle))).Concat(Live(typeof(obj_item_storage_bag_rebar_quiver))).Concat(Live(typeof(obj_item_ammo_casing_rebar)))
            .Concat(new[] { typeof(obj_item_storage_box_syndie_kit_rebarxbowsyndie), typeof(obj_item_ammo_casing_chemgun) });
        foreach (Type t in types)
        {
            var made = r.In(() => Dm.@new(t, locate(2, 2, 1)) as atom);
            r.Step(1);
            r.In(() => qdel(made));
            r.Step(1);
        }
        r.AssertNoRuntimes();
    }
}
