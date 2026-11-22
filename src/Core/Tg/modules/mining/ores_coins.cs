// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/ores_coins.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J6 (D298): the ores and gibtonite (lines 1-408); gaps P4a (D327): the coins (:515-end), money with the economy. The ore redemption machine's
// on_orm_collection is translated, unreached until the machine. The sand's storm_hating and the raptors' and golems' food are inert
// components (Lavaland's). Gaps AS1: the rig on gibtonite (an assembly holder with an igniter, attached and wrenched off).

// tg: code/modules/mining/ores_coins.dm:6
public partial class obj_item_stack_ore : obj_item_stack
{
    // tg: code/modules/mining/ores_coins.dm:1  #define ORESTACK_OVERLAYS_MAX 10
    private const float ORESTACK_OVERLAYS_MAX = 10;

    // tg: code/modules/mining/ores_coins.dm:37
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        float difference = min(ORESTACK_OVERLAYS_MAX, Dm.num(amount)) - (LAZYLEN(stack_overlays) + 1);
        if (!Dm.truthy(difference))
            return dot;

        if (difference < 0 && LAZYLEN(stack_overlays) > 0) //amount < stack_overlays, remove excess.
        {
            if (LAZYLEN(stack_overlays) - difference <= 0)
            {
                stack_overlays = null;
                return dot;
            }
            stack_overlays.len = Dm.length(stack_overlays) + (int)difference;
        }
        else //amount > stack_overlays, add some.
        {
            for (int i = 1; i <= difference; i++)
            {
                mutable_appearance newore = mutable_appearance(icon, icon_state) as mutable_appearance;
                newore.pixel_w = rand(-8, 8);
                newore.pixel_z = rand(-8, 8);
                LAZYADD(ref stack_overlays, newore);
            }
        }

        if (stack_overlays != null)
            Dm.append(ref dot, stack_overlays);
        return dot;
    }

    /// Called when the ore is collected by an ore redemption machine. returns the ore itself.
    // tg: code/modules/mining/ores_coins.dm:64
    public virtual object on_orm_collection() => refined_type == null ? null : this;

    // tg: code/modules/mining/ores_coins.dm:67
    public override async DmTask<object> welder_act(mob_living user = null, obj_item I = null)
    {
        await base.welder_act(user, I);
        if (refined_type == null)
            return TRUE;

        if (!Dm.truthy(await I.use_tool(this, user, 0, volume: 50)))
            return TRUE;

        float sheets_made = Dm.num(values_sum(mats_per_unit)) / SHEET_MATERIAL_AMOUNT;
        Dm.@new(refined_type, drop_location(), max(1, floor(sheets_made)));
        await use(max(1, ceil(1 / sheets_made)));
        return TRUE;
    }

    // tg: code/modules/mining/ores_coins.dm:80
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        object dot = await base.fire_act(exposed_temperature, exposed_volume);
        if (refined_type == null)
            return dot;

        float probability = rand(0, 100) / 100f;
        float burn_value = probability * Dm.num(amount);
        float amount_refined = floor(burn_value * Dm.num(values_sum(mats_per_unit)) / SHEET_MATERIAL_AMOUNT);
        if (amount_refined > 1)
            Dm.@new(refined_type, drop_location(), amount_refined);
        qdel(this);
        return dot;
    }

    // tg: code/modules/mining/ores_coins.dm:502
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        pixel_x = Dm.num(base_pixel_x) + rand(0, 16) - 8;
        pixel_y = Dm.num(base_pixel_y) + rand(0, 8) - 8;
        return dot;
    }

    // tg: code/modules/mining/ores_coins.dm:507
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        if (Dm.num(severity) >= EXPLODE_DEVASTATE)
        {
            qdel(this);
            return TRUE;
        }

        return FALSE;
    }
}

