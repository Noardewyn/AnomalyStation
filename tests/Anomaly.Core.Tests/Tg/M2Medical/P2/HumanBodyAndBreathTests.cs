using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

using Anomaly.Core.Tests.Tg.M2Medical.T1;

namespace Anomaly.Core.Tests.Tg.M2Medical.P2;

/// <summary>
/// A recording listener for the breath scenarios (a fixture type of the port's own, <c>/datum/test_m2p2_listener</c>,
/// rules 15): each <c>COMSIG_CARBON_PRE_BREATHE</c> of the carbon it watches (carbon/life.dm:76, sent at the start of every
/// breath that is not blocked), with the world tick and SSmobs' fire count at that moment.
/// </summary>
[DmPath("/datum/test_m2p2_listener")]
public class datum_test_m2p2_listener : datum
{
    public readonly List<(long Tick, float TimesFired)> Breaths = new List<(long, float)>();

    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;

    public void Watch(mob_living_carbon carbon) => RegisterSignal(carbon, COMSIG_CARBON_PRE_BREATHE, nameof(on_breathe));

    [SignalHandler]
    public virtual void on_breathe(datum source = null, object seconds_per_tick = null)
        => Breaths.Add((World.Current.Tick, SSmobs.times_fired));
}

/// <summary>
/// M2 P2 (carbon, human, body and breath) on a booted toy room with <c>NotTranslatedThrows</c> on: a human spawns with tg's
/// body (species, dna, the six default limbs and the organs of the human species), breathes the room's air every 4th
/// SSmobs fire (carbon/life.dm:43-56) and, with 150 toxin damage, falls into hard crit and dies of the missed breaths at the
/// computed tick window (scope.md 2e "Example 1": 76 missed breaths, about 3,000-3,160 ticks).
/// </summary>
public class HumanBodyAndBreathTests
{
    private const int TicksPerSecond = 20;

    private static TgToyRoom StrictRoom(ulong seed)
    {
        TgToyRoom room = TgToyRoom.Boot(seed: seed);
        DmWorldState.Of(room.World).NotTranslatedThrows = true;
        return room;
    }

    private static void AssertNoRuntimes(TgToyRoom room) => Assert.True(room.Runtimes.Count == 0, string.Join("\n", room.RuntimeText));

    [Fact]
    public void AHumanSpawnsWithTgsBodyAndOrgans()
    {
        TgToyRoom room = StrictRoom(61);
        room.In(() =>
        {
            var human = Dm.@new<mob_living_carbon_human>(TgToyRoom.T(4, 4));
            Assert.Same(TgToyRoom.T(4, 4), human.loc);
            // the species and its dna (human.dm:22 create_dna, set_species; dna.dm)
            Assert.IsType<datum_species_human>(human.dna.species);
            Assert.Same(human, human.dna.holder);
            // the six default limbs of the human species (_species.dm bodypart_overrides, carbon.dm create_bodyparts)
            var zones = Dm.each_cast<obj_item_bodypart>(human.get_bodyparts()).Select(p => p.body_zone).OrderBy(z => z).ToArray();
            Assert.Equal(new[] { BODY_ZONE_CHEST, BODY_ZONE_HEAD, BODY_ZONE_L_ARM, BODY_ZONE_L_LEG, BODY_ZONE_R_ARM, BODY_ZONE_R_LEG }.OrderBy(z => z).ToArray(), zones);
            // the organs of the human species (regenerate_organs), each in its slot and owned by the human
            foreach (string slot in new[] { ORGAN_SLOT_BRAIN, ORGAN_SLOT_HEART, ORGAN_SLOT_LUNGS, ORGAN_SLOT_LIVER, ORGAN_SLOT_STOMACH, ORGAN_SLOT_EYES, ORGAN_SLOT_EARS, ORGAN_SLOT_TONGUE, ORGAN_SLOT_APPENDIX })
            {
                var organ = human.get_organ_slot(slot) as obj_item_organ;
                Assert.True(organ != null, slot);
                Assert.Same(human, organ.owner);
            }
            Assert.True(Dm.truthy(((obj_item_organ_heart)human.get_organ_slot(ORGAN_SLOT_HEART)).is_beating()));
            // the eyes lift the organless blindness of setup_organless_effects (human.dm:57, cure_blind(NO_EYES) on insertion)
            Assert.Null(human.has_status_effect(typeof(datum_status_effect_grouped_blindness)));
            Assert.Equal(100f, human.health);
            Assert.Equal(STABLE, human.stat);
            Assert.Equal(0f, Dm.num(human.get_brute_loss()) + Dm.num(human.get_fire_loss()) + Dm.num(human.get_tox_loss()) + Dm.num(human.get_oxy_loss()));
        });
        room.Step(10 * TicksPerSecond); // five SSmobs fires: Life with the breath, the organs and the blood
        room.In(() =>
        {
            var human = Dm.each_cast<mob_living_carbon_human>(GLOB.human_list).Single();
            Assert.Equal(STABLE, human.stat);
            Assert.Equal(100f, human.health);
        });
        AssertNoRuntimes(room);
    }

