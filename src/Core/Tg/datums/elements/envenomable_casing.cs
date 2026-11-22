// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/envenomable_casing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps GU1, D333: the arrows dipped in a chemical). The var block is generated (D111).

/**
 * ### envenomable caseless element!
 *
 * Non bespoke element (1 in existence) that lets caseless bullets be dippable.
 * When you fire the bullet, it will gain venomous. The casing itself isn't venomous to prevent bullshit
 */
// tg: code/datums/elements/envenomable_casing.dm:7
public partial class datum_element_envenomable_casing : datum_element
{
    // tg: code/datums/elements/envenomable_casing.dm:13
    [DmParams("target", "amount_allowed")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object amount_allowed = Dm.arg(__extra, 0) ?? 5;
        object dot = await base.Attach(target, __extra);
        if (target is not obj_item_ammo_casing) // isammocasing (__DEFINES/is_helpers.dm), inline (rules 11.1 (a))
            return ELEMENT_INCOMPATIBLE;
        this.amount_allowed = amount_allowed;
        RegisterSignal(target, COMSIG_ITEM_INTERACTING_WITH_ATOM, nameof(handle_interaction));
        RegisterSignal(target, COMSIG_ATOM_EXAMINE, nameof(on_examine_before_dip));
        return dot;
    }

    // tg: code/datums/elements/envenomable_casing.dm:21
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, Dm.list(COMSIG_ITEM_INTERACTING_WITH_ATOM, COMSIG_ATOM_EXAMINE));
        return dot;
    }

    ///signal called on the parent attacking an item
    // tg: code/datums/elements/envenomable_casing.dm:26
    [SignalHandler]
    public virtual object handle_interaction(obj_item_ammo_casing casing = null, mob user = null, atom target = null, DmList modifiers = null)
    {
        if (!Dm.truthy(target.is_open_container()))
            return NONE;
        if (casing.loaded_projectile == null)
        {
            user.balloon_alert(user, I18n.T("casing is already spent!"));
            return ITEM_INTERACT_BLOCKING;
        }

        datum_reagent venom_applied = ((datum_reagents)target.reagents).get_master_reagent() as datum_reagent;
        if (venom_applied == null)
            return ITEM_INTERACT_BLOCKING;
        object amount_applied = min(venom_applied.volume, amount_allowed);

        Waiting.NoSleep(casing.loaded_projectile.AddElement(typeof(datum_element_venomous), venom_applied.type, amount_applied));
        to_chat(user, span_notice(I18n.T("You coat [] in [].", casing, venom_applied)));
        Waiting.NoSleep(((datum_reagents)target.reagents).remove_reagent(venom_applied.type, amount_applied));
        ///stops further poison application
        UnregisterSignal(casing, COMSIG_ITEM_INTERACTING_WITH_ATOM);
        RegisterSignal(casing, COMSIG_ATOM_EXAMINE, nameof(on_examine_after_dip), @override: TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    ///signal called on parent being examined while not coated
    // tg: code/datums/elements/envenomable_casing.dm:49
    [SignalHandler]
    public virtual void on_examine_before_dip(obj_item_ammo_casing casing = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(ref examine_list, span_notice(I18n.T("You can dip it in a chemical to deliver a poisonous kick.")));
    }

    ///ditto, but after it's been coated
    // tg: code/datums/elements/envenomable_casing.dm:54
    [SignalHandler]
    public virtual void on_examine_after_dip(obj_item_ammo_casing casing = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(ref examine_list, span_warning(I18n.T("It's coated in some kind of chemical...")));
    }
}