// tg: code/modules/mining/ores_coins.dm:124
public partial class obj_item_stack_ore_glass : obj_item_stack_ore
{
    // tg: code/modules/mining/ores_coins.dm:142
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_storm_hating)));
        return dot;
    }

    //we need to smelt the glass beforehand because the silo and orm don't accept sand mats
    // tg: code/modules/mining/ores_coins.dm:146
    public override object on_orm_collection()
    {
        //If we spawn the sheet of glass on the turf the ORM is "listening" to, it'll get redeemed before we can use it as return value and weird stuff my happen.
        object glass = Dm.@new(refined_type, null, amount);
        qdel(this);
        return glass;
    }

    // tg: code/modules/mining/ores_coins.dm:152
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.sand_recipes);
        return dot;
    }

    // tg: code/modules/mining/ores_coins.dm:156
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        if (Dm.truthy(base.throw_impact(hit_atom, throwingdatum, __extra)) || !ishuman(hit_atom))
            return null;
        mob_living_carbon_human C = hit_atom as mob_living_carbon_human;
        if (Dm.truthy(C.is_eyes_covered()))
        {
            C.visible_message(span_danger(I18n.T("[]'s eye protection blocks the sand!", C)), span_warning(I18n.T("Your eye protection blocks the sand!")));
            return null;
        }
        Waiting.Detach(C.adjust_eye_blur(12 * SECONDS));
        Waiting.Detach(C.adjust_stamina_loss(15)); //the pain from your eyes burning does stamina damage
        Waiting.Detach(C.adjust_confusion(5 * SECONDS));
        to_chat(C, span_userdanger(I18n.T("\\The [] gets into your eyes! The pain, it burns!", this)));
        qdel(this);
        return null;
    }

    // tg: code/modules/mining/ores_coins.dm:169
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        if (Dm.truthy(severity))
        {
            qdel(this);
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/mining/ores_coins.dm:175
    public override async DmTask<object> interact_with_atom(atom target = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(target, user, modifiers);
        if (Dm.truthy(dot))
            return dot;

        if (!Dm.truthy(target.is_dunkable()))
            return NONE;

        datum_reagent_cement cement = (target.reagents as datum_reagents)?.has_reagent(typeof(datum_reagent_cement), check_subtypes: TRUE) as datum_reagent_cement;
        if (cement == null)
            return NONE;

        float sand_to_use = min(floor(Dm.num(cement.volume) / Dm.num(cement.units_per_aggregate)), Dm.num(amount));
        if (!Dm.truthy(sand_to_use))
        {
            to_chat(user, span_warning(I18n.T("You don't have enough [] to mix with [].", cement.name, this)));
            return ITEM_INTERACT_BLOCKING;
        }

        await use(sand_to_use);
        await (target.reagents as datum_reagents).convert_reagent(cement.type, cement.concrete_type, conversion_volume: sand_to_use * Dm.num(cement.units_per_aggregate));
        user.visible_message(span_warning(I18n.T("[] slips something into []!", user, target)), span_notice(I18n.T("You dissolve [] in [].", this, target)), null, 2);
        playsound(target, Dm.resource("sound/effects/bubbles/bubbles.ogg"), 50, TRUE);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/mining/ores_coins.dm:223
public partial class obj_item_stack_ore_plasma : obj_item_stack_ore
{
    // tg: code/modules/mining/ores_coins.dm:237
    public override async DmTask<object> welder_act(mob_living user = null, obj_item I = null)
    {
        to_chat(user, span_warning(I18n.T("You can't hit a high enough temperature to smelt [] properly!", this)));
        return TRUE;
    }
}

// tg: code/modules/mining/ores_coins.dm:273
public partial class obj_item_stack_ore_diamond : obj_item_stack_ore
{
    // tg: code/modules/mining/ores_coins.dm:288
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // AddElement(/datum/element/raptor_food, ...): the raptors' food (the raptors, not ported)
        Dm.Deferred("code/modules/mining/ores_coins.dm:290", "Initialize: AddElement(/datum/element/raptor_food): the raptors' food (the raptors, not ported)");
        return dot;
    }
}

// tg: code/modules/mining/ores_coins.dm:333
public partial class obj_item_gibtonite : obj_item
{
    // tg: code/modules/mining/ores_coins.dm:355
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed), Dm.named("require_twohands", TRUE)));
        // AddComponent(/datum/component/golem_food, ...): the golems' food (the golems, not ported)
        Dm.Deferred("code/modules/mining/ores_coins.dm:358", "Initialize: AddComponent(/datum/component/golem_food): the golems' food (the golems, not ported)");
        return dot;
    }

    // tg: code/modules/mining/ores_coins.dm:360
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (rig != null)
            Dm.append(ref dot, span_warning(I18n.T("There is some kind of device <b>rigged</b> to it!")));
        else
            Dm.append(ref dot, span_notice(I18n.T("You could <b>rig</b> something to it.")));
        return dot;
    }

    // tg: code/modules/mining/ores_coins.dm:367
    public override object Destroy(float? force = null)
    {
        qdel(rig);
        rig = null;
        rig_overlay = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mining/ores_coins.dm:372
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == rig)
        {
            rig = null;
            attacher = null;
            cut_overlays();
            UnregisterSignal(this, COMSIG_IGNITER_ACTIVATE);
        }
        return dot;
    }

    // tg: code/modules/mining/ores_coins.dm:380
    public override object IsSpecialAssembly() => TRUE;

    // tg: code/modules/mining/ores_coins.dm:383
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_assembly_holder)) && rig == null)
        {
            // gaps AS1: the assembly holder
            obj_item_assembly_holder holder = (obj_item_assembly_holder)tool;
            if (Dm.locate<obj_item_assembly_igniter>(holder.assemblies) == null)
                return NONE;

            if (!Dm.truthy(await user.transferItemToLoc(holder, this)))
                return ITEM_INTERACT_BLOCKING;

            add_fingerprint(user);
            rig = holder;
            holder.master = this;
            await holder.on_attach();
            // rig_overlay = holder: tg keeps the holder itself in the var (typed /mutable_appearance, generated so here), shifts it and
            // adds it as the overlay; the var is read nowhere else (Exited cuts every overlay), so the holder is used directly
            holder.pixel_z -= 5;
            add_overlay(holder);
            RegisterSignal(this, COMSIG_IGNITER_ACTIVATE, nameof(igniter_prime));
            log_bomber(user, Dm.text("attached [] to ", holder), this);
            attacher = key_name(user);
            user.balloon_alert_to_viewers(I18n.T("attached rig"));
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.eq(tool.tool_behaviour, TOOL_MINING) || istype(tool, typeof(obj_item_resonator)) || tool.force >= 10)
        {
            GibtoniteReaction(user, "A resonator has primed for detonation a");
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_mining_scanner)) || istype(tool, typeof(obj_item_t_scanner_adv_mining_scanner)))
        {
            defuse(user);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/mining/ores_coins.dm:415
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (rig == null)
            return NONE;

        await rig.on_found();
        if (QDELETED(this))
            return ITEM_INTERACT_BLOCKING;

        user.balloon_alert_to_viewers(I18n.T("detached rig"));
        user.log_message(Dm.text("detached [] from [].", rig, this), LOG_GAME);
        await user.put_in_hands(rig);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mining/ores_coins.dm:428
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        defuse(user);
        return ITEM_INTERACT_SUCCESS;
    }

    /// Stop the reaction and reduce ore explosive power
    // tg: code/modules/mining/ores_coins.dm:433
    public virtual void defuse(mob defuser = null)
    {
        if (!Dm.truthy(primed))
            return;
        primed = FALSE;
        if (Dm.truthy(det_timer))
            deltimer(det_timer);
        defuser?.visible_message(span_notice(I18n.T("The chain reaction stopped! ...The ore's quality looks diminished.")), span_notice(I18n.T("You stopped the chain reaction. ...The ore's quality looks diminished.")));
        icon_state = "gibtonite";
        quality = GIBTONITE_QUALITY_LOW;
    }

    // tg: code/modules/mining/ores_coins.dm:443
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (wires != null)
            return await wires.interact(user);
        return await base.attack_self(user, modifiers, __extra);
    }

    // tg: code/modules/mining/ores_coins.dm:449
    public override async DmTask<object> bullet_act(obj_projectile proj = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        GibtoniteReaction(proj.firer as mob, "A projectile has primed for detonation a");
        return await base.bullet_act(proj, def_zone, piercing_hit, blocked);
    }

    // tg: code/modules/mining/ores_coins.dm:453
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        GibtoniteReaction(null, "An explosion has primed for detonation a");
        return TRUE;
    }

    // tg: code/modules/mining/ores_coins.dm:457
    public virtual void GibtoniteReaction(mob user = null, string triggered_by = null)
    {
        if (Dm.truthy(primed))
            return;
        primed = TRUE;
        playsound(this, Dm.resource("sound/effects/hit_on_shattered_glass.ogg"), 50, TRUE);
        icon_state = "gibtonite_active";
        bool notify_admins = false;
        if (!Dm.truthy(is_mining_level(z))) //Only annoy the admins ingame if we're triggered off the mining zlevel
            notify_admins = true;

        if (user != null)
            user.visible_message(span_warning(I18n.T("[] strikes \\the [], causing a chain reaction!", user, this)), span_danger(I18n.T("You strike \\the [], causing a chain reaction.", this)));

        object attacher_text = Dm.truthy(attacher) ? Dm.text("Igniter attacher: []", ADMIN_LOOKUPFLW(attacher)) : null;

        if (Dm.truthy(triggered_by))
            log_bomber(user, triggered_by, this, attacher_text, notify_admins ? TRUE : FALSE);
        else
            log_bomber(user, "Something has primed a", this, Dm.text("for detonation.[][]", Dm.truthy(attacher_text) ? " " : "", attacher_text), notify_admins ? TRUE : FALSE);

        det_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(detonate), notify_admins ? TRUE : FALSE), det_time, TIMER_STOPPABLE, file: "code/modules/mining/ores_coins.dm", line: 477);
    }

    // tg: code/modules/mining/ores_coins.dm:479
    public virtual void detonate(object notify_admins = null)
    {
        if (Dm.truthy(primed))
        {
            if (Dm.eq(quality, GIBTONITE_QUALITY_HIGH))
                Waiting.NoSleep(explosion(this, devastation_range: 2, heavy_impact_range: 4, light_impact_range: 9, flame_range: 0, flash_range: 0, adminlog: Dm.num(notify_admins)));
            else if (Dm.eq(quality, GIBTONITE_QUALITY_MEDIUM))
                Waiting.NoSleep(explosion(this, devastation_range: 1, heavy_impact_range: 2, light_impact_range: 5, flame_range: 0, flash_range: 0, adminlog: Dm.num(notify_admins)));
            else if (Dm.eq(quality, GIBTONITE_QUALITY_LOW))
                Waiting.NoSleep(explosion(this, heavy_impact_range: 1, light_impact_range: 3, flame_range: 0, flash_range: 0, adminlog: Dm.num(notify_admins)));
            qdel(this);
        }
    }

    // tg: code/modules/mining/ores_coins.dm:490
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        object dot = base.throw_impact(hit_atom, throwingdatum, __extra);
        if (Dm.num(throwingdatum.dist_travelled) < 2 || !isliving(hit_atom))
            return dot;
        mob_living hit_mob = hit_atom as mob_living;
        Waiting.Detach(hit_mob.Paralyze(1.5f * SECONDS));
        Waiting.Detach(hit_mob.Knockdown(8 * SECONDS));
        return dot;
    }

    // tg: code/modules/mining/ores_coins.dm:498
    public virtual void igniter_prime(params object[] __extra)
    {
        GibtoniteReaction(null, "An attached rig has primed a");
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/modules/mining/ores_coins.dm:136  GLOBAL_LIST_INIT(sand_recipes, ...)
    public DmList sand_recipes;

    public virtual void InitGlobalsand_recipes()
    {
        sand_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("pile of dirt", typeof(obj_machinery_hydroponics_soil), 3, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_TOOLS)),
            Dm.@new<datum_stack_recipe>("sandstone", typeof(obj_item_stack_sheet_mineral_sandstone), 1, 1, 50, Dm.named("crafting_flags", CRAFT_NO_MATERIALS), Dm.named("category", CAT_MISC)),
            Dm.@new<datum_stack_recipe>("aesthetic volcanic floor tile", typeof(obj_item_stack_tile_basalt), 2, 1, 50, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_TILES)));
        Dm.append(ref gvars_datum_init_order, "sand_recipes");
    }
}

