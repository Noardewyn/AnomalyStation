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
/// M4c slice 2: tg's space suits, helmets and magboots (spacesuits/_spacesuits.dm, softsuit.dm, shoes/magboots.dm,
/// components/shoe_footstep.dm). The EVA suit and helmet stop the pressure damage of a vacuum by their STOPSPRESSUREDAMAGE
/// flags and give tg's cold protection (the head always, the body only with the thermal regulator on); the regulator draws
/// THERMAL_REGULATOR_COST from the suit's cell each process and moves the body temperature by tg's suit change rate; the
/// spacesuit HUD shows the cell. The magboots' mag-pulse (attack_self, the action button's proc) adds TRAIT_NEGATES_GRAVITY:
/// on a floor without gravity the wearer has gravity, does not drift after a push and walks with the rigstep sounds; off,
/// the push drifts; on a space tile (groundless) the boots do not hold, as in tg.
/// </summary>
public class SpaceSuitTests
{
    private const int Second = 20;
    private const string Chonk = "sound/items/modsuit/rigstep_chonk.ogg";
    private const string Medium = "sound/items/modsuit/rigstep_medium.ogg";
    // tg: code/modules/clothing/spacesuits/_spacesuits.dm:2  THERMAL_REGULATOR_COST (0.018 * STANDARD_CELL_CHARGE)
    private const float RegulatorCost = 0.018f * STANDARD_CELL_CHARGE;
    // tg: code/modules/mob/living/carbon/human/life.dm:7  THERMAL_PROTECTION_HEAD
    private const float HeadProtection = 0.3f;

