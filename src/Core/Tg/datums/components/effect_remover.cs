// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/effect_remover.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3b: the syndicate tome clears runes with it) but the screentip, add_item_context, a live-partial row (the
// owner's rule of 2026-10-29). The cult, heretic and cosmic runes it clears are not ported yet. The var block is generated (D111).

/*
 * Simple component for something that is able to destroy
 * certain effects (such as cult runes) in one attack.
 */
// tg: code/datums/components/effect_remover.dm:5
public partial class datum_component_effect_remover : datum_component
{
    // tg: code/datums/components/effect_remover.dm:20
    [DmParams("success_forcesay", "success_feedback", "tip_text", "on_clear_callback", "effects_we_clear", "time_to_remove")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object success_forcesay = Dm.arg(__extra, 0);
        object success_feedback = Dm.arg(__extra, 1);
        object tip_text = Dm.arg(__extra, 2);
        datum_callback on_clear_callback = Dm.arg<datum_callback>(__extra, 3);
        object effects_we_clear = Dm.arg(__extra, 4);
        object time_to_remove = Dm.arg(__extra, 5);

        object dot = await base.Initialize(__extra);
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;

        if (!Dm.truthy(effects_we_clear))
        {
            stack_trace(Dm.text("[] was instantiated without any valid removable effects!", type), "code/datums/components/effect_remover.dm", 33);
            return COMPONENT_INCOMPATIBLE;
        }

        this.success_feedback = success_feedback;
        this.success_forcesay = success_forcesay;
        this.tip_text = tip_text;
        this.on_clear_callback = on_clear_callback;
        this.effects_we_clear = typecacheof(effects_we_clear) as DmList;
        this.time_to_remove = Dm.num(time_to_remove);
        return dot;
    }

    // tg: code/datums/components/effect_remover.dm:43
    public override object Destroy(float? force = null)
    {
        on_clear_callback = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/effect_remover.dm:47
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ITEM_INTERACTING_WITH_ATOM, nameof(try_remove_effect));

        if (Dm.truthy(tip_text))
        {
            obj_item item_parent = (obj_item)parent;
            item_parent.item_flags = Dm.num(Dm.bitor(item_parent.item_flags, ITEM_HAS_CONTEXTUAL_SCREENTIPS));
            RegisterSignal(parent, COMSIG_ITEM_REQUESTING_CONTEXT_FOR_TARGET, nameof(add_item_context));
        }
    }

    // tg: code/datums/components/effect_remover.dm:55
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_ITEM_INTERACTING_WITH_ATOM, COMSIG_ITEM_REQUESTING_CONTEXT_FOR_TARGET));
    }

    /*
     * Signal proc for [COMSIG_ITEM_INTERACTING_WITH_ATOM].
     */
    // tg: code/datums/components/effect_remover.dm:62
    [SignalHandler]
    public virtual object try_remove_effect(datum source = null, mob_living user = null, atom target = null, object @params = null)
    {
        if (!isliving(user))
            return NONE;

        if (HAS_TRAIT(target, TRAIT_ILLUSORY_EFFECT))
        {
            to_chat(user, span_notice(I18n.T("You pass [] through the [], but nothing seems to happen. Is it really even there?", parent, target)));
            return NONE;
        }

        if (Dm.truthy(is_type_in_typecache(target, effects_we_clear))) // Make sure we get all subtypes and everything
        {
            Waiting.Detach(do_remove_effect(target as obj_effect, user));
            return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }

    /*
     * Actually removes the effect, invoking our on_clear_callback before it's deleted.
     */
    // tg: code/datums/components/effect_remover.dm:79
    public virtual async DmTask do_remove_effect(obj_effect target = null, mob_living user = null)
    {
        if (Dm.truthy(time_to_remove) && !Dm.truthy(await do_after(user, time_to_remove, target)))
            return;

        obj_item item_parent = (obj_item)parent;
        if (Dm.truthy(success_forcesay))
            await user.say(success_forcesay, forced: item_parent.name);
        if (Dm.truthy(success_feedback))
        {
            object real_feedback = replacetext(success_feedback, "%THEEFFECT", Dm.text("\\the []", target));
            real_feedback = replacetext(real_feedback, "%THEWEAPON", Dm.text("\\the []", item_parent));
            to_chat(user, span_notice(Dm.str(real_feedback)));
        }
        if (on_clear_callback != null)
            await on_clear_callback.Invoke(target, user);

        if (!QDELETED(target))
            qdel(target);
    }
}