// The coin's value is a value of its materials.
// Yes, the gold standard makes a come-back!
// This is the only way to make coins that are possible to produce on station actually worth anything.
// tg: code/modules/mining/ores_coins.dm:520 (gaps P4a: the coins, money with the economy)
public partial class obj_item_coin : obj_item
{
    // tg: code/modules/mining/ores_coins.dm:541
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        coinflip = pick(sideslist);
        icon_state = Dm.text("coin_[]", coinflip);
        pixel_x = base_pixel_x + rand(0, 16) - 8;
        pixel_y = base_pixel_y + rand(0, 8) - 8;
        add_traits(Dm.list(TRAIT_FISHING_BAIT, TRAIT_BAIT_ALLOW_FISHING_DUD), INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/mining/ores_coins.dm:549
    public override async DmTask finalize_material_effects(DmList materials = null)
    {
        await base.finalize_material_effects(materials);
        if (Dm.truthy(override_material_worth))
            return;
        value = 0;
        foreach (datum_material material in Dm.each_cast<datum_material>(materials))
            value += Dm.num(material.value_per_unit) * Dm.num(((DmList)materials[material])[MATERIAL_LIST_OPTIMAL_AMOUNT]);
    }

    // tg: code/modules/mining/ores_coins.dm:557
    public override object get_item_credit_value()
    {
        return value;
    }

    // tg: code/modules/mining/ores_coins.dm:560
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] contemplates suicide with \\the []!", user, this)));
        if (!Dm.truthy(await attack_self(user)))
        {
            user.visible_message(span_suicide(I18n.T("[] couldn't flip \\the []!", user, this)));
            return SHAME;
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(manual_suicide), user), 1 * SECONDS, file: "code/modules/mining/ores_coins.dm", line: 565); //10 = time takes for flip animation
        return MANUAL_SUICIDE_NONLETHAL;
    }

    // tg: code/modules/mining/ores_coins.dm:568
    public virtual async DmTask manual_suicide(mob_living user = null)
    {
        float index = sideslist.Find(coinflip);
        if (index == 2) //tails
        {
            user.visible_message(span_suicide(I18n.T("\\the [] lands on []! [] promptly falls over, dead!", this, coinflip, user)));
            await user.adjust_oxy_loss(200);
            await user.death(FALSE);
            user.set_suicide(TRUE);
            user.suicide_log();
        }
        else
            user.visible_message(span_suicide(I18n.T("\\the [] lands on []! [] keeps on living!", this, coinflip, user))); //Don't put it in your pocket. It's your lucky quarter.
    }

    // tg: code/modules/mining/ores_coins.dm:579
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        // MONEY_NAME_AUTOPURAL(value) (__DEFINES/economy.dm:99), inline (rules 11.1)
        Dm.append(ref dot, span_info(I18n.T("It's worth [] [].", value, Dm.text("credit[]", Dm.eq(value, 1) ? "" : "s"))));
        return dot;
    }

    // tg: code/modules/mining/ores_coins.dm:583
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_stack_cable_coil @string)
            return NONE;

        if (Dm.truthy(string_attached))
        {
            to_chat(user, span_warning(I18n.T("There already is a string attached to this coin!")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await @string.use(1)))
        {
            to_chat(user, span_warning(I18n.T("You need one length of cable to attach a string to the coin!")));
            return ITEM_INTERACT_BLOCKING;
        }

        add_overlay("coin_string_overlay");
        string_attached = TRUE;
        to_chat(user, span_notice(I18n.T("You attach a string to the coin.")));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mining/ores_coins.dm:601
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        await base.wirecutter_act(user, tool);
        if (!Dm.truthy(string_attached))
            return TRUE;

        Dm.@new<obj_item_stack_cable_coil>(drop_location(), 1);
        overlays = Dm.list();
        string_attached = null;
        to_chat(user, span_notice(I18n.T("You detach the string from the coin.")));
        return TRUE;
    }

    // tg: code/modules/mining/ores_coins.dm:612
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (cooldown < world.time)
        {
            if (Dm.truthy(string_attached)) //does the coin have a wire attached
            {
                to_chat(user, span_warning(I18n.T("The coin won't flip very well with something attached!")));
                return FALSE; //do not flip the coin
            }
            cooldown = world.time + 15;
            flick(Dm.text("coin_[]_flip", coinflip), this);
            coinflip = pick(sideslist);
            icon_state = Dm.text("coin_[]", coinflip);
            playsound(user.loc as atom, "sound/items/coinflip.ogg", 50, TRUE);
            object oldloc = loc;
            await sleep(1.5f * SECONDS);
            if (loc == oldloc && user != null && !Dm.truthy(user.incapacitated))
            {
                user.visible_message(span_notice(I18n.T("[] flips []. It lands on [].", user, this, coinflip)),
                    span_notice(I18n.T("You flip []. It lands on [].", this, coinflip)),
                    span_hear(I18n.T("You hear the clattering of loose change.")));
            }
            if (Dm.truthy(has_action))
            {
                if (Dm.eq(coinflip, heads_name))
                    await heads_action(user);
                else
                    await tails_action(user);
            }
        }
        return TRUE; //did the coin flip? useful for suicide_act
    }

    // tg: code/modules/mining/ores_coins.dm:635
    public virtual async DmTask heads_action(mob user = null)
    {
        return;
    }

    // tg: code/modules/mining/ores_coins.dm:638
    public virtual async DmTask tails_action(mob user = null)
    {
        return;
    }
}

