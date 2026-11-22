// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/poll_ignore.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file (the ghosts' polls' "Never For This Round" lists). The categories are global
// #defines outside code/__DEFINES, so they are public consts here (rules 11.4).

//Each lists stores ckeys for "Never for this round" option category

public static partial class GlobalProcs
{
    // tg: code/_globalvars/lists/poll_ignore.dm:3
    public const string POLL_IGNORE_ACADEMY_WIZARD = "academy_wizard";
    // tg: code/_globalvars/lists/poll_ignore.dm:4
    public const string POLL_IGNORE_ALIEN_LARVA = "alien_larva";
    // tg: code/_globalvars/lists/poll_ignore.dm:5
    public const string POLL_IGNORE_ASH_SPIRIT = "ash_spirit";
    // tg: code/_globalvars/lists/poll_ignore.dm:6
    public const string POLL_IGNORE_ASHWALKER = "ashwalker";
    // tg: code/_globalvars/lists/poll_ignore.dm:7
    public const string POLL_IGNORE_BLOB = "blob";
    // tg: code/_globalvars/lists/poll_ignore.dm:8
    public const string POLL_IGNORE_BLOOD_WORM = "blood_worm";
    // tg: code/_globalvars/lists/poll_ignore.dm:9
    public const string POLL_IGNORE_BOTS = "bots";
    // tg: code/_globalvars/lists/poll_ignore.dm:10
    public const string POLL_IGNORE_CARGORILLA = "cargorilla";
    // tg: code/_globalvars/lists/poll_ignore.dm:11
    public const string POLL_IGNORE_CONSTRUCT = "construct";
    // tg: code/_globalvars/lists/poll_ignore.dm:12
    public const string POLL_IGNORE_CONTRACTOR_SUPPORT = "contractor_support";
    // tg: code/_globalvars/lists/poll_ignore.dm:13
    public const string POLL_IGNORE_DRONE = "drone";
    // tg: code/_globalvars/lists/poll_ignore.dm:14
    public const string POLL_IGNORE_FIRE_SHARK = "fire_shark";
    // tg: code/_globalvars/lists/poll_ignore.dm:15
    public const string POLL_IGNORE_FUGITIVE = "fugitive";
    // tg: code/_globalvars/lists/poll_ignore.dm:16
    public const string POLL_IGNORE_GLITCH = "glitch";
    // tg: code/_globalvars/lists/poll_ignore.dm:17
    public const string POLL_IGNORE_GOLEM = "golem";
    // tg: code/_globalvars/lists/poll_ignore.dm:18
    public const string POLL_IGNORE_HERETIC_MONSTER = "heretic_monster";
    // tg: code/_globalvars/lists/poll_ignore.dm:19
    public const string POLL_IGNORE_HOLOPARASITE = "holoparasite";
    // tg: code/_globalvars/lists/poll_ignore.dm:20
    public const string POLL_IGNORE_HORRIFIC_NECKTIE = "horrific_necktie";
    // tg: code/_globalvars/lists/poll_ignore.dm:21
    public const string POLL_IGNORE_IMAGINARYFRIEND = "imaginary_friend";
    // tg: code/_globalvars/lists/poll_ignore.dm:22
    public const string POLL_IGNORE_LAVALAND_ELITE = "lavaland_elite";
    // tg: code/_globalvars/lists/poll_ignore.dm:23
    public const string POLL_IGNORE_MAID_IN_MIRROR = "maid_in_mirror";
    // tg: code/_globalvars/lists/poll_ignore.dm:24
    public const string POLL_IGNORE_MONKEY_HELMET = "mind_magnified_monkey";
    // tg: code/_globalvars/lists/poll_ignore.dm:25
    public const string POLL_IGNORE_PAI = "pai";
    // tg: code/_globalvars/lists/poll_ignore.dm:26
    public const string POLL_IGNORE_POSIBRAIN = "posibrain";
    // tg: code/_globalvars/lists/poll_ignore.dm:27
    public const string POLL_IGNORE_POSSESSED_BLADE = "possessed_blade";
    // tg: code/_globalvars/lists/poll_ignore.dm:28
    public const string POLL_IGNORE_PYROSLIME = "slime";
    // tg: code/_globalvars/lists/poll_ignore.dm:29
    public const string POLL_IGNORE_RAW_PROPHET = "raw_prophet";
    // tg: code/_globalvars/lists/poll_ignore.dm:30
    public const string POLL_IGNORE_REGAL_RAT = "regal_rat";
    // tg: code/_globalvars/lists/poll_ignore.dm:31
    public const string POLL_IGNORE_RUST_SPIRIT = "rust_spirit";
    // tg: code/_globalvars/lists/poll_ignore.dm:32
    public const string POLL_IGNORE_SENTIENCE_POTION = "sentience_potion";
    // tg: code/_globalvars/lists/poll_ignore.dm:33
    public const string POLL_IGNORE_SHADE = "shade";
    // tg: code/_globalvars/lists/poll_ignore.dm:34
    public const string POLL_IGNORE_SHUTTLE_DENIZENS = "shuttle_denizens";
    // tg: code/_globalvars/lists/poll_ignore.dm:35
    public const string POLL_IGNORE_SPECTRAL_BLADE = "spectral_blade";
    // tg: code/_globalvars/lists/poll_ignore.dm:36
    public const string POLL_IGNORE_SPIDER = "spider";
    // tg: code/_globalvars/lists/poll_ignore.dm:37
    public const string POLL_IGNORE_SPLITPERSONALITY = "split_personality";
    // tg: code/_globalvars/lists/poll_ignore.dm:38
    public const string POLL_IGNORE_STALKER = "stalker";
    // tg: code/_globalvars/lists/poll_ignore.dm:39
    public const string POLL_IGNORE_SYNDICATE = "syndicate";
    // tg: code/_globalvars/lists/poll_ignore.dm:40
    public const string POLL_IGNORE_SYNDICATE_MONKEY = "syndicate_monkey";
    // tg: code/_globalvars/lists/poll_ignore.dm:41
    public const string POLL_IGNORE_VENUSHUMANTRAP = "venus_human_trap";
    // tg: code/_globalvars/lists/poll_ignore.dm:42
    public const string POLL_IGNORE_RECOVERED_CREW = "recovered_crew";
    // tg: code/_globalvars/lists/poll_ignore.dm:43
    public const string POLL_IGNORE_EXPERIMENTAL_CLONER = "experimental_cloner";
    // tg: code/_globalvars/lists/poll_ignore.dm:44
    public const string POLL_IGNORE_FREE_SPORE = "free_spore";