    private static T Wear<T>(AudioRoom r, mob_living_carbon_human who, object slot) where T : obj_item
        => r.In(() =>
        {
            T item = Dm.@new<T>(who.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(who.equip_to_slot_or_del(item, slot))), typeof(T).Name + " goes on");
            return item;
        });

    private static (AudioRoom r, mob_living_carbon_human bob, obj_item_clothing_suit_space_eva suit, obj_item_clothing_head_helmet_space_eva helmet) Suited(ulong seed, bool internals = true)
    {
        var r = new AudioRoom(9, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        var suit = Wear<obj_item_clothing_suit_space_eva>(r, bob, ITEM_SLOT_OCLOTHING);
        var helmet = Wear<obj_item_clothing_head_helmet_space_eva>(r, bob, ITEM_SLOT_HEAD);
        if (internals)
        {
            Wear<obj_item_clothing_mask_breath>(r, bob, ITEM_SLOT_MASK);
            var tank = Wear<obj_item_tank_internals_oxygen>(r, bob, ITEM_SLOT_SUITSTORE);
            r.In(() => Waiting.NoSleep(bob.toggle_internals(tank)));
            Assert.Same(tank, r.In(() => bob.@internal));
        }
        return (r, bob, suit, helmet);
    }

    private static (int X, int Y, int Z) Loc(AudioRoom r, atom a) => r.In(() => ((int)a.x, (int)a.y, (int)a.z));

    private static float Brute(AudioRoom r, mob_living who) => r.In(() => Dm.num(who.get_brute_loss()));

    [Fact]
    public void TheSuitMakesItsHighCapacityCellAndStartsWithTheRegulatorOff()
    {
        var r = new AudioRoom(5, 5, seed: 7401);
        r.In(() =>
        {
            var suit = Dm.@new<obj_item_clothing_suit_space_eva>(locate(2, 2, 1));
            Assert.IsType<obj_item_stock_parts_power_store_cell_high>(suit.cell_instance);
            Assert.Same(suit, suit.cell_instance.loc);
            Assert.Equal(STANDARD_CELL_CHARGE * 10f, suit.cell_instance.charge); // cell.dm:119, full at Initialize (power_store.dm:53)
            Assert.Equal(100f, Dm.num(suit.cell_instance.percent()));
            Assert.Equal(FALSE, suit.thermal_on);
            Assert.Equal(SPACE_SUIT_MIN_TEMP_PROTECT_OFF, suit.min_cold_protection_temperature);
            Assert.Same(suit.cell_instance, suit.get_cell());
            var helmet = Dm.@new<obj_item_clothing_head_helmet_space_eva>(locate(2, 2, 1));
            Assert.Equal("space", helmet.icon_state);
            // the cell's description carries tg's rating text (power_store.dm:55, display_energy: siunit 3 decimals)
            Assert.Contains("This one has a rating of 100 kJ", suit.cell_instance.desc as string);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void SuitAndHelmetStopThePressureDamageOfAVacuum()
    {
        var (r, bob, suit, _) = Suited(7402);
        r.In(() => Waiting.NoSleep(suit.toggle_spacesuit(bob)));
        mob_living_carbon_human bare = r.Dummy(6, 6);
        r.In(() => ADD_TRAIT(bare, TRAIT_NOBREATH, "test")); // only the pressure and the cold reach it

        r.In(() => DevCommands.Gas(AIRLESS_ATMOS));
        r.Step(Second);
        // calculate_affecting_pressure (human/life.dm:73): covered chest, head, hands and feet read as one atmosphere
        Assert.Equal(ONE_ATMOSPHERE, r.In(() => Dm.num(bob.calculate_affecting_pressure(0))));
        Assert.Equal(0f, r.In(() => Dm.num(bare.calculate_affecting_pressure(0))));

        r.Step(60 * Second);
        Assert.Equal(0f, Brute(r, bob));
        Assert.Equal(0f, r.In(() => Dm.num(bob.get_fire_loss())));
        Assert.Equal(0f, r.In(() => Dm.num(bob.get_oxy_loss())));
        Assert.True(Brute(r, bare) > 0f, "the unsuited human took tg's low pressure damage");
        float temp = r.In(() => bob.bodytemperature);
        Assert.InRange(temp, r.In(() => Dm.num(bob.get_body_temp_cold_damage_limit())), r.In(() => Dm.num(bob.get_body_temp_heat_damage_limit())));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHelmetAlwaysAndTheSuitOnlyWithTheRegulatorGiveColdProtection()
    {
        var (r, bob, suit, _) = Suited(7403, internals: false);
        r.In(() =>
        {
            // the space around: TCMB (2.7 K). The helmet holds to SPACE_HELM_MIN_TEMP_PROTECT (2 K); the suit with the
            // regulator off only to SPACE_SUIT_MIN_TEMP_PROTECT_OFF (72 K) (human/life.dm:320 get_cold_protection_flags)
            Assert.Equal(HeadProtection, Dm.num(bob.get_cold_protection(TCMB)), 4);
            Assert.Equal(1f, Dm.num(bob.get_cold_protection(100f)), 4);
        });
        r.In(() => Waiting.NoSleep(suit.ui_action_click(bob, null))); // the action button's proc (_spacesuits.dm:327)
        r.In(() =>
        {
            Assert.Equal(TRUE, suit.thermal_on);
            Assert.Equal(SPACE_SUIT_MIN_TEMP_PROTECT, suit.min_cold_protection_temperature);
            Assert.Equal(1f, Dm.num(bob.get_cold_protection(TCMB)), 4);
        });
        r.In(() => Waiting.NoSleep(suit.toggle_spacesuit(bob)));
        Assert.Equal(HeadProtection, r.In(() => Dm.num(bob.get_cold_protection(TCMB))), 4);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRegulatorDrawsTheCellAndWarmsTheWearerByTgsRate()
    {
        var (r, bob, suit, _) = Suited(7404, internals: false);
        r.In(() => Waiting.NoSleep(suit.toggle_spacesuit(bob)));
        r.In(() =>
        {
            float charge = suit.cell_instance.charge;
            bob.bodytemperature = 290f;
            // one process by hand (_spacesuits.dm:162): the cell pays THERMAL_REGULATOR_COST, the body moves toward
            // temperature_setting by get_temp_change_amount at BODYTEMP_SUIT_CHANGE_RATE per second
            float expected = 290f + get_temp_change_amount(suit.temperature_setting - 290f, BODYTEMP_SUIT_CHANGE_RATE * 2);
            suit.process(2);
            Assert.Equal(charge - RegulatorCost, suit.cell_instance.charge, 2);
            Assert.Equal(expected, bob.bodytemperature, 3);
            Assert.True(bob.bodytemperature > 290f);
        });

        // SSobj processes the worn suit: the charge falls by whole regulator costs
        float before = r.In(() => suit.cell_instance.charge);
        r.Step(10 * Second);
        float used = before - r.In(() => suit.cell_instance.charge);
        Assert.True(used >= RegulatorCost, "the regulator ran: " + used);
        float ticks = used / RegulatorCost;
        Assert.Equal(MathF.Round(ticks), ticks, 2);

        // a cell below the cost: the regulator cuts off by itself (_spacesuits.dm:178)
        r.In(() =>
        {
            suit.cell_instance.charge = RegulatorCost / 2;
            suit.process(2);
            Assert.Equal(FALSE, suit.thermal_on);
            Assert.Equal(SPACE_SUIT_MIN_TEMP_PROTECT_OFF, suit.min_cold_protection_temperature);
            // and it does not turn on again without charge
            Waiting.NoSleep(suit.toggle_spacesuit(bob));
            Assert.Equal(FALSE, suit.thermal_on);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCellComesOutThroughTheOpenCoverAndTheHudFollowsIt()
    {
        var (r, bob, suit, _) = Suited(7405, internals: false);
        atom_movable_screen_spacesuit hud = r.In(() => bob.hud_used.screen_objects[HUD_MOB_SPACESUIT] as atom_movable_screen_spacesuit);
        Assert.NotNull(hud);
        r.In(() => Waiting.NoSleep(suit.toggle_spacesuit(bob)));
        r.In(() =>
        {
            suit.process(2);
            Assert.Equal("spacesuit_high", hud.icon_state);
            Assert.Equal(TRUE, hud.cached_thermal_on);
        });

        // turned off by the HUD button (screen_objects.dm:472): the "off" overlay
        Assert.True(r.In(() => Dm.@in(hud, bob.client.screen)), "the display is on the player's screen");
        r.Command(() => ClientSeam.Click(bob.client, hud, "icon-x=16;icon-y=16"));
        r.Step(Second);
        Assert.Equal(FALSE, r.In(() => suit.thermal_on));
        // tg redraws the display on the next hover (screen_objects.dm:479, MouseEntered -> update_hud_icon): the "off" overlay
        // and, while hovered, the charge in the maptext
        r.Command(() => ClientSeam.Hover(bob.client, hud));
        r.Step();
        r.In(() =>
        {
            Assert.Equal(FALSE, hud.cached_thermal_on);
            Assert.Equal(TRUE, hud.hovering);
            Assert.Contains("%", hud.maptext as string);
            Assert.Contains(atom_movable_screen_spacesuit.off_overlay, Dm.each_cast<object>(Waiting.NoSleep(hud.update_overlays())));
        });

        // the cover (alt-click, _spacesuits.dm:264) and the suit used in hand take the cell out (:276)
        r.In(() =>
        {
            Waiting.NoSleep(suit.click_alt(bob));
            Assert.Equal(TRUE, suit.cell_cover_open);
            obj_item_stock_parts_power_store cell = suit.cell_instance;
            Waiting.NoSleep(suit.attack_self(bob));
            Assert.Null(suit.cell_instance);
            Assert.True(Dm.truthy(bob.is_holding(cell)), "the cell is in a hand");
            Assert.Equal("spacesuit_missing", hud.icon_state);
            Waiting.NoSleep(suit.toggle_spacesuit(bob));
            Assert.Equal(FALSE, suit.thermal_on); // no cell, no regulator
        });

        // taking the suit off clears the display (:152)
        r.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.dropItemToGround(suit))));
            Assert.Null(hud.icon_state);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheEvaHelmetVisorGoesUpAndDownWithItsSounds()
    {
        var (r, bob, _, helmet) = Suited(7406, internals: false);
        r.Sounds.Heard.Clear();
        r.In(() => Waiting.NoSleep(helmet.click_alt(bob)));
        r.In(() =>
        {
            Assert.Equal(TRUE, helmet.up);
            Assert.Equal("space-novisor", helmet.icon_state);
            Assert.False(Dm.truthy(Dm.bitand(helmet.clothing_flags, STOPSPRESSUREDAMAGE)), "the open visor lets the pressure in");
        });
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.Contains("visor", StringComparison.Ordinal));
        r.In(() => Waiting.NoSleep(helmet.attack_self(bob)));
        r.In(() =>
        {
            Assert.Equal(FALSE, helmet.up);
            Assert.Equal("space", helmet.icon_state);
            Assert.True(Dm.truthy(Dm.bitand(helmet.clothing_flags, STOPSPRESSUREDAMAGE)));
        });
        r.AssertNoRuntimes();
    }

    // ---- magboots ----

    private static (AudioRoom r, mob_living_carbon_human bob, obj_item_clothing_shoes_magboots boots) Booted(ulong seed, bool noGravityLane = true)
    {
        var r = new AudioRoom(16, 9, seed: seed);
        if (noGravityLane)
        {
            // a hull walkway: floors (grounded turfs) without gravity, (2..14, 4)
            r.In(() =>
            {
                for (int x = 2; x <= 14; x++)
                    ((turf)locate(x, 4, 1)).force_no_gravity = TRUE;
            });
        }
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_GODMODE, "test"));
        var boots = Wear<obj_item_clothing_shoes_magboots>(r, bob, ITEM_SLOT_FEET);
        r.Step(Second);
        return (r, bob, boots);
    }

    private static void Pulse(AudioRoom r, mob who, obj_item_clothing_shoes_magboots boots)
        => r.In(() => Waiting.NoSleep(boots.attack_self(who))); // the item action's proc (action buttons: M4c slice 3)

    [Fact]
    public void MagPulseAddsTheTraitsTheSlowdownAndTheIconState()
    {
        var (r, bob, boots) = Booted(7411, noGravityLane: false);
        float before = r.In(() => Dm.num(bob.cached_multiplicative_slowdown));
        Assert.Equal("magboots0", r.In(() => boots.icon_state));
        Pulse(r, bob, boots);
        r.In(() =>
        {
            Assert.Equal(TRUE, boots.magpulse);
            Assert.Equal("magboots1", boots.icon_state);
            Assert.Equal(2f, boots.slowdown); // SHOES_SLOWDOWN 0 + slowdown_active 2 (magboots.dm:21)
            Assert.True(HAS_TRAIT(bob, TRAIT_NEGATES_GRAVITY));
            Assert.True(HAS_TRAIT(bob, TRAIT_NO_SLIP_WATER));
            Assert.True(HAS_TRAIT(bob, TRAIT_NO_SLIP_ICE));
            Assert.True(HAS_TRAIT(bob, TRAIT_NO_SLIP_SLIDE));
            Assert.Equal(before + 2f, Dm.num(bob.cached_multiplicative_slowdown), 3);
            Assert.Contains("enabled", string.Join(" ", Dm.each_cast<object>(Waiting.NoSleep(boots.examine(bob)))));
        });
        Pulse(r, bob, boots);
        r.In(() =>
        {
            Assert.Equal(FALSE, boots.magpulse);
            Assert.Equal("magboots0", boots.icon_state);
            Assert.Equal(0f, boots.slowdown);
            Assert.False(HAS_TRAIT(bob, TRAIT_NEGATES_GRAVITY));
            Assert.Equal(before, Dm.num(bob.cached_multiplicative_slowdown), 3);
        });
        // boots in a hand toggle as well (Z, attack_self), and their traits reach nobody until worn
        r.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.dropItemToGround(boots))));
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(boots))));
        });
        Pulse(r, bob, boots);
        r.In(() =>
        {
            Assert.Equal(TRUE, boots.magpulse);
            Assert.False(HAS_TRAIT(bob, TRAIT_NEGATES_GRAVITY));
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(boots, ITEM_SLOT_FEET, disable_warning: TRUE, bypass_equip_delay_self: TRUE))));
            Assert.True(HAS_TRAIT(bob, TRAIT_NEGATES_GRAVITY));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void OnAFloorWithoutGravityTheBootsHoldThePlayerAgainstAPush()
    {
        var (r, bob, boots) = Booted(7412);
        Assert.False(r.In(() => Dm.truthy(bob.has_gravity())), "the walkway has no gravity");
        Pulse(r, bob, boots);
        r.In(() =>
        {
            Assert.True(HAS_TRAIT_FROM(bob, TRAIT_IGNORING_GRAVITY, IGNORING_GRAVITY_NEGATION)); // init_signals.dm:293
            Assert.True(Dm.truthy(bob.mob_negates_gravity()));
            Assert.True(Dm.truthy(bob.has_gravity()));
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.Process_Spacemove(EAST))));
        });
        (int X, int Y, int _) start = Loc(r, bob);
        r.In(() => Waiting.NoSleep(bob.newtonian_move(dir2angle(EAST))));
        r.Step(5 * Second);
        Assert.Null(r.In(() => bob.drift_handler));
        Assert.Equal(start, Loc(r, bob));

        // off: the same push drifts the player along the walkway
        Pulse(r, bob, boots);
        Assert.False(r.In(() => Dm.truthy(bob.has_gravity())));
        r.In(() => Waiting.NoSleep(bob.newtonian_move(dir2angle(EAST))));
        Assert.NotNull(r.In(() => bob.drift_handler));
        r.Step(5 * Second);
        Assert.True(Loc(r, bob).X > start.X + 1, $"the player drifted east: {start} -> {Loc(r, bob)}");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WithTheBootsOnThePlayerWalksTheWalkwayWithRigsteps()
    {
        var (r, bob, boots) = Booted(7413);
        // off: no gravity and nothing to push from, so client/Move is refused (Process_Spacemove)
        Assert.False(r.In(() => Dm.truthy(Waiting.NoSleep(bob.Process_Spacemove(EAST)))));
        Pulse(r, bob, boots);
        r.Sounds.Heard.Clear();
        r.In(() =>
        {
            for (int i = 0; i < 4; i++)
            {
                Assert.True(Dm.truthy(Waiting.NoSleep(bob.Process_Spacemove(EAST))));
                Assert.True(Dm.truthy(Waiting.NoSleep(bob.Move(get_step(bob, EAST), EAST))), "the step was refused");
            }
        });
        Assert.Equal(8, Loc(r, bob).X);
        // shoe_footstep (steps_per_play 1): every other step plays rigstep_chonk at volume 50; the body's own footsteps are
        // silenced (TRAIT_SILENT_FOOTSTEPS)
        List<HeardSound> chonks = r.Sounds.Of(bob, Chonk);
        Assert.Equal(2, chonks.Count);
        Assert.All(chonks, s => Assert.True(s.Volume > 0));
        Assert.DoesNotContain(r.Sounds.Heard, h => ReferenceEquals(h.Target, bob) && h.File != null && h.File.StartsWith("sound/effects/footstep/", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WithGravityTheBootsOffStepWithRigstepMedium()
    {
        var (r, bob, _) = Booted(7414, noGravityLane: false);
        r.Sounds.Heard.Clear();
        r.In(() =>
        {
            for (int i = 0; i < 4; i++)
                Assert.True(Dm.truthy(Waiting.NoSleep(bob.Move(get_step(bob, EAST), EAST))));
        });
        Assert.Equal(2, r.Sounds.Of(bob, Medium).Count);
        Assert.Empty(r.Sounds.Of(bob, Chonk));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void OnASpaceTileTheBootsDoNotHold()
    {
        // tg: a space turf is groundless (is_helpers.dm:30), so the mag-pulse gives no gravity there (init_signals.dm:295)
        var r = new AudioRoom(16, 9, seed: 7415);
        r.In(() =>
        {
            for (int x = 3; x <= 13; x++)
                Waiting.NoSleep(locate(x, 4, 1).ChangeTurf(typeof(turf_open_space)));
        });
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_GODMODE, "test"));
        var boots = Wear<obj_item_clothing_shoes_magboots>(r, bob, ITEM_SLOT_FEET);
        r.Step(Second);
        Pulse(r, bob, boots);
        Assert.False(r.In(() => Dm.truthy(bob.has_gravity())));
        r.In(() => Waiting.NoSleep(bob.newtonian_move(dir2angle(EAST))));
        r.Step(3 * Second);
        Assert.True(Loc(r, bob).X > 6, "the player drifted over space: " + Loc(r, bob));
        r.AssertNoRuntimes();
    }
}
