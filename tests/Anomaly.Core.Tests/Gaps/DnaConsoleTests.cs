using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.M4bAudio;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package P3 (D327): the DNA scanner and console. The console finds the scanner beside it, the window reads the subject's
/// genes; a gene pulse steps a gene, gives genetic damage and a completed sequence activates and discovers its mutation; the
/// console prints activators and mutators, stores and combines mutations, applies a chromosome, saves a genetic makeup and prints
/// its timed injector (the temporary transformation); the scramble and the enzyme pulse; every DNA machine and item type made
/// and destroyed with no runtime.
/// </summary>
public class DnaConsoleTests
{
    private const int ScannerX = 4, ScannerY = 4;

    private static readonly Type[] AllTypes =
    {
        typeof(obj_machinery_dna_scannernew), typeof(obj_machinery_computer_dna_console), typeof(obj_item_disk_data), typeof(obj_item_disk_data_debug),
        typeof(obj_item_chromosome_stabilizer), typeof(obj_item_chromosome_synchronizer), typeof(obj_item_chromosome_power), typeof(obj_item_chromosome_energy),
        typeof(obj_item_dnainjector_activator), typeof(obj_item_dnainjector_timed), typeof(obj_item_sequence_scanner),
    };

    private sealed record Lab(AudioRoom R, Feed Feed, mob_living_carbon_human Gene, mob_living_carbon_human Subject, obj_machinery_dna_scannernew Scanner,
        obj_machinery_computer_dna_console Console);

    private static string Diag(Lab l) => "chat: " + string.Join(" | ", l.Feed.Chat.Select(c => c.Html)) + " runtimes: " + string.Join("\n", l.R.Room.RuntimeText);

