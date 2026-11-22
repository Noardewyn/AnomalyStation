using System.Globalization;
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
/// M7 slice T3 (D164): the gas analyzer on MetaStation through the real server. A player takes an analyzer, uses it (Z, tg's attack_self)
/// on the tile he stands on, and his chat shows tg's readout of that tile's air: the moles, each gas's share, the temperature, the
/// volume and the pressure, with tg's rounding. He right-clicks the analyzer in hand (attack_self_secondary) and the translated
/// GasAnalyzer window opens with the scan in it (the mix's name, its gases with tg's "mol (%)" text, the temperature, volume and
/// pressure) and the atmos handbook under it: a click on the mix's Oxygen button shows the gas page (its specific heat), and the
/// reactions tab lists the reactions with their factor texts.
/// </summary>
public class GasAnalyzerGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static RealHeadlessWorld Meta(int seed) => new(new LoopbackOptions { Seed = (ulong)seed, TimeoutSteps = 12000 },
        MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);

    private static string Fixed(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

    [MetaServerFact]
    public void A_player_scans_the_room_he_stands_in_and_reads_tgs_values_in_his_chat_and_in_the_analyzers_window()
    {
        using RealHeadlessWorld w = Meta(71);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var texts = new List<string>();
        p.Client.ServerTextReceived += t => texts.Add(t.Html);
        In(w, () => Waiting.NoSleep(me.forceMove(locate(46, 122, 1))));
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;

        w.Host.EnqueueConsoleLine("give Engineer /obj/item/analyzer");
        w.Run(5);
        var analyzer = In(w, () => me.get_active_held_item() as obj_item_analyzer);
        Assert.NotNull(analyzer);
        texts.Clear();
        p.Session.Keybinding(KeybindingCode.ActivateInhand); // Z: attack_self, scan_atom of his own tile
        w.Run(Second);

        // what tg computes for that tile now (no tick has passed between the scan and this read but the second of waiting: the mix is read again)
        (double Moles, double Pressure, double Temperature, double Volume, List<(string Name, double Amount)> Gases) Air() => In(w, () =>
        {
            datum_gas_mixture air = ((turf_open)get_turf(me)).air;
            var names = (DmList)GLOB.meta_gas_info[META_GAS_NAME];
            return (Dm.num(air.total_moles()), Dm.num(air.return_pressure()), Dm.num(air.temperature), Dm.num(air.volume),
                Dm.pairs(air.moles).Select(kv => (Dm.str(names[kv.key]), (double)Dm.num(kv.value))).ToList());
        });
        var now = Air();
        string readout = texts.FirstOrDefault(t => t.Contains("Results of analysis of", StringComparison.Ordinal));
        Assert.NotNull(readout);
        output.WriteLine(readout);
        Assert.Contains("boxed_message", readout);
        Assert.Contains("Moles: ", readout);
        Assert.Contains("Pressure: ", readout);
        Assert.Contains("Volume: " + Dm.str(now.Volume) + " L", readout);
        // the oxygen and nitrogen of the station air, each with its share to tg's rounding
        foreach ((string name, double amount) in now.Gases.Where(g => g.Amount > 1))
            Assert.Contains(name + ": ", readout);
        double scannedPressure = double.Parse(readout.Split("Pressure: ")[1].Split(' ')[0], CultureInfo.InvariantCulture);
        Assert.InRange(scannedPressure, now.Pressure - 1, now.Pressure + 1); // the room's air is quiet: the same to within a kilopascal
        Assert.InRange(scannedPressure, 80, 130); // Meta's station air
        double scannedMoles = double.Parse(readout.Split("Moles: ")[1].Split(' ')[0], CultureInfo.InvariantCulture);
        Assert.InRange(scannedMoles, now.Moles * 0.95, now.Moles * 1.05);

        // the right-click opens the window with the scan in it
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        UiNode Shown(int index)
        {
            windows.Sync(replica.UiWindows, 1280, 720, 1);
            return windows.Get(index)?.Content;
        }
        In(w, () => Waiting.NoSleep(analyzer.attack_self_secondary(me)));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("GasAnalyzer", window.Interface);
        UiValue mix = window.Merged.Arr("gasmixes").Single();
        Assert.True(mix.Num("total_moles") > 0);
        Assert.Equal(20, window.Merged.Arr("gasInfo").Count); // the handbook rides in the static data
        Assert.True(window.Merged.Arr("reactionInfo").Count >= 23);
        UiNode content = Shown(window.Window);
        Assert.Equal(500, windows.Get(window.Window).Context.WindowWidth);
        string text = content.AllText();
        output.WriteLine(text);
        Assert.Contains("Oxygen", text);
        Assert.Contains("Nitrogen", text);
        Assert.Contains("Temperature", text);
        Assert.Contains("Pressure", text);
        Assert.Contains(" kPa", text);
        Assert.Contains("No Gas Selected", text);
        Assert.Contains(Fixed(mix.Num("pressure")) + " kPa", text);
        // the mix's Oxygen button opens the gas page of the handbook (Specific Heat: 20 J/(mol * K))
        UiNode oxygen = content.Walk().First(n => n.Kind == UiKind.Button && n.Key == "gas:o2");
        oxygen.OnClick();
        content = Shown(window.Window);
        text = content.AllText();
        Assert.Contains("> Oxygen", text);
        Assert.Contains("Specific Heat:", text);
        Assert.Contains("J/(mol * K)", text);
        Assert.Contains("Relevant Reactions", text);
        // the Reactions tab lists the reactions; a reaction's page has its factor texts
        content.Walk().First(n => n.Kind == UiKind.Tab && n.Key == "tab:reactions").OnClick();
        content = Shown(window.Window);
        Assert.Contains("No reaction selected", content.AllText());
        content.Walk().First(n => n.Kind == UiKind.Button && n.Key == "reactionlistbutton:plasmafire").OnClick();
        content = Shown(window.Window);
        text = content.AllText();
        Assert.Contains("Plasma Combustion", text.Replace("Plasma combustion", "Plasma Combustion", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Oxygen", text);
        Assert.Contains("Temperature:", text);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
