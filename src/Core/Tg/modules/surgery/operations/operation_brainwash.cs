// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_brainwash.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated. The brainwashing itself (brainwash(), the brainwashed
// antagonist datum and its objective), the admin message and logs, and the sleeper agent's conspiracies phobia stay deferred
// leaves (not-translated.md section 118: the antagonists, the brain traumas, admin tooling). The operation, its objective prompt,
// messages and brain damage run.

// tg: code/modules/surgery/operations/operation_brainwash.dm:3
public partial class datum_surgery_operation_organ_brainwash : datum_surgery_operation_organ
{
    // tg: code/modules/surgery/operations/operation_brainwash.dm:1  #define OPERATION_OBJECTIVE "objective"
    protected const string OPERATION_OBJECTIVE = "objective";

    // tg: code/modules/surgery/operations/operation_brainwash.dm:25
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.initial(typeof(atom_movable_screen_alert_hypnosis), (atom_movable_screen_alert a) => a.overlay_icon), Dm.initial(typeof(atom_movable_screen_alert_hypnosis), (atom_movable_screen_alert a) => a.overlay_state));
    }

    // tg: code/modules/surgery/operations/operation_brainwash.dm:28
    public override async DmTask<object> pre_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        operation_args[OPERATION_OBJECTIVE] = await tgui_input_text(surgeon, I18n.T("Choose the objective to imprint on your patient's brain"), I18n.T("Brainwashing"), max_length: MAX_MESSAGE_LEN);
        return Dm.num(Dm.truthy(operation_args[OPERATION_OBJECTIVE]));
    }

    // tg: code/modules/surgery/operations/operation_brainwash.dm:32
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ_brain organ = (obj_item_organ_brain)operating_on;
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You begin to brainwash []...", organ.owner)),
            span_notice(I18n.T("[] begins to fix []'s brain.", surgeon, organ.owner)),
            span_notice(I18n.T("[] begins to perform surgery on []'s brain.", surgeon, organ.owner)));
        await display_pain(organ.owner, I18n.T("Your head pounds with unimaginable pain!")); // Same message as other brain surgeries
    }

    // tg: code/modules/surgery/operations/operation_brainwash.dm:42
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_organ_brain organ = (obj_item_organ_brain)operating_on;
        if (!Dm.truthy(organ.owner.mind))
        {
            to_chat(surgeon, span_warning(I18n.T("[] doesn't respond to the brainwashing, as if [] lacked a mind...", organ.owner, organ.owner.p_they())));
            return await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        }
        if (HAS_MIND_TRAIT(organ.owner, TRAIT_UNCONVERTABLE))
        {
            to_chat(surgeon, span_warning(I18n.T("[] seems resistant to the brainwashing...", organ.owner)));
            return await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        }

        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You successfully brainwash []!", organ.owner)),
            span_notice(I18n.T("[] successfully brainwashes []!", surgeon, organ.owner)),
            span_notice(I18n.T("[] finishes performing surgery on []'s brain.", surgeon, organ.owner)));
        await on_brainwash(organ.owner, surgeon, tool as obj_item, operation_args);
        return null;
    }

    // tg: code/modules/surgery/operations/operation_brainwash.dm:58
    public virtual async DmTask on_brainwash(mob_living_carbon brainwashed = null, mob_living surgeon = null, obj_item tool = null, DmList operation_args = null)
    {
        object objective = operation_args[OPERATION_OBJECTIVE] is var or__1 && Dm.truthy(or__1) ? or__1 : "Oooo no objective set somehow report this to an admin";
        to_chat(brainwashed, span_notice(I18n.T("A new thought forms in your mind: '[]'", objective)));
        Dm.Deferred("code/modules/surgery/operations/operation_brainwash.dm:61", "on_brainwash: brainwash(brainwashed, objective), the brainwashed antagonist datum and its objective (the antagonists)");
        message_admins(Dm.text("[] surgically brainwashed [] with the objective '[]'.", ADMIN_LOOKUPFLW(surgeon), ADMIN_LOOKUPFLW(brainwashed), objective));
        surgeon.log_message(Dm.text("has brainwashed [] with the objective '[]' using brainwashing surgery.", key_name(brainwashed), objective), LOG_ATTACK);
        brainwashed.log_message(Dm.text("has been brainwashed with the objective '[]' by [] using brainwashing surgery.", objective, key_name(surgeon)), LOG_VICTIM, log_globally: FALSE);
        surgeon.log_message(Dm.text("surgically brainwashed [] with the objective '[]'.", key_name(brainwashed), objective), LOG_GAME);
    }

    // tg: code/modules/surgery/operations/operation_brainwash.dm:67
    public override async DmTask on_failure(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ_brain organ = (obj_item_organ_brain)operating_on;
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You screw up, bruising the brain's tissue!")),
            span_notice(I18n.T("[] screws up, causing brain damage!", surgeon)),
            span_notice(I18n.T("[] completes the surgery on []'s brain.", surgeon, organ.owner)));
        await display_pain(organ.owner, I18n.T("Your head throbs with horrible pain!"));
        await organ.apply_organ_damage(40);
    }
}

// tg: code/modules/surgery/operations/operation_brainwash.dm:96
public partial class datum_surgery_operation_organ_brainwash_sleeper : datum_surgery_operation_organ_brainwash
{
    // tg: code/modules/surgery/operations/operation_brainwash.dm:117
    public override async DmTask<object> pre_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        operation_args[OPERATION_OBJECTIVE] = pick(possible_objectives);
        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_brainwash.dm:121
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ_brain organ = (obj_item_organ_brain)operating_on;
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You begin to brainwash []...", organ.owner)),
            span_notice(I18n.T("[] begins to fix []'s brain.", surgeon, organ.owner)),
            span_notice(I18n.T("[] begins to perform surgery on []'s brain.", surgeon, organ.owner)));
        await display_pain(organ.owner, I18n.T("Your head pounds with unimaginable pain!")); // Same message as other brain surgeries
    }

    // tg: code/modules/surgery/operations/operation_brainwash.dm:131
    public override async DmTask on_brainwash(mob_living_carbon brainwashed = null, mob_living surgeon = null, obj_item tool = null, DmList operation_args = null)
    {
        await base.on_brainwash(brainwashed, surgeon, tool, operation_args);
        await brainwashed.gain_trauma(Dm.@new<datum_brain_trauma_mild_phobia_conspiracies>(), TRAUMA_RESILIENCE_LOBOTOMY);
    }
}
