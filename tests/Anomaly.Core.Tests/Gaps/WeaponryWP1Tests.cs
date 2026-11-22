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
/// Gaps package WP1 (D333): the weaponry leftovers, in a booted toy room with Bob as a player: the spear (its reach and armour
/// penetration in one hand and wielded; a wireprod and a plasma shard make a plasma spear), the explosive lance and the IED (a halved
/// pipe with fuel, a stuffing, cable and a timer), the chainsaw (its cord, its beheading of a dead body), the sabre (the assistant bane,
/// the sheath sounds), the chain of command's hasten, the baseball bat's knockback, the high frequency blade's slash and deflection,
/// the strobe shield's flash and burnt-out look, the headpike taken down, the Grey Tide's illusions, the canes, the supermatter
/// sword, the pillow lance's charge and the nunchaku's throw-mode block.
/// </summary>
public class WeaponryWP1Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
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

    private static bool Heard(AudioRoom r, string file) => r.Sounds.Heard.Any(h => (h.File ?? "").Contains(file, StringComparison.Ordinal));

    private sealed class FlickRecorder : IAppearanceSink
    {
        private readonly List<(object Target, object State)> _flicks;

        public FlickRecorder(List<(object Target, object State)> flicks) => _flicks = flicks;

        public void Changed(object atom, AppearanceAspect aspect) { }

        public void Animate(object target, DmList arguments) { }

        public void Flick(object target, object state) => _flicks.Add((target, state));

        public void LightChanged(object atom) { }

        public void VisibilityChanged(object atom) { }
    }

    [Fact]
    public void TheSpearReachesTwoTilesInOneHandAndOneWieldedWithTwiceThePenetration()
    {
        var (r, _, bob) = Room(9101);
        var spear = Hold<obj_item_spear>(r, bob);
        float pen = r.In(() =>
        {
            Assert.Equal(2f, spear.reach);
            Assert.Equal(10f, spear.force);
            Assert.Equal("spearglass0", spear.icon_state);
            return spear.armour_penetration;
        });
        r.In(() => Waiting.NoSleep(spear.attack_self(bob)));
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(spear, TRAIT_WIELDED));
            Assert.Equal(1f, spear.reach);
            Assert.Equal(18f, spear.force);
            Assert.Equal(pen * 2, spear.armour_penetration, 3);
            Assert.Equal("spearglass1", spear.icon_state);
        });
        r.In(() => Waiting.NoSleep(spear.attack_self(bob)));
        r.In(() =>
        {
            Assert.False(HAS_TRAIT(spear, TRAIT_WIELDED));
            Assert.Equal(2f, spear.reach);
            Assert.Equal(pen, spear.armour_penetration, 3);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWireprodAndAPlasmaShardMakeAPlasmaSpearInTheHandThatHeldTheProd()
    {
        var (r, feed, bob) = Room(9102);
        var prod = Hold<obj_item_wireprod>(r, bob);
        var shard = r.In(() => Dm.@new<obj_item_shard_plasma>(bob.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(prod.item_interaction(bob, shard)));
        r.Step(2);
        r.In(() =>
        {
            Assert.True(QDELETED(prod));
            Assert.True(QDELETED(shard));
            var spear = bob.get_active_held_item() as obj_item_spear;
            Assert.NotNull(spear);
            Assert.Equal("spearplasma", spear.icon_prefix);
            Assert.Equal("spearplasma0", spear.icon_state);
            Assert.IsType<datum_material_alloy_plasmaglass>(Waiting.NoSleep(spear.get_material_from_slot(typeof(datum_material_slot_weapon_head_speartip))));
        });
        Assert.True(feed.Any("tip."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHalvedPipeTakesFuelAStuffingCableAndATimerToMakeAnIedThatGoesOffOnItsTimer()
    {
        var (r, _, bob) = Room(9103);
        var pipe = Hold<obj_item_sliced_pipe>(r, bob);
        r.In(() => Waiting.NoSleep(((datum_reagents)pipe.reagents).add_reagent(typeof(datum_reagent_fuel), 10)));
        var meat = r.In(() => Dm.@new<obj_item_food_meat_slab_monkey>(bob.loc));
        r.Step(2);
        r.In(() => Assert.True(Dm.eq(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(pipe.item_interaction(bob, meat)))));
        var coil = r.In(() => Dm.@new<obj_item_stack_cable_coil>(bob.loc, 30));
        r.Step(2);
        r.In(() => Assert.True(Dm.eq(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(pipe.item_interaction(bob, coil)))));
        r.In(() =>
        {
            Assert.Equal(1f, pipe.wires_are_in);
            Assert.Equal("slicedapart-cable", pipe.icon_state);
            // tg's power: 5 * (0.5 * 10 / 20) - one stuffing / 2
            Assert.Equal(0.75f, Dm.num(pipe.power), 3);
        });
        var timer = r.In(() => Dm.@new<obj_item_assembly_timer>(bob.loc));
        r.Step(2);
        // An assembly comes secured; the pipe takes only an unsecured one (ghettobomb.dm:269) and secures it in the IED.
        r.In(() => Assert.False(Dm.truthy(Waiting.NoSleep(timer.toggle_secure()))));
        r.In(() => Assert.True(Dm.eq(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(pipe.item_interaction(bob, timer)))));
        r.Step(2);
        var ied = r.In(() => bob.get_active_held_item() as obj_item_grenade_iedcasing);
        Assert.NotNull(ied);
        r.In(() =>
        {
            Assert.Same(timer, ied.activator);
            Assert.Same(ied, timer.holder);
            Assert.Equal(1f, Dm.num(ied.effects[typeof(obj_item_food_meat_slab)]));
            Assert.Equal("pipebomb-timer", ied.icon_state);
        });

        r.In(() => Waiting.NoSleep(ied.attack_self(bob)));
        r.In(() => Assert.Equal("pipebomb-timer-a", ied.icon_state));
        r.Step(12 * Second);
        r.In(() =>
        {
            Assert.True(QDELETED(ied), "the IED did not go off");
            Assert.NotEmpty(Dm.each(range(3, locate(4, 4, 1))).OfType<obj_effect_decal_cleanable>());
        });
    }

    [Fact]
    public void TheExplosiveLanceShoutsItsWarCryAndBlowsItsIedOnAWieldedHit()
    {
        var (r, _, bob) = Room(9104);
        var lance = Hold<obj_item_spear_explosive>(r, bob);
        var target = Place<obj_structure_closet_crate>(r, 5, 4);
        var ied = r.In(() =>
        {
            Assert.IsType<obj_item_grenade_iedcasing_spawned>(lance.explosive);
            var spawned = (obj_item_grenade_iedcasing_spawned)lance.explosive;
            Assert.IsType<obj_item_assembly_timer>(spawned.activator);
            Assert.Equal(2.5f, Dm.num(spawned.power), 3);
            lance.war_cry = "FOR THE STATION";
            return spawned;
        });
        r.In(() => Waiting.NoSleep(lance.attack_self(bob)));
        r.In(() => Assert.True(HAS_TRAIT(lance, TRAIT_WIELDED)));
        r.In(() => Waiting.NoSleep(lance.afterattack(target, bob)));
        r.Step(Second);
        r.In(() =>
        {
            Assert.True(QDELETED(lance));
            Assert.True(QDELETED(ied));
        });
        Assert.True(Heard(r, "explosion"));
    }

    [Fact]
    public void TheChainsawStartsWithItsCordAndBeheadsADeadBodyAimedAtTheHead()
    {
        var (r, feed, bob) = Room(9105);
        var saw = Hold<obj_item_chainsaw>(r, bob);
        r.In(() =>
        {
            Assert.Equal(1.5f, saw.toolspeed);
            Assert.False(HAS_TRAIT(saw, TRAIT_TRANSFORM_ACTIVE));
        });
        r.In(() => Waiting.NoSleep(saw.attack_self(bob)));
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(saw, TRAIT_TRANSFORM_ACTIVE));
            Assert.Equal(24f, saw.force);
            Assert.Equal(0.5f, saw.toolspeed);
            Assert.True(((datum_component_butchering)saw.GetComponent(typeof(datum_component_butchering))).butchering_enabled > 0);
        });
        Assert.True(feed.Any("it begins to whirr."));

        mob_living_carbon_human body = r.Dummy(5, 4);
        r.Step(2);
        r.In(() =>
        {
            Waiting.NoSleep(body.death());
            bob.zone_selected = BODY_ZONE_HEAD;
        });
        r.In(() => Waiting.Detach(saw.attack__proc(body, bob)));
        r.Step(16 * Second);
        r.In(() => Assert.Null(body.get_bodypart(BODY_ZONE_HEAD)));
        Assert.True(Heard(r, "chainsawhit"));
    }

    [Fact]
    public void TheSabreBanesAnAssistantsLiverAndSheathesWithItsSound()
    {
        var (r, _, bob) = Room(9106);
        var sabre = Hold<obj_item_melee_sabre>(r, bob);
        mob_living_carbon_human tider = r.Dummy(5, 4);
        r.Step(2);
        r.In(() =>
        {
            Assert.False(Dm.truthy(sabre.bane_check(tider)));
            var liver = tider.get_organ_slot(ORGAN_SLOT_LIVER) as obj_item_organ_liver;
            ADD_TRAIT(liver, TRAIT_MAINTENANCE_METABOLISM, "test");
            Assert.True(Dm.truthy(sabre.bane_check(tider)));
            Assert.NotEmpty(Dm.each(sabre.GetComponents(typeof(datum_component_bane))));
            Assert.NotNull(sabre.GetComponent(typeof(datum_component_jousting)));
        });

        // The captain's sheath comes with its own sabre (belt.dm:724): drawn, it rings out; put back, it slides in.
        var sheath = r.In(() => Dm.@new<obj_item_storage_belt_sheath_sabre>(bob.loc));
        r.Step(2);
        var own = r.In(() => Dm.locate<obj_item_melee_sabre>(sheath.contents));
        Assert.NotNull(own);
        r.In(() => Waiting.NoSleep(((datum_storage)sheath.atom_storage).remove_single(bob, own, bob.loc)));
        r.Step(2);
        Assert.True(Heard(r, "items/unsheath.ogg"));
        r.In(() => Assert.True(Dm.truthy(((datum_storage)sheath.atom_storage).attempt_insert(own, bob))));
        r.Step(2);
        Assert.True(Heard(r, "items/sheath.ogg"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheChainOfCommandHastensWhomItLashesOnTheRightButton()
    {
        var (r, _, bob) = Room(9107);
        var chain = Hold<obj_item_melee_chainofcommand>(r, bob);
        mob_living_carbon_human crewman = r.Dummy(5, 4);
        r.Step(2);
        float before = r.In(() => Dm.num(crewman.cached_multiplicative_slowdown));
        r.In(() => Assert.True(Dm.eq(SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN, Waiting.NoSleep(chain.attack_secondary(crewman, bob)))));
        r.Step(2);
        r.In(() =>
        {
            Assert.True(Dm.truthy(crewman.has_status_effect(typeof(datum_status_effect_speed_boost_commanded))));
            Assert.True(Dm.num(crewman.cached_multiplicative_slowdown) < before, "the hasten did not speed up its target");
        });
        Assert.True(Heard(r, "weapons/whip"));
        r.Step(8 * Second);
        r.In(() => Assert.False(Dm.truthy(crewman.has_status_effect(typeof(datum_status_effect_speed_boost_commanded)))));
        r.In(() => Assert.True(Dm.eq(SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN, Waiting.NoSleep(chain.attack_secondary(bob, bob)))));
        r.In(() => Assert.False(Dm.truthy(bob.has_status_effect(typeof(datum_status_effect_speed_boost_commanded)))));
    }

    [Fact]
    public void TheBaseballBatKnocksItsTargetAwayAndTheMetalOneReflects()
    {
        var (r, _, bob) = Room(9108);
        var bat = Hold<obj_item_melee_baseball_bat>(r, bob);
        mob_living_carbon_human batter = r.Dummy(5, 4);
        r.Step(2);
        // the knock's direction is get_cardinal_dir(bat, target) (baseball_bat.dm:54): a held bat is at 0,0 (BYOND x of an atom
        // inside another), so tg rolls rand() * (dx + dy) < dy between the vertical and the horizontal and a vertical pick of an
        // eastward get_dir leaves no direction, the target's own turf; the stream is pinned right before the hit to a horizontal
        // roll (any earlier draw, such as gaps MS1's moods, moved it)
        r.In(() =>
        {
            r.World.Rng.Reseed(9108, 1);
            Waiting.NoSleep(bat.attack__proc(batter, bob));
        });
        r.Step(2 * Second);
        r.In(() => Assert.True(batter.x > 5, "the bat did not knock its target away"));
        var metal = r.In(() => Dm.@new<obj_item_melee_baseball_bat_ablative>(bob.loc));
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(metal.IsReflect()))));
    }

    [Fact]
    public void TheHighFrequencyBladeWieldedSlashesAThingForThriceItsForceAndDeflectsShots()
    {
        var (r, _, bob) = Room(9109);
        var blade = Hold<obj_item_highfrequencyblade>(r, bob);
        var crate = Place<obj_structure_closet_crate>(r, 5, 4);
        r.In(() => Assert.False(Dm.truthy(Waiting.NoSleep(blade.hit_reaction(bob, null, attack_type: PROJECTILE_ATTACK, final_block_chance: 0)))));
        r.In(() => Waiting.NoSleep(blade.attack_self(bob)));
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(blade, TRAIT_WIELDED));
            Assert.Equal("hfrequency1", blade.icon_state);
            Assert.True(Dm.truthy(Waiting.NoSleep(blade.hit_reaction(bob, null, attack_type: PROJECTILE_ATTACK, final_block_chance: 0))));
        });
        float integrity = r.In(() => Dm.num(crate.atom_integrity));
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(blade.slash(crate, bob, Dm.list())))));
        r.In(() => Assert.True(Dm.num(crate.atom_integrity) < integrity - blade.force, "the slash did not take thrice the force"));
        Assert.True(Heard(r, "zapbang"));
    }

    [Fact]
    public void TheStrobeShieldFlashesInHandAndShowsItsBurntOutBulbAsARiotShield()
    {
        var (r, _, bob) = Room(9110);
        var flicks = new List<(object Target, object State)>();
        PresentationSinks.Of(r.World).Appearance = new FlickRecorder(flicks);
        var shield = Hold<obj_item_shield_riot_flash>(r, bob);
        r.In(() =>
        {
            Assert.IsType<obj_item_assembly_flash_handheld>(shield.embedded_flash);
            Assert.Equal("flashshield", shield.icon_state);
        });
        r.In(() => Waiting.NoSleep(shield.attack_self(bob)));
        r.Step(2);
        Assert.Contains(flicks, f => ReferenceEquals(f.Target, shield) && Dm.eq(f.State, "flashshield_flash"));
        r.In(() =>
        {
            ((obj_item_assembly_flash_handheld)shield.embedded_flash).burnt_out = TRUE;
            shield.update_appearance();
            Assert.Equal("riot", shield.icon_state);
        });
    }

    [Fact]
    public void AHeadpikeTakenDownDropsItsHeadAndItsSpear()
    {
        var (r, feed, bob) = Room(9111);
        var pike = Place<obj_structure_headpike>(r, 5, 4);
        r.In(() =>
        {
            var head = Dm.@new<obj_item_bodypart_head>(pike);
            var spear = Dm.@new<obj_item_spear>(pike);
            pike.victim = head;
            pike.spear = spear;
            pike.update_appearance(Dm.bitor(UPDATE_NAME, UPDATE_OVERLAYS));
            Assert.EndsWith("on a spear", pike.name);
        });
        r.In(() => Waiting.NoSleep(pike.attack_hand(bob)));
        r.Step(2);
        r.In(() =>
        {
            Assert.True(QDELETED(pike));
            turf there = locate(5, 4, 1);
            Assert.NotEmpty(Dm.each(there.contents).OfType<obj_item_bodypart_head>());
            Assert.NotEmpty(Dm.each(there.contents).OfType<obj_item_spear>());
        });
        Assert.True(feed.Any("You take down"));
    }

    [Fact]
    public void TheGreyTideRaisesIllusionsOfItsBearerThatReadAsHimAndFade()
    {
        var (r, _, bob) = Room(9112);
        var tide = Hold<obj_item_spear_grey_tide>(r, bob);
        mob_living_carbon_human foe = r.Dummy(5, 4);
        r.Step(2);
        mob_living_basic_illusion clone = null;
        for (int i = 0; i < 12 && clone == null; i++)
        {
            r.In(() => Waiting.NoSleep(tide.afterattack(foe, bob)));
            r.Step(2);
            clone = r.In(() => Dm.each(range(2, bob)).OfType<mob_living_basic_illusion>().FirstOrDefault());
        }
        Assert.NotNull(clone);
        r.In(() =>
        {
            Assert.Same(bob, clone.parent_mob_ref.resolve());
            Assert.Contains(Dm.text("greytide([])", REF(bob)), Dm.each(bob.allies).Select(Dm.str));
            // tg tests isnull() where it means !isnull(): the clone keeps its own health whatever full_setup gives it
            Assert.Equal(100f, clone.maxHealth);
        });
        r.Step(11 * Second);
        r.In(() => Assert.True(QDELETED(clone), "the illusion did not fade"));
    }

    [Fact]
    public void AWhiteCaneGuidesTheBlindOnlyWhileExtended()
    {
        var (r, feed, bob) = Room(9113);
        var cane = Hold<obj_item_cane_white>(r, bob);
        r.In(() =>
        {
            Assert.NotNull(cane.GetComponent(typeof(datum_component_walking_aid)));
            Assert.False(HAS_TRAIT(cane, TRAIT_BLIND_TOOL));
        });
        r.In(() => Waiting.NoSleep(cane.attack_self(bob)));
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(cane, TRAIT_TRANSFORM_ACTIVE));
            Assert.True(HAS_TRAIT(cane, TRAIT_BLIND_TOOL));
            Assert.Equal(7f, cane.force);
        });
        Assert.True(feed.Any("balloon: extended"));
        r.In(() => Waiting.NoSleep(cane.attack_self(bob)));
        r.In(() => Assert.False(HAS_TRAIT(cane, TRAIT_BLIND_TOOL)));
        Assert.True(Heard(r, "batonextend"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSupermatterSwordDustsWhatItHits()
    {
        var (r, _, bob) = Room(9114);
        var sword = Hold<obj_item_melee_supermatter_sword>(r, bob);
        var crate = Place<obj_structure_closet_crate>(r, 5, 4);
        r.In(() =>
        {
            Assert.NotNull(sword.shard);
            Assert.Null(sword.shard.countdown);
        });
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(sword.pre_attack(crate, bob)))));
        r.Step(2);
        r.In(() => Assert.True(QDELETED(crate), "the sword did not dust the crate"));
    }

    [Fact]
    public void ThePillowLanceChargesAfterTwoTilesAndImpalesForStamina()
    {
        var (r, feed, bob) = Room(9115);
        var lance = Hold<obj_item_spear_pillow>(r, bob);
        mob_living_carbon_human foe = r.Dummy(6, 4);
        r.In(() => Waiting.NoSleep(lance.attack_self(bob)));
        r.In(() => Assert.True(HAS_TRAIT(lance, TRAIT_WIELDED)));
        r.In(() => Waiting.NoSleep(bob.setDir(EAST)));
        for (int i = 0; i < 4; i++)
        {
            r.In(() => lance.check_move(bob));
            r.Step(2);
        }
        r.In(() =>
        {
            Assert.True(Dm.truthy(lance.charging));
            Assert.True(Dm.num(lance.current_tile_charge) >= 2);
        });
        Assert.True(feed.Any("balloon: charging!"));
        float stamina = r.In(() => Dm.num(foe.get_stamina_loss()));
        r.In(() => lance.impale(bob, foe));
        r.Step(Second);
        r.In(() => Assert.True(Dm.num(foe.get_stamina_loss()) > stamina, "the impale did no stamina damage"));
        Assert.True(Heard(r, "effects/bang"));
    }

    [Fact]
    public void TheNunchakuInThrowModeBlocksAMeleeAttackForALittleStamina()
    {
        var (r, _, bob) = Room(9116);
        var chucks = Hold<obj_item_melee_baton_nunchaku>(r, bob);
        float stamina = r.In(() => Dm.num(bob.get_stamina_loss()));
        r.In(() => Assert.False(Dm.truthy(Waiting.NoSleep(chucks.hit_reaction(bob, null, attack_type: PROJECTILE_ATTACK, final_block_chance: 0)))));
        r.In(() => bob.throw_mode = THROW_MODE_TOGGLE);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(chucks.hit_reaction(bob, null, attack_type: THROWN_PROJECTILE_ATTACK, final_block_chance: 0)))));
        r.In(() =>
        {
            Assert.True(Dm.num(bob.get_stamina_loss()) > stamina);
            Assert.Contains(chucks.icon_state, new[] { "nunchaku", "nunchaku_x", "nunchaku_y" });
        });
    }
}
