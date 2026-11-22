// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4b, D316: the pie cannon; the pneumatic cannons come with it). The var blocks are generated (D111):
// `tank` holds the tank object (types tool, ObjectHoldingVars). The chat icons of the examine lines (icon2html) are left out
// (chat icons, not-translated section 76).

// tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:14
public partial class obj_item_pneumatic_cannon : obj_item
{
    /// Fire all loaded contents at once.
    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:3
    private const float PCANNON_FIREALL = 1;
    /// First in, Last out.
    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:5
    private const float PCANNON_FILO = 2;
    /// First in, first out. Default value.
    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:7
    private const float PCANNON_FIFO = 3;

    ///Defines for the pressure strength of the cannon
    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:10
    private const float LOW_PRESSURE = 1;
    private const float MID_PRESSURE = 2;
    private const float HIGH_PRESSURE = 3;

    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:69
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(selfcharge))
            init_charge();
        return dot;
    }

    //wrapper so it can be vv'd easier
    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:74
    public virtual void init_charge()
    {
        START_PROCESSING(SSobj, this);
    }

    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:77
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        charge_tick++;
        if (charge_tick >= charge_ticks && charge_type != null)
            Waiting.NoSleep(fill_with_type(charge_type, charge_amount));
        return null;
    }

    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:82
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:86
    public override object CanItemAutoclick(object @object = null, object location = null, object @params = null)
    {
        return automatic;
    }

    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:89
    public virtual string pressure_setting_to_text(float pressure_setting)
    {
        switch (pressure_setting)
        {
            case LOW_PRESSURE:
                return "low";
            case MID_PRESSURE:
                return "medium";
            case HIGH_PRESSURE:
                return "high";
            default:
                throw Dm.CRASH(Dm.text("Invalid pressure setting: []!", pressure_setting), "code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm", 98);
        }
    }

    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:100
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        DmList @out = Dm.list();
        if (!in_range(user, this))
        {
            Dm.append(ref @out, span_notice(I18n.T("You'll need to get closer to see any more.")));
            return dot;
        }
        if (Dm.truthy(selfcharge))
        {
            if (Dm.truthy(length(loadedItems)))
                Dm.append(ref @out, span_info(I18n.T("It has [] [] loaded.", length(loadedItems), Dm.initial(charge_type, (atom p) => p.name))));
        }
        else
        {
            foreach (obj_item I in Dm.each<obj_item>(loadedItems))
            {
                Dm.append(ref @out, span_info(I18n.T("It has \\a [] loaded.", I)));
                CHECK_TICK();
            }
        }
        if (!Dm.truthy(length(loadedItems)))
            Dm.append(ref @out, span_info(I18n.T("The chamber has nothing loaded.")));
        if (tank != null)
            Dm.append(ref @out, span_notice(I18n.T("It has \\a [] mounted onto it. It could be removed with a <b>screwdriver</b>.", tank)));
        if (needs_air == TRUE)
            Dm.append(ref dot, span_notice(I18n.T("Use a <b>wrench</b> to change the pressure level. Current output level is <b>[]</b>.", pressure_setting_to_text(pressure_setting))));
        Dm.append(ref dot, @out.Join("\n"));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:121
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (tank != null)
        {
            tool.play_tool_sound(this);
            await updateTank(tank, 1, user);
        }
        return TRUE;
    }

    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:127
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (needs_air == FALSE)
            return null;
        playsound(this, Dm.resource("sound/items/tools/ratchet.ogg"), 50, TRUE);
        pressure_setting = pressure_setting >= HIGH_PRESSURE ? LOW_PRESSURE : pressure_setting + 1;
        balloon_alert(user, I18n.T("output level set to []", pressure_setting_to_text(pressure_setting)));
        return TRUE;
    }

    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:135
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;
        if (!isitem(tool))
            return NONE;

        if (tool is obj_item_tank_internals new_tank)
        {
            if (needs_air == FALSE)
                return ITEM_INTERACT_BLOCKING;

            if (tank != null)
                return ITEM_INTERACT_BLOCKING;

            if (Dm.num(new_tank.volume) <= 3)
            {
                to_chat(user, span_warning(I18n.T("\\The [] is too small for \\the [].", tool, this)));
                return ITEM_INTERACT_BLOCKING;
            }

            await updateTank(new_tank, FALSE, user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool.type == type)
        {
            to_chat(user, span_warning(I18n.T("You're fairly certain that putting a pneumatic cannon inside another pneumatic cannon would cause a spacetime disruption.")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (loadedWeightClass >= maxWeightClass)
        {
            to_chat(user, span_warning(I18n.T("\\The [] can't hold any more items!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        await load_item(tool, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:166
    public virtual object can_load_item(atom_movable I = null, mob user = null)
    {
        if (I is not obj_item item) //Players can't load non items, this allows for admin varedit inserts.
            return TRUE;
        if (allowed_typecache != null && !Dm.truthy(is_type_in_typecache(item, allowed_typecache)))
        {
            if (user != null)
                to_chat(user, span_warning(I18n.T("[] won't fit into []!", item, this)));
            return null;
        }
        if ((loadedWeightClass + Dm.num(item.w_class)) > maxWeightClass) //Only make messages if there's a user
        {
            if (user != null)
                to_chat(user, span_warning(I18n.T("\\The [] won't fit into \\the []!", item, this)));
            return FALSE;
        }
        if (Dm.num(item.w_class) > Dm.num(w_class))
        {
            if (user != null)
                to_chat(user, span_warning(I18n.T("\\The [] is too large to fit into \\the []!", item, this)));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:183
    public virtual async DmTask<object> load_item(atom_movable I = null, mob user = null)
    {
        if (!Dm.truthy(can_load_item(I, user)))
            return FALSE;
        if (user != null) //Only use transfer proc if there's a user, otherwise just set loc.
        {
            if (!Dm.truthy(await user.transferItemToLoc(I as obj_item, this)))
                return FALSE;
            to_chat(user, span_notice(I18n.T("You load \\the [] into \\the [].", I, this)));
        }
        else
        {
            await I.forceMove(this);
        }
        Dm.append(ref loadedItems, I);
        if (I is obj_item item)
            loadedWeightClass += Dm.num(item.w_class);
        else
            loadedWeightClass++;
        return TRUE;
    }

    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:199
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (Dm.truthy(user.combat_mode))
            return ITEM_INTERACT_SKIP_TO_ATTACK;
        if (!Dm.truthy(await pre_fire(user, interacting_with)))
            return NONE;
        await Fire(user, interacting_with);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:207
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        await Fire(user, interacting_with);
        return ITEM_INTERACT_SUCCESS;
    }

    /** Checks */
    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:212
    public virtual async DmTask<object> pre_fire(mob_living user = null, atom target = null)
    {
        if (Dm.truthy(await user.Adjacent(target)))
        {
            if (Dm.@in(target, user.contents))
                return FALSE;
            if (!ismob(target))
                return FALSE;
        }
        return TRUE;
    }

    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:220
    public virtual async DmTask Fire(mob_living user = null, atom target = null)
    {
        if (user == null && target == null)
            return;
        float discharge = 0;
        if (!Dm.truthy(await can_trigger_gun(user)))
            return;
        if (loadedItems == null || !Dm.truthy(loadedWeightClass))
        {
            to_chat(user, span_warning(I18n.T("\\The [] has nothing loaded.", this)));
            return;
        }
        if (tank == null && Dm.truthy(needs_air))
        {
            to_chat(user, span_warning(I18n.T("\\The [] can't fire without a source of gas.", this)));
            return;
        }
        if (HAS_TRAIT(user, TRAIT_PACIFISM))
        {
            to_chat(user, span_warning(I18n.T("You can't bring yourself to fire \\the []! You don't want to risk harming anyone...", this)));
            return;
        }
        if (tank != null && !Dm.truthy(tank.remove_air(gasPerThrow * pressure_setting)))
        {
            to_chat(user, span_warning(I18n.T("\\The [] lets out a weak hiss and doesn't react!", this)));
            return;
        }
        if (HAS_TRAIT(user, TRAIT_CLUMSY) && prob(75) && Dm.truthy(clumsyCheck) && iscarbon(user))
        {
            mob_living_carbon C = (mob_living_carbon)user;
            C.visible_message(span_warning(I18n.T("[] loses [] grip on [], causing it to go off!", C, C.p_their(), this)), span_userdanger(I18n.T("[] slips out of your hands and goes off!", this)));
            await C.dropItemToGround(this, TRUE);
            if (prob(10))
            {
                target = get_turf(user);
            }
            else
            {
                DmList possible_targets = range(3, this);
                target = pick(possible_targets) as atom;
            }
            discharge = 1;
        }
        if (!Dm.truthy(discharge))
        {
            user.visible_message(span_danger(I18n.T("[] fires \\the []!", user, this)),
                span_danger(I18n.T("You fire \\the []!", this)));
        }
        log_combat(user, target, "fired at", this);
        atom T = get_target(target, get_turf(this));
        playsound(this, fire_sound, 50, TRUE);
        await fire_items(T, user);
        if (pressure_setting >= 3 && iscarbon(user))
        {
            mob_living_carbon C = (mob_living_carbon)user;
            C.visible_message(span_warning(I18n.T("[] is thrown down by the force of the cannon!", C)), span_userdanger(I18n.T("[] slams into your shoulder, knocking you down!", this)));
            await C.Paralyze(60);
        }
    }

    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:260
    public virtual async DmTask fire_items(atom target = null, mob user = null)
    {
        if (fire_mode == PCANNON_FIREALL)
        {
            foreach (obj_item ITD in Dm.each<obj_item>(loadedItems)) //Item To Discharge
            {
                if (!Dm.truthy(await throw_item(target, ITD, user)))
                    break;
            }
        }
        else
        {
            for (int i = 1; i <= throw_amount; i++)
            {
                if (!Dm.truthy(loadedItems.len))
                    break;
                object I;
                if (fire_mode == PCANNON_FILO)
                    I = loadedItems[loadedItems.len];
                else
                    I = loadedItems[1];
                if (!Dm.truthy(await throw_item(target, I, user)))
                    break;
            }
        }
    }

    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:277
    public virtual async DmTask<object> throw_item(atom target = null, object thrown = null, mob user = null)
    {
        if (thrown is not atom_movable AM)
            return FALSE;
        Dm.remove(ref loadedItems, AM);
        if (AM is obj_item I)
            loadedWeightClass -= Dm.num(I.w_class);
        else
            loadedWeightClass--;
        await AM.forceMove(get_turf(this));
        await AM.throw_at(target, pressure_setting * 10 * range_multiplier, pressure_setting * 2, user, spin: spin_item);
        return TRUE;
    }

    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:290
    public virtual atom get_target(atom target = null, turf starting = null)
    {
        if (range_multiplier == 1)
            return target;
        float x_o = (target.x - starting.x);
        float y_o = (target.y - starting.y);
        float new_x = clamp((starting.x + (x_o * range_multiplier)), 0, Dm.num(world.maxx));
        float new_y = clamp((starting.y + (y_o * range_multiplier)), 0, Dm.num(world.maxy));
        turf newtarget = locate(new_x, new_y, starting.z);
        return newtarget;
    }

    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:300
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (Dm.truthy(loadedItems.Remove(gone)))
        {
            if (gone is obj_item item)
                loadedWeightClass -= Dm.num(item.w_class);
            else
                loadedWeightClass--;
        }
        else if (gone == tank)
        {
            tank = null;
            update_appearance();
        }
        return dot;
    }

    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:319
    public virtual async DmTask updateTank(obj_item_tank_internals thetank = null, float? removing = null, mob_living user = null)
    {
        removing ??= 0;
        if (Dm.truthy(removing))
        {
            if (tank == null)
                return;
            to_chat(user, span_notice(I18n.T("You detach \\the [] from \\the [].", thetank, this)));
            await tank.forceMove(user.drop_location() as atom);
            await user.put_in_hands(tank);
            tank = null;
        }
        if (!Dm.truthy(removing))
        {
            if (tank != null)
            {
                to_chat(user, span_warning(I18n.T("\\The [] already has a tank.", this)));
                return;
            }
            if (!Dm.truthy(await user.transferItemToLoc(thetank, this)))
                return;
            to_chat(user, span_notice(I18n.T("You hook \\the [] up to \\the [].", thetank, this)));
            tank = thetank;
        }
        update_appearance();
    }

    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:337
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (tank == null)
            return dot;
        Dm.append(ref dot, tank.icon_state);
        return dot;
    }

    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:343
    public virtual async DmTask<object> fill_with_type(Type type = null, float amount = 0)
    {
        if (!ispath(type, typeof(obj)) && !ispath(type, typeof(mob)))
            return FALSE;
        float loaded = 0;
        for (int i = 1; i <= amount; i++)
        {
            atom_movable I = Dm.@new(type) as atom_movable;
            if (!Dm.truthy(await load_item(I, null)))
            {
                qdel(I);
                return loaded;
            }
            loaded++;
            CHECK_TICK();
        }
        return null;
    }
}

// tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:355
public partial class obj_item_pneumatic_cannon_pie : obj_item_pneumatic_cannon
{
    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:366
    public static DmList pie_typecache => TgState.Current.obj_item_pneumatic_cannon_pie__pie_typecache ??= typecacheof(typeof(obj_item_food_pie)) as DmList;

    // tg: code/game/objects/items/weaponry/ranged/pneumatic_cannon.dm:368
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        allowed_typecache = pie_typecache;
        return dot;
    }
}

public sealed partial class TgState
{
    internal DmList obj_item_pneumatic_cannon_pie__pie_typecache;
}
