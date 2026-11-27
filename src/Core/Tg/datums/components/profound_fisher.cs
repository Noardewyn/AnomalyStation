// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/profound_fisher.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: a mob, or gloves worn, that fish without a rod in hand through an internal one; a mob with no player only pretends,
// pulling a mindless reward after a wait its happiness shortens. The var blocks are generated (D111).

///component that allows player mobs to play the fishing minigame without a rod equipped, non-player mobs will "pretend" fish
// tg: code/datums/components/profound_fisher.dm:2
public partial class datum_component_profound_fisher : datum_component
{
    ///the fishing rod this mob will use (tg types it /obj/item/fishing_rod/mob_fisher, but the MOD fishing glove passes its own rod)
    // tg: code/datums/components/profound_fisher.dm:4
    public obj_item_fishing_rod our_rod;

    // tg: code/datums/components/profound_fisher.dm:8
    [DmParams("our_rod", "delete_rod_when_deleted")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object our_rod = Dm.arg(__extra, 0);
        object delete_rod_when_deleted = Dm.arg(__extra, 1) ?? TRUE;
        bool isgloves = istype(parent, typeof(obj_item_clothing_gloves));
        if (!isliving(parent) && !isgloves)
            return COMPONENT_INCOMPATIBLE;
        this.our_rod = Dm.truthy(our_rod) ? (obj_item_fishing_rod)our_rod : await Dm.new_async<obj_item_fishing_rod_mob_fisher>(parent);
        this.our_rod.@internal = TRUE;
        this.delete_rod_when_deleted = Dm.num(delete_rod_when_deleted);
        ADD_TRAIT(this.our_rod, TRAIT_NOT_BARFABLE, REF(this));
        RegisterSignal(this.our_rod, COMSIG_MOVABLE_MOVED, nameof(on_rod_moved));

        if (!isgloves)
        {
            RegisterSignal(parent, COMSIG_HOSTILE_PRE_ATTACKINGTARGET, nameof(pre_attack));
            RegisterSignal(parent, COMSIG_MOB_COMPLETE_FISHING, nameof(stop_fishing));
        }
        else
        {
            obj_item_clothing gloves = (obj_item_clothing)parent;
            RegisterSignal(gloves, COMSIG_ITEM_EQUIPPED, nameof(on_equip));
            RegisterSignal(gloves, COMSIG_ITEM_DROPPED, nameof(on_drop));
            RegisterSignal(gloves, COMSIG_ATOM_ATTACK_HAND_SECONDARY, nameof(open_rod_menu));
            RegisterSignal(gloves, COMSIG_ATOM_EXAMINE, nameof(on_examine));
            Dm.combine(ref gloves.flags_1, HAS_CONTEXTUAL_SCREENTIPS_1);
            RegisterSignal(gloves, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM, nameof(on_requesting_context_from_item));
            mob_living wearer = gloves.loc as mob_living;
            if (wearer != null && wearer.get_item_by_slot(ITEM_SLOT_GLOVES) == gloves)
                RegisterSignal(wearer, COMSIG_LIVING_UNARMED_ATTACK, nameof(on_unarmed_attack));
        }
        return null;
    }

    // tg: code/datums/components/profound_fisher.dm:34
    [SignalHandler]
    public virtual object on_requesting_context_from_item(datum source = null, DmList context = null, obj_item held_item = null, mob_living user = null)
    {
        if (held_item == null && Dm.truthy(user.contains(parent as atom)))
        {
            context[SCREENTIP_CONTEXT_RMB] = I18n.T("Open rod UI");
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return null;
    }

    // tg: code/datums/components/profound_fisher.dm:40
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(examine_list, span_info(I18n.T("When [] or [], [] with a empty hand to open the integrated fishing rod interface.", EXAMINE_HINT(I18n.T("held")), EXAMINE_HINT(I18n.T("equipped")), EXAMINE_HINT(I18n.T("right-click")))));
        Dm.append(examine_list, span_tinynoticeital(I18n.T("To fish, you need to turn combat mode off.")));
    }

    ///Handles replacing the fishing rod if somehow removed from the parent movable if delete_rod_when_deleted is TRUE, otherwise delete the component.
    // tg: code/datums/components/profound_fisher.dm:46
    [SignalHandler]
    public virtual void on_rod_moved(datum source = null)
    {
        if (QDELETED(this) || our_rod.loc == parent)
            return;
        if (Dm.truthy(delete_rod_when_deleted))
        {
            UnregisterSignal(our_rod, COMSIG_MOVABLE_MOVED);
            if (!QDELETED(our_rod))
                qdel(our_rod);
            our_rod = Dm.@new(our_rod.type, parent) as obj_item_fishing_rod;
        }
        else
            qdel(this);
    }

