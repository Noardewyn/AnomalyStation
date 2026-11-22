using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>M10x slice C: tg's orbit menu window (tgui's Orbit/*.tsx): its sections, search, sorts, colours, names and acts.</summary>
public class OrbitWindowTests
{
    private const string Json = """
        {
          "alive": [
            {"ref":"[mob_2]","full_name":"Zed Doe (as Captain)","name":"Zed Doe","client":1,"health":100,"job":"Captain","icon":"icons/mob/huds/hud.dmi","icon_state":"hudcaptain"},
            {"ref":"[mob_3]","full_name":"Amy Lee","name":"Amy Lee","client":0,"health":50,"job":"Station Engineer","icon":"icons/mob/huds/hud.dmi","icon_state":"hudstationengineer","orbiters":2}
          ],
          "antagonists": [
            {"ref":"[mob_9]","full_name":"Bad Guy","name":"Bad Guy","client":1,"health":80,"antag":"Traitor","antag_group":"Traitors","icon":"icons/mob/huds/antag_hud.dmi","icon_state":"traitor"}
          ],
          "critical": [ {"ref":"[obj_4]","full_name":"supermatter crystal","extra":"Integrity: 5%"} ],
          "deadchat_controlled": [],
          "dead": [],
          "ghosts": [ {"ref":"[mob_7]","full_name":"Alice"} ],
          "misc": [ {"ref":"[obj_4]","full_name":"supermatter crystal","extra":"Integrity: 5%"} ],
          "npcs": [],
          "orbiting": {"ref":"[mob_3]","full_name":"Amy Lee","name":"Amy Lee","client":0,"health":50,"job":"Station Engineer"},
          "can_observe": 1
        }
        """;

    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, "Orbit", "Orbit", 2, UiValue.FromJson(Json), (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (context, Orbit.Render(context), acts);
    }

    [Fact]
    public void The_window_lists_tgs_sections_with_their_counts_and_the_critical_notice()
    {
        (TguiContext c, UiNode tree, _) = Render();
        Assert.Equal(400, c.WindowWidth);
        Assert.Equal(550, c.WindowHeight);
        string text = tree.AllText();
        Assert.Contains("Alive - (2)", text);
        Assert.Contains("Traitors - (1)", text);
        Assert.Contains("Ghosts - (1)", text);
        Assert.Contains("Misc - (1)", text);
        Assert.DoesNotContain("Dead - (", text); // an empty section is not drawn
        Assert.Contains("Supermatter Crystal", text); // the critical notice, title cased
        // the alive sorted by the shown name: Amy Lee, then Zed Doe (his own ID: "(as ...)" shows the name)
        List<UiNode> buttons = tree.Walk().Where(n => n.Kind == UiKind.Button && n.OnClick != null && n.Text != null).ToList();
        int amy = buttons.FindIndex(b => b.AllText().Contains("Amy Lee")), zed = buttons.FindIndex(b => b.AllText().Contains("Zed Doe"));
        Assert.True(amy >= 0 && zed > amy);
        Assert.True(buttons[amy].Selected, "the orbited item is marked");
        Assert.Equal("grey", buttons[amy].Color); // away from keyboard
        Assert.Equal("good", buttons[zed].Color); // health 100 in the health view
    }

    [Fact]
    public void Clicking_an_item_orbits_it_with_the_auto_observe_toggle()
    {
        var local = new TguiLocalState();
        (_, UiNode tree, List<(string Action, string Params)> acts) = Render(local);
        tree.Walk().First(n => n.Kind == UiKind.Button && n.AllText().Contains("Amy Lee")).OnClick();
        Assert.Equal(("orbit", "{\"auto_observe\":false,\"ref\":\"[mob_3]\"}"), acts.Single());
        tree.Walk().First(n => n.Kind == UiKind.Button && n.Icon == "toggle-off").OnClick();
        Assert.True(local.Get("autoObserve", false));
        (_, tree, acts) = Render(local);
        tree.Walk().First(n => n.Kind == UiKind.Button && n.AllText().Contains("Amy Lee")).OnClick();
        Assert.Equal(("orbit", "{\"auto_observe\":true,\"ref\":\"[mob_3]\"}"), acts.Single());
    }

    [Fact]
    public void The_search_filters_and_enter_orbits_the_least_orbited_match()
    {
        UiValue data = UiValue.FromJson(Json);
        Assert.Equal("[mob_2]", Orbit.MostRelevant(data, "")!.Str("ref")); // the first of the fewest orbiters
        Assert.Equal("[mob_3]", Orbit.MostRelevant(data, "engineer")!.Str("ref")); // by job
        Assert.Null(Orbit.MostRelevant(data, "nobody"));
        Assert.Single(Orbit.FilterAndSort(data.Arr("alive"), "amy", Orbit.ViewHealth));
    }

    [Fact]
    public void Tgs_helpers_name_colour_and_sort_as_the_tsx()
    {
        Assert.Equal("\"Zed Doe\"", Orbit.GetDisplayName("Zed Doe [Captain]", "Zed Doe"));
        Assert.Equal("Zed Doe", Orbit.GetDisplayName("Zed Doe (as Captain)", "Zed Doe"));
        Assert.Equal("Bob", Orbit.GetDisplayName("Bob", null));
        Assert.Equal("Unknown", Orbit.GetDisplayName("Unknown [x]", "Unknown"));
        Assert.Equal("engineering", Orbit.GetDepartmentByJob("Station Engineer"));
        Assert.Equal("command", Orbit.GetDepartmentByJob("Captain (Acting)"));
        Assert.Null(Orbit.GetDepartmentByJob("Assistant"));
        UiValue amy = UiValue.FromJson(Json).Arr("alive")[1];
        UiValue zed = UiValue.FromJson(Json).Arr("alive")[0];
        Assert.Equal("blue", Orbit.GetDisplayColor(zed, Orbit.ViewDepartment));
        Assert.Equal("good", Orbit.GetDisplayColor(zed, Orbit.ViewOrbiters));
        Assert.Equal("grey", Orbit.GetDisplayColor(amy, Orbit.ViewHealth));
        Assert.Equal("grey", Orbit.GetDisplayColor(UiValue.FromJson("{\"full_name\":\"x\"}"), Orbit.ViewHealth));
        Assert.Equal("good", Orbit.GetDisplayColor(UiValue.FromJson("{\"full_name\":\"x\"}"), Orbit.ViewHealth, "purple"));
        Assert.Equal("id-badge", Orbit.NextViewMode("ghost"));
        Assert.Equal("heart", Orbit.NextViewMode("id-badge"));
        Assert.Contains("Health: 50%", Orbit.Tooltip(amy, false));
        Assert.Contains("Status: Away", Orbit.Tooltip(amy, false));
        Assert.Contains("Integrity:  5%", Orbit.Tooltip(UiValue.FromJson(Json).Arr("misc")[0], false));
        var groups = Orbit.GetAntagCategories(UiValue.FromJson(Json).Arr("antagonists"));
        Assert.Equal("Traitors", Assert.Single(groups).Title);
    }

    [Fact]
    public void The_blade_widens_the_window_and_shows_the_orbited_ones_info()
    {
        var local = new TguiLocalState();
        local.Set("bladeOpen", true);
        (TguiContext c, UiNode tree, _) = Render(local);
        Assert.Equal(650, c.WindowWidth);
        string text = tree.AllText();
        Assert.Contains("Orbit Settings", text);
        Assert.Contains("Orbiting", text);
        Assert.Contains("Station Engineer", text);
        Assert.Contains("Engineering", text);
    }
}
