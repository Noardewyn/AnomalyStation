using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// The aquarium windows (tg's Aquarium.tsx and FishAnalyzer.tsx) without the engine: the aquarium's temperature knob and fluid buttons
/// locked under the assisted mode, its mode dropdown and feeding interval, each fish with its hearts or skull and its pet, rename and
/// take-out acts, each prop taken out by a click; the analyzer's fish with their food, traits, bars, evolutions and needs, and its search.
/// </summary>
public class AquariumWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(TguiInterface render, string name, string json, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, name, "aquarium", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private const string AquariumJson = "{\"fluidType\":\"Freshwater\",\"temperature\":297,\"lockedFluidTemp\":0,\"currentMode\":\"Manual\","
        + "\"currentTooltip\":\"Take full control.\",\"feedingInterval\":3,\"minTemperature\":273.15,\"maxTemperature\":373.15,"
        + "\"fluidTypes\":[\"Saltwater\",\"Freshwater\",\"Sulfuric Water\",\"Air\"],\"heartIcon\":\"icons/effects/effects.dmi\","
        + "\"aquariumModes\":[\"Manual\",\"Assisted\",\"Stasis\"],"
        + "\"fishData\":[{\"fish_ref\":\"[0x21]\",\"fish_name\":\"GOLDFISH\",\"fish_happiness\":2,\"fish_icon\":\"icons/obj/aquarium/fish.dmi\",\"fish_icon_state\":\"goldfish\",\"fish_alive\":1},"
        + "{\"fish_ref\":\"[0x22]\",\"fish_name\":\"GUPPY\",\"fish_happiness\":0,\"fish_icon\":\"icons/obj/aquarium/fish.dmi\",\"fish_icon_state\":\"guppy\",\"fish_alive\":0}],"
        + "\"propData\":[{\"prop_ref\":\"[0x23]\",\"prop_name\":\"aquarium sand\",\"prop_icon\":\"icons/obj/aquarium/tanks.dmi\",\"prop_icon_state\":\"sand\"}]}";

    [Fact]
    public void The_aquarium_window_shows_its_settings_fish_and_props_and_sends_tgs_acts()
    {
        var (context, tree, acts) = Render(Aquarium.Render, "Aquarium", AquariumJson);
        Assert.Equal((765.0, 500.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(297, ByKey(tree, "knob").Value);
        Assert.Equal((273.15, 373.15), (ByKey(tree, "knob").MinValue, ByKey(tree, "knob").MaxValue));
        Assert.True(ByKey(ByKey(tree, "fluid-Freshwater"), "pick").Selected);
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "fluid-lock");
        Assert.Equal(new[] { "Manual", "Assisted", "Stasis" }, ByKey(tree, "modes").Options);
        Assert.Equal("Take full control.", ByKey(tree, "mode-help").Tooltip);

        // the live goldfish has three full hearts of five, the dead guppy a skull
        UiNode goldfish = ByKey(tree, "fish-[0x21]");
        Assert.Equal("GOLDFISH", ByKey(goldfish, "name").Text);
        Assert.Equal(new[] { "full_heart", "full_heart", "full_heart", "empty_heart", "empty_heart" },
            ByKey(goldfish, "hearts").Children.Select(h => h.DmIconState));
        Assert.Equal("skull-crossbones", ByKey(ByKey(tree, "fish-[0x22]"), "dead").Icon);
        Assert.Equal("Aquarium sand", ByKey(ByKey(tree, "prop-[0x23]"), "name").Text);

        ByKey(tree, "knob").OnChange(300);
        ByKey(ByKey(tree, "fluid-Air"), "pick").OnClick();
        ByKey(tree, "modes").OnSelected("Stasis");
        ByKey(tree, "interval").OnChange(5);
        ByKey(goldfish, "pet").OnClick();
        ByKey(goldfish, "rename").OnCommit("Bubbles");
        ByKey(goldfish, "remove").OnClick();
        ByKey(ByKey(tree, "prop-[0x23]"), "take").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("temperature", "{\"temperature\":300}"),
            ("fluid", "{\"fluid\":\"Air\"}"),
            ("change_mode", "{\"new_mode\":\"Stasis\"}"),
            ("feeding_interval", "{\"feeding_interval\":5}"),
            ("pet_fish", "{\"fish_reference\":\"[0x21]\"}"),
            ("rename_fish", "{\"fish_reference\":\"[0x21]\",\"chosen_name\":\"Bubbles\"}"),
            ("remove_item", "{\"item_reference\":\"[0x21]\"}"),
            ("remove_item", "{\"item_reference\":\"[0x23]\"}"),
        }, acts);
    }

    [Fact]
    public void The_assisted_mode_locks_the_temperature_and_the_fluid()
    {
        var (_, tree, _) = Render(Aquarium.Render, "Aquarium", AquariumJson.Replace("\"lockedFluidTemp\":0", "\"lockedFluidTemp\":1"));
        Assert.Equal("LOCKED", ByKey(ByKey(tree, "temperature-lock"), "locked").Text);
        Assert.NotNull(ByKey(tree, "fluid-lock"));
        Assert.True(ByKey(tree, "knob").Disabled);
        Assert.True(ByKey(ByKey(tree, "fluid-Air"), "pick").Disabled);
    }

    private const string Fish = "{\"fish_name\":\"goldfish\",\"fish_icon\":\"icons/obj/aquarium/fish.dmi\",\"fish_icon_state\":\"goldfish\",\"fish_food\":\"Nutriment\","
        + "\"fish_food_color\":\"#664330\",\"fish_min_temp\":291.15,\"fish_max_temp\":299.15,\"fish_fluid_compatible\":1,\"fish_fluid_type\":\"Freshwater\","
        + "\"fish_traits\":[{\"trait_name\":\"Shiny Lover\",\"trait_desc\":\"Likes shiny lures.\",\"trait_inherit\":50}],"
        + "\"fish_evolutions\":[{\"evolution_name\":\"Three-eyed Goldfish\",\"evolution_icon\":\"icons/obj/aquarium/fish.dmi\",\"evolution_icon_state\":\"three_eyes\",\"evolution_probability\":3,\"evolution_conditions\":\"\"}],"
        + "\"fish_suitable_temp\":0,\"fish_health\":75,\"fish_size\":20,\"fish_weight\":400,\"fish_hunger\":0.5,\"fish_breed_timer\":0}";

    [Fact]
    public void The_fish_analyzer_window_reads_a_fish()
    {
        var (context, tree, _) = Render(FishAnalyzer.Render, "FishAnalyzer", "{\"fish_scanned\":1,\"fish_list\":[" + Fish + "]}");
        Assert.Equal((530.0, 270.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Fish Analyzer", context.WindowTitle);
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "search");
        UiNode fish = ByKey(tree, "entry-0");
        Assert.Equal("GOLDFISH", ByKey(fish, "name").Text);
        Assert.Equal(("Nutriment", "#664330"), (ByKey(fish, "food").Text, ByKey(fish, "food").Background));
        Assert.Equal(("400 kiloclam", "20 cm"), (ByKey(fish, "weight").Text, ByKey(fish, "size").Text));
        Assert.Equal("Shiny Lover", ByKey(fish, "trait").Text);
        Assert.Contains("Inheritance: 50%", ByKey(fish, "trait").Tooltip);
        Assert.Equal(0.75, ByKey(fish, "health").Value);
        Assert.Equal(0.5, ByKey(fish, "hunger").Value);
        Assert.Equal(("3%", "three_eyes"), (ByKey(fish, "probability").Text, ByKey(fish, "to-icon").DmIconState));
        Assert.Equal(("291.15k - 299.15k", "red"), (ByKey(fish, "temperature").Text, ByKey(fish, "temperature").Color));
        Assert.Equal(("Freshwater", "#1ac400"), (ByKey(fish, "fluid").Text, ByKey(fish, "fluid").Color));
    }

    [Fact]
    public void The_fish_analyzer_searches_an_aquariums_fish()
    {
        string json = "{\"fish_scanned\":0,\"fish_list\":[" + Fish + "," + Fish.Replace("\"goldfish\",\"fish_icon\"", "\"guppy\",\"fish_icon\"") + "]}";
        var local = new TguiLocalState();
        var (context, tree, _) = Render(FishAnalyzer.Render, "FishAnalyzer", json, local);
        Assert.Equal((700.0, 460.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(2, tree.Walk().Count(n => n.Key != null && n.Key.StartsWith("entry-", StringComparison.Ordinal)));
        ByKey(tree, "search").OnInput("gup");
        var (_, searched, _) = Render(FishAnalyzer.Render, "FishAnalyzer", json, local);
        Assert.Equal(new[] { "GUPPY" }, searched.Walk().Where(n => n.Key == "name").Select(n => n.Text));
    }

    [Fact]
    public void The_two_windows_are_registered_under_their_interface_names()
    {
        Assert.Equal((TguiInterface)Aquarium.Render, TguiInterfaces.Find("Aquarium"));
        Assert.Equal((TguiInterface)FishAnalyzer.Render, TguiInterfaces.Find("FishAnalyzer"));
    }
}
