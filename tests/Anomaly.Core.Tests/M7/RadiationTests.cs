using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using T1 = Anomaly.Core.Tests.Tg.M3Doors.T1.T1;

namespace Anomaly.Core.Tests.M7;

/// <summary>Records what a radiation pulse tells an atom: the insulation on its way (COMSIG_IN_RANGE_OF_IRRADIATION) and the
/// threshold signal (COMSIG_IN_THRESHOLD_OF_IRRADIATION) sent only when the pulse is above its threshold.</summary>
[DmPath("/datum/t7_rad_probe")]
public class datum_t7_rad_probe : datum
{
    public readonly List<float> InRange = new();
    public int InThreshold;

    [SignalHandler]
    public virtual void on_in_range(datum source = null, datum_radiation_pulse_information pulse_information = null, object insulation_to_target = null)
        => InRange.Add(Dm.num(insulation_to_target));

    [SignalHandler]
    public virtual object on_in_threshold(datum source = null, datum_radiation_pulse_information pulse_information = null)
    {
        InThreshold++;
        return null;
    }
}

/// <summary>
/// M7 radiation slice (D163): tg's radiation. <c>radiation_pulse</c> (__HELPERS/radiation.dm:25) queues a pulse that
/// SSradiation (controllers/subsystem/radiation.dm) walks: the insulation of every turf and atom on the line multiplies down from
/// 1, the threshold stops it, the chance falls off with distance and insulation; the irradiated component
/// (datums/components/irradiated.dm) burns and poisons a human; the radiation suit and TRAIT_RADIMMUNE protect; the radioactive
/// emitter pulses on a cooldown; the geiger counter reads the danger. The rooms boot the real Master, so SSradiation exists; the
/// statistical tests fire it directly (as the MC would, its state set to running).
/// </summary>
public class RadiationTests
{
    private const int Second = 20; // ticks

    private static object Pulse(TgToyRoom room, int x, int y, float range, float threshold, float chance = DEFAULT_RADIATION_CHANCE, float exposure = 0)
        => room.In(() => radiation_pulse(locate(x, y, 1), range, threshold, chance, exposure));

    /// <summary>SSradiation's fire, as the MC runs it (MC_TICK_CHECK pauses a subsystem that is not running).</summary>
    private static void Fire(TgToyRoom room) => room.In(() =>
    {
        var ss = SSradiation;
        var before = ss.state;
        ss.state = SS_RUNNING;
        Waiting.NoSleep(ss.fire(FALSE));
        ss.state = before;
        Assert.Equal(0, ss.processing.len);
    });

    private static datum_t7_rad_probe Probe(TgToyRoom room, atom on) => room.In(() =>
    {
        var probe = Dm.@new<datum_t7_rad_probe>();
        probe.RegisterSignal(on, COMSIG_IN_RANGE_OF_IRRADIATION, nameof(datum_t7_rad_probe.on_in_range));
        probe.RegisterSignal(on, COMSIG_IN_THRESHOLD_OF_IRRADIATION, nameof(datum_t7_rad_probe.on_in_threshold));
        return probe;
    });

    private static bool Irradiated(TgToyRoom room, atom a) => room.In(() => HAS_TRAIT(a, TRAIT_IRRADIATED));

    private static void Clean(TgToyRoom room, atom a) => room.In(() => qdel(a.GetComponent(typeof(datum_component_irradiated))));

    /// <summary>tg's perceived chance (radiation.dm:75-81) for a pulse of chance, range and threshold seen at distance with insulation.</summary>
    private static double TgChance(double chance, double range, double threshold, double distance, double insulation)
    {
        double intensity = -Math.Log(1 - chance / 100) * Math.Pow(1 + range / 2, 2);
        double perceived = intensity / Math.Pow(1 + distance, 2) * (insulation - threshold) / (1 - threshold);
        return 1 - Math.Exp(-perceived);
    }

