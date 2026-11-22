using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// M7 slice T3 (D164): the gas analyzer (gas_analyzer.dm: the scan of a tile and of an object, atmos_scan's readout, the ranged scan,
/// the scan history, the window's data with the atmos handbooks, the barometer, the storm trait) and the handbook text of
/// reaction_factors.dm.
/// </summary>
public class GasAnalyzerTests
{
    private const int Second = 20;
    private const string Air = "o2=21;n2=79;TEMP=293.15";
    private const string Left = "icon-x=16;icon-y=16;left=1;button=left";

    private static AudioRoom Room(ulong seed, Action<World> extras = null) => new AudioRoom(13, 10, seed: seed, mapExtras: extras);

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item => r.In(() =>
    {
        foreach (object held in Dm.each(who.held_items.Copy()))
        {
            if (held is obj_item old)
                qdel(old);
        }
        T item = Dm.@new<T>(get_turf(who));
        Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(item, merge_stacks: FALSE))), "the item reaches a hand");
        return item;
    });

    private static void SetAir(AudioRoom r, int x, int y, string mix) => r.In(() => ((turf_open)locate(x, y, 1)).air = SSair.parse_gas_string(mix) as datum_gas_mixture);

    private static string Box(ChatCapture chat) => string.Join("\n", chat.Html);

    [Fact]
    public void TheAnalyzerPrintsTgsReadoutOfAKnownMixOnItsOwnTile()
    {
        var r = Room(8201);
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        var analyzer = Hold<obj_item_analyzer>(r, bob);
        SetAir(r, 4, 5, Air);
        r.Sounds.Heard.Clear();
        r.In(() => Waiting.NoSleep(analyzer.attack_self(bob)));

        string text = Box(chat);
        Assert.Contains("boxed_message", text);
        Assert.Contains("Results of analysis of", text);
        // 100 moles at 293.15 K in 2500 L: P = n R T / V = 100 * 8.31 * 293.15 / 2500 = 97.44 kPa
        Assert.Contains("Moles: 100 mol", text);
        Assert.Contains("Oxygen: 21 mol (21 %)", text);
        Assert.Contains("Nitrogen: 79 mol (79 %)", text);
        Assert.Contains("Temperature: 20 &deg;C (293.15 K)", text);
        Assert.Contains("Volume: 2500 L", text);
        Assert.Contains("Pressure: 97.44 kPa", text);
        Assert.Contains("Heat Capacity:", text);
        Assert.Contains("Thermal Energy:", text);
        // atmos_scan plays the industrial scan for the user (:222) and everyone sees the use (:223)
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.Contains("industrial_scan", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnEmptyTileReadsAsEmptyWithItsVolume()
    {
        var r = Room(8202);
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        var analyzer = Hold<obj_item_analyzer>(r, bob);
        r.In(() => ((turf_open)locate(4, 5, 1)).air = Dm.@new<datum_gas_mixture>());
        r.In(() => Waiting.NoSleep(analyzer.attack_self(bob)));
        string text = Box(chat);
        Assert.Contains(" is empty!", text);
        Assert.Contains("Volume: 2500 L", text);
        Assert.DoesNotContain("Moles:", text);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnObjectWithAirIsScannedThroughItsAnalyzerActAndTheWindowDataKeepsTheScanInTgsShape()
    {
        obj_machinery_portable_atmospherics_canister_air can = null;
        var r = Room(8203, w => can = Dm.@new<obj_machinery_portable_atmospherics_canister_air>(locate(5, 5, 1)));
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        var analyzer = Hold<obj_item_analyzer>(r, bob);
        r.Command(() => ClientSeam.Click(bob.client, can, Left));
        r.Step(Second);
        string text = Box(chat);
        Assert.Contains("Results of analysis of", text);
        Assert.Contains("Oxygen:", text);
        Assert.Contains("Nitrogen:", text);

        // ui_data: the last scan (:140-146), gas_mixture_parser's shape (atmospherics.dm:31-62)
        DmList data = r.In(() => (DmList)Waiting.NoSleep(analyzer.ui_data(bob)));
        r.In(() =>
        {
            DmList mixes = (DmList)data["gasmixes"];
            Assert.Equal(1f, mixes.len);
            DmList mix = (DmList)mixes[1];
            Assert.Equal(Dm.str(format_text(capitalize(LOWER_TEXT(can.name)))), Dm.str(mix["name"]));
            Assert.True(Dm.num(mix["total_moles"]) > 0);
            Assert.Equal(Dm.num(can.air_contents.return_pressure()), Dm.num(mix["pressure"]), 2);
            Assert.Equal(Dm.num(can.air_contents.temperature), Dm.num(mix["temperature"]), 2);
            Assert.Equal(REF(can.air_contents), mix["reference"]);
            DmList gases = (DmList)mix["gases"];
            Assert.Equal(2f, gases.len); // oxygen and nitrogen
            DmList first = (DmList)gases[1];
            Assert.Equal(3f, first.len);
            Assert.Equal("o2", Dm.str(first[1]));
            Assert.Equal("Oxygen", Dm.str(first[2]));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheStaticDataIsTgsAtmosHandbookWithEveryGasAndEveryReactionFactorText()
    {
        var r = Room(8204);
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        var analyzer = Hold<obj_item_analyzer>(r, bob);
        r.In(() =>
        {
            // SSair's Initialize makes the handbooks (air.dm:104); the toy room does not initialize SSair, a booted round does
            if (!Dm.truthy(length(GLOB.gas_handbook)))
                atmos_handbooks_init();
            DmList data = (DmList)Waiting.NoSleep(analyzer.ui_static_data(bob));
            Assert.Equal("cr", Dm.str(data["moneySymbol"]));
            Assert.Equal("credits", Dm.str(data["moneyName"]));
            DmList gases = (DmList)data["gasInfo"];
            DmList reactions = (DmList)data["reactionInfo"];
            Assert.Equal((float)GAS_TYPE_COUNT, gases.len);
            Assert.Equal(Dm.each(valid_subtypesof(typeof(datum_gas_reaction))).Count(), (int)reactions.len);

            DmList oxygen = Dm.each(gases).OfType<DmList>().First(g => Dm.str(g["id"]) == "o2");
            Assert.Equal("Oxygen", Dm.str(oxygen["name"]));
            Assert.Equal(20f, Dm.num(oxygen["specific_heat"]));
            Assert.True(Dm.truthy(oxygen["description"]));
            Assert.Contains("plasmafire", Dm.each(((DmList)oxygen["reactions"])).Select(Dm.str)); // the reactions a gas is a factor of, id to name

            DmList fire = Dm.each(reactions).OfType<DmList>().First(x => Dm.str(x["id"]) == "plasmafire");
            Assert.True(Dm.truthy(fire["name"]));
            DmList factors = (DmList)fire["factors"];
            Assert.Equal(7f, factors.len); // oxygen, plasma, tritium, water vapor, carbon dioxide, temperature, energy
            DmList oxygenFactor = (DmList)factors[1];
            Assert.Equal("gas", Dm.str(oxygenFactor["factor_type"]));
            Assert.Equal("o2", Dm.str(oxygenFactor["factor_id"]));
            Assert.Equal("Oxygen consumption is determined by the temperature, ranging from 1.4 moles per mole of Plasma consumed at 373.15K to 0.4 moles per mole of Plasma consumed at 1643.15K. Higher Oxygen concentration up to 10 times the Plasma increases the speed of Plasma consumption.", Dm.str(oxygenFactor["desc"]));
            DmList temperature = (DmList)factors[6];
            Assert.Equal("misc", Dm.str(temperature["factor_type"]));
            Assert.Equal("Temperature", Dm.str(temperature["factor_name"]));
            Assert.Equal("Reaction is influenced by the temperature of the place where the reaction is occurring.", Dm.str(temperature["tooltip"]));
            // every reaction has factors, as tg's init_factors give them
            Assert.All(Dm.each(reactions).OfType<DmList>(), x => Assert.True(((DmList)x["factors"]).len > 0, Dm.str(x["id"])));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheNormalAnalyzerReachesOneTileAndTheRangedOneFifteenButNotThroughAWall()
    {
        obj_machinery_portable_atmospherics_canister_air near = null, far = null, behind = null;
        var r = Room(8205, w =>
        {
            near = Dm.@new<obj_machinery_portable_atmospherics_canister_air>(locate(5, 5, 1));
            far = Dm.@new<obj_machinery_portable_atmospherics_canister_air>(locate(10, 5, 1));
            Dm.@new<turf_closed_wall>(locate(8, 7, 1));
            behind = Dm.@new<obj_machinery_portable_atmospherics_canister_air>(locate(10, 7, 1));
        });
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;

        var plain = Hold<obj_item_analyzer>(r, bob);
        r.In(() => Waiting.NoSleep(plain.ranged_interact_with_atom(far, bob, Dm.list())));
        Assert.Empty(chat.Html); // 6 tiles, range 1 (ranged_scan_distance)
        r.In(() => Waiting.NoSleep(plain.interact_with_atom(near, bob, Dm.list())));
        Assert.Contains("Results of analysis of", Box(chat));

        chat.Html.Clear();
        var ranged = Hold<obj_item_analyzer_ranged>(r, bob);
        r.In(() => Waiting.NoSleep(ranged.ranged_interact_with_atom(far, bob, Dm.list())));
        Assert.Contains("Results of analysis of", Box(chat));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBarometerNeedsTheOutdoorsTakesTgsCooldownAndPingsWhenReady()
    {
        var r = Room(8206);
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        var analyzer = Hold<obj_item_analyzer>(r, bob);

        r.Sounds.Heard.Clear();
        r.In(() => Waiting.NoSleep(analyzer.click_alt(bob)));
        Assert.Contains("barometer function won't work indoors!", Box(chat)); // the testroom is indoors
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/effects/pop.ogg");

        r.In(() => ((area)get_turf(bob).loc).outdoors = TRUE);
        chat.Html.Clear();
        r.In(() => Assert.Equal(CLICK_ACTION_SUCCESS, Dm.num(Waiting.NoSleep(analyzer.click_alt(bob)))));
        Assert.Contains("barometer function was unable to trace any weather patterns.", Box(chat)); // no weather in the core version
        r.In(() => Assert.True(Dm.truthy(analyzer.cooldown)));

        chat.Html.Clear();
        r.In(() => Assert.Equal(CLICK_ACTION_BLOCKING, Dm.num(Waiting.NoSleep(analyzer.click_alt(bob)))));
        Assert.Contains("barometer function is preparing itself.", Box(chat));

        // 25 seconds later (cooldown_time = 25 SECONDS): "ready" and the click sound
        r.Sounds.Heard.Clear();
        r.Step(25 * Second + 2);
        Assert.Contains("barometer function is ready!", Box(chat));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/machines/click.ogg");
        r.In(() => Assert.False(Dm.truthy(analyzer.cooldown)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void HoldingTheAnalyzerGivesTheStormTraitAndDroppingItTakesItAway()
    {
        var r = Room(8207);
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        r.In(() => Assert.False(HAS_TRAIT(bob, TRAIT_DETECT_STORM)));
        var analyzer = Hold<obj_item_analyzer>(r, bob);
        r.In(() => Assert.True(HAS_TRAIT(bob, TRAIT_DETECT_STORM)));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(analyzer)));
        r.In(() => Assert.False(HAS_TRAIT(bob, TRAIT_DETECT_STORM)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ARightClickOpensTheGasAnalyzerWindowForAReader()
    {
        var r = Room(8208);
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        var analyzer = Hold<obj_item_analyzer>(r, bob);
        r.In(() => { if (!Dm.truthy(length(GLOB.gas_handbook))) atmos_handbooks_init(); }); // the toy room does not initialize SSair
        bob.client.dm_tgui_output.Clear();
        r.Command(() => Waiting.NoSleep(analyzer.attack_self_secondary(bob)));
        r.Step(2);
        TguiOutput open = bob.client.dm_tgui_output.First(o => o.Kind == TguiOutputKind.Message);
        using JsonDocument doc = JsonDocument.Parse(open.Json);
        JsonElement payload = doc.RootElement.GetProperty("payload");
        Assert.Equal("GasAnalyzer", payload.GetProperty("config").GetProperty("interface").GetProperty("name").GetString());
        Assert.Equal(GAS_TYPE_COUNT, payload.GetProperty("static_data").GetProperty("gasInfo").GetArrayLength());
        r.AssertNoRuntimes();
    }
}
