// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/wearable_client_colour.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice T3 (D164, the mesons' tint): the whole file. The var block is generated.

/// An element that adds a client colour to the wearer when equipped to the right slot, under the right conditions.
// tg: code/datums/elements/wearable_client_colour.dm:2
public partial class datum_element_wearable_client_colour : datum_element
{
    // GAP(types): tg's `colour_type` holds a typepath (`var/datum/client_colour/colour_type`, :6), but the generated var is typed as
    // an instance of the declared class (types-converter.md 3.5, V2); the typepath is kept here.
    private Type colour_type__path;

    // tg: code/datums/elements/wearable_client_colour.dm:14
    [DmParams("target", "colour_type", "equip_slots", "colour_source", "custom_colour", "forced", "comsig_toggle")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        Type colour_type = Dm.arg<Type>(__extra, 0);
        object equip_slots = Dm.arg(__extra, 1);
        object colour_source = Dm.arg(__extra, 2);
        object custom_colour = Dm.arg(__extra, 3);
        object forced = Dm.arg(__extra, 4);
        object comsig_toggle = Dm.arg(__extra, 5);
        forced ??= FALSE;
        comsig_toggle ??= COMSIG_CLICK_ALT;
        object dot = null;
        dot = await base.Attach(target, Dm.forward(__extra, colour_type, equip_slots, colour_source, custom_colour, forced, comsig_toggle));
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;

        obj_item item_target = (obj_item)target;
        RegisterSignal(target, COMSIG_ITEM_EQUIPPED, nameof(on_equipped));
        RegisterSignal(target, COMSIG_ITEM_DROPPED, nameof(on_dropped));

        colour_type__path = colour_type;
        this.equip_slots = Dm.num(equip_slots);
        this.colour_source = colour_source;
        this.custom_colour = custom_colour;
        this.forced = Dm.num(forced);

        if (!Dm.truthy(forced))
        {
            if (Dm.eq(comsig_toggle, COMSIG_CLICK_ALT))
                key_info = EXAMINE_HINT("Alt-Click");
            else if (Dm.eq(comsig_toggle, COMSIG_CLICK_ALT_SECONDARY))
                key_info = EXAMINE_HINT("Right-Alt-Click");
            else if (Dm.eq(comsig_toggle, COMSIG_CLICK_CTRL))
                key_info = EXAMINE_HINT("Ctrl-Click");
            else if (Dm.eq(comsig_toggle, COMSIG_CLICK_CTRL_SHIFT))
                key_info = EXAMINE_HINT("Ctrl-Shift-Click");
            else
            {
                stack_trace(Dm.text("Unsupported comsig_toggle arg value ([]) for [], defaulting to [COMSIG_CLICK_ALT]", comsig_toggle, type), "code/datums/elements/wearable_client_colour.dm", 38);
                key_info = EXAMINE_HINT("Alt-Click");
                comsig_toggle = COMSIG_CLICK_ALT;
            }
            RegisterSignal(target, comsig_toggle, nameof(toggle_see_worn_colors));
            RegisterSignal(target, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        }

        if (ismob(item_target.loc))
        {
            mob wearer = (mob)item_target.loc;
            if (Dm.truthy(Dm.bitand(wearer.get_slot_by_item(item_target), this.equip_slots)))
                try_client_colour(wearer);
        }
        return dot;
    }

    // tg: code/datums/elements/wearable_client_colour.dm:50
    [DmParams("source")]
    public override object Detach(datum source = null, params object[] __extra)
    {
        DmList fairly_long_list = Dm.list(
            COMSIG_ITEM_EQUIPPED,
            COMSIG_ITEM_DROPPED,
            COMSIG_CLICK_ALT,
            COMSIG_CLICK_ALT_SECONDARY,
            COMSIG_CLICK_CTRL,
            COMSIG_CLICK_CTRL_SHIFT,
            COMSIG_ATOM_EXAMINE
            );
        UnregisterSignal(source, fairly_long_list);
        obj_item source_item = (obj_item)source;
        if (ismob(source_item.loc))
        {
            mob wearer = (mob)source_item.loc;
            if (Dm.truthy(Dm.bitand(wearer.get_slot_by_item(source_item), equip_slots)))
                remove_client_colour(wearer);
        }
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/wearable_client_colour.dm:67
    [SignalHandler]
    public virtual void on_equipped(obj_item source = null, mob equipper = null, object slot = null)
    {
        if (Dm.truthy(Dm.bitand(slot, equip_slots)))
            try_client_colour(equipper);
    }

    // tg: code/datums/elements/wearable_client_colour.dm:72
    [SignalHandler]
    public virtual void on_dropped(obj_item source = null, mob dropper = null)
    {
        remove_client_colour(dropper);
    }

    // tg: code/datums/elements/wearable_client_colour.dm:77
    public virtual void try_client_colour(mob equipper = null)
    {
        if (!Dm.truthy(forced))
        {
            RegisterSignal(equipper, SIGNAL_ADDTRAIT(TRAIT_SEE_WORN_COLOURS), nameof(on_trait_added));
            RegisterSignal(equipper, SIGNAL_REMOVETRAIT(TRAIT_SEE_WORN_COLOURS), nameof(on_trait_removed));
            if (!HAS_TRAIT(equipper, TRAIT_SEE_WORN_COLOURS))
                return;
        }
        apply_client_colour(equipper);
    }

    // tg: code/datums/elements/wearable_client_colour.dm:86
    [SignalHandler]
    public virtual void on_trait_added(mob source = null, object trait = null)
    {
        apply_client_colour(source);
    }

    // tg: code/datums/elements/wearable_client_colour.dm:90
    public virtual void apply_client_colour(mob equipper = null)
    {
        object colour_to_add = colour_type__path;
        if (Dm.truthy(custom_colour))
        {
            datum_client_colour made = Dm.@new((Type)colour_to_add) as datum_client_colour;
            made.color = custom_colour;
            colour_to_add = made;
        }
        equipper.add_client_colour(colour_to_add, colour_source);
    }

    // tg: code/datums/elements/wearable_client_colour.dm:96
    [SignalHandler]
    public virtual void on_trait_removed(mob source = null, object trait = null)
    {
        source.remove_client_colour(colour_source);
    }

    // tg: code/datums/elements/wearable_client_colour.dm:100
    public virtual void remove_client_colour(mob dropper = null)
    {
        if (!Dm.truthy(forced))
        {
            UnregisterSignal(dropper, Dm.list(SIGNAL_ADDTRAIT(TRAIT_SEE_WORN_COLOURS), SIGNAL_REMOVETRAIT(TRAIT_SEE_WORN_COLOURS)));
            if (!HAS_TRAIT(dropper, TRAIT_SEE_WORN_COLOURS))
                return;
        }
        dropper.remove_client_colour(colour_source);
    }

    // tg: code/datums/elements/wearable_client_colour.dm:109
    [SignalHandler]
    public virtual object toggle_see_worn_colors(obj_item source = null, mob clicker = null)
    {
        if (source.loc != clicker || HAS_TRAIT(clicker, TRAIT_INCAPACITATED))
            return null;
        if (HAS_TRAIT(clicker, TRAIT_SEE_WORN_COLOURS))
        {
            REMOVE_TRAIT(clicker, TRAIT_SEE_WORN_COLOURS, CLOTHING_TRAIT);
            clicker.balloon_alert(clicker, I18n.T("glasses colors disabled"));
        }
        else
        {
            ADD_TRAIT(clicker, TRAIT_SEE_WORN_COLOURS, CLOTHING_TRAIT);
            clicker.balloon_alert(clicker, I18n.T("glasses colors enabled"));
        }
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/datums/elements/wearable_client_colour.dm:123
    [SignalHandler]
    public virtual void on_examine(obj_item source = null, mob user = null, DmList examine_texts = null)
    {
        Dm.append(ref examine_texts, span_info(I18n.T("While holding or wearing it, [] to toggle on/off the screen color from glasses and such.", key_info)));
    }
}