    [Fact]
    public void ThePulseMultipliesTheInsulationOfWallsDoorsAndWindowsOnItsLine()
    {
        var room = T1.Room(width: 15, height: 15);
        // the source at 8,8; a crowbar 3 tiles away in each direction, the blocker 2 tiles away on the line
        var targets = new Dictionary<string, (int X, int Y)>
        {
            ["open"] = (11, 8), ["wall"] = (5, 8), ["airlock"] = (8, 11), ["window"] = (8, 5),
            ["reinforced wall"] = (11, 11), ["reinforced window"] = (5, 5), ["wall and window"] = (11, 5),
        };
        room.In(() =>
        {
            Waiting.NoSleep(locate(6, 8, 1).ChangeTurf(typeof(turf_closed_wall)));
            Dm.@new<obj_machinery_door_airlock>(locate(8, 10, 1));
            Dm.@new<obj_structure_window_fulltile>(locate(8, 6, 1));
            Waiting.NoSleep(locate(10, 10, 1).ChangeTurf(typeof(turf_closed_wall_r_wall)));
            Dm.@new<obj_structure_window_reinforced_fulltile>(locate(6, 6, 1));
            Waiting.NoSleep(locate(9, 7, 1).ChangeTurf(typeof(turf_closed_wall)));
            Dm.@new<obj_structure_window_fulltile>(locate(10, 6, 1));
        });
        var probes = targets.ToDictionary(t => t.Key, t => Probe(room, T1.At<obj_item_crowbar>(room, t.Value.X, t.Value.Y)));

        Assert.Equal(TRUE, Pulse(room, 8, 8, 3, 0, 100));
        Fire(room);

        // the insulation each target hears (signals_radiation.dm:6), tg's RAD_* defines (__DEFINES/radiation.dm:29-35)
        float At(string name) => Assert.Single(probes[name].InRange);
        Assert.Equal(1f, At("open"));
        Assert.Equal(RAD_MEDIUM_INSULATION, At("wall"), 4);
        Assert.Equal(RAD_MEDIUM_INSULATION, At("airlock"), 4);
        Assert.Equal(RAD_VERY_LIGHT_INSULATION, At("window"), 4);
        Assert.Equal(RAD_HEAVY_INSULATION, At("reinforced wall"), 4);
        Assert.Equal(RAD_LIGHT_INSULATION, At("reinforced window"), 4);
        Assert.Equal(RAD_MEDIUM_INSULATION * RAD_VERY_LIGHT_INSULATION, At("wall and window"), 4);
        // threshold 0: every one of them was above it and heard the threshold signal
        Assert.All(probes.Values, p => Assert.Equal(1, p.InThreshold));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void TheThresholdStopsThePulseAtTheInsulationItNamesAndAHundredPercentChanceAlwaysIrradiates()
    {
        var room = T1.Room(width: 15, height: 15);
        room.In(() => Waiting.NoSleep(locate(6, 8, 1).ChangeTurf(typeof(turf_closed_wall))));
        var behind = T1.At<obj_item_crowbar>(room, 5, 8);
        var open = T1.At<obj_item_crowbar>(room, 11, 8);
        var probe = Probe(room, behind);

        // the wall's 0.7 is not above a 0.7 threshold (radiation.dm:63): heard, never irradiated
        Pulse(room, 8, 8, 3, RAD_MEDIUM_INSULATION, 100);
        Fire(room);
        Assert.Equal(RAD_MEDIUM_INSULATION, Assert.Single(probe.InRange), 4);
        Assert.Equal(0, probe.InThreshold);
        Assert.False(Irradiated(room, behind));
        Assert.True(Irradiated(room, open)); // chance 100: perceived_chance is 100 (radiation.dm:80-81)

        // a lower threshold lets the wall's insulation through
        Pulse(room, 8, 8, 3, 0.69f, 100);
        Fire(room);
        Assert.Equal(1, probe.InThreshold);
        Assert.True(Irradiated(room, behind));

        // out of max_range: not reached at all
        var far = T1.At<obj_item_crowbar>(room, 13, 8);
        Pulse(room, 8, 8, 3, 0, 100);
        Fire(room);
        Assert.False(Irradiated(room, far));

        // SSradiation that cannot fire takes no pulse (radiation.dm:32)
        room.In(() => SSradiation.can_fire = FALSE);
        Assert.Null(Pulse(room, 8, 8, 3, 0, 100));
        Assert.Equal(0, room.In(() => SSradiation.processing.len));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void TheChanceFallsOffWithDistanceAndInsulationAsTgComputesIt()
    {
        var room = T1.Room(width: 15, height: 15, seed: 77);
        // tg's own example (radiation.dm:22-24): chance 30, max_range 8 gives 30% at 4 tiles
        var at2 = T1.At<obj_item_crowbar>(room, 4, 2);
        var at4 = T1.At<obj_item_crowbar>(room, 6, 2);
        var at8 = T1.At<obj_item_crowbar>(room, 10, 2);
        // a window (0.9) on the line to a target 4 tiles up, threshold 0.5 below
        var windowed = T1.At<obj_item_crowbar>(room, 2, 6);
        room.In(() => Dm.@new<obj_structure_window_fulltile>(locate(2, 4, 1)));
        var hits = new int[4];
        const int Trials = 600;
        for (int i = 0; i < Trials; i++)
        {
            Pulse(room, 2, 2, 8, 0, 30);
            Fire(room);
            atom[] row = { at2, at4, at8 };
            for (int k = 0; k < 3; k++)
            {
                if (Irradiated(room, row[k]))
                {
                    hits[k]++;
                    Clean(room, row[k]);
                }
            }
            Clean(room, windowed);
        }
        Assert.Equal(0.30, TgChance(30, 8, 0, 4, 1), 3);
        Assert.InRange(hits[0] / (double)Trials, TgChance(30, 8, 0, 2, 1) - 0.06, TgChance(30, 8, 0, 2, 1) + 0.06); // 62.9%
        Assert.InRange(hits[1] / (double)Trials, 0.30 - 0.06, 0.30 + 0.06);
        Assert.InRange(hits[2] / (double)Trials, TgChance(30, 8, 0, 8, 1) - 0.04, TgChance(30, 8, 0, 8, 1) + 0.04);
        Assert.True(hits[0] > hits[1] && hits[1] > hits[2], $"falls off with distance: {string.Join(",", hits)}");

        // the threshold and the insulation scale the intensity by (insulation - threshold) / (1 - threshold) (radiation.dm:78)
        for (int i = 0; i < Trials; i++)
        {
            Pulse(room, 2, 2, 8, 0.5f, 30);
            Fire(room);
            if (Irradiated(room, windowed))
            {
                hits[3]++;
                Clean(room, windowed);
            }
            Clean(room, at4);
            Clean(room, at2);
            Clean(room, at8);
        }
        double windowChance = TgChance(30, 8, 0.5, 4, RAD_VERY_LIGHT_INSULATION); // 24.8%
        Assert.InRange(hits[3] / (double)Trials, windowChance - 0.06, windowChance + 0.06);
        T1.NoRuntimes(room);
    }

    [Fact]
    public void AnIrradiatedHumanGlowsHasTheAlertAndTakesTgsToxinAndBurnDamageOverTime()
    {
        var room = T1.Room(width: 15, height: 15);
        var human = T1.Human(room, 8, 8);
        Assert.Equal(0f, room.In(() => Dm.num(human.get_tox_loss())));
        Pulse(room, 8, 8, 2, 0, 100);
        Fire(room);

        Assert.True(Irradiated(room, human));
        room.In(() =>
        {
            // irradiated.dm:42: RADIATION_IMMEDIATE_TOX_DAMAGE
            Assert.Equal(10f, Dm.num(human.get_tox_loss()));
            // irradiated.dm:49: the HUD alert
            Assert.IsType<atom_movable_screen_alert_irradiated>(human.alerts[ALERT_IRRADIATED]);
            // irradiated.dm:156: the green outline
            var glow = human.get_filter("rad_glow") as dm_filter;
            Assert.NotNull(glow);
        });
        float burnBefore = room.In(() => Dm.num(human.get_fire_loss()));

        // 25 s later (RADIATION_TOX_INTERVAL) two more toxin damage; a burn splotch of 11 within 30 to 60 s. The step is 28 s, not
        // 26: the component processes on SSobj's 2 s ticks (irradiated.dm:61), whose phase the Master's first-fire stagger (master.dm:610, :621)
        // shifts when a subsystem is added (SSvis_overlays of M7 slice T3 and the delamination slice's SSsupermatter_cascade moved it past 26 s)
        room.Step(28 * Second);
        Assert.Equal(12f, room.In(() => Dm.num(human.get_tox_loss())));
        room.Step(34 * Second);
        Assert.Equal(14f, room.In(() => Dm.num(human.get_tox_loss())));
        Assert.True(room.In(() => Dm.num(human.get_fire_loss())) >= burnBefore + 11f, "a burn splotch within 60 s (irradiated.dm:143)");

        // the toxins healed, the next process ends the irradiation (irradiated.dm:88-90): the alert and the glow go
        room.In(() => Waiting.NoSleep(human.adjust_tox_loss(-100)));
        room.Step(3 * Second);
        Assert.False(Irradiated(room, human));
        room.In(() =>
        {
            Assert.Null(human.alerts[ALERT_IRRADIATED]);
            Assert.Null(human.get_filter("rad_glow"));
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void TheRadiationSuitAndHoodAndTraitRadimmuneProtectASpaceSuitDoesNot()
    {
        var room = T1.Room(width: 15, height: 15);
        var suited = T1.Human(room, 7, 8);
        var halfSuited = T1.Human(room, 8, 8);
        var immune = T1.Human(room, 9, 8);
        var eva = T1.Human(room, 8, 9);
        room.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(suited.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_suit_utility_radiation>(suited.loc), ITEM_SLOT_OCLOTHING))));
            Assert.True(Dm.truthy(Waiting.NoSleep(suited.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_head_utility_radiation>(suited.loc), ITEM_SLOT_HEAD))));
            Assert.True(Dm.truthy(Waiting.NoSleep(halfSuited.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_suit_utility_radiation>(halfSuited.loc), ITEM_SLOT_OCLOTHING))));
            ADD_TRAIT(immune, TRAIT_RADIMMUNE, "t7_test");
            Assert.True(Dm.truthy(Waiting.NoSleep(eva.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_suit_space_eva>(eva.loc), ITEM_SLOT_OCLOTHING))));
            Assert.True(Dm.truthy(Waiting.NoSleep(eva.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_head_helmet_space_eva>(eva.loc), ITEM_SLOT_HEAD))));
            // the element's trait (radiation_protected_clothing.dm:12) and tg's head-to-toe check (radiation.dm:134)
            Assert.True(HAS_TRAIT(suited.wear_suit, TRAIT_RADIATION_PROTECTED_CLOTHING));
            Assert.True(Dm.truthy(SSradiation.wearing_rad_protected_clothing(suited)));
            Assert.False(Dm.truthy(SSradiation.wearing_rad_protected_clothing(halfSuited)));
            Assert.False(Dm.truthy(SSradiation.can_irradiate_basic(immune)));
        });

        Pulse(room, 8, 8, 3, 0, 100);
        Fire(room);
        Assert.False(Irradiated(room, suited));
        Assert.True(Irradiated(room, halfSuited)); // the head is bare
        Assert.False(Irradiated(room, immune));
        Assert.True(Irradiated(room, eva)); // tg's space suits carry no radiation protection in this snapshot
        Assert.Equal(0f, room.In(() => Dm.num(suited.get_tox_loss())));

        // SSradiation.irradiate keeps the clothing check; the component itself deletes itself on a TRAIT_RADIMMUNE parent
        // (irradiated.dm:30-32), which _component.dm:59 reports as tg's own runtime ("deleted itself before it could be added")
        Assert.Equal(TRUE, room.In(() => SSradiation.irradiate(suited)));
        Assert.False(Irradiated(room, suited));
        Assert.Equal(FALSE, room.In(() => SSradiation.irradiate(immune)));
        Assert.Throws<DmCrashException>(() => room.In(() => Waiting.NoSleep(immune.AddComponent(typeof(datum_component_irradiated)))));
        Assert.False(Irradiated(room, immune));
        Assert.Null(room.In(() => immune.GetComponent(typeof(datum_component_irradiated))));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ARadioactiveEmitterPulsesOnItsCooldown()
    {
        var room = T1.Room(width: 15, height: 15);
        var rock = T1.At<obj_item_crowbar>(room, 8, 8);
        var near = T1.At<obj_item_crowbar>(room, 9, 8);
        var probe = Probe(room, near);
        room.In(() => Waiting.NoSleep(rock.AddComponent(typeof(datum_component_radioactive_emitter), 3 * SECONDS, 2, 0)));
        var emitter = room.In(() => rock.GetComponent(typeof(datum_component_radioactive_emitter)) as datum_component_radioactive_emitter);
        Assert.Equal(3f * SECONDS, room.In(() => Dm.num(emitter.cooldown_time)));
        // 10 s: a pulse on the first process and one every 3 s (radioactive_emitter.dm:72-77), each reaching the crowbar next to it
        room.Step(10 * Second);
        Assert.InRange(probe.InRange.Count, 3, 4);
        Assert.All(probe.InRange, i => Assert.Equal(1f, i));

        // the cooldown is clamped to MIN_PULSE_COOLDOWN (0.5 s) at Initialize; a second AddComponent passes its args to the first
        // (COMPONENT_DUPE_UNIQUE_PASSARGS, InheritComponent: radioactive_emitter.dm:54-69)
        room.In(() => Waiting.NoSleep(rock.AddComponent(typeof(datum_component_radioactive_emitter), 10 * SECONDS, 1, RAD_HEAVY_INSULATION)));
        Assert.Same(emitter, room.In(() => rock.GetComponent(typeof(datum_component_radioactive_emitter))));
        Assert.Equal((10f * SECONDS, 1f, RAD_HEAVY_INSULATION), room.In(() => (Dm.num(emitter.cooldown_time), Dm.num(emitter.range), Dm.num(emitter.threshold))));
        var clamped = T1.At<obj_item_crowbar>(room, 3, 3);
        room.In(() => Waiting.NoSleep(clamped.AddComponent(typeof(datum_component_radioactive_emitter), 1)));
        Assert.Equal(0.5f * SECONDS, room.In(() => Dm.num((clamped.GetComponent(typeof(datum_component_radioactive_emitter)) as datum_component_radioactive_emitter).cooldown_time)));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void TheGeigerCounterReadsTgsPerceivedDangerAndItsSoundLoopsWhilePulsesComeIn()
    {
        // get_perceived_radiation_danger (radiation.dm:60-72)
        var room = T1.Room(width: 15, height: 15);
        room.In(() =>
        {
            datum_radiation_pulse_information P(float threshold, float chance)
            {
                var p = Dm.@new<datum_radiation_pulse_information>();
                p.threshold = threshold;
                p.chance = chance;
                return p;
            }
            Assert.Equal(PERCEIVED_RADIATION_DANGER_EXTREME, Dm.num(get_perceived_radiation_danger(P(0.5f, 30), 0.8f)));
            Assert.Equal(PERCEIVED_RADIATION_DANGER_HIGH, Dm.num(get_perceived_radiation_danger(P(0.5f, 29), 0.8f)));
            Assert.Equal(PERCEIVED_RADIATION_DANGER_MEDIUM, Dm.num(get_perceived_radiation_danger(P(0.8f, 30), 0.4f)));
            Assert.Equal(PERCEIVED_RADIATION_DANGER_LOW, Dm.num(get_perceived_radiation_danger(P(0.8f, 30), 0.5f)));
        });

        var human = T1.Human(room, 8, 8);
        var geiger = T1.At<obj_item_geiger_counter>(room, 8, 8);
        room.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(human.put_in_active_hand(geiger))));
            Waiting.NoSleep(geiger.attack_self(human)); // on
        });
        Assert.Equal("geiger_on_1", room.In(() => geiger.icon_state));
        var sound = room.In(() => (geiger.GetComponent(typeof(datum_component_geiger_sound)) as datum_component_geiger_sound).sound);

        // a pulse through a wall to the holder: the held counter hears it on its holder (geiger_counter.dm:83-86) and its loc
        // (geiger_sound.dm:54-64): insulation 0.7 under a 0.8 threshold, 0.7 / 0.8 above 0.5: LOW
        room.In(() => Waiting.NoSleep(locate(6, 8, 1).ChangeTurf(typeof(turf_closed_wall))));
        Pulse(room, 4, 8, 5, 0.8f, 30);
        Fire(room);
        Assert.Equal(PERCEIVED_RADIATION_DANGER_LOW, room.In(() => Dm.num(geiger.last_perceived_radiation_danger)));
        Assert.Equal("geiger_on_2", room.In(() => geiger.icon_state));
        Assert.NotNull(room.In(() => sound.last_radiation_pulse));
        Assert.True(room.In(() => Dm.truthy(sound.loop_started) || Dm.truthy(sound.timer_id)), "the geiger loop plays");
        Assert.False(Irradiated(room, human));

        // an open line at 30%: EXTREME, and the loop picks from the "ext" sounds (geiger_sound.dm:87)
        Pulse(room, 8, 6, 5, 0.5f, 30);
        Fire(room);
        Assert.Equal(PERCEIVED_RADIATION_DANGER_EXTREME, room.In(() => Dm.num(geiger.last_perceived_radiation_danger)));
        Assert.Equal("geiger_on_5", room.In(() => geiger.icon_state));
        string picked = room.In(() => Dm.str(sound.get_sound()));
        Assert.StartsWith("sound/items/geiger/ext", picked);

        // 5 s without a pulse (TIME_WITHOUT_RADIATION_BEFORE_RESET): the reading resets and the loop stops
        room.Step(6 * Second);
        Assert.Null(room.In(() => geiger.last_perceived_radiation_danger));
        Assert.Equal("geiger_on_1", room.In(() => geiger.icon_state));
        Assert.False(room.In(() => Dm.truthy(sound.loop_started) || Dm.truthy(sound.timer_id)));
        Assert.Null(room.In(() => sound.last_radiation_pulse));

        // the scan: an irradiated target answers the counter's signal (irradiated.dm:179-189)
        room.In(() => Waiting.NoSleep(human.AddComponent(typeof(datum_component_irradiated))));
        object answer = room.In(() => human._SendSignal(COMSIG_GEIGER_COUNTER_SCAN, Dm.list(human, human, geiger)));
        Assert.Equal(COMSIG_GEIGER_COUNTER_SCAN_SUCCESSFUL, Dm.num(answer));
        room.In(() => geiger.scan(human, human));
        room.In(() => geiger.scan(geiger, human)); // a clean target: "free of radioactive contamination"

        // switched off: the sound component goes and the icon is off
        room.In(() => Waiting.NoSleep(geiger.attack_self(human)));
        Assert.Null(room.In(() => geiger.GetComponent(typeof(datum_component_geiger_sound))));
        Assert.Equal("geiger_off", room.In(() => geiger.icon_state));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void AMinimumExposureTimeHoldsTheIrradiationBackUntilTheCountdownHasRun()
    {
        var room = T1.Room(width: 15, height: 15);
        var item = T1.At<obj_item_crowbar>(room, 9, 8);
        // a uranium pulse (radiation_countdown.dm): the first exposure adds the countdown and irradiates nothing
        Pulse(room, 8, 8, 2, 0, 100, URANIUM_RADIATION_MINIMUM_EXPOSURE_TIME);
        Fire(room);
        Assert.False(Irradiated(room, item));
        Assert.NotNull(room.In(() => item.GetComponent(typeof(datum_component_radiation_countdown))));
        // within the exposure time the countdown cancels (CANCEL_IRRADIATION)
        room.Step(1 * Second);
        Pulse(room, 8, 8, 2, 0, 100, URANIUM_RADIATION_MINIMUM_EXPOSURE_TIME);
        Fire(room);
        Assert.False(Irradiated(room, item));
        // after it, SKIP_MINIMUM_EXPOSURE_TIME_CHECK: the pulse goes through
        room.Step(3 * Second);
        Pulse(room, 8, 8, 2, 0, 100, URANIUM_RADIATION_MINIMUM_EXPOSURE_TIME);
        Fire(room);
        Assert.True(Irradiated(room, item));
        // 10 s after the last exposure the countdown removes itself
        room.Step(11 * Second);
        Assert.Null(room.In(() => item.GetComponent(typeof(datum_component_radiation_countdown))));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void TheUraniumAirlockRadiatesAndBreathingTritiumIrradiates()
    {
        var room = T1.Room(width: 15, height: 15);
        var airlock = T1.At<obj_machinery_door_airlock_uranium>(room, 8, 8);
        room.In(() => airlock.radiate());
        room.In(() =>
        {
            var pulse = Assert.Single(Dm.each<datum_radiation_pulse_information>(SSradiation.processing));
            // airlock.dm:2182-2189
            Assert.Equal((2f, RAD_LIGHT_INSULATION, (float)URANIUM_IRRADIATION_CHANCE, URANIUM_RADIATION_MINIMUM_EXPOSURE_TIME),
                (Dm.num(pulse.max_range), Dm.num(pulse.threshold), Dm.num(pulse.chance), Dm.num(pulse.minimum_exposure_time)));
        });
        Fire(room);

        // _lungs.dm:586-590: above tritium_irradiation_moles_min the chance rises to 60% at 15 kPa over it; at 16 kPa, 60%
        var human = T1.Human(room, 3, 3);
        var lungs = room.In(() => human.get_organ_slot(ORGAN_SLOT_LUNGS) as obj_item_organ_lungs);
        int hits = 0;
        for (int i = 0; i < 100; i++)
        {
            room.In(() =>
            {
                var breath = Dm.@new<datum_gas_mixture>(BREATH_VOLUME);
                Waiting.NoSleep(lungs.too_much_tritium(human, breath, 16, 0));
            });
            if (Irradiated(room, human))
            {
                hits++;
                Clean(room, human);
            }
        }
        Assert.InRange(hits, 40, 80);
        T1.NoRuntimes(room);
    }
}
