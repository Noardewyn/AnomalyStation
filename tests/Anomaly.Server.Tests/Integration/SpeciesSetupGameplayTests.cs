using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// D349: a player picks a species in the lobby's Character Setup the way the client does it (tg's PreferencesMenu through the real
/// server and the wire): the species page lists the server's roundstart species, a click picks one, the species' own features show
/// on the character page and take a value, and the round starts the character as that species with those features.
/// </summary>
public class SpeciesSetupGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static TguiWindow Window(TguiWindows windows, ReplicaWorld replica, string @interface)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Open.FirstOrDefault(x => x.Interface == @interface);
    }

    private static RealHeadlessWorld Lobby(ulong seed, bool allSpecies = false)
    {
        var config = new ServerConfig { Seed = seed, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f, Lobby = true, AllSpeciesRoundstart = allSpecies };
        var w = new RealHeadlessWorld(new LoopbackOptions { Seed = seed, TimeoutSteps = 8000 }, config, strict: false);
        for (int i = 0; i < 10 * Second && In(w, () => GlobalProcs.SSticker.current_state) < Defines.GAME_STATE_PREGAME; i++)
            w.Run(1);
        Assert.Equal(Defines.GAME_STATE_PREGAME, In(w, () => GlobalProcs.SSticker.current_state));
        return w;
    }

    [Fact]
    public void A_player_picks_the_lizard_in_character_setup_sets_his_tail_and_starts_the_round_as_a_lizard()
    {
        using RealHeadlessWorld w = Lobby(4711);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        ReplicaWorld replica = alice.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters),
            SendClose = window => alice.Session.UiClose(window),
        };
        w.Run(Second / 2);
        Window(windows, replica, Limits.LobbyUiInterface).Context.Act("character_setup");
        w.Run(Second / 2);
        TguiWindow prefs = Window(windows, replica, "PreferencesMenu");
        Assert.Equal(0, w.Host.SessionOf(1).UiWindowsDropped); // the window fits the wire's caps (Limits.MaxUiWindowBytes, MaxUiValues)
        Assert.NotNull(prefs);

        // the species the server offers reach the client
        UiValue species = prefs.Context.Data.Get("constant_data")?.Get("species");
        output.WriteLine("species on the client: " + string.Join(", ", species?.Fields.Select(f => f.Key) ?? []));
        Assert.NotNull(species);
        Assert.Subset(species.Fields.Select(f => f.Key).ToHashSet(), new HashSet<string> { "felinid", "human", "lizard" }); // and every other live roundstart species (D349)

        // the client's species page: one button per species; a click picks the lizard
        prefs.Content.Walk().Single(n => n.Kind == UiKind.Button && n.Tooltip == "Species").OnClick();
        prefs = Window(windows, replica, "PreferencesMenu");
        List<UiNode> buttons = prefs.Content.Walk().Where(n => n.Kind == UiKind.Button && n.Key?.StartsWith("species:", StringComparison.Ordinal) == true).ToList();
        Assert.Equal(species.Fields.Count(), buttons.Count);
        buttons.Single(b => b.Key == "species:lizard").OnClick();
        w.Run(Second / 2);
        prefs = Window(windows, replica, "PreferencesMenu");
        Assert.Equal("lizard", prefs.Context.Data.Get("character_preferences")?.Get("misc")?.Str("species"));

        // back on the character page the lizard's own features show; his tail takes a value
        prefs.Content.Walk().Single(n => n.Kind == UiKind.Button && n.Text == "Go Back").OnClick();
        prefs = Window(windows, replica, "PreferencesMenu");
        Assert.Contains(prefs.Content.Walk(), n => n.Kind == UiKind.LabeledItem && n.Label == "Tail");
        prefs.Context.Act("set_preference", ("preference", "feature_lizard_tail"), ("value", "Dark Tiger"));
        prefs.Context.Act("set_preference", ("preference", "feature_mcolor"), ("value", "#22aa44"));
        w.Run(Second / 2);

        Window(windows, replica, Limits.LobbyUiInterface).Context.Act("ready_toggle");
        w.Run(Second / 2);
        alice.Session.DevCommand("startnow");
        // the round's setup takes a few seconds of world time (the ruins, the fish's caches): wait for it, as the lobby chat test does
        for (int i = 0; i < 20 * Second && In(w, () => GlobalProcs.SSticker.current_state) != Defines.GAME_STATE_PLAYING; i++)
            w.Run(1);
        w.Run(Second);
        Assert.Equal(Defines.GAME_STATE_PLAYING, In(w, () => GlobalProcs.SSticker.current_state));
        var lizard = Assert.IsType<mob_living_carbon_human>(w.MobOf(alice));
        Assert.IsType<datum_species_lizard>(In(w, () => lizard.dna.species));
        Assert.Equal("Dark Tiger", In(w, () => Dm.str(lizard.dna.features[Defines.FEATURE_TAIL_LIZARD])));
        Assert.Equal("#22aa44", In(w, () => Dm.str(lizard.dna.features[Defines.FEATURE_MUTANT_COLOR])));
        Assert.NotNull(In(w, () => lizard.get_organ_slot(Defines.ORGAN_SLOT_EXTERNAL_TAIL)));
    }

    [Fact]
    public void Every_offered_species_sends_each_of_its_features_to_character_setup()
    {
        using RealHeadlessWorld w = Lobby(4713, allSpecies: true);
        var runtimes = new List<string>();
        w.World.RuntimeErrorSink = e => runtimes.Add(e.ToString());
        RealHeadlessWorld.Player alice = w.Join("Alice");
        ReplicaWorld replica = alice.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters),
            SendClose = window => alice.Session.UiClose(window),
        };
        w.Run(Second / 2);
        Window(windows, replica, Limits.LobbyUiInterface).Context.Act("character_setup");
        w.Run(Second / 2);
        TguiWindow prefs = Window(windows, replica, "PreferencesMenu");
        Assert.True(w.Host.SessionOf(1).UiWindowsDropped == 0, w.Err.ToString()); // the window fits the wire's caps (Limits.MaxUiWindowBytes, MaxUiValues)
        Assert.True(runtimes.Count == 0, string.Join("\n\n", runtimes.Take(3)));
        Assert.NotNull(prefs);
        Assert.NotNull(prefs.Context.Data.Get("constant_data"));
        Assert.NotNull(prefs.Context.Data.Get("constant_data").Get("species"));
        var missing = new List<string>();
        foreach ((string id, UiValue entry) in prefs.Context.Data.Get("constant_data").Get("species").Fields.ToList())
        {
            prefs.Context.Act("set_preference", ("preference", "species"), ("value", id));
            w.Run(Second / 2);
            prefs = Window(windows, replica, "PreferencesMenu");
            Assert.Equal(id, prefs.Context.Data.Get("character_preferences").Get("misc").Str("species"));
            // every feature the species enables is a preference the page holds a value of (tg's PreferencesMenu reads them there)
            var held = prefs.Context.Data.Get("character_preferences").Fields.SelectMany(section => section.Value.Fields.Select(f => f.Key)).ToHashSet();
            foreach (UiValue feature in entry.Get("enabled_features")?.Items ?? [])
                if (!held.Contains(feature.AsStr()))
                    missing.Add(id + ": " + feature.AsStr());
        }
        output.WriteLine(string.Join("\n", missing));
        Assert.Empty(missing);
    }

    [Fact]
    public void The_all_species_option_offers_every_live_species()
    {
        using RealHeadlessWorld w = Lobby(4712, allSpecies: true);
        List<string> ids = In(w, () => Dm.each(GlobalProcs.get_selectable_species()).Select(Dm.str).ToList());
        output.WriteLine("selectable: " + string.Join(", ", ids));
        Assert.Contains("lizard", ids);
        Assert.Contains("monkey", ids);
        Assert.True(ids.Count > 3);
    }
}
