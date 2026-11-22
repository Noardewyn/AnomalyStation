// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/mobs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the DOING_INTERACTION* macros (:636-639) translated (wave 4, package C, with do_after); the constants of
// this file are generated (Defines), and its other function-like macros come with their users.
// M2 P1 (the living core) added the members of its paths, marked "M2 P1" below.

public partial class datum_controller_global_vars
{
    /// Basic pet commands that regal rat minions can use.
    // tg: code/__DEFINES/mobs.dm:1112  GLOBAL_LIST_INIT(regal_rat_minion_commands, list(...))
    public DmList regal_rat_minion_commands;
    public virtual void InitGlobalregal_rat_minion_commands()
    {
        regal_rat_minion_commands = Dm.list(typeof(datum_pet_command_idle), typeof(datum_pet_command_free), typeof(datum_pet_command_protect_owner), typeof(datum_pet_command_follow), typeof(datum_pet_command_attack_mouse));
        Dm.append(ref gvars_datum_init_order, "regal_rat_minion_commands");
    }
}

public static partial class GlobalProcs
{
    // M8 slice A (metabolism): the reagent effect multiplier.
    // tg: code/__DEFINES/mobs.dm:513  #define REAGENTS_EFFECT_MULTIPLIER (1 / (REAGENTS_METABOLISM * SSMOBS_DT)) // When multiplied with the volume of reagent metabolized you get an value from 0->1 to scale all reagent affects
    public static float REAGENTS_EFFECT_MULTIPLIER() => (1 / (REAGENTS_METABOLISM * SSMOBS_DT()));
    // tg: code/__DEFINES/mobs.dm:514  #define REM REAGENTS_EFFECT_MULTIPLIER //! Shorthand for the above define for ease of use in equations and the like
    public static float REM() => REAGENTS_EFFECT_MULTIPLIER();

    // tg: code/__DEFINES/mobs.dm:636  #define DOING_INTERACTION(user, interaction_key) (LAZYACCESS(user.do_afters, interaction_key))
    public static object DOING_INTERACTION(object user, object interaction_key) => LAZYACCESS(((mob)user).do_afters, interaction_key);

    // tg: code/__DEFINES/mobs.dm:637  #define DOING_INTERACTION_LIMIT(user, interaction_key, max_interaction_count) ((LAZYACCESS(user.do_afters, interaction_key) || 0) >= max_interaction_count)
    public static bool DOING_INTERACTION_LIMIT(object user, object interaction_key, object max_interaction_count) => Dm.ge((LAZYACCESS(((mob)user).do_afters, interaction_key) is var or__1 && Dm.truthy(or__1) ? or__1 : 0), max_interaction_count);

    // tg: code/__DEFINES/mobs.dm:638  #define DOING_INTERACTION_WITH_TARGET(user, target) (LAZYACCESS(user.do_afters, target))
    public static object DOING_INTERACTION_WITH_TARGET(object user, object target) => LAZYACCESS(((mob)user).do_afters, target);

    // tg: code/__DEFINES/mobs.dm:639  #define DOING_INTERACTION_WITH_TARGET_LIMIT(user, target, max_interaction_count) ((LAZYACCESS(user.do_afters, target) || 0) >= max_interaction_count)
    public static bool DOING_INTERACTION_WITH_TARGET_LIMIT(object user, object target, object max_interaction_count) => Dm.ge((LAZYACCESS(((mob)user).do_afters, target) is var or__1 && Dm.truthy(or__1) ? or__1 : 0), max_interaction_count);

    // M9 slice B (emotes): the per-emote audio cooldowns.
    // tg: code/__DEFINES/mobs.dm:1081  #define MANUAL_SPECIFIC_EMOTE_AUDIO_COOLDOWN(type) "manual_specific_emote_audio_cooldown_[type]"
    public static string MANUAL_SPECIFIC_EMOTE_AUDIO_COOLDOWN(object type) => Dm.text("manual_specific_emote_audio_cooldown_[]", type);

    // tg: code/__DEFINES/mobs.dm:1085  #define FORCED_SPECIFIC_EMOTE_AUDIO_COOLDOWN(type) "forced_specific_emote_audio_cooldown_[type]"
    public static string FORCED_SPECIFIC_EMOTE_AUDIO_COOLDOWN(object type) => Dm.text("forced_specific_emote_audio_cooldown_[]", type);

    // M2 P1 (living core): the succumb and unconsciousness tests.
    /// Returns whether or not the given mob can succumb
    // tg: code/__DEFINES/mobs.dm:659  #define CAN_SUCCUMB(target) ((target.stat == SOFT_CRIT || target.stat == HARD_CRIT) && !HAS_TRAIT(target, TRAIT_NODEATH))
    public static bool CAN_SUCCUMB(object target) => (((mob)target).stat == SOFT_CRIT || ((mob)target).stat == HARD_CRIT) && !HAS_TRAIT(target, TRAIT_NODEATH);

