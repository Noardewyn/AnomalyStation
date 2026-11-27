using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The two station-trait jobs on MetaStation through the real server (tg's lobby, station traits on): during the pregame the Veteran
/// Advisor and the Human AI traits are set up (as an admin's planned traits are); the lobby shows their sign-up buttons, Alice signs up
/// for the veteran and Bob for the human AI, both ready, and the round starts with Alice as the Veteran Security Advisor on a security
/// officer's spot in his formal uniform with his cane and his PTSD, and Bob as the Human AI at the AI's spot in the satellite with his
/// robotic ID, his omni door remote and his robotic tongue.
/// </summary>
public sealed class TraitJobGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static TguiWindows WindowsOf(RealHeadlessWorld.Player p) => new()
    {
        SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
        SendClose = window => p.Session.UiClose(window),
    };

    private static TguiWindow Window(TguiWindows windows, RealHeadlessWorld.Player p, string @interface)
    {
        windows.Sync(p.Session.Replica.UiWindows, 1280, 720, 1);
        return windows.Open.FirstOrDefault(x => x.Interface == @interface);
    }

    private static void RunUntil(RealHeadlessWorld w, Func<bool> done, int ticks, string what)
    {
        for (int i = 0; i < ticks && !done(); i++)
            w.Run(1);
        Assert.True(done(), what);
    }

    private static mob MobOf(RealHeadlessWorld w, RealHeadlessWorld.Player p)
        => w.World.Objects.TryGet(w.Host.AvatarOf(p.Name), out object o) ? o as mob : null;

    // The player signs up with the trait's lobby button and readies
    private static void SignUp(RealHeadlessWorld w, RealHeadlessWorld.Player p, string trait)
    {
        TguiWindows windows = WindowsOf(p);
        RunUntil(w, () => Window(windows, p, Limits.LobbyUiInterface)?.Context.Data.Arr("stationTraits").Any(t => t.Str("name") == trait) == true,
            5 * Second, "the lobby shows the " + trait + " button");
        TguiWindow lobby = Window(windows, p, Limits.LobbyUiInterface);
        UiValue button = lobby.Context.Data.Arr("stationTraits").Single(t => t.Str("name") == trait);
        lobby.Context.Act("sign_up", ("ref", button.Str("ref")));
        RunUntil(w, () => Window(windows, p, Limits.LobbyUiInterface).Context.Data.Arr("stationTraits").Single(t => t.Str("name") == trait).Str("iconState") == "signup_on",
            2 * Second, p.Name + " signed up for " + trait);
        Window(windows, p, Limits.LobbyUiInterface).Context.Act("ready_toggle");
        w.Run(Second / 2);
        Assert.True(Window(windows, p, Limits.LobbyUiInterface).Context.Data.Truthy("isReady"));
    }

    [MetaServerFact]
    public void On_MetaStation_the_players_who_sign_up_start_the_round_as_the_veteran_advisor_and_the_human_ai()
    {
        ServerConfig meta = MapJoinTests.MapConfig("metastation", MetaServer.Folder());
        var config = new ServerConfig
        {
            Seed = meta.Seed, Console = false, StatusEveryTicks = 100000, Map = meta.Map, MapsFolder = meta.MapsFolder, MoveSpeedScale = 1f,
            Lobby = true, StationTraits = true, DevCommands = true,
        };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 14921, TimeoutSteps = 8000 }, config, strict: false);
        RunUntil(w, () => In(w, () => GlobalProcs.SSticker.current_state) >= Defines.GAME_STATE_PREGAME, 10 * Second, "the pregame");
        In(w, () =>
        {
            GlobalProcs.SSstation.setup_trait(typeof(datum_station_trait_job_veteran_advisor));
            GlobalProcs.SSstation.setup_trait(typeof(datum_station_trait_job_human_ai));
            return true;
        });

        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        SignUp(w, alice, "Veteran Advisor");
        SignUp(w, bob, "Human AI");

        // the round starts: tg's PRE_JOBS_ASSIGNED gives each signed-up player the trait's job
        alice.Session.DevCommand("startnow");
        RunUntil(w, () => In(w, () => GlobalProcs.SSticker.current_state) == Defines.GAME_STATE_PLAYING
            && MobOf(w, alice) is mob_living_carbon_human && MobOf(w, bob) is mob_living_carbon_human, 15 * Second, "the round starts with both in their bodies");
        w.Run(Second);

        var veteran = Assert.IsType<mob_living_carbon_human>(MobOf(w, alice));
        Assert.Equal("Veteran Security Advisor", In(w, () => Dm.str(veteran.mind.assigned_role.title)));
        output.WriteLine("the veteran starts in " + In(w, () => GlobalProcs.get_area(veteran)?.GetType().Name));
        Assert.IsAssignableFrom<area_station_security>(In(w, () => GlobalProcs.get_area(veteran)));
        Assert.Equal(typeof(obj_item_clothing_under_rank_security_officer_formal), In(w, () => veteran.w_uniform?.GetType()));
        Assert.Contains(In(w, () => Dm.each<obj_item>(veteran.held_items).Select(i => i.GetType()).ToList()), t => t == typeof(obj_item_cane));
        Assert.True(In(w, () => Dm.truthy(veteran.has_trauma_type(typeof(datum_brain_trauma_special_ptsd)))));

        var human_ai = Assert.IsType<mob_living_carbon_human>(MobOf(w, bob));
        Assert.Equal(Defines.JOB_HUMAN_AI, In(w, () => Dm.str(human_ai.mind.assigned_role.title))); // "Big Brother"
        output.WriteLine("the human AI starts in " + In(w, () => GlobalProcs.get_area(human_ai)?.GetType().Name));
        Assert.IsAssignableFrom<area_station_ai>(In(w, () => GlobalProcs.get_area(human_ai)));
        List<Type> carried = In(w, () => Dm.each<atom>(human_ai.get_all_contents()).Select(a => a.GetType()).ToList());
        Assert.Contains(typeof(obj_item_card_id_advanced_robotic), carried); // worn, or in the wallet a station trait may give
        Assert.Contains(typeof(obj_item_door_remote_omni), carried);
        Assert.IsType<obj_item_organ_tongue_robot>(In(w, () => human_ai.get_organ_slot(Defines.ORGAN_SLOT_TONGUE)));
    }
}