    // tg: code/datums/components/profound_fisher.dm:58
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(our_rod, COMSIG_MOVABLE_MOVED);
        if (!Dm.truthy(delete_rod_when_deleted))
        {
            our_rod.@internal = FALSE;
            REMOVE_TRAIT(our_rod, TRAIT_NOT_BARFABLE, REF(this));
        }
        else if (!QDELETED(our_rod))
            QDEL_NULL(ref our_rod);
        our_rod = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/profound_fisher.dm:68
    [SignalHandler]
    public virtual void on_equip(obj_item source = null, atom equipper = null, object slot = null)
    {
        if (!Dm.eq(slot, ITEM_SLOT_GLOVES))
            return;
        RegisterSignal(equipper, COMSIG_LIVING_UNARMED_ATTACK, nameof(on_unarmed_attack));
        RegisterSignal(equipper, COMSIG_MOB_COMPLETE_FISHING, nameof(stop_fishing));
    }

    // tg: code/datums/components/profound_fisher.dm:75
    [SignalHandler]
    public virtual object open_rod_menu(datum source = null, mob user = null, DmList modifiers = null)
    {
        Waiting.Detach(our_rod.ui_interact(user));
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/datums/components/profound_fisher.dm:80
    [SignalHandler]
    public virtual void on_drop(datum source = null, atom dropper = null)
    {
        UnregisterSignal(dropper, Dm.list(COMSIG_LIVING_UNARMED_ATTACK, COMSIG_MOB_COMPLETE_FISHING));
        REMOVE_TRAIT(dropper, TRAIT_PROFOUND_FISHER, TRAIT_GENERIC); //this will cancel the current minigame if the fishing rod was internal.
    }

    // tg: code/datums/components/profound_fisher.dm:85
    [SignalHandler]
    public virtual object on_unarmed_attack(mob_living source = null, atom attack_target = null, object proximity_flag = null, DmList modifiers = null)
    {
        if (!Dm.truthy(Waiting.NoSleep(should_fish_on(source, attack_target))))
            return null;
        if (source.client != null)
            Waiting.Detach(begin_fishing(source, attack_target));
        else
            Waiting.Detach(pretend_fish(source, attack_target));
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/datums/components/profound_fisher.dm:95
    [SignalHandler]
    public virtual object pre_attack(mob_living source = null, atom target = null)
    {
        if (!Dm.truthy(Waiting.NoSleep(should_fish_on(source, target))))
            return null;
        if (source.client != null)
            Waiting.Detach(begin_fishing(source, target));
        else
            Waiting.Detach(pretend_fish(source, target));
        return COMPONENT_HOSTILE_NO_ATTACK;
    }

    // tg: code/datums/components/profound_fisher.dm:106
    public virtual async DmTask<object> should_fish_on(mob_living user = null, atom target = null)
    {
        if (!HAS_TRAIT(target, TRAIT_FISHING_SPOT) || Dm.truthy(GLOB.fishing_challenges_by_user[user]))
            return FALSE;
        if (Dm.truthy(user.combat_mode) || !Dm.truthy(await target.IsReachableBy(user)))
            return FALSE;
        return TRUE;
    }

    // tg: code/datums/components/profound_fisher.dm:113
    public virtual async DmTask begin_fishing(mob_living user = null, atom target = null)
    {
        await our_rod.melee_attack_chain(user, target);
        ADD_TRAIT(user, TRAIT_PROFOUND_FISHER, TRAIT_GENERIC);
    }

    // tg: code/datums/components/profound_fisher.dm:117
    [SignalHandler]
    public virtual void stop_fishing(datum source = null)
    {
        REMOVE_TRAIT(source, TRAIT_PROFOUND_FISHER, TRAIT_GENERIC);
    }

    // tg: code/datums/components/profound_fisher.dm:121
    public virtual async DmTask pretend_fish(mob_living source = null, atom target = null)
    {
        if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(source, target)))
            return;
        DmList fish_spot_container = Dm.list_n(NPC_FISHING_SPOT);
        target.SEND_SIGNAL(COMSIG_NPC_FISHING, Dm.list(target, fish_spot_container));
        datum_fish_source fish_spot = fish_spot_container[NPC_FISHING_SPOT] as datum_fish_source;
        if (fish_spot == null)
            return;
        obj_effect_fishing_float @float = await Dm.new_async<obj_effect_fishing_float>(get_turf(target), target);
        playsound(@float, Dm.resource("sound/effects/splash.ogg"), 100);
        if (!Dm.truthy(FALSE)) // PERFORM_ALL_TESTS(fish_sources) is FALSE in production (rules 1.7)
        {
            float happiness_percentage = Dm.num((source.ai_controller as datum_ai_controller)?.blackboard[BB_BASIC_HAPPINESS]) * 0.01f;
            float fishing_speed = 10 * SECONDS - round(4 * SECONDS * happiness_percentage);
            if (!Dm.truthy(await do_after(source, fishing_speed, target: target)) && !QDELETED(fish_spot))
            {
                qdel(@float);
                return;
            }
        }
        object reward_loot = await fish_spot.roll_mindless_reward(our_rod, source, target);
        await fish_spot.dispense_reward(reward_loot, source, target);
        playsound(@float, Dm.resource("sound/effects/bigsplash.ogg"), 100);
        qdel(@float);
    }
}
