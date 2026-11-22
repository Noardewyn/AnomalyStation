using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps SL1 (D333): the client's SkillStation window (tg's SkillStation.jsx) shows the inserted chip with its implant and eject
/// buttons, the operation's time left and the implanted chips with their extract and toggle buttons; the SkillPanel window
/// (SkillPanel.jsx) lists each skill with its level, experience and the three admin buttons; NtosSkillTracker (NtosSkillTracker.jsx)
/// shows each skill's level and progress bars and the legendary reward button. All three are registered under their names.
/// </summary>
public class SkillWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string iface, Func<TguiContext, UiNode> render, string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, iface, "w", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    [Fact]
    public void TheSkillStationShowsTheInsertedAndImplantedChips()
    {
        Assert.NotNull(TguiInterfaces.Find("SkillStation"));
        var (context, empty, _) = Render("SkillStation", SkillStation.Render,
            "{\"working\":0,\"timeleft\":0,\"skillchip_ready\":0,\"complexity_used\":0,\"complexity_max\":3,\"slots_used\":0,\"slots_max\":5,\"current\":[]}");
        Assert.Equal((500.0, 500.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Please insert a skillchip.", ByKey(empty, "insert").Text);
        Assert.Equal("No skillchips detected.", ByKey(empty, "none").Text);
        Assert.DoesNotContain(empty.Walk(), n => n.Key == "working");

        var (_, tree, acts) = Render("SkillStation", SkillStation.Render,
            "{\"working\":0,\"timeleft\":0,\"skillchip_ready\":1,\"skill_name\":\"Memory of a Musical\",\"skill_desc\":\"Sing.\",\"skill_icon\":\"music\","
            + "\"complexity\":1,\"slot_use\":1,\"implantable\":1,\"implantable_reason\":null,\"complexity_used\":1,\"complexity_max\":3,\"slots_used\":1,\"slots_max\":5,"
            + "\"current\":[{\"name\":\"Kommand\",\"icon\":\"arrow-down\",\"complexity\":1,\"slot_use\":1,\"cooldown\":0,\"active\":1,\"removable\":1,\"active_error\":\"\",\"ref\":\"[0x1]\"}]}");
        Assert.Contains("Memory of a Musical", tree.AllText());
        Assert.False(ByKey(tree, "implant").Disabled);
        Assert.Equal("good", ByKey(tree, "implant").Color);
        Assert.Equal("Extract", ByKey(tree, "remove").Tooltip);
        Assert.Equal("Deactivate", ByKey(tree, "toggle").Tooltip);
        Assert.Equal("1/3", ByKey(tree, "complexity_button").Text);
        ByKey(tree, "implant").OnClick();
        ByKey(tree, "eject").OnClick();
        ByKey(tree, "remove").OnClick();
        ByKey(tree, "toggle").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("implant", null), ("eject", null), ("remove", "{\"ref\":\"[0x1]\"}"), ("toggle_activate", "{\"ref\":\"[0x1]\"}"),
        }, acts);

        // working: the warning with the time left, the buttons disabled
        var (_, working, _) = Render("SkillStation", SkillStation.Render,
            "{\"working\":1,\"timeleft\":125,\"skillchip_ready\":1,\"skill_name\":\"X\",\"implantable\":1,\"current\":[]}");
        Assert.Equal("Time Left: 00:00:12", ByKey(working, "timeleft").Text);
        Assert.True(ByKey(working, "implant").Disabled);
        Assert.True(ByKey(working, "eject").Disabled);
    }

    [Fact]
    public void TheSkillPanelListsTheSkillsWithTheAdminButtons()
    {
        Assert.NotNull(TguiInterfaces.Find("SkillPanel"));
        var (context, tree, acts) = Render("SkillPanel", SkillPanel.Render,
            "{\"skills\":[{\"playername\":\"Bob\",\"path\":\"/datum/skill/mining\",\"name\":\"Mining\",\"desc\":\"A dwarf's biggest skill, after drinking.\",\"lvlnum\":2,"
            + "\"lvl\":\"NOVICE\",\"exp\":175,\"exp_prog\":75,\"exp_req\":150,\"exp_percent\":0.07,\"max_exp\":2500},"
            + "{\"playername\":\"Bob\",\"path\":\"/datum/skill/cleaning\",\"name\":\"Cleaning\",\"desc\":\"x\",\"lvlnum\":7,\"lvl\":\"LEGENDARY\",\"exp\":2500,\"exp_prog\":0,\"exp_req\":0,\"exp_percent\":1,\"max_exp\":2500}]}");
        Assert.Equal((600.0, 500.0), (context.WindowWidth, context.WindowHeight));
        Assert.Contains("Total Experience: [175 XP]", tree.AllText());
        Assert.Contains("XP To Next Level:[75 / 150]", tree.AllText());
        Assert.Contains("[MAXXED]", tree.AllText());
        Assert.Contains("Overall Skill Progress: [175 / 2500]", tree.AllText());
        UiNode mining = ByKey(tree, "Mining");
        mining.Walk().First(n => n.Key == "adj_exp").OnClick();
        mining.Walk().First(n => n.Key == "set_exp").OnClick();
        mining.Walk().First(n => n.Key == "set_lvl").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("adj_exp", "{\"skill\":\"/datum/skill/mining\"}"), ("set_exp", "{\"skill\":\"/datum/skill/mining\"}"), ("set_lvl", "{\"skill\":\"/datum/skill/mining\"}"),
        }, acts);
        Assert.Equal("#6abf40", SkillPanel.LevelColor(2)); // tg: hsl(2 * 50, 50%, 50%)
    }

    [Fact]
    public void TheSkillTrackerShowsLevelsProgressAndTheReward()
    {
        Assert.NotNull(TguiInterfaces.Find("NtosSkillTracker"));
        var (context, tree, acts) = Render("NtosSkillTracker", NtosSkillTracker.Render,
            "{\"PC_device_theme\":\"ntos\",\"skills\":[{\"name\":\"Mining\",\"desc\":\"d\",\"title\":\"Miner\",\"lvl_name\":\"NOVICE\",\"progress_percent\":0.5,\"overall_percent\":0.07},"
            + "{\"name\":\"Cleaning\",\"desc\":\"c\",\"title\":\"Custodian\",\"lvl_name\":\"LEGENDARY\",\"reward\":1}]}");
        Assert.Equal((500.0, 600.0), (context.WindowWidth, context.WindowHeight));
        Assert.Contains("Skill Tracker", tree.AllText());
        UiNode mining = ByKey(tree, "skill_0");
        Assert.Equal("Mining", mining.Title);
        Assert.Equal(0.5, mining.Walk().First(n => n.Key == "progress_bar").Value);
        Assert.Equal("50%", mining.Walk().First(n => n.Key == "progress_bar").Text);
        Assert.DoesNotContain(mining.Walk(), n => n.Key == "reward_button");
        UiNode cleaning = ByKey(tree, "skill_1");
        Assert.Equal("—", cleaning.Walk().First(n => n.Key == "progress").Text);
        UiNode reward = cleaning.Walk().First(n => n.Key == "reward_button");
        Assert.Equal("Contact the Professional Custodian Association", reward.Text);
        reward.OnClick();
        Assert.Equal(new (string, string)[] { ("PRG_reward", "{\"skill\":\"Cleaning\"}") }, acts);
    }
}
