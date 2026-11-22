// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/pinnable_accessory.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// J1 (D298): the whole file (medals and badges that are pinned onto someone else's uniform). The var block is generated.

/// This accessory can be pinned onto someone else
// tg: code/datums/components/pinnable_accessory.dm:2
public partial class datum_component_pinnable_accessory : datum_component
{
    // tg: code/datums/components/pinnable_accessory.dm:10
    [DmParams("silent", "pinning_time", "on_pre_pin")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object silent = Dm.arg(__extra, 0);
        object pinning_time = Dm.arg(__extra, 1);
        datum_callback on_pre_pin = Dm.arg(__extra, 2) as datum_callback;
        silent ??= FALSE;
        pinning_time ??= 2 * SECONDS;
        object dot = await base.Initialize(Dm.forward(__extra, silent, pinning_time, on_pre_pin));
        if (!istype(parent, typeof(obj_item_clothing_accessory)))
            return COMPONENT_INCOMPATIBLE;
        this.silent = silent;
        this.pinning_time = pinning_time;
        this.on_pre_pin = on_pre_pin;
        return dot;
    }

    // tg: code/datums/components/pinnable_accessory.dm:19
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ITEM_INTERACTING_WITH_ATOM, nameof(on_atom_interact));
    }

    // tg: code/datums/components/pinnable_accessory.dm:22
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_ITEM_INTERACTING_WITH_ATOM);
    }

    /// Called when you whack someone with this accessory
    // tg: code/datums/components/pinnable_accessory.dm:26
    [SignalHandler]
    public virtual object on_atom_interact(obj_item_clothing_accessory badge = null, mob_living user = null, atom target = null, DmList modifiers = null)
    {
        if (!ishuman(target) || target == user)
            return null;

        Waiting.Detach(try_to_pin(badge, target as mob_living_carbon_human, user)); // INVOKE_ASYNC
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    /// Actually try to pin it on
    // tg: code/datums/components/pinnable_accessory.dm:34
    public virtual async DmTask try_to_pin(obj_item_clothing_accessory badge = null, mob_living_carbon_human distinguished = null, mob user = null)
    {
        obj_item_clothing_under distinguished_uniform = distinguished.w_uniform as obj_item_clothing_under;
        if (!(distinguished_uniform is obj_item_clothing_under))
        {
            distinguished.balloon_alert(user, I18n.T("no uniform to pin on!"));
            return;
        }

        if (!Dm.truthy(badge.can_attach_accessory(distinguished_uniform, user as mob_living)))
        {
            // Check handles feedback messages and etc
            return;
        }

        if (!Dm.truthy(silent))
        {
            user.visible_message(
                span_notice(I18n.T("[] tries to pin [] on []'s chest.", user, badge, distinguished)),
                span_notice(I18n.T("You try to pin [] on []'s chest.", badge, distinguished))
            );
        }

        if (on_pre_pin != null && !Dm.truthy(await on_pre_pin.Invoke(distinguished, user)))
            return;
        if (!Dm.truthy(pin_checks(user as mob_living, distinguished)) || !Dm.truthy(await do_after(user, pinning_time, distinguished, extra_checks: Dm.@new<datum_callback>(this, nameof(pin_checks), user, distinguished))))
            return;

        object pinned = await distinguished_uniform.attach_accessory(badge, user as mob_living);
        if (Dm.truthy(silent))
            return;

        if (Dm.truthy(pinned))
        {
            user.visible_message(
                span_notice(I18n.T("[] pins [] on []'s chest.", user, badge, distinguished)),
                span_notice(I18n.T("You pin [] on []'s chest.", badge, distinguished))
            );
        }
        else
        {
            user.visible_message(
                span_warning(I18n.T("[] fails to pin [] on []'s chest, seemingly unable to part with it.", user, badge, distinguished)),
                span_warning(I18n.T("You fail to pin [] on []'s chest.", badge, distinguished))
            );
        }
    }

    /// Callback for do_after to check if we can still be pinned
    // tg: code/datums/components/pinnable_accessory.dm:67
    public virtual object pin_checks(mob_living pinner = null, mob_living_carbon_human pinning_on = null)
    {
        if (QDELETED(parent) || QDELETED(pinner) || QDELETED(pinning_on))
            return FALSE;
        if (!Dm.truthy(pinner.is_holding(parent as obj_item)) || !Dm.truthy(Waiting.NoSleep(pinner.Adjacent(pinning_on))))
            return FALSE;
        obj_item_clothing_accessory badge = parent as obj_item_clothing_accessory;
        obj_item_clothing_under pinning_on_uniform = pinning_on.w_uniform as obj_item_clothing_under;
        if (!(pinning_on_uniform is obj_item_clothing_under) || !Dm.truthy(badge.can_attach_accessory(pinning_on_uniform, pinner)))
            return FALSE;
        return TRUE;
    }
}
