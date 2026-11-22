using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps S4d (D327): the client's NtosVirtualPet window without the engine: the stats with the pet's picture drawn from its parts (DV405),
/// the customization's apply, the shared feed's likes and the trick sequence.
/// </summary>
public class NtosVirtualPetWindowTests
{
    private static (TguiContext Context, List<(string Action, string Params)> Acts) Context(string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, "NtosVirtualPet", "NtosVirtualPet", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, acts);
    }

    private const string Header = "\"PC_device_theme\":\"ntos\",\"PC_batteryicon\":null,\"PC_batterypercent\":null,\"PC_ntneticon\":\"sig_high.gif\","
        + "\"PC_stationtime\":\"00:12:30\",\"PC_stationdate\":\"Fri, October 23, 2566\",\"PC_programheaders\":[],\"PC_lowpower_mode\":0,\"PC_showexitprogram\":1";

    private const string Picture = "{\"icon\":\"icons/mob/simple/pets.dmi\",\"icon_state\":\"orbie\",\"color\":\"#c3ecf3\","
        + "\"hat_icon\":\"icons/mob/clothing/head/hats.dmi\",\"hat_icon_state\":\"tophat\",\"hat_pixel_w\":0,\"hat_pixel_z\":9}";

    private const string Data = "{" + Header + ",\"currently_summoned\":0,\"selected_area\":\"No location set\",\"in_dropzone\":0,\"can_reroll\":1,"
        + "\"pet_state\":\"asleep\",\"level\":2,\"happiness\":40,\"maximum_happiness\":1800,\"current_exp\":30,\"required_exp\":250,"
        + "\"hunger\":5,\"maximum_hunger\":20,\"pet_area\":\"Bar\",\"can_summon\":1,\"steps_counter\":120,"
        + "\"pet_state_icons\":[{\"name\":\"asleep\",\"icon\":" + Picture + "},{\"name\":\"dead\",\"icon\":null}],"
        + "\"possible_emotes\":[\"none\",\"flip\",\"spin\"],"
        + "\"hat_selections\":[{\"hat_id\":null,\"hat_name\":\"none\"},{\"hat_id\":\"/obj/item/clothing/head/hats/tophat\",\"hat_name\":\"top-hat\"}],"
        + "\"possible_colors\":[{\"color_name\":\"white\",\"color_value\":null},{\"color_name\":\"light blue\",\"color_value\":\"#c3ecf3\"}],"
        + "\"pet_hat\":\"none\",\"pet_gender\":\"male\",\"pet_name\":\"Orbie\",\"pet_color\":\"\",\"can_alter_appearance\":1,\"preview_icon\":" + Picture + ","
        + "\"pet_updates\":[{\"update_id\":3,\"update_name\":\"Sparkles\",\"update_picture\":" + Picture + ",\"update_message\":\"has reached level 2!\","
        + "\"update_already_liked\":0,\"update_likers\":1}]}";

    [Fact]
    public void TheStatsDrawThePictureFromItsParts()
    {
        var (c, acts) = Context(Data);
        UiNode tree = NtosVirtualPet.Render(c);
        UiNode icon = tree.Walk().Single(n => n.Key == "state_icon");
        Assert.Equal(UiKind.Overlay, icon.Kind);
        UiNode @base = icon.Walk().Single(n => n.Key == "base");
        Assert.Equal(("icons/mob/simple/pets.dmi", "orbie", "#c3ecf3"), (@base.DmIcon, @base.DmIconState, @base.Color));
        Assert.Equal("tophat", icon.Walk().Single(n => n.Key == "hat").DmIconState);
        Assert.Contains(tree.Walk(), n => n.Text == "Asleep");
        Assert.Equal("Current Level: 2", tree.Walk().Single(n => n.Key == "level").Text);
        Assert.Equal("Release", tree.Walk().Single(n => n.Key == "summon").Text);
        UiNode reroll = tree.Walk().Single(n => n.Key == "reroll");
        Assert.Equal("Generate", reroll.Text); // no location yet
        Assert.Equal(" Steps: 120", tree.Walk().Single(n => n.Key == "steps").Text);
        tree.Walk().Single(n => n.Key == "summon").OnClick();
        reroll.OnClick();
        Assert.Equal(new[] { ("summon_pet", (string)null), ("get_feed_location", (string)null) }, acts);
    }

    [Fact]
    public void TheCustomizationAppliesTheChosenHatColourNameAndGender()
    {
        var (c, acts) = Context(Data);
        NtosVirtualPet.Render(c).Walk().Single(n => n.Key == "tab:1").OnClick();
        UiNode tree = NtosVirtualPet.Render(c);
        Assert.Equal(UiKind.Overlay, tree.Walk().Single(n => n.Key == "preview_icon").Kind);
        tree.Walk().Single(n => n.Key == "pick-hat").OnSelected("top-hat");
        tree.Walk().Single(n => n.Key == "pick-color").OnSelected("light blue");
        tree.Walk().Single(n => n.Key == "name").OnInput("Sparky");
        tree.Walk().Single(n => n.Key == "gender:female").OnClick();
        tree = NtosVirtualPet.Render(c);
        Assert.True(tree.Walk().Single(n => n.Key == "gender:female").Selected);
        Assert.Equal("light blue", tree.Walk().Single(n => n.Key == "pick-color").TextValue);
        tree.Walk().Single(n => n.Key == "apply" && n.Kind == UiKind.Button).OnClick();
        Assert.Equal(("apply_customization", "{\"chosen_hat\":\"/obj/item/clothing/head/hats/tophat\",\"chosen_name\":\"Sparky\",\"chosen_color\":\"#c3ecf3\",\"chosen_gender\":\"female\"}"), acts.Single());
    }

    [Fact]
    public void TheFeedLikesAnUpdateAndTheTricksAreTaught()
    {
        var (c, acts) = Context(Data);
        NtosVirtualPet.Render(c).Walk().Single(n => n.Key == "tab:2").OnClick();
        UiNode feed = NtosVirtualPet.Render(c);
        UiNode update = feed.Walk().Single(n => n.Key == "update:3");
        Assert.Contains(update.Walk(), n => n.Text == "Sparkl has reached level 2!"); // tg cuts the name to six letters
        Assert.Equal("1", update.Walk().Single(n => n.Key == "like").Text);
        update.Walk().Single(n => n.Key == "like").OnClick();
        Assert.Equal(("like_update", "{\"update_reference\":3}"), acts.Last());

        feed.Walk().Single(n => n.Key == "tab:3").OnClick();
        UiNode tricks = NtosVirtualPet.Render(c);
        tricks.Walk().Single(n => n.Key == "rename").OnCommit("dance");
        tricks.Walk().Single(n => n.Key == "trick:0").OnSelected("flip");
        tricks.Walk().Single(n => n.Key == "trick:2").OnSelected("spin");
        tricks = NtosVirtualPet.Render(c);
        Assert.Equal("Dance", tricks.Walk().Single(n => n.Key == "tricks").Title);
        tricks.Walk().Single(n => n.Key == "teach").OnClick();
        Assert.Equal(("teach_tricks", "{\"trick_name\":\"dance\",\"tricks\":[\"flip\",\"none\",\"spin\",\"none\"]}"), acts.Last());
    }
}
