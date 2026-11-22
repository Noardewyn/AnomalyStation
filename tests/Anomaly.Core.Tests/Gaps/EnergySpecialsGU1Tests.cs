using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package GU1 (D333), the special energy guns of `guns/energy/special.dm` and `beam_rifle.dm` with their casings and
/// projectiles, in a booted toy room with Bob as a player: the plasma cutter drills rock and flies on through it, takes plasma for
/// charge and welds on its charge; the wormhole projector refuses to fire without a core, then opens a blue portal with the left
/// button and an orange one with the right, linked to each other; the gravity gun's bolts throw loose things away from and toward
/// where they hit; the tesla cannon sparks while folded, unfolds in tg's 1.5 s and shocks its target; the marksman revolver flips a
/// coin on the right button and regains it, and a shot on a coin splits into two at the nearby men; the ion carbine's seclight mount
/// and the Event Horizon's scope (it refuses to fire unscoped); the photon cannon's light and its lit bolt; the Mind Flayer's ray, the
/// mime's finger gun bullet and the honkmother's banana; the meteor gun's meteor; and every type of the package spawns live.
/// </summary>
public class EnergySpecialsGU1Tests
{
    /// <summary>Every type the package made live or translated procs of here (special.dm, beam_rifle.dm, their casings and projectiles).</summary>
    internal static readonly Type[] AllTypes =
    {
        typeof(obj_item_gun_energy_ionrifle), typeof(obj_item_gun_energy_ionrifle_carbine), typeof(obj_item_gun_energy_floragun), typeof(obj_item_gun_energy_meteorgun),
        typeof(obj_item_gun_energy_meteorgun_pen), typeof(obj_item_gun_energy_mindflayer), typeof(obj_item_gun_energy_plasmacutter), typeof(obj_item_gun_energy_plasmacutter_adv),
        typeof(obj_item_gun_energy_wormhole_projector), typeof(obj_item_gun_energy_wormhole_projector_core_inserted), typeof(obj_item_gun_energy_gravity_gun),
        typeof(obj_item_gun_energy_tesla_cannon), typeof(obj_item_gun_energy_marksman_revolver), typeof(obj_item_gun_energy_photon), typeof(obj_item_gun_energy_event_horizon),
        typeof(obj_item_ammo_casing_energy_gravity), typeof(obj_item_ammo_casing_energy_gravity_repulse), typeof(obj_item_ammo_casing_energy_gravity_attract),
        typeof(obj_item_ammo_casing_energy_gravity_chaos), typeof(obj_item_ammo_casing_energy_wormhole), typeof(obj_item_ammo_casing_energy_wormhole_orange),
        typeof(obj_item_ammo_casing_energy_event_horizon), typeof(obj_projectile_beam_event_horizon), typeof(obj_projectile_beam_mindflayer), typeof(obj_projectile_beam_wormhole),
        typeof(obj_projectile_beam_wormhole_orange), typeof(obj_projectile_bullet_honker), typeof(obj_projectile_bullet_mime), typeof(obj_projectile_bullet_marksman),
        typeof(obj_projectile_bullet_coin), typeof(obj_projectile_energy_photon), typeof(obj_projectile_energy_tesla), typeof(obj_projectile_energy_tesla_revolver),
        typeof(obj_projectile_energy_tesla_cannon), typeof(obj_projectile_energy_tesla_cannon__2), typeof(obj_projectile_gravityrepulse), typeof(obj_projectile_gravityattract),
        typeof(obj_projectile_gravitychaos), typeof(obj_projectile_meteor), typeof(obj_projectile_plasma), typeof(obj_projectile_plasma_adv),
        typeof(obj_projectile_plasma_adv_mech), typeof(obj_projectile_plasma_turret),
    };