    /// Checks if the mob is unconscious (or in other words, has [TRAIT_KNOCKEDOUT]) - note dead mobs are considered unconscious
    // tg: code/__DEFINES/mobs.dm:1121  #define IS_UNCONSCIOUS(mob) (HAS_TRAIT(mob, TRAIT_KNOCKEDOUT))
    public static bool IS_UNCONSCIOUS(object mob) => HAS_TRAIT(mob, TRAIT_KNOCKEDOUT);

    /// Checks if the mob is in soft crit, hard crit, dead, or is otherwise unconscious (which hard crit and death apply anyways)
    // tg: code/__DEFINES/mobs.dm:1123  #define IS_UNCONSCIOUS_OR_CRIT(mob) (IS_UNCONSCIOUS(mob) || mob.stat >= SOFT_CRIT)
    public static bool IS_UNCONSCIOUS_OR_CRIT(object mob) => IS_UNCONSCIOUS(mob) || ((mob)mob).stat >= SOFT_CRIT;

    /// Checks if the mob is unconscious but not dead
    // tg: code/__DEFINES/mobs.dm:1125  #define IS_UNCONSCIOUS_AND_ALIVE(mob) (IS_UNCONSCIOUS(mob) && mob.stat != DEAD)
    public static bool IS_UNCONSCIOUS_AND_ALIVE(object mob) => IS_UNCONSCIOUS(mob) && ((mob)mob).stat != DEAD;

    // tg: code/__DEFINES/mobs.dm:1127  #define IS_DEAD_OR_FAKING(mob) (mob.stat == DEAD || HAS_TRAIT(mob, TRAIT_FAKEDEATH))
    public static bool IS_DEAD_OR_FAKING(object mob) => ((mob)mob).stat == DEAD || HAS_TRAIT(mob, TRAIT_FAKEDEATH);

    // M2 P2 (carbon, human): the human's toxin damage and the toxin vomiting status effect.
    // tg: code/__DEFINES/mobs.dm:1076  #define AT_TOXIN_VOMIT_THRESHOLD(mob) (mob.get_tox_loss() > 45 && mob.nutrition > 20)
    public static bool AT_TOXIN_VOMIT_THRESHOLD(object mob) => Dm.gt(((mob_living)mob).get_tox_loss(), 45) && ((mob_living)mob).nutrition > 20;
}

public partial class datum_controller_global_vars
{
    /// Assoc list of all heights, to offset values
    // tg: code/__DEFINES/mobs.dm:777  GLOBAL_ALIST_INIT(human_heights_to_offsets, alist(...))
    public DmList human_heights_to_offsets;

    // tg: code/__DEFINES/mobs.dm:777  (GLOBAL_MANAGED(human_heights_to_offsets, alist(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalhuman_heights_to_offsets()
    {
        human_heights_to_offsets = Dm.alist(
            Dm.kv(MONKEY_HEIGHT_DWARF, Dm.list(Dm.kv(UPPER_BODY, -9), Dm.kv(LOWER_BODY, -3))),
            Dm.kv(MONKEY_HEIGHT_MEDIUM, Dm.list(Dm.kv(UPPER_BODY, -7), Dm.kv(LOWER_BODY, -4))),
            Dm.kv(HUMAN_HEIGHT_DWARF, Dm.list(Dm.kv(UPPER_BODY, -5), Dm.kv(LOWER_BODY, -4))),
            Dm.kv(HUMAN_HEIGHT_SHORTEST, Dm.list(Dm.kv(UPPER_BODY, -2), Dm.kv(LOWER_BODY, -1))),
            Dm.kv(HUMAN_HEIGHT_SHORT, Dm.list(Dm.kv(UPPER_BODY, -1), Dm.kv(LOWER_BODY, -1))),
            Dm.kv(HUMAN_HEIGHT_MEDIUM, Dm.list(Dm.kv(UPPER_BODY, 0), Dm.kv(LOWER_BODY, 0))),
            Dm.kv(HUMAN_HEIGHT_TALL, Dm.list(Dm.kv(UPPER_BODY, 1), Dm.kv(LOWER_BODY, 1))),
            Dm.kv(HUMAN_HEIGHT_TALLER, Dm.list(Dm.kv(UPPER_BODY, 2), Dm.kv(LOWER_BODY, 1))),
            Dm.kv(HUMAN_HEIGHT_TALLEST, Dm.list(Dm.kv(UPPER_BODY, 3), Dm.kv(LOWER_BODY, 2))));
        Dm.append(ref gvars_datum_init_order, "human_heights_to_offsets");
    }
}
