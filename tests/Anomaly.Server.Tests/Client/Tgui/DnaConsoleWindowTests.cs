using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps P3 (D327): the client's DnaConsole window without the engine: the scanner's subject and buttons, the mode buttons, the
/// sequencer's gene pictures and gene pairs (a click steps a gene, a right click steps it back, Ctrl+click sets it to X), the
/// mutation info's injectors and saves, the storage tabs, the enzymes' pulse board and the genetic makeup buffers, and the pulse's
/// dimmer.
/// </summary>
public class DnaConsoleWindowTests
{
    private static (TguiContext Context, List<(string Action, string Params)> Acts) Context(string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, "DnaConsole", "DNA Console", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, acts);
    }

    private const string Occupant = "[{\"Alias\":\"Mutation 3\",\"Sequence\":\"ATCGXTGC\",\"DefaultSeq\":\"ATXGCTGC\",\"Discovered\":1,\"Source\":\"occupant\","
        + "\"Name\":\"Glowy\",\"Description\":\"The subject's skin emits a soft glow.\",\"Instability\":5,\"Quality\":1,\"Active\":0,\"Scrambled\":0,\"Class\":1,"
        + "\"Image\":\"dna_discovered.gif\"},"
        + "{\"Alias\":\"Mutation 7\",\"Sequence\":\"GGCCAATT\",\"DefaultSeq\":\"GGCCAATT\",\"Discovered\":0,\"Source\":\"occupant\",\"Active\":0,\"Scrambled\":0,"
        + "\"Class\":1,\"Image\":\"dna_undiscovered.gif\"}]";

    private const string Console = "[{\"Alias\":\"Mutation 12\",\"Name\":\"Strength\",\"Source\":\"console\",\"Active\":1,\"Description\":\"Muscles.\","
        + "\"Instability\":10,\"ByondRef\":\"[0x21]\",\"Type\":\"/datum/mutation/strong\",\"CanChromo\":1,\"ValidChromos\":\"Power\",\"AppliedChromo\":null,"
        + "\"ValidStoredChromos\":[]}]";

    private static string Data(string mode, string extra = "") => "{\"view\":{\"consoleMode\":\"" + mode + "\",\"storageMode\":\"console\","
        + "\"storageConsSubMode\":\"mutations\",\"storageDiskSubMode\":\"mutations\",\"sequencerMutation\":\"Mutation 3\"},"
        + "\"storage\":{\"occupant\":" + Occupant + ",\"console\":" + Console + ",\"disk\":null,\"injector\":[]},"
        + "\"isScannerConnected\":1,\"scannerOpen\":0,\"scannerLocked\":0,\"pulseStrength\":1,\"pulseDuration\":2,\"stdDevStr\":1,\"stdDevAcc\":\">95 %\","
        + "\"isViableSubject\":1,\"subjectName\":\"Jane Doe\",\"subjectStatus\":0,\"subjectHealth\":100,\"subjectEnzymes\":\"abc\",\"isMonkey\":0,"
        + "\"subjectUNI\":\"0123456789ab\",\"subjectUF\":\"fedcba\",\"subjectDamage\":12.5,\"hasDelayedAction\":0,\"isScrambleReady\":0,\"isJokerReady\":1,"
        + "\"isInjectorReady\":1,\"isCrisprReady\":0,\"crisprCharges\":0,\"scrambleSeconds\":42,\"jokerSeconds\":0,\"injectorSeconds\":0,\"isPulsing\":0,"
        + "\"timeToPulse\":0,\"geneticMakeupCooldown\":0,\"hasDisk\":0,\"diskCapacity\":0,\"diskReadOnly\":1,\"diskHasMakeup\":0,\"diskMakeupBuffer\":null,"
        + "\"chromoStorage\":[{\"Name\":\"power chromosome\",\"Description\":\"More power.\",\"Index\":1}],\"makeupCapacity\":3,"
        + "\"makeupStorage\":{\"1\":{\"label\":\"Slot 1:Jane Doe\",\"UI\":\"0123\",\"UE\":\"abc\",\"UF\":\"fed\",\"name\":\"Jane Doe\",\"blood_type\":\"O-\"},"
        + "\"2\":null,\"3\":null},\"maxAdvInjectors\":2,\"heldScannerBuffer\":{\"Mutation 3\":\"ATCGATGC\"}" + extra + "}";

    [Fact]
    public void TheScannerShowsItsSubjectAndTheModesSwitchTheView()
    {
        var (c, acts) = Context(Data("storage"));
        UiNode tree = DnaConsole.Render(c);
        Assert.Equal((550.0, 710.0, "DNA Console"), (c.WindowWidth, c.WindowHeight, c.WindowTitle));
        UiNode scanner = tree.Walk().Single(n => n.Key == "dna-scanner");
        Assert.Equal("Jane Doe", scanner.Walk().Single(n => n.Key == "name" && n.Kind == UiKind.Box).Text);
        UiNode status = scanner.Walk().Single(n => n.Key == "status" && n.Kind == UiKind.Box);
        Assert.Equal(("Conscious", "good"), (status.Text, status.Color));
        Assert.Equal("12.5%", tree.Walk().Single(n => n.Key == "damage" && n.Kind == UiKind.ProgressBar).Text);
        UiNode scramble = tree.Walk().Single(n => n.Key == "scramble");
        Assert.Equal(("Scramble DNA (42s)", true), (scramble.Text, scramble.Disabled));
        tree.Walk().Single(n => n.Key == "lock").OnClick();
        Assert.Equal(("toggle_lock", (string)null), acts.Last());
        tree.Walk().Single(n => n.Key == "mode:sequencer").OnClick();
        Assert.Equal(("set_view", "{\"consoleMode\":\"sequencer\"}"), acts.Last());
        Assert.True(tree.Walk().Single(n => n.Key == "mode:storage").Selected);
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "pulsing");
    }

    [Fact]
    public void TheSequencerStepsGenesAndMarksWhatTheConsoleClearedAndTheHeldScanner()
    {
        var (c, acts) = Context(Data("sequencer"));
        UiNode tree = DnaConsole.Render(c);
        UiNode picture = tree.Walk().Single(n => n.Key == "genome:Mutation 3");
        Assert.True(picture.Selected);
        Assert.Equal("icons/ui/dna/dna_discovered", picture.Walk().Single(n => n.Key == "gif").DmIcon);
        Assert.Contains(picture.Walk(), n => n.Key == "in-scanner"); // the held scanner knows this one
        Assert.DoesNotContain(tree.Walk().Single(n => n.Key == "genome:Mutation 7").Walk(), n => n.Key == "in-scanner");
        tree.Walk().Single(n => n.Key == "genome:Mutation 7").OnClick();
        Assert.Equal(new[] { ("set_view", "{\"sequencerMutation\":\"Mutation 7\"}"), ("check_discovery", "{\"alias\":\"Mutation 7\"}") }, acts.TakeLast(2));

        UiNode sequencer = tree.Walk().Single(n => n.Key == "genome-sequencer");
        UiNode gene3 = sequencer.Walk().Single(n => n.Key == "gene:2");
        Assert.Equal(("C", "blue", "orange"), (gene3.Text, gene3.Color, gene3.StrokeColor)); // the console X'd its default
        Assert.Equal("grey", sequencer.Walk().Single(n => n.Key == "gene:4").Color);
        gene3.OnClickWith(new UiClick(false, false, false, false));
        Assert.Equal(("pulse_gene", "{\"pos\":3,\"pulseAction\":1,\"alias\":\"Mutation 3\"}"), acts.Last());
        gene3.OnClickWith(new UiClick(false, false, false, true));
        Assert.Equal(("pulse_gene", "{\"pos\":3,\"pulseAction\":2,\"alias\":\"Mutation 3\"}"), acts.Last());
        gene3.OnClickWith(new UiClick(false, true, false, false));
        Assert.Equal(("pulse_gene", "{\"pos\":3,\"pulseAction\":0,\"alias\":\"Mutation 3\"}"), acts.Last());
        Assert.Contains(tree.Walk(), n => n.Key == "held-scanner");
        tree.Walk().Single(n => n.Key == "joker").OnClick();
        Assert.Equal(("set_view", "{\"jokerActive\":\"1\"}"), acts.Last());

        // the discovered mutation's info: its name in its quality's colour
        UiNode info = tree.Walk().Single(n => n.Key == "sequence-info");
        Assert.Equal(("Glowy", "good"), (info.Walk().Single(n => n.Key == "name" && n.Kind == UiKind.Box).Text, info.Walk().Single(n => n.Key == "name" && n.Kind == UiKind.Box).Color));
    }

    [Fact]
    public void TheStorageListsTheConsolesMutationsAndPrintsTheirInjectors()
    {
        var (c, acts) = Context(Data("storage"));
        UiNode tree = DnaConsole.Render(c);
        UiNode tab = tree.Walk().Single(n => n.Key == "mut:[0x21]");
        Assert.Equal(("Strength", true), (tab.Text, tab.Selected)); // the first one is shown when none is chosen
        tree.Walk().Single(n => n.Key == "mutator" && n.Kind == UiKind.Button).OnClick();
        Assert.Equal(("print_injector", "{\"mutref\":\"[0x21]\",\"is_activator\":0,\"source\":\"console\"}"), acts.Last());
        tree.Walk().Single(n => n.Key == "storage").Walk().Single(n => n.Key == "delete" && n.Kind == UiKind.Button).OnClick();
        Assert.Equal(("delete_console_mut", "{\"mutref\":\"[0x21]\"}"), acts.Last());
        Assert.True(tree.Walk().Single(n => n.Key == "save-disk" && n.Kind == UiKind.Button).Disabled); // no disk
        tree.Walk().Single(n => n.Key == "cons-chromosomes").OnClick();
        Assert.Equal(("set_view", "{\"storageConsSubMode\":\"chromosomes\"}"), acts.Last());
    }

    [Fact]
    public void TheEnzymesPulseABlockAndTheBuffersPrintAndTransfer()
    {
        var (c, acts) = Context(Data("enzymes"));
        UiNode tree = DnaConsole.Render(c);
        Assert.Equal(12, tree.Walk().Count(n => n.Key != null && n.Key.StartsWith("pulse:", StringComparison.Ordinal) && n.Kind == UiKind.Button));
        tree.Walk().Single(n => n.Key == "pulse:4" && n.Kind == UiKind.Button).OnClick();
        Assert.Equal(("makeup_pulse", "{\"index\":5,\"type\":\"ui\"}"), acts.Last());
        UiNode slot = tree.Walk().Single(n => n.Key == "slot:1");
        Assert.Equal("Slot 1:Jane Doe", slot.Title);
        Assert.NotNull(slot.Buttons);
        slot.Walk().Single(n => n.Key == "print:ue").OnClick();
        Assert.Equal(("makeup_injector", "{\"index\":1,\"type\":\"ue\"}"), acts.Last());
        slot.Walk().Single(n => n.Key == "transfer:mixed").OnClick();
        Assert.Equal(("makeup_apply", "{\"index\":1,\"type\":\"mixed\"}"), acts.Last());
        Assert.Equal("Slot 2", tree.Walk().Single(n => n.Key == "slot:2").Title);
        tree.Walk().Single(n => n.Key == "strength" && n.Kind == UiKind.NumberInput).OnChange(7);
        Assert.Equal(("set_pulse_strength", "{\"val\":7}"), acts.Last());
    }

    [Fact]
    public void APulseInProgressDimsTheWindow()
    {
        var (c, _) = Context(Data("features").Replace("\"isPulsing\":0,\"timeToPulse\":0", "\"isPulsing\":1,\"timeToPulse\":9"));
        UiNode tree = DnaConsole.Render(c);
        UiNode dimmer = tree.Walk().Single(n => n.Key == "pulsing");
        Assert.Equal(UiKind.Modal, dimmer.Kind);
        Assert.Equal("9s", dimmer.Walk().Single(n => n.Key == "pulse-time").Text);
        Assert.Equal("Unique Features", tree.Walk().Single(n => n.Key == "pulse-board").Title);
    }
}
