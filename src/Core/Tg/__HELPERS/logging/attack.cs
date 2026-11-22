// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/logging/attack.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// log_attack (D325: /atom/proc/log_message's attack log), log_combat (W4.0 contracts), log_wound (M6 slice 2, check_wounding logs each wound)
// and log_bomber (syndicate N3, D316; gaps A1) are translated.

public static partial class GlobalProcs
{
    /// Generic attack logging
    // tg: code/__HELPERS/logging/attack.dm:2
    public static void log_attack(object text = null, DmList data = null)
    {
        logger.Log(LOG_CATEGORY_ATTACK, text, data);
    }

    /**
     * Log a combat message in the attack log
     *
     * Arguments:
     * * atom/user - argument is the actor performing the action
     * * atom/target - argument is the target of the action
     * * what_done - is a verb describing the action (e.g. punched, throwed, kicked, etc.)
     * * atom/object - is a tool with which the action was made (usually an item)
     * * addition - is any additional text, which will be appended to the rest of the log line
     */
    // tg: code/__HELPERS/logging/attack.dm:15
    public static void log_combat(atom user = null, atom target = null, object what_done = null, object @object = null, object addition = null)
    {
        object ssource = key_name(user);
        object starget = key_name(target);

        var living_target = target as mob_living;
        string hp = living_target != null ? Dm.text(" (NEWHP: []) ", living_target.health) : "";

        string sobject = "";
        if (Dm.truthy(@object))
            sobject = Dm.text(" with []", @object);
        string saddition = "";
        if (Dm.truthy(addition))
            saddition = Dm.text(" []", addition);

        string postfix = Dm.text("[][][]", sobject, saddition, hp);

        string message = Dm.text("[] [][]", what_done, starget, postfix);
        // tg calls user.log_message and target.log_message on a null user or target too (a runtime there); the port skips a null one
        user?.log_message(message, LOG_ATTACK, color: "red");

        if (user != target)
        {
            string reverse_message = Dm.text("was [] by [][]", what_done, ssource, postfix);
            target?.log_message(reverse_message, LOG_VICTIM, color: "orange", log_globally: FALSE);
        }
    }

    /**
     * Log a wound in the victim's attack log
     *
     * Arguments:
     * * victim- The guy who got wounded
     * * suffered_wound- The wound, already applied, that we're logging. It has to already be attached so we can get the limb from it
     * * dealt_damage- How much damage is associated with the attack that dealt with this wound.
     * * dealt_wound_bonus- The wound_bonus, if one was specified, of the wounding attack
     * * dealt_exposed_wound_bonus- The exposed_wound_bonus, if one was specified *and applied*, of the wounding attack. Not shown if armor was present
     * * base_roll- Base wounding ability of an attack is a random number from 1 to (dealt_damage * WOUND_DAMAGE_EXPONENT). This is the number that was rolled in there, before mods
     */
    // tg: code/__HELPERS/logging/attack.dm:52
    public static void log_wound(atom victim = null, datum_wound suffered_wound = null, object dealt_damage = null, object dealt_wound_bonus = null, object dealt_exposed_wound_bonus = null, object base_roll = null)
    {
        if (QDELETED(victim) || suffered_wound == null)
            return;
        string message = Dm.text("suffered: [][]", suffered_wound, suffered_wound.limb != null ? Dm.text(" to []", suffered_wound.limb.plaintext_zone) : null); // maybe indicate if it's a promote/demote?

        if (Dm.truthy(dealt_damage))
        {
            message += Dm.text(" | Damage: []", dealt_damage);
            // The base roll is useful since it can show how lucky someone got with the given attack. For example, dealing a cut
            if (Dm.truthy(base_roll))
                message += Dm.text(" (rolled []/[])", base_roll, Dm.pow(Dm.num(dealt_damage), WOUND_DAMAGE_EXPONENT));
        }

        if (Dm.truthy(dealt_wound_bonus))
            message += Dm.text(" | WB: []", dealt_wound_bonus);

        if (Dm.truthy(dealt_exposed_wound_bonus))
            message += Dm.text(" | BWB: []", dealt_exposed_wound_bonus);

        victim.log_message(message, LOG_ATTACK, color: "blue");
    }

    // tg: code/__HELPERS/logging/attack.dm:72
    public static void log_bomber(atom user = null, object details = null, atom bomb = null, object additional_details = null, object message_admins = null)
    {
        message_admins ??= TRUE;
        string bomb_message = Dm.text("[][][].", details, bomb != null ? Dm.text(" [] at []", bomb.name, AREACOORD(bomb)) : "", Dm.truthy(additional_details) ? Dm.text(" []", additional_details) : "");

        if (user != null)
        {
            user.log_message(bomb_message, LOG_ATTACK); //let it go to individual logs as well as the game log
            bomb_message = Dm.text("[] at [] [].", key_name(user), AREACOORD(user), bomb_message);
        }
        log_game(bomb_message);

        Dm.append(ref GLOB.bombers, bomb_message);
        var bomb_area = get_area(bomb) as area;
        if (Dm.truthy(message_admins) && !Dm.truthy(Dm.bitand(bomb_area?.area_flags, QUIET_LOGS))) // Don't spam the logs with deathmatch bombs
            GlobalProcs.message_admins(Dm.text("[][][][].", user != null ? Dm.text("[] at [] ", ADMIN_LOOKUPFLW(user), ADMIN_VERBOSEJMP(user)) : "", details, bomb != null ? Dm.text(" [] at []", bomb.name, ADMIN_VERBOSEJMP(bomb)) : "", Dm.truthy(additional_details) ? Dm.text(" []", additional_details) : ""));
    }
}
