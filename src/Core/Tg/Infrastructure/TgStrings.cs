using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Host code (M2 F1): tg's text data in the world's file seam. tg reads its name and word lists at world start with
// world.file2list("strings/...") (_globalvars/lists/names.dm, languages.dm); the port reads them the same way, from the
// world's file seam (DmWorldState.Files), so the host puts the snapshot's files there before the boot, as it writes
// config/config.txt. The files are not copied into the repository: they are read from the tgstation clone, found as the
// icons tool and the defines converter find it (ANOMALY_TGSTATION, else ../tgstation next to the repository or next to the
// main checkout of a worktree).

/// <summary>tg's <c>strings/</c> files that the translated <c>GLOBAL_LIST_INIT</c>s read, and how a host mounts them.</summary>
public static class TgStrings
{
    /// <summary>The environment variable that names the tgstation clone (as for the icons tool and the converter).</summary>
    public const string EnvironmentVariable = "ANOMALY_TGSTATION";

    /// <summary>
    /// The genetics mutations' speech (gaps P3: datums/mutations/speech.dm and hulk.dm, through strings()): SSatoms' setupGenetics
    /// makes one of every mutation at its Initialize (controllers/subsystem/atoms.dm), and the chav, elvis, heckacious, medieval
    /// and ork mutations read these in their New, so every world that initializes SSatoms needs them.
    /// </summary>
    public static readonly ImmutableArray<string> GeneticsFiles = ImmutableArray.Create(
        "strings/chav_replacement.json", "strings/elvis_replacement.json", "strings/heckacious.json",
        "strings/medieval_replacement.json", "strings/ork_replacement.json"); // declared before Files, whose initializer adds them

