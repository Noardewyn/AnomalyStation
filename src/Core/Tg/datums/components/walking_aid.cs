// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/walking_aid.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J3, D298: the scythe carries it). The var block is generated.

/**
 * Walking Aid Component
 *
 * Add this to an item to allow it to act as a cane (or crutch) while held.
 * Items with this component will help mobs avoid limping from broken
 * leg bones, and lessen the slowdown caused by missing legs.
 *
 * Used by canes, crutches, and pole-like items such as spears and staffs.
 */
// tg: code/datums/components/walking_aid.dm:11
public partial class datum_component_walking_aid : datum_component
{
    // tg: code/datums/components/walking_aid.dm:21
    [DmParams("limbless_slowdown_modifier", "required_trait", "waddling")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;

        this.waddling = Dm.num(Dm.arg(__extra, 2) ?? FALSE);
        this.required_trait = Dm.arg(__extra, 1);
        this.limbless_slowdown_modifier = Dm.num(Dm.arg(__extra, 0) ?? 0.6f);
        return null;
    }

    // tg: code/datums/components/walking_aid.dm:29
    public override object Destroy(float? force = null)
    {
        Waiting.NoSleep(remove_support());
        return base.Destroy(force);
    }

    // tg: code/datums/components/walking_aid.dm:33
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(on_equip));
        RegisterSignal(parent, COMSIG_ITEM_DROPPED, nameof(on_drop));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE_TAGS, nameof(get_examine_tags));
        RegisterSignal(parent, SIGNAL_ADDTRAIT(TRAIT_WIELDED), nameof(update_legs));
        RegisterSignal(parent, SIGNAL_REMOVETRAIT(TRAIT_WIELDED), nameof(update_legs));
        RegisterSignal(parent, SIGNAL_ADDTRAIT(Dm.str(required_trait)), nameof(update_legs));
        RegisterSignal(parent, SIGNAL_REMOVETRAIT(Dm.str(required_trait)), nameof(update_legs));
    }

    // tg: code/datums/components/walking_aid.dm:42
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_ITEM_EQUIPPED,
            COMSIG_ITEM_DROPPED,
            COMSIG_ATOM_EXAMINE_TAGS,
            SIGNAL_ADDTRAIT(TRAIT_WIELDED),
            SIGNAL_REMOVETRAIT(TRAIT_WIELDED),
            SIGNAL_ADDTRAIT(Dm.str(required_trait)),
            SIGNAL_REMOVETRAIT(Dm.str(required_trait))));
        Waiting.NoSleep(remove_support());
    }

    // tg: code/datums/components/walking_aid.dm:54
    [SignalHandler]
    public virtual void on_equip(datum source = null, mob equipper = null, object slot = null)
    {
        Waiting.NoSleep(remove_support());
        if (!Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HANDS)))
            return;
        if (!isliving(equipper))
            return;

        Waiting.NoSleep(apply_support((mob_living)equipper));
    }

    // tg: code/datums/components/walking_aid.dm:65
    [SignalHandler]
    public virtual void on_drop(datum source = null, mob user = null)
    {
        Waiting.NoSleep(remove_support());
    }

    // tg: code/datums/components/walking_aid.dm:69
    [SignalHandler]
    public virtual void get_examine_tags(atom source = null, mob user = null, DmList examine_list = null)
    {
        examine_list["walking-aid"] = I18n.T("It can help lessen the slowdown caused from a missing or injured leg, when held on the same side as the injury.");
    }

    // Updates our leg status when wielded/unwielded a two handed walking aid like a spear
    // tg: code/datums/components/walking_aid.dm:75
    [SignalHandler]
    public virtual void update_legs(atom source = null)
    {
        mob_living user = current_user_ref?.resolve() as mob_living;
        if (user != null)
            Waiting.NoSleep(user.update_usable_leg_status());
    }

    // tg: code/datums/components/walking_aid.dm:81
    public virtual async DmTask apply_support(mob_living user = null)
    {
        if (current_user_ref != null)
            await remove_support();

        current_user_ref = WEAKREF(user) as datum_weakref;
        RegisterSignal(user, COMSIG_CARBON_LIMPING, nameof(handle_limping));
        RegisterSignal(user, COMSIG_LIVING_LIMBLESS_SLOWDOWN, nameof(handle_slowdown));
        await user.update_usable_leg_status();

        if (Dm.truthy(waddling))
            Dm.Deferred("code/datums/components/walking_aid.dm:91", "apply_support: AddElementTrait(TRAIT_WADDLING, REF(src), /datum/element/waddling): no walking aid of the port waddles yet");
    }

    // tg: code/datums/components/walking_aid.dm:93
    public virtual async DmTask remove_support()
    {
        mob_living user = current_user_ref?.resolve() as mob_living;
        current_user_ref = null;
        if (user == null)
            return;
        UnregisterSignal(user, Dm.list(COMSIG_CARBON_LIMPING, COMSIG_LIVING_LIMBLESS_SLOWDOWN));
        await user.update_usable_leg_status();

        if (Dm.truthy(waddling))
            REMOVE_TRAIT(user, TRAIT_WADDLING, REF(this));
    }

    // tg: code/datums/components/walking_aid.dm:104
    public virtual object is_active()
    {
        // if both hands are holding it, then it is not being used for support
        if (HAS_TRAIT(parent, TRAIT_WIELDED))
            return FALSE;

        if (required_trait == null)
            return TRUE;

        return HAS_TRAIT(parent, Dm.str(required_trait));
    }

    // tg: code/datums/components/walking_aid.dm:114
    [SignalHandler]
    public virtual object handle_limping(mob_living user = null, obj_item_bodypart limping_leg = null)
    {
        if (!Dm.truthy(is_active()))
            return NONE;
        if (limping_leg == null)
            return NONE;

        object supported_zone = get_supported_leg_zone(user);
        if (supported_zone == null)
            return NONE;
        if (!Dm.eq(limping_leg.body_zone, supported_zone))
            return NONE;

        return COMPONENT_CANCEL_LIMP;
    }

    // tg: code/datums/components/walking_aid.dm:130
    [SignalHandler]
    public virtual void handle_slowdown(mob_living user = null, object limbless_slowdown = null, DmList slowdown_mods = null)
    {
        if (!Dm.truthy(is_active()))
            return;
        if (!iscarbon(user))
            return;
        mob_living_carbon carbon_user = (mob_living_carbon)user;
        float leg_amount = carbon_user.usable_legs;
        if (leg_amount >= carbon_user.default_num_legs)
            return;
        if (!Dm.truthy(leg_amount)) // someday support dual-wielding crutches but for now they are destined to waddle
            return;

        object supported_zone = get_supported_leg_zone(user);
        if (supported_zone == null)
            return;
        if (Dm.truthy(carbon_user.get_bodypart(Dm.str(supported_zone)))) // make sure their leg is actually missing
            return;

        slowdown_mods.Add(limbless_slowdown_modifier);
    }

    // tg: code/datums/components/walking_aid.dm:152
    public virtual object get_supported_leg_zone(mob_living user = null)
    {
        object held_hand_zone = user.get_hand_zone_of_item((obj_item)parent);

        if (Dm.eq(held_hand_zone, BODY_ZONE_R_ARM))
            return BODY_ZONE_R_LEG;
        if (Dm.eq(held_hand_zone, BODY_ZONE_L_ARM))
            return BODY_ZONE_L_LEG;
        return null;
    }
}
