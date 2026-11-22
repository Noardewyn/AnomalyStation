// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/equipment/lazarus_injector.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package MD3, D333's station order: the medical machines): the lazarus injector that revives a dead animal
// once and makes it friendly to its reviver (mob/living's lazarus_revive), its signal for the creatures that handle it themselves
// (the bees), the EMP's malfunction that keeps the revived hostile to everyone else, and its empty picture. The var block is
// generated (D111).

/**
 * Players can revive simplemobs with this.
 *
 * In-game item that can be used to revive a simplemob once. This makes the mob friendly.
 * Becomes useless after use.
 * Becomes malfunctioning when EMP'd.
 * If a hostile mob is revived with a malfunctioning injector, it will be hostile to everyone except whoever revived it and gets robust searching enabled.
 */
// tg: code/modules/mining/equipment/lazarus_injector.dm:9
public partial class obj_item_lazarus_injector : obj_item
{
    // tg: code/modules/mining/equipment/lazarus_injector.dm:28
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        atom target = interacting_with;
        if (!Dm.truthy(loaded))
            return NONE;
        if (Dm.truthy(Dm.bitand(target.SEND_SIGNAL(COMSIG_ATOM_ON_LAZARUS_INJECTOR, Dm.list(target, this, user)), LAZARUS_INJECTOR_USED)))
            return ITEM_INTERACT_SUCCESS;
        if (!isliving(target))
            return NONE;

        var target_animal = (mob_living)target;
        if (!Dm.truthy(target_animal.compare_sentience_type(revive_type))) // Will also return false if not a basic or simple mob, which are the only two we want anyway
        {
            balloon_alert(user, I18n.T("invalid creature!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (target_animal.stat != DEAD)
        {
            balloon_alert(user, I18n.T("it's not dead!"));
            return ITEM_INTERACT_BLOCKING;
        }

        await target_animal.lazarus_revive(user, malfunctioning);
        expend(target_animal, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mining/equipment/lazarus_injector.dm:49
    public virtual void expend(atom revived_target = null, mob user = null)
    {
        user.visible_message(span_notice(I18n.T("[] injects [] with [], reviving it.", user, revived_target, this)));
        SSblackbox.record_feedback("tally", "lazarus_injector", 1, revived_target.GetType());
        loaded = FALSE;
        playsound(this, Dm.resource("sound/effects/refill.ogg"), 50, TRUE);
        icon_state = "lazarus_empty";
    }

    // tg: code/modules/mining/equipment/lazarus_injector.dm:56
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        if (!Dm.truthy(malfunctioning))
            malfunctioning = TRUE;
        return dot;
    }

    // tg: code/modules/mining/equipment/lazarus_injector.dm:63
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!Dm.truthy(loaded))
            Dm.append(ref dot, span_info(I18n.T("[] is empty.", this)));
        if (Dm.truthy(malfunctioning))
            Dm.append(ref dot, span_info(I18n.T("The display on [] seems to be flickering.", this)));
        return dot;
    }
}
