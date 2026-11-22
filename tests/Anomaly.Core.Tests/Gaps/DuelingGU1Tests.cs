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
/// Gaps package GU1 (D333), fork F2: the dueling pistols, the laser gatling gun and the hand-cranked guns in a booted toy room with
/// Bob as a player: the dueling case's two pistols paired into one duel, a duel run from the positioning through the readiness and
/// the countdown to the shot that takes the opponent's head (the setting switched in hand, its overlay and the chaff's colour), the
/// safety of an unpaired or idle pistol; the minigun drawn from the pack on the back and fired (the pack heats, refills the gun's
/// cell, locks the trigger when hot and cools), snapped back when dropped; the laser musket cranked to a full cell (its sound, its
/// "charged" in-hand look) and fired; the thermal pistols spun only with the gun-flip trait; the inferno and cryo shots on a chilled
/// and an overheated man; every type the fork made live spawns live.
/// </summary>
public class DuelingGU1Tests
{
    private const int LineY = 4;

    /// <summary>Every atom type the fork made live (dueling.dm, laser_gatling.dm, crank_guns.dm, thermal.dm, the dueling case).</summary>
    internal static readonly Type[] AllTypes =
    {
        typeof(obj_item_gun_energy_dueling), typeof(obj_item_storage_lockbox_dueling), typeof(obj_effect_temp_visual_dueling_chaff),
        typeof(obj_item_ammo_casing_energy_duel), typeof(obj_projectile_energy_duel), typeof(obj_item_minigunpack),
        typeof(obj_item_gun_energy_laser_musket), typeof(obj_item_gun_energy_laser_musket_prime), typeof(obj_item_gun_energy_laser_musket_repeater),
        typeof(obj_item_gun_energy_disabler_smoothbore), typeof(obj_item_gun_energy_disabler_smoothbore_prime), typeof(obj_item_gun_energy_laser_thermal),
        typeof(obj_item_gun_energy_laser_thermal_inferno), typeof(obj_item_gun_energy_laser_thermal_cryo), typeof(obj_projectile_energy_inferno),
        typeof(obj_projectile_energy_cryo),
    };

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(14, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T item = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(who.put_in_hands(item)));
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    private static object Pull(AudioRoom r, mob_living shooter, obj_item_gun gun, atom target) =>
        r.In(() => Waiting.NoSleep(gun.try_fire_gun(target, shooter, "icon-x=16;icon-y=16")));

