// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/pet_carrier.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

//Used to transport little animals without having to drag them across the station.
//Comes with a handy lock to prevent them from running off.
// tg: code/game/objects/items/pet_carrier.dm:5
public partial class obj_item_pet_carrier : obj_item
{
    private bool pet_carrier_full() => length(occupants) >= Dm.num(max_occupants) || Dm.num(occupant_weight) >= Dm.num(max_occupant_weight);

    // tg: code/game/objects/items/pet_carrier.dm:48
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // register_context(): the context tips (screentips, not now); AddElement(/datum/element/cuffable_item): not-translated.md 114
        return dot;
    }

    // tg: code/game/objects/items/pet_carrier.dm:53
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(length(occupants)))
        {
            foreach (object V in Dm.each(occupants.Copy()))
                Waiting.NoSleep(remove_occupant(V as mob_living));
        }
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/pet_carrier.dm:59
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (isliving(gone) && Dm.@in(gone, occupants))
        {
            mob_living living_gone = gone as mob_living;
            occupants.Remove(gone);
            occupant_weight = Dm.num(occupant_weight) - Dm.num(living_gone.mob_size);
        }
        return dot;
    }

    // tg: code/game/objects/items/pet_carrier.dm:66
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(length(occupants)))
        {
            foreach (object V in Dm.each(occupants))
                dot.Add(span_notice(I18n.T("It has [] inside.", V)));
        }
        else
            dot.Add(span_notice(I18n.T("It has nothing inside.")));

        // At some point these need to be converted to contextual screentips
        dot.Add(span_notice(I18n.T("Activate it in your hand to [] its door. Click-drag onto floor to release its occupants.", Dm.truthy(open) ? "close" : "open")));
        if (!Dm.truthy(open) && Dm.truthy(allows_locking))
            dot.Add(span_notice(I18n.T("Alt-click to [] its door.", Dm.truthy(locked) ? "unlock" : "lock")));
        return dot;
    }

    // tg: code/game/objects/items/pet_carrier.dm:80
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(open))
        {
            to_chat(user, span_notice(I18n.T("You close []'s door.", this)));
            playsound(user, close_sound, 50, TRUE);
            open = FALSE;
        }
        else
        {
            if (Dm.truthy(locked))
            {
                to_chat(user, span_warning(I18n.T("[] is locked!", this)));
                return null;
            }
            to_chat(user, span_notice(I18n.T("You open []'s door.", this)));
            playsound(user, open_sound, 50, TRUE);
            open = TRUE;
        }
        update_appearance();
        return null;
    }

    // tg: code/game/objects/items/pet_carrier.dm:94
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(open) || !Dm.truthy(allows_locking))
            return CLICK_ACTION_BLOCKING;
        locked = Dm.truthy(locked) ? FALSE : TRUE;
        to_chat(user, span_notice(I18n.T("You flip the lock switch [].", Dm.truthy(locked) ? "down" : "up")));
        if (Dm.truthy(locked))
            playsound(user, Dm.resource("sound/machines/airlock/boltsdown.ogg"), 30, TRUE);
        else
            playsound(user, Dm.resource("sound/machines/airlock/boltsup.ogg"), 30, TRUE);
        update_appearance();
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/pet_carrier.dm:106
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (Dm.truthy(user.combat_mode) || !isliving(interacting_with))
            return NONE;
        if (!Dm.truthy(open))
        {
            to_chat(user, span_warning(I18n.T("You need to open []'s door!", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        mob_living target = interacting_with as mob_living;
        if (Dm.num(target.mob_size) > Dm.num(max_occupant_weight))
        {
            if (ishuman(target))
            {
                if (HAS_TRAIT(target, TRAIT_CATLIKE_INSTINCT))
                    to_chat(user, span_warning(I18n.T("You'd need a lot of catnip and treats, plus maybe a laser pointer, for that to work.")));
                else
                    to_chat(user, span_warning(I18n.T("Humans, generally, do not fit into pet carriers.")));
            }
            else
                to_chat(user, span_warning(I18n.T("You get the feeling [] isn't meant for a [].", target, name)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (user == target)
        {
            to_chat(user, span_warning(I18n.T("Why would you ever do that?")));
            return ITEM_INTERACT_BLOCKING;
        }
        await load_occupant(user, target);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/pet_carrier.dm:126
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (Dm.truthy(open))
        {
            loc.visible_message(span_notice(I18n.T("[] climbs out of []!", user, this)), span_warning(I18n.T("[] jumps out of []!", user, this)));
            await remove_occupant(user);
            return null;
        }
        else if (!Dm.truthy(locked))
        {
            loc.visible_message(span_notice(I18n.T("[] pushes open the door to []!", user, this)), span_warning(I18n.T("[] pushes open the door of []!", user, this)));
            open = TRUE;
            update_appearance();
            return null;
        }
        else if (user.client != null)
            await container_resist_act(user);
        return null;
    }

    // tg: code/game/objects/items/pet_carrier.dm:140
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        user.change_next_special_move(CLICK_CD_BREAKOUT);
        if (Dm.num(user.mob_size) <= MOB_SIZE_SMALL)
        {
            to_chat(user, span_notice(I18n.T("You poke a limb through []'s bars and start fumbling for the lock switch... (This will take some time.)", this)));
            to_chat(loc, span_warning(I18n.T("You see [] reach through the bars and fumble for the lock switch!", user)));
            if (!Dm.truthy(await do_after(user, rand(300, 400), user)) || Dm.truthy(open) || !Dm.truthy(locked) || !Dm.@in(user, occupants))
                return null;
            loc.visible_message(span_warning(I18n.T("[] flips the lock switch on [] by reaching through!", user, this)), null, null, null, user);
            to_chat(user, span_bolddanger(I18n.T("Bingo! The lock pops open!")));
            locked = FALSE;
            playsound(this, Dm.resource("sound/machines/airlock/boltsup.ogg"), 30, TRUE);
            update_appearance();
        }
        else
        {
            loc.visible_message(span_warning(I18n.T("[] starts rattling as something pushes against the door!", this)), null, null, null, user);
            to_chat(user, span_notice(I18n.T("You start pushing out of []... (This will take about 20 seconds.)", this)));
            if (!Dm.truthy(await do_after(user, 20 * SECONDS, user)) || Dm.truthy(open) || !Dm.truthy(locked) || !Dm.@in(user, occupants))
                return null;
            loc.visible_message(span_warning(I18n.T("[] shoves out of []!", user, this)), null, null, null, user);
            to_chat(user, span_notice(I18n.T("You shove open []'s door against the lock's resistance and fall out!", this)));
            locked = FALSE;
            open = TRUE;
            update_appearance();
            await remove_occupant(user);
        }
        return null;
    }

    // tg: code/game/objects/items/pet_carrier.dm:165
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(open))
        {
            icon_state = $"{base_icon_state}_open";
            return await base.update_icon_state(__extra);
        }
        icon_state = $"{base_icon_state}_{(!Dm.truthy(length(occupants)) ? "closed" : "occupied")}";
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/pet_carrier.dm:171
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (!Dm.truthy(open))
            dot.Add($"{base_icon_state}_{(Dm.truthy(locked) ? "locked" : "unlocked")}");
        return dot;
    }

    // tg: code/game/objects/items/pet_carrier.dm:176
    public override async DmTask<object> mouse_drop_dragged(atom over_atom = null, mob user = null, object src_location = null, object over_location = null, object @params = null)
    {
        if (isopenturf(over_atom) && Dm.truthy(open) && Dm.truthy(length(occupants)))
        {
            user.visible_message(span_notice(I18n.T("[] unloads [].", user, this)), span_notice(I18n.T("You unload [] onto [].", this, over_atom)));
            foreach (object V in Dm.each(occupants.Copy()))
                await remove_occupant(V as mob_living, over_atom as turf);
        }
        return null;
    }

    // tg: code/game/objects/items/pet_carrier.dm:193
    public virtual async DmTask load_occupant(mob_living user = null, mob_living target = null)
    {
        if (pet_carrier_full())
        {
            to_chat(user, span_warning(I18n.T("[] is already carrying too much!", this)));
            return;
        }
        user.visible_message(span_notice(I18n.T("[] starts loading [] into [].", user, target, this)), span_notice(I18n.T("You start loading [] into []...", target, this)), null, null, target);
        to_chat(target, span_userdanger(I18n.T("[] starts loading you into [] [].", user, user.p_their(), name)));
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, target)))
            return;
        if (Dm.@in(target, occupants))
            return;
        if (pet_carrier_full()) //Run the checks again, just in case
        {
            to_chat(user, span_warning(I18n.T("[] is already carrying too much!", this)));
            return;
        }
        user.visible_message(span_notice(I18n.T("[] loads [] into []!", user, target, this)), span_notice(I18n.T("You load [] into [].", target, this)), null, null, target);
        to_chat(target, span_userdanger(I18n.T("[] loads you into [] [].", user, user.p_their(), name)));
        await add_occupant(target);
    }

    // tg: code/game/objects/items/pet_carrier.dm:212
    public virtual async DmTask add_occupant(mob_living occupant = null)
    {
        if (Dm.@in(occupant, occupants) || occupant == null)
            return;
        await occupant.forceMove(this);
        occupants.Add(occupant);
        occupant_weight = Dm.num(occupant_weight) + Dm.num(occupant.mob_size);
    }

    // tg: code/game/objects/items/pet_carrier.dm:219
    public virtual async DmTask remove_occupant(mob_living occupant = null, turf new_turf = null)
    {
        if (!Dm.@in(occupant, occupants) || occupant == null)
            return;
        await occupant.forceMove(new_turf ?? drop_location() as atom);
        occupants.Remove(occupant);
        occupant_weight = Dm.num(occupant_weight) - Dm.num(occupant.mob_size);
        await occupant.setDir(SOUTH);
    }
}

// tg: code/game/objects/items/pet_carrier.dm:263
public partial class obj_item_pet_carrier_small_mouse : obj_item_pet_carrier_small
{
    // tg: code/game/objects/items/pet_carrier.dm:269
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        mob_living_basic_mouse hero_mouse = Dm.@new<mob_living_basic_mouse>(this);
        Waiting.NoSleep(add_occupant(hero_mouse)); //mouse hero
        return base.Initialize(mapload, __extra);
    }
}
