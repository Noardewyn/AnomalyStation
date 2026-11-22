// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/hat_wearer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

/**
 * # mobs that can wear hats!
 */
// tg: code/datums/elements/hat_wearer.dm:4
public partial class datum_element_hat_wearer : datum_element
{
    // tg: code/datums/elements/hat_wearer.dm:14
    [DmParams("target", "offsets", "remove_hat_signals", "traits_prevent_checks")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        DmList offsets = Dm.arg<DmList>(__extra, 0) ?? Dm.list();
        DmList remove_hat_signals = Dm.arg<DmList>(__extra, 1) ?? Dm.list();
        object traits_prevent_checks = Dm.arg(__extra, 2) ?? Dm.list();
        object dot = await base.Attach(target, __extra);
        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;
        this.offsets = offsets;
        this.traits_prevent_checks = traits_prevent_checks;
        this.remove_hat_signals = remove_hat_signals;
        RegisterSignal(target, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(on_overlays_updated));
        RegisterSignal(target, COMSIG_ATOM_EXITED, nameof(exited));
        RegisterSignal(target, COMSIG_ATOM_ENTERED, nameof(on_entered));
        RegisterSignal(target, COMSIG_ATOM_ATTACKBY, nameof(on_attack_by));
        if (!Dm.truthy(length(remove_hat_signals)))
            return dot;
        RegisterSignals(target, remove_hat_signals, nameof(remove_hat));
        return dot;
    }

    // tg: code/datums/elements/hat_wearer.dm:27
    public override object Detach(datum source = null, params object[] __extra)
    {
        atom target = (atom)source;
        obj_item hat = Dm.locate<obj_item_clothing_head>(target);
        if (hat != null)
            Waiting.NoSleep(hat.forceMove(get_turf(target)));
        UnregisterSignal(target, Dm.list(COMSIG_ATOM_UPDATE_OVERLAYS, COMSIG_ATOM_EXITED, COMSIG_ATOM_ENTERED, COMSIG_ATOM_ATTACKBY));
        if (Dm.truthy(length(remove_hat_signals)))
            UnregisterSignal(target, remove_hat_signals);
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/hat_wearer.dm:40
    [SignalHandler]
    public virtual void on_overlays_updated(atom source = null, DmList overlays = null)
    {
        obj_item hat = Dm.locate<obj_item_clothing_head>(source);
        if (hat == null)
            return;
        mutable_appearance hat_overlay = mutable_appearance(hat.worn_icon, hat.icon_state) as mutable_appearance;
        hat_overlay.pixel_w = Dm.num(offsets[1]);
        hat_overlay.pixel_z = Dm.num(offsets[2]);
        Dm.append(ref overlays, hat_overlay);
    }

    // tg: code/datums/elements/hat_wearer.dm:50
    [SignalHandler]
    public virtual void exited(atom_movable source = null, atom_movable exited = null)
    {
        if (exited is not obj_item_clothing_head)
            return;
        source.update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/datums/elements/hat_wearer.dm:57
    [SignalHandler]
    public virtual void on_entered(atom_movable source = null, atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        if (arrived is not obj_item_clothing_head)
            return;
        foreach (obj_item_clothing_head already_worn in Dm.each<obj_item_clothing_head>(source))
        {
            if (already_worn == arrived)
                continue;
            Waiting.NoSleep(already_worn.forceMove(get_turf(source)));
        }
        source.update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/datums/elements/hat_wearer.dm:68
    [SignalHandler]
    public virtual object on_attack_by(atom_movable source = null, obj_item item = null, mob_living attacker = null)
    {
        if (item is not obj_item_clothing_head)
            return null;

        foreach (object trait_check in Dm.each<object>(traits_prevent_checks))
        {
            if (HAS_TRAIT(source, trait_check))
            {
                source.balloon_alert(attacker, "not possible right now!");
                return COMPONENT_NO_AFTERATTACK;
            }
        }

        Waiting.Detach(place_hat(source, item, attacker));
        return COMPONENT_NO_AFTERATTACK;
    }

    // tg: code/datums/elements/hat_wearer.dm:82
    public virtual async DmTask place_hat(atom_movable source = null, obj_item item = null, mob_living attacker = null)
    {
        if (!Dm.truthy(await do_after(attacker, delay: 3 * SECONDS, target: source)))
        {
            source.balloon_alert(attacker, "must stay still!");
            return;
        }
        await item.forceMove(source);
    }

    // tg: code/datums/elements/hat_wearer.dm:88
    [SignalHandler]
    public virtual void remove_hat(atom_movable source = null)
    {
        obj our_hat = Dm.locate<obj_item_clothing_head>(source);
        if (our_hat == null)
            return;
        Waiting.NoSleep(our_hat.forceMove(source.drop_location() as atom));
    }
}
