// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/reagent_dispenser.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4b: the water tank that refills a fire extinguisher): the /obj/structure/reagent_dispensers base, so that the
// water tank, the high-capacity water tank, the foam tank and the cooking oil tank (var-only subtypes) are live: the holder
// filled at Initialize, examine, the tap opened with a wrench and its leak, the hand and item paths. M5 slice 7: take_damage and atom_deconstruct
// (both reach boom). Gaps F1: knock_down and boom's chem_splash. Gaps AS1: the rigged assembly (IsSpecialAssembly, rig_boom, the
// assembly holder with an igniter attached in item_interaction and taken off in attack_hand); the elevation
// element is a deferred leaf (the client's structure top, D239). The wall dispensers (the pepper spray refiller, the virus food dispenser) came with gaps SE1; the water cooler, the irish keg, the
// serving dish and the misspelt keg type came with gaps ST3. E15: the plumbing tank conversion and the plumbed tanks (:694-737). The var blocks are generated
// (D111).

// tg: code/modules/reagents/reagent_dispenser.dm:1  #define REAGENT_SPILL_DIVISOR 200 (file-local, #undef at the end)
// tg: code/modules/reagents/reagent_dispenser.dm:2  #define COOLER_JUG_EJECT_TIME (8 SECONDS) (the water cooler's)

// tg: code/modules/reagents/reagent_dispenser.dm:4
[DmPath("/obj/structure/reagent_dispensers")]
public partial class obj_structure_reagent_dispensers : obj_structure
{
    // tg: code/modules/reagents/reagent_dispenser.dm:1  #define REAGENT_SPILL_DIVISOR 200 (file-local)
    private const float REAGENT_SPILL_DIVISOR = 200;