    // tg: code/_globalvars/lists/poll_ignore.dm:91
    public static object init_poll_ignore()
    {
        DmList dot = Dm.list();
        foreach (object k in Dm.each(GLOB.poll_ignore_desc))
            dot[k] = Dm.list();
        return dot;
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/_globalvars/lists/poll_ignore.dm:46  GLOBAL_LIST_INIT(poll_ignore_desc, list(...))
    public DmList poll_ignore_desc;

    // tg: code/_globalvars/lists/poll_ignore.dm:46  (GLOBAL_MANAGED(poll_ignore_desc, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalpoll_ignore_desc()
    {
        poll_ignore_desc = Dm.list(
            Dm.kv(POLL_IGNORE_ACADEMY_WIZARD, "Academy Wizard Defender"),
            Dm.kv(POLL_IGNORE_ALIEN_LARVA, "Xenomorph larva"),
            Dm.kv(POLL_IGNORE_ASH_SPIRIT, "Ash Spirit"),
            Dm.kv(POLL_IGNORE_ASHWALKER, "Ashwalker eggs"),
            Dm.kv(POLL_IGNORE_BLOB, "Blob spores"),
            Dm.kv(POLL_IGNORE_BOTS, "Bots"),
            Dm.kv(POLL_IGNORE_CARGORILLA, "Cargorilla"),
            Dm.kv(POLL_IGNORE_CONSTRUCT, "Construct"),
            Dm.kv(POLL_IGNORE_CONTRACTOR_SUPPORT, "Contractor Support Unit"),
            Dm.kv(POLL_IGNORE_DRONE, "Drone shells"),
            Dm.kv(POLL_IGNORE_FIRE_SHARK, "Fire Shark"),
            Dm.kv(POLL_IGNORE_FUGITIVE, "Fugitive Hunter"),
            Dm.kv(POLL_IGNORE_GLITCH, "Glitch"),
            Dm.kv(POLL_IGNORE_GOLEM, "Golems"),
            Dm.kv(POLL_IGNORE_HERETIC_MONSTER, "Heretic Monster"),
            Dm.kv(POLL_IGNORE_HOLOPARASITE, "Holoparasite"),
            Dm.kv(POLL_IGNORE_HORRIFIC_NECKTIE, "Horrific Necktie"),
            Dm.kv(POLL_IGNORE_IMAGINARYFRIEND, "Imaginary Friend"),
            Dm.kv(POLL_IGNORE_LAVALAND_ELITE, "Lavaland elite"),
            Dm.kv(POLL_IGNORE_MAID_IN_MIRROR, "Maid in the Mirror"),
            Dm.kv(POLL_IGNORE_MONKEY_HELMET, "Mind magnified monkey"),
            Dm.kv(POLL_IGNORE_PAI, JOB_PERSONAL_AI),
            Dm.kv(POLL_IGNORE_POSIBRAIN, "Positronic brain"),
            Dm.kv(POLL_IGNORE_POSSESSED_BLADE, "Possessed blade"),
            Dm.kv(POLL_IGNORE_PYROSLIME, "Slime"),
            Dm.kv(POLL_IGNORE_RAW_PROPHET, "Raw Prophet"),
            Dm.kv(POLL_IGNORE_REGAL_RAT, "Regal rat"),
            Dm.kv(POLL_IGNORE_RUST_SPIRIT, "Rust Walker"),
            Dm.kv(POLL_IGNORE_SENTIENCE_POTION, "Sentience potion"),
            Dm.kv(POLL_IGNORE_SHADE, "Shade"),
            Dm.kv(POLL_IGNORE_SHUTTLE_DENIZENS, "Shuttle denizens"),
            Dm.kv(POLL_IGNORE_SPECTRAL_BLADE, "Spectral blade"),
            Dm.kv(POLL_IGNORE_SPIDER, "Spiders"),
            Dm.kv(POLL_IGNORE_SPLITPERSONALITY, "Split Personality"),
            Dm.kv(POLL_IGNORE_STALKER, "Stalker"),
            Dm.kv(POLL_IGNORE_SYNDICATE, "Syndicate"),
            Dm.kv(POLL_IGNORE_SYNDICATE_MONKEY, "Syndicate Monkey"),
            Dm.kv(POLL_IGNORE_VENUSHUMANTRAP, "Venus Human Traps"),
            Dm.kv(POLL_IGNORE_RECOVERED_CREW, "Recovered Crew"),
            Dm.kv(POLL_IGNORE_FREE_SPORE, "Free spore")
        );
        Dm.append(ref gvars_datum_init_order, nameof(poll_ignore_desc));
    }

    // tg: code/_globalvars/lists/poll_ignore.dm:88  GLOBAL_LIST_INIT(poll_ignore, init_poll_ignore())
    public DmList poll_ignore;

    // tg: code/_globalvars/lists/poll_ignore.dm:88  (GLOBAL_MANAGED(poll_ignore, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalpoll_ignore()
    {
        poll_ignore = init_poll_ignore() as DmList;
        Dm.append(ref gvars_datum_init_order, nameof(poll_ignore));
    }
}
