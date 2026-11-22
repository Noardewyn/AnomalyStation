// Translated from tgstation (snapshot 1d99bd7f): code/modules/mafia/_defines.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): Mafia's defines and its globals (with controller.dm's two role lists). The file never #undefs them and the .dme includes it outside
// code/__DEFINES, so the defines converter does not emit them: they are public members of GlobalProcs (rules 11.4).

public static partial class GlobalProcs
{
    ///The amount of players required to start a Mafia game
    // tg: code/modules/mafia/_defines.dm:2
    public const int MAFIA_MIN_PLAYER_COUNT = 6;
    ///how many people can play mafia without issues (running out of spawns, procs not expecting more than this amount of people, etc)
    // tg: code/modules/mafia/_defines.dm:4
    public const int MAFIA_MAX_PLAYER_COUNT = 12;

    ///The time spent during the first day, which is shorter due to not having a voting period.
    // tg: code/modules/mafia/_defines.dm:7
    public const float FIRST_DAY_PERIOD_LENGTH = 20 * SECONDS;
    ///The length of a Day period
    // tg: code/modules/mafia/_defines.dm:9
    public const float DAY_PERIOD_LENGTH = 1 * MINUTES;
    ///The length of a Voting period, when people decide who they want to put up for hanging that day.
    // tg: code/modules/mafia/_defines.dm:11
    public const float VOTING_PERIOD_LENGTH = 30 * SECONDS;
    ///The length of the judgment period, where people vote on whether to lynch the person they voted up.
    // tg: code/modules/mafia/_defines.dm:13
    public const float JUDGEMENT_PERIOD_LENGTH = 30 * SECONDS;
    ///The length of the lynch period, if the judged person is deemed guilty and is sentenced to death.
    // tg: code/modules/mafia/_defines.dm:15
    public const float LYNCH_PERIOD_LENGTH = 5 * SECONDS;
    ///The length of the night period where people can do their night abilities and speak with their mafia team.
    // tg: code/modules/mafia/_defines.dm:17
    public const float NIGHT_PERIOD_LENGTH = 40 * SECONDS;
    ///The length of the roundend report, where people can look over the round and the details.
    // tg: code/modules/mafia/_defines.dm:19
    public const float VICTORY_LAP_PERIOD_LENGTH = 20 * SECONDS;

    ///The cooldown between being able to send your notes in chat.
    // tg: code/modules/mafia/_defines.dm:22
    public const float MAFIA_NOTE_SENDING_COOLDOWN = 15 * SECONDS;

    ///How fast the game will speed up when half the players are gone.
    // tg: code/modules/mafia/_defines.dm:25
    public const int MAFIA_SPEEDUP_INCREASE = 2;

    //'Team' flags, used to know which alliance your role is part of.
    // tg: code/modules/mafia/_defines.dm:28-32
    public const int MAFIA_TEAM_TOWN = 1 << 0;
    public const int MAFIA_TEAM_MAFIA = 1 << 1;
    public const int MAFIA_TEAM_SOLO = 1 << 2;
    //those part of the 'dead' faction can hear dead chat, not a "faction" of players.
    public const int MAFIA_TEAM_DEAD = 1 << 3;

    //types of town roles for random setup gen
    /// Add this if you don't want a role to be a choice in the selection
    // tg: code/modules/mafia/_defines.dm:36
    public const string TOWN_OVERFLOW = "town overflow";
    /// roles that learn info about others in the game (chaplain, detective, psych)
    // tg: code/modules/mafia/_defines.dm:38
    public const string TOWN_INVEST = "town invest";
    /// roles that keep other roles safe (doctor, sec officer, and weirdly enough lawyer counts)
    // tg: code/modules/mafia/_defines.dm:40
    public const string TOWN_PROTECT = "town protect";
    /// roles that are only there to kill bad guys.
    // tg: code/modules/mafia/_defines.dm:42
    public const string TOWN_KILLING = "town killing";
    /// roles that don't fit into anything else (hop)
    // tg: code/modules/mafia/_defines.dm:44
    public const string TOWN_SUPPORT = "town support";

