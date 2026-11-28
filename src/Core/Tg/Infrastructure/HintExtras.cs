using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// DEVIATION(DV697) (ui-overhaul.md section 12, "The hints, improved"; protocol version 100's ContextHints tail): what the prompt beside the crosshair
// and the hint strip over the hands show beyond tg's screentip of the hovered thing: tg's screentip line for the same thing with an empty hand (the
// world interact key's), the held item's own lines (tg's screentip of the item hovered in hand), whether tg's quick equip would put it anywhere, and
// whether the empty hand's click on a turf moves the pulled thing. Host code, not tg code: it asks tg's own context builders and checks, as tg's
// on_mouse_enter (_atom.dm:868) and quick equip (inventory.dm:600) would, without acting. Called in the tick, from the screentip sink.

public static class HintExtras
{
    /// <summary>
    /// tg's screentip context of <paramref name="target"/> for <paramref name="user"/> holding <paramref name="held"/> (null: an empty hand), gathered
    /// as tg's <c>on_mouse_enter</c> gathers it (the target's <c>COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM</c> and the held item's
    /// <c>COMSIG_ITEM_REQUESTING_CONTEXT_FOR_TARGET</c>, behind the same flags); null when tg would show no context.
    /// </summary>
    public static DmList Context(atom target, mob user, obj_item held)
    {
        if (target == null || user == null || QDELETED(target))
            return null;
        // tg: _atom.dm:908
        if (!Dm.truthy(Dm.bitand(target.flags_1, HAS_CONTEXTUAL_SCREENTIPS_1)) && !Dm.truthy(Dm.bitand(held?.item_flags, ITEM_HAS_CONTEXTUAL_SCREENTIPS)))
            return null;
        DmList context = Dm.list();
        object returns = Dm.bitor(
            !Dm.truthy(target._listen_lookup?[COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM]) ? (object)NONE : target._SendSignal(COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM, Dm.list(target, context, held, user)),
            held == null || !Dm.truthy(held._listen_lookup?[COMSIG_ITEM_REQUESTING_CONTEXT_FOR_TARGET]) ? (object)NONE : held._SendSignal(COMSIG_ITEM_REQUESTING_CONTEXT_FOR_TARGET, Dm.list(held, context, target, user)));
        return Dm.truthy(Dm.bitand(returns, CONTEXTUAL_SCREENTIP_SET)) ? context : null;
    }

    // The label tg's context gives the key, or null.
    private static string Label(DmList context, string key) => context != null && Dm.@in(key, context) && context[key] is string { Length: > 0 } label ? label : null;

    /// <summary>
    /// The world interact key's line on <paramref name="target"/>: tg's left-click label for it with an empty hand, only while the active hand holds an
    /// item (with an empty hand the left click's own line says it) and the key would act there (<see cref="WorldInteract.Acts"/>, in reach of an empty
    /// hand); null otherwise or when tg gives that click no label.
    /// </summary>
    public static string EmptyHandLabel(mob_living user, atom target)
    {
        if (user == null || target == null || user.get_active_held_item() == null || !WorldInteract.Acts(user, target))
            return null;
        if (!Dm.@in(target, user.DirectAccess()) && !Dm.truthy(Waiting.NoSleep(target.IsReachableBy(user, null))))
            return null;
        return Label(Context(target, user, null), SCREENTIP_CONTEXT_LMB);
    }

    /// <summary>
    /// The held item's own lines: tg's activate in hand (the label tg's screentip gives the item hovered in hand, else "Use" where its type or a component
    /// has an <c>attack_self</c>), tg's alt-click on it (likewise, else "Alt use" where it has a <c>click_alt</c>), and whether tg's quick equip would put
    /// it somewhere (a human's only: tg's quick-equip key is the human's). Nulls and false with an empty active hand.
    /// </summary>
    public static (string Use, string Alt, bool Equips) Held(mob_living user)
    {
        if (user?.get_active_held_item() is not obj_item item || QDELETED(item))
            return (null, null, false);
        DmList context = Context(item, user, item);
        string use = Label(context, SCREENTIP_CONTEXT_LMB);
        if (use == null && (InertSeam.TgDefiningClass(item, "attack_self") is Type self && self != typeof(obj_item) || Dm.truthy(item._listen_lookup?[COMSIG_ITEM_ATTACK_SELF])))
            use = I18n.T("Use");
        string alt = Label(context, SCREENTIP_CONTEXT_ALT_LMB);
        if (alt == null && (InertSeam.TgDefiningClass(item, "click_alt") is Type click && click != typeof(atom) || Dm.truthy(item._listen_lookup?[COMSIG_CLICK_ALT])))
            alt = I18n.T("Alt use");
        return (use, alt, user is mob_living_carbon_human && QuickEquips(user, item));
    }

    // tg's equip_to_best_slot (inventory.dm:600) asked without acting: a slot of equip_to_appropriate_slot's order whose mob_can_equip takes it,
    // else (not for an item with an equip delay) the open storage, the other held items and the storages worn on the belt, the back, the dextrous
    // storage, the suit and the uniform, whose can_insert takes it, all with tg's messages off.
    private static bool QuickEquips(mob_living user, obj_item item)
    {
        DmList slots = Dm.truthy(item.slot_equipment_priority) ? item.slot_equipment_priority : Dm.list(
            ITEM_SLOT_BACK, ITEM_SLOT_ID, ITEM_SLOT_ICLOTHING, ITEM_SLOT_OCLOTHING, ITEM_SLOT_MASK, ITEM_SLOT_HEAD, ITEM_SLOT_NECK,
            ITEM_SLOT_FEET, ITEM_SLOT_GLOVES, ITEM_SLOT_EARS, ITEM_SLOT_EYES, ITEM_SLOT_BELT, ITEM_SLOT_SUITSTORE, ITEM_SLOT_LPOCKET, ITEM_SLOT_RPOCKET,
            ITEM_SLOT_DEX_STORAGE);
        foreach (object slot in Dm.each(slots))
        {
            if (Dm.truthy(Waiting.NoSleep(item.mob_can_equip(user, slot, disable_warning: 1))))
                return true;
        }
        if (Dm.truthy(item.equip_delay_self))
            return false;
        if (Dm.truthy(user.active_storage?.can_insert(item, user, messages: 0)))
            return true;
        var gear = new List<obj_item>();
        foreach (obj_item other in Dm.each_cast<obj_item>(user.held_items))
        {
            if (other != item)
                gear.Add(other);
        }
        foreach (object slot in new object[] { ITEM_SLOT_BELT, ITEM_SLOT_BACK, ITEM_SLOT_DEX_STORAGE, ITEM_SLOT_OCLOTHING, ITEM_SLOT_ICLOTHING })
        {
            if (user.get_item_by_slot(slot) is obj_item worn)
                gear.Add(worn);
        }
        foreach (obj_item g in gear)
        {
            if (Dm.truthy(g?.atom_storage?.can_insert(item, user, messages: 0)))
                return true;
        }
        return false;
    }

    /// <summary>
    /// The move-pulled key's line on <paramref name="target"/>: a turf, while <paramref name="user"/> pulls something with the active hand empty (tg's
    /// empty-hand click on a turf is <c>Move_Pulled</c>, turf.dm:248). Reach is the hover's (DV691: an answer out of reach is empty).
    /// </summary>
    public static bool MovesPulled(mob_living user, atom target)
        => target is turf && user?.pulling != null && user.get_active_held_item() == null;
}