    /// <summary>The paths, relative to the clone's root, of the files the translated world start and strings() read.</summary>
    public static readonly ImmutableArray<string> Files = ImmutableArray.Create(
    
        // _globalvars/lists/names.dm
        "strings/names/ai.txt", "strings/names/cargorilla.txt", "strings/names/carp.txt", "strings/names/clown.txt",
        "strings/names/death_commando.txt", "strings/names/cyberauth.txt", "strings/names/ethereal.txt",
        "strings/names/first_female.txt", "strings/names/first_male.txt", "strings/names/first.txt", "strings/names/golem.txt",
        "strings/names/guardian_tarot.txt", "strings/names/guardian_descriptions.txt", "strings/names/guardian_gamepieces.txt",
        "strings/names/hackers.txt", "strings/names/hive_names.txt", "strings/names/last.txt",
        "strings/names/lizard_female.txt", "strings/names/lizard_male.txt", "strings/names/martial_prefix.txt",
        "strings/names/megacarp1.txt", "strings/names/megacarp2.txt", "strings/names/mime.txt",
        "strings/names/mirror_maid.txt", "strings/names/moth_first.txt", "strings/names/moth_last.txt",
        "strings/names/nightmare.txt", "strings/names/ninjaname.txt", "strings/names/ninjatitle.txt",
        "strings/names/operative_alias.txt", "strings/names/plasmaman.txt", "strings/names/posibrain.txt",
        "strings/names/religion.txt", "strings/station_names.txt", "strings/station_prefixes.txt",
        "strings/station_suffixes.txt", "strings/names/syndicate_monkey.txt", "strings/names/vampire_house_names.txt",
        "strings/names/voidwalker.txt", "strings/names/wizardfirst.txt", "strings/names/wizardsecond.txt",
        // _globalvars/lists/languages.dm
        "strings/1400_most_common_alpha.txt", "strings/1000_most_common_frequency.txt",
        // the scars' texts (datums/wounds/scars/_scars.dm, through strings(): __HELPERS/_string_lists.dm; M6 slice 2)
        "strings/wounds/flesh_scar_desc.json", "strings/wounds/bone_scar_desc.json", "strings/wounds/scar_loc.json",
        // the lobby music (SSticker Initialize, controllers/subsystem/ticker.dm:113; M10 slice F)
        "strings/round_start_sounds.txt",
        // the tips (send_tip_of_the_round, __HELPERS/game.dm:328; mannitol's overdose, medicine_reagents.dm:1225; M8 slice B)
        "strings/tips.txt", "strings/sillytips.txt", "strings/chemistrytips.txt",
        // the hallucinations' lines (modules/hallucination/fake_chat.dm, mother.dm, telepathy.dm; M8 slice G)
        "strings/crustacean_replacement.json", "strings/hallucination.json", "strings/mother.json",
        // the memories' stories (datums/memory/_memory.dm:299, through strings(): MEMORY_FILE; gaps package G4)
        "strings/memories.json",
        // the valentine cards and candy hearts (modules/events/holiday/vday.dm:133, :146, through strings(): VALENTINE_FILE; gaps R3)
        "strings/valentines.json",
        "strings/canadian_replacement.json", // gaps J4: the warden's drill hat
        // gaps J2 (D298): the french beret's and the italian moustache's speech
        "strings/french_replacement.json", "strings/italian_replacement.json",
        // the slurring speech effects' replacements (datums/status_effects/debuffs/speech_debuffs.dm:233, through strings(): drunk, cult,
        // heretic; a hallucinated crossbow bolt's slur reaches the drunk one)
        "strings/slurring_drunk_text.json", "strings/slurring_cult_text.json", "strings/slurring_heretic_text.json",
        // _globalvars/lists/strings.dm (M9 slice D2: the station name's greek letters and phonetic alphabet)
        "strings/names/adjectives.txt", "strings/names/adverbs.txt", "strings/dreamstrings.txt", "strings/fishing_tips.txt",
        "strings/greek_letters.txt", "strings/names/gross_adjectives.txt", "strings/names/ing_verbs.txt", "strings/junkmail.txt",
        "strings/numbers_as_words.txt", "strings/phonetic_alphabet.txt", "strings/names/verbs.txt", "strings/wisdoms.txt",
        // the photocopier's paper forms (GLOB.paper_blanks, modules/paperwork/photocopier.dm:43; gaps S3)
        "config/blanks.json", "config/silicon_laws.txt",
        // syndicate A2: the traitors' code words (generate_code_phrase, __HELPERS/names.dm: ION_FILE) and their employers' flavor
        // (TRAITOR_FLAVOR_FILE, datum_traitor.dm's pick_employer)
        "strings/ion_laws.json", "strings/antagonist_flavor/traitor_flavor.json",
        "strings/antagonist_flavor/malfunction_flavor.json", // silicons SI8: the malfunctioning AI's flavor (malf_ai.dm:38)
        // the round-start command report's flavour text (datums/communications.dm:165, through pick_list_replacements; syndicate A3)
        "strings/flavor_reports.json",
        // the cards (syndicate G3d): the Wiz-Off deck's (modules/cards/deck/wizoff.dm:10, through strings()); the CAS decks' texts
        // are not mounted (D343: CC BY-NC-SA 2.0)
        "strings/wizoff.json",
        // the phobias' trigger words (construct_phobia_regex, _globalvars/phobias.dm: PHOBIA_FILE; syndicate N3b)
        "strings/phobia.json",
        // the contractor's wanted messages (antagonists/traitor/contractor/syndicate_contract.dm:52, through strings(): WANTED_FILE;
        // syndicate X1a)
        "strings/wanted_message.json",
        // gaps RG3: the eigenstasium overdose's alternative selves' lines (datums/status_effects/neutral.dm:528, through strings():
        // EIGENSTASIUM_FILE)
        "strings/eigenstasium.json",
        // gaps E3 (D327): the shuttle insurance's rogue ship names (events/shuttle_insurance.dm:34, through strings(): PIRATE_NAMES_FILE) and
        // the luchador masks' speech (clothing/masks/boxing.dm:44)
        "strings/pirates.json", "strings/luchador_replacement.json",
        // gaps CL1: the gondola mask's speech (clothing/masks/gondola.dm:11); and the bane quotes of the AI's spine break
        // (datums/ai/generic_behaviors/break_spine.dm:41), which no mount had listed yet
        "strings/spurdo_replacement.json", "strings/bane.json",
        // gaps RG1: the char overdose's boomer lines (food_reagents.dm:1148, BOOMER_FILE) and the drunk scientist's Ballmer peak
        // (status_effects/debuffs/drunk.dm:171, VISTA_FILE), both through pick_list_replacements
        "strings/boomer.json", "strings/steve.json",
        // gaps OG1: the brain damage's lines (BRAIN_DAMAGE_FILE: the brain damage mood, mood_events/generic_negative_events.dm:136, which
        // a recovered crewmember's decayed brain reaches, and the mild traumas' babble, brain_damage/mild.dm:69) and the zombie
        // tongue's words (tongue/_tongue.dm:470)
        "strings/traumas.json", "strings/zombie_replacement.json",
        // gaps BR1 (D339): the virtual domains' safehouse rooms (the safehouse modular map root, modules/mapping/modular_map_loader/
        // modular_map_loader.dm:30, through rustg_read_toml_file)
        "strings/modular_maps/safehouse.toml"
    ).AddRange(GeneticsFiles);


