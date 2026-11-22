// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/special/blastcannon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4b, D316: the blast cannon and its blast wave), but the admin messages and the game logs (admin tools:
// a Dm.Deferred leaf and comments). The var blocks are generated (D111).

/**
 * A gun that consumes a TTV to shoot an projectile with equivalent power.
 *
 * It's basically an immovable rod launcher.
 */
// tg: code/modules/projectiles/guns/special/blastcannon.dm:12
public partial class obj_item_gun_blastcannon : obj_item_gun
{
    /// How much to scale the explosion ranges for blastcannon shots.
    // tg: code/modules/projectiles/guns/special/blastcannon.dm:2  #define BLASTCANNON_RANGE_EXP (1 / GLOB.DYN_EX_SCALE)
    private static float BLASTCANNON_RANGE_EXP => 1 / GLOB.DYN_EX_SCALE;
    /// How much to scale the explosion ranges for blastcannon shots.
    // tg: code/modules/projectiles/guns/special/blastcannon.dm:4
    private const float BLASTCANNON_RANGE_SCALE = PI;

    // tg: code/modules/projectiles/guns/special/blastcannon.dm:50
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_ATOM_INTERNAL_EXPLOSION, nameof(channel_blastwave));
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        return dot;
    }

    // tg: code/modules/projectiles/guns/special/blastcannon.dm:55
    public override object Destroy(float? force = null)
    {
        if (bomb != null)
            QDEL_NULL(ref bomb);
        UnregisterSignal(this, COMSIG_ATOM_INTERNAL_EXPLOSION);
        cached_firer = null;
        cached_target = null;
        return base.Destroy(force);
    }

    // tg: code/modules/projectiles/guns/special/blastcannon.dm:63
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == bomb)
        {
            bomb = null;
            update_appearance();
        }
        return dot;
    }

    // tg: code/modules/projectiles/guns/special/blastcannon.dm:69
    public override async DmTask<object> assume_air(datum_gas_mixture giver = null)
    {
        qdel(giver);
        return null; // Required to make the TTV not vent gas directly into the firer.
    }

    // tg: code/modules/projectiles/guns/special/blastcannon.dm:73
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (bomb != null)
            Dm.append(ref dot, span_notice(I18n.T("A bomb is loaded inside.")));
        return dot;
    }

    // tg: code/modules/projectiles/guns/special/blastcannon.dm:78
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (bomb != null)
        {
            await bomb.forceMove(user.loc);
            await user.put_in_hands(bomb);
            user.visible_message(span_warning(I18n.T("[] detaches [] from [].", user, bomb, this)));
            bomb = null;
        }
        update_appearance();
        return await base.attack_self(user, modifiers, __extra);
    }

    // tg: code/modules/projectiles/guns/special/blastcannon.dm:87
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]_[]", base_icon_state, bomb != null ? "loaded" : "empty");
        inhand_icon_state = icon_state;
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/projectiles/guns/special/blastcannon.dm:92
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_transfer_valve bomb_to_attach)
            return NONE;
        if (bomb != null)
        {
            to_chat(user, span_warning(I18n.T("[] is already attached to []!", bomb, this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(bomb_to_attach.ready()))
        {
            to_chat(user, span_warning(I18n.T("What good would an incomplete bomb do?")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(bomb_to_attach, this)))
        {
            to_chat(user, span_warning(I18n.T("[] seems to be stuck to your hand!", bomb_to_attach)));
            return ITEM_INTERACT_BLOCKING;
        }

        user.visible_message(span_warning(I18n.T("[] attaches [] to []!", user, bomb_to_attach, this)),
            span_notice(I18n.T("You attach bomb_to_attach to [].", this)));
        bomb = bomb_to_attach;
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/projectiles/guns/special/blastcannon.dm:112
    public override async DmTask<object> try_fire_gun(atom target = null, mob_living user = null, object @params = null)
    {
        if ((bomb == null && Dm.truthy(bombcheck)) || target == null || (get_dist(get_turf(target), get_turf(user)) <= 2))
            return await base.try_fire_gun(target, user, @params);

        cached_target = WEAKREF(target) as datum_weakref;
        cached_modifiers = @params;
        if (bomb != null && Dm.truthy(bomb.valve_open))
        {
            user.visible_message(
                span_danger(I18n.T("[] points [] at []!", user, this, target)),
                span_danger(I18n.T("You point [] at []!", this, target)));
            return FALSE;
        }

        cached_firer = WEAKREF(user) as datum_weakref;
        if (bomb == null)
        {
            await fire_debug(target, user, @params);
            return TRUE;
        }

        playsound(this, dry_fire_sound, 30, TRUE); // *click
        user.visible_message(
            span_danger(I18n.T("[] opens [] on [] [] and points [] at []!", user, bomb, user.p_their(), this, p_them(), target)),
            span_danger(I18n.T("You open [] on your [] and point [] at []!", bomb, this, p_them(), target)));
        var current_turf = get_turf(this) as turf;
        var target_turf = get_turf(target) as turf;
        message_admins(Dm.text("Blastcannon transfer valve opened by [] at [] while aiming at [] (target).", ADMIN_LOOKUPFLW(user), ADMIN_VERBOSEJMP(current_turf), ADMIN_VERBOSEJMP(target_turf)));
        user.log_message(Dm.text("opened blastcannon transfer valve at [] while aiming at [] (target).", AREACOORD(current_turf), AREACOORD(target_turf)), LOG_GAME);
        await bomb.toggle_valve();
        update_appearance();
        return TRUE;
    }

    /**
     * Channels an internal explosion into a blastwave projectile.
     *
     * Arguments:
     * - [blastwave_data][/list]: A list containing all of the data for the blastwave.
     */
    // tg: code/modules/projectiles/guns/special/blastcannon.dm:149
    [SignalHandler]
    public virtual object channel_blastwave(atom source = null, DmList arguments = null)
    {
        object dot = COMSIG_CANCEL_EXPLOSION;

        float heavy = Dm.num(Dm.pow(arguments[EXARG_KEY_DEV_RANGE], BLASTCANNON_RANGE_EXP)) * BLASTCANNON_RANGE_SCALE;
        float medium = Dm.num(Dm.pow(arguments[EXARG_KEY_HEAVY_RANGE], BLASTCANNON_RANGE_EXP)) * BLASTCANNON_RANGE_SCALE;
        float light = Dm.num(Dm.pow(arguments[EXARG_KEY_LIGHT_RANGE], BLASTCANNON_RANGE_EXP)) * BLASTCANNON_RANGE_SCALE;
        float range = max(heavy, medium, light, 0);
        if (!Dm.truthy(range))
        {
            visible_message(span_warning(I18n.T("[] lets out a little \"phut\".", this)));
            return dot;
        }

        if (!ismob(loc))
        {
            Waiting.Detach(fire_dropped(heavy, medium, light)); // INVOKE_ASYNC
            return dot;
        }

        mob holding = (mob)loc;
        object target = cached_target?.resolve();
        if (target != null && (holding.get_active_held_item() == this) && cached_firer != null && (holding == cached_firer.resolve()))
            Waiting.Detach(fire_intentionally(target as atom, holding, heavy, medium, light, cached_modifiers)); // INVOKE_ASYNC
        else
            Waiting.Detach(fire_accidentally(holding, heavy, medium, light)); // INVOKE_ASYNC
        return dot;
    }

    /**
     * Prepares and fires a blastwave.
     *
     * Arguments:
     * - [target][/atom]: The thing that the blastwave is being fired at.
     * - heavy: The devastation range of the blastwave.
     * - medium: The heavy impact range of the blastwave.
     * - light: The light impact range of the blastwave.
     * - modifiers: Modifiers as a string used while firing this.
     * - spread: How inaccurate the blastwave is.
     */
    // tg: code/modules/projectiles/guns/special/blastcannon.dm:184
    public virtual async DmTask fire_blastwave(atom target = null, float heavy = 0, float medium = 0, float light = 0, object modifiers = null, float spread = 0)
    {
        turf start_turf = get_turf(this);

        object cap_multiplier = SSmapping.level_trait(start_turf.z, ZTRAIT_BOMBCAP_MULTIPLIER);
        if (cap_multiplier == null)
            cap_multiplier = 1;
        float capped_heavy = min(GLOB.MAX_EX_DEVESTATION_RANGE * Dm.num(cap_multiplier), heavy);
        float capped_medium = min(GLOB.MAX_EX_HEAVY_RANGE * Dm.num(cap_multiplier), medium);
        SSexplosions.shake_the_room(start_turf, max(heavy, medium, light, 0), (capped_heavy * 15) + (capped_medium * 20), capped_heavy, capped_medium);

        obj_projectile_blastwave blastwave = Dm.@new<obj_projectile_blastwave>(loc, heavy, medium, light);
        await blastwave.aim_projectile(target, start_turf, params2list(modifiers), spread);
        await blastwave.fire();
        cached_firer = null;
        cached_target = null;
        cached_modifiers = null;
    }

    /**
     * Handles firing the blastcannon intentionally at a specific target.
     *
     * Arguments:
     * - [firer][/mob]: The mob who is firing this weapon.
     * - [target][/atom]: The target we are firing the
     * - heavy: The devastation range of the blastwave.
     * - medium: The heavy impact range of the blastwave.
     * - light: The light impact range of the blastwave.
     * - modifiers: The modifier string to use when preparing the blastwave.
     */
    // tg: code/modules/projectiles/guns/special/blastcannon.dm:214
    public virtual async DmTask fire_intentionally(atom target = null, mob firer = null, float heavy = 0, float medium = 0, float light = 0, object modifiers = null)
    {
        firer.visible_message(
            span_danger(I18n.T("[] fires a blast wave at []!", firer, target)),
            span_danger(I18n.T("You fire a blast wave at []!", target)));
        var start_turf = get_turf(this) as turf;
        var target_turf = get_turf(target) as turf;
        message_admins(Dm.text("Blast wave fired from [] at [] ([]) by [] with power []/[]/[].", ADMIN_VERBOSEJMP(start_turf), ADMIN_VERBOSEJMP(target_turf), target, ADMIN_LOOKUPFLW(firer), heavy, medium, light));
        firer.log_message(Dm.text("fired a blast wave from [] at [] ([]) with power []/[]/[].", AREACOORD(start_turf), AREACOORD(target_turf), target, heavy, medium, light), LOG_GAME);
        firer.log_message(Dm.text("fired a blast wave from [] at [] ([]) with power []/[]/[].", AREACOORD(start_turf), AREACOORD(target_turf), target, heavy, medium, light), LOG_ATTACK, log_globally: FALSE);
        await fire_blastwave(target, heavy, medium, light, modifiers);
    }

    /**
     * Handles firing the blastcannon if it was handed to someone else between opening the valve and the bomb exploding.
     *
     * Arguments:
     * - [holder][/mob]: The person who happened to be holding the cannon when it went off.
     * - heavy: The devastation range of the blastwave.
     * - medium: The heavy impact range of the blastwave.
     * - light: The light impact range of the blastwave.
     */
    // tg: code/modules/projectiles/guns/special/blastcannon.dm:236
    public virtual async DmTask fire_accidentally(mob holder = null, float heavy = 0, float medium = 0, float light = 0)
    {
        turf target;
        bool holding;
        if (Dm.truthy(holder.is_holding(this)))
        {
            target = get_edge_target_turf(holder, holder.dir) as turf;
            holding = true;
        }
        else
        {
            target = get_edge_target_turf(holder, dir) as turf;
            holding = false;
        }

        holder.visible_message(
            span_danger(I18n.T("[] suddenly goes off[]!", this, holding ? I18n.T(" in []'s hands", holder) : null)),
            span_danger(I18n.T("[] suddenly goes off[]!", this, holding ? I18n.T(" in your hands") : null)));
        var firer = cached_firer?.resolve() as mob;
        var start_turf = get_turf(this) as turf;
        message_admins(Dm.text("Blast wave primed by [] fired from [] roughly towards [] while being held by [] with power []/[]/[].", ADMIN_LOOKUPFLW(firer), ADMIN_VERBOSEJMP(start_turf), ADMIN_VERBOSEJMP(target), ADMIN_LOOKUPFLW(holder), heavy, medium, light));
        log_game(Dm.text("Blast wave primed by [] fired from [] roughly towards [] while being held by [] with power []/[]/[].", key_name(firer), AREACOORD(start_turf), AREACOORD(target), key_name(holder), heavy, medium, light));
        await fire_blastwave(target, heavy, medium, light, null, (90 * (rand() - 0.5f))); // +- anywhere between 0 and 45 degrees
    }

    /**
     * Handles firing the blastcannon if it was dropped on the ground or shoved into a backpack.
     *
     * Arguments:
     * - heavy: The devastation range of the blastwave.
     * - medium: The heavy impact range of the blastwave.
     * - light: The light impact range of the blastwave.
     */
    // tg: code/modules/projectiles/guns/special/blastcannon.dm:264
    public virtual async DmTask fire_dropped(float heavy = 0, float medium = 0, float light = 0)
    {
        this.visible_message(span_danger(I18n.T("[] suddenly goes off!", this)));
        turf target = get_edge_target_turf(this, dir) as turf;
        var firer = cached_firer?.resolve() as mob;
        var start_turf = get_turf(this) as turf;
        message_admins(Dm.text("Blast wave primed by [] fired from [] roughly towards [] at [] ([]) with power []/[]/[].", ADMIN_LOOKUPFLW(firer), ADMIN_VERBOSEJMP(start_turf), ADMIN_VERBOSEJMP(target), ADMIN_VERBOSEJMP(loc as atom), loc, heavy, medium, light));
        log_game(Dm.text("Blast wave primed by [] fired from [] roughly towards [] at [] ([]) with power []/[]/[].", key_name(firer), AREACOORD(start_turf), AREACOORD(target), AREACOORD(loc as atom), loc, heavy, medium, light));
        await fire_blastwave(target, heavy, medium, light, null, 360 * rand());
    }

    /**
     * Handles firing the blastcannon with debug power.
     *
     * Arguments:
     * - [target][/atom]: The thing the blastcannon is being fired at.
     * - [user][/mob]: The person firing the blastcannon.
     * - modifiers: A string containing click data.
     */
    // tg: code/modules/projectiles/guns/special/blastcannon.dm:281
    public virtual async DmTask fire_debug(atom target = null, mob user = null, object modifiers = null)
    {
        await fire_intentionally(target, user, debug_power * 0.25f, debug_power * 0.5f, debug_power, modifiers);
    }
}

/// The projectile used by the blastcannon
// tg: code/modules/projectiles/guns/special/blastcannon.dm:287
public partial class obj_projectile_blastwave : obj_projectile
{
    // tg: code/modules/projectiles/guns/special/blastcannon.dm:304
    [DmParams("mapload", "heavy_ex_range", "medium_ex_range", "light_ex_range", "reactionary")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        float heavy_ex_range = Dm.num(Dm.arg(__extra, 0));
        float medium_ex_range = Dm.num(Dm.arg(__extra, 1));
        float light_ex_range = Dm.num(Dm.arg(__extra, 2));
        object reactionary = Dm.arg(__extra, 3) ?? config.Get(typeof(datum_config_entry_flag_reactionary_explosions));
        range = max(heavy_ex_range, medium_ex_range, light_ex_range, 0);
        this.heavy_ex_range = heavy_ex_range;
        this.medium_ex_range = medium_ex_range;
        this.light_ex_range = light_ex_range;
        this.reactionary = reactionary;
        return base.Initialize(mapload, Dm.forward(__extra, heavy_ex_range, medium_ex_range, light_ex_range, reactionary));
    }

    // Though the projectile itself is not damaging its effects are
    // tg: code/modules/projectiles/guns/special/blastcannon.dm:313
    public override object is_hostile_projectile()
    {
        return TRUE;
    }

    // tg: code/modules/projectiles/guns/special/blastcannon.dm:316
    public override async DmTask<object> reduce_range()
    {
        object dot = await base.reduce_range();
        if (QDELETED(this))
            return dot;

        float decrement = 1;
        atom location = loc;
        if (Dm.truthy(reactionary))
            decrement += location.explosive_resistance;

        range = max(Dm.num(range) - decrement + 1, 0); // Already decremented by 1 in the parent. Exists so that if we pass through something with negative block it extends the range.
        heavy_ex_range = max(heavy_ex_range - decrement, 0);
        medium_ex_range = max(medium_ex_range - decrement, 0);
        light_ex_range = max(light_ex_range - decrement, 0);

        if (Dm.truthy(heavy_ex_range))
            Dm.append(ref SSexplosions.highturf, location);
        else if (Dm.truthy(medium_ex_range))
            Dm.append(ref SSexplosions.medturf, location);
        else if (Dm.truthy(light_ex_range))
            Dm.append(ref SSexplosions.lowturf, location);
        else
        {
            qdel(this);
            return dot;
        }
        return dot;
    }

    // tg: code/modules/projectiles/guns/special/blastcannon.dm:341
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        return FALSE;
    }
}
