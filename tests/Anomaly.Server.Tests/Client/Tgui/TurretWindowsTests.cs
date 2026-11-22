using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Syndicate stage G4d (D316): the client's turret windows without the engine: PortableTurret.jsx and TurretControl.tsx read tg's
/// ui_data the way the JSX and TSX do and send tg's acts.
/// </summary>
public class TurretWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<string> Acts) Render(TguiInterface view, string name, string json)
    {
        var acts = new List<string>();
        var context = new TguiContext(1, name, name, 2, UiValue.FromJson(json), (a, p) => acts.Add(a), new TguiLocalState());
        return (context, view(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    [Fact]
    public void An_unlocked_turret_shows_its_target_flags_and_sends_tgs_acts()
    {
        var (context, tree, acts) = Render(PortableTurret.Render, "PortableTurret", "{\"locked\":0,\"on\":1,\"check_weapons\":0,\"neutralize_criminals\":4,"
            + "\"neutralize_all\":0,\"neutralize_unidentified\":16,\"neutralize_nonmindshielded\":0,\"neutralize_cyborgs\":0,\"neutralize_heads\":0,"
            + "\"manual_control\":0,\"silicon_user\":0,\"allow_manual_control\":0,\"lasertag_turret\":0}");
        Assert.Equal((310.0, 292.0), (context.WindowWidth, context.WindowHeight));
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.NoticeBox && n.Text == "Swipe an ID card to lock this interface.");
        Assert.Equal(("On", true), (ByKey(tree, "power").Text, ByKey(tree, "power").Selected));
        Assert.True(ByKey(tree, "shootcriminals").Checkbox);
        Assert.True(ByKey(tree, "checkxenos").Checkbox);
        Assert.False(ByKey(tree, "authweapon").Checkbox);
        Assert.True(ByKey(tree, "shootheads").Checkbox); // "Ignore Command" is checked while the heads are not targeted
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "manual");
        ByKey(tree, "power").OnClick();
        ByKey(tree, "shootall").OnClick();
        Assert.Equal(new[] { "power", "shootall" }, acts);
    }

    [Fact]
    public void A_lasertag_turret_has_only_its_power_and_a_locked_one_disables_it()
    {
        var (context, tree, _) = Render(PortableTurret.Render, "PortableTurret", "{\"locked\":1,\"on\":0,\"lasertag_turret\":1}");
        Assert.Equal((310.0, 110.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Off", ByKey(tree, "power").Text);
        Assert.True(ByKey(tree, "power").Disabled);
        Assert.DoesNotContain(tree.Walk(), n => n.Title == "Target Settings");
    }

    [Fact]
    public void The_turret_control_panel_shows_its_modes_and_locks_for_a_crewmember()
    {
        var (context, tree, acts) = Render(TurretControl.Render, "TurretControl", "{\"locked\":0,\"siliconUser\":0,\"enabled\":1,\"lethal\":1,\"shootCyborgs\":0}");
        Assert.Equal((305.0, 164.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(("Enabled", "power-off"), (ByKey(tree, "power").Text, ByKey(tree, "power").Icon));
        Assert.Equal(("Lethal", "bad"), (ByKey(tree, "mode").Text, ByKey(tree, "mode").Color));
        Assert.Equal("No", ByKey(tree, "shoot_silicons").Text);
        ByKey(tree, "mode").OnClick();
        ByKey(tree, "shoot_silicons").OnClick();
        Assert.Equal(new[] { "mode", "shoot_silicons" }, acts);

        tree = TurretControl.Render(new TguiContext(1, "TurretControl", "TurretControl", 2, UiValue.FromJson("{\"locked\":1,\"siliconUser\":0,\"enabled\":0,\"lethal\":0,\"shootCyborgs\":1}"), (a, p) => { }, new TguiLocalState()));
        Assert.True(ByKey(tree, "power").Disabled);
        Assert.Equal(("Stun", "average"), (ByKey(tree, "mode").Text, ByKey(tree, "mode").Color));
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.NoticeBox && n.Text == "Swipe an ID card to unlock this interface.");
    }
}
