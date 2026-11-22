using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps SP1 (D338): the preferences menu's species page without the engine (tg's SpeciesPage.tsx): the Species button beside Rotate
/// opens it; the species' 64 px pictures (the human first), the chosen species' name, diet, description, perks, preview and lore; a
/// click picks a species (set_preference species); Go Back returns to the character page. The gender buttons hide for a species
/// without sexes, and the lizard's mutant colour is a colour input.
/// </summary>
public class PreferencesSpeciesWindowTests
{
    private static (TguiContext Context, Func<UiNode> Render, List<(string Action, string Params)> Acts) Make(string json)
    {
        var acts = new List<(string, string)>();
        var local = new TguiLocalState();
        var context = new TguiContext(1, "PreferencesMenu", "PreferencesMenu", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local);
        return (context, () => PreferencesMenu.Render(context), acts);
    }

    private static string Species(string name, string icon, int sexes, string perks, string diet, string features) =>
        "{\"name\":\"" + name + "\",\"desc\":\"About " + name + ".\",\"lore\":[\"First line of " + name + ".\",\"Second line.\"],\"icon\":\"" + icon + "\","
        + "\"use_skintones\":0,\"sexes\":" + sexes + ",\"enabled_features\":[" + features + "],\"perks\":" + perks + ",\"diet\":" + diet + "}";

    private const string LizardPerks = "{\"positive\":[{\"ui_icon\":\"fa-dragon\",\"name\":\"Lavaland Lungs\",\"description\":\"They breathe ash.\"}],"
        + "\"neutral\":[{\"ui_icon\":\"thermometer-half\",\"name\":\"Cold-blooded\",\"description\":\"No body heat of their own.\"}],\"negative\":[]}";

    private const string LizardDiet = "{\"liked_food\":[\"GORE\",\"MEAT\",\"BUGS\"],\"disliked_food\":[\"GRAIN\",\"GROSS\"],\"toxic_food\":[\"DAIRY\",\"TOXIC\"]}";

    private static string Json(string species, string gender = "male") =>
        "{\"window\":0,\"active_slot\":1,\"character_profiles\":[\"Bob\",null],\"name_to_use\":\"real_name\",\"character_preview_view\":\"preview_map\","
        + "\"character_preferences\":{\"misc\":{\"gender\":\"" + gender + "\",\"species\":\"" + species + "\"},\"names\":{\"real_name\":\"Bob\"},"
        + "\"clothing\":{},\"features\":{},\"secondary_features\":{\"feature_mcolor\":\"#22aa44\",\"feature_lizard_tail\":\"Smooth\"},"
        + "\"non_contextual\":{},\"randomization\":{},\"supplemental_features\":{}},"
        + "\"constant_data\":{\"species\":{"
        + "\"lizard\":" + Species("Lizardperson", "Lizardperson", 1, LizardPerks, LizardDiet, "\"feature_mcolor\",\"feature_lizard_tail\"") + ","
        + "\"human\":" + Species("Human", "Human", 1, "{\"positive\":[],\"neutral\":[],\"negative\":[]}", "null", "") + ","
        + "\"sexless\":" + Species("Sexless", "Sexless", 0, "{\"positive\":[],\"neutral\":[],\"negative\":[]}", "null", "")
        + "},\"feature_lizard_tail\":{\"choices\":[\"Smooth\",\"Dark Tiger\"]}},"
        + "\"species_icons\":{\"Lizardperson\":[\"iconops:lizard\",\"\"],\"Human\":[\"iconops:human\",\"\"],\"Sexless\":[\"iconops:sexless\",\"\"]}}";

    [Fact]
    public void The_species_button_opens_the_species_page_and_a_click_picks_a_species()
    {
        var (context, render, acts) = Make(Json("lizard"));
        UiNode tree = render();
        // the character page: Species beside Rotate, the lizard's mutant colour as a colour input and his tail's dropdown
        UiNode speciesButton = tree.Walk().Single(n => n.Kind == UiKind.Button && n.Tooltip == "Species");
        Assert.Equal("paw", speciesButton.Icon);
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.LabeledItem && n.Label == "Mutant color");
        Assert.Contains(tree.Walk(), n => n.Key == "color:feature_mcolor");
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.LabeledItem && n.Label == "Tail");
        Assert.Contains(tree.Walk(), n => n.Icon == "venus"); // the lizard has sexes: the gender buttons show

        speciesButton.OnClick();
        tree = render();
        // the page: Go Back, the species' 64 px buttons with the human first, the chosen one selected
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.Button && n.Text == "Go Back");
        List<UiNode> buttons = tree.Walk().Where(n => n.Kind == UiKind.Button && n.Key?.StartsWith("species:", StringComparison.Ordinal) == true).ToList();
        Assert.Equal("species:human", buttons[0].Key);
        Assert.Equal(new[] { "species:human", "species:lizard", "species:sexless" }, buttons.Select(b => b.Key).OrderBy(k => k));
        Assert.True(buttons.Single(b => b.Key == "species:lizard").Selected);
        Assert.Equal("iconops:lizard", buttons.Single(b => b.Key == "species:lizard").Children.Single().DmIcon);
        Assert.Equal(("64px", "64px"), (buttons[0].Width as string, buttons[0].Height as string));
        // the lizard's section: his name, description, perks (positive and neutral), diet and lore
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.Section && n.Title == "Lizardperson");
        Assert.Contains(tree.Walk(), n => n.Text == "About Lizardperson.");
        Assert.Contains(tree.Walk(), n => n.Key == "perk:Lavaland Lungs");
        Assert.Contains(tree.Walk(), n => n.Key == "perk:Cold-blooded");
        Assert.Contains(tree.Walk(), n => n.Key == "diet:liked_food");
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.Icon && n.Icon == "skull"); // gore, liked
        Assert.DoesNotContain(tree.Walk(), n => n.Kind == UiKind.Icon && n.Icon == "trash"); // gross food shows only when liked
        Assert.Contains(tree.Walk(), n => n.Text?.StartsWith("First line of Lizardperson.", StringComparison.Ordinal) == true);
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.ByondUi && n.MapRef == "preview_map");
        // the Character page button stays selected (PageButton's otherActivePages)
        Assert.True(tree.Walk().Single(n => n.Key == "page:Character").Children.Single().Selected);

        buttons.Single(b => b.Key == "species:human").OnClick();
        Assert.Equal("set_preference", acts.Last().Action);
        Assert.Contains("\"preference\":\"species\"", acts.Last().Params, StringComparison.Ordinal);
        Assert.Contains("\"value\":\"human\"", acts.Last().Params, StringComparison.Ordinal);

        tree.Walk().Single(n => n.Kind == UiKind.Button && n.Text == "Go Back").OnClick();
        tree = render();
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.Button && n.Tooltip == "Species");
    }

    [Fact]
    public void A_species_without_sexes_hides_the_gender_buttons()
    {
        var (_, render, _) = Make(Json("sexless"));
        UiNode tree = render();
        Assert.DoesNotContain(tree.Walk(), n => n.Icon == "venus" || n.Icon == "mars");
    }
}