    private static Lab Room(ulong seed, bool subjectInside = true)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        r.In(() => (get_area(locate(ScannerX, ScannerY, 1)) as area).requires_power = FALSE); // the toy room has no APC
        var scanner = r.In(() => Dm.@new<obj_machinery_dna_scannernew>(locate(ScannerX, ScannerY, 1)));
        var console = r.In(() => Dm.@new<obj_machinery_computer_dna_console>(locate(ScannerX + 1, ScannerY, 1)));
        mob_living_carbon_human gene = r.Join(ScannerX + 2, ScannerY, "Gene");
        mob_living_carbon_human subject = r.Dummy(ScannerX - 1, ScannerY);
        r.Step(Second);
        r.In(() =>
        {
            ADD_TRAIT(gene, TRAIT_ADVANCEDTOOLUSER, "test");
            console.stored_research = Dm.@new<datum_techweb>(); // the toy room has no research servers
            console.injector_ready = 0; // the console's start-up cooldowns
            console.scramble_ready = 0;
            console.joker_ready = 0;
            console.enzyme_copy_timer = 0;
        });
        if (subjectInside)
        {
            r.In(() =>
            {
                Waiting.NoSleep(scanner.open_machine());
                Waiting.NoSleep(scanner.close_machine(subject));
            });
            r.Step(2);
        }
        return new Lab(r, feed, gene, subject, scanner, console);
    }

    private static JsonElement Open(Lab l, out string windowId)
    {
        Take(l.Gene);
        Click(l.R, l.Gene, l.Console);
        return LastUpdate(Take(l.Gene), out windowId).GetProperty("data");
    }

    private static string Ref(Lab l, datum d) => l.R.In(() => Dm.str(REF(d)));

    [Fact]
    public void The_console_finds_its_scanner_and_the_window_reads_the_subjects_genes()
    {
        Lab l = Room(9201);
        Assert.Same(l.Scanner, l.R.In(() => l.Console.connected_scanner));
        Assert.Same(l.Console, l.R.In(() => l.Scanner.linked_console));
        Assert.Same(l.Scanner, l.R.In(() => l.Subject.loc));
        Assert.Equal("scanner_occupied", l.R.In(() => l.Scanner.icon_state));
        JsonElement data = Open(l, out string window);
        Assert.Equal(1, data.GetProperty("isScannerConnected").GetInt32());
        Assert.Equal(1, data.GetProperty("isViableSubject").GetInt32());
        Assert.Equal(l.R.In(() => l.Subject.name), data.GetProperty("subjectName").GetString());
        Assert.Equal(l.R.In(() => Dm.str(l.Subject.dna.unique_identity)), data.GetProperty("subjectUNI").GetString());
        JsonElement occupant = data.GetProperty("storage").GetProperty("occupant");
        Assert.Equal(l.R.In(() => l.Subject.dna.mutation_index.len), occupant.GetArrayLength());
        JsonElement first = occupant[0];
        Assert.StartsWith("Mutation ", first.GetProperty("Alias").GetString());
        Assert.Equal(0, first.GetProperty("Discovered").GetInt32()); // nothing discovered yet: no name
        Assert.False(first.TryGetProperty("Name", out _));
        Assert.Equal("dna_undiscovered.gif", first.GetProperty("Image").GetString());
        Assert.Equal("storage", data.GetProperty("view").GetProperty("consoleMode").GetString());

        // the window's mode buttons set the view state
        Act(l.R, l.Gene, window, "set_view", ("consoleMode", "sequencer"));
        Assert.Equal("sequencer", Data(l.R, l.Gene, out _).GetProperty("view").GetProperty("consoleMode").GetString());

        // the door: the bolts keep it shut, the button opens it and lets the subject out
        Act(l.R, l.Gene, window, "toggle_lock");
        Assert.Equal(1f, l.R.In(() => l.Scanner.locked));
        Act(l.R, l.Gene, window, "toggle_door");
        Assert.Same(l.Scanner, l.R.In(() => l.Subject.loc));
        Act(l.R, l.Gene, window, "toggle_lock");
        Act(l.R, l.Gene, window, "toggle_door");
        Assert.NotSame(l.Scanner, l.R.In(() => l.Subject.loc));
        Assert.Equal("scanner_open", l.R.In(() => l.Scanner.icon_state));
        Assert.Null(l.R.In(() => l.Console.scanner_occupant));
        l.R.AssertNoRuntimes();
    }

    [Fact]
    public void Pulsing_the_genes_of_a_sequence_activates_and_discovers_its_mutation()
    {
        Lab l = Room(9202);
        // the subject's first dormant mutation, one gene short of its sequence
        (Type path, string truth) = l.R.In(() =>
        {
            foreach (object p in Dm.each(l.Subject.dna.mutation_index))
            {
                if (l.Subject.dna.get_mutation(p) == null && (Type)p != typeof(datum_mutation_race)) // Monkified would make a monkey of the subject
                    return ((Type)p, Dm.str(GET_SEQUENCE(p)));
            }
            return (null, null);
        });
        Assert.NotNull(path);
        string alias = l.R.In(() => Dm.str(((datum_mutation)GET_INITIALIZED_MUTATION(path)).alias));
        // gene 1 one step before the truth: the next gene (A, T, C, G) is the right one
        string[] letters = { "A", "T", "C", "G" };
        string before = letters[(Array.IndexOf(letters, truth[..1]) + 3) % 4];
        l.R.In(() => l.Subject.dna.mutation_index[path] = before + truth[1..]);
        JsonElement data = Open(l, out string window);
        Act(l.R, l.Gene, window, "pulse_gene", ("pos", 1), ("pulseAction", 1), ("alias", alias));
        Assert.Equal(truth, l.R.In(() => Dm.str(l.Subject.dna.mutation_index[path])));
        Assert.NotNull(l.R.In(() => l.Subject.dna.get_mutation(path))); // domutcheck activated it
        Assert.True(l.R.In(() => Dm.@in(path, l.Console.stored_research.discovered_mutations)), Diag(l));
        Assert.True(l.Feed.Any("Successfully discovered"), Diag(l));
        var damage = l.R.In(() => l.Subject.has_status_effect(typeof(datum_status_effect_genetic_damage)) as datum_status_effect_genetic_damage);
        Assert.NotNull(damage);
        Assert.Equal(1f, l.R.In(() => damage.total_damage)); // GENETIC_DAMAGE_STRENGTH_MULTIPLIER / the laser's tier 1

        // the discovered mutation shows its name and picture now
        JsonElement after = Data(l.R, l.Gene, out _);
        Act(l.R, l.Gene, window, "set_view", ("consoleMode", "sequencer"));
        after = Data(l.R, l.Gene, out _);
        JsonElement entry = after.GetProperty("storage").GetProperty("occupant").EnumerateArray().Single(e => e.GetProperty("Alias").GetString() == alias);
        Assert.Equal(l.R.In(() => ((datum_mutation)GET_INITIALIZED_MUTATION(path)).name), entry.GetProperty("Name").GetString());
        Assert.Equal("dna_discovered.gif", entry.GetProperty("Image").GetString());
        Assert.Equal(1, entry.GetProperty("Active").GetInt32());

        // Ctrl+click sets a gene to X, in the sequence and the default sequence
        Act(l.R, l.Gene, window, "pulse_gene", ("pos", 2), ("pulseAction", 0), ("alias", alias));
        Assert.Equal("X", l.R.In(() => Dm.str(l.Subject.dna.mutation_index[path]))[1..2]);
        Assert.Equal("X", l.R.In(() => Dm.str(l.Subject.dna.default_mutation_genes[path]))[1..2]);
        l.R.AssertNoRuntimes();
    }

    [Fact]
    public void The_console_prints_injectors_and_stores_combines_and_chromosomes_mutations()
    {
        Lab l = Room(9203);
        datum_mutation strong = l.R.In(() => (datum_mutation)Waiting.NoSleep(Dm.@new<datum_mutation_strong>().make_copy()));
        datum_mutation radioactive = l.R.In(() => (datum_mutation)Waiting.NoSleep(Dm.@new<datum_mutation_radioactive>().make_copy()));
        l.R.In(() =>
        {
            l.Console.stored_mutations.Add(strong);
            l.Console.stored_mutations.Add(radioactive);
        });
        JsonElement data = Open(l, out string window);
        Assert.Equal(2, data.GetProperty("storage").GetProperty("console").GetArrayLength());

        // combining strength with radioactivity makes the hulk and discovers it
        Act(l.R, l.Gene, window, "combine_console", ("firstref", Ref(l, strong)), ("secondref", Ref(l, radioactive)));
        Assert.Equal(3f, l.R.In(() => l.Console.stored_mutations.len));
        Assert.IsType<datum_mutation_hulk>(l.R.In(() => l.Console.stored_mutations[3]));
        Assert.True(l.R.In(() => Dm.@in(typeof(datum_mutation_hulk), l.Console.stored_research.discovered_mutations)));

        // a mutator of the stored strength: an injector that forces the mutation
        Act(l.R, l.Gene, window, "print_injector", ("mutref", Ref(l, strong)), ("is_activator", 0), ("source", "console"));
        var mutator = l.R.In(() => Dm.locate<obj_item_dnainjector_activator>(l.Console.loc));
        Assert.NotNull(mutator);
        Assert.Equal("Strength mutator", l.R.In(() => mutator.name));
        Assert.Equal(1f, l.R.In(() => mutator.force_mutate));
        Assert.True(l.R.In(() => l.Console.injector_ready > world.time)); // the injector's cooldown
        mob_living_carbon_human patient = l.R.Dummy(2, 2);
        Assert.True(l.R.In(() => Dm.truthy(Waiting.NoSleep(mutator.inject(patient, l.Gene)))));
        Assert.NotNull(l.R.In(() => patient.dna.get_mutation(typeof(datum_mutation_strong))));
        Assert.True(l.R.In(() => HAS_TRAIT(patient, TRAIT_STRENGTH)));

        // the disk: inserted, a mutation saved to it, read back in the window
        var disk = l.R.In(() => Dm.@new<obj_item_disk_data>(l.Console));
        l.R.In(() => l.Console.diskette = disk);
        Act(l.R, l.Gene, window, "save_disk", ("mutref", Ref(l, radioactive)), ("source", "console"));
        Assert.Equal(1f, l.R.In(() => disk.mutations.len));
        JsonElement withDisk = Data(l.R, l.Gene, out _);
        Assert.Equal(1, withDisk.GetProperty("hasDisk").GetInt32());
        Assert.Equal(9, withDisk.GetProperty("diskCapacity").GetInt32());

        // a power chromosome on the subject's active mutation: its power grows by half
        datum_mutation active = l.R.In(() =>
        {
            Waiting.NoSleep(l.Subject.dna.add_mutation(typeof(datum_mutation_glow), MUTATION_SOURCE_ACTIVATED));
            return (datum_mutation)l.Subject.dna.get_mutation(typeof(datum_mutation_glow));
        });
        var chromosome = l.R.In(() => Dm.@new<obj_item_chromosome_power>(l.Gene.loc));
        l.R.In(() => Waiting.NoSleep(l.Console.item_interaction(l.Gene, chromosome)));
        Assert.True(l.R.In(() => Dm.@in(chromosome, l.Console.stored_chromosomes)));
        Act(l.R, l.Gene, window, "apply_chromo", ("mutref", Ref(l, active)), ("chromo", "power chromosome"));
        Assert.Equal(1.5f, l.R.In(() => active.power_coeff));
        Assert.Equal((float)CHROMOSOME_USED, l.R.In(() => active.can_chromosome));
        Assert.Equal("power chromosome", l.R.In(() => Dm.str(active.chromosome_name)));
        Assert.True(l.R.In(() => QDELETED(chromosome)));
        Assert.False(l.R.In(() => Dm.@in(chromosome, l.Console.stored_chromosomes)));
        l.R.AssertNoRuntimes();
    }

    [Fact]
    public void A_saved_genetic_makeup_prints_a_timed_injector_that_lends_its_name_for_a_minute()
    {
        Lab l = Room(9204);
        JsonElement data = Open(l, out string window);
        string subjectName = l.R.In(() => Dm.str(l.Subject.real_name));
        Act(l.R, l.Gene, window, "save_makeup_console", ("index", 1));
        DmList slot = l.R.In(() => l.Console.genetic_makeup_buffer[1] as DmList);
        Assert.NotNull(slot);
        Assert.Equal(subjectName, l.R.In(() => Dm.str(slot["name"])));
        Assert.Equal(l.R.In(() => Dm.str(l.Subject.dna.unique_enzymes)), l.R.In(() => Dm.str(slot["UE"])));
        JsonElement saved = Data(l.R, l.Gene, out _);
        Assert.Equal("Slot 1:" + subjectName, saved.GetProperty("makeupStorage").GetProperty("1").GetProperty("label").GetString());

        Act(l.R, l.Gene, window, "makeup_injector", ("index", 1), ("type", "ue"));
        var injector = l.R.In(() => Dm.locate<obj_item_dnainjector_timed>(l.Console.loc));
        Assert.NotNull(injector);
        Assert.Equal(subjectName, l.R.In(() => Dm.str(injector.stored_dna.real_name)));

        mob_living_carbon_human patient = l.R.Dummy(2, 2);
        string patientName = l.R.In(() => Dm.str(patient.real_name));
        Assert.True(l.R.In(() => Dm.truthy(Waiting.NoSleep(injector.inject(patient, l.Gene)))));
        Assert.Equal(subjectName, l.R.In(() => Dm.str(patient.real_name)));
        Assert.NotNull(l.R.In(() => patient.has_status_effect(typeof(datum_status_effect_temporary_transformation_dna_injector))));
        l.R.Step(61 * Second);
        Assert.Equal(patientName, l.R.In(() => Dm.str(patient.real_name))); // back after the injector's minute
        Assert.Null(l.R.In(() => patient.has_status_effect(typeof(datum_status_effect_temporary_transformation_dna_injector))));
        l.R.AssertNoRuntimes();
    }

    [Fact]
    public void The_scramble_rerolls_the_genes_and_the_enzyme_pulse_changes_the_identity()
    {
        Lab l = Room(9205);
        l.R.In(() => Waiting.NoSleep(l.Subject.dna.add_mutation(typeof(datum_mutation_glow), MUTATION_SOURCE_ACTIVATED)));
        string before = l.R.In(() => Dm.str(l.Subject.dna.unique_identity));
        JsonElement data = Open(l, out string window);
        Act(l.R, l.Gene, window, "scramble_dna");
        Assert.Equal(0f, l.R.In(() => l.Subject.dna.mutations.len));
        Assert.True(l.R.In(() => l.Console.scramble_ready > world.time));
        Assert.True(l.Feed.Any("DNA scrambled."), Diag(l));
        Assert.Equal(50f, l.R.In(() => (l.Subject.has_status_effect(typeof(datum_status_effect_genetic_damage)) as datum_status_effect_genetic_damage).total_damage));

        // a strong, long pulse on the identity's first block changes the identity when its time is up
        l.R.In(() =>
        {
            l.Console.pulse_strength = 15;
            l.Console.pulse_duration = 30;
        });
        string identity = l.R.In(() => Dm.str(l.Subject.dna.unique_identity));
        Act(l.R, l.Gene, window, "makeup_pulse", ("type", "ui"), ("index", 1));
        Assert.True(l.R.In(() => l.Console.genetic_damage_pulse_index == 1));
        JsonElement pulsing = Data(l.R, l.Gene, out _);
        Act(l.R, l.Gene, window, "set_view", ("consoleMode", "enzymes"));
        Assert.Equal(1, Data(l.R, l.Gene, out _).GetProperty("isPulsing").GetInt32());
        l.R.Step(34 * Second); // the 30 s pulse, done at SSmachines' next process (every 2 s)
        Assert.Equal(0f, l.R.In(() => l.Console.genetic_damage_pulse_index));
        Assert.NotEqual(identity, l.R.In(() => Dm.str(l.Subject.dna.unique_identity)));
        l.R.AssertNoRuntimes();
    }

    [Fact]
    public void Genetic_damage_poisons_above_its_threshold_and_wears_off()
    {
        Lab l = Room(9206, subjectInside: false);
        l.R.In(() => Waiting.NoSleep(l.Subject.apply_status_effect(typeof(datum_status_effect_genetic_damage), 501f)));
        float tox = l.R.In(() => Dm.num(l.Subject.get_tox_loss()));
        l.R.Step(10 * Second);
        Assert.True(l.R.In(() => Dm.num(l.Subject.get_tox_loss())) > tox);
        // a second dose adds to the first (STATUS_EFFECT_REFRESH)
        l.R.In(() => Waiting.NoSleep(l.Subject.apply_status_effect(typeof(datum_status_effect_genetic_damage), 10f)));
        var effect = l.R.In(() => l.Subject.has_status_effect(typeof(datum_status_effect_genetic_damage)) as datum_status_effect_genetic_damage);
        Assert.InRange(l.R.In(() => effect.total_damage), 500f, 510f);
        l.R.In(() => effect.total_damage = 1);
        l.R.Step(6 * Second);
        Assert.Null(l.R.In(() => l.Subject.has_status_effect(typeof(datum_status_effect_genetic_damage))));
        l.R.AssertNoRuntimes();
    }

    [Fact]
    public void Every_dna_type_is_made_and_destroyed_with_no_runtimes()
    {
        Lab l = Room(9207, subjectInside: false);
        foreach (Type type in AllTypes)
        {
            atom made = l.R.In(() => (atom)Dm.@new(type, locate(2, 2, 1)));
            l.R.Step(2);
            Assert.False(l.R.In(() => InertSeam.IsInert(made)), type.Name);
            l.R.In(() => qdel(made));
            l.R.Step(2);
        }
        Assert.True(l.R.In(() => ((obj_item_disk_data_debug)Dm.@new<obj_item_disk_data_debug>(locate(2, 2, 1))).mutations.len) == l.R.In(() => subtypesof(typeof(datum_mutation)).len));
        l.R.AssertNoRuntimes();
    }
}
