// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/tanks/watertank.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps J5, D298): the backpack tanks with their misters and the atmospheric firepack with its nozzle (the extinguisher, the resin launcher and resin foam).
// The chemical injector backpack (:370 on) stays inert, not a starting item.

//Hydroponics tank and base code
// tg: code/game/objects/items/tanks/watertank.dm:2
public partial class obj_item_watertank : obj_item
{
    // tg: code/game/objects/items/tanks/watertank.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(volume, OPENCONTAINER);
        noz = make_noz() as obj_item;
        RegisterSignal(noz, COMSIG_MOVABLE_MOVED, nameof(noz_move));
        Waiting.NoSleep(AddElement(typeof(datum_element_drag_pickup)));
        return dot;
    }

    // tg: code/game/objects/items/tanks/watertank.dm:33
    public override object Destroy(float? force = null)
    {
        qdel(noz);
        noz = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/tanks/watertank.dm:38
    public override async DmTask<object> ui_action_click(mob user = null, object action = null)
    {
        await toggle_mister(user as mob_living);
        return null;
    }

    // tg: code/game/objects/items/tanks/watertank.dm:41
    public virtual async DmTask toggle_mister(mob_living user = null)
    {
        if (user == null)
            return;
        if (user.get_item_by_slot(user.getBackSlot()) != this)
        {
            to_chat(user, span_warning(I18n.T("The watertank must be worn properly to use!")));
            return;
        }
        if (Dm.truthy(user.incapacitated))
            return;

        if (QDELETED(noz))
        {
            noz = make_noz() as obj_item;
            RegisterSignal(noz, COMSIG_MOVABLE_MOVED, nameof(noz_move));
        }
        if (Dm.@in(noz, contents))
        {
            //Detach the nozzle into the user's hands
            if (!Dm.truthy(await user.put_in_hands(noz)))
            {
                to_chat(user, span_warning(I18n.T("You need a free hand to hold the mister!")));
                return;
            }
        }
        else
        {
            //Remove from their hands and put back "into" the tank
            await remove_noz();
        }
    }

    // tg: code/game/objects/items/tanks/watertank.dm:62  GAME_VERB(/obj/item/watertank, toggle_mister_verb, "Toggle Mister", null)
    public virtual async DmTask<object> __gvb_toggle_mister_verb(DmList structured_args = null)
    {
        await toggle_mister(usr as mob_living);
        return null;
    }

    // tg: code/game/objects/items/tanks/watertank.dm:62
    public virtual void toggle_mister_verb(params object[] __extra)
    {
        Waiting.Detach(__gvb_toggle_mister_verb());
    }

    // tg: code/game/objects/items/tanks/watertank.dm:65
    public virtual object make_noz() => Dm.@new<obj_item_reagent_containers_spray_mister>(this);

    // tg: code/game/objects/items/tanks/watertank.dm:68
    public virtual void noz_move(atom_movable mover = null, atom oldloc = null, object direction = null)
    {
        if (mover.loc == this || mover.loc == loc)
            return;
        balloon_alert(loc as mob, I18n.T("nozzle snaps back"));
        Waiting.Detach(mover.forceMove(this));
    }

    // tg: code/game/objects/items/tanks/watertank.dm:74
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!Dm.truthy(Dm.bitand(slot, ITEM_SLOT_BACK)))
            await remove_noz();
    }

    // tg: code/game/objects/items/tanks/watertank.dm:79
    public virtual async DmTask remove_noz()
    {
        if (!QDELETED(noz))
        {
            if (ismob(noz.loc))
            {
                mob M = noz.loc as mob;
                await M.temporarilyRemoveItemFromInventory(noz, TRUE);
            }
            await noz.forceMove(this);
        }
    }

    // tg: code/game/objects/items/tanks/watertank.dm:86
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (user.get_item_by_slot(user.getBackSlot()) == this)
        {
            await toggle_mister(user as mob_living);
            return null;
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/game/objects/items/tanks/watertank.dm:92
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool != noz)
            return NONE;
        await remove_noz();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/tanks/watertank.dm:98
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        await remove_noz();
        return dot;
    }
}

// This mister item is intended as an extension of the watertank and always attached to it.
// Therefore, it's designed to be "locked" to the player's hands or extended back onto
// the watertank backpack. Allowing it to be placed elsewhere or created without a parent
// watertank object will likely lead to weird behaviour or runtimes.
// tg: code/game/objects/items/tanks/watertank.dm:106
public partial class obj_item_reagent_containers_spray_mister : obj_item_reagent_containers_spray
{
    // tg: code/game/objects/items/tanks/watertank.dm:121
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (loc?.reagents == null)
            return INITIALIZE_HINT_QDEL;
        reagents = loc.reagents; //This mister is really just a proxy for the tank's reagents
        return dot;
    }

    // tg: code/game/objects/items/tanks/watertank.dm:127
    public override async DmTask<object> try_spray(atom target = null, mob user = null)
    {
        if (target.loc == loc) //Safety check so you don't fill your mister with mutagen or something and then blast yourself in the face with it
            return FALSE;
        return await base.try_spray(target, user);
    }
}

