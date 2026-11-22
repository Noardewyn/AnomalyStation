// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/slippery_item.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N3, D316: an item coated in space lube slips out of the hands). The var block is generated (D111).

/// Item is slippery - picking it up or using it may cause it to immediately fall out of the user's hands
// tg: code/datums/components/slippery_item.dm:2
public partial class datum_component_slippery_item : datum_component
{
    // tg: code/datums/components/slippery_item.dm:14
    [DmParams("fall_chance", "fall_catch_chance", "examine_msg", "duration", "wash_flags")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object fall_chance = Dm.arg(__extra, 0) ?? 50;
        object fall_catch_chance = Dm.arg(__extra, 1) ?? 0;
        object examine_msg = Dm.arg(__extra, 2);
        object duration = Dm.arg(__extra, 3) ?? INFINITY;
        object wash_flags = Dm.arg(__extra, 4) ?? NONE;
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;

        this.fall_chance = Dm.num(fall_chance);
        this.fall_catch_chance = Dm.num(fall_catch_chance);
        this.examine_msg = Dm.truthy(examine_msg) ? examine_msg : "It looks very slippery, and may fall out of your hands when you try to use it.";
        this.wash_flags = Dm.num(wash_flags);
        if (!Dm.eq(duration, INFINITY))
        {
            float time__1 = Dm.num(duration);
            // QDEL_IN(src, duration) (__DEFINES/qdel.dm:60), inline (rules 11.1 (f))
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), time__1 > GC_FILTER_QUEUE ? WEAKREF(this) : this), time__1,
                file: "code/datums/components/slippery_item.dm", line: 23);
        }
        return null;
    }

    // tg: code/datums/components/slippery_item.dm:25
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(parent, COMSIG_COMPONENT_CLEAN_ACT, nameof(on_cleaned));
        // bunch of generic "item used" triggers, feel free to expand
        RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(on_equip));
        RegisterSignal(parent, COMSIG_ITEM_PRE_ATTACK, nameof(on_preattack));
        RegisterSignal(parent, COMSIG_ITEM_AFTERATTACK, nameof(on_afterattack));
        RegisterSignal(parent, COMSIG_GUN_TRY_FIRE, nameof(on_tryfire));
        RegisterSignal(parent, COMSIG_GRENADE_ARMED, nameof(on_grenade_arm));
        RegisterSignal(parent, COMSIG_ITEM_USED_IN_SURGERY, nameof(on_surgery_started));
    }

    // tg: code/datums/components/slippery_item.dm:36
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_ATOM_EXAMINE,
            COMSIG_COMPONENT_CLEAN_ACT,
            COMSIG_ITEM_EQUIPPED,
            COMSIG_ITEM_PRE_ATTACK,
            COMSIG_ITEM_AFTERATTACK,
            COMSIG_GUN_TRY_FIRE,
            COMSIG_GRENADE_ARMED,
            COMSIG_ITEM_USED_IN_SURGERY));
    }

    // tg: code/datums/components/slippery_item.dm:48
    [SignalHandler]
    public virtual void on_examine(obj_item source = null, mob_living user = null, DmList examine_list = null)
    {
        Dm.append(examine_list, examine_msg);
    }

    // tg: code/datums/components/slippery_item.dm:53
    [SignalHandler]
    public virtual object on_cleaned(obj_item source = null, object clean_flags = null)
    {
        if (Dm.truthy(wash_flags) && Dm.truthy(Dm.bitand(wash_flags, clean_flags)))
        {
            qdel(this);
            return COMPONENT_CLEANED;
        }
        return null;
    }

    // tg: code/datums/components/slippery_item.dm:60
    [SignalHandler]
    public virtual void on_equip(obj_item source = null, mob_living user = null, object slot = null)
    {
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HANDS)))
            try_fall(source, user);
    }

    // tg: code/datums/components/slippery_item.dm:66
    [SignalHandler]
    public virtual object on_preattack(obj_item source = null, atom target = null, mob_living user = null, params object[] __extra)
    {
        if (Dm.truthy(try_fall(source, user)))
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        return null;
    }

    // tg: code/datums/components/slippery_item.dm:72
    [SignalHandler]
    public virtual void on_afterattack(obj_item source = null, atom target = null, mob_living user = null, params object[] __extra)
    {
        try_fall(source, user);
    }

    // tg: code/datums/components/slippery_item.dm:77
    [SignalHandler]
    public virtual object on_tryfire(obj_item source = null, mob_living user = null, params object[] __extra)
    {
        if (Dm.truthy(try_fall(source, user)))
            return COMPONENT_CANCEL_GUN_FIRE;
        return null;
    }

    // tg: code/datums/components/slippery_item.dm:83
    [SignalHandler]
    public virtual void on_grenade_arm(obj_item source = null, params object[] __extra)
    {
        if (isliving(source.loc))
            try_fall(source, source.loc as mob_living);
    }

    // tg: code/datums/components/slippery_item.dm:89
    [SignalHandler]
    public virtual object on_surgery_started(obj_item source = null, datum surgery = null, atom_movable operating_on = null, mob_living surgeon = null)
    {
        if (Dm.truthy(try_fall(source, surgeon)))
            return ITEM_INTERACT_BLOCKING;
        return null;
    }

    /// Check for falling and handle it if it happens.
    /// Returns TRUE if the item fell, FALSE otherwise
    // tg: code/datums/components/slippery_item.dm:97
    public virtual object try_fall(obj_item source = null, mob_living user = null)
    {
        return Waiting.WaitforFalse(try_fall__body(source, user), null); // set waitfor = FALSE
    }

    private async DmTask<object> try_fall__body(obj_item source, mob_living user)
    {
        // prevents fall -> catch -> fall -> catch. even though that'd be funny, you wouldn't even be able to see it happening.
        if (Dm.eq(last_fall, world.time))
            return FALSE;
        if (!prob(fall_chance))
            return FALSE;
        if (source.loc != user || !Dm.truthy(await user.dropItemToGround(source)))
            return FALSE;
        if (QDELETED(source))
            return FALSE; // dropdel

        playsound(source, Dm.resource("sound/misc/slip.ogg"), 20, TRUE, SILENCED_SOUND_EXTRARANGE);
        source.SpinAnimation(4, 1);

        last_fall = world.time;
        if (prob(fall_catch_chance) && !HAS_TRAIT(user, TRAIT_CLUMSY))
        {
            foreach (object empty_hand in Dm.each(user.get_empty_held_indexes()))
            {
                if (Dm.eq(empty_hand, user.active_hand_index))
                    continue;
                if (Dm.truthy(await user.putItemFromInventoryInHandIfPossible(source, empty_hand)))
                    to_chat(user, span_notice(I18n.T("[] slips out of your hands - but you manage to catch it, just in time.", source)));
                return TRUE;
            }
        }

        to_chat(user, span_warning(I18n.T("[] slips out of your hands!", source)));
        return TRUE;
    }
}
