using System.Collections.Generic;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/DnaConsole/ (index.jsx, constants.ts, DnaScanner.jsx,
// DnaConsoleSequencer.jsx, DnaConsoleEnzymes.jsx, DnaConsoleStorage.jsx, MutationInfo.jsx, GeneticMakeupInfo.jsx; gaps P3, D327): the
// DNA console: the scanner's subject, door, bolts and scramble; the mode buttons; the genetic sequencer with its gene pictures
// (tg's /datum/asset/simple/genetics GIFs, icons/ui/dna/), the gene pairs that a click steps, a right click steps back and a
// Ctrl+click sets to X, the joker and the held sequence scanner's copy; the enzymes and features pulse board with the emitter's
// settings and the genetic makeup buffers; the console, disk and advanced injector storage with the mutation info, the
// chromosomes and the combining. Dimmer is the kit's Modal (over the window, where tg dims its section); a picture's corner
// letter is an Overlay anchor; tg's orange outline of a gene the console X'd is the button's StrokeColor. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class DnaConsole
{
    // ---- constants.ts ----
    private const int CHROMOSOME_NEVER = 0;
    private const int CHROMOSOME_NONE = 1;
    private const int CHROMOSOME_USED = 2;

    private const string CONSOLE_MODE_ENZYMES = "enzymes";
    private const string CONSOLE_MODE_FEATURES = "features";
    private const string CONSOLE_MODE_SEQUENCER = "sequencer";
    private const string CONSOLE_MODE_STORAGE = "storage";

    // tg: constants.ts:11  GENE_COLORS
    private static string GeneColor(string gene) => gene switch
    {
        "A" or "T" => "green",
        "G" or "C" => "blue",
        _ => "grey",
    };

    private const int MUT_NORMAL = 1;
    private const int MUT_EXTRA = 2;
    private const int MUT_OTHER = 3;

    private const string STORAGE_CONS_SUBMODE_MUTATIONS = "mutations";
    private const string STORAGE_CONS_SUBMODE_CHROMOSOMES = "chromosomes";
    private const string STORAGE_DISK_SUBMODE_MUTATIONS = "mutations";
    private const string STORAGE_DISK_SUBMODE_ENZYMES = "diskenzymes";
    private const string STORAGE_MODE_CONSOLE = "console";
    private const string STORAGE_MODE_DISK = "disk";
    private const string STORAGE_MODE_ADVINJ = "injector";

    private const int SUBJECT_CONCIOUS = 0;
    private const int SUBJECT_SOFT_CRIT = 1;
    private const int SUBJECT_UNCONSCIOUS = 2;
    private const int SUBJECT_HARD_CRIT = 3;
    private const int SUBJECT_DEAD = 4;
    private const int SUBJECT_TRANSFORMING = 5;

    private const int PULSE_STRENGTH_MAX = 15;
    private const int PULSE_DURATION_MAX = 30;

    // tg: constants.ts:44  MUT_COLORS (__DEFINES/DNA.dm's POSITIVE, NEGATIVE, MINOR_NEGATIVE)
    private static string MutColor(double quality) => (int)quality switch
    {
        1 => "good",
        2 => "bad",
        4 => "average",
        _ => null,
    };

    private const int CLEAR_GENE = 0;
    private const int NEXT_GENE = 1;
    private const int PREV_GENE = 2;

    private static UiValue View(TguiContext c) => c.Data.Get("view");

    private static IReadOnlyList<UiValue> Storage(TguiContext c, string key) => c.Data.Get("storage").Arr(key);

    // ---- index.jsx ----

    // tg: index.jsx:24
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string consoleMode = View(c).Str("consoleMode");

        UiNode mode = consoleMode switch
        {
            CONSOLE_MODE_STORAGE => Storage(c),
            CONSOLE_MODE_SEQUENCER => Sequencer(c),
            CONSOLE_MODE_ENZYMES => Enzymes(c, data.Str("subjectUNI"), "ui", "Enzymes"),
            CONSOLE_MODE_FEATURES => Enzymes(c, data.Str("subjectUF"), "uf", "Features"),
            _ => null,
        };
        var content = new UiNode(UiKind.Stack) { Key = "dna-console", Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "scanner" }.With(DnaScanner(c)),
            new UiNode(UiKind.StackItem) { Key = "commands" }.With(Commands(c)),
            new UiNode(UiKind.StackItem) { Key = "mode", Grow = 1 }.With(mode));
        // tg: index.jsx:31  <Dimmer>: the pulse in progress
        UiNode dimmer = data.Truthy("isPulsing")
            ? new UiNode(UiKind.Modal) { Key = "pulsing" }.With(
                new UiNode(UiKind.Box) { Key = "pulse-text", FontSize = "14px", TextAlign = "center", Icon = "spinner", Text = " Pulse in progress..." },
                new UiNode(UiKind.Box) { Key = "pulse-time", FontSize = "14px", TextAlign = "center", Text = TguiFormat.JsNumber(data.Num("timeToPulse")) + "s" })
            : null;
        return c.Window(550, 710, "DNA Console", dimmer, content);
    }

    // tg: index.jsx:73
    private static UiNode Commands(TguiContext c)
    {
        UiValue data = c.Data;
        string consoleMode = View(c).Str("consoleMode");
        UiNode ModeButton(string text, string mode, bool disabled = false) => new UiNode(UiKind.Button)
        {
            Key = "mode:" + mode,
            Text = text,
            Disabled = disabled,
            Selected = consoleMode == mode,
            OnClick = () => c.Act("set_view", ("consoleMode", mode)),
        };
        var list = new UiNode(UiKind.LabeledList).With(
            new UiNode(UiKind.LabeledItem) { Key = "mode", Label = "Mode" }.With(
                ModeButton("Storage", CONSOLE_MODE_STORAGE),
                ModeButton("Sequencer", CONSOLE_MODE_SEQUENCER, !data.Truthy("isViableSubject")),
                ModeButton("Enzymes", CONSOLE_MODE_ENZYMES),
                ModeButton("Features", CONSOLE_MODE_FEATURES)));
        if (data.Truthy("hasDisk"))
        {
            list.With(new UiNode(UiKind.LabeledItem) { Key = "disk", Label = "Disk" }.With(new UiNode(UiKind.Button)
            {
                Key = "eject-disk",
                Icon = "eject",
                Text = "Eject",
                OnClick = () =>
                {
                    c.Act("eject_disk");
                    c.Act("set_view", ("storageMode", STORAGE_MODE_CONSOLE));
                },
            }));
        }
        return new UiNode(UiKind.Section)
        {
            Key = "dna-commands",
            Title = "DNA Console",
            Buttons = !data.Truthy("isInjectorReady")
                ? new UiNode(UiKind.Box) { Key = "injector-cd", Color = "label", Text = "Injector on cooldown (" + TguiFormat.JsNumber(data.Num("injectorSeconds")) + "s)" }
                : null,
        }.With(list);
    }

    // ---- DnaScanner.jsx ----

    // tg: DnaScanner.jsx:20
    private static UiNode ScannerButtons(TguiContext c)
    {
        UiValue data = c.Data;
        if (!data.Truthy("isScannerConnected"))
            return new UiNode(UiKind.Button) { Key = "connect", Text = "Connect Scanner", OnClick = () => c.Act("connect_scanner") };
        bool locked = data.Truthy("scannerLocked");
        bool open = data.Truthy("scannerOpen");
        var buttons = new UiNode(UiKind.Fragment);
        if (data.Truthy("hasDelayedAction"))
            buttons.With(new UiNode(UiKind.Button) { Key = "cancel-delay", Text = "Cancel Delayed Action", OnClick = () => c.Act("cancel_delay") });
        if (data.Truthy("isViableSubject"))
        {
            bool ready = data.Truthy("isScrambleReady");
            buttons.With(new UiNode(UiKind.Button)
            {
                Key = "scramble",
                Disabled = !ready || data.Truthy("isPulsing"),
                Text = "Scramble DNA" + (!ready ? " (" + TguiFormat.JsNumber(data.Num("scrambleSeconds")) + "s)" : ""),
                OnClick = () => c.Act("scramble_dna"),
            });
        }
        return buttons.With(
            new UiNode(UiKind.Button)
            {
                Key = "lock",
                Icon = locked ? "lock" : "lock-open",
                Color = locked ? "bad" : null,
                Disabled = open,
                Text = locked ? "Locked" : "Unlocked",
                OnClick = () => c.Act("toggle_lock"),
            },
            new UiNode(UiKind.Button)
            {
                Key = "door",
                Disabled = locked,
                Text = open ? "Close" : "Open",
                OnClick = () => c.Act("toggle_door"),
            });
    }

    // tg: DnaScanner.jsx:76  SubjectStatus
    private static UiNode SubjectStatus(double status) => (int)status switch
    {
        SUBJECT_CONCIOUS => new UiNode(UiKind.Box) { Key = "status", Color = "good", Text = "Conscious" },
        SUBJECT_UNCONSCIOUS or SUBJECT_HARD_CRIT => new UiNode(UiKind.Box) { Key = "status", Color = "average", Text = "Unconscious" },
        SUBJECT_SOFT_CRIT => new UiNode(UiKind.Box) { Key = "status", Color = "average", Text = "Critical" },
        SUBJECT_DEAD => new UiNode(UiKind.Box) { Key = "status", Color = "bad", Text = "Dead" },
        SUBJECT_TRANSFORMING => new UiNode(UiKind.Box) { Key = "status", Color = "bad", Text = "Transforming" },
        _ => new UiNode(UiKind.Box) { Key = "status", Text = "Unknown" },
    };

    private static readonly (string, double, double)[] HealthRanges =
    {
        ("olive", 101, double.PositiveInfinity),
        ("good", 70, 101),
        ("average", 30, 70),
        ("bad", double.NegativeInfinity, 30),
    };

    private static readonly (string, double, double)[] DamageRanges =
    {
        ("bad", 71, double.PositiveInfinity),
        ("average", 30, 71),
        ("good", 0, 30),
        ("olive", double.NegativeInfinity, 0),
    };

    // tg: DnaScanner.jsx:118  DnaScannerContent
    private static UiNode ScannerContent(TguiContext c)
    {
        UiValue data = c.Data;
        if (!data.Truthy("isScannerConnected"))
            return new UiNode(UiKind.Box) { Key = "no-scanner", Color = "bad", Text = "DNA Scanner is not connected." };
        if (!data.Truthy("isViableSubject"))
            return new UiNode(UiKind.Box) { Key = "no-subject", Color = "average", Text = "No viable subject found in DNA Scanner." };
        double health = data.Num("subjectHealth");
        double damage = data.Num("subjectDamage");
        return new UiNode(UiKind.LabeledList).With(
            new UiNode(UiKind.LabeledItem) { Key = "status", Label = "Status" }.With(new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Key = "name", Text = data.Str("subjectName") }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Icon) { Key = "arrow", Color = "label", Icon = "long-arrow-alt-right" }),
                new UiNode(UiKind.StackItem).With(SubjectStatus(data.Num("subjectStatus", -1))))),
            new UiNode(UiKind.LabeledItem) { Key = "health", Label = "Health" }.With(new UiNode(UiKind.ProgressBar)
            {
                Key = "health",
                Value = health,
                MinValue = 0,
                MaxValue = 100,
                Ranges = HealthRanges,
                Text = TguiFormat.JsNumber(health) + "%",
            }),
            new UiNode(UiKind.LabeledItem) { Key = "damage", Label = "Genetic Damage" }.With(new UiNode(UiKind.ProgressBar)
            {
                Key = "damage",
                Value = damage,
                MinValue = 0,
                MaxValue = 100,
                Ranges = DamageRanges,
                Text = TguiFormat.JsNumber(damage) + "%",
            }));
    }

    // tg: DnaScanner.jsx:173
    private static UiNode DnaScanner(TguiContext c) =>
        new UiNode(UiKind.Section) { Key = "dna-scanner", Title = "DNA Scanner", Buttons = ScannerButtons(c) }.With(ScannerContent(c));

    // ---- DnaConsoleSequencer.jsx ----

    // tg: DnaConsoleSequencer.jsx:18  GenomeImage
    private static UiNode GenomeImage(string key, string image, bool selected, bool inScanner, System.Action onClick)
    {
        string picture = "icons/ui/dna/" + (image.EndsWith(".gif") ? image[..^4] : image);
        var overlay = new UiNode(UiKind.Overlay) { Key = "picture", Width = "64px", Height = "37px" }.With(
            new UiNode(UiKind.Box) { Key = "gif", DmIcon = picture });
        if (inScanner)
            overlay.With(new UiNode(UiKind.Box) { Key = "in-scanner", Anchor = "bottom-right", Color = "good", FontSize = "10px", Bold = true, Text = "M" });
        return new UiNode(UiKind.Button)
        {
            Key = key,
            Color = "transparent",
            Selected = selected,
            OnClick = onClick,
        }.With(overlay);
    }

    // tg: DnaConsoleSequencer.jsx:56  GeneCycler
    private static UiNode GeneCycler(TguiContext c, string alias, string gene, int index, bool disabled, string outline)
    {
        return new UiNode(UiKind.Button)
        {
            Key = "gene:" + index,
            Width = "22px",
            TextAlign = "center",
            Color = disabled ? GeneColor("X") : GeneColor(gene),
            StrokeColor = outline,
            Text = gene,
            OnClickWith = click =>
            {
                if (click.Right)
                {
                    c.Act("pulse_gene", ("pos", index + 1), ("pulseAction", PREV_GENE), ("alias", alias));
                    return;
                }
                if (click.Ctrl)
                {
                    c.Act("pulse_gene", ("pos", index + 1), ("pulseAction", CLEAR_GENE), ("alias", alias));
                    return;
                }
                c.Act("pulse_gene", ("pos", index + 1), ("pulseAction", NEXT_GENE), ("alias", alias));
            },
        };
    }

    // tg: DnaConsoleSequencer.jsx:98  isPairMatched
    private static bool IsPairMatched(string sequence, int index)
    {
        char gene1 = index < sequence.Length ? sequence[index] : '\0';
        char gene2 = index + 1 < sequence.Length ? sequence[index + 1] : '\0';
        return (gene1 == 'A' && gene2 == 'T') || (gene1 == 'T' && gene2 == 'A') || (gene1 == 'G' && gene2 == 'C') || (gene1 == 'C' && gene2 == 'G');
    }

    // tg: DnaConsoleSequencer.jsx:109  buildGenomePairs: a pair is its two genes over each other with the bond between them; every
    // fourth pair after the first starts with tg's short divider
    private static UiNode BuildGenomePairs(string sequence, System.Func<int, UiNode> renderGene)
    {
        var pairs = new UiNode(UiKind.Stack) { Key = "pairs", Wrap = true };
        for (int i = 0; i < sequence.Length; i += 2)
        {
            if (i % 8 == 0 && i != 0)
                pairs.With(new UiNode(UiKind.StackItem) { Key = i + "_divider" }.With(new UiNode(UiKind.Box) { Width = "8px", Height = "2px", Background = "label" }));
            pairs.With(new UiNode(UiKind.StackItem) { Key = "pair:" + i }.With(new UiNode(UiKind.Stack) { Vertical = true, Align = "center" }.With(
                new UiNode(UiKind.StackItem) { Key = "top" }.With(renderGene(i)),
                new UiNode(UiKind.StackItem) { Key = "bond" }.With(new UiNode(UiKind.Box) { Width = "3px", Height = "8px", Background = IsPairMatched(sequence, i) ? "label" : "red" }),
                new UiNode(UiKind.StackItem) { Key = "bottom" }.With(renderGene(i + 1)))));
        }
        return pairs;
    }

    // tg: DnaConsoleSequencer.jsx:149  GenomeSequencer
    private static UiNode GenomeSequencer(TguiContext c, UiValue mutation)
    {
        if (mutation == null)
            return new UiNode(UiKind.Box) { Key = "none", Color = "average", Text = "No genome selected for sequencing." };
        if (mutation.Truthy("Scrambled"))
            return new UiNode(UiKind.Box) { Key = "scrambled", Color = "average", Text = "Sequence unreadable due to unpredictable mutation." };
        string sequence = mutation.Str("Sequence");
        string defaultSeq = mutation.Str("DefaultSeq", null);
        bool disabled = mutation.Truthy("Scrambled") || mutation.Num("Class") != MUT_NORMAL;
        UiNode pairs = BuildGenomePairs(sequence, i => i < sequence.Length
            ? GeneCycler(c, mutation.Str("Alias"), sequence[i].ToString(), i, disabled,
                defaultSeq != null && i < defaultSeq.Length && defaultSeq[i] == 'X' && !mutation.Truthy("Active") ? "orange" : null)
            : null);
        return new UiNode(UiKind.Fragment).With(
            pairs,
            new UiNode(UiKind.Box) { Key = "tip", Color = "label", Text = "Tip: Ctrl+Click on the gene to set it to X. Right Click to cycle in reverse." });
    }

    // tg: DnaConsoleSequencer.jsx:187  GenomeSequencerReadOnly
    private static UiNode GenomeSequencerReadOnly(string sequence) =>
        BuildGenomePairs(sequence, i => i < sequence.Length
            ? new UiNode(UiKind.Button) { Key = "gene:" + i, Width = "22px", TextAlign = "center", Color = GeneColor(sequence[i].ToString()), Text = sequence[i].ToString() }
            : null);

    // tg: DnaConsoleSequencer.jsx:206
    private static UiNode Sequencer(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> mutations = Storage(c, "occupant");
        UiValue view = View(c);
        string sequencerMutation = view.Str("sequencerMutation", null);
        bool jokerActive = view.Truthy("jokerActive");
        UiValue mutation = mutations.FirstOrDefault(m => m.Str("Alias") == sequencerMutation);
        UiValue heldScannerBuffer = data.Get("heldScannerBuffer");
        bool hasBuffer = heldScannerBuffer is { IsNull: false };
        string scannerSequence = hasBuffer && mutation != null ? heldScannerBuffer.Str(mutation.Str("Alias"), null) : null;

        var images = new UiNode(UiKind.Section) { Key = "sequences", Title = "Sequences", Height = "214px", Scrollable = mutations.Count > 8 };
        var grid = new UiNode(UiKind.Stack) { Key = "images", Wrap = true };
        foreach (UiValue m in mutations)
        {
            string alias = m.Str("Alias");
            grid.With(new UiNode(UiKind.StackItem) { Key = "image:" + alias }.With(GenomeImage("genome:" + alias, m.Str("Image"), alias == sequencerMutation,
                hasBuffer && heldScannerBuffer.Truthy(alias), () =>
                {
                    c.Act("set_view", ("sequencerMutation", alias));
                    c.Act("check_discovery", ("alias", alias));
                })));
        }
        images.With(grid);

        var result = new UiNode(UiKind.Fragment).With(new UiNode(UiKind.Stack) { Key = "top" }.With(
            new UiNode(UiKind.StackItem) { Key = "list", Width = mutations.Count <= 8 ? "154px" : "174px" }.With(images),
            new UiNode(UiKind.StackItem) { Key = "info", Grow = 1 }.With(
                new UiNode(UiKind.Section) { Key = "sequence-info", Title = "Sequence Info" }.With(MutationInfo(c, mutation)))));

        double subjectStatus = data.Num("subjectStatus", -1);
        if (subjectStatus == SUBJECT_DEAD)
            return result.With(new UiNode(UiKind.Section) { Key = "dead", Color = "bad" }.With(new UiNode(UiKind.Box) { Color = "bad", Text = "Genetic sequence corrupted. Subject diagnostic report: DECEASED." }));
        if (data.Truthy("isMonkey") && mutation?.Str("Name", null) != "Monkified")
            return result.With(new UiNode(UiKind.Section) { Key = "monkey", Color = "bad" }.With(new UiNode(UiKind.Box) { Color = "bad", Text = "Genetic sequence corrupted. Subject diagnostic report: MONKEY." }));
        if (subjectStatus == SUBJECT_TRANSFORMING)
            return result.With(new UiNode(UiKind.Section) { Key = "transforming", Color = "bad" }.With(new UiNode(UiKind.Box) { Color = "bad", Text = "Genetic sequence corrupted. Subject diagnostic report: TRANSFORMING." }));

        UiNode jokerButtons;
        if (!data.Truthy("isJokerReady"))
            jokerButtons = new UiNode(UiKind.Box) { Key = "joker-cd", Color = "label", Text = "Joker on cooldown (" + TguiFormat.JsNumber(data.Num("jokerSeconds")) + "s)" };
        else if (jokerActive)
            jokerButtons = new UiNode(UiKind.Fragment).With(
                new UiNode(UiKind.Box) { Key = "joker-hint", Color = "label", Text = "Click on a gene to reveal it." },
                new UiNode(UiKind.Button) { Key = "joker-cancel", Text = "Cancel Joker", OnClick = () => c.Act("set_view", ("jokerActive", "")) });
        else
            jokerButtons = new UiNode(UiKind.Button) { Key = "joker", Icon = "crown", Color = "purple", Text = "Use Joker", OnClick = () => c.Act("set_view", ("jokerActive", "1")) };
        result.With(new UiNode(UiKind.Section) { Key = "genome-sequencer", Title = "Genome Sequencer™", Buttons = jokerButtons }.With(GenomeSequencer(c, mutation)));
        if (!string.IsNullOrEmpty(scannerSequence))
            result.With(new UiNode(UiKind.Section) { Key = "held-scanner", Title = "Held Scanner" }.With(GenomeSequencerReadOnly(scannerSequence)));
        return result;
    }

    // ---- MutationInfo.jsx ----

    // tg: MutationInfo.jsx:27  isSameMutation
    private static bool IsSameMutation(UiValue a, UiValue b) => a.Str("Alias") == b.Str("Alias") && a.Str("AppliedChromo") == b.Str("AppliedChromo");

    // tg: MutationInfo.jsx:31  ChromosomeInfo
    private static UiNode ChromosomeInfo(TguiContext c, UiValue mutation, bool disabled)
    {
        double canChromo = mutation.Num("CanChromo", -1);
        if (canChromo == CHROMOSOME_NEVER)
            return new UiNode(UiKind.Box) { Key = "chromo", Color = "label", Text = "No compatible chromosomes" };
        if (canChromo == CHROMOSOME_NONE)
        {
            if (disabled)
                return new UiNode(UiKind.Box) { Key = "chromo", Color = "label", Text = "No chromosome applied." };
            List<string> valid = mutation.Arr("ValidStoredChromos").Select(v => v.AsStr()).ToList();
            string reference = mutation.Str("ByondRef");
            return new UiNode(UiKind.Fragment).With(
                new UiNode(UiKind.Dropdown)
                {
                    Key = "chromo:" + reference,
                    Width = "240px",
                    Options = valid,
                    Disabled = valid.Count == 0,
                    TextValue = valid.Count == 0 ? "No Suitable Chromosomes" : "Select a chromosome",
                    OnSelected = value => c.Act("apply_chromo", ("chromo", value), ("mutref", reference)),
                },
                new UiNode(UiKind.Box) { Key = "compatible", Color = "label", Text = "Compatible with: " + mutation.Str("ValidChromos") });
        }
        if (canChromo == CHROMOSOME_USED)
            return new UiNode(UiKind.Box) { Key = "chromo", Color = "label", Text = "Applied chromosome: " + mutation.Str("AppliedChromo") };
        return null;
    }

    // tg: MutationInfo.jsx:72  MutationCombiner
    private static UiNode MutationCombiner(TguiContext c, IReadOnlyList<UiValue> mutations, UiValue source)
    {
        string BrefFromName(string name) => mutations.FirstOrDefault(m => m.Str("Name") == name)?.Str("ByondRef", null);
        return new UiNode(UiKind.Dropdown)
        {
            Key = "combine:" + source.Str("ByondRef"),
            Width = "240px",
            Options = mutations.Select(m => m.Str("Name")).ToList(),
            Disabled = mutations.Count == 0,
            TextValue = "Combine mutations",
            OnSelected = value => c.Act("combine_" + source.Str("Source"), ("firstref", BrefFromName(value)), ("secondref", source.Str("ByondRef"))),
        };
    }

    // tg: MutationInfo.jsx:96
    private static UiNode MutationInfo(TguiContext c, UiValue mutation)
    {
        UiValue data = c.Data;
        if (mutation == null)
            return new UiNode(UiKind.Box) { Key = "nothing", Color = "label", Text = "Nothing to show." };
        string source = mutation.Str("Source");
        if (source == "occupant" && !mutation.Truthy("Discovered"))
            return new UiNode(UiKind.LabeledList).With(new UiNode(UiKind.LabeledItem) { Key = "name", Label = "Name", Text = mutation.Str("Alias") });

        IReadOnlyList<UiValue> diskMutations = Storage(c, "disk");
        IReadOnlyList<UiValue> mutationStorage = Storage(c, "console");
        IReadOnlyList<UiValue> advInjectors = Storage(c, "injector");
        bool savedToConsole = mutationStorage.Any(x => IsSameMutation(x, mutation));
        bool savedToDisk = diskMutations.Any(x => IsSameMutation(x, mutation));
        // filter(uniqBy([...diskMutations, ...mutationStorage], Name), x => x.Name !== mutation.Name)
        List<UiValue> combinedMutations = diskMutations.Concat(mutationStorage).GroupBy(m => m.Str("Name")).Select(g => g.First())
            .Where(x => x.Str("Name") != mutation.Str("Name")).ToList();
        bool active = mutation.Truthy("Active");
        double mutClass = mutation.Num("Class", -1);
        string reference = mutation.Str("ByondRef");

        var info = new UiNode(UiKind.Fragment).With(
            new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Key = "name", Label = "Name" }.With(new UiNode(UiKind.Box) { Key = "name", Color = MutColor(mutation.Num("Quality")), Text = mutation.Str("Name") }),
                new UiNode(UiKind.LabeledItem) { Key = "desc", Label = "Description", Text = mutation.Str("Description") },
                new UiNode(UiKind.LabeledItem) { Key = "instability", Label = "Instability", Text = TguiFormat.JsNumber(mutation.Num("Instability")) }),
            new UiNode(UiKind.Divider) { Key = "div1" });

        var stack = new UiNode(UiKind.Stack) { Key = "actions", Vertical = true };
        // tg: MutationInfo.jsx:145  the combiner of disk and console mutations
        if (source == "disk" || source == "console")
            stack.With(new UiNode(UiKind.StackItem) { Key = "combiner" }.With(MutationCombiner(c, combinedMutations, mutation)));
        if (source == "occupant" || source == "disk" || source == "console")
        {
            bool injectorBlocked = !data.Truthy("isInjectorReady") || !active || mutClass == MUT_OTHER;
            stack.With(new UiNode(UiKind.StackItem) { Key = "injectors" }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Key = "adv" }.With(new UiNode(UiKind.Dropdown)
                {
                    Key = "advinj:" + reference,
                    Width = "240px",
                    Options = advInjectors.Select(i => i.Str("name")).ToList(),
                    Disabled = advInjectors.Count == 0 || !active,
                    TextValue = "Add to advanced injector",
                    OnSelected = value => c.Act("add_advinj_mut", ("mutref", reference), ("advinj", value), ("source", source)),
                }),
                new UiNode(UiKind.StackItem) { Key = "print" }.With(new UiNode(UiKind.Stack).With(
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "activator",
                        Icon = "syringe",
                        Disabled = injectorBlocked,
                        Text = "Print Activator",
                        OnClick = () => c.Act("print_injector", ("mutref", reference), ("is_activator", 1), ("source", source)),
                    }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "mutator",
                        Icon = "syringe",
                        Disabled = injectorBlocked,
                        Text = "Print Mutator",
                        OnClick = () => c.Act("print_injector", ("mutref", reference), ("is_activator", 0), ("source", source)),
                    }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "crispr",
                        Icon = "syringe",
                        Disabled = !active || !data.Truthy("isCrisprReady"),
                        Text = "CRISPR [" + TguiFormat.JsNumber(data.Num("crisprCharges")) + "]",
                        OnClick = () => c.Act("crispr", ("mutref", reference), ("source", source)),
                    }))))));
        }

        var saves = new UiNode(UiKind.Stack) { Key = "saves" };
        if (source == "disk" || source == "occupant")
        {
            saves.With(new UiNode(UiKind.StackItem) { Key = "save-console" }.With(new UiNode(UiKind.Button)
            {
                Key = "save-console",
                Icon = "save",
                Disabled = savedToConsole || !active || mutClass == MUT_OTHER,
                Text = "Save to Console",
                OnClick = () => c.Act("save_console", ("mutref", reference), ("source", source)),
            }));
        }
        if (source == "console" || source == "occupant")
        {
            saves.With(new UiNode(UiKind.StackItem) { Key = "save-disk" }.With(new UiNode(UiKind.Button)
            {
                Key = "save-disk",
                Icon = "save",
                Disabled = savedToDisk || !data.Truthy("hasDisk") || data.Num("diskCapacity") <= 0 || data.Truthy("diskReadOnly") || !active || mutClass == MUT_OTHER,
                Text = "Save to Disk",
                OnClick = () => c.Act("save_disk", ("mutref", reference), ("source", source)),
            }));
        }
        if (source == "console" || source == "disk" || source == "injector")
        {
            saves.With(new UiNode(UiKind.StackItem) { Key = "delete" }.With(new UiNode(UiKind.Button)
            {
                Key = "delete",
                Icon = "times",
                Color = "red",
                Text = "Delete from " + source,
                OnClick = () => c.Act("delete_" + source + "_mut", ("mutref", reference)),
            }));
        }
        if (mutClass == MUT_EXTRA || (mutation.Truthy("Scrambled") && source == "occupant"))
        {
            saves.With(new UiNode(UiKind.StackItem) { Key = "nullify" }.With(new UiNode(UiKind.Button)
            {
                Key = "nullify",
                Text = "Nullify",
                OnClick = () => c.Act("nullify", ("mutref", reference)),
            }));
        }
        stack.With(new UiNode(UiKind.StackItem) { Key = "saves" }.With(
            saves,
            new UiNode(UiKind.Divider) { Key = "div2" },
            ChromosomeInfo(c, mutation, source != "occupant")));
        return info.With(stack);
    }

    // ---- GeneticMakeupInfo.jsx ----

    // tg: GeneticMakeupInfo.jsx:3
    private static UiNode GeneticMakeupInfo(UiValue makeup)
    {
        string Or(string key) => makeup?.Str(key, null) is { Length: > 0 } v ? v : "None";
        return new UiNode(UiKind.Section) { Key = "makeup-info", Title = "Enzyme Information" }.With(new UiNode(UiKind.LabeledList).With(
            new UiNode(UiKind.LabeledItem) { Key = "name", Label = "Name", Text = Or("name") },
            new UiNode(UiKind.LabeledItem) { Key = "blood", Label = "Blood Type", Text = Or("blood_type") },
            new UiNode(UiKind.LabeledItem) { Key = "ue", Label = "Unique Enzyme", Text = Or("UE") },
            new UiNode(UiKind.LabeledItem) { Key = "ui", Label = "Unique Identifier", Text = Or("UI") },
            new UiNode(UiKind.LabeledItem) { Key = "uf", Label = "Unique Features", Text = Or("UF") }));
    }

    // ---- DnaConsoleEnzymes.jsx ----

    // tg: DnaConsoleEnzymes.jsx:17  GeneticMakeupBufferInfo
    private static UiNode MakeupBufferInfo(TguiContext c, int index, UiValue makeup)
    {
        UiValue data = c.Data;
        bool viable = data.Truthy("isViableSubject");
        // Type of the action for applying makeup
        string applyAction = viable ? "makeup_apply" : "makeup_delay";
        if (makeup == null || makeup.IsNull)
            return new UiNode(UiKind.Box) { Key = "empty", Color = "average", Text = "No stored subject data." };
        UiNode Row(string key, string label, string type) => new UiNode(UiKind.LabeledItem) { Key = key, Label = label }.With(
            new UiNode(UiKind.Button)
            {
                Key = "print:" + type,
                Icon = "syringe",
                Disabled = !data.Truthy("isInjectorReady"),
                Text = "Print",
                OnClick = () => c.Act("makeup_injector", ("index", index), ("type", type)),
            },
            new UiNode(UiKind.Button)
            {
                Key = "transfer:" + type,
                Icon = "exchange-alt",
                Text = "Transfer" + (!viable ? " (Delayed)" : ""),
                OnClick = () => c.Act(applyAction, ("index", index), ("type", type)),
            });
        return new UiNode(UiKind.Fragment).With(
            GeneticMakeupInfo(makeup),
            new UiNode(UiKind.Divider) { Key = "div" },
            new UiNode(UiKind.Box) { Key = "actions-title", Bold = true, Color = "label", Text = "Makeup Actions" },
            new UiNode(UiKind.LabeledList).With(
                Row("ue", "Enzymes", "ue"),
                Row("ui", "Identity", "ui"),
                Row("uf", "Features", "uf"),
                Row("mixed", "Full Makeup", "mixed"),
                new UiNode(UiKind.LabeledItem) { Key = "export" }.With(new UiNode(UiKind.Button)
                {
                    Key = "export",
                    Icon = "save",
                    Disabled = !data.Truthy("hasDisk") || data.Truthy("diskReadOnly"),
                    Text = "Export To Disk",
                    OnClick = () => c.Act("save_makeup_disk", ("index", index)),
                })));
    }

    // tg: DnaConsoleEnzymes.jsx:140  GeneticMakeupBuffers
    private static UiNode MakeupBuffers(TguiContext c)
    {
        UiValue data = c.Data;
        bool hasDisk = data.Truthy("hasDisk");
        bool diskHasMakeup = data.Truthy("diskHasMakeup");
        double capacity = data.Get("makeupCapacity") is { IsNull: false } cap ? cap.AsNum(3) : 3;
        UiValue storage = data.Get("makeupStorage");
        var section = new UiNode(UiKind.Section) { Key = "makeup-buffers", Title = "Genetic Makeup Buffers" };
        // tg: DnaConsoleEnzymes.jsx:200  <Dimmer>: the transfer's cooldown
        double cooldown = data.Num("geneticMakeupCooldown");
        if (cooldown != 0)
            section.With(new UiNode(UiKind.Modal) { Key = "makeup-cooldown" }.With(
                new UiNode(UiKind.Box) { Key = "cd-text", FontSize = "14px", TextAlign = "center", Icon = "spinner", Text = " Genetic makeup transfer ready in..." },
                new UiNode(UiKind.Box) { Key = "cd-time", FontSize = "14px", TextAlign = "center", Text = TguiFormat.JsNumber(cooldown) + "s" }));
        for (int i = 1; i <= capacity; i++)
        {
            int index = i;
            UiValue makeup = storage?.Get(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            bool has = makeup is { IsNull: false };
            string title = has ? (makeup.Str("label", null) is { Length: > 0 } label ? label : makeup.Str("name")) : "Slot " + i;
            var buttons = new UiNode(UiKind.Fragment);
            if (hasDisk && diskHasMakeup)
                buttons.With(new UiNode(UiKind.Button) { Key = "import", Disabled = !hasDisk || !diskHasMakeup, Text = "Import from disk", OnClick = () => c.Act("load_makeup_disk", ("index", index)) });
            buttons.With(
                new UiNode(UiKind.Button) { Key = "save", Disabled = !data.Truthy("isViableSubject"), Text = "Save", OnClick = () => c.Act("save_makeup_console", ("index", index)) },
                new UiNode(UiKind.Button) { Key = "delete", Icon = "times", Color = "red", Disabled = !has, OnClick = () => c.Act("del_makeup_console", ("index", index)) });
            section.With(new UiNode(UiKind.Collapsible) { Key = "slot:" + i, Title = title, Buttons = buttons }.With(MakeupBufferInfo(c, index, has ? makeup : null)));
        }
        return section;
    }

    // tg: DnaConsoleEnzymes.jsx:213  PulseEmitterProbs
    private static UiNode PulseEmitterProbs(TguiContext c)
    {
        UiValue data = c.Data;
        double stdDevStr = data.Num("stdDevStr");
        return new UiNode(UiKind.Section) { Key = "probs", Title = "Probabilities" }.With(new UiNode(UiKind.LabeledList).With(
            new UiNode(UiKind.LabeledItem) { Key = "acc", Label = "Accuracy", TextAlign = "right", Text = data.Str("stdDevAcc") },
            new UiNode(UiKind.LabeledItem) { Key = "p1", Label = "P(±" + TguiFormat.JsNumber(stdDevStr) + ")", TextAlign = "right", Text = "68 %" },
            new UiNode(UiKind.LabeledItem) { Key = "p2", Label = "P(±" + TguiFormat.JsNumber(stdDevStr * 2) + ")", TextAlign = "right", Text = "95 %" }));
    }

    // tg: DnaConsoleEnzymes.jsx:233  PulseBoard: the enzyme's characters in columns of three buttons
    private static UiNode PulseBoard(TguiContext c, string subjectBlock, string type, string name)
    {
        var blocks = new UiNode(UiKind.Stack) { Key = "blocks", Wrap = true };
        var buffer = new List<UiNode>();
        for (int i = 0; i < subjectBlock.Length; i++)
        {
            int index = i;
            buffer.Add(new UiNode(UiKind.Button)
            {
                Key = "pulse:" + i,
                Fluid = true,
                TextAlign = "center",
                Text = subjectBlock[i].ToString(),
                OnClick = () => c.Act("makeup_pulse", ("index", index + 1), ("type", type)),
            });
            if (buffer.Count >= 3)
            {
                blocks.With(new UiNode(UiKind.StackItem) { Key = "block:" + i, Width = "22px" }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(
                    buffer.Select(b => new UiNode(UiKind.StackItem) { Key = b.Key }.With(b)).ToArray())));
                buffer.Clear();
            }
        }
        return new UiNode(UiKind.Section) { Key = "pulse-board", Title = "Unique " + name }.With(blocks);
    }

    // tg: DnaConsoleEnzymes.jsx:273  PulseSettings
    private static UiNode PulseSettings(TguiContext c)
    {
        UiValue data = c.Data;
        UiNode Number(string key, double value, int max, string action) => new UiNode(UiKind.NumberInput)
        {
            Key = key,
            Width = "32px",
            Step = 1,
            StepPixelSize = 10,
            Value = value,
            MinValue = 1,
            MaxValue = max,
            OnChange = v => c.Act(action, ("val", v)),
        };
        return new UiNode(UiKind.Section) { Key = "emitter", Title = "Emitter Configuration" }.With(new UiNode(UiKind.LabeledList).With(
            new UiNode(UiKind.LabeledItem) { Key = "strength", Label = "Output level" }.With(Number("strength", data.Num("pulseStrength"), PULSE_STRENGTH_MAX, "set_pulse_strength")),
            new UiNode(UiKind.LabeledItem) { Key = "duration", Label = "Pulse duration" }.With(Number("duration", data.Num("pulseDuration"), PULSE_DURATION_MAX, "set_pulse_duration"))));
    }

    // tg: DnaConsoleEnzymes.jsx:318
    private static UiNode Enzymes(TguiContext c, string subjectBlock, string type, string name)
    {
        if (!c.Data.Truthy("isScannerConnected"))
            return new UiNode(UiKind.Section) { Key = "no-scanner", Color = "bad" }.With(new UiNode(UiKind.Box) { Color = "bad", Text = "DNA Scanner is not connected." });
        return new UiNode(UiKind.Fragment).With(
            new UiNode(UiKind.Stack) { Key = "pulse" }.With(
                new UiNode(UiKind.StackItem) { Key = "settings", Width = "155px" }.With(PulseSettings(c)),
                new UiNode(UiKind.StackItem) { Key = "probs", Width = "140px" }.With(PulseEmitterProbs(c)),
                new UiNode(UiKind.StackItem) { Key = "board", Grow = 1 }.With(PulseBoard(c, subjectBlock ?? "", type, name))),
            MakeupBuffers(c));
    }

    // ---- DnaConsoleStorage.jsx ----

    // tg: DnaConsoleStorage.jsx:25
    private static UiNode Storage(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue view = View(c);
        string storageMode = view.Str("storageMode");
        string consSubMode = view.Str("storageConsSubMode");
        string diskSubMode = view.Str("storageDiskSubMode");
        IReadOnlyList<UiValue> mutations = Storage(c, storageMode);
        var section = new UiNode(UiKind.Section) { Key = "storage", Fill = true, Title = "Storage", Buttons = StorageButtons(c) };
        if (storageMode == STORAGE_MODE_CONSOLE && consSubMode == STORAGE_CONS_SUBMODE_MUTATIONS)
            section.With(StorageMutations(c, mutations));
        if (storageMode == STORAGE_MODE_CONSOLE && consSubMode == STORAGE_CONS_SUBMODE_CHROMOSOMES)
            section.With(StorageChromosomes(c));
        if (storageMode == STORAGE_MODE_DISK && diskSubMode == STORAGE_DISK_SUBMODE_MUTATIONS)
            section.With(StorageMutations(c, mutations));
        if (storageMode == STORAGE_MODE_DISK && diskSubMode == STORAGE_DISK_SUBMODE_ENZYMES)
        {
            section.With(
                GeneticMakeupInfo(data.Get("diskMakeupBuffer")),
                new UiNode(UiKind.Button) { Key = "del-makeup", Icon = "times", Color = "red", Disabled = !data.Truthy("diskHasMakeup"), Text = "Delete", OnClick = () => c.Act("del_makeup_disk") });
        }
        if (storageMode == STORAGE_MODE_ADVINJ)
            section.With(AdvancedInjectors(c));
        return section;
    }

    // tg: DnaConsoleStorage.jsx:63
    private static UiNode AdvancedInjectors(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> advInjectors = Storage(c, "injector");
        var section = new UiNode(UiKind.Section) { Key = "adv-injectors", Fill = true, Title = "Advanced Injectors" };
        int index = 0;
        foreach (UiValue injector in advInjectors)
        {
            string name = injector.Str("name");
            section.With(new UiNode(UiKind.Collapsible)
            {
                Key = "injector:" + name,
                Title = name,
                Buttons = new UiNode(UiKind.Fragment).With(
                    new UiNode(UiKind.Button) { Key = "print", Icon = "syringe", Disabled = !data.Truthy("isInjectorReady"), Text = "Print", OnClick = () => c.Act("print_adv_inj", ("name", name)) },
                    new UiNode(UiKind.Button) { Key = "delete", Color = "red", Icon = "times", OnClick = () => c.Act("del_adv_inj", ("name", name)) }),
            }.With(StorageMutations(c, injector.Arr("mutations"), "advinj" + index)));
            index++;
        }
        // tg: DnaConsoleStorage.jsx:108  <Button.Input buttonText="Create new injector">: an input that commits the name
        section.With(new UiNode(UiKind.Input)
        {
            Key = "new-injector",
            MinWidth = "200px",
            Tooltip = "Create new injector",
            TextValue = "",
            Disabled = advInjectors.Count >= data.Num("maxAdvInjectors"),
            OnCommit = value => c.Act("new_adv_inj", ("name", value)),
        });
        return section;
    }

    // tg: DnaConsoleStorage.jsx:123
    private static UiNode StorageButtons(TguiContext c)
    {
        UiValue view = View(c);
        string storageMode = view.Str("storageMode");
        string consSubMode = view.Str("storageConsSubMode");
        string diskSubMode = view.Str("storageDiskSubMode");
        var buttons = new UiNode(UiKind.Fragment);
        if (storageMode == STORAGE_MODE_CONSOLE)
        {
            buttons.With(
                new UiNode(UiKind.Button) { Key = "cons-mutations", Selected = consSubMode == STORAGE_CONS_SUBMODE_MUTATIONS, Text = "Mutations", OnClick = () => c.Act("set_view", ("storageConsSubMode", STORAGE_CONS_SUBMODE_MUTATIONS)) },
                new UiNode(UiKind.Button) { Key = "cons-chromosomes", Selected = consSubMode == STORAGE_CONS_SUBMODE_CHROMOSOMES, Text = "Chromosomes", OnClick = () => c.Act("set_view", ("storageConsSubMode", STORAGE_CONS_SUBMODE_CHROMOSOMES)) });
        }
        if (storageMode == STORAGE_MODE_DISK)
        {
            buttons.With(
                new UiNode(UiKind.Button) { Key = "disk-mutations", Selected = diskSubMode == STORAGE_CONS_SUBMODE_MUTATIONS, Text = "Mutations", OnClick = () => c.Act("set_view", ("storageDiskSubMode", STORAGE_CONS_SUBMODE_MUTATIONS)) },
                new UiNode(UiKind.Button) { Key = "disk-enzymes", Selected = diskSubMode == STORAGE_DISK_SUBMODE_ENZYMES, Text = "Enzymes", OnClick = () => c.Act("set_view", ("storageDiskSubMode", STORAGE_DISK_SUBMODE_ENZYMES)) });
        }
        return buttons.With(
            new UiNode(UiKind.Button) { Key = "console", Text = "Console", Selected = storageMode == STORAGE_MODE_CONSOLE, OnClick = () => c.Act("set_view", ("storageMode", STORAGE_MODE_CONSOLE), ("storageConsSubMode", STORAGE_CONS_SUBMODE_MUTATIONS)) },
            new UiNode(UiKind.Button) { Key = "disk", Text = "Disk", Disabled = !c.Data.Truthy("hasDisk"), Selected = storageMode == STORAGE_MODE_DISK, OnClick = () => c.Act("set_view", ("storageMode", STORAGE_MODE_DISK), ("storageDiskSubMode", STORAGE_DISK_SUBMODE_MUTATIONS)) },
            new UiNode(UiKind.Button) { Key = "advinj", Text = "Adv. Injector", Selected = storageMode == STORAGE_MODE_ADVINJ, OnClick = () => c.Act("set_view", ("storageMode", STORAGE_MODE_ADVINJ)) });
    }

    // tg: DnaConsoleStorage.jsx:215
    private static UiNode StorageChromosomes(TguiContext c)
    {
        IReadOnlyList<UiValue> chromos = c.Data.Arr("chromoStorage");
        List<UiValue> uniqueChromos = chromos.GroupBy(x => x.Str("Name")).Select(g => g.First()).ToList();
        string chromoName = View(c).Str("storageChromoName", null);
        UiValue chromo = chromos.FirstOrDefault(x => x.Str("Name") == chromoName);
        var tabs = new UiNode(UiKind.Tabs) { Key = "chromo-tabs", Vertical = true };
        foreach (UiValue x in uniqueChromos)
        {
            string name = x.Str("Name");
            tabs.With(new UiNode(UiKind.Tab)
            {
                Key = "chromo:" + x.Str("Index"),
                Candystripe = true,
                Selected = name == chromoName,
                Text = name,
                OnClick = () => c.Act("set_view", ("storageChromoName", name)),
            });
        }
        var info = new UiNode(UiKind.Section) { Key = "chromo-info", Title = "Chromosome Info" };
        if (chromo == null)
            info.With(new UiNode(UiKind.Box) { Key = "nothing", Color = "label", Text = "Nothing to show." });
        else
        {
            string name = chromo.Str("Name");
            info.With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Key = "name", Label = "Name", Text = name },
                    new UiNode(UiKind.LabeledItem) { Key = "desc", Label = "Description", Text = chromo.Str("Description") },
                    new UiNode(UiKind.LabeledItem) { Key = "amount", Label = "Amount", Text = chromos.Count(x => x.Str("Name") == name).ToString(System.Globalization.CultureInfo.InvariantCulture) }),
                new UiNode(UiKind.Button) { Key = "eject", Icon = "eject", Text = "Eject Chromosome", OnClick = () => c.Act("eject_chromo", ("chromo", name)) });
        }
        return new UiNode(UiKind.Stack) { Key = "chromosomes", Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "list", Width = "140px" }.With(new UiNode(UiKind.Section) { Key = "chromo-list", Fill = true, Scrollable = true }.With(tabs)),
            new UiNode(UiKind.StackItem) { Key = "div" }.With(new UiNode(UiKind.Divider)),
            new UiNode(UiKind.StackItem) { Key = "info", Grow = 1 }.With(info));
    }

    // tg: DnaConsoleStorage.jsx:278
    private static UiNode StorageMutations(TguiContext c, IReadOnlyList<UiValue> mutations, string customMode = "")
    {
        UiValue view = View(c);
        string mode = view.Str("storageMode") + customMode;
        string stateKey = "storage" + mode + "MutationRef";
        string mutationRef = view.Str(stateKey, null);
        UiValue mutation = mutations.FirstOrDefault(m => m.Str("ByondRef") == mutationRef);
        // If no mutation is selected but there are stored mutations, pick the first
        // mutation and set that as the currently showed one.
        if (mutation == null && mutations.Count > 0)
        {
            mutation = mutations[0];
            mutationRef = mutation.Str("ByondRef");
        }
        var tabs = new UiNode(UiKind.Tabs) { Key = "mutation-tabs", Vertical = true };
        foreach (UiValue m in mutations)
        {
            string reference = m.Str("ByondRef");
            tabs.With(new UiNode(UiKind.Tab)
            {
                Key = "mut:" + reference,
                Candystripe = true,
                Selected = reference == mutationRef,
                Text = m.Str("Name"),
                OnClick = () => c.Act("set_view", (stateKey, reference)),
            });
        }
        return new UiNode(UiKind.Stack) { Key = "mutations:" + mode, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "list", Width = "140px" }.With(new UiNode(UiKind.Section) { Key = "mut-list", Fill = true, Scrollable = true }.With(tabs)),
            new UiNode(UiKind.StackItem) { Key = "div" }.With(new UiNode(UiKind.Divider)),
            new UiNode(UiKind.StackItem) { Key = "info", Grow = 1 }.With(
                new UiNode(UiKind.Section) { Key = "mutation-info", Title = "Mutation Info" }.With(MutationInfo(c, mutation))));
    }
}
