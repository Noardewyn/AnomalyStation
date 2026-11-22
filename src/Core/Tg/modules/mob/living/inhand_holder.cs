// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/inhand_holder.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The base holder (gaps package C2: picking up the pets); its machine_wash is in washing_machine.cs (gaps H1). The drone's, snail's and purple raptor's holders stay with their mobs.

//Generic system for picking up mobs.
//Currently works for head and hands.
// tg: code/modules/mob/living/inhand_holder.dm:4
public partial class obj_item_mob_holder : obj_item
{
    // tg: code/modules/mob/living/inhand_holder.dm:16
    [DmParams("mapload", "held_mob", "worn_state", "head_icon", "lh_icon", "rh_icon", "worn_slot_flags")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        mob_living held_mob = Dm.arg<mob_living>(__extra, 0);
        object worn_state = Dm.arg(__extra, 1);
        object head_icon = Dm.arg(__extra, 2);
        object lh_icon = Dm.arg(__extra, 3);
        object rh_icon = Dm.arg(__extra, 4);
        object worn_slot_flags = Dm.arg(__extra, 5) ?? NONE;
        if (Dm.truthy(head_icon))
            worn_icon = head_icon;
        if (Dm.truthy(worn_state))
            inhand_icon_state = worn_state as string;
        if (Dm.truthy(lh_icon))
            lefthand_file = lh_icon;
        if (Dm.truthy(rh_icon))
            righthand_file = rh_icon;
        if (Dm.truthy(worn_slot_flags))
            slot_flags = Dm.num(worn_slot_flags);
        update_weight_class(held_mob.held_w_class);
        insert_mob(held_mob);
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/mob/living/inhand_holder.dm:31
    public override object Destroy(float? force = null)
    {
        if (held_mob?.loc == this)
            Waiting.NoSleep(release());
        held_mob = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/inhand_holder.dm:37
    public virtual object insert_mob(mob_living new_prisoner = null)
    {
        if (new_prisoner == null)
            return FALSE;
        Waiting.NoSleep(new_prisoner.setDir(SOUTH));
        update_visuals(new_prisoner);
        held_mob = new_prisoner;
        RegisterSignal(held_mob, COMSIG_QDELETING, nameof(on_mob_deleted));
        Waiting.NoSleep(new_prisoner.forceMove(this));
        name = new_prisoner.name;
        desc = new_prisoner.desc;
        return TRUE;
    }

    // tg: code/modules/mob/living/inhand_holder.dm:49
    [SignalHandler]
    public virtual void on_mob_deleted()
    {
        held_mob = null;
        if (isliving(loc))
        {
            mob_living holder = loc as mob_living;
            Waiting.NoSleep(holder.temporarilyRemoveItemFromInventory(this, force: TRUE));
        }
        qdel(this);
    }

    // tg: code/modules/mob/living/inhand_holder.dm:57
    public virtual void update_visuals(mob_living held_guy = null)
    {
        appearance = held_guy.appearance;
    }

    // tg: code/modules/mob/living/inhand_holder.dm:60
    public override async DmTask<object> on_thrown(mob_living_carbon user = null, atom target = null)
    {
        if (Dm.truthy(Dm.bitand(item_flags, ABSTRACT)) || HAS_TRAIT(this, TRAIT_NODROP))
            return null;
        if (HAS_TRAIT(user, TRAIT_PACIFISM))
        {
            to_chat(user, span_notice(Dm.text("You set [] down gently on the ground.", this)));
            await release();
            return null;
        }

        mob_living throw_mob = held_mob;
        await release();
        return throw_mob;
    }

    // tg: code/modules/mob/living/inhand_holder.dm:72
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (held_mob != null && isturf(loc))
            await release();
        return dot;
    }

    // tg: code/modules/mob/living/inhand_holder.dm:77
    public virtual async DmTask<object> release(float? display_messages = null)
    {
        display_messages ??= TRUE;
        if (held_mob == null)
        {
            if (!QDELETED(this))
                qdel(this);
            return FALSE;
        }
        mob_living released_mob = held_mob;
        if (isliving(loc))
        {
            mob_living captor = loc as mob_living;
            if (Dm.truthy(display_messages))
                to_chat(captor, span_warning(Dm.text("[] wriggles free!", released_mob)));
            await captor.dropItemToGround(this);
        }
        else
            await released_mob.forceMove(drop_location() as atom);
        await released_mob.reset_perspective();
        await released_mob.setDir(SOUTH);
        if (Dm.truthy(display_messages))
            released_mob.visible_message(span_warning(Dm.text("[] uncurls!", released_mob)));
        if (!QDELETED(this))
            qdel(this);
        return TRUE;
    }

    // tg: code/modules/mob/living/inhand_holder.dm:98
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        return await release();
    }

    // tg: code/modules/mob/living/inhand_holder.dm:101
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        return await release();
    }

    // tg: code/modules/mob/living/inhand_holder.dm:104
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (held_mob == gone)
            await release();
        return dot;
    }

    // tg: code/modules/mob/living/inhand_holder.dm:109
    public override async DmTask<object> on_found(mob finder = null)
    {
        if (held_mob != null && Dm.truthy(held_mob.will_escape_storage()))
        {
            to_chat(finder, span_warning(Dm.text("\\A [] pops out! ", held_mob.name)));
            finder.visible_message(span_warning(Dm.text("\\A [] pops out of the container [] is opening!", held_mob.name, finder)), ignored_mobs: finder);
            await release(display_messages: FALSE);
        }
        return null;
    }

    // tg: code/modules/mob/living/inhand_holder.dm:149
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot) || held_mob == null) //overriden or mob missing
            return dot;
        await user.UnarmedAttack(held_mob, proximity_flag: TRUE, modifiers: modifiers as DmList);
        return dot;
    }

    // tg: code/modules/mob/living/inhand_holder.dm:155
    public override async DmTask<object> base_item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        object dot = await base.base_item_interaction(user, tool, modifiers);
        if (Dm.truthy(dot) || held_mob == null) // Another interaction was performed
            return dot;
        await tool.melee_attack_chain(user, held_mob, modifiers); //Interact with the mob with our tool
        return dot;
    }

    // tg: code/modules/mob/living/inhand_holder.dm:161
    public override object IsContainedAtomAccessible(atom contained = null, atom_movable user = null)
    {
        return TRUE;
    }
}

// tg: code/modules/mob/living/inhand_holder.dm:116  (silicons SI7, D326: a drone picked up)
public partial class obj_item_mob_holder_drone : obj_item_mob_holder
{
    // tg: code/modules/mob/living/inhand_holder.dm:116
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        //If we're not being put onto a drone, end it all
        if (!isdrone(Dm.arg(__extra, 0)))
            return INITIALIZE_HINT_QDEL;
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/mob/living/inhand_holder.dm:122
    public override object insert_mob(mob_living new_prisoner = null)
    {
        object dot = base.insert_mob(new_prisoner);
        if (!isdrone(new_prisoner))
        {
            qdel(this);
            return dot;
        }
        name = "drone (hiding)";
        desc = "This drone is scared and has curled up into a ball!";
        return dot;
    }

    // tg: code/modules/mob/living/inhand_holder.dm:130
    public override void update_visuals(mob_living contained = null)
    {
        if (contained is not mob_living_basic_drone drone)
        {
            base.update_visuals(contained);
            return;
        }
        icon = Dm.resource("icons/mob/silicon/drone.dmi");
        icon_state = Dm.text("[]_hat", drone.visualAppearance);
    }
}