    private const int LineY = 4;

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who, Action<T> setup = null) where T : obj_item
    {
        T item = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        if (setup != null)
            r.In(() => setup(item));
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_active_hand(item)))));
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    // tg's try_fire_gun, as the click chain calls it (the right button adds "right=1")
    private static object Pull(AudioRoom r, mob_living_carbon_human shooter, obj_item_gun gun, atom target, bool right = false) =>
        r.In(() => Waiting.NoSleep(gun.try_fire_gun(target, shooter, "icon-x=16;icon-y=16" + (right ? ";right=1" : ""))));

    private static List<T> Fired<T>(AudioRoom r) where T : obj_projectile => r.In(() => Dm.each<T>().Where(p => Dm.truthy(p.fired) && !QDELETED(p)).ToList());

    private static void UntilGone(AudioRoom r, obj_projectile shot)
    {
        for (int i = 0; i < 6 * Second && !r.In(() => QDELETED(shot)); i++)
            r.Step();
        Assert.True(r.In(() => QDELETED(shot)), "the shot is still flying");
    }

    private static bool Heard(AudioRoom r, string file) => r.Sounds.Heard.Any(h => (h.File ?? "").Contains(file, StringComparison.Ordinal));

    private static string Said(Feed feed) => string.Join(" | ", feed.Chat.Select(c => c.Html));

    private static turf At(AudioRoom r, int x, int y) => r.In(() => locate(x, y, 1));

    private static turf Rock(AudioRoom r, int x, int y) =>
        r.In(() => (turf)Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_closed_mineral_iron))));

    private static string Examine(AudioRoom r, atom a, mob who) => r.In(() => string.Join("\n", Dm.each(Waiting.NoSleep(a.examine(who))).Select(o => Dm.str(o))));

    [Fact]
    public void EveryTypeSpawnsLiveIsExaminedAndGoes()
    {
        var (r, _, bob) = Room(9501);
        var failures = new List<string>();
        foreach (Type type in AllTypes)
        {
            if (InertSeam.IsInertType(type))
                failures.Add(type.Name + " is inert");
            atom thing = r.In(() => (atom)Dm.@new(type, locate(6, LineY, 1)));
            r.Step(2);
            r.In(() => Waiting.NoSleep(thing.examine(bob)));
            r.In(() => { if (!QDELETED(thing)) qdel(thing); });
            r.Step(2);
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
        r.Step(2 * Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePlasmaCutterDrillsRockTakesPlasmaForChargeAndWeldsOnItsCharge()
    {
        var (r, feed, bob) = Room(9502);
        var cutter = Hold<obj_item_gun_energy_plasmacutter>(r, bob);
        Assert.Equal(TOOL_WELDER, r.In(() => Dm.str(cutter.tool_behaviour)));
        Assert.Contains("% charged.", Examine(r, cutter, bob));
        Rock(r, 5, LineY);
        r.Step(2);

        float full = r.In(() => cutter.cell.charge);
        Assert.True(Dm.truthy(Pull(r, bob, cutter, At(r, 5, LineY))), "the cutter did not fire");
        r.Step(Second);
        // the blast drills the rock (gets_drilled: the ore drops, the turf opens) and its sound is the cutter's
        Assert.False(r.In(() => locate(5, LineY, 1) is turf_closed_mineral), "the rock is still there");
        Assert.True(Heard(r, "weapons/plasma_cutter"), r.Sounds.Dump());
        float spent = r.In(() => cutter.cell.charge);
        Assert.True(spent < full, "no charge was spent");

        // a plasma sheet gives 0.2 * STANDARD_CELL_CHARGE * 2 (capped at the cell's max)
        var sheets = r.In(() => Dm.@new<obj_item_stack_sheet_mineral_plasma>(bob.loc, 5));
        r.Step(2);
        Assert.True(Dm.eq(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(cutter.item_interaction(bob, sheets)))), "the sheet did not charge the cell");
        Assert.Equal(Math.Min(r.In(() => cutter.cell.maxcharge), spent + 0.4f * STANDARD_CELL_CHARGE), r.In(() => cutter.cell.charge), 1);
        Assert.Equal(4f, r.In(() => Dm.num(sheets.amount)));
        Assert.True(feed.Any("balloon: cell recharged"), Said(feed));
        r.In(() => cutter.cell.charge = cutter.cell.maxcharge);
        Assert.True(Dm.eq(ITEM_INTERACT_BLOCKING, r.In(() => Waiting.NoSleep(cutter.item_interaction(bob, sheets)))), "a full cell took the sheet");
        Assert.True(feed.Any("balloon: already fully charged!"), Said(feed));

        // welding: an instant use of amount 1 spends PLASMA_CUTTER_CHARGE_WELD (0.025 * STANDARD_CELL_CHARGE), the sparks come and go
        turf floor = At(r, 4, LineY);
        float before = r.In(() => cutter.cell.charge);
        Assert.Equal(TRUE, r.In(() => Dm.num(Waiting.NoSleep(cutter.use_tool(floor, bob, 0, 1)))));
        Assert.Equal(before - 0.025f * STANDARD_CELL_CHARGE, r.In(() => cutter.cell.charge), 1);
        Assert.Empty(r.In(() => Dm.each(floor.overlays).Where(o => o is IDmAppearance a && a.icon_state == "welding_sparks").ToList()));
        // an empty cell refuses the weld
        r.In(() => cutter.cell.charge = 0);
        Assert.False(r.In(() => Dm.truthy(cutter.tool_use_check(bob, 1))));
        Assert.True(feed.Any("balloon: not enough charge!"), Said(feed));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheWormholeProjectorNeedsACoreThenOpensLinkedBlueAndOrangePortals()
    {
        var (r, feed, bob) = Room(9503);
        var coreless = Hold<obj_item_gun_energy_wormhole_projector>(r, bob);
        Assert.Contains("right-click", Examine(r, coreless, bob));
        Pull(r, bob, coreless, At(r, 13, LineY));
        r.Step(2);
        Assert.True(feed.Any("NO CORE INSTALLED"), Said(feed));
        Assert.Empty(r.In(() => Dm.each<obj_effect_portal>().ToList()));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(coreless)));

        var projector = Hold<obj_item_gun_energy_wormhole_projector_core_inserted>(r, bob);
        // left button: the blue beam to the east wall, a blue portal where it stops
        Assert.True(Dm.truthy(Pull(r, bob, projector, At(r, 13, LineY))), "the blue beam did not fire");
        r.Step(2);
        obj_effect_portal blue = r.In(() => projector.p_blue);
        Assert.NotNull(blue);
        Assert.Null(r.In(() => projector.p_orange));
        Assert.True(Heard(r, "weapons/pulse3"), r.Sounds.Dump());

        // right button: the projector switches to orange (its icon follows the mode), the orange portal links with the blue one
        r.Step(2 * Second);
        Assert.True(Dm.truthy(Pull(r, bob, projector, At(r, 3, 9), right: true)), "the orange beam did not fire");
        r.Step(2);
        Assert.Equal(2f, r.In(() => projector.select));
        Assert.Equal("wormhole_projector2", r.In(() => projector.icon_state));
        obj_effect_portal orange = r.In(() => projector.p_orange);
        Assert.NotNull(orange);
        Assert.Equal("portal1", r.In(() => orange.icon_state));
        Assert.Same(orange, r.In(() => blue.linked));
        Assert.Same(blue, r.In(() => orange.linked));
        Assert.Equal(2, r.In(() => Dm.each<obj_effect_portal>().Count(p => !QDELETED(p))));

        // a new blue portal replaces the old one; the projector forgets a portal that goes
        r.Step(2 * Second);
        Assert.True(Dm.truthy(Pull(r, bob, projector, At(r, 3, 1))), "the second blue beam did not fire");
        r.Step(2);
        Assert.Equal(1f, r.In(() => projector.select));
        Assert.True(r.In(() => QDELETED(blue)));
        Assert.NotSame(blue, r.In(() => projector.p_blue));
        r.In(() => qdel(orange));
        r.Step(2);
        Assert.Null(r.In(() => projector.p_orange));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheGravityGunsBoltsThrowLooseThingsAwayFromAndTowardWhereTheyHit()
    {
        var (r, feed, bob) = Room(9504);
        var gun = Hold<obj_item_gun_energy_gravity_gun>(r, bob);
        Pull(r, bob, gun, At(r, 13, LineY));
        r.Step(2);
        Assert.Empty(Fired<obj_projectile>(r)); // no gravitational anomaly core yet
        r.In(() => gun.firing_core = TRUE);

        // repulse: a crowbar two tiles past the man is thrown away from the hit
        mob_living_carbon_human rob = r.Dummy(7, LineY);
        var crowbar = r.In(() => Dm.@new<obj_item_crowbar>(locate(8, LineY + 1, 1)));
        r.Step(2);
        Assert.True(Dm.truthy(Pull(r, bob, gun, rob)), "the repulse bolt did not fire");
        var bolt = r.In(() => Assert.Single(Dm.each<obj_projectile_gravityrepulse>().Where(p => Dm.truthy(p.fired)).ToList()));
        Assert.Equal(4f, r.In(() => Dm.num(bolt.power))); // the gun's power, through its casing
        UntilGone(r, bolt);
        turf hit = r.In(() => bolt.T);
        Assert.NotNull(hit);
        Assert.NotEmpty(r.In(() => Dm.each<obj_effect_temp_visual_gravpush>().ToList()));
        r.Step(2 * Second);
        Assert.True(r.In(() => get_dist(crowbar, hit)) > 2, "the crowbar was not thrown away");
        Assert.True(Heard(r, "weapons/wave"), r.Sounds.Dump());

        // attract: the mode switches in hand, and the next bolt pulls the crowbar to where it hits
        r.Step(4 * Second); // the casing's delay of 5 s
        r.In(() => Waiting.NoSleep(gun.attack_self(bob)));
        Assert.True(feed.Any("balloon: set to attract"), Said(feed));
        r.In(() => qdel(rob));
        mob_living_carbon_human sam = r.Dummy(9, LineY);
        r.In(() => Waiting.NoSleep(crowbar.forceMove(locate(11, LineY + 2, 1))));
        r.Step(2);
        Assert.True(Dm.truthy(Pull(r, bob, gun, sam)), "the attract bolt did not fire");
        var pull = r.In(() => Assert.Single(Dm.each<obj_projectile_gravityattract>().Where(p => Dm.truthy(p.fired)).ToList()));
        UntilGone(r, pull);
        turf pulled_to = r.In(() => pull.T);
        r.Step(2 * Second);
        Assert.True(r.In(() => get_dist(crowbar, pulled_to)) <= 1, "the crowbar was not pulled in");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheTeslaCannonSparksFoldedUnfoldsAndShocksItsTarget()
    {
        var (r, feed, bob) = Room(9505);
        var cannon = Hold<obj_item_gun_energy_tesla_cannon>(r, bob);
        mob_living_carbon_human rob = r.Dummy(6, LineY);
        r.Step(2);
        Assert.NotNull(r.In(() => cannon.GetComponent(typeof(datum_component_automatic_fire))));

        // folded: the trigger only arcs to the stock
        Assert.False(Dm.truthy(Pull(r, bob, cannon, rob)));
        Assert.True(feed.Any("balloon: electricity arcing to stock!"), Said(feed));
        Assert.Equal(0f, r.In(() => Dm.num(rob.get_fire_loss())));

        // unfolding takes tg's 1.5 s
        r.Command(() => Waiting.Detach(cannon.attack_self(bob)));
        r.Step(Second);
        Assert.Equal(0f, r.In(() => cannon.ready_to_fire));
        r.Step(Second);
        Assert.Equal(1f, r.In(() => cannon.ready_to_fire));
        Assert.Equal("tesla_unfolded", r.In(() => cannon.icon_state));
        Assert.Equal((float)WEIGHT_CLASS_BULKY, r.In(() => cannon.w_class));
        Assert.True(feed.Any("balloon: unfolded stock"), Said(feed));
        Assert.True(Heard(r, "gun/tesla/click_creak"), r.Sounds.Dump());
        Assert.True(Heard(r, "gun/tesla/squeak_latch"), r.Sounds.Dump());

        // unfolded: the hitscan bolt and the shock burn the man
        float charge = r.In(() => cannon.cell.charge);
        Assert.True(Dm.truthy(Pull(r, bob, cannon, rob)), "the cannon did not fire");
        // DV77: the lightning names beam.dmi's states, tesla and tesla1 to tesla24 (tg's varied beam took Beam's alpha, 255, as its count)
        var segments = new List<string>();
        for (int t = 0; t < 4; t++)
        {
            segments.AddRange(r.In(() => Dm.each<obj_effect_ebeam>().Where(b => b.icon is not Anomaly.Core.DmShim.icon).Select(b => b.icon_state as string).ToList()));
            r.Step();
        }
        Assert.NotEmpty(segments);
        Assert.All(segments, state => Assert.True(state == "tesla" || Enumerable.Range(1, 24).Select(i => "tesla" + i).Contains(state), $"state [{state}]"));
        r.Step(Second);
        Assert.True(r.In(() => Dm.num(rob.get_fire_loss())) > 0, "the man was not burned");
        Assert.True(r.In(() => cannon.cell.charge) < charge);

        // folding is at once
        r.In(() => Waiting.NoSleep(cannon.attack_self(bob)));
        Assert.Equal(0f, r.In(() => cannon.ready_to_fire));
        Assert.Equal("tesla", r.In(() => cannon.icon_state));
        Assert.Equal((float)WEIGHT_CLASS_NORMAL, r.In(() => cannon.w_class));
        Assert.True(feed.Any("balloon: folded stock"), Said(feed));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMarksmanRevolverFlipsACoinRegainsItAndAShotOnACoinSplitsAtTheMenNearby()
    {
        var (r, feed, bob) = Room(9506);
        var revolver = Hold<obj_item_gun_energy_marksman_revolver>(r, bob);
        Assert.Contains("It currently has 4 out of 4 coins", Examine(r, revolver, bob));

        // the right button flips a coin toward the clicked tile
        Assert.True(Dm.eq(ITEM_INTERACT_SUCCESS, Pull(r, bob, revolver, At(r, 8, LineY), right: true)), "no coin was flipped");
        Assert.Equal(3f, r.In(() => revolver.coin_count));
        Assert.Single(r.In(() => Dm.each<obj_projectile_bullet_coin>().ToList()));
        Assert.True(Heard(r, "effects/coin2"), r.Sounds.Dump());
        Assert.True(feed.Any("You flip a coin towards"), Said(feed));
        // and it comes back (SSobj's process: a coin each coin_regen_rate, a ding)
        r.Step(6 * Second);
        Assert.Equal(4f, r.In(() => revolver.coin_count));
        Assert.True(Heard(r, "machines/ding"), r.Sounds.Dump());

        // a shot on a coin splits in two shots of twice its damage, at the two men within two tiles of it
        mob_living_carbon_human rob = r.Dummy(7, LineY);
        mob_living_carbon_human sam = r.Dummy(6, LineY + 2);
        var coin = r.In(() => Dm.@new<obj_projectile_bullet_coin>(locate(6, LineY, 1), locate(6, LineY, 1), bob));
        var shot = r.In(() => Dm.@new<obj_projectile_bullet_marksman>(locate(5, LineY, 1)));
        r.In(() =>
        {
            coin.firer = bob; // as aim_projectile sets it when the revolver flips the coin
            shot.firer = bob;
            shot.fired_from = revolver;
        });
        r.Step(2);
        r.In(() => Waiting.NoSleep(coin.check_splitshot(bob, shot, TRUE)));
        r.Step(Second);
        Assert.True(feed.Any("and splits!"), Said(feed));
        Assert.True(r.In(() => Dm.truthy(coin.used)));
        Assert.True(r.In(() => QDELETED(coin)), "the coin was not removed");
        float hurt = r.In(() => Dm.num(rob.get_brute_loss()) + Dm.num(sam.get_brute_loss()));
        Assert.True(hurt >= 60, "the split shots did not hit: " + hurt);
        r.In(() => qdel(shot));
        r.Step(2);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheIonCarbineHasItsSeclightMountAndTheEventHorizonFiresOnlyScoped()
    {
        var (r, feed, bob) = Room(9507);
        var carbine = r.In(() => Dm.@new<obj_item_gun_energy_ionrifle_carbine>(locate(5, LineY, 1)));
        r.Step(2);
        var mount = r.In(() => carbine.GetComponent(typeof(datum_component_seclite_attachable)) as datum_component_seclite_attachable);
        Assert.NotNull(mount);
        Assert.Equal(18f, r.In(() => mount.overlay_x));
        Assert.Equal(11f, r.In(() => mount.overlay_y));
        Assert.Equal("flight", r.In(() => Dm.str(mount.light_overlay)));

        var rifle = Hold<obj_item_gun_energy_event_horizon>(r, bob);
        Assert.NotNull(r.In(() => rifle.GetComponent(typeof(datum_component_scope))));
        mob_living_carbon_human rob = r.Dummy(7, LineY);
        r.Step(2);
        float charge = r.In(() => rifle.cell.charge);
        Pull(r, bob, rifle, rob);
        r.Step(2);
        Assert.True(feed.Any("balloon: must be scoped!"), Said(feed));
        Assert.Equal(charge, r.In(() => rifle.cell.charge));
        Assert.Empty(r.In(() => Dm.each<obj_reality_tear>().ToList()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePhotonCannonIsLitAndItsLitBoltStunsWhatItPasses()
    {
        var (r, _, bob) = Room(9508);
        var cannon = Hold<obj_item_gun_energy_photon>(r, bob);
        Assert.Equal(1f, r.In(() => cannon.light_on));
        mob_living_carbon_human rob = r.Dummy(6, LineY);
        r.Step(2);
        Assert.True(Dm.truthy(Pull(r, bob, cannon, rob)), "the photon cannon did not fire");
        var bolt = r.In(() => Assert.Single(Dm.each<obj_projectile_energy_photon>().Where(p => Dm.truthy(p.fired)).ToList()));
        Assert.Equal(1f, r.In(() => bolt.light_on));
        UntilGone(r, bolt);
        Assert.True(r.In(() => Dm.num(rob.get_stamina_loss())) >= 5, "the bolt did not hit the man");
        Assert.True(r.In(() => bolt.speed) < 1, "the bolt did not slow (reduce_range)");
        Assert.True(Heard(r, "weapons/lasercannonfire"), r.Sounds.Dump());
        r.AssertNoRuntimes();
    }

    // The bullets of bullets/special.dm and the mind flayer's ray, each shot at a man as its gun or spell fires it
    private static void ShootAt(AudioRoom r, mob_living_carbon_human from, Type projectile, atom target)
    {
        var shot = r.In(() => (obj_projectile)Dm.@new(projectile, get_turf(from)));
        r.In(() =>
        {
            shot.firer = from;
            Waiting.NoSleep(shot.aim_projectile(target, from));
            Waiting.NoSleep(shot.fire());
        });
        r.Step(Second);
    }

    [Fact]
    public void TheMindFlayerHurtsTheBrainTheFingerGunSilencesAndTheBananaSlips()
    {
        var (r, _, bob) = Room(9509);
        var flayer = Hold<obj_item_gun_energy_mindflayer>(r, bob);
        mob_living_carbon_human rob = r.Dummy(6, LineY);
        r.Step(2);
        Assert.True(Dm.truthy(Pull(r, bob, flayer, rob)), "the mind flayer did not fire");
        r.Step(Second);
        Assert.True(r.In(() => Dm.num(rob.get_organ_loss(ORGAN_SLOT_BRAIN))) >= 20, "the brain was not hurt");
        Assert.NotNull(r.In(() => rob.has_status_effect(typeof(datum_status_effect_hallucination))));

        mob_living_carbon_human sam = r.Dummy(6, LineY + 2);
        r.Step(2);
        ShootAt(r, bob, typeof(obj_projectile_bullet_mime), sam);
        Assert.NotNull(r.In(() => sam.has_status_effect(typeof(datum_status_effect_silenced))));
        Assert.True(r.In(() => Dm.num(sam.get_brute_loss())) > 0, "the finger gun's bullet did not hurt");

        mob_living_carbon_human tom = r.Dummy(7, LineY - 2);
        r.Step(2);
        ShootAt(r, bob, typeof(obj_projectile_bullet_honker), tom);
        Assert.True(Heard(r, "items/bikehorn"), r.Sounds.Dump());
        Assert.Equal((float)LYING_DOWN, r.In(() => tom.body_position));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMeteorGunsMeteorStrikesWithItsImpact()
    {
        var (r, _, bob) = Room(9510);
        var gun = Hold<obj_item_gun_energy_meteorgun>(r, bob);
        Assert.True(Dm.truthy(Pull(r, bob, gun, At(r, 13, LineY))), "the meteor gun did not fire");
        var meteor = r.In(() => Assert.Single(Dm.each<obj_projectile_meteor>().Where(p => Dm.truthy(p.fired)).ToList()));
        Assert.NotNull(r.In(() => meteor.GetComponent(typeof(datum_component_meteor_combat))));
        UntilGone(r, meteor);
        Assert.True(Heard(r, "effects/meteorimpact"), r.Sounds.Dump());
        r.Step(2 * Second);
        r.AssertNoRuntimes();
    }
}
