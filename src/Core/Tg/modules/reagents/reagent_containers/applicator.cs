// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/reagent_containers/applicator.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice C: the whole file (the pills' and patches' base: applying to oneself or forcing on another). The var block is
// generated.

/// Generic reagent applicator type for pills and patches
// tg: code/modules/reagents/reagent_containers/applicator.dm:2
public partial class obj_item_reagent_containers_applicator : obj_item_reagent_containers
{
    // tg: code/modules/reagents/reagent_containers/applicator.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(((datum_reagents)reagents).total_volume) && Dm.truthy(rename_with_volume))
            name = Dm.add(name, I18n.T(" ([]u)", ((datum_reagents)reagents).total_volume)) as string;
        return dot;
    }

    /// Consumption effects, must be overriden by children
    // tg: code/modules/reagents/reagent_containers/applicator.dm:22
    public virtual async DmTask<object> on_consumption(mob consumer = null, mob giver = null, DmList modifiers = null)
    {
        return null;
    }

    // tg: code/modules/reagents/reagent_containers/applicator.dm:25
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!ismob(interacting_with))
            return NONE;

        mob target_mob = (mob)interacting_with;
        if (!Dm.truthy(canconsume(target_mob, user)))
            return ITEM_INTERACT_BLOCKING;

        user.changeNext_move(CLICK_CD_MELEE);
        if (target_mob == user)
        {
            target_mob.visible_message(span_notice(I18n.T("[] attempts to [] [].", user, apply_method, this)));
            if (Dm.truthy(self_delay))
            {
                if (!Dm.truthy(await do_after(user, self_delay, target_mob)))
                    return ITEM_INTERACT_BLOCKING;
            }
            to_chat(target_mob, span_notice(I18n.T("You [] [].", apply_method, this)));
            await on_consumption(user, user, modifiers);
            return ITEM_INTERACT_SUCCESS;
        }

        target_mob.visible_message(span_danger(I18n.T("[] attempts to force [] to [] [].", user, target_mob, apply_method, this)), span_userdanger(I18n.T("[] attempts to force you to [] [].", user, apply_method, this)));
        if (!Dm.truthy(await do_after(user, CHEM_INTERACT_DELAY(application_delay, user), target_mob)))
            return ITEM_INTERACT_BLOCKING;

        target_mob.visible_message(span_danger(I18n.T("[] forces [] to [] [].", user, target_mob, apply_method, this)), span_userdanger(I18n.T("[] forces you to [] [].", user, apply_method, this)));
        await on_consumption(target_mob, user, modifiers);
        return ITEM_INTERACT_SUCCESS;
    }
}
