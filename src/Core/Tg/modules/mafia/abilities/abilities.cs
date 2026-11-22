// Translated from tgstation (snapshot 1d99bd7f): code/modules/mafia/abilities/abilities.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): a Mafia ability: registered on its night phase's signal, checked against its use period, the
// roleblock and its target rules, interrupted by a visit's signal, and targeted at night. The var block is generated (D111).

// tg: code/modules/mafia/abilities/abilities.dm:1
public partial class datum_mafia_ability : datum
{
    // tg: code/modules/mafia/abilities/abilities.dm:20
    [DmParams("host_role")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        datum_mafia_role host_role = Dm.arg(__extra, 0) as datum_mafia_role;
        this.host_role = host_role;
        if (Dm.truthy(action_priority))
        {
            RegisterSignal(host_role.mafia_game_controller, action_priority, nameof(perform_action_target));
            RegisterSignal(host_role.mafia_game_controller, COMSIG_MAFIA_NIGHT_END, nameof(clean_action_refs));
        }
    }

    // tg: code/modules/mafia/abilities/abilities.dm:27
    public override object Destroy(float? force = null)
    {
        host_role = null;
        target_role = null;
        return base.Destroy(force);
    }

    ///Handles special messagese sent by ability-specific stuff (such as changeling chat).
    // tg: code/modules/mafia/abilities/abilities.dm:33
    [SignalHandler]
    public virtual object handle_speech(datum source = null, DmList speech_args = null)
    {
        return FALSE;
    }

    /**
     * Called when refs need to be cleared, the last thing that is called in a night cycle.
     */
    // tg: code/modules/mafia/abilities/abilities.dm:40
    [SignalHandler]
    public virtual void clean_action_refs(datum_mafia_controller game = null)
    {
        target_role = null;
        using_ability = Dm.initial(this, (datum_mafia_ability a) => a.using_ability);
    }

    /**
     * Used to check if this ability can be used on a potential target.
     */
    // tg: code/modules/mafia/abilities/abilities.dm:54
    public virtual object validate_action_target(datum_mafia_role potential_target = null, float? silent = null)
    {
        silent ??= FALSE;
        if (host_role.game_status == MAFIA_DEAD)
            return FALSE;
        if (host_role.mafia_game_controller.phase != valid_use_period)
            return FALSE;
        if (Dm.truthy(Dm.bitand(host_role.role_flags, ROLE_ROLEBLOCKED)))
        {
            host_role.send_message_to_player(span_warning(I18n.T("You were roleblocked!")));
            return FALSE;
        }

        if (potential_target != null)
        {
            if (Dm.truthy(Dm.bitand(use_flags, CAN_USE_ON_DEAD)))
            {
                if (potential_target.game_status != MAFIA_DEAD)
                {
                    if (!Dm.truthy(silent))
                        host_role.send_message_to_player(span_notice(I18n.T("This can only be used on dead players.")));
                    return FALSE;
                }
            }
            else if (potential_target.game_status == MAFIA_DEAD)
            {
                if (!Dm.truthy(silent))
                    host_role.send_message_to_player(span_notice(I18n.T("This can only be used on living players.")));
                return FALSE;
            }
            if (!Dm.truthy(Dm.bitand(use_flags, CAN_USE_ON_SELF)) && potential_target == host_role)
            {
                if (!Dm.truthy(silent))
                    host_role.send_message_to_player(span_notice(I18n.T("This can only be used on others.")));
                return FALSE;
            }
            if (!Dm.truthy(Dm.bitand(use_flags, CAN_USE_ON_OTHERS)) && potential_target != host_role)
            {
                if (!Dm.truthy(silent))
                    host_role.send_message_to_player(span_notice(I18n.T("This can only be used on yourself.")));
                return FALSE;
            }
        }
        return TRUE;
    }

    /**
     * Called when using the ability.
     * Will first check if you are using the ability, then whether you can use it.
     * Finally it will check if you are interrupted, then will pass that you've performed it.
     */
    // tg: code/modules/mafia/abilities/abilities.dm:93
    public virtual async DmTask<object> perform_action_target(datum_mafia_controller game = null, datum_mafia_role day_target = null)
    {
        await DmTask.CompletedTask;
        if (!Dm.truthy(using_ability))
            return FALSE;
        if (!Dm.truthy(validate_action_target(target_role)))
            return FALSE;

        if (target_role != null)
        {
            if (Dm.truthy(Dm.bitand(target_role.SEND_SIGNAL(COMSIG_MAFIA_ON_VISIT, Dm.list(target_role, game, host_role)), MAFIA_VISIT_INTERRUPTED))) //visited a warden. something that prevents you by visiting that person
            {
                host_role.send_message_to_player(span_danger(I18n.T("Your [] was interrupted!", name)));
                return FALSE;
            }
        }

        return TRUE;
    }

    /**
     * ##set_target
     *
     * Used for Night abilities ONLY
     * Sets the ability's target, which will cause the action to be performed on them at the end of the night.
     * Subtypes can override this for things like self-abilities (such as shooting visitors).
     */
    // tg: code/modules/mafia/abilities/abilities.dm:116
    public virtual object set_target(datum_mafia_role new_target = null)
    {
        if (!Dm.truthy(validate_action_target(new_target)))
            return FALSE;

        string feedback_text = I18n.T("You will %WILL_PERFORM% []%SELF%", ability_action);
        if (Dm.truthy(Dm.bitand(use_flags, CAN_USE_ON_SELF)))
            feedback_text = replacetext(feedback_text, "%SELF%", ".") as string;
        else
            feedback_text = replacetext(feedback_text, "%SELF%", I18n.T(" [].", new_target.body)) as string;

        if (target_role == new_target)
        {
            using_ability = FALSE;
            target_role = null;
            feedback_text = replacetext(feedback_text, "%WILL_PERFORM%", I18n.T("not")) as string;
        }
        else
        {
            using_ability = TRUE;
            target_role = new_target;
            feedback_text = replacetext(feedback_text, "%WILL_PERFORM%", I18n.T("now")) as string;
        }

        host_role.send_message_to_player(span_notice(feedback_text));
        return TRUE;
    }
}
