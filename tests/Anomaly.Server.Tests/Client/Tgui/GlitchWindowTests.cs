using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// The bitrunning glitch's info window (tg's AntagInfoGlitch.tsx): a terminal listing with the glitch's name, the lines of its role
/// (the cyber police's martial arts, the cyber tactical unit's weapons, the netguardian's order) and its objective, in the
/// ntos_terminal theme.
/// </summary>
public class GlitchWindowTests
{
    private static UiNode Render(string name, out TguiContext context)
    {
        string json = "{\"antag_name\":\"" + name + "\",\"objectives\":[{\"count\":1,\"name\":\"objective\",\"explanation\":\"Initiate lockdown on all rogue scripts.\",\"complete\":0}]}";
        context = new TguiContext(1, "AntagInfoGlitch", "w", 2, UiValue.FromJson(json), (_, _) => { }, new TguiLocalState());
        return AntagInfoGlitch.Render(context);
    }

    private static string Html(UiNode tree, string key) => tree.Walk().First(n => n.Key == key && n.Kind == UiKind.Html).TextValue;

    [Fact]
    public void TheCyberPoliceReadsItsRoleAndObjectiveOnATerminal()
    {
        Assert.NotNull(TguiInterfaces.Find("AntagInfoGlitch"));
        UiNode tree = Render("Cyber Police", out TguiContext context);
        Assert.Equal((350.0, 450.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("ntos_terminal", context.WindowTheme);
        Assert.Contains("Initialize(Cyber Police)", Html(tree, "init"));
        Assert.Contains("martial arts", Html(tree, "specific1"));
        Assert.Contains("forbidden", Html(tree, "specific2"));
        Assert.Equal("// Initiate lockdown on all rogue scripts.", tree.Walk().First(n => n.Key == "objective" && n.Kind == UiKind.Box).Text);
        Assert.Contains("KILL()", Html(tree, "kill"));
    }

    [Fact]
    public void EachGlitchGetsItsOwnLines()
    {
        Assert.Contains("lethal weaponry", Html(Render("Cyber Tac", out _), "specific1"));
        Assert.Contains("ORGANIC LIFE MUST BE TERMINATED.", Html(Render("NetGuardian Prime", out _), "specific1"));
        // a domain's ghost actor has no role lines
        Assert.DoesNotContain(Render("Virtual Domain Actor", out _).Walk(), n => n.Key == "specific1");
    }
}
