using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M5;

/// <summary>
/// M5 slice 1: tg's atom integrity (game/atom/atom_defense.dm, game/objects/obj_defense.dm) on the window and the grille in a
/// booted toy room: take_damage through the armour and damage_deflection, the break at integrity_failure, the destruction at
/// 0 with the window's debris, repair_damage, a thrown toolbox (throwforce, the demolition modifier), a melee hit with an
/// item (/obj/attacked_by), the heat of the air (the atmos_sensitive element) and fire_act, the attack sounds, and the
/// DV27 guard on a type whose own damage procs are not translated.
/// </summary>
public class IntegrityTests
{
    private const int Second = 20;
    private const string GlassHit = "sound/effects/glass/glasshit.ogg";
    private const string GrilleHit = "sound/effects/grillehit.ogg";

    private static T Place<T>(AudioRoom r, int x, int y) where T : atom => r.In(() => Dm.@new<T>(locate(x, y, 1)));

    private static float Integrity(AudioRoom r, atom a) => r.In(() => Dm.num(a.get_integrity()));

    private static object Hit(AudioRoom r, atom a, float amount, string type, object flag, float pen = 0)
        => r.In(() => Waiting.NoSleep(a.take_damage(amount, type, flag, TRUE, null, pen)));

    private static List<T> On<T>(AudioRoom r, int x, int y) => r.In(() => Dm.each_cast<atom>(locate(x, y, 1).contents).OfType<T>().ToList());

    [Fact]
    public void Dv378_AFullTileWindowHoldsWhatFallsOntoItsTileAndADirectionalOneDoesNot()
    {
        var r = new AudioRoom(10, 10, seed: 5103);
        Place<obj_structure_window_reinforced_fulltile>(r, 5, 5);
        Place<obj_structure_window>(r, 6, 5);
        r.Step();
        Assert.False(r.In(() => Dm.truthy(((turf)locate(5, 5, 1)).zPassIn(DOWN))));
        Assert.True(r.In(() => Dm.truthy(((turf)locate(6, 5, 1)).zPassIn(DOWN))));
    }