    // This check is necessary for assemblies to automatically detect that we are compatible
    // tg: code/modules/reagents/reagent_dispenser.dm:42
    public override object IsSpecialAssembly()
    {
        return accepts_rig;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:45
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref rig);
        return base.Destroy(force);
    }

    /**
     * rig_boom: Wrapper to log when a reagent_dispenser is set off by an assembly
     *
     */
    // tg: code/modules/reagents/reagent_dispenser.dm:53
    public virtual async DmTask rig_boom()
    {
        // GAP(types): last_rigger is generated as text (tg's "" default) and holds the rigger's key name (see item_interaction), so the
        // bomb log names the rigger in its details instead of as its user
        log_bomber(null, Dm.text("[] rigged [] exploded", last_rigger, this), this);
        await boom();
    }

    // The earlier of the type's two Initialize definitions (rules 7.5: the later one's ..() calls it).
    // tg: code/modules/reagents/reagent_dispenser.dm:56
    public object Initialize__d0(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (Dm.eq(icon_state, "water") && Dm.truthy(check_holidays(APRIL_FOOLS)))
            icon_state = "water_fools";
        if (Dm.truthy(climbable))
        {
            Waiting.NoSleep(AddElement(typeof(datum_element_climbable), Dm.named("climb_time", 4 * SECONDS), Dm.named("climb_stun", 4 * SECONDS)));
            Dm.Deferred("code/modules/reagents/reagent_dispenser.dm:63", "Initialize: AddElement(/datum/element/elevation, pixel_shift = 14): the raised position of what stands on it: the client draws a standing mob on the structure's top instead (D239, DV154)");
        }
        return dot;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:65
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(can_be_tanked))
            Dm.append(ref dot, span_notice(I18n.T("Use a sheet of iron to convert this into a plumbing-compatible tank.")));
        if (Dm.truthy(openable))
        {
            if (!Dm.truthy(leaking))
                Dm.append(ref dot, span_notice(I18n.T("Its tap looks like it could be <b>wrenched</b> open.")));
            else
                Dm.append(ref dot, span_warning(I18n.T("Its tap is <b>wrenched</b> open!")));
        }
        if (Dm.truthy(accepts_rig) && get_dist(user, this) <= 2)
        {
            if (rig != null)
                Dm.append(ref dot, span_warning(I18n.T("There is some kind of device <b>rigged</b> to the tank!")));
            else
                Dm.append(ref dot, span_notice(I18n.T("It looks like you could <b>rig</b> a device to the tank.")));
        }
        return dot;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:82  (M5 slice 7)
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        damage_type ??= BRUTE;
        damage_flag ??= 0;
        sound_effect ??= 1;
        object dot = await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        if (Dm.truthy(dot) && Dm.num(atom_integrity) > 0)
        {
            if (Dm.truthy(tank_volume) && (Dm.eq(damage_flag, BULLET) || Dm.eq(damage_flag, LASER)))
                await boom();
        }
        return dot;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:87
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_assembly_holder)) && Dm.truthy(accepts_rig))
        {
            if (rig != null)
            {
                balloon_alert(user, I18n.T("another device is in the way!"));
                return ITEM_INTERACT_BLOCKING;
            }
            obj_item_assembly_holder holder = (obj_item_assembly_holder)tool;
            if (Dm.locate<obj_item_assembly_igniter>(holder.assemblies) == null)
                return ITEM_INTERACT_BLOCKING;

            user.balloon_alert_to_viewers(I18n.T("attaching rig..."));
            add_fingerprint(user);
            if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: this)) || !Dm.truthy(await user.transferItemToLoc(holder, this)))
                return ITEM_INTERACT_BLOCKING;
            rig = holder;
            // GAP(types): holder.master = src: the port types master an item (items.dm:108, var/obj/item/master, hand-declared in
            // items.cs) and the tank is a structure; what reads it (the holder's update_appearance of its master, receive_signal, an
            // assembly window's host, a mousetrap's host) changes nothing on a tank, whose rig is set off by the igniter's signal
            await holder.on_attach();
            // assembliesoverlay = holder: tg keeps the holder itself in the var (typed /mutable_appearance, generated so here), shifts it
            // and adds it as the overlay; attack_hand cuts every overlay, so the holder is used directly
            holder.pixel_w += 6;
            holder.pixel_z += 1;
            add_overlay(holder);
            RegisterSignal(this, COMSIG_IGNITER_ACTIVATE, nameof(rig_boom));
            log_bomber(user, Dm.text("attached [] to ", holder.name), this);
            last_rigger = Dm.str(key_name(user)); // GAP(types): tg keeps the mob (see rig_boom)
            user.balloon_alert_to_viewers(I18n.T("attached rig"));
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_stack_sheet_iron)) && Dm.truthy(can_be_tanked))
        {
            obj_item_stack_sheet_iron metal_stack = (obj_item_stack_sheet_iron)tool;
            await metal_stack.use(1);
            obj_structure_reagent_dispensers_plumbed_storage new_tank = Dm.@new<obj_structure_reagent_dispensers_plumbed_storage>(drop_location());
            ((datum_reagents)new_tank.reagents).maximum_volume = ((datum_reagents)reagents).maximum_volume;
            await ((datum_reagents)reagents).trans_to(new_tank, ((datum_reagents)reagents).total_volume);
            new_tank.name = Dm.text("stationary []", name);
            new_tank.update_appearance(UPDATE_OVERLAYS);
            await new_tank.set_anchored(anchored);
            qdel(this);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:128
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == rig)
            rig = null;
        return dot;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:133
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot) || rig == null)
            return dot;
        // mousetrap rigs only make sense if you can set them off, can't step on them
        // If you see a mousetrap-rigged fuel tank, just leave it alone
        await rig.on_found();
        if (QDELETED(this))
            return dot;
        user.balloon_alert_to_viewers(I18n.T("detaching rig..."));
        if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
            return dot;
        user.balloon_alert_to_viewers(I18n.T("detached rig"));
        user.log_message(Dm.text("detached [] from [].", rig, this), LOG_GAME);
        if (!Dm.truthy(await user.put_in_hands(rig)))
            await rig.forceMove(get_turf(user));
        rig = null;
        last_rigger = null;
        cut_overlays();
        UnregisterSignal(this, COMSIG_IGNITER_ACTIVATE);
        return dot;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:154
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        create_reagents(tank_volume, reagent_flags);
        if (reagent_id != null)
            Waiting.NoSleep(((datum_reagents)reagents).add_reagent(reagent_id, tank_volume));
        object dot = Initialize__d0(mapload, __extra);
        return dot;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:166  (M5x slice 3)
    public virtual async DmTask boom()
    {
        if (QDELETED(this))
            return; // little bit of sanity sauce before we wreck ourselves somehow
        // DEVIATION(DV27): the interaction guard (D132) on the type's own boom: a dispenser whose tg boom is not translated
        // (the water cooler's tip-over) is left as it is, where this one would burst it.
        if (InertSeam.MissingOverride(this, "boom"))
            return;
        datum_reagents holder = reagents as datum_reagents;
        // GAP(D112): an inert dispenser never ran Initialize, so it has no reagents to burst with (a blast or a singularity reaching it)
        if (holder == null && InertSeam.IsInert(this))
            return;
        datum_reagent_fuel volatiles = holder.has_reagent(typeof(datum_reagent_fuel)) as datum_reagent_fuel;
        float fuel_amt = 0;
        if (volatiles != null && Dm.num(volatiles.volume) >= 25)
        {
            fuel_amt = Dm.num(volatiles.volume);
            await holder.del_reagent(typeof(datum_reagent_fuel)); // not actually used for the explosion
        }
        if (Dm.truthy(holder.total_volume))
        {
            if (!Dm.truthy(fuel_amt))
                visible_message(span_danger(I18n.T("\\The [] ruptures!", this)));
            // Leave it up to future terrorists to figure out the best way to mix reagents with fuel for a useful boom here
            await chem_splash(loc, null, 2 + floor((holder.total_volume + fuel_amt) / 1000), Dm.list(holder), extra_heat: (fuel_amt / 50), adminlog: (fuel_amt < 25) ? TRUE : FALSE);
        }

        if (Dm.truthy(fuel_amt)) // with that done, actually explode
        {
            visible_message(span_danger(I18n.T("\\The [] explodes!", this)));
            // old code for reference:
            // standard fuel tank = 1000 units = heavy_impact_range = 1, light_impact_range = 5, flame_range = 5
            // big fuel tank = 5000 units = devastation_range = 1, heavy_impact_range = 2, light_impact_range = 7, flame_range = 12
            // It did not account for how much fuel was actually in the tank at all, just the size of the tank.
            // I encourage others to better scale these numbers in the future.
            // As it stands this is a minor nerf in exchange for an easy bombing technique working that has been broken for a while.
            // switch(fuel_amt): DM's "if(a to b)" ranges are inclusive and tested in order
            if (fuel_amt >= 25 && fuel_amt <= 150)
                await explosion(this, light_impact_range: 1, flame_range: 2);
            else if (fuel_amt >= 150 && fuel_amt <= 300)
                await explosion(this, light_impact_range: 2, flame_range: 3);
            else if (fuel_amt >= 300 && fuel_amt <= 750)
                await explosion(this, heavy_impact_range: 1, light_impact_range: 3, flame_range: 5);
            else if (fuel_amt >= 750 && fuel_amt <= 1500)
                await explosion(this, heavy_impact_range: 1, light_impact_range: 4, flame_range: 6);
            else if (fuel_amt >= 1500)
                await explosion(this, devastation_range: 1, heavy_impact_range: 2, light_impact_range: 6, flame_range: 8);
        }
        qdel(this);
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:201  (M5 slice 7)
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        if (!Dm.truthy(disassembled))
            await boom();
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:204
    public virtual async DmTask<object> tank_leak()
    {
        datum_reagents holder = reagents as datum_reagents;
        if (Dm.truthy(leaking) && holder != null && holder.total_volume >= amount_to_leak)
        {
            await holder.expose(get_turf(this), TOUCH, amount_to_leak / max(amount_to_leak, holder.total_volume));
            await holder.remove_reagent(reagent_id, amount_to_leak);
            playsound(this, Dm.resource("sound/effects/glug.ogg"), 33, TRUE, SILENCED_SOUND_EXTRARANGE);
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:213  (gaps F1; nothing in tg calls it)
    public virtual async DmTask knock_down()
    {
        datum_reagents holder = reagents as datum_reagents;
        float range = holder.total_volume / REAGENT_SPILL_DIVISOR;
        await do_chem_smoke(round(range), drop_location() as atom, drop_location(), carry: holder, silent: FALSE, log: TRUE);
        await holder.clear_reagents();
        qdel(this);
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:218
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wrench_act(user, tool);
        if (!Dm.truthy(openable))
            return FALSE;
        leaking = Dm.not(leaking);
        balloon_alert(user, I18n.T("[] tap", Dm.truthy(leaking) ? I18n.T("opened") : I18n.T("closed")));
        user.log_message(Dm.text("[] [].", Dm.truthy(leaking) ? "opened" : "closed", this), LOG_GAME);
        await tank_leak();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:227
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        momentum_change ??= TRUE;
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        await tank_leak();
        return dot;
    }
}

// tg: code/modules/reagents/reagent_dispenser.dm:256 (M5x slice 3: the fuel tank, a blast's and a fire's chained explosion;
// blob_act is left in docs/ai/specs/live-partial.txt; zap_act: the M7 zaps slice; bullet_act: the M7 projectiles slice). The var block is generated.
[DmPath("/obj/structure/reagent_dispensers/fueltank")]
public partial class obj_structure_reagent_dispensers_fueltank : obj_structure_reagent_dispensers
{
    // tg: code/modules/reagents/reagent_dispenser.dm:265
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (Dm.truthy(check_holidays(APRIL_FOOLS)))
            icon_state = "fuel_fools";
        return dot;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:274  (set waitfor = FALSE, inherited from /atom/ex_act: the wrapper of
    // waiting-model.md 5.4)
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(ex_act__body(dot), dot);
    }

    private async DmTask<object> ex_act__body(DotBox<object> dot)
    {
        await boom();
        return TRUE;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:278
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        await boom();
        return null;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:280  (the M7 zaps slice)
    public override async DmTask<object> zap_act(object power = null, object zap_flags = null)
    {
        object dot = null;
        dot = await base.zap_act(power, zap_flags); //extend the zap
        if (Dm.truthy(Dm.bitand(ZAP_OBJ_DAMAGE, zap_flags)))
            await boom();
        return dot;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:285  (the M7 projectiles slice)
    public override async DmTask<object> bullet_act(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        if (Dm.num(hitting_projectile.damage) > 0 && ((hitting_projectile.damage_type == BURN) || (hitting_projectile.damage_type == BRUTE)))
        {
            log_bomber(hitting_projectile.firer, "detonated a", this, "via projectile");
            await boom();
            return await hitting_projectile.on_hit(this, 0);
        }

        // we override parent like this because otherwise we won't actually properly log the fact that a projectile caused this welding tank to explode.
        // if this sucks, feel free to change it, but make sure the damn thing will log. thanks.
        return await base.bullet_act(hitting_projectile, def_zone, piercing_hit, blocked);
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:295
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        datum_reagents holder = reagents as datum_reagents;
        obj_item_weldingtool refilling_welder = tool as obj_item_weldingtool;
        if (refilling_welder != null && !Dm.truthy(refilling_welder.welding))
        {
            if (Dm.truthy(((datum_reagents)refilling_welder.reagents).has_reagent(typeof(datum_reagent_fuel), refilling_welder.max_fuel)))
            {
                to_chat(user, span_warning(I18n.T("Your [] is already full!", refilling_welder.name)));
                return ITEM_INTERACT_BLOCKING;
            }
            await holder.trans_to(refilling_welder, refilling_welder.max_fuel, transferred_by: user);
            user.visible_message(span_notice(I18n.T("[] refills [] [].", user, user.p_their(), refilling_welder.name)), span_notice(I18n.T("You refill [].", refilling_welder)));
            playsound(this, Dm.resource("sound/effects/refill.ogg"), 50, TRUE);
            refilling_welder.update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        obj_item_lighter refilling_lighter = tool as obj_item_lighter;
        if (refilling_lighter != null && !Dm.truthy(refilling_lighter.lit))
        {
            if (Dm.truthy(((datum_reagents)refilling_lighter.reagents).has_reagent(typeof(datum_reagent_fuel), refilling_lighter.maximum_fuel)))
            {
                to_chat(user, span_warning(I18n.T("Your [] is already full!", refilling_lighter.name)));
                return ITEM_INTERACT_BLOCKING;
            }
            await holder.trans_to(refilling_lighter, refilling_lighter.maximum_fuel, transferred_by: user);
            user.visible_message(span_notice(I18n.T("[] refills [] [].", user, user.p_their(), refilling_lighter.name)), span_notice(I18n.T("You refill [].", refilling_lighter)));
            playsound(this, Dm.resource("sound/effects/refill.ogg"), 25, TRUE);
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(holder.has_reagent(typeof(datum_reagent_fuel))))
        {
            to_chat(user, span_warning(I18n.T("[] is out of fuel!", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        user.visible_message(
            span_danger(I18n.T("[] catastrophically fails at refilling [] []!", user, user.p_their(), tool.name)),
            span_userdanger(I18n.T("That was stupid of you.")));
        log_bomber(user, "detonated a", this, Dm.text("via []", tool.name));
        await boom();
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/reagents/reagent_dispenser.dm:339  (gaps SE1, D333: the pepper spray refiller on the brig's walls)
public partial class obj_structure_reagent_dispensers_wall_peppertank : obj_structure_reagent_dispensers_wall
{
    // tg: code/modules/reagents/reagent_dispenser.dm:347
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(1))
            desc = I18n.T("IT'S PEPPER TIME, BITCH!");
        if (Dm.truthy(mapload))
            Waiting.NoSleep(find_and_mount_on_atom());
        return dot;
    }
}

// tg: code/modules/reagents/reagent_dispenser.dm:658  (gaps SE1: the virology's virus food dispenser, mounted the same way)
public partial class obj_structure_reagent_dispensers_wall_virusfood : obj_structure_reagent_dispensers_wall
{
    // tg: code/modules/reagents/reagent_dispenser.dm:666
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload))
            Waiting.NoSleep(find_and_mount_on_atom());
        return dot;
    }
}

// tg: code/modules/reagents/reagent_dispenser.dm:588  (syndicate N1a, D316: the beer nuke's keg; tg's keg Initialize is written for a
// misspelt /obj/structure/reagent_dispenders/keg, a type of its own that nothing makes, so the keg has none)
public partial class obj_structure_reagent_dispensers_keg : obj_structure_reagent_dispensers
{
    // tg: code/modules/reagents/reagent_dispenser.dm:599
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(keg_print))
            Dm.append(ref dot, keg_print);
        return dot;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:604
    public override async DmTask<object> blob_act(obj_structure_blob B = null)
    {
        await explosion(this, heavy_impact_range: 3, light_impact_range: 5, flame_range: 10, flash_range: 7);
        if (!QDELETED(this))
            qdel(this);
        return null;
    }
}

// tg: code/modules/reagents/reagent_dispenser.dm:694  (E15)
public partial class obj_structure_reagent_dispensers_plumbed : obj_structure_reagent_dispensers
{
    // tg: code/modules/reagents/reagent_dispenser.dm:701
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_simple_supply)));
        return dot;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:705
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/reagents/reagent_dispenser.dm:710  (E15)
public partial class obj_structure_reagent_dispensers_plumbed_storage : obj_structure_reagent_dispensers_plumbed
{
    // tg: code/modules/reagents/reagent_dispenser.dm:715
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        return dot;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:720
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(reagents))
            return dot;

        if (!Dm.truthy(((datum_reagents)reagents).total_volume))
            return dot;

        mutable_appearance tank_color = (mutable_appearance)mutable_appearance(Dm.resource("icons/obj/medical/chemical_tanks.dmi"), "tank_chem_overlay");
        tank_color.color = mix_color_from_reagents(((datum_reagents)reagents).reagent_list);
        Dm.append(ref dot, tank_color);
        return dot;
    }
}

// gaps ST3 (D333): the water cooler (its paper cups, its jug swapped and taken off, tipped over and stood up again, its water drawn
// in the jug), the special irish keg's random cocktail, the serving dish's wrench, and the misspelt keg type's Initialize.

// tg: code/modules/reagents/reagent_dispenser.dm:355
public partial class obj_structure_reagent_dispensers_water_cooler : obj_structure_reagent_dispensers
{
    // tg: code/modules/reagents/reagent_dispenser.dm:371
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(2) && Dm.truthy(mapload))
            Waiting.NoSleep(((datum_reagents)reagents).convert_reagent(typeof(datum_reagent_water), typeof(datum_reagent_consumable_fruit_punch)));
        create_jug();
        update_appearance();
        return dot;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:378
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        our_jug = null;
        return dot;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:382
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (paper_cups > 1)
            Dm.append(ref dot, I18n.T("There are [] paper cups left.", paper_cups));
        else if (paper_cups == 1)
            Dm.append(ref dot, I18n.T("There is one paper cup left."));
        else
            Dm.append(ref dot, I18n.T("There are no paper cups left."));
        return dot;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:391
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.truthy(tipped))
        {
            balloon_alert(user, I18n.T("un-tipping..."));
            if (!Dm.truthy(await do_after(user, 5 * SECONDS, this)))
                return dot;
            tipped = FALSE;
            update_appearance();
            return dot;
        }

        if (Dm.truthy((user as mob_living)?.combat_mode) && our_jug != null)
        {
            balloon_alert(user, I18n.T("removing jug..."));
            if (!Dm.truthy(await do_after(user, 8 * SECONDS, this))) // COOLER_JUG_EJECT_TIME
                return dot;
            await eject_jug(user as mob_living);
            return dot;
        }

        if (!Dm.truthy(paper_cups))
        {
            to_chat(user, span_warning(I18n.T("There aren't any cups left!")));
            return dot;
        }
        user.visible_message(span_notice(I18n.T("[] takes a cup from [].", user, this)), span_notice(I18n.T("You take a paper cup from [].", this)));
        obj_item_reagent_containers_cup_glass_sillycup new_cup = Dm.@new<obj_item_reagent_containers_cup_glass_sillycup>(get_turf(this));
        await user.put_in_hands(new_cup);
        paper_cups--;
        return dot;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:421
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(tipped))
            icon_state = "water_cooler_disgraced";
        else
        {
            if (our_jug == null)
                icon_state = "water_cooler_forlorn";
            else
                icon_state = "water_cooler";
        }
        return dot;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:431
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        datum_reagents holder = reagents as datum_reagents;
        if (holder == null)
            return dot;

        if (!Dm.truthy(holder.total_volume))
            return dot;

        object mixcolor = null;
        float vol_counter = 0;
        float vol_temp;

        foreach (datum_reagent stored_reagent in Dm.each<datum_reagent>(holder.reagent_list))
        {
            vol_temp = Dm.num(stored_reagent.volume);
            vol_counter += vol_temp;

            object chosen_color = stored_reagent.color;
            if (stored_reagent is datum_reagent_water)
            {
                if (!Dm.truthy(mixcolor))
                {
                    mixcolor = "#2694D6"; //Override the water to be a nice blue
                    continue;
                }
                else
                    chosen_color = "#2694D6";
            }
            else
            {
                if (!Dm.truthy(mixcolor))
                {
                    mixcolor = stored_reagent.color;
                    continue;
                }
                else
                    chosen_color = stored_reagent.color;
            }

            if (Dm.length(mixcolor) >= Dm.length(chosen_color))
                mixcolor = BlendRGB(mixcolor, chosen_color, vol_temp / vol_counter);
            else
                mixcolor = BlendRGB(chosen_color, mixcolor, vol_temp / vol_counter);
        }

        if (Dm.truthy(mixcolor))
        {
            string overlay_tag;
            if (vol_counter > 100)
                overlay_tag = "100";
            else
                overlay_tag = "50"; //Can't be 0, because there'd be no mixcolor
            mutable_appearance tank_overlay = mutable_appearance(Dm.resource("icons/obj/medical/chemical_tanks.dmi"), "water_cooler_overlay" + overlay_tag) as mutable_appearance;
            tank_overlay.color = mixcolor;
            Dm.append(ref dot, tank_overlay);
        }
        return dot;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:476
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;

        object dot = ITEM_INTERACT_BLOCKING;
        if (Dm.eq(await default_unfasten_wrench(user, tool, time: 6 * SECONDS), SUCCESSFUL_UNFASTEN))
            return ITEM_INTERACT_SUCCESS;
        return dot;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:484
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        if (Dm.truthy(tipped))
        {
            balloon_alert(user, I18n.T("it's already tipped!"));
            return null;
        }

        if (Dm.truthy(anchored))
        {
            balloon_alert(user, I18n.T("it's anchored!"));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }

        if (!Dm.truthy(await do_after(user, 1.5f * SECONDS, this)))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        Waiting.Detach(boom()); // INVOKE_ASYNC(src, PROC_REF(boom), user): boom takes no argument, DM drops the user
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:498
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!(tool is obj_item_reagent_containers_cooler_jug))
            return null;

        if (Dm.truthy(tipped))
        {
            balloon_alert(user, I18n.T("it's tipped!"));
            return null;
        }

        obj_item_reagent_containers_cooler_jug new_jug = (obj_item_reagent_containers_cooler_jug)tool;
        balloon_alert(user, I18n.T("replacing jug..."));
        if (!Dm.truthy(await do_after(user, 8 * SECONDS, this))) // COOLER_JUG_EJECT_TIME
            return null;

        if (!Dm.truthy(await user.transferItemToLoc(new_jug, this)))
            return ITEM_INTERACT_BLOCKING;

        // tg passes the old jug as eject_jug's throw_away: a jug that was in throws itself out of the hands it goes to and the
        // cooler's water is poured away before the new jug's goes in (faithful)
        await eject_jug(user, our_jug);
        our_jug = new_jug;
        await ((datum_reagents)our_jug.reagents).trans_to(reagents, tank_volume);
        balloon_alert(user, I18n.T("attached"));
        user.log_message(Dm.text("attached a [] to [] at [] containing ([])", new_jug, this, AREACOORD(this), ((datum_reagents)new_jug.reagents).get_reagent_log_string()), LOG_ATTACK);
        add_fingerprint(user);
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:523
    public override async DmTask boom()
    {
        if (QDELETED(this))
            return;
        datum_reagents holder = (datum_reagents)reagents;
        if (Dm.truthy(holder.total_volume))
        {
            visible_message(span_danger(I18n.T("\\The [] flips on it's side and spills everywhere!", this)));
            await chem_splash(get_turf(this), null, 2 + floor(Dm.num(holder.total_volume) / 1000), Dm.list(holder));
        }
        await eject_jug(throw_away: TRUE);
        playsound(this, Dm.resource("sound/effects/glass/glassbash.ogg"), 100);
        tip_over();
    }

    ///Creates an empty jug inside of the cooler. Doesn't need to be filled bc it absorbs the cooler's reagent on eject.
    // tg: code/modules/reagents/reagent_dispenser.dm:534
    public virtual void create_jug()
    {
        our_jug = Dm.@new<obj_item_reagent_containers_cooler_jug>(this);
    }

    ///Eject the jug in a variety of ways. If there is a user, the jug goes into their hands. throw_away is passed on tip, to empty and throw the jug away. We delete the reagents since we create a splash before this is called.
    // tg: code/modules/reagents/reagent_dispenser.dm:538
    public virtual async DmTask eject_jug(mob_living user = null, object throw_away = null)
    {
        throw_away ??= FALSE;
        if (our_jug == null)
            return;

        if (user != null)
            await user.put_in_hands(our_jug);
        else
            await our_jug.forceMove(drop_location() as atom);

        if (Dm.truthy(throw_away))
        {
            turf turf_to_throw_at = get_ranged_target_turf(this, pick(GLOB.alldirs), 2) as turf;
            Waiting.Detach(our_jug.throw_at(turf_to_throw_at, 2, 3));
            await ((datum_reagents)reagents).remove_all(tank_volume); //Gets spilled on floor during boom()
        }
        else
            await ((datum_reagents)reagents).trans_to(((datum_reagents)our_jug.reagents), tank_volume);

        our_jug = null;
        update_appearance();
    }

    ///Handles the visual stuff related to the cooler itself tipping.
    // tg: code/modules/reagents/reagent_dispenser.dm:558
    public virtual void tip_over()
    {
        tipped = TRUE;
        update_appearance();
    }
}

