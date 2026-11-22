// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/prosthetic_icon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps WP1 (D333): the file whole, with the chainsaw (weaponry/melee/chainsaw.dm) that wears it: an item mounted as a prosthetic
// arm takes the in-hand state "<prefix>_<base state>" (with "_on" while transformed on and its wielded digit), and its icon angle;
// off the arm it goes back to its own. The var block and the element flags are generated (D111).

/// Allows you to modify an item's icon state when it is used as a prosthetic limb.
// tg: code/datums/elements/prosthetic_icon.dm:2
public partial class datum_element_prosthetic_icon : datum_element
{
    // tg: code/datums/elements/prosthetic_icon.dm:14
    [DmParams("target", "icon_state_prefix", "icon_angle", "transforming", "wielding")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object icon_state_prefix = Dm.arg(__extra, 0);
        object icon_angle = Dm.arg(__extra, 1);
        object transforming = Dm.arg(__extra, 2) ?? FALSE;
        object wielding = Dm.arg(__extra, 3) ?? FALSE;
        object dot = await base.Attach(target, Dm.forward(__extra, icon_state_prefix, icon_angle, transforming, wielding));
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;

        this.icon_state_prefix = icon_state_prefix as string;
        this.icon_angle = icon_angle;
        this.transforming = Dm.num(transforming);
        this.wielding = Dm.num(wielding);

        RegisterSignal(target, COMSIG_ATOM_UPDATE_ICON, nameof(on_update_icon));
        RegisterSignals(target, Dm.list(COMSIG_ITEM_POST_USED_AS_PROSTHETIC, COMSIG_ITEM_DROPPED_FROM_PROSTHETIC), nameof(update_source));
        ((atom)target).update_appearance();
        return dot;
    }

    // tg: code/datums/elements/prosthetic_icon.dm:29
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, Dm.list(COMSIG_ATOM_UPDATE_ICON, COMSIG_ITEM_POST_USED_AS_PROSTHETIC, COMSIG_ITEM_DROPPED_FROM_PROSTHETIC));
        ((atom)source).update_appearance();
        return dot;
    }

    // tg: code/datums/elements/prosthetic_icon.dm:34
    [SignalHandler]
    public virtual object on_update_icon(obj_item source = null)
    {
        if (!HAS_TRAIT_FROM(source, TRAIT_NODROP, HAND_REPLACEMENT_TRAIT))
        {
            source.inhand_icon_state = source.base_icon_state as string;
            if (Dm.truthy(transforming) && HAS_TRAIT(source, TRAIT_TRANSFORM_ACTIVE))
                source.inhand_icon_state += "_on";
            if (Dm.truthy(wielding))
                source.inhand_icon_state += Dm.text("[]", HAS_TRAIT(source, TRAIT_WIELDED) ? 1 : 0);
            source.icon_angle = Dm.num(Dm.initial(source, (obj_item p) => p.icon_angle));
            return NONE;
        }

        source.inhand_icon_state = Dm.text("[]_[]", icon_state_prefix, source.base_icon_state);
        if (Dm.truthy(transforming) && HAS_TRAIT(source, TRAIT_TRANSFORM_ACTIVE))
            source.inhand_icon_state += "_on";
        if (Dm.truthy(wielding))
            source.inhand_icon_state += Dm.text("[]", HAS_TRAIT(source, TRAIT_WIELDED) ? 1 : 0);
        if (isnum(icon_angle))
            source.icon_angle = Dm.num(icon_angle);
        Waiting.NoSleep(source.update_inhand_icon());
        return COMSIG_ATOM_NO_UPDATE_ICON_STATE;
    }

    // tg: code/datums/elements/prosthetic_icon.dm:56
    [SignalHandler]
    public virtual void update_source(obj_item source = null)
    {
        source.update_appearance();
        Waiting.NoSleep(source.update_inhand_icon());
    }
}