// tg: code/modules/mining/ores_coins.dm:656
public partial class obj_item_coin_titanium : obj_item_coin
{
    // tg: code/modules/mining/ores_coins.dm:659
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_flash_powder), 8));
    }
}

// tg: code/modules/mining/ores_coins.dm:668
public partial class obj_item_coin_mythril : obj_item_coin
{
    // tg: code/modules/mining/ores_coins.dm:671
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_medicine_omnizine_godblood), 8));
    }
}

// tg: code/modules/mining/ores_coins.dm:677
public partial class obj_item_coin_runite : obj_item_coin
{
    // tg: code/modules/mining/ores_coins.dm:680
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_iron), 4), Dm.kv(typeof(datum_reagent_consumable_ethanol_ritual_wine), 4));
    }
}

// tg: code/modules/mining/ores_coins.dm:687
public partial class obj_item_coin_antagtoken : obj_item_coin
{
    // tg: code/modules/mining/ores_coins.dm:697
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_ants), 4), Dm.kv(typeof(datum_reagent_consumable_eggyolk), 4));
    }
}

// tg: code/modules/mining/ores_coins.dm:703
public partial class obj_item_coin_gold_debug : obj_item_coin_gold
{
    // tg: code/modules/mining/ores_coins.dm:707
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_gold_cursed), 8));
    }

    // tg: code/modules/mining/ores_coins.dm:710
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (cooldown < world.time)
        {
            if (Dm.truthy(string_attached)) //does the coin have a wire attached
            {
                to_chat(user, span_warning(I18n.T("The coin won't flip very well with something attached!")));
                return FALSE; //do not flip the coin
            }
            cooldown = world.time + 15;
            flick(Dm.text("coin_[]_flip", coinflip), this);
            coinflip = pick(sideslist);
            icon_state = Dm.text("coin_[]", coinflip);
            playsound(user.loc as atom, "sound/items/coinflip.ogg", 50, TRUE);
            object oldloc = loc;
            await sleep(1.5f * SECONDS);
            if (loc == oldloc && user != null && !Dm.truthy(user.incapacitated))
            {
                user.visible_message(span_notice(I18n.T("[] flips []. It lands on [].", user, this, coinflip)),
                    span_notice(I18n.T("You flip []. It lands on [].", this, coinflip)),
                    span_hear(I18n.T("You hear the clattering of loose change.")));
            }
            await SSeconomy.fire();
            to_chat(user, Dm.text("<span class='bounty'>[] is the inflation value.</span>", SSeconomy.inflation_value__proc()));
        }
        return TRUE; //did the coin flip? useful for suicide_act
    }
}

