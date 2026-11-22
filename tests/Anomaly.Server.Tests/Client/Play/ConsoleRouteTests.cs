using Anomaly.Client.Shared;
using Anomaly.Server;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// The dev console's labels and the client's own commands (the owner's request, 2026-10-10): each typed line is labelled
/// [client], [dev] or [server] in the log; help and clear run in the client and are never sent; the client's verb lists
/// match the server's parsers.
/// </summary>
public class ConsoleRouteTests
{
    [Theory]
    [InlineData("help", ConsoleRoute.Client)]
    [InlineData("CLEAR", ConsoleRoute.Client)]
    [InlineData("?", ConsoleRoute.Client)]
    [InlineData("godmode me", ConsoleRoute.Dev)]
    [InlineData("valve 3 4", ConsoleRoute.Dev)]
    [InlineData("dev-help", ConsoleRoute.Dev)]
    [InlineData("spawn /mob/living/carbon/human", ConsoleRoute.Dev)]
    [InlineData("spawn /obj/item/crowbar", ConsoleRoute.Dev)] // tg's spawn panel: any type (the dev menu, 2026-10-13)
    [InlineData("delete #5", ConsoleRoute.Dev)]
    [InlineData("air add plasma 10 3 4", ConsoleRoute.Dev)]
    [InlineData("teleport me 10 10", ConsoleRoute.Server)]
    [InlineData("list", ConsoleRoute.Server)]
    [InlineData("levitate me", ConsoleRoute.Dev)] // unknown: the server answers as for a dev command
    public void ALineIsClassifiedByItsVerb(string line, ConsoleRoute route)
        => Assert.Equal(route, DevConsoleModel.Classify(line));

    [Fact]
    public void HelpAndClearRunInTheClientAndSentLinesAreLabelled()
    {
        var console = new DevConsoleModel();
        Assert.False(console.TryRunLocal("godmode me")); // not the client's: the caller sends it
        console.OnSent("godmode me", 1);
        console.OnSent("list", 2);
        Assert.Contains("> [dev] godmode me", console.Log);
        Assert.Contains("> [server] list", console.Log);

        Assert.True(console.TryRunLocal("help"));
        Assert.Contains("> [client] help", console.Log);
        Assert.Contains("[server] the server console's own, when the server runs with --client-console", console.Log);
        Assert.Contains(console.Log, l => l.StartsWith("  godmode WHO ", StringComparison.Ordinal) && l.EndsWith("toggle taking no damage, stuns, stagger, fire or slowdowns", StringComparison.Ordinal));
        Assert.Equal(console.Log.Count, console.LogMarkup.Count);
        Assert.Contains(console.LogMarkup, l => l.Contains("[color=", StringComparison.Ordinal));

        Assert.True(console.TryRunLocal("help airmode"));
        Assert.Contains("[dev] airmode WHO MODE", console.Log);
        Assert.Contains(console.Log, l => l.Contains("filtering, contaminated", StringComparison.Ordinal));
        Assert.Equal("help airmode", console.History[^1]); // in the history like any line

        Assert.True(console.TryRunLocal("clear"));
        Assert.Empty(console.Log);
    }

    [Fact]
    public void TheUiCommandRunsTheScenesInterfaceSwitch()
    {
        // the owner (2026-11-11): "ui hide|show|toggle" hides the whole interface; the console only passes the mode to the scene
        var console = new DevConsoleModel();
        Assert.Equal(ConsoleRoute.Client, DevConsoleModel.Classify("ui hide"));
        Assert.True(console.TryRunLocal("ui hide"));
        Assert.Contains(console.Log, l => l.Contains("there is no interface here", StringComparison.Ordinal)); // no scene behind it
        var modes = new List<string>();
        console.Interface = mode =>
        {
            modes.Add(mode);
            return "switched " + mode;
        };
        Assert.True(console.TryRunLocal("ui SHOW"));
        Assert.True(console.TryRunLocal("ui"));
        Assert.Equal(new[] { "show", "toggle" }, modes);
        Assert.Contains(console.Log, l => l == "switched toggle");
    }

