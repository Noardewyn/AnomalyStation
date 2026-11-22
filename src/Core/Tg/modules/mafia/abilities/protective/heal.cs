// Translated from tgstation (snapshot 1d99bd7f): code/modules/mafia/abilities/protective/heal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): The doctor's heal and the security officer's defence. The var block is generated (D111).

/**
 * Heal
 *
 * During the night, Healing will prevent a player from dying.
 * Can be used to protect yourself, but only once.
 */
// tg: code/modules/mafia/abilities/protective/heal.dm:7
public partial class datum_mafia_ability_heal : datum_mafia_ability
{
    // tg: code/modules/mafia/abilities/protective/heal.dm:16
    public override object set_target(datum_mafia_role new_target = null)
    {
        object dot = base.set_target(new_target);
        if (!Dm.truthy(dot))
            return FALSE;
        if (Dm.truthy(Dm.bitand(new_target.role_flags, ROLE_VULNERABLE)))
        {
            host_role.send_message_to_player(span_notice(I18n.T("[] can't be protected.", new_target)));
            return FALSE;
        }
        return dot;
    }

    // tg: code/modules/mafia/abilities/protective/heal.dm:24
    public override async DmTask<object> perform_action_target(datum_mafia_controller game = null, datum_mafia_role day_target = null)
    {
        object dot = await base.perform_action_target(game, day_target);
        if (!Dm.truthy(dot))
            return FALSE;

        if (target_role == host_role)
            use_flags = Dm.num(Dm.bitand(use_flags, ~CAN_USE_ON_SELF));
        RegisterSignal(target_role, COMSIG_MAFIA_ON_KILL, nameof(prevent_kill));
        return TRUE;
    }

    // tg: code/modules/mafia/abilities/protective/heal.dm:34
    [SignalHandler]
    public override void clean_action_refs(datum_mafia_controller game = null)
    {
        if (target_role != null)
            UnregisterSignal(target_role, COMSIG_MAFIA_ON_KILL);
        base.clean_action_refs(game);
    }

    // tg: code/modules/mafia/abilities/protective/heal.dm:39
    [SignalHandler]
    public virtual object prevent_kill(datum source = null, datum_mafia_controller game = null, object attacker = null, object lynch = null)
    {
        if (host_role == target_role)
        {
            host_role.send_message_to_player(span_warning(I18n.T("You were attacked last night!")));
            return MAFIA_PREVENT_KILL;
        }
        host_role.send_message_to_player(span_warning(I18n.T("The person you protected tonight was attacked!")));
        target_role.send_message_to_player(span_greentext(I18n.T("You were attacked last night, but []!", saving_message)));
        return MAFIA_PREVENT_KILL;
    }
}

/**
 * Defend subtype
 * Kills both players when successfully defending.
 */
// tg: code/modules/mafia/abilities/protective/heal.dm:52
public partial class datum_mafia_ability_heal_defend : datum_mafia_ability_heal
{
    // tg: code/modules/mafia/abilities/protective/heal.dm:57
    [SignalHandler]
    public override object prevent_kill(datum source = null, datum_mafia_controller game = null, object attacker = null, object lynch = null)
    {
        object dot = base.prevent_kill(source, game, attacker, lynch);
        if (host_role == target_role)
            return FALSE;

        datum_mafia_role attacking_role = attacker as datum_mafia_role;
        if (Dm.truthy(Waiting.NoSleep(attacking_role.kill(game, host_role, FALSE)))) //you attack the attacker
            attacking_role.send_message_to_player(span_userdanger(I18n.T("You have been ambushed by Security!")));
        Waiting.NoSleep(host_role.kill(game, attacker, FALSE)); //the attacker attacks you, they were able to attack the target so they can attack you.
        return FALSE;
    }
}