// tg: code/modules/mining/ores_coins.dm:741
public partial class obj_item_coin_eldritch : obj_item_coin
{
    // tg: code/modules/mining/ores_coins.dm:753
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_carbon), 5), Dm.kv(typeof(datum_reagent_toxin_plasma), 5), Dm.kv(typeof(datum_reagent_eldritch), 4));
    }

    // tg: code/modules/mining/ores_coins.dm:756
    public override async DmTask heads_action(mob user = null)
    {
        var living_user = user as mob_living;
        // IS_HERETIC(user) (__DEFINES/antagonists.dm:293), expanded inline (rules 11.1)
        if (!(Dm.truthy(user.mind?.has_antag_datum(typeof(datum_antagonist_heretic))) || HAS_TRAIT(user, TRAIT_ACT_AS_HERETIC)))
        {
            await living_user.adjust_brute_loss(5);
            return;
        }
        foreach (obj_machinery_door_airlock target_airlock in Dm.each<obj_machinery_door_airlock>(range(airlock_range, user)))
        {
            if (Dm.truthy(target_airlock.density))
            {
                await target_airlock.open();
                continue;
            }
            await target_airlock.close(null, TRUE); // force_crush = TRUE
        }
    }

    // tg: code/modules/mining/ores_coins.dm:767
    public override async DmTask tails_action(mob user = null)
    {
        var living_user = user as mob_living;
        // IS_HERETIC(user) (__DEFINES/antagonists.dm:293), expanded inline (rules 11.1)
        if (!(Dm.truthy(user.mind?.has_antag_datum(typeof(datum_antagonist_heretic))) || HAS_TRAIT(user, TRAIT_ACT_AS_HERETIC)))
        {
            await living_user.adjust_fire_loss(5);
            return;
        }
        foreach (obj_machinery_door_airlock target_airlock in Dm.each<obj_machinery_door_airlock>(range(airlock_range, user)))
        {
            if (Dm.truthy(target_airlock.locked))
            {
                target_airlock.unlock();
                continue;
            }
            target_airlock.@lock();
        }
    }
}
