// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/hot_potato.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4a, D316). The bomber log is a leaf. The var
// block is generated (D111).

//CREATOR'S NOTE: DO NOT FUCKING GIVE THIS TO BOTANY!
// tg: code/game/objects/items/hot_potato.dm:2
public partial class obj_item_hot_potato : obj_item
{
    // tg: code/game/objects/items/hot_potato.dm:32
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(active))
            deactivate();
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/hot_potato.dm:37
    public virtual void colorize(mob target = null)
    {
        //Clear color from old target
        if (current != null)
        {
            if (current.resolve() is mob M)
                Waiting.NoSleep(M.remove_atom_colour(FIXED_COLOUR_PRIORITY));
        }
        //Give to new target
        current = null;
        //Swap colors
        color_val = Dm.truthy(color_val) ? FALSE : TRUE;
        if (target != null)
        {
            current = WEAKREF(target) as datum_weakref;
            Waiting.NoSleep(target.add_atom_colour(Dm.truthy(color_val) ? "#ffff00" : "#00ffff", FIXED_COLOUR_PRIORITY));
        }
    }

    // tg: code/game/objects/items/hot_potato.dm:51
    public virtual async DmTask detonate()
    {
        atom location = loc;
        location.visible_message(span_userdanger(I18n.T("[] []!", this, Dm.truthy(detonate_explosion) ? "explodes" : "activates")), span_userdanger(I18n.T("[] activates! You've ran out of time!", this)));
        if (Dm.truthy(detonate_explosion) && isliving(loc))
        {
            mob_living victim_mob = loc as mob_living;
            if (Dm.truthy(victim_mob.is_holding(this)))
                await victim_mob.gib(DROP_ALL_REMAINS);
            await explosion(this, detonate_dev_range, detonate_heavy_range, detonate_light_range, detonate_fire_range, detonate_flash_range);
        }
        deactivate();
        if (!Dm.truthy(reusable))
        {
            if (loc is mob M)
                await M.dropItemToGround(this, TRUE);
            qdel(this);
        }
    }

    // tg: code/game/objects/items/hot_potato.dm:66
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(activate(timer, user)))
        {
            user.visible_message(span_boldwarning(I18n.T("[] squeezes [], which promptly starts to flash red-hot colors!", user, this)), span_boldwarning(I18n.T("You squeeze [], activating its countdown and attachment mechanism!", this)),
                span_boldwarning(I18n.T("You hear a mechanical click and a loud beeping!")));
            return null;
        }
        return await base.attack_self(user, modifiers, __extra);
    }

    // tg: code/game/objects/items/hot_potato.dm:73
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!isliving(loc))
            return null;
        mob_living L = loc as mob_living;
        colorize(L);
        if (!Dm.truthy(stimulant))
            return null;
        Waiting.NoSleep(L.SetStun(0));
        Waiting.NoSleep(L.SetKnockdown(0));
        Waiting.NoSleep(L.SetSleeping(0));
        Waiting.NoSleep(L.SetImmobilized(0));
        Waiting.NoSleep(L.SetParalyzed(0));
        Waiting.NoSleep(L.SetUnconscious(0));
        datum_reagents holder = (datum_reagents)L.reagents;
        Waiting.NoSleep(holder.add_reagent(typeof(datum_reagent_medicine_muscle_stimulant), clamp(5 - Dm.num(holder.get_reagent_amount(typeof(datum_reagent_medicine_muscle_stimulant))), 0, 5))); //If you don't have legs or get bola'd, tough luck!
        return null;
    }

    // tg: code/game/objects/items/hot_potato.dm:89
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(active))
        {
            dot.Add(span_warning(I18n.T("[] is flashing red-hot! You should probably get rid of it!", this)));
            if (Dm.truthy(show_timer))
                dot.Add(span_warning(I18n.T("[]'s timer looks to be at []!", this, DisplayTimeText(activation_time - Dm.num(world.time)))));
        }
        return dot;
    }

    // tg: code/game/objects/items/hot_potato.dm:96
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(active))
            to_chat(user, span_userdanger(I18n.T("You have a really bad feeling about []!", this)));
    }

    // tg: code/game/objects/items/hot_potato.dm:101
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        object dot = await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;

        return await force_onto(target_mob, user);
    }

    // tg: code/game/objects/items/hot_potato.dm:108
    public virtual async DmTask<object> force_onto(mob_living victim = null, mob user = null)
    {
        if (victim == null || user != loc || victim == user)
            return FALSE;
        if (victim.client == null)
            to_chat(user, span_boldwarning(I18n.T("[] refuses to attach to a non-sapient creature!", this)));
        if (IS_UNCONSCIOUS_OR_CRIT(victim) || !Dm.truthy(victim.usable_legs))
            to_chat(user, span_boldwarning(I18n.T("[] refuses to attach to someone incapable of using it!", this)));
        await user.temporarilyRemoveItemFromInventory(this, TRUE);
        object dot = FALSE;
        if (!Dm.truthy(await victim.put_in_hands(this)))
        {
            if (Dm.truthy(forceful_attachment))
            {
                await victim.dropItemToGround(victim.get_inactive_held_item() as obj_item);
                if (!Dm.truthy(await victim.put_in_hands(this)))
                {
                    await victim.dropItemToGround(victim.get_active_held_item() as obj_item);
                    if (Dm.truthy(await victim.put_in_hands(this)))
                        dot = TRUE;
                }
                else
                    dot = TRUE;
            }
        }
        else
            dot = TRUE;
        if (Dm.truthy(dot))
        {
            log_combat(user, victim, Dm.text("forced a hot potato with explosive variables ([]-[]/[]/[]/[]/[]) onto", detonate_explosion, detonate_dev_range, detonate_heavy_range, detonate_light_range, detonate_flash_range, detonate_fire_range));
            user.visible_message(span_userdanger(I18n.T("[] forces [] onto []!", user, this, victim)), span_userdanger(I18n.T("You force [] onto []!", this, victim)), span_boldwarning(I18n.T("You hear a mechanical click and a beep.")));
            colorize(null);
        }
        else
        {
            log_combat(user, victim, Dm.text("tried to force a hot potato with explosive variables ([]-[]/[]/[]/[]/[]) onto", detonate_explosion, detonate_dev_range, detonate_heavy_range, detonate_light_range, detonate_flash_range, detonate_fire_range));
            user.visible_message(span_boldwarning(I18n.T("[] tried to force [] onto [], but it could not attach!", user, this, victim)), span_boldwarning(I18n.T("You try to force [] onto [], but it is unable to attach!", this, victim)), span_boldwarning(I18n.T("You hear a mechanical click and two buzzes.")));
            await user.put_in_hands(this);
        }
        return dot;
    }

    // tg: code/game/objects/items/hot_potato.dm:137
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        colorize(null);
        return dot;
    }

    // tg: code/game/objects/items/hot_potato.dm:141
    public virtual object activate(object delay = null, mob user = null)
    {
        if (Dm.truthy(active))
            return null;
        update_appearance();
        if (Dm.truthy(sticky))
            ADD_TRAIT(this, TRAIT_NODROP, HOT_POTATO_TRAIT);
        name = Dm.text("primed []", name);
        activation_time = Dm.num(timer) + Dm.num(world.time);
        detonation_timerid = _addtimer(Dm.@new<datum_callback>(this, nameof(detonate)), Dm.num(delay), TIMER_STOPPABLE, file: "code/game/objects/items/hot_potato.dm", line: 149);
        START_PROCESSING(SSfastprocess, this);
        if (user != null)
            log_bomber(user, "has primed a", this, Dm.text("for detonation (Timer:[],Explosive:[],Range:[]/[]/[]/[])", delay, detonate_explosion, detonate_dev_range, detonate_heavy_range, detonate_light_range, detonate_fire_range));
        else
            log_bomber(null, null, this, Dm.text("was primed for detonation (Timer:[],Explosive:[],Range:[]/[]/[]/[])", delay, detonate_explosion, detonate_dev_range, detonate_heavy_range, detonate_light_range, detonate_fire_range));
        active = TRUE;
        if (Dm.truthy(detonate_explosion)) //doesn't send a notification unless it's a genuine, exploding hot potato.
        {
            notify_ghosts(
                I18n.T("[] has primed a Hot Potato!", user.real_name),
                source: this,
                header: "Hot Hot Hot!"
            );
        }
        return null;
    }

    // tg: code/game/objects/items/hot_potato.dm:163
    public virtual void deactivate()
    {
        update_appearance();
        name = Dm.initial(this, (obj_item_hot_potato p) => p.name);
        REMOVE_TRAIT(this, TRAIT_NODROP, HOT_POTATO_TRAIT);
        deltimer(detonation_timerid);
        STOP_PROCESSING(SSfastprocess, this);
        detonation_timerid = null;
        colorize(null);
        active = FALSE;
    }

    // tg: code/game/objects/items/hot_potato.dm:173
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.truthy(active) ? icon_on : icon_off;
        return await base.update_icon_state(__extra);
    }
}
