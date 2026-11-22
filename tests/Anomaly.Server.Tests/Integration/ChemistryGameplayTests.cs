using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Gaps package P5 (D298) through the real server on MetaStation: a chemist at the chemistry lab's dispenser fills a beaker with
/// mercury and chlorine through the ChemDispenser window, heats it in the reaction chamber (ChemHeater) until calomel forms, and
/// prints calomel pills at the ChemMaster (its window, then tg's name prompt); the client sees the windows, the beaker on the
/// dispenser and the pills.
/// </summary>
public class ChemistryGameplayTests
{
    private const int Second = 20; // ticks per second

    private readonly ITestOutputHelper _output;

    public ChemistryGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    private static ClickTarget On(atom a) => new(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    private static turf ClearNeighbour(atom a)
        => new[] { SOUTH, NORTH, EAST, WEST }.Select(d => get_step(a, d) as turf)
            .FirstOrDefault(t => t != null && Clear(t) && Dm.truthy(Waiting.NoSleep(a.Adjacent(t))));

    private static List<string> Seen(ReplicaWorld replica, atom a)
    {
        Assert.True(replica.TryGet(a.dm_id, out ReplicaEntity seen));
        return CryoGameplayTests.States(replica, seen.State.Appearance);
    }

    private static T InLab<T>(RealHeadlessWorld w) where T : atom => In(w, () => Dm.each<T>()
        .Where(m => m.GetType() == typeof(T) && m.z == 1 && get_area(m) is area_station_medical_chemistry && ClearNeighbour(m) != null).OrderBy(m => m.dm_id).FirstOrDefault());

    private static UiWindowState Window(ReplicaWorld replica, string iface) => replica.UiWindows.Tgui().LastOrDefault(win => win.Interface == iface);

    [MetaServerFact]
    public void A_chemist_dispenses_heats_calomel_and_prints_pills_and_the_client_sees_the_windows_and_the_pills()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 97, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Chemist");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        var dispenser = InLab<obj_machinery_chem_dispenser>(w);
        var heater = InLab<obj_machinery_chem_heater_withbuffer>(w);
        var master = InLab<obj_machinery_chem_master>(w);
        Assert.NotNull(dispenser);
        Assert.NotNull(heater);
        Assert.NotNull(master);
        Assert.False(In(w, () => InertSeam.IsInert(dispenser) || InertSeam.IsInert(heater) || InertSeam.IsInert(master)));
        _output.WriteLine(In(w, () => $"dispenser {dispenser.x},{dispenser.y}; heater {heater.x},{heater.y}; ChemMaster {master.x},{master.y}"));

        // the chemist's outfit and an empty large beaker in hand
        var beaker = In(w, () =>
        {
            foreach (obj_item worn in Dm.each<obj_item>(me.get_equipped_items(INCLUDE_POCKETS)))
                qdel(worn);
            Waiting.NoSleep(me.equip_outfit_and_loadout(Dm.@new<datum_outfit_job_chemist>()));
            Waiting.NoSleep(me.drop_all_held_items());
            var b = Dm.@new<obj_item_reagent_containers_cup_beaker_large>(me.loc);
            Waiting.NoSleep(me.put_in_active_hand(b));
            return b;
        });

        // 1. the dispenser: a click with the beaker puts it in and opens the window; Mercury and Chlorine at 10 units
        Do(w, () => Waiting.NoSleep(me.forceMove(ClearNeighbour(dispenser))));
        w.Run(Second);
        Assert.True(p.Session.Click(On(dispenser), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Same(dispenser, In(w, () => beaker.loc));
        Assert.Contains("disp_beaker", Seen(replica, dispenser));
        UiWindowState window = Window(replica, "ChemDispenser");
        Assert.NotNull(window);
        UiNode content = Shown(windows, replica, window.Window);
        content.Walk().First(n => n.Key == "amount:10").OnClick();
        w.Run(Second);
        content = Shown(windows, replica, window.Window);
        content.Walk().First(n => n.Key == "dispense:Mercury:").OnClick();
        content.Walk().First(n => n.Key == "dispense:Chlorine:").OnClick();
        w.Run(Second);
        Assert.Equal(10f, In(w, () => Dm.num(((datum_reagents)beaker.reagents).get_reagent_amount(typeof(datum_reagent_mercury)))));
        Assert.Equal(10f, In(w, () => Dm.num(((datum_reagents)beaker.reagents).get_reagent_amount(typeof(datum_reagent_chlorine)))));
        content = Shown(windows, replica, window.Window);
        Assert.Contains(content.Walk(), n => n.Text == "10 units of Mercury");
        content.Walk().First(n => n.Key == "eject").OnClick();
        w.Run(Second);
        Assert.Same(me, In(w, () => beaker.loc));
        p.Session.UiClose(window.Window);
        w.Run(Second);

        // 2. the reaction chamber: the beaker in, 420 K and the power: calomel forms above 374 K
        Do(w, () => Waiting.NoSleep(me.forceMove(ClearNeighbour(heater))));
        w.Run(Second);
        Assert.True(p.Session.Click(On(heater), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Same(heater, In(w, () => beaker.loc));
        Assert.Contains("mixer1b", Seen(replica, heater));
        window = Window(replica, "ChemHeater");
        Assert.NotNull(window);
        content = Shown(windows, replica, window.Window);
        content.Walk().First(n => n.Key == "targetTemp").OnChange(420);
        content.Walk().First(n => n.Key == "power").OnClick();
        w.Run(40 * Second);
        float calomel = In(w, () => Dm.num(((datum_reagents)beaker.reagents).get_reagent_amount(typeof(datum_reagent_medicine_calomel))));
        _output.WriteLine(In(w, () => $"beaker at {((datum_reagents)beaker.reagents).chem_temp} K, calomel {calomel}"));
        Assert.True(calomel > 0, "calomel formed in the chamber");
        content = Shown(windows, replica, window.Window);
        content.Walk().First(n => n.Key == "eject").OnClick();
        w.Run(Second);
        p.Session.UiClose(window.Window);
        w.Run(Second);

        // 3. the ChemMaster: the beaker in, all of it to the buffer, a pill, print two and name them
        Do(w, () => Waiting.NoSleep(me.forceMove(ClearNeighbour(master))));
        w.Run(Second);
        Assert.True(p.Session.Click(On(master), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Same(master, In(w, () => beaker.loc));
        window = Window(replica, "ChemMaster");
        Assert.NotNull(window);
        content = Shown(windows, replica, window.Window);
        foreach (UiNode all in content.Walk().Where(n => n.Key == "all").ToList())
            all.OnClick();
        w.Run(Second);
        Assert.True(In(w, () => ((datum_reagents)master.reagents).total_volume) > 0, "the buffer holds the beaker's reagents");
        content = Shown(windows, replica, window.Window);
        content.Walk().First(n => n.Key != null && n.Key.StartsWith("container:", StringComparison.Ordinal) && n.DmIconState != null && n.DmIconState.StartsWith("pill", StringComparison.Ordinal)).OnClick();
        w.Run(Second);
        content = Shown(windows, replica, window.Window);
        content.Walk().First(n => n.Key == "count").OnChange(2);
        content = Shown(windows, replica, window.Window);
        content.Walk().First(n => n.Key == "print").OnClick();
        w.Run(Second);
        UiWindowState prompt = Window(replica, "TextInputModal");
        Assert.NotNull(prompt);
        p.Session.UiAct(prompt.Window, "submit", UiValue.ObjectOf(("entry", UiValue.Of("calomel pill"))));
        w.Run(4 * Second);
        var pills = In(w, () => Dm.each<obj_item_reagent_containers_applicator_pill>().Where(pill => Dm.str(pill.name) == "calomel pill").ToList());
        Assert.Equal(2, pills.Count);
        Assert.True(In(w, () => Dm.num(((datum_reagents)pills[0].reagents).get_reagent_amount(typeof(datum_reagent_medicine_calomel)))) > 0, "the pill holds calomel");
        Assert.True(replica.TryGet(In(w, () => pills[0].dm_id), out _), "the client sees the printed pill");

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