//Janitor tank
// tg: code/game/objects/items/tanks/watertank.dm:133
public partial class obj_item_watertank_janitor : obj_item_watertank
{
    // tg: code/game/objects/items/tanks/watertank.dm:140
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep((reagents as datum_reagents).add_reagent(typeof(datum_reagent_space_cleaner), 500));
        return dot;
    }

    // tg: code/game/objects/items/tanks/watertank.dm:155
    public override object make_noz() => Dm.@new<obj_item_reagent_containers_spray_mister_janitor>(this);
}

// tg: code/game/objects/items/tanks/watertank.dm:144
public partial class obj_item_reagent_containers_spray_mister_janitor : obj_item_reagent_containers_spray_mister
{
    // tg: code/game/objects/items/tanks/watertank.dm:158
    public override void mode_change_message(mob user = null)
    {
        to_chat(user, span_notice(I18n.T("You [] the nozzle. You'll now use [] units per spray.", Dm.eq(amount_per_transfer_from_this, 10) ? "remove" : "affix", amount_per_transfer_from_this)));
    }
}

//Security tank
// tg: code/game/objects/items/tanks/watertank.dm:162
public partial class obj_item_watertank_pepperspray : obj_item_watertank
{
    // tg: code/game/objects/items/tanks/watertank.dm:171
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep((reagents as datum_reagents).add_reagent(typeof(datum_reagent_consumable_condensedcapsaicin), 1000));
        return dot;
    }

    // tg: code/game/objects/items/tanks/watertank.dm:186
    public override object make_noz() => Dm.@new<obj_item_reagent_containers_spray_mister_pepperspray>(this);
}

// tg: code/game/objects/items/tanks/watertank.dm:175
public partial class obj_item_reagent_containers_spray_mister_pepperspray : obj_item_reagent_containers_spray_mister
{
    // tg: code/game/objects/items/tanks/watertank.dm:189
    public override void mode_change_message(mob user = null)
    {
        to_chat(user, span_notice(I18n.T("You [] the nozzle. You'll now use [] units per spray.", Dm.eq(amount_per_transfer_from_this, 10) ? "remove" : "affix", amount_per_transfer_from_this)));
    }
}

//ATMOS FIRE FIGHTING BACKPACK
// tg: code/game/objects/items/tanks/watertank.dm:193
public partial class obj_item_watertank_atmos : obj_item_watertank
{
    // tg: code/game/objects/items/tanks/watertank.dm:201
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep((reagents as datum_reagents).add_reagent(typeof(datum_reagent_water), 200));
        return dot;
    }

    // tg: code/game/objects/items/tanks/watertank.dm:205
    public override object make_noz() => Dm.@new<obj_item_extinguisher_mini_nozzle>(this);

    // tg: code/game/objects/items/tanks/watertank.dm:208
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        icon_state = "waterbackpackatmos";
        if (noz is obj_item_extinguisher_mini_nozzle N)
            N.nozzle_mode = 0;
        return dot;
    }
}

// tg: code/game/objects/items/tanks/watertank.dm:215
public partial class obj_item_extinguisher_mini_nozzle : obj_item_extinguisher_mini
{
    private const int EXTINGUISHER = 0;
    private const int RESIN_LAUNCHER = 1;
    private const int RESIN_FOAM = 2;

    // GAP(types): tg's `var/obj/item/tank` (:237) is an item; the generated var reads the path's last name as a type
    public obj_item tank_item;

