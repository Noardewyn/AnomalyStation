using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.T2;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M4b;

/// <summary>
/// M4b slice 3 (fire and plasma): tg's gas reactions (reactions.dm) and hotspots (LINDA_fire.dm) in a booted 16 x 16 toy
/// room where SSair fires every 0.5 s: the reactions are indexed by gas; a spark on plasma and oxygen starts a hotspot that
/// burns the plasma into carbon dioxide and water vapour, heats the air, spreads to the tiles around and dies when the fuel
/// is gone; a spark on plain air does nothing; a fire by a firelock closes it (the hot alarm); a human in the fire catches
/// fire, and a burning human stops, drops and rolls when they resist until the fire is out.
/// </summary>
public class FireTests
{
    private const int Second = 20; // ticks per second

    // 20 moles of plasma per tile in normal air: a lean mix (oxygen / plasma below PLASMA_OXYGEN_FULLBURN), which burns in a few
    // seconds per tile
    private const string PlasmaAir = "o2=22;n2=82;plasma=20;TEMP=293.15";

    private static void SetAir(T2Room r, int x, int y, string mix) => r.In(() =>
    {
        var t = (turf_open)locate(x, y, 1);
        t.air = SSair.parse_gas_string(mix) as datum_gas_mixture;
        SSair.add_to_active(t);
    });

    private static float Moles(T2Room r, Type gas) => r.In(() =>
    {
        float total = 0;
        for (int x = 2; x < T2Room.Size; x++)
            for (int y = 2; y < T2Room.Size; y++)
                if (locate(x, y, 1) is turf_open t)
                    total += Dm.num(t.air.moles[gas]);
        return total;
    });

    private static float HottestAir(T2Room r) => r.In(() =>
    {
        float hottest = 0;
        for (int x = 2; x < T2Room.Size; x++)
            for (int y = 2; y < T2Room.Size; y++)
                if (locate(x, y, 1) is turf_open t)
                    hottest = Math.Max(hottest, t.air.temperature);
        return hottest;
    });

    private static List<object> Items(DmList list) => new List<object>(Dm.each(list));

    private static int Hotspots(T2Room r) => r.In(() => (int)SSair.hotspots.len);

    private static obj_effect_hotspot HotspotAt(T2Room r, int x, int y) => r.In(() => ((turf_open)locate(x, y, 1)).active_hotspot);

