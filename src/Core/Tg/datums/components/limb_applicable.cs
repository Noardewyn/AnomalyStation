// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/limb_applicable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M6 slice 6 (gauze): the whole file (the gauze is wrapped around a limb through it). The var block is generated. The proc
// on_apply is on_apply__proc: the var on_apply has its name (rules 2.4, Appendix B.2).

/**
 * ## Limb Applicable
 *
 * Allows item to be attached to limbs by clicking on humans with them while limb targeting
 *
 * The item then goes in the limb's contents, where it will be added to the limb's applied_items list
 * The applied_items is indexed by category so you can easily check for the presence of a given item or category of item
 */
// tg: code/datums/components/limb_applicable.dm:9
public partial class datum_component_limb_applicable : datum_component
{
    // tg: code/datums/components/limb_applicable.dm:26
    [DmParams("valid_zones", "apply_category", "override_existing", "can_apply", "do_apply", "on_apply")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        DmList valid_zones = Dm.arg<DmList>(__extra, 0) ?? GLOB.all_body_zones.Copy();
        object apply_category = Dm.arg(__extra, 1);
        object override_existing = Dm.arg(__extra, 2) ?? TRUE;
        datum_callback can_apply = Dm.arg(__extra, 3) as datum_callback;
        datum_callback do_apply = Dm.arg(__extra, 4) as datum_callback;
        datum_callback on_apply = Dm.arg(__extra, 5) as datum_callback;
        object dot = await base.Initialize(Dm.forward(__extra, valid_zones, apply_category, override_existing, can_apply, do_apply, on_apply));
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;

        this.valid_zones = valid_zones;
        this.apply_category = Dm.truthy(apply_category) ? apply_category : REF(parent);
        this.override_existing = override_existing;
        this.can_apply = can_apply;
        this.do_apply = do_apply;
        this.on_apply = on_apply;
        return dot;
    }

    // tg: code/datums/components/limb_applicable.dm:46
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ITEM_INTERACTING_WITH_ATOM, nameof(on_apply__proc));
        RegisterSignal(parent, COMSIG_ITEM_REQUESTING_CONTEXT_FOR_TARGET, nameof(add_context));

        obj_item applying_item = parent as obj_item;
        applying_item.flags_1 = Dm.num(Dm.bitor(applying_item.flags_1, HAS_CONTEXTUAL_SCREENTIPS_1));
    }

    // tg: code/datums/components/limb_applicable.dm:53
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_ITEM_INTERACTING_WITH_ATOM);
        UnregisterSignal(parent, COMSIG_ITEM_REQUESTING_CONTEXT_FOR_TARGET);
    }

    // tg: code/datums/components/limb_applicable.dm:57
    public virtual object add_context(obj_item source = null, DmList context = null, atom target = null, mob_living user = null)
    {
        if (isliving(target))
        {
            context[SCREENTIP_CONTEXT_LMB] = I18n.T("Apply []", source.name);
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return NONE;
    }

    // tg: code/datums/components/limb_applicable.dm:63
    [SignalHandler]
    public virtual object on_apply__proc(datum source = null, mob user = null, atom interacting_with = null)
    {
        if (!isliving(interacting_with))
            return NONE;

        mob_living target = interacting_with as mob_living;
        obj_item_bodypart applying_to = target.get_bodypart((string)deprecise_zone(user.zone_selected)) as obj_item_bodypart;

        if (applying_to == null)
        {
            target.balloon_alert(user, I18n.T("no bodypart!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.@in(applying_to.body_zone, valid_zones))
        {
            target.balloon_alert(user, I18n.T("can't be applied there!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(override_existing))
        {
            obj_item existing = LAZYACCESS(applying_to.applied_items, apply_category) as obj_item;
            if (existing != null)
            {
                target.balloon_alert(user, I18n.T("something is already there!"));
                return ITEM_INTERACT_BLOCKING;
            }
        }

        if (can_apply != null)
        {
            object try_apply = Waiting.NoSleep(can_apply.Invoke(user, target, applying_to));
            if (Dm.truthy(Dm.bitand(try_apply, LIMB_APPLICABLE_BLOCK_APPLICATION)))
                return Dm.truthy(Dm.bitand(try_apply, LIMB_APPLICABLE_BLOCK_ITEM_INTERACTION)) ? ITEM_INTERACT_BLOCKING : NONE;
        }

        Waiting.Detach(on_apply_async(user, target, applying_to));
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/datums/components/limb_applicable.dm:94
    public virtual async DmTask on_apply_async(mob user = null, mob_living target = null, obj_item_bodypart applying_to = null)
    {
        if (!Dm.truthy(do_apply == null ? null : await do_apply.Invoke(user, target, applying_to)))
            return;
        obj_item applying = parent as obj_item;
        if (isstack(parent))
        {
            obj_item_stack stack_parent = parent as obj_item_stack;
            applying = await stack_parent.split_stack(1) as obj_item;
        }
        else if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(applying)))
        {
            target.balloon_alert(user, I18n.T("can't part with []!", applying.name));
            return;
        }

        await applying_to.apply_item(applying, apply_category, override_existing is float f ? f : Dm.num(override_existing));
        if (on_apply != null)
            await on_apply.Invoke(user, target, applying_to);
    }
}