    //other types (mafia team, neutrals)
    /// normal vote kill changelings
    // tg: code/modules/mafia/_defines.dm:48
    public const string MAFIA_REGULAR = "mafia";
    /// every other changeling role that has extra abilities
    // tg: code/modules/mafia/_defines.dm:50
    public const string MAFIA_SPECIAL = "mafia special";
    /// role that wins solo that nobody likes
    // tg: code/modules/mafia/_defines.dm:52
    public const string NEUTRAL_KILL = "neutral killing";
    /// role that upsets the game aka obsessed, usually worse for town than mafia but they can vote against mafia
    // tg: code/modules/mafia/_defines.dm:54
    public const string NEUTRAL_DISRUPT = "neutral chaos";

    //role flags (special status of roles like detection immune)
    ///to all forms of detection, shows themselves as an assistant.
    // tg: code/modules/mafia/_defines.dm:58
    public const int ROLE_UNDETECTABLE = 1 << 0;
    ///has the ability to theoretically kill someone singlehandedly, blocks their team from losing against another teams.
    // tg: code/modules/mafia/_defines.dm:60
    public const int ROLE_CAN_KILL = 1 << 1;
    ///can only be one in a randomly generated game
    // tg: code/modules/mafia/_defines.dm:62
    public const int ROLE_UNIQUE = 1 << 2;
    ///role is public to all other players in the game.
    // tg: code/modules/mafia/_defines.dm:64
    public const int ROLE_REVEALED = 1 << 3;
    ///can not be defended, protected, or any other form of protection. all kills succeed no matter what.
    // tg: code/modules/mafia/_defines.dm:66
    public const int ROLE_VULNERABLE = 1 << 4;
    ///cannot perform any actions that night, preselected actions fail
    // tg: code/modules/mafia/_defines.dm:68
    public const int ROLE_ROLEBLOCKED = 1 << 5;

    ///Flag that decides whether the Mafia ability can be used on other people.
    // tg: code/modules/mafia/_defines.dm:71
    public const int CAN_USE_ON_OTHERS = 1 << 0;
    ///Flag that decides whether the Mafia ability can be used on themselves.
    // tg: code/modules/mafia/_defines.dm:73
    public const int CAN_USE_ON_SELF = 1 << 1;
    ///Flag that decides whether the Mafia ability can be used on dead players.
    // tg: code/modules/mafia/_defines.dm:75
    public const int CAN_USE_ON_DEAD = 1 << 2;

    // tg: code/modules/mafia/_defines.dm:77-82
    public const string MAFIA_PHASE_SETUP = "No Game";
    public const string MAFIA_PHASE_DAY = "Morning Period";
    public const string MAFIA_PHASE_VOTING = "Voting Period";
    public const string MAFIA_PHASE_JUDGEMENT = "Judgment";
    public const string MAFIA_PHASE_NIGHT = "Night Period";
    public const string MAFIA_PHASE_VICTORY_LAP = "Victory Lap";

    // tg: code/modules/mafia/_defines.dm:84-85
    public const int MAFIA_ALIVE = 1;
    public const int MAFIA_DEAD = 2;

    // tg: code/modules/mafia/_defines.dm:87-88
    public const string COMSIG_MAFIA_ON_VISIT = "mafia_onvisit";
    public const int MAFIA_VISIT_INTERRUPTED = 1;

    // tg: code/modules/mafia/_defines.dm:90-91
    public const string COMSIG_MAFIA_ON_KILL = "mafia_onkill";
    public const int MAFIA_PREVENT_KILL = 1;

    //in order of events + game end

    ///Sends all signals that must go immediately as night starts.
    // tg: code/modules/mafia/_defines.dm:96
    public const string COMSIG_MAFIA_SUNDOWN = "sundown";
    ///Sends all signals that must go first, aka roleblocks.
    // tg: code/modules/mafia/_defines.dm:98
    public const string COMSIG_MAFIA_NIGHT_PRE_ACTION_PHASE = "night_start";
    ///Sends the signal that all regular actions must go, such as
    // tg: code/modules/mafia/_defines.dm:100
    public const string COMSIG_MAFIA_NIGHT_ACTION_PHASE = "night_actions";
    /// now killing happens from the roles that do that. the reason this is post action phase is to ensure doctors can protect and lawyers can block
    // tg: code/modules/mafia/_defines.dm:102
    public const string COMSIG_MAFIA_NIGHT_KILL_PHASE = "night_kill";
    /// now clearing refs to prepare for the next day. Do not do any actions here, it's just for ref clearing.
    // tg: code/modules/mafia/_defines.dm:104
    public const string COMSIG_MAFIA_NIGHT_END = "night_end";