    /// <summary>
    /// Copies every file of <see cref="Files"/> that exists under <paramref name="tgRoot"/> into <paramref name="files"/> at
    /// the same path; returns how many were copied. Call it before the world boots (GLOB's InitGlobal procs read them).
    /// </summary>
    public static int Mount(IDmFileSystem files, string tgRoot) => Mount(files, tgRoot, Files);

    /// <summary>
    /// <see cref="Mount(IDmFileSystem, string)"/> for the files of <paramref name="relativePaths"/> only (a world that needs a
    /// few of tg's strings, such as <see cref="GeneticsFiles"/>, without the name lists that change what its mobs are called).
    /// </summary>
    public static int Mount(IDmFileSystem files, string tgRoot, IEnumerable<string> relativePaths)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(relativePaths);
        if (string.IsNullOrEmpty(tgRoot))
            return 0;
        int copied = 0;
        foreach (string relative in relativePaths)
        {
            string path = Path.Combine(tgRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
                continue;
            files.WriteText(relative, File.ReadAllText(path));
            copied++;
        }
        return copied;
    }

    /// <summary>
    /// The tgstation clone: <c>$ANOMALY_TGSTATION</c>, else <c>../tgstation</c> next to the repository (the directory holding
    /// <c>AnomalyStation.sln</c>) found from one of <paramref name="startDirectories"/>, else next to the main checkout of
    /// a git worktree; null when none holds <c>tgstation.dme</c>.
    /// </summary>
    public static string Locate(params string[] startDirectories)
    {
        string fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrEmpty(fromEnvironment))
            return IsTgRoot(fromEnvironment) ? Path.GetFullPath(fromEnvironment) : null;
        foreach (string start in startDirectories)
        {
            if (string.IsNullOrEmpty(start))
                continue;
            // a build's own copy: a tgstation/ folder in the start folder or above it (the exported client's, the server's)
            for (var dir = new DirectoryInfo(Path.GetFullPath(start)); dir != null; dir = dir.Parent)
            {
                string shipped = Path.Combine(dir.FullName, "tgstation");
                if (IsTgRoot(shipped))
                    return shipped;
            }
            string repo = FindRepositoryRoot(start);
            if (repo == null)
                continue;
            string next = Path.GetFullPath(Path.Combine(repo, "..", "tgstation"));
            if (IsTgRoot(next))
                return next;
            string main = MainCheckout(repo);
            if (main != null)
            {
                string besideMain = Path.GetFullPath(Path.Combine(main, "..", "tgstation"));
                if (IsTgRoot(besideMain))
                    return besideMain;
            }
        }
        return null;
    }

    /// <summary>Whether the current world's GLOB holds tg's human name lists (first names and last names).</summary>
    public static bool HaveNames()
        => HasEntries(GLOB.first_names_male) && HasEntries(GLOB.first_names_female) && HasEntries(GLOB.last_names);

    private static bool HasEntries(DmList list)
    {
        if (list == null)
            return false;
        foreach (object item in Dm.each(list))
        {
            if (item is string s && s.Length > 0)
                return true;
        }
        return false;
    }

    private static bool IsTgRoot(string path) => File.Exists(Path.Combine(path, "tgstation.dme"));

    private static string FindRepositoryRoot(string start)
    {
        var dir = new DirectoryInfo(Path.GetFullPath(start));
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AnomalyStation.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    // For a git worktree (.git is a file "gitdir: <main>/.git/worktrees/<name>" whose commondir points at the main .git),
    // the main checkout's directory; null otherwise.
    private static string MainCheckout(string repo)
    {
        string dotGit = Path.Combine(repo, ".git");
        if (!File.Exists(dotGit))
            return null;
        string pointer = File.ReadAllText(dotGit).Trim();
        if (!pointer.StartsWith("gitdir:", StringComparison.Ordinal))
            return null;
        string gitDir = Path.GetFullPath(Path.Combine(repo, pointer.Substring(7).Trim()));
        string commonDir = gitDir;
        string commonFile = Path.Combine(gitDir, "commondir");
        if (File.Exists(commonFile))
            commonDir = Path.GetFullPath(Path.Combine(gitDir, File.ReadAllText(commonFile).Trim()));
        return Path.GetDirectoryName(commonDir.TrimEnd(Path.DirectorySeparatorChar));
    }
}