///Pre-tipped version for mapping.
// tg: code/modules/reagents/reagent_dispenser.dm:564
public partial class obj_structure_reagent_dispensers_water_cooler_fallen : obj_structure_reagent_dispensers_water_cooler
{
    // tg: code/modules/reagents/reagent_dispenser.dm:568
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        tip_over();
        return dot;
    }

    // tg: code/modules/reagents/reagent_dispenser.dm:572
    public override void create_jug()
    {
        return;
    }
}

// tg: code/modules/reagents/reagent_dispenser.dm:576
public partial class obj_structure_reagent_dispensers_water_cooler_jugless : obj_structure_reagent_dispensers_water_cooler
{
    // tg: code/modules/reagents/reagent_dispenser.dm:579
    public override void create_jug()
    {
        return;
    }
}

// tg: code/modules/reagents/reagent_dispenser.dm:595  (tg's misspelt type: a root of its own that nothing makes; translated as it is)
public partial class obj_structure_reagent_dispenders_keg : obj_structure_reagent_dispenders
{
    // tg: code/modules/reagents/reagent_dispenser.dm:595
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }
}

// tg: code/modules/reagents/reagent_dispenser.dm:639
public partial class obj_structure_reagent_dispensers_keg_gold_irish : obj_structure_reagent_dispensers_keg_gold
{
    // tg: code/modules/reagents/reagent_dispenser.dm:643
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        reagent_id = pick(
            typeof(datum_reagent_consumable_ethanol_irishcoffee),
            typeof(datum_reagent_consumable_ethanol_irish_cream),
            typeof(datum_reagent_consumable_ethanol_irishcarbomb),
            typeof(datum_reagent_consumable_ethanol_b52)) as Type;
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/reagents/reagent_dispenser.dm:679
public partial class obj_structure_reagent_dispensers_servingdish : obj_structure_reagent_dispensers
{
    // tg: code/modules/reagents/reagent_dispenser.dm:686
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }
}