    /// signal sent to roles when the game is confirmed starting
    // tg: code/modules/mafia/_defines.dm:107
    public const string COMSIG_MAFIA_GAME_START = "game_start";
    /// signal sent to roles when the game is confirmed ending
    // tg: code/modules/mafia/_defines.dm:109
    public const string COMSIG_MAFIA_GAME_END = "game_end";

    // tg: code/modules/mafia/controller.dm:6  #define UNIVERSALLY_HATED_REQUIREMENT 12 (#undef at the file's end): the file's define
    internal const int UNIVERSALLY_HATED_REQUIREMENT = 12;
}

public partial class datum_controller_global_vars
{
    /// list of ghosts who want to play mafia, every time someone enters the list it checks to see if enough are in
    // tg: code/modules/mafia/_defines.dm:112  GLOBAL_LIST_EMPTY(mafia_signup)
    public DmList mafia_signup;

    // tg: code/modules/mafia/_defines.dm:112
    public virtual void InitGlobalmafia_signup()
    {
        mafia_signup = Dm.list();
        Dm.append(ref gvars_datum_init_order, "mafia_signup");
    }

    /// list of ghosts who want to play mafia that have since disconnected. They are kept in the lobby, but not counted for starting a game.
    // tg: code/modules/mafia/_defines.dm:114  GLOBAL_LIST_EMPTY(mafia_bad_signup)
    public DmList mafia_bad_signup;

    // tg: code/modules/mafia/_defines.dm:114
    public virtual void InitGlobalmafia_bad_signup()
    {
        mafia_bad_signup = Dm.list();
        Dm.append(ref gvars_datum_init_order, "mafia_bad_signup");
    }

    /// list of PDAs who want to play mafia, every time someone enters the list it checks to see if enough are in
    // tg: code/modules/mafia/_defines.dm:116  GLOBAL_LIST_EMPTY(pda_mafia_signup)
    public DmList pda_mafia_signup;

    // tg: code/modules/mafia/_defines.dm:116
    public virtual void InitGlobalpda_mafia_signup()
    {
        pda_mafia_signup = Dm.list();
        Dm.append(ref gvars_datum_init_order, "pda_mafia_signup");
    }

    /// the current global mafia game running.
    // tg: code/modules/mafia/_defines.dm:118  GLOBAL_VAR(mafia_game)
    public object mafia_game;

    // tg: code/modules/mafia/_defines.dm:118  (GLOBAL_UNMANAGED(mafia_game), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalmafia_game()
    {
        return;
    }

    /// list of ghosts in mafia_signup who have voted to start early
    // tg: code/modules/mafia/_defines.dm:120  GLOBAL_LIST_EMPTY(mafia_early_votes)
    public DmList mafia_early_votes;

    // tg: code/modules/mafia/_defines.dm:120
    public virtual void InitGlobalmafia_early_votes()
    {
        mafia_early_votes = Dm.list();
        Dm.append(ref gvars_datum_init_order, "mafia_early_votes");
    }

    // tg: code/modules/mafia/controller.dm:1  GLOBAL_LIST_INIT(mafia_roles_by_name, setup_mafia_roles_by_name())
    public DmList mafia_roles_by_name;

    // tg: code/modules/mafia/controller.dm:1
    public virtual void InitGlobalmafia_roles_by_name()
    {
        mafia_roles_by_name = GlobalProcs.setup_mafia_roles_by_name() as DmList;
        Dm.append(ref gvars_datum_init_order, "mafia_roles_by_name");
    }

    // tg: code/modules/mafia/controller.dm:3  GLOBAL_LIST_INIT(mafia_role_by_alignment, setup_mafia_role_by_alignment())
    public DmList mafia_role_by_alignment;

    // tg: code/modules/mafia/controller.dm:3
    public virtual void InitGlobalmafia_role_by_alignment()
    {
        mafia_role_by_alignment = GlobalProcs.setup_mafia_role_by_alignment() as DmList;
        Dm.append(ref gvars_datum_init_order, "mafia_role_by_alignment");
    }
}
