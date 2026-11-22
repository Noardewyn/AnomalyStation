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
/// Silicons SI8 (D326) through the real server on MetaStation with a headless client: Mira, made the AI and then a malfunctioning one,
/// clicks her Malfunction Modules button, buys Blackout in tg's MalfunctionModulePicker and gets its action; she opens the core room's
/// APC from its window and hacks it, and the hack pays her processing time.
/// </summary>
public class MalfGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

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

    [MetaServerFact]
    public void Mira_the_malf_AI_buys_a_module_and_hacks_an_APC()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 84, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player mira = w.Join("Mira");
        w.Run(4 * Second);
        var runtimes = new List<string>();
        Action<Exception> previous = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { runtimes.Add(e.ToString()); previous?.Invoke(e); };
        string Runtimes() => string.Join("\n----\n", runtimes);

        var human = (mob_living_carbon_human)w.MobOf(mira);
        Do(w, () => Waiting.Detach(human.AIize(human.client)));
        w.Run(4 * Second);
        var ai = Assert.IsType<mob_living_silicon_ai>(w.MobOf(mira));
        Do(w, () => Waiting.Detach(ai.mind.add_antag_datum(typeof(datum_antagonist_malf_ai))));
        w.Run(4 * Second);
        Assert.True(In(w, () => Dm.truthy(IS_MALF_AI(ai))), "Mira is malfunctioning; runtimes: " + Runtimes());

        // the Malfunction Modules button (tg's choose_modules action) opens the picker
        long button = In(w, () => Dm.each<atom_movable_screen_movable_action_button>(ai.client.screen).Single(b => b.linked_action is datum_action_innate_choose_modules).dm_id);
        Assert.True(mira.Session.Click(new ClickTarget(TargetKind.Screen, button, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(2 * Second);
        UiWindowState picker = mira.Session.Replica.UiWindows.Values.Single(v => v.Interface == "MalfunctionModulePicker");
        Assert.True(mira.Session.UiAct(picker.Window, "buy", UiValue.ObjectOf(("name", UiValue.Of("Blackout")))));
        w.Run(2 * Second);
        Assert.True(In(w, () => Dm.each<datum_action_innate_ai_blackout>(ai.actions).Any()), "the Blackout ability; runtimes: " + Runtimes());
        output.WriteLine("Blackout bought; processing time " + In(w, () => ai.malf_picker.processing_time));
        float before = In(w, () => ai.malf_picker.processing_time);
        Assert.Equal(35f, before);

        // the core room's APC: its window's hack button
        var apc = In(w, () => Dm.each<obj_machinery_power_apc>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_apc))).Where(a => a.z == ai.z).OrderBy(a => get_dist(a, ai)).First());
        Do(w, () => Waiting.Detach(apc.ui_interact(ai)));
        w.Run(2 * Second);
        UiWindowState window = mira.Session.Replica.UiWindows.Values.Single(v => v.Interface == "Apc");
        Assert.True(mira.Session.UiAct(window.Window, "hack", UiValue.ObjectOf()));
        w.Run(35 * Second);
        Assert.True(In(w, () => Dm.@in(apc, ai.hacked_apcs)), "the APC is hacked; runtimes: " + Runtimes());
        Assert.Same(ai, In(w, () => apc.malfai));
        Assert.True(In(w, () => ai.malf_picker.processing_time) > before, "the hack pays processing time");
        Assert.True(runtimes.Count == 0, Runtimes());
    }
}