    [Fact]
    public void TheClientsVerbListsMatchTheServers()
    {
        foreach (string verb in DevConsoleModel.DevVerbs.Where(v => v != "dev-help"))
            Assert.True(DevCommandLine.IsDevVerb(verb), verb + " is a dev verb of the server");
        // every verb the server's dev help names is in the client's list
        foreach (string word in DevCommandLine.Help.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
            if (DevCommandLine.IsDevVerb(word))
                Assert.True(DevConsoleModel.DevVerbs.Contains(word.ToLowerInvariant()), word + " (in the server's dev help) is in the client's list");
        foreach (string verb in DevConsoleModel.ServerVerbs)
        {
            ConsoleCommand.TryParse(verb, out ConsoleCommand command, out string error);
            Assert.True(error == null || !error.StartsWith("unknown command", StringComparison.Ordinal), verb + " is a server console verb");
            if (verb != "spawn")
                Assert.False(DevCommandLine.IsDevVerb(verb), verb + " is no dev verb");
        }
        foreach (string verb in DevConsoleModel.ClientVerbs)
            Assert.False(DevCommandLine.IsDevVerb(verb), verb + " is the client's only");
    }

    [Theory]
    [InlineData("he", "lp")]
    [InlineData("HE", "lp")]
    [InlineData("tel", "eport")]
    [InlineData("god", "")] // a whole verb is not completed to godmode
    [InlineData("godmode ", "me")]
    [InlineData("damage me b", "rute")]
    [InlineData("damage me brute 5 l_", "arm")]
    [InlineData("airmode me pa", "nic")]
    [InlineData("give me /obj/item/cr", "owbar")]
    [InlineData("gas v", "acuum")]
    [InlineData("help val", "ve")]
    [InlineData("levitate ", "")]
    [InlineData("", "")]
    public void TheCompletionIsTheRestOfTheWordAtTheCaret(string line, string rest)
        => Assert.Equal(rest, ConsoleCatalog.Completion(line));

    [Fact]
    public void TabTakesTheCompletionAndASpaceWhenMoreWordsFollow()
    {
        Assert.Equal("help ", ConsoleCatalog.Complete("he"));
        Assert.Equal("clear", ConsoleCatalog.Complete("cle"));
        Assert.Equal("godmode me", ConsoleCatalog.Complete("godmode "));
        Assert.Equal("damage me ", ConsoleCatalog.Complete("damage m"));
        Assert.Equal("kill", ConsoleCatalog.Complete("kill")); // nothing to take
    }

    [Fact]
    public void TheSuggestionsListTheMatchingCommandsThenTheSyntax()
    {
        var he = ConsoleCatalog.Suggestions("he");
        Assert.Contains(he, l => l.Plain.StartsWith("[client] help [VERB]", StringComparison.Ordinal) && l.Plain.EndsWith("this list, or one command in detail", StringComparison.Ordinal));
        Assert.Contains(he, l => l.Plain.StartsWith("[dev]    heal WHO [KIND]", StringComparison.Ordinal)); // the labels padded to one column
        Assert.StartsWith("[dev]    god WHO ", ConsoleCatalog.Suggestions("god")[0].Plain, StringComparison.Ordinal); // the exact name first

        var spawn = ConsoleCatalog.Suggestions("spawn ");
        Assert.Contains(spawn, l => l.Plain.StartsWith("[dev] spawn TYPE [X Y [Z [N [DIR]]]]", StringComparison.Ordinal));
        Assert.Contains(spawn, l => l.Plain.StartsWith("[server] spawn TYPE", StringComparison.Ordinal));

        var damage = ConsoleCatalog.Suggestions("damage me ");
        Assert.Contains("[b]", damage[0].Markup, StringComparison.Ordinal); // the word at the caret is highlighted
        Assert.Contains(damage, l => l.Plain.Trim() == "brute  burn  tox  oxy");
        Assert.Equal("/obj/item/card/id/advanced/debug  /obj/item/crowbar  /obj/item/clothing/mask/gas", ConsoleCatalog.Suggestions("give me /obj/item/c")[1].Plain.Trim());
        Assert.Empty(ConsoleCatalog.Suggestions(""));
        Assert.Empty(ConsoleCatalog.Suggestions("levitate me"));
        // every command the typed part can still become is listed, however many (the panel scrolls)
        Assert.Equal(ConsoleCatalog.Commands.Count(c => c.Names.Any(n => n.StartsWith("d", StringComparison.Ordinal))), ConsoleCatalog.Suggestions("d").Count);
    }

    [Fact]
    public void EveryUsageParsesOnTheServerAndTheChoicesAreTheServers()
    {
        var samples = new Dictionary<string, string>
        {
            ["WHO"] = "me", ["KIND"] = "brute", ["N"] = "10", ["ZONE"] = "head", ["S"] = "2", ["STACKS"] = "5", ["ML"] = "500",
            ["MIX"] = "o2=22;n2=82;TEMP=293.15", ["X"] = "3", ["Y"] = "4", ["Z"] = "1", ["F"] = "1.5", ["ITEM"] = "/obj/item/crowbar",
            ["MODE"] = "filtering", ["TYPE"] = "/obj/item/crowbar", ["HALLUCINATION"] = "/datum/hallucination/body/husk", ["SECONDS"] = "30", ["OUTFIT"] = "/datum/outfit/job/assistant", ["ANTAGONIST"] = "/datum/antagonist/traitor", ["TRAUMA"] = "/datum/brain_trauma/mild/phobia/clowns", ["CODE"] = "12345", ["REASON"] = "bye", ["VERB"] = "kill",
            ["GAS"] = "plasma", ["MOLES"] = "10", ["R"] = "1", ["K"] = "300", ["DIR"] = "2", ["SLOT"] = "back", ["ENERGY"] = "600",
            ["DEV"] = "1", ["HEAVY"] = "2", ["LIGHT"] = "3", ["FLASH"] = "0", ["FLAME"] = "3", ["INTENSITY"] = "4", ["THRESHOLD"] = "0.7",
            ["DEV"] = "1", ["HEAVY"] = "2", ["LIGHT"] = "3", ["FLASH"] = "0", ["FLAME"] = "3", ["POWER"] = "1000000", ["TILES"] = "1.5",
            ["ACCEL"] = "4", ["DECEL"] = "3", ["BACK"] = "0.3", ["STRAFE"] = "0.1", ["PLATFORM"] = "2",
        };
        foreach (ConsoleCommandInfo c in ConsoleCatalog.Commands.Where(c => c.Route != ConsoleRoute.Client && c.Verb != "dev-help"))
        {
            string line = string.Join(' ', c.Usage.Split(' ').Select(t => t.Trim('[', ']').Split('|')[^1]).Select(t => samples.TryGetValue(t, out string v) ? v : t));
            if (c.Route == ConsoleRoute.Dev)
                Assert.True(DevCommandLine.TryParse(line, out _, out string error), line + ": " + error);
            else
                Assert.True(ConsoleCommand.TryParse(line, out _, out string error), line + ": " + error);
        }
        Assert.Equal(DevCommandLine.AirModes, ConsoleCatalog.AirModes);
        foreach (string kind in ConsoleCatalog.Choices["KIND"])
            Assert.True(DevCommandLine.TryParse("heal me " + kind, out _, out _), kind);
        foreach (string zone in ConsoleCatalog.Choices["ZONE"])
            Assert.True(DevCommandLine.TryParse("bleed me " + zone + " 1", out _, out _), zone);
    }
}