    [Fact]
    public void TheGasReactionsAreIndexedByTheirGasesAndTheFiresGrowHotspots()
    {
        var r = new T2Room(4301);
        r.In(() =>
        {
            // init_gas_reactions: each reaction sits in the priority group of its rarest gas (the lowest rarity of its
            // requirements, reactions.dm:24); plasma fire needs plasma and oxygen
            var fires = new List<object>();
            foreach ((object gas, object groups) in Dm.pairs(SSair.gas_reactions))
            {
                if (groups is DmList g)
                    foreach (object reaction in Dm.each(g[PRIORITY_FIRE]))
                        fires.Add(reaction);
            }
            Assert.Contains(fires, x => x is datum_gas_reaction_standard_plasmafire);
            Assert.Contains(fires, x => x is datum_gas_reaction_standard_h2fire);
            Assert.Contains(fires, x => x is datum_gas_reaction_standard_tritfire);
            Assert.Contains(fires, x => x is datum_gas_reaction_standard_freonfire);
            float plasma_rarity = Dm.initial(typeof(datum_gas_plasma), (datum_gas p) => p.rarity);
            float oxygen_rarity = Dm.initial(typeof(datum_gas_oxygen), (datum_gas p) => p.rarity);
            Type key = plasma_rarity < oxygen_rarity ? typeof(datum_gas_plasma) : typeof(datum_gas_oxygen);
            DmList plasma_fires = (DmList)((DmList)SSair.gas_reactions[key])[PRIORITY_FIRE];
            Assert.Contains(Items(plasma_fires), x => x is datum_gas_reaction_standard_plasmafire);
            // init_hotspot_reactions: the four fires make a hotspot bigger (expands_hotspot)
            Assert.Equal(4f, SSair.hotspot_reactions.len);
            Assert.Contains(typeof(datum_gas_reaction_standard_plasmafire), Items(SSair.hotspot_reactions));
            // a gas no reaction needs is culled to null
            Assert.Null(SSair.gas_reactions[typeof(datum_gas_helium)]);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASparkOnPlainAirStartsNoFire()
    {
        var r = new T2Room(4302);
        r.Step(Second);
        string reply = r.In(() => DevCommands.Ignite(locate(8, 8, 1)));
        Assert.Contains("nothing caught fire", reply);
        Assert.Equal(0, Hotspots(r));
        r.Step(2 * Second);
        Assert.Equal(0, Hotspots(r));
        Assert.True(HottestAir(r) < 300f, "the air stays cool: " + HottestAir(r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void PlasmaBurnsSpreadsAndBurnsOut()
    {
        var r = new T2Room(4303);
        r.Step(Second);
        for (int x = 6; x <= 10; x++)
            for (int y = 6; y <= 10; y++)
                SetAir(r, x, y, PlasmaAir);
        float plasmaBefore = Moles(r, typeof(datum_gas_plasma));
        Assert.Equal(0f, Moles(r, typeof(datum_gas_carbon_dioxide)));

        string reply = r.In(() => DevCommands.Ignite(locate(8, 8, 1)));
        Assert.Contains("is burning", reply);
        Assert.NotNull(HotspotAt(r, 8, 8));
        Assert.Equal(1, Hotspots(r));

        // the hotspot reacts the air: the plasma burns into carbon dioxide and water vapour and the air heats up past the
        // temperature at which a fire spreads by radiated heat (FIRE_MINIMUM_TEMPERATURE_TO_SPREAD)
        r.Step(5 * Second);
        Assert.True(Moles(r, typeof(datum_gas_plasma)) < plasmaBefore, "plasma burns");
        Assert.True(Moles(r, typeof(datum_gas_carbon_dioxide)) > 0f, "carbon dioxide forms");
        Assert.True(Moles(r, typeof(datum_gas_water_vapor)) > 0f, "water vapour forms");
        Assert.True(HottestAir(r) > FIRE_MINIMUM_TEMPERATURE_TO_SPREAD, "the air is hot: " + HottestAir(r));
        Assert.True(Hotspots(r) > 1, "the fire spread to the tiles around: " + Hotspots(r));

        // without fuel the hotspots die (INSUFFICIENT plasma, LINDA_fire.dm:300)
        int peak = 0;
        for (int i = 0; i < 120 && (Hotspots(r) > 0 || peak == 0); i++)
        {
            r.Step(Second);
            peak = Math.Max(peak, Hotspots(r));
        }
        Assert.Equal(0, Hotspots(r));
        Assert.True(Moles(r, typeof(datum_gas_plasma)) < plasmaBefore * 0.2f, $"most of the plasma burned: {Moles(r, typeof(datum_gas_plasma))} of {plasmaBefore}");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHumanInTheFireCatchesFireAndHeatsUp()
    {
        var r = new T2Room(4304);
        mob_living_carbon_human bob = r.Dummy(8, 8, name: "bob");
        r.Step(Second);
        for (int x = 6; x <= 10; x++)
            for (int y = 6; y <= 10; y++)
                SetAir(r, x, y, PlasmaAir);
        float bodyBefore = r.In(() => bob.bodytemperature);
        r.In(() => DevCommands.Ignite(locate(8, 8, 1)));
        bool burning = false;
        for (int i = 0; i < 10 * 2 && !burning; i++)
        {
            r.Step(Second / 2);
            burning = r.In(() => Dm.truthy(bob.on_fire));
        }
        Assert.True(burning, "the hotspot's fire_act set bob on fire");
        Assert.True(r.In(() => bob.fire_stacks) > 0f);
        Assert.NotNull(r.In(() => bob.has_status_effect(typeof(datum_status_effect_fire_handler_fire_stacks))));
        Assert.NotNull(r.In(() => bob.alerts[ALERT_FIRE]));
        r.Step(4 * Second);
        Assert.True(r.In(() => bob.bodytemperature) > bodyBefore + 10f, $"the fire heats bob: {r.In(() => bob.bodytemperature)} from {bodyBefore}");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABurningHumanStopsDropsAndRollsUntilTheFireIsOut()
    {
        var r = new T2Room(4305);
        mob_living_carbon_human bob = r.Dummy(8, 8, name: "bob");
        r.Step(Second);
        r.In(() =>
        {
            Waiting.NoSleep(bob.adjust_fire_stacks(4));
            Assert.True(Dm.truthy(bob.ignite_mob()));
        });
        Assert.True(r.In(() => Dm.truthy(bob.on_fire)));
        r.Step(2 * Second);
        Assert.True(r.In(() => Dm.truthy(bob.on_fire)), "plain air keeps the fire going");

        // the resist verb (B): a burning carbon that can move stops, drops and rolls (carbon/resist_fire)
        r.In(() => Waiting.NoSleep(bob.execute_resist()));
        Assert.NotNull(r.In(() => bob.has_status_effect(typeof(datum_status_effect_stop_drop_roll))));
        Assert.Equal(LYING_DOWN, r.In(() => bob.body_position));
        bool outOfFire = false;
        for (int i = 0; i < 30 && !outOfFire; i++)
        {
            r.Step(Second / 2);
            outOfFire = r.In(() => !Dm.truthy(bob.on_fire));
        }
        Assert.True(outOfFire, "rolling put the fire out");
        Assert.Equal(0f, r.In(() => bob.fire_stacks));
        Assert.Null(r.In(() => bob.has_status_effect(typeof(datum_status_effect_stop_drop_roll))));
        Assert.Null(r.In(() => bob.has_status_effect(typeof(datum_status_effect_fire_handler_fire_stacks))));
        Assert.Null(r.In(() => bob.alerts[ALERT_FIRE]));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AFireByAFirelockClosesIt()
    {
        var r = new T2Room(4306);
        // a wall at x = 8 with one doorway at (8, 8) holding a firelock; the fire burns on the east side
        r.In(() =>
        {
            for (int y = 2; y < T2Room.Size; y++)
                if (y != 8)
                    Waiting.NoSleep(locate(8, y, 1).ChangeTurf(typeof(turf_closed_indestructible)));
        });
        var door = r.In(() => Dm.@new<obj_machinery_door_firedoor>(locate(8, 8, 1)));
        r.Step(Second);
        Assert.False(r.In(() => Dm.truthy(door.density)));
        for (int x = 9; x <= 12; x++)
            for (int y = 6; y <= 10; y++)
                SetAir(r, x, y, PlasmaAir);
        r.In(() => DevCommands.Ignite(locate(10, 8, 1)));
        bool closed = false;
        for (int i = 0; i < 20 && !closed; i++)
        {
            r.Step(Second / 2);
            closed = r.In(() => Dm.truthy(door.density));
        }
        Assert.True(closed, "the hot air closed the firelock");
        Assert.Equal(FIRELOCK_ALARM_TYPE_HOT, r.In(() => door.alarm_type));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWholeRoomOfPlasmaAirBurnsFromOneSpark()
    {
        // A lean mix (5 moles of plasma to 22 of oxygen per tile) burns hot enough to spread room-wide; a fuel-rich one (30 moles)
        // smoulders around the spark: the burn is oxygen-limited and plasma's specific heat (200, ten times oxygen's) soaks up
        // the heat, so the air stays under FIRE_MINIMUM_TEMPERATURE_TO_SPREAD (tg's numbers, measured 2026-10-13).
        var r = new T2Room(4307);
        r.Step(Second);
        string filled = r.In(() => DevCommands.Gas("o2=22;n2=82;plasma=5;TEMP=293.15"));
        Assert.Contains("open turf(s) now hold", filled);
        r.In(() => DevCommands.Ignite(locate(8, 8, 1)));
        var trace = new List<string>();
        for (int i = 0; i < 20; i++)
        {
            r.Step(Second / 2);
            trace.Add($"{i * 0.5f + 0.5f:0.0}s: {Hotspots(r)} hotspots, hottest {HottestAir(r):0} K, (8,8) {r.In(() => ((turf_open)locate(8, 8, 1)).air.temperature):0} K");
        }
        Assert.True(Hotspots(r) > 20, "the fire spread through the room:\n" + string.Join("\n", trace));
        r.AssertNoRuntimes();
    }
}