    [Fact]
    public void ARealAirTurfGivesTheHumanABreathEveryFourthSSmobsFire()
    {
        TgToyRoom room = StrictRoom(62);
        datum_test_m2p2_listener listener = null;
        mob_living_carbon_human human = null;
        float o2Before = 0;
        room.In(() =>
        {
            human = Dm.@new<mob_living_carbon_human>(TgToyRoom.T(4, 4));
            listener = Dm.@new<datum_test_m2p2_listener>();
            listener.Watch(human);
            var air = ((turf_open)human.loc).air;
            Assert.IsType<datum_gas_mixture_turf>(air);
            o2Before = Dm.num(air.moles[typeof(datum_gas_oxygen)]);
            Assert.False(Dm.truthy(air.moles[typeof(datum_gas_carbon_dioxide)]));
        });
        room.Step(40 * TicksPerSecond); // twenty SSmobs fires
        Assert.True(listener.Breaths.Count >= 4, $"{listener.Breaths.Count} breaths");
        // tg: breathe() when SSmobs.times_fired % next_breath == 0, next_breath 4 for healthy lungs and heart
        Assert.All(listener.Breaths, b => Assert.Equal(0f, b.TimesFired % 4));
        for (int i = 1; i < listener.Breaths.Count; i++)
            Assert.Equal(4 * 2 * TicksPerSecond, listener.Breaths[i].Tick - listener.Breaths[i - 1].Tick);
        room.In(() =>
        {
            Assert.False(Dm.truthy(human.failed_last_breath));
            Assert.Equal(0f, Dm.num(human.get_oxy_loss()));
            // each breath takes BREATH_PERCENTAGE of the tile's moles and gives back its oxygen as carbon dioxide (the
            // lungs' breathe_gas_volume, breath_out merged into the breath, the breath merged into the turf)
            var air = ((turf_open)human.loc).air;
            Assert.True(Dm.num(air.moles[typeof(datum_gas_oxygen)]) < o2Before);
            Assert.True(Dm.num(air.moles[typeof(datum_gas_carbon_dioxide)]) > 0);
        });
        AssertNoRuntimes(room);
    }

    [Fact]
    public void ToxinsOfOneHundredFiftyPutAHumanInHardCritAndTheMissedBreathsKillIt()
    {
        TgToyRoom room = StrictRoom(63);
        mob_living_carbon_human human = null;
        long damagedAt = 0;
        room.In(() =>
        {
            human = Dm.@new<mob_living_carbon_human>(TgToyRoom.T(4, 4));
            Waiting.NoSleep(human.adjust_tox_loss(150));
            damagedAt = World.Current.Tick;
            // health 100 - 150 = -50: below HEALTH_THRESHOLD_FULLCRIT (-30), above HEALTH_THRESHOLD_DEAD (-100)
            Assert.Equal(-50f, human.health);
            Assert.Equal(HARD_CRIT, human.stat);
        });
        // In hard crit every breath is missed (carbon/life.dm:93, losebreath) and the empty breath makes the lungs'
        // handle_suffocation deal SUFFOCATION_OXYLOSS * SUFFOCATION_OXYLOSS_CRIT_MODIFIER = 0.66 oxygen damage per SSmobs
        // fire (_lungs.dm:768): 50 more damage after 76 breaths, one every 2 s.
        long diedAt = 0;
        for (int i = 0; i < 200 * TicksPerSecond && diedAt == 0; i++)
        {
            room.Step();
            if (room.In(() => human.stat == DEAD))
                diedAt = room.World.Tick;
        }
        long ticks = diedAt - damagedAt;
        Assert.True(ticks >= 3000 && ticks <= 3160, $"died {ticks} ticks after the damage");
        room.In(() =>
        {
            Assert.True(human.health <= HEALTH_THRESHOLD_DEAD);
            Assert.InRange(Dm.num(human.get_oxy_loss()), 49.5f, 51f);
            Assert.True(Dm.truthy(human.failed_last_breath));
        });
        AssertNoRuntimes(room);
    }

    [Fact]
    public void ARoomWithoutOxygenSuffocatesAHumanAndItsOxygenLossKnocksItOut()
    {
        TgToyRoom room = StrictRoom(64);
        mob_living_carbon_human human = null;
        room.In(() =>
        {
            human = Dm.@new<mob_living_carbon_human>(TgToyRoom.T(4, 4));
            // M4b slice 1: the air moves, so the whole room loses its oxygen
            MedRoom.RoomAir(air => air.set_gas(typeof(datum_gas_oxygen), 0));
        });
        // without oxygen the breath fails: a breath on every SSmobs fire, SUFFOCATION_OXYLOSS (3) each until crit
        room.Step(20 * 2 * TicksPerSecond);
        room.In(() =>
        {
            Assert.True(Dm.truthy(human.failed_last_breath));
            Assert.True(Dm.num(human.get_oxy_loss()) >= OXYLOSS_PASSOUT_THRESHOLD, $"{human.get_oxy_loss()} oxyloss");
            // carbon_defense.dm check_passout: TRAIT_KNOCKEDOUT from oxyloss at OXYLOSS_PASSOUT_THRESHOLD
            Assert.True(HAS_TRAIT_FROM(human, TRAIT_KNOCKEDOUT, OXYLOSS_TRAIT));
            Assert.True(IS_UNCONSCIOUS(human) || human.stat >= SOFT_CRIT);
        });
        AssertNoRuntimes(room);
    }
}
