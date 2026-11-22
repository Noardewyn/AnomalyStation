using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The air alarm window (2026-10-16, the last M5 leftover) through the real server on MetaStation: tg's AirAlarm interface. An assistant
/// clicks an air alarm of a room with vents and scrubbers: the window opens locked ("Swipe an ID card to unlock this interface.", no
/// controls, no vents in tg's ui_data); a right click (tg's attack_hand_secondary, togglelock) is refused, the assistant has no
/// engineering access. A debug ID card in the hand swipes it open (item_interaction's GetID): the controls come with the area's vents
/// and scrubbers. Through the translated window: the area's atmosphere alarm raised, a vent turned off, a scrubber set to siphon, the panic siphon mode, the pressure's
/// danger-above threshold set to 600 kPa in the threshold editor; the card locks it again.
/// </summary>
public class AirAlarmGameplayTests
{
    private const int Second = 20;

    private readonly ITestOutputHelper _output;

    public AirAlarmGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private static UiNode ByText(UiNode tree, string text) => tree.Walk().First(n => n.Kind == UiKind.Button && n.Text == text);

    [MetaServerFact]
    public void An_id_card_unlocks_the_air_alarm_whose_window_runs_the_rooms_vents_scrubbers_modes_and_thresholds()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 41, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        int bootRuntimes = w.World.RuntimeErrorCount;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };

        // A powered, complete air alarm of a room with vents and scrubbers (the first in the map's order).
        obj_machinery_airalarm alarm = In(w, () => Dm.each_cast<obj_machinery_airalarm>(GlobalProcs.GLOB.air_alarms).First(a =>
            a.buildstage == AIR_ALARM_BUILD_COMPLETE && !Dm.truthy(Dm.bitand(a.machine_stat, Dm.bitor(NOPOWER, BROKEN))) && a.loc is turf_open
            && Dm.length(a.my_area.air_vents) > 0 && Dm.length(a.my_area.air_scrubbers) > 0));
        _output.WriteLine($"the alarm: {In(w, () => alarm.name)} at {In(w, () => alarm.x)},{In(w, () => alarm.y)}; vents {In(w, () => Dm.length(alarm.my_area.air_vents))}, scrubbers {In(w, () => Dm.length(alarm.my_area.air_scrubbers))}");
        using (w.World.Enter())
            Waiting.NoSleep(me.forceMove(alarm.loc));
        w.Run(Second);

        // An empty hand opens it locked: the lock notice, tg's status, no controls and no vents in the data.
        Assert.True(p.Session.Click(alarm.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("AirAlarm", window.Interface);
        Assert.Equal(1, window.Merged.Num("locked"));
        Assert.Null(window.Merged.Field("vents"));
        UiNode content = Shown(windows, replica, window.Window);
        Assert.Contains("Swipe an ID card to unlock this interface.", content.AllText());
        Assert.Contains("Pressure", content.AllText());
        Assert.Contains("Optimal", content.AllText());
        Assert.DoesNotContain(content.Walk(), n => n.Key == "back");
        Assert.Equal(475, windows.Get(window.Window).Context.WindowWidth);

        // A right click is the lock toggle: the assistant's card has no engineering access.
        Assert.True(p.Session.Click(alarm.dm_id, ClickButton.Right, ClickModifiers.None));
        w.Run(Second);
        Assert.Equal(1f, In(w, () => alarm.locked));

        // A debug ID card in the hand swipes it open; the window updates with the controls.
        using (w.World.Enter())
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(Dm.@new<obj_item_card_id_advanced_debug>(me.loc)))));
        Assert.True(p.Session.Click(alarm.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Equal(0f, In(w, () => alarm.locked));
        Assert.Equal(0, replica.UiWindows[window.Window].Merged.Num("locked"));
        content = Shown(windows, replica, window.Window);
        Assert.Contains("Swipe an ID card to lock this interface.", content.AllText());
        Assert.Contains("Air Controls", content.AllText());

        // Home: the area's atmosphere alarm raised (tg's send_alarm and the hazard danger level at once). The window's atmosAlarm reads
        // area_danger, which only check_danger refreshes: at the turf's next exposure (a few seconds here) tg's check finds no danger and
        // clears the alarm while area_danger takes the raised value, so the window shows the alarm once it is gone (tg's own order).
        ByText(content, "Area Atmosphere Alarm").OnClick();
        w.Run(2);
        Assert.True(In(w, () => Dm.truthy(alarm.my_area.active_alarms[ALARM_ATMOS])), "the area's atmosphere alarm is raised");
        Assert.Equal(AIR_ALARM_ALERT_HAZARD, In(w, () => alarm.danger_level));
        Assert.Equal(2, replica.UiWindows[window.Window].Merged.Num("dangerLevel"));
        Assert.Contains("Danger (Internals Required)", Shown(windows, replica, window.Window).AllText());
        content = Shown(windows, replica, window.Window);

        // Vent Controls: the first vent turned off.
        var vent = In(w, () => Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_pump>(alarm.my_area.air_vents).First());
        string ventRef = In(w, () => (string)GlobalProcs.REF(vent));
        Assert.Equal(1f, In(w, () => vent.on));
        ByText(content, "Vent Controls").OnClick();
        content = Shown(windows, replica, window.Window);
        Assert.Contains("Vent Controls", content.AllText());
        UiNode ventSection = ByKey(content, ventRef);
        ByKey(ventSection, "power").OnClick();
        w.Run(Second);
        Assert.Equal(0f, In(w, () => vent.on));
        content = Shown(windows, replica, window.Window);
        Assert.Equal("Off", ByKey(ByKey(content, ventRef), "power").Text);

        // Scrubber Controls: the first scrubber set to siphon.
        ByKey(content, "back").OnClick();
        content = Shown(windows, replica, window.Window);
        ByText(content, "Scrubber Controls").OnClick();
        content = Shown(windows, replica, window.Window);
        var scrubber = In(w, () => Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_scrubber>(alarm.my_area.air_scrubbers).First());
        string scrubberRef = In(w, () => (string)GlobalProcs.REF(scrubber));
        UiNode scrubberSection = ByKey(content, scrubberRef);
        Assert.Equal("Scrubbing", ByKey(scrubberSection, "scrubbing").Text);
        Assert.Contains(scrubberSection.Walk(), n => n.Kind == UiKind.Button && n.Text == "CO₂" && n.Selected);
        ByKey(scrubberSection, "scrubbing").OnClick();
        w.Run(Second);
        Assert.Equal(0f, In(w, () => scrubber.scrubbing));
        Assert.Equal("Siphoning", ByKey(ByKey(Shown(windows, replica, window.Window), scrubberRef), "scrubbing").Text);

        // Home: the panic siphon.
        ByKey(Shown(windows, replica, window.Window), "back").OnClick();
        content = Shown(windows, replica, window.Window);
        ByText(content, "Panic Siphon").OnClick();
        w.Run(Second);
        Assert.IsType<datum_air_alarm_mode_panic_siphon>(In(w, () => alarm.selected_mode));
        Assert.Equal("danger", ByText(Shown(windows, replica, window.Window), "Panic Siphon").Color);

        // Alarm Thresholds: the pressure's danger above edited to 600 kPa.
        ByText(Shown(windows, replica, window.Window), "Alarm Thresholds").OnClick();
        content = Shown(windows, replica, window.Window);
        UiNode pressureRow = ByKey(content, "tlv:Pressure");
        ByKey(pressureRow, "hazard_max").Children.Single().OnClick();
        content = Shown(windows, replica, window.Window);
        Assert.Contains("Editing the maximum hazard value for pressure...", content.AllText());
        UiNode field = ByKey(content, "threshold:pressure:hazard_max");
        Assert.True(field.CommitOnly);
        field.OnChange(600);
        w.Run(Second);
        Assert.Equal(600f, In(w, () => ((datum_tlv)alarm.tlv_collection["pressure"]).hazard_max));
        content = Shown(windows, replica, window.Window);
        Assert.Equal("600 kPa", ByKey(ByKey(content, "tlv:Pressure"), "hazard_max").Children.Single().Text);
        ByKey(content, "close").OnClick();
        Assert.DoesNotContain(Shown(windows, replica, window.Window).Walk(), n => n.Kind == UiKind.Modal);

        // The card locks it again: the controls go.
        Assert.True(p.Session.Click(alarm.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Equal(1f, In(w, () => alarm.locked));
        content = Shown(windows, replica, window.Window);
        Assert.DoesNotContain(content.Walk(), n => n.Key == "back");
        Assert.Contains("Swipe an ID card to unlock this interface.", content.AllText());

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