    [Fact]
    public void TakeDamageAppliesTheArmourTheDeflectionAndThePenetration()
    {
        var r = new AudioRoom(10, 10, seed: 5101);
        var window = Place<obj_structure_window_fulltile>(r, 5, 5);
        r.Step();
        Assert.Equal(r.In(() => window.max_integrity), Integrity(r, window));
        float melee = r.In(() => Dm.num(window.get_armor_rating(MELEE)));
        float fire = r.In(() => Dm.num(window.get_armor_rating(FIRE)));
        Assert.Equal(50f, melee); // /datum/armor/structure_window
        Assert.Equal(80f, fire);

        // 20 melee through 50 melee armour: round(20 * 0.5, 0.1) (atom_defense.dm:104)
        float start = Integrity(r, window);
        Assert.Equal(10f, Dm.num(Hit(r, window, 20, BRUTE, MELEE)));
        Assert.Equal(start - 10f, Integrity(r, window));
        // armour penetration 50 halves nothing left: PENETRATE_ARMOUR(50, 50) = 0, so the whole hit lands
        Assert.Equal(20f, Dm.num(Hit(r, window, 20, BRUTE, MELEE, pen: 50)));
        // burn through the fire armour: 10 * 0.2
        Assert.Equal(2f, Dm.num(Hit(r, window, 10, BURN, FIRE)), 3);
        // a damage type other than brute and burn does nothing (run_atom_armor :97)
        float before = Integrity(r, window);
        Assert.Null(Hit(r, window, 10, TOX, MELEE));
        Assert.Equal(before, Integrity(r, window));

        // damage_deflection ignores a melee hit below it (the reinforced window's 11)
        var rwindow = Place<obj_structure_window_reinforced_fulltile>(r, 6, 5);
        r.Step();
        Assert.Equal(11f, r.In(() => rwindow.damage_deflection));
        float rstart = Integrity(r, rwindow);
        Assert.Null(Hit(r, rwindow, 10, BRUTE, MELEE));
        Assert.Equal(rstart, Integrity(r, rwindow));
        // but not a hit of another flag
        Assert.NotNull(Hit(r, rwindow, 10, BRUTE, ""));
        Assert.Equal(rstart - 10f, Integrity(r, rwindow));

        // INDESTRUCTIBLE plays the sound and takes nothing
        r.In(() => Dm.combine(ref rwindow.resistance_flags, INDESTRUCTIBLE));
        Assert.Null(Hit(r, rwindow, 1000, BRUTE, ""));
        Assert.Equal(rstart - 10f, Integrity(r, rwindow));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AGrilleBreaksAtItsIntegrityFailureAndDropsARod()
    {
        var r = new AudioRoom(10, 10, seed: 5102);
        var grille = Place<obj_structure_grille>(r, 5, 5);
        r.Step();
        Assert.True(r.In(() => Dm.truthy(grille.density)));
        float max = r.In(() => grille.max_integrity);
        float failure = r.In(() => grille.integrity_failure) * max;
        // a hit that leaves it just above the failure point does not break it
        Hit(r, grille, max - failure - 1, BRUTE, "");
        Assert.False(r.In(() => Dm.truthy(grille.broken)));
        Hit(r, grille, 2, BRUTE, "");
        r.In(() =>
        {
            Assert.True(Dm.truthy(grille.broken), "atom_break at integrity_failure (atom_defense.dm:43)");
            Assert.False(Dm.truthy(grille.density));
            Assert.Equal(20f, Dm.num(grille.get_integrity())); // grille.dm:336
            Assert.Equal("brokengrille", grille.icon_state);
        });
        Assert.Single(On<obj_item_stack_rods>(r, 5, 5));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWindowIsDestroyedAtZeroAndLeavesItsDebris()
    {
        var r = new AudioRoom(10, 10, seed: 5103);
        mob_living_carbon_human bob = r.Join(3, 5, "Bob");
        var window = Place<obj_structure_window_reinforced_fulltile>(r, 5, 5);
        r.Step(Second);
        r.Sounds.Heard.Clear();
        Hit(r, window, 10000, BRUTE, "");
        Assert.True(r.In(() => QDELETED(window)), "atom_destruction at 0 deconstructs and deletes it");
        Assert.Equal(2, On<obj_item_shard>(r, 5, 5).Count); // a full-tile window: two shards (window.dm:366)
        Assert.Single(On<obj_item_stack_rods>(r, 5, 5)); // reinforced: rods
        Assert.Single(On<obj_effect_decal_cleanable_glass>(r, 5, 5)); // the glass material's debris
        Assert.Contains(r.Sounds.Heard, h => ReferenceEquals(h.Target, bob) && h.File != null && h.File.StartsWith("sound/effects/glass/glassbr", StringComparison.Ordinal));
        Assert.Contains(r.Sounds.Heard, h => ReferenceEquals(h.Target, bob) && h.File == GlassHit);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void RepairDamageRestoresIntegrityUpToTheMaximum()
    {
        var r = new AudioRoom(10, 10, seed: 5104);
        var window = Place<obj_structure_window_fulltile>(r, 5, 5);
        r.Step();
        float max = r.In(() => window.max_integrity);
        Hit(r, window, 40, BRUTE, ""); // no armour for a flagless hit
        Assert.Equal(max - 40f, Integrity(r, window));
        Assert.Equal(15f, Dm.num(r.In(() => Waiting.NoSleep(window.repair_damage(15)))));
        Assert.Equal(max - 25f, Integrity(r, window));
        Assert.Equal(25f, Dm.num(r.In(() => Waiting.NoSleep(window.repair_damage(1000)))));
        Assert.Equal(max, Integrity(r, window));
        Assert.Null(r.In(() => Waiting.NoSleep(window.repair_damage(-5)))); // only recovers
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AThrownToolboxDamagesAWindowByItsThrowforce()
    {
        var r = new AudioRoom(12, 10, seed: 5105);
        mob_living_carbon_human bob = r.Join(3, 5, "Bob");
        var window = Place<obj_structure_window_fulltile>(r, 6, 5);
        r.Step(Second);
        var box = r.In(() =>
        {
            var b = Dm.@new<obj_item_storage_toolbox_mechanical>(bob.loc);
            Waiting.NoSleep(bob.put_in_hands(b));
            bob.throw_mode_on(THROW_MODE_TOGGLE);
            return b;
        });
        float start = Integrity(r, window);
        // obj/hitby (obj_defense.dm:2): throwforce times the demolition modifier, through the melee armour
        float expected = r.In(() => round(box.throwforce * Dm.num(box.get_demolition_modifier(window)) * (100 - Dm.num(window.get_armor_rating(MELEE))) * 0.01f, DAMAGE_PRECISION));
        r.Sounds.Heard.Clear();
        r.Command(() => Waiting.Detach(bob.throw_item(window)));
        r.Step(2 * Second);
        Assert.Equal(start - expected, Integrity(r, window), 3);
        Assert.Contains(r.Sounds.Heard, h => ReferenceEquals(h.Target, bob) && h.File == GlassHit);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMeleeHitWithAnItemDamagesAWindowAndTheGrilleSoundsItsHit()
    {
        var r = new AudioRoom(12, 10, seed: 5106);
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        var window = Place<obj_structure_window_reinforced_fulltile>(r, 6, 5);
        var grille = Place<obj_structure_grille>(r, 4, 5);
        r.Step(Second);
        var box = r.In(() =>
        {
            var b = Dm.@new<obj_item_storage_toolbox_mechanical>(bob.loc);
            Waiting.NoSleep(bob.put_in_hands(b));
            // in combat mode: out of it, a toolbox with tools offers them in a radial (_toolbox.dm:51)
            bob.set_combat_mode(TRUE);
            return b;
        });
        // /obj/attacked_by (objs.dm:81): CALCULATE_FORCE times the demolition modifier (the toolbox's 1.25), through the armour
        float expected = r.In(() => round(box.force * Dm.num(box.get_demolition_modifier(window)) * (100 - Dm.num(window.get_armor_rating(MELEE))) * 0.01f, DAMAGE_PRECISION));
        float start = Integrity(r, window);
        r.Sounds.Heard.Clear();
        r.Command(() => ClientSeam.Click(bob.client, window, "left=1;icon-x=16;icon-y=16"));
        r.Step(Second);
        Assert.Equal(start - expected, Integrity(r, window), 3);
        Assert.Contains(r.Sounds.Heard, h => ReferenceEquals(h.Target, bob) && h.File == GlassHit);

        // the grille's own item_interaction (window placing, construction: M5 slice 5) is a stub, so a click is not a hit yet;
        // its attack sound (grille.dm:316) on a hit
        float gstart = Integrity(r, grille);
        r.Sounds.Heard.Clear();
        Hit(r, grille, 5, BRUTE, "");
        Assert.Equal(gstart - 5f, Integrity(r, grille));
        Assert.Contains(r.Sounds.Heard, h => ReferenceEquals(h.Target, bob) && h.File == GrilleHit);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void HotAirAndFireDamageAWindow()
    {
        var r = new AudioRoom(10, 10, seed: 5107);
        var window = Place<obj_structure_window_fulltile>(r, 5, 5);
        var hot = Place<obj_structure_window_fulltile>(r, 7, 5);
        r.Step(Second);

        // fire_act (obj_defense.dm:125): 0.02 * 1000 = 20 burn with the FIRE flag, through the fire armour 80
        float start = Integrity(r, window);
        r.In(() => Waiting.NoSleep(window.fire_act(1000, 100)));
        Assert.Equal(start - 4f, Integrity(r, window), 3);

        // the air's heat (the atmos_sensitive element, window.dm:439-443): above T0C + heat_resistance the window takes
        // round(volume / 100) burn each SSair cycle
        float hstart = Integrity(r, hot);
        r.In(() =>
        {
            var t = (turf_open)locate(7, 5, 1);
            t.air = SSair.parse_gas_string("n2=82;o2=22;TEMP=1500") as datum_gas_mixture;
            SSair.add_to_active(t);
            t.atmos_conditions_changed();
        });
        r.Step(10 * Second);
        Assert.True(Integrity(r, hot) < hstart || r.In(() => QDELETED(hot)), "the hot air damaged the window");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACrushDealsTheCollisionDamage()
    {
        var r = new AudioRoom(10, 10, seed: 5109);
        var grille = Place<obj_structure_grille>(r, 5, 5);
        var pusher = Place<obj_item_storage_toolbox_mechanical>(r, 4, 5);
        r.Step();
        float start = Integrity(r, grille);
        // obj_defense.dm:97: max(0, (force - move_resist * 3) / (move_resist * 3)) * 10, here (6000 - 3000) / 3000 * 10 = 10
        Assert.Equal(1000f, r.In(() => Dm.num(grille.move_resist)));
        Assert.Equal(TRUE, Dm.num(r.In(() => Waiting.NoSleep(grille.move_crushed(pusher, 6000, EAST)))));
        Assert.Equal(start - 10f, Integrity(r, grille));
        Assert.Equal(TRUE, Dm.num(r.In(() => Waiting.NoSleep(grille.force_pushed(pusher, 6000, EAST)))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AGrilleKeepsHearingItsTurfAfterTheTurfIsReplaced()
    {
        // the atmos_sensitive grille listens to its turf's COMSIG_TURF_EXPOSE; ChangeTurf hands the listeners to the new turf
        // (change_turf.dm), and in the port the grille's own record of what it listens to follows (TURF-IDENTITY)
        var r = new AudioRoom(10, 10, seed: 5110);
        var grille = Place<obj_structure_grille>(r, 5, 5);
        r.Step();
        turf replaced = r.In(() => (turf)Waiting.NoSleep(locate(5, 5, 1).ChangeTurf(typeof(turf_open_floor_plating))));
        Assert.Same(replaced, r.In(() => grille.loc));
        r.In(() =>
        {
            Assert.True(Dm.truthy(replaced._listen_lookup?[COMSIG_TURF_EXPOSE]));
            var air = ((turf_open)replaced).air;
            replaced._SendSignal(COMSIG_TURF_EXPOSE, Dm.list(replaced, air, 293.15f));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ATypeWhoseOwnDamageProcsAreNotTranslatedTakesNoDamage()
    {
        var r = new AudioRoom(10, 10, seed: 5108);
        // the meteor heart ruin's fluff has its own play_attack_sound, not translated: the guard refuses the damage (DV27)
        // (the locker of M5 slice 1 is translated since M5 slice 7, the cable since M5 slice 10, the disposal pipe since D325,
        // the cardboard cutout since syndicate G3c, the secure crate since syndicate G4c, the pinata since syndicate N2b, the
        // projected forcefield since gaps DV2)
        var closet = Place<obj_structure_meateor_fluff>(r, 5, 5);
        r.Step(Second);
        float start = Integrity(r, closet);
        Assert.Null(Hit(r, closet, 50, BRUTE, ""));
        Assert.Equal(start, Integrity(r, closet));
        Assert.True(r.In(() => InertSeam.MissingDamageOverride(closet)));
        var window = Place<obj_structure_window>(r, 6, 6);
        Assert.False(r.In(() => InertSeam.MissingDamageOverride(window)));
        r.AssertNoRuntimes();
    }
}