    private static void InHand(AudioRoom r, mob who, obj_item item) => r.In(() => Waiting.NoSleep(item.attack_self(who)));

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList());

    private static bool Heard(AudioRoom r, string file) => r.Sounds.Heard.Any(h => (h.File ?? "").Contains(file, StringComparison.Ordinal));

    private static string Said(Feed feed) => string.Join(" | ", feed.Chat.Select(c => c.Html));

    private static List<obj_projectile> Flying(AudioRoom r) => r.In(() => Dm.each<obj_projectile>().Where(p => Dm.truthy(p.fired) && !QDELETED(p)).ToList());

    // step until every fired shot is gone (hit or out of range)
    private static void LetFly(AudioRoom r)
    {
        for (int i = 0; i < 4 * Second && Flying(r).Count > 0; i++)
            r.Step();
    }

    [Fact]
    public void EveryTypeSpawnsLiveIsExaminedAndGoes()
    {
        var (r, _, bob) = Room(9551);
        var failures = new List<string>();
        foreach (Type type in AllTypes.Concat(new[] { typeof(obj_item_gun_energy_minigun), typeof(datum_duel), typeof(datum_component_crank_recharge) }))
        {
            if (InertSeam.IsInertType(type))
                failures.Add(type.Name + " is inert");
        }
        foreach (Type type in AllTypes)
        {
            atom thing = r.In(() => (atom)Dm.@new(type, locate(5, LineY, 1)));
            r.Step(2);
            if (r.In(() => QDELETED(thing)))
            {
                if (type != typeof(obj_effect_temp_visual_dueling_chaff))
                    failures.Add(type.Name + " deleted itself");
                continue;
            }
            r.In(() => Waiting.NoSleep(thing.examine(bob)));
            r.In(() => { if (!QDELETED(thing)) qdel(thing); });
            r.Step(2);
        }
        // the gatling gun is made inside its pack; alone it deletes itself (tg: no pack, no gun)
        obj_item_gun_energy_minigun lone = r.In(() => Dm.@new<obj_item_gun_energy_minigun>(locate(5, LineY, 1)));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(lone)), "a gatling gun without its pack");
        Assert.True(failures.Count == 0, string.Join("\n", failures));
        r.Step(2 * Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCasesTwoPistolsArePairedIntoOneDuelAndAnUnpairedOrIdlePistolDoesNotFire()
    {
        var (r, feed, bob) = Room(9552);
        mob_living_carbon_human rob = r.Dummy(8, LineY);
        obj_item_storage_lockbox_dueling box = r.In(() => Dm.@new<obj_item_storage_lockbox_dueling>(bob.loc));
        r.Step(2);
        List<obj_item_gun_energy_dueling> pistols = r.In(() => Dm.each<obj_item_gun_energy_dueling>(box.contents).ToList());
        Assert.Equal(2, pistols.Count);
        datum_duel duel = r.In(() => pistols[0].duel);
        Assert.NotNull(duel);
        r.In(() =>
        {
            Assert.Same(duel, pistols[1].duel);
            Assert.False(string.IsNullOrEmpty(duel.pairing_code));
            Assert.Equal((float)datum_duel.DUEL_IDLE, Dm.num(duel.state));
            Assert.Contains("duel_red", Dm.each(pistols[0].overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)));
            Assert.Contains("The pairing code is: " + duel.pairing_code, string.Join("\n", Dm.each(Waiting.NoSleep(pistols[0].examine_more(bob))).Select(o => Dm.str(o))), StringComparison.Ordinal);
        });

        // an unpaired pistol: its safety says so and nothing is fired
        obj_item_gun_energy_dueling loose = Hold<obj_item_gun_energy_dueling>(r, bob);
        Assert.False(Dm.truthy(Pull(r, bob, loose, rob)));
        Assert.True(feed.Any("is currently unpaired"), Said(feed));
        Assert.Empty(Flying(r));

        // two loose pistols used one on the other are paired
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(loose)));
        obj_item_gun_energy_dueling second = Hold<obj_item_gun_energy_dueling>(r, bob);
        r.In(() => Waiting.NoSleep(loose.item_interaction(bob, second)));
        Assert.True(feed.Any("Pairing established. Pairing code: "), Said(feed));
        r.In(() =>
        {
            Assert.NotNull(loose.duel);
            Assert.Same(loose.duel, second.duel);
        });

        // a paired pistol in an idle duel is locked until the FIRE signal
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(second)));
        obj_item_gun_energy_dueling taken = pistols[0];
        r.In(() => Waiting.NoSleep(bob.put_in_hands(taken)));
        r.In(() => Assert.Same(taken, bob.get_active_held_item()));
        Assert.False(Dm.truthy(Pull(r, bob, taken, rob)));
        Assert.True(feed.Any("is locked. Wait for FIRE signal before shooting."), Said(feed));
        Assert.Empty(Flying(r));

        // one pistol of a pair gone: the duel is cleared from the other
        r.In(() => qdel(loose));
        r.Step(2);
        r.In(() => Assert.Null(second.duel));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADuelRunsFromPositioningThroughTheCountdownToTheShotThatTakesTheOpponentsHead()
    {
        var (r, feed, bob) = Room(9553);
        mob_living_carbon_human rob = r.Dummy(8, LineY); // five tiles from Bob, a clear floor between them
        obj_item_storage_lockbox_dueling box = r.In(() => Dm.@new<obj_item_storage_lockbox_dueling>(bob.loc));
        r.Step(2);
        List<obj_item_gun_energy_dueling> pistols = r.In(() => Dm.each<obj_item_gun_energy_dueling>(box.contents).ToList());
        obj_item_gun_energy_dueling mine = pistols[0], his = pistols[1];
        r.In(() => Waiting.NoSleep(bob.put_in_hands(mine)));
        r.In(() => Waiting.NoSleep(rob.put_in_hands(his)));
        r.In(() => Assert.Same(his, rob.get_active_held_item()));
        datum_duel duel = r.In(() => mine.duel);

        // used in hand with both pistols held: the duel begins
        InHand(r, bob, mine);
        Assert.True(feed.Any("Set your gun setting and move 5 steps away from your opponent."), Said(feed));
        Assert.Equal((float)datum_duel.DUEL_PREPARATION, r.In(() => Dm.num(duel.state)));

        // SSobj (two seconds) checks the positions: five tiles apart, nothing between
        for (int i = 0; i < 3 * 2 * Second && r.In(() => Dm.num(duel.state)) != datum_duel.DUEL_READY; i++)
            r.Step();
        Assert.Equal((float)datum_duel.DUEL_READY, r.In(() => Dm.num(duel.state)));
        Assert.True(feed.Any("Position confirmed. Confirm readiness by pulling the trigger once."), Said(feed));

        // in a duel, the pistol used in hand switches its setting: wide (red) to cone (green)
        InHand(r, bob, mine);
        r.Step(2);
        r.In(() => Assert.Equal("cone", mine.setting));
        Assert.True(feed.Any("setting to cone mode"), Said(feed));
        Assert.Contains("duel_green", Overlays(r, mine));

        // the triggers pulled once confirm readiness and fire nothing
        Pull(r, bob, mine, rob);
        Pull(r, rob, his, bob);
        Assert.True(feed.Any("You confirm your readiness."), Said(feed));
        Assert.Empty(Flying(r));
        r.In(() => Assert.Equal(2f, duel.confirmations.len));

        // the countdown from ten, one step a cycle, to FIRE
        for (int i = 0; i < 14 * 2 * Second && r.In(() => Dm.num(duel.state)) != datum_duel.DUEL_FIRING; i++)
            r.Step();
        Assert.Equal((float)datum_duel.DUEL_FIRING, r.In(() => Dm.num(duel.state)));
        Assert.True(feed.Any("Readiness confirmed. Starting countdown."), Said(feed));
        Assert.True(feed.Any("9!") && feed.Any("1!") && feed.Any("Fire!"), Said(feed));

        // Bob fires: the chaff at his feet in his setting's colour, the beam homes on Rob and takes his head
        Assert.True(Dm.truthy(Pull(r, bob, mine, rob)), "the pistol did not fire on the FIRE signal");
        obj_effect_temp_visual_dueling_chaff chaff = r.In(() => Dm.locate<obj_effect_temp_visual_dueling_chaff>(bob.loc));
        Assert.NotNull(chaff);
        r.In(() =>
        {
            Assert.Equal("cone", Dm.str(chaff.setting));
            Assert.Equal("green", Dm.str(chaff.color));
        });
        LetFly(r);
        r.In(() =>
        {
            Assert.Null(rob.get_bodypart(BODY_ZONE_HEAD));
            Assert.True(Dm.truthy(duel.fired[mine]));
        });

        // one shot a duel: the pistol is locked again
        r.Step(Second);
        Assert.False(Dm.truthy(Pull(r, bob, mine, rob)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheGatlingGunIsDrawnFromThePackFiredHeatsThePackLocksWhenHotAndSnapsBackWhenDropped()
    {
        var (r, feed, bob) = Room(9554);
        mob_living_carbon_human rob = r.Dummy(9, LineY);
        obj_item_minigunpack pack = r.In(() => Dm.@new<obj_item_minigunpack>(bob.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(bob.equip_to_slot_or_del(pack, ITEM_SLOT_BACK)));
        r.In(() =>
        {
            Assert.Same(pack, bob.back);
            Assert.NotNull(pack.gun);
            Assert.Same(pack, pack.gun.loc);
            Assert.NotNull(pack.battery);
            Assert.Equal("holstered", pack.icon_state);
        });

        // clicked on the back: the gun in hand, the pack shows it out
        r.In(() => Waiting.NoSleep(pack.attack_hand(bob)));
        obj_item_gun_energy_minigun gun = r.In(() => pack.gun);
        r.In(() =>
        {
            Assert.Same(gun, bob.get_active_held_item());
            Assert.Equal(1f, pack.armed);
            Assert.Equal("notholstered", pack.icon_state);
        });
        r.In(() => Waiting.NoSleep(pack.attack_hand(bob)));
        Assert.True(feed.Any("You are already holding the gun!"), Said(feed));

        // a shot: the pack heats by one and its fusion core refills the gun's cell
        float coreBefore = r.In(() => pack.battery.charge);
        Assert.True(Dm.truthy(Pull(r, bob, gun, rob)), "the gatling gun did not fire");
        r.In(() =>
        {
            Assert.Equal(1f, pack.overheat);
            Assert.Equal(gun.cell.maxcharge, gun.cell.charge);
            Assert.True(pack.battery.charge < coreBefore, "the core gave nothing");
        });
        LetFly(r);
        Assert.True(r.In(() => Dm.num(rob.get_fire_loss())) > 0, "the laser did not burn Rob");

        // hot to the cap: the heat sensor locks the trigger; the heat diffuses on each SSobj cycle
        r.Step(Second);
        r.In(() => pack.overheat = pack.overheat_max);
        Assert.False(Dm.truthy(Pull(r, bob, gun, rob)));
        Assert.True(feed.Any("heat sensor locked the trigger"), Said(feed));
        r.Step(3 * Second);
        r.In(() => Assert.True(pack.overheat < pack.overheat_max, "the pack did not cool"));

        // dropped, the gun snaps back onto the pack
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(gun)));
        r.Step(2);
        r.In(() =>
        {
            Assert.Same(pack, gun.loc);
            Assert.Equal(0f, pack.armed);
            Assert.Equal("holstered", pack.icon_state);
            Assert.Null(bob.get_active_held_item());
        });
        Assert.True(feed.Any("You attach the laser gatling gun to the backpack power source."), Said(feed));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheLaserMusketIsCrankedToAFullCellWithItsSoundAndChargedLookAndFired()
    {
        var (r, feed, bob) = Room(9555);
        mob_living_carbon_human rob = r.Dummy(9, LineY);
        obj_item_gun_energy_laser_musket musket = Hold<obj_item_gun_energy_laser_musket>(r, bob);
        r.In(() => Assert.NotNull(musket.GetComponent(typeof(datum_component_crank_recharge))));
        r.In(() => Assert.Equal("musketcharged", musket.inhand_icon_state)); // a full cell (charge ratio 4)

        // a full cell does not take a crank
        InHand(r, bob, musket);
        Assert.True(feed.Any("balloon: already charged!"), Said(feed));

        // emptied, each crank (two seconds) gives half a standard cell, with the crank's sound
        r.In(() =>
        {
            musket.cell.charge = 0;
            musket.update_appearance();
        });
        r.In(() => Assert.Equal("musket", musket.inhand_icon_state));
        float max = r.In(() => musket.cell.maxcharge);
        InHand(r, bob, musket);
        Assert.True(feed.Any("balloon: charging..."), Said(feed));
        Assert.True(Heard(r, "weapons/laser_crank"), r.Sounds.Dump());
        r.Step(3 * Second);
        Assert.True(feed.Any("balloon: charged"), Said(feed));
        r.In(() => Assert.Equal(STANDARD_CELL_CHARGE * 0.5f, musket.cell.charge));
        for (int i = 0; i < 30 && r.In(() => musket.cell.charge) < max; i++)
        {
            InHand(r, bob, musket);
            r.Step(3 * Second);
        }
        r.In(() => Assert.Equal(max, musket.cell.charge));
        r.In(() => Assert.Equal("musketcharged", musket.inhand_icon_state));

        // fired: its laser burns Rob
        Assert.True(Dm.truthy(Pull(r, bob, musket, rob)), "the musket did not fire");
        LetFly(r);
        Assert.True(r.In(() => Dm.num(rob.get_fire_loss())) > 0, "the musket's laser did not burn Rob");
        r.In(() => Assert.True(musket.cell.charge < max));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheThermalPistolsAreSpunOnlyWithTheGunFlipTraitAndTheirShotsFloorAnOverheatedManAndBurstAChilledOne()
    {
        var (r, feed, bob) = Room(9556);
        mob_living_carbon_human rob = r.Dummy(10, LineY);
        obj_item_gun_energy_laser_thermal_cryo cryo = Hold<obj_item_gun_energy_laser_thermal_cryo>(r, bob);

        // spun to charge: not without the holster's gun-flip trait
        r.In(() =>
        {
            cryo.cell.charge = 0;
            cryo.update_appearance();
        });
        InHand(r, bob, cryo);
        Assert.True(feed.Any("balloon: need holster to spin!"), Said(feed));
        r.In(() => ADD_TRAIT(bob, TRAIT_GUNFLIP, "test"));
        InHand(r, bob, cryo);
        Assert.True(Heard(r, "weapons/kinetic_reload"), r.Sounds.Dump());
        r.Step(2 * Second);
        Assert.True(feed.Any("balloon: charged"), Said(feed));
        r.In(() => Assert.True(cryo.cell.charge > 0, "the spin gave no charge"));
        r.In(() => cryo.cell.charge = cryo.cell.maxcharge);
        r.Step(Second);

        // the frozen shot on a man heated far above his heat limit: knocked down, burnt, cooled back
        float normal = r.In(() => ((datum_species)rob.dna.species).bodytemp_normal);
        float burnBefore = r.In(() => Dm.num(rob.get_fire_loss()));
        r.In(() => rob.bodytemperature = ((datum_species)rob.dna.species).bodytemp_heat_damage_limit + 400);
        Assert.True(Dm.truthy(Pull(r, bob, cryo, rob)), "the cryo pistol did not fire");
        LetFly(r);
        r.In(() =>
        {
            Assert.True(Dm.truthy(rob.IsKnockdown()), "the cryo shot did not knock him down");
            Assert.True(Dm.num(rob.get_fire_loss()) >= burnBefore + 20 - 0.01f, "the cryo shot did not burn him");
            Assert.True(Math.Abs(rob.bodytemperature - normal) < 20, "the shot did not cool him: " + rob.bodytemperature);
        });
        Assert.True(Heard(r, "weapons/sonic_jackhammer"), r.Sounds.Dump());

        // the molten shot on a man chilled far below his cold limit: a burst of fire, and he is warm again
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(cryo)));
        obj_item_gun_energy_laser_thermal_inferno inferno = Hold<obj_item_gun_energy_laser_thermal_inferno>(r, bob);
        r.In(() => Waiting.NoSleep(rob.fully_heal()));
        r.Step(11 * Second); // the knockdown (10 s) is over
        r.In(() => rob.bodytemperature = ((datum_species)rob.dna.species).bodytemp_cold_damage_limit - 200);
        Assert.True(Dm.truthy(Pull(r, bob, inferno, rob)), "the inferno pistol did not fire");
        LetFly(r);
        r.In(() => Assert.True(rob.bodytemperature >= normal - 20, "the burst did not warm him back to normal: " + rob.bodytemperature));
        Assert.True(Heard(r, "weapons/sear"), r.Sounds.Dump());
        r.Step(2 * Second);
        r.AssertNoRuntimes();
    }
}
