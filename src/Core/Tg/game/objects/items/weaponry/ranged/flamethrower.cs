// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/weaponry/ranged/flamethrower.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 10 (the flamethrower assembly): the flamethrower: built from a welder and rods (weldingtool.cs) or crafted, the
// igniter and the plasma tank put in, the screwdriver that secures the igniter, the wrench that takes it apart, the
// igniter toggle (its sounds, the light, SSobj processing that heats its turf), the jet along the line to the target
// (5% of the tank's air per turf, its plasma fivefold, the turf's hotspot) and the recharger's refill. Deferred leaves: the
// bullet interception component (projectiles, after the core version), the combat log and the admin message (M10). The
// examine is translated (D291). The var blocks are generated.

// tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:1
public partial class obj_item_flamethrower : obj_item
{
    // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:42  var/static/list/slapcraft_recipe_list (rules 10.6)
    private static ref DmList Initialize__slapcraft_recipe_list => ref TgState.Current.obj_item_flamethrower__Initialize__slapcraft_recipe_list;

    // The first definition of Initialize (tg defines it twice on this type; the second's ..() calls this one, rules 2.5).
    // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:40
    public virtual object Initialize__d0(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        Initialize__slapcraft_recipe_list ??= Dm.list(typeof(datum_crafting_recipe_flamethrower));

        Waiting.NoSleep(AddElement(
            typeof(datum_element_slapcrafting),
            Dm.named("slapcraft_recipes", Initialize__slapcraft_recipe_list)));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:50
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(weldtool))
            QDEL_NULL(ref weldtool);
        if (Dm.truthy(igniter))
            QDEL_NULL(ref igniter);
        if (Dm.truthy(ptank))
            QDEL_NULL(ref ptank);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:59
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(lit) || !Dm.truthy(igniter))
            return PROCESS_KILL;
        atom location = loc;
        if (Dm.truthy(ismob(location)))
        {
            var M = (mob)location;
            if (Dm.truthy(M.is_holding(this)))
                location = M.loc;
        }
        if (Dm.truthy(isturf(location))) //start a fire if possible
            igniter.flamethrower_process((turf_open)(location as turf_open));
        return null;
    }

    // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:71
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        inhand_icon_state = Dm.text("flamethrower_[]", lit);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:75
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(igniter))
            Dm.append(ref dot, Dm.text("+igniter[]", status));
        if (Dm.truthy(ptank))
            Dm.append(ref dot, "+ptank");
        if (Dm.truthy(lit))
            Dm.append(ref dot, "+lit");
        return dot;
    }

    // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:84
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(ptank))
            return NONE;
        if (Dm.truthy(HAS_TRAIT(user, TRAIT_PACIFISM)))
        {
            to_chat(user, span_warning(I18n.T("You can't bring yourself to fire \\the []! You don't want to risk harming anyone...", this)));
            log_combat(user, interacting_with, "attempted to flamethrower", this, Dm.text("with gas mixture: {[]}, flamethrower: \"[]\" ([]), igniter: \"[]\", tank: \"[]\" and tank distribution pressure: \"[]\"", print_gas_mixture(ptank.return_analyzable_air() as datum_gas_mixture), name, this, igniter.name, ptank.name, siunit(1000 * ptank.distribute_pressure, unit: "Pa", maxdecimals: 9)) + (Dm.truthy(lit) ? " while lit" : "" + " but failed due to pacifism."));
            return ITEM_INTERACT_BLOCKING;
        }
        turf target_turf = get_turf(interacting_with);
        if (Dm.truthy(target_turf))
        {
            object turflist = get_line(user, target_turf);
            log_combat(user, interacting_with, "flamethrowered", this, Dm.text("with gas mixture: {[]}, flamethrower: \"[]\", igniter: \"[]\", tank: \"[]\" and tank distribution pressure: \"[]\"", print_gas_mixture(ptank.return_analyzable_air() as datum_gas_mixture), name, igniter.name, ptank.name, siunit(1000 * ptank.distribute_pressure, unit: "Pa", maxdecimals: 9)) + (Dm.truthy(lit) ? " while lit." : "."));
            await flame_turf(turflist);
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:98
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = ITEM_INTERACT_SUCCESS;
        if (Dm.truthy(status))
            return ITEM_INTERACT_BLOCKING;
        tool.play_tool_sound(this);
        turf T = get_turf(this);
        if (Dm.truthy(weldtool))
        {
            await weldtool.forceMove(T);
            weldtool = null;
        }
        if (Dm.truthy(igniter))
        {
            await igniter.forceMove(T);
            igniter = null;
        }
        if (Dm.truthy(ptank))
        {
            await ptank.forceMove(T);
            ptank = null;
        }
        Dm.@new<obj_item_stack_rods>(T);
        qdel(this);
        return dot;
    }

    // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:116
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(igniter) && !Dm.truthy(lit))
        {
            tool.play_tool_sound(this);
            status = Dm.truthy(status) ? FALSE : TRUE;
            to_chat(user, span_notice(I18n.T("[] is now []!", igniter, Dm.truthy(status) ? "secured" : "unsecured")));
            update_appearance();
            return TRUE;
        }
        return null;
    }

    // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:124
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_assembly_igniter))) // isigniter(tool) (__DEFINES/is_helpers.dm:310), expanded inline
        {
            var inserting_igniter = (obj_item_assembly_igniter)tool;
            if (Dm.truthy(inserting_igniter.secured))
                return ITEM_INTERACT_BLOCKING;
            if (Dm.truthy(igniter))
                return ITEM_INTERACT_BLOCKING;
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            igniter = inserting_igniter;
            update_appearance();
            balloon_alert(user, I18n.T("attached"));
            return ITEM_INTERACT_SUCCESS;
        }
        if (istype(tool, typeof(obj_item_tank_internals_plasma)))
        {
            if (Dm.truthy(ptank))
            {
                if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                    return ITEM_INTERACT_BLOCKING;
                await ptank.forceMove(get_turf(this));
                ptank = (obj_item_tank_internals_plasma)tool;
                to_chat(user, span_notice(I18n.T("You swap the plasma tank in []!", this)));
                return ITEM_INTERACT_SUCCESS;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            ptank = (obj_item_tank_internals_plasma)tool;
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:153
    public override object return_analyzable_air()
    {
        if (Dm.truthy(ptank))
            return ptank.return_analyzable_air();
        else
            return null;
    }

    // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:159
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        toggle_igniter(user);
        return null;
    }

    // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:162
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (ptank == null)
            return NONE;

        await user.put_in_hands(ptank);
        ptank = null;
        to_chat(user, span_notice(I18n.T("You remove the plasma tank from []!", this)));
        update_appearance();
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:172
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (ptank != null)
            Dm.append(ref dot, span_notice(I18n.T("\\The [] has \\a [] attached. Alt-click to remove it.", this, ptank)));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:177
    public virtual void toggle_igniter(mob user = null)
    {
        if (!Dm.truthy(ptank))
        {
            to_chat(user, span_notice(I18n.T("Attach a plasma tank first!")));
            return;
        }
        if (!Dm.truthy(status))
        {
            to_chat(user, span_notice(I18n.T("Secure the igniter first!")));
            return;
        }
        to_chat(user, span_notice(I18n.T("You [] []!", Dm.truthy(lit) ? "extinguish" : "ignite", this)));
        lit = Dm.truthy(lit) ? FALSE : TRUE;
        if (Dm.truthy(lit))
        {
            playsound(loc, acti_sound, 50, TRUE); // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:187
            START_PROCESSING(SSobj, this);
            if (!Dm.truthy(warned_admins))
            {
                message_admins(Dm.text("[] has lit a flamethrower.", ADMIN_LOOKUPFLW(user)));
                warned_admins = TRUE;
            }
        }
        else
        {
            playsound(loc, deac_sound, 50, TRUE); // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:193
            STOP_PROCESSING(SSobj, this);
        }
        Waiting.NoSleep(set_light_on(lit));
        update_appearance();
    }

    // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:198
    public override async DmTask on_craft_completion(DmList components = null, datum_crafting_recipe current_recipe = null, atom crafter = null)
    {
        await base.on_craft_completion(components, current_recipe, crafter);
        weldtool = Dm.locate<obj_item_weldingtool>(contents);
        igniter = Dm.locate<obj_item_assembly_igniter>(contents);
        weldtool.status = FALSE;
        igniter.secured = FALSE;
        status = TRUE;
        update_appearance();
    }

    //Called from turf.dm turf/dblclick
    // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:208
    public virtual async DmTask flame_turf(object turflist = null)
    {
        if (!Dm.truthy(lit) || Dm.truthy(operating))
            return;
        operating = TRUE;
        turf previousturf = get_turf(this);
        foreach (turf T in Dm.each<turf>(turflist))
        {
            if (T == previousturf)
                continue; //so we don't burn the tile we be standin on
            object turfs_sharing_with_prev = previousturf.get_atmos_adjacent_turfs(alldir: 1);
            if (!Dm.@in(T, turfs_sharing_with_prev))
                break;
            if (Dm.truthy(igniter))
                await igniter.ignite_turf(this, (turf_open)(T as turf_open));
            else
                await default_ignite(T);
            await sleep(0.1f * SECONDS);
            previousturf = T;
        }
        operating = FALSE;
    }

    // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:227
    public virtual async DmTask default_ignite(turf target = null, float? release_amount = null)
    {
        release_amount ??= 0.05f;
        //TODO: DEFERRED Consider checking to make sure tank pressure is high enough before doing this...
        //Transfer 5% of current tank air contents to turf
        var tank_mix = (datum_gas_mixture)await ptank.return_air();
        var air_transfer = (datum_gas_mixture)tank_mix.remove_ratio(release_amount);

        if (Dm.truthy(air_transfer.moles[typeof(datum_gas_plasma)]))
        {
            float moles = Dm.num(air_transfer.moles[typeof(datum_gas_plasma)]) * 5; //Suffering
            air_transfer.set_gas(typeof(datum_gas_plasma), moles);
        }
        await target.assume_air(air_transfer);
        //Burn it based on transferred gas
        target.hotspot_expose((Dm.num(tank_mix.temperature) * 2) + 380, 500);
        //location.hotspot_expose(1000,500,1)
    }

    // The second definition of Initialize (rules 2.5: its ..() is the first definition, Initialize__d0).
    // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:241
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = Initialize__d0(mapload, __extra);
        // AddComponent(/datum/component/bullet_intercepting, block_chance = 15, ...): a held flamethrower's tank stopping a
        // bullet or a laser and bursting (intercepted_bullet_reaction, check_tank)
        Dm.Deferred("code/game/objects/items/weaponry/ranged/flamethrower.dm:243", "Initialize: AddComponent(/datum/component/bullet_intercepting, ...), the tank that stops a bullet and bursts (projectiles, after the core version)");
        if (Dm.truthy(create_full))
        {
            if (!Dm.truthy(weldtool))
                weldtool = Dm.@new<obj_item_weldingtool>(this);
            weldtool.status = FALSE;
            if (!Dm.truthy(igniter))
                igniter = (obj_item_assembly_igniter)Dm.@new(igniter_type, this);
            igniter.secured = FALSE;
            status = TRUE;
            if (Dm.truthy(create_with_tank))
                ptank = Dm.@new<obj_item_tank_internals_plasma_full>(this);
            update_appearance();
        }
        RegisterSignal(this, COMSIG_ITEM_RECHARGED, nameof(instant_refill));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:271
    public virtual void intercepted_bullet_reaction(mob_living holder = null, obj_projectile bullet = null)
    {
        Dm.NotTranslated("code/game/objects/items/weaponry/ranged/flamethrower.dm:273", "intercepted_bullet_reaction: the tank burst by a projectile (projectiles, after the core version)");
    }

    // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:278
    public virtual object check_tank()
    {
        return ptank;
    }

    // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:287
    [SignalHandler]
    public virtual object instant_refill()
    {
        if (Dm.truthy(ptank))
        {
            var tank_mix = (datum_gas_mixture)Waiting.NoSleep(ptank.return_air());
            tank_mix.set_gas(typeof(datum_gas_plasma), (10 * ONE_ATMOSPHERE) * Dm.num(ptank.volume) / (R_IDEAL_GAS_EQUATION * T20C));
        }
        else
            ptank = Dm.@new<obj_item_tank_internals_plasma_full>(this);
        update_appearance();
        return null;
    }
}

// tg: code/modules/assembly/igniter.dm:6
public partial class obj_item_assembly_igniter : obj_item_assembly
{
    // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:281
    public virtual void flamethrower_process(turf_open location = null)
    {
        location.hotspot_expose(heat, 2);
    }

    // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:284
    public virtual async DmTask ignite_turf(obj_item_flamethrower F = null, turf_open location = null, float? release_amount = null)
    {
        release_amount ??= 0.05f;
        await F.default_ignite(location, release_amount);
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/weaponry/ranged/flamethrower.dm:42  var/static/list/slapcraft_recipe_list = list(/datum/crafting_recipe/flamethrower)
    internal DmList obj_item_flamethrower__Initialize__slapcraft_recipe_list;
}
