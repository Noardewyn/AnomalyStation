using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M1Client.P1;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The mining and labour shuttles' trips between the station and Lavaland, through their ShuttleConsole windows on the converted
/// MetaStation: the mining shuttle flies its passengers and cargo down from the station (igniting, then through transit with the
/// ripples at the Lavaland dock, its airlocks shutting) and is called back from the mining base's console; the labour shuttle is
/// summoned by the camp's one-way console and sent home by a guard, not by a prisoner; and, in a toy room, the mining console's
/// warning to a head revolutionary and its lock for an exile.
/// </summary>
public class MiningShuttleTests
{
    private readonly ITestOutputHelper _output;

    public MiningShuttleTests(ITestOutputHelper output) => _output = output;

    private static string[] Ids(JsonElement data) => data.GetProperty("locations").EnumerateArray().Select(l => l.GetProperty("id").GetString()).ToArray();

    [MetaFact]
    public void The_mining_shuttle_flies_its_crew_and_cargo_down_to_Lavaland_and_is_called_back_from_the_base()
    {
        var m = MetaRound.Boot(13020);
        obj_docking_port_mobile shuttle = null;
        obj_machinery_computer_shuttle_mining aboard = null, baseConsole = null;
        mob_living_carbon_human passenger = null;
        obj_structure_closet_crate crate = null;
        float lavaland = 0;
        m.In(() =>
        {
            shuttle = (obj_docking_port_mobile)SSshuttle.getShuttle("mining");
            Assert.Equal("mining_home", Dm.str(((obj_docking_port_stationary)shuttle.get_docked()).shuttle_id));
            lavaland = ((obj_docking_port_stationary)SSshuttle.getDock("mining_away")).z;
            aboard = Dm.each<obj_machinery_computer_shuttle_mining>(Dm.world).First(c => c.GetType() == typeof(obj_machinery_computer_shuttle_mining) && get_area(c) is area_shuttle_mining);
            baseConsole = Dm.each<obj_machinery_computer_shuttle_mining>(Dm.world).First(c => c.GetType() == typeof(obj_machinery_computer_shuttle_mining) && Dm.eq(c.z, lavaland) && get_area(c) is not area_shuttle_mining);
            Assert.False(InertSeam.IsInert(aboard));
            Assert.False(InertSeam.IsInert(baseConsole));
            var floors = Dm.each<turf>(shuttle.return_turfs()).OfType<turf_open>().Where(t => !Dm.each<atom>(t.contents).Any(a => Dm.truthy(a.density))).ToList();
            passenger = Dm.@new<mob_living_carbon_human>(floors[0]);
            crate = Dm.@new<obj_structure_closet_crate>(floors[^1]);
        });
        var miner = m.Join("Mina", aboard);

        JsonElement data = m.Open(miner, aboard, out string window);
        _output.WriteLine("aboard: " + data);
        Assert.Contains("mining_away", Ids(data));
        Assert.Equal("Idle", data.GetProperty("status").GetString());
        Assert.Equal(0, data.GetProperty("locked").GetInt32());
        m.Act(miner, window, "move", ("shuttle_id", "mining_away"));
        Assert.True(m.Feed.Any("Shuttle departing. Please stand away from the doors."), string.Join(" | ", m.Feed.Chat.Select(c => c.Html)));
        // igniting, a second launch is refused
        Assert.Equal(SHUTTLE_IGNITING, m.In(() => Dm.str(shuttle.mode)));
        m.Act(miner, window, "move", ("shuttle_id", "mining_away"));
        Assert.True(m.Feed.Any("Shuttle already in transit."), string.Join(" | ", m.Feed.Chat.Select(c => c.Html)));

        // in transit, the ripples mark the dock it will land on in its last SHUTTLE_RIPPLE_TIME
        int ticks = 2 * 10; // the two acts' steps
        while (ticks < 1200 && m.In(() => Dm.each(shuttle.ripples).Count() == 0))
        {
            m.Step(1);
            ticks++;
        }
        m.In(() =>
        {
            Assert.Equal(SHUTTLE_CALL, Dm.str(shuttle.mode));
            Assert.True(Dm.num(shuttle.timeLeft(1)) <= SHUTTLE_RIPPLE_TIME);
            Assert.NotEmpty(Dm.each(shuttle.ripples));
            Assert.All(Dm.each<obj_effect_abstract_ripple>(shuttle.ripples), ripple => Assert.Equal(lavaland, ripple.z));
        });
        ticks += m.FlyTo(shuttle, "mining_away");
        float expected = m.In(() => (shuttle.ignitionTime + shuttle.callTime) / Dm.world.tick_lag);
        _output.WriteLine($"down in {ticks} ticks from the first act (ignition and flight {expected})");
        // the first act lands within its ten steps (the verb queue), the shuttle subsystem fires once a second
        Assert.InRange(ticks, expected - 4, expected + 3 * Second + 10);
        m.In(() =>
        {
            Assert.Equal(lavaland, passenger.z);
            Assert.Equal(lavaland, crate.z);
            Assert.Equal(lavaland, miner.z);
            Assert.IsType<area_shuttle_mining>(get_area(passenger));
            Assert.Empty(Dm.each(shuttle.ripples));
        });
        // an ordinary shuttle flies silent in tg (the hyperspace sounds are the emergency and arrivals shuttles'): its airlocks shut
        string heard() => "heard: " + string.Join(", ", m.Sounds.Heard.Where(s => ReferenceEquals(s.Target, miner)).Select(s => s.File).Distinct());
        Assert.True(m.Sounds.Heard.Any(s => ReferenceEquals(s.Target, miner) && (s.File ?? "").EndsWith("airlockclose.ogg", StringComparison.Ordinal)), heard());

        // the miner gets up after the landing's jolt, steps off at the base, and calls the shuttle home from the base's console
        m.Step(10 * Second);
        m.In(() =>
        {
            turf spot = Dm.each<turf>(orange(1, baseConsole)).OfType<turf_open>().First(t => !Dm.truthy(t.density) && !Dm.each<atom>(t.contents).Any(a => Dm.truthy(a.density)) && get_area(t) is not area_shuttle_mining);
            Waiting.NoSleep(miner.forceMove(spot));
        });
        data = m.Open(miner, baseConsole, out window);
        _output.WriteLine("base: " + data);
        Assert.Contains("mining_home", Ids(data));
        Assert.DoesNotContain("mining_away", Ids(data));
        m.Act(miner, window, "move", ("shuttle_id", "mining_home"));
        m.FlyTo(shuttle, "mining_home");
        m.In(() =>
        {
            Assert.Equal(1f, passenger.z);
            Assert.Equal(1f, crate.z);
            Assert.Equal(lavaland, miner.z);
            Assert.True(Dm.truthy(is_station_level(passenger.z)));
        });
        var errors = m.Log.Runtimes.Where(e => (e.ToString() ?? "").Contains("shuttle", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Empty(errors);
    }

    [MetaFact]
    public void The_labour_shuttle_is_summoned_to_the_camp_and_only_a_guard_sends_it_home()
    {
        var m = MetaRound.Boot(13021);
        obj_docking_port_mobile shuttle = null;
        obj_machinery_computer_shuttle_labor_one_way camp = null;
        obj_machinery_computer_shuttle_labor aboard = null;
        float lavaland = 0;
        m.In(() =>
        {
            shuttle = (obj_docking_port_mobile)SSshuttle.getShuttle("laborcamp");
            Assert.Equal("laborcamp_home", Dm.str(((obj_docking_port_stationary)shuttle.get_docked()).shuttle_id));
            lavaland = ((obj_docking_port_stationary)SSshuttle.getDock("laborcamp_away")).z;
            camp = Dm.each<obj_machinery_computer_shuttle_labor_one_way>(Dm.world).Single();
            Assert.Equal(lavaland, camp.z);
            aboard = Dm.each<obj_machinery_computer_shuttle_labor>(Dm.world).First(c => c is not obj_machinery_computer_shuttle_labor_one_way && get_area(c) is area_shuttle_labor);
        });
        var prisoner = m.Join("Pat", camp);
        JsonElement data = m.Open(prisoner, camp, out string window);
        _output.WriteLine("camp: " + data);
        Assert.Equal(new[] { "laborcamp_away" }, Ids(data));
        m.Act(prisoner, window, "move", ("shuttle_id", "laborcamp_away"));
        m.FlyTo(shuttle, "laborcamp_away");
        // docked at the camp, the one-way console has nowhere left to send it
        data = m.Open(prisoner, camp, out window);
        Assert.Empty(Ids(data));
        Assert.Equal("Locked", data.GetProperty("status").GetString());

        // the prisoner boards: the shuttle's own console wants the brig's access
        m.In(() =>
        {
            turf spot = Dm.each<turf>(orange(1, aboard)).OfType<turf_open>().First(t => !Dm.truthy(t.density) && !Dm.each<atom>(t.contents).Any(a => Dm.truthy(a.density)) && get_area(t) is area_shuttle_labor);
            Waiting.NoSleep(prisoner.forceMove(spot));
        });
        data = m.Open(prisoner, aboard, out window);
        Assert.Contains("laborcamp_home", Ids(data));
        m.Act(prisoner, window, "move", ("shuttle_id", "laborcamp_home"));
        Assert.True(m.Feed.Any("Access denied."), string.Join(" | ", m.Feed.Chat.Select(c => c.Html)));
        Assert.Equal("laborcamp_away", m.Docked(shuttle));

        var guard = m.Join("Gus", aboard, typeof(datum_id_trim_job_security_officer));
        data = m.Open(guard, aboard, out window);
        m.Act(guard, window, "move", ("shuttle_id", "laborcamp_home"));
        m.FlyTo(shuttle, "laborcamp_home");
        m.In(() =>
        {
            Assert.Equal(1f, prisoner.z);
            Assert.Equal(1f, guard.z);
            Assert.IsType<area_shuttle_labor>(get_area(prisoner));
        });
    }

    [Fact]
    public void The_mining_console_warns_a_head_revolutionary_once_and_locks_for_an_exile()
    {
        var r = new AudioRoom(12, 9, seed: 13022);
        Feed feed = Listen(r);
        r.In(() =>
        {
            SSmapping.z_list = Dm.list();
            SSmapping.z_level_to_plane_offset ??= Dm.list();
            SSmapping.z_level_to_lowest_plane_offset ??= Dm.list();
            SSmapping.gravity_by_z_level ??= Dm.list();
            SSmapping.z_level_to_stack ??= Dm.list();
            SSmapping.manage_z_level(Dm.@new<datum_space_level>(1, "station", Dm.list(Dm.kv(ZTRAIT_STATION, TRUE))), FALSE, contain_turfs: FALSE);
        });
        var console = r.In(() => Dm.@new<obj_machinery_computer_shuttle_mining>(locate(5, 5, 1)));
        var head = r.Join(5, 4, "Hank");
        var exile = r.Join(6, 4, "Ezra");
        r.Step(Second);
        r.In(() =>
        {
            GLOB.revolution_handler ??= Dm.@new<datum_revolution_handler>();
            var datum = Dm.@new<datum_antagonist_rev_head>();
            Waiting.NoSleep(head.mind.add_antag_datum(datum, GLOB.revolution_handler.revs));
            Assert.NotNull(head.mind.has_antag_datum(typeof(datum_antagonist_rev_head)));
            var implant = Dm.@new<obj_item_implant_exile>();
            Assert.True(Dm.truthy(Waiting.NoSleep(implant.implant(exile, null, TRUE, TRUE))));
        });
        r.In(() => Waiting.NoSleep(console.attack_hand(head)));
        Assert.True(feed.Any("You get a feeling that leaving the station might be a REALLY dumb idea..."), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.True(r.In(() => Dm.@in(head.mind, obj_machinery_computer_shuttle.dumb_rev_heads)));
        int warnings = feed.Chat.Count(c => (c.Html ?? "").Contains("REALLY dumb idea", StringComparison.Ordinal));
        r.In(() => Waiting.NoSleep(console.attack_hand(head)));
        Assert.Equal(warnings, feed.Chat.Count(c => (c.Html ?? "").Contains("REALLY dumb idea", StringComparison.Ordinal)));

        r.In(() => Waiting.NoSleep(console.attack_hand(exile)));
        Assert.True(feed.Any("the shuttle controls lock in response to your exile implant."), string.Join(" | ", feed.Chat.Select(c => c.Html)));
    }
}

/// A booted Meta at round start with the players' commands run in the world's own phase, as the host runs them.
internal sealed class MetaRound
{
    public World World;
    public TgTestWorld.Log Log;
    public readonly Feed Feed = new();
    public readonly SoundCapture Sounds = new();
    public readonly Queue<Action> Commands = new();

    public T In<T>(Func<T> body)
    {
        using (World.Enter())
            return body();
    }

    public void In(Action body)
    {
        using (World.Enter())
            body();
    }

    public void Step(int ticks = 1)
    {
        for (int i = 0; i < ticks; i++)
            World.Step();
    }

    public static MetaRound Boot(ulong seed)
    {
        var meta = new MetaRound();
        meta.World = MetaMaps.Boot(out meta.Log, seed);
        TgTestWorld.RunLevel(meta.World, RUNLEVEL_GAME);
        meta.In(() =>
        {
            PresentationSinks.Of(meta.World).Messages = meta.Feed;
            PresentationSinks.Of(meta.World).Sounds = meta.Sounds;
            ClientSeam.SetHost(meta.World, new RecordingHost());
            meta.World.AddPhase("test player commands", _ =>
            {
                while (meta.Commands.Count > 0)
                    meta.Commands.Dequeue()();
            });
        });
        return meta;
    }

    /// A human of <paramref name="key"/> on the open tile beside <paramref name="near"/>, in a jumpsuit and with an ID of <paramref name="trim"/>.
    public mob_living_carbon_human Join(string key, atom near, Type trim = null)
    {
        mob_living_carbon_human body = In(() =>
        {
            turf spot = Dm.each<turf>(orange(1, near)).OfType<turf_open>().First(t => !Dm.truthy(t.density) && !Dm.each<atom>(t.contents).Any(a => Dm.truthy(a.density)));
            var human = Dm.@new<mob_living_carbon_human>(spot);
            Waiting.NoSleep(human.equip_to_slot_or_del(Dm.@new<obj_item_clothing_under_color_grey>(spot), ITEM_SLOT_ICLOTHING));
            if (trim != null)
            {
                var card = Dm.@new<obj_item_card_id_advanced>(spot);
                Waiting.NoSleep(SSid_access.apply_trim_to_card(card, trim));
                card.registered_name = key;
                Waiting.NoSleep(human.equip_to_slot_or_del(card, ITEM_SLOT_ID));
            }
            return human;
        });
        Commands.Enqueue(() =>
        {
            ClientSeam.KeyMob(body, key);
            ClientSeam.Connect(key);
        });
        Step(2);
        Assert.NotNull(body.client);
        return body;
    }

    /// The player clicks <paramref name="console"/> and reads its window's newest data; the window's id comes out.
    public JsonElement Open(mob who, atom console, out string windowId, string ui = "ShuttleConsole")
    {
        In(() => who.client.dm_tgui_output.Clear());
        Commands.Enqueue(() => ClientSeam.Click(who.client, console, "icon-x=16;icon-y=16;left=1;button=left"));
        Step(4);
        List<TguiOutput> outputs = In(() => GapsHarness.Take(who));
        JsonElement payload = LastUpdate(outputs, out windowId);
        Assert.Equal(ui, InterfaceOf(payload));
        return payload.GetProperty("data");
    }

    public void Act(mob who, string windowId, string action, params (string Key, object Value)[] args)
    {
        Commands.Enqueue(() =>
        {
            DmList payload = Dm.list();
            foreach ((string key, object value) in args)
                payload[key] = value;
            ClientSeam.TguiMessage(who.client, windowId, "act/" + action, payload);
        });
        Step(10); // the act waits in the verb queue for its subsystem's next fire
    }

    /// Steps until <paramref name="shuttle"/> docks at <paramref name="dockId"/>; the ticks it took out.
    public int FlyTo(obj_docking_port_mobile shuttle, string dockId, int limit = 1200)
    {
        int ticks = 0;
        while (ticks < limit && !In(() => shuttle.get_docked() is obj_docking_port_stationary d && Dm.eq(d.shuttle_id, dockId)))
        {
            World.Step();
            ticks++;
        }
        Assert.True(ticks < limit, $"the shuttle did not reach {dockId}");
        return ticks;
    }

    public string Docked(obj_docking_port_mobile shuttle) => In(() => Dm.str((shuttle.get_docked() as obj_docking_port_stationary)?.shuttle_id));
}