    // tg: code/game/objects/items/tanks/watertank.dm:236
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        tank_item = loc as obj_item;
        if (tank_item?.reagents == null)
            return INITIALIZE_HINT_QDEL;
        reagents = tank_item.reagents;
        max_water = (tank_item.reagents as datum_reagents).maximum_volume;
        return dot;
    }

    // tg: code/game/objects/items/tanks/watertank.dm:245
    public override object Destroy(float? force = null)
    {
        reagents = null; //This is a borrowed reference from the tank.
        tank_item = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/tanks/watertank.dm:250
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        bool uses_pack = tank_item is obj_item_watertank_atmos;
        switch ((int)Dm.num(nozzle_mode))
        {
            case EXTINGUISHER:
                nozzle_mode = RESIN_LAUNCHER;
                if (uses_pack)
                    tank_item.icon_state = "waterbackpackatmos_1";
                balloon_alert(user, I18n.T("switched to resin launcher"));
                return null;
            case RESIN_LAUNCHER:
                nozzle_mode = RESIN_FOAM;
                if (uses_pack)
                    tank_item.icon_state = "waterbackpackatmos_2";
                balloon_alert(user, I18n.T("switched to resin foam"));
                return null;
            case RESIN_FOAM:
                nozzle_mode = EXTINGUISHER;
                if (uses_pack)
                    tank_item.icon_state = "waterbackpackatmos_0";
                balloon_alert(user, I18n.T("switched to fire extinguisher"));
                return null;
        }
        return null;
    }

    // tg: code/game/objects/items/tanks/watertank.dm:273
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (Dm.truthy(await AttemptRefill(interacting_with, user)))
            return NONE;
        return await base.interact_with_atom(interacting_with, user, modifiers);
    }

    // tg: code/game/objects/items/tanks/watertank.dm:278
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (Dm.eq(nozzle_mode, EXTINGUISHER))
            return await base.ranged_interact_with_atom(interacting_with, user, modifiers);

        bool Adj = Dm.truthy(await user.Adjacent(interacting_with));
        if (Dm.eq(nozzle_mode, RESIN_LAUNCHER))
        {
            if (Adj && Dm.truthy(user.combat_mode))
                return ITEM_INTERACT_SKIP_TO_ATTACK;
            datum_reagents R = reagents as datum_reagents;
            if (Dm.num(R.total_volume) < 100)
            {
                balloon_alert(user, I18n.T("not enough water!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!(resin_cooldown <= world.time))
            {
                balloon_alert(user, I18n.T("still recharging!"));
                return ITEM_INTERACT_BLOCKING;
            }
            resin_cooldown = world.time + 10 * SECONDS;
            await R.remove_all(100);
            obj_effect_resin_container resin = Dm.@new<obj_effect_resin_container>(get_turf(this));
            user.log_message("used Resin Launcher", LOG_GAME);
            playsound(this, Dm.resource("sound/items/syringeproj.ogg"), 40, TRUE);
            float delay = 2;
            datum_move_loop loop = GLOB.move_manager.move_towards(resin, interacting_with, delay, timeout: delay * 5, priority: MOVEMENT_ABOVE_SPACE_PRIORITY) as datum_move_loop;
            RegisterSignal(loop, COMSIG_MOVELOOP_POSTPROCESS, nameof(resin_stop_check));
            RegisterSignal(loop, COMSIG_QDELETING, nameof(resin_landed));
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.eq(nozzle_mode, RESIN_FOAM))
        {
            if (!isturf(interacting_with))
                return NONE;
            if (!Adj)
            {
                balloon_alert(user, I18n.T("too far!"));
                return ITEM_INTERACT_BLOCKING;
            }
            foreach (object thing in Dm.each(interacting_with))
            {
                if (thing is obj_effect_particle_effect_fluid_foam_metal_resin || thing is obj_structure_foamedmetal_resin)
                {
                    balloon_alert(user, I18n.T("already has resin!"));
                    return ITEM_INTERACT_BLOCKING;
                }
            }
            if (Dm.num(metal_synthesis_cooldown) < 5)
            {
                obj_effect_particle_effect_fluid_foam_metal_resin foam = Dm.@new<obj_effect_particle_effect_fluid_foam_metal_resin>(get_turf(interacting_with));
                foam.group.target_size = 0f;
                metal_synthesis_cooldown = Dm.num(metal_synthesis_cooldown) + 1;
                _addtimer(Dm.@new<datum_callback>(this, nameof(reduce_metal_synth_cooldown)), 10 * SECONDS, file: "code/game/objects/items/tanks/watertank.dm", line: 321);
                return ITEM_INTERACT_SUCCESS;
            }

            balloon_alert(user, I18n.T("still being synthesized!"));
            return ITEM_INTERACT_BLOCKING;
        }

        return NONE;
    }

    // tg: code/game/objects/items/tanks/watertank.dm:329
    [SignalHandler]
    public virtual void resin_stop_check(datum_move_loop source = null, object result = null)
    {
        if (Dm.eq(result, MOVELOOP_SUCCESS))
            return;
        resin_landed(source);
        qdel(source);
    }

    // tg: code/game/objects/items/tanks/watertank.dm:336
    [SignalHandler]
    public virtual void resin_landed(datum_move_loop source = null)
    {
        if (source.moving is not obj_effect_resin_container || QDELETED(source.moving))
            return;
        obj_effect_resin_container resin = source.moving as obj_effect_resin_container;
        resin.Smoke();
    }

    // tg: code/game/objects/items/tanks/watertank.dm:343
    public virtual void reduce_metal_synth_cooldown()
    {
        metal_synthesis_cooldown = Dm.num(metal_synthesis_cooldown) - 1;
    }
}

// tg: code/game/objects/items/tanks/watertank.dm:346
public partial class obj_effect_resin_container : obj_effect
{
    // tg: code/game/objects/items/tanks/watertank.dm:355
    public virtual void Smoke()
    {
        Waiting.NoSleep(do_foam(4, this, loc as turf, foam_type: typeof(datum_effect_system_fluid_spread_foam_metal_resin)));
        playsound(this, Dm.resource("sound/effects/bamf.ogg"), 100, TRUE);
        qdel(this);
    }

    // Please don't spacedrift thanks
    // tg: code/game/objects/items/tanks/watertank.dm:361
    public override async DmTask<object> newtonian_move(object inertia_angle = null, float? instant = null, float? start_delay = null, float? drift_force = null, object controlled_cap = null, float? force_loop = null) => TRUE;
}
