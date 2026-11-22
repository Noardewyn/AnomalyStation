// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/voucher_redeemer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4d (D298): the whole file. The var block is generated (D111).

/**
 * Accepts a [voucher_type] and a [set_type] and allows the user to redeem the voucher for items from a set.
 */
// tg: code/datums/elements/voucher_redeemer.dm:4
public partial class datum_element_voucher_redeemer : datum_element
{
    // tg: code/datums/elements/voucher_redeemer.dm:16
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        var voucher_type = (Dm.arg(__extra, 0) ?? typeof(obj_item_coin)) as Type;
        var set_type = (Dm.arg(__extra, 1) ?? typeof(datum_voucher_set)) as Type;
        object dot = await base.Attach(target, __extra);
        if (!ismovable(target))
            return ELEMENT_INCOMPATIBLE;
        if (!ispath(voucher_type, typeof(obj_item)))
        {
            stack_trace(Dm.text("Invalid voucher type for voucher_redeemer []", (object)voucher_type ?? "null"), "code/datums/elements/voucher_redeemer.dm", 21);
            return ELEMENT_INCOMPATIBLE;
        }
        if (!ispath(set_type, typeof(datum_voucher_set)))
        {
            stack_trace(Dm.text("Invalid set type for voucher_redeemer []", (object)set_type ?? "null"), "code/datums/elements/voucher_redeemer.dm", 24);
            return ELEMENT_INCOMPATIBLE;
        }

        this.voucher_type = voucher_type;
        this.set_type = set_type;
        RegisterSignal(target, COMSIG_ATOM_ITEM_INTERACTION, nameof(redeem_voucher));
        return dot;
    }

    // tg: code/datums/elements/voucher_redeemer.dm:31
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_ATOM_ITEM_INTERACTION);
        return dot;
    }

    // tg: code/datums/elements/voucher_redeemer.dm:35
    public virtual object redeem_voucher(atom source = null, mob_living redeemer = null, obj_item voucher = null, DmList modifiers = null)
    {
        if (!Dm.istype(voucher, voucher_type))
            return NONE;
        Waiting.Detach(redeem_voucher_async(source, redeemer, voucher)); // INVOKE_ASYNC
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/datums/elements/voucher_redeemer.dm:43
    public virtual void generate_sets()
    {
        set_instances = Dm.list();
        foreach (Type static_set in Dm.each_cast<Type>(subtypesof(set_type)))
            set_instances[Dm.initial(static_set, (datum_voucher_set p) => p.name)] = Dm.@new(static_set);
    }

    // tg: code/datums/elements/voucher_redeemer.dm:48
    public virtual void generate_options()
    {
        cached_options = Dm.list();
        foreach (object set_name in Dm.each(set_instances))
        {
            var current_set = (datum_voucher_set)set_instances[set_name];
            var option = Dm.@new<datum_radial_menu_choice>();
            option.image = image(current_set.icon, icon_state: current_set.icon_state);
            if (Dm.truthy(current_set.description))
                option.info = span_boldnotice(Dm.text("[]", current_set.description));
            cached_options[set_name] = option;
        }
    }

    // tg: code/datums/elements/voucher_redeemer.dm:58
    public virtual async DmTask redeem_voucher_async(atom source = null, mob_living redeemer = null, obj_item voucher = null)
    {
        if (set_instances == null)
        {
            generate_sets();
            generate_options();
        }

        object selection = await show_radial_menu(redeemer, source, cached_options, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), voucher, redeemer), radius: 38, require_near: TRUE, tooltips: TRUE);
        if (!Dm.truthy(selection))
            return;

        var chosen_set = (datum_voucher_set)set_instances[selection];
        chosen_set.spawn_set(source.drop_location() as atom);
        if (Dm.truthy(chosen_set.blackbox_key))
            SSblackbox.record_feedback("tally", chosen_set.blackbox_key, 1, selection);
        playsound(source, Dm.resource("sound/machines/card_slide.ogg"), 33, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        source.balloon_alert(redeemer, I18n.T("redeemed []", LOWER_TEXT(selection)));
        qdel(voucher);
    }

    // tg: code/datums/elements/voucher_redeemer.dm:73
    public virtual object check_menu(obj_item voucher = null, mob_living redeemer = null)
    {
        if (redeemer == null)
            return FALSE;
        if (Dm.truthy(redeemer.incapacitated))
            return FALSE;
        if (QDELETED(voucher))
            return FALSE;
        if (!Dm.truthy(redeemer.is_holding(voucher)))
            return FALSE;
        return TRUE;
    }
}
