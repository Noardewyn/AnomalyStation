// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/lighter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J3, D298). The light middleman's flicker is the lighting module's (D24, as the welder's and the flare's). The var blocks are generated.

// tg: code/game/objects/items/lighter.dm:2
public partial class obj_item_lighter : obj_item
{
    // tg: code/game/objects/items/lighter.dm:44
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (light_system == OVERLAY_LIGHT || light_system == OVERLAY_LIGHT_DIRECTIONAL || light_system == OVERLAY_LIGHT_BEAM) // IS_OVERLAY_LIGHT_SYSTEM (__DEFINES/lighting.dm), inline
        {
            // middleman = new(src, "lighter"); RegisterSignal(middleman, COMSIG_LIGHT_MIDDLEMAN_UPDATED, PROC_REF(light_updated)); middleman.being_overriding_light():
            // the flicker, the lighting module (D24, rules 13: the client computes light from the replicated light_on, range, power and colour).
            Dm.Deferred("code/game/objects/items/lighter.dm:47", "Initialize: the light middleman's flicker (the lighting module, D24)");
        }
        create_reagents(maximum_fuel, Dm.bitor(REFILLABLE, DRAINABLE));
        if (Dm.truthy(spawns_with_reagent))
            Waiting.NoSleep(((datum_reagents)reagents).add_reagent(typeof(datum_reagent_fuel), maximum_fuel));
        if (!Dm.truthy(overlay_state))
            overlay_state = pick(overlay_list);
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_bullet_intercepting),
            Dm.named("block_chance", 0.5f),
            Dm.named("active_slots", ITEM_SLOT_SUITSTORE),
            Dm.named("on_intercepted", Dm.@new<datum_callback>(this, nameof(on_intercepted_bullet)))));
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/lighter.dm:63
    public override object Destroy(float? force = null)
    {
        if (middleman != null)
            QDEL_NULL(ref middleman);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/lighter.dm:68
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_iron), 1), Dm.kv(typeof(datum_reagent_fuel), 5), Dm.kv(typeof(datum_reagent_fuel_oil), 5));
    }

    // tg: code/game/objects/items/lighter.dm:71
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.num(get_fuel()) <= 0)
            dot.Add(span_warning(I18n.T("It is out of lighter fluid! Refill it with welder fuel.")));
        else
            dot.Add(span_notice(I18n.T("It contains [] units of fuel out of [].", get_fuel(), maximum_fuel)));
        return dot;
    }

    // tg: code/game/objects/items/lighter.dm:78
    [SignalHandler]
    public virtual void light_updated(datum source = null)
    {
        // fire_flicker_middleman(middleman): the flicker, the lighting module (D24); no middleman is made (Initialize).
        Dm.Deferred("code/game/objects/items/lighter.dm:80", "light_updated: fire_flicker_middleman (the lighting module, D24)");
    }

    /// Destroy the lighter when it's shot by a bullet
    // tg: code/game/objects/items/lighter.dm:83
    public virtual async DmTask on_intercepted_bullet(mob_living victim = null, obj_projectile bullet = null)
    {
        victim.visible_message(span_warning(I18n.T("\\The [] shatters on []'s lighter!", bullet, victim)));
        playsound(victim, SFX_RICOCHET, 100, TRUE);
        Dm.@new<obj_effect_decal_cleanable_blood_oil>(get_turf(this));
        do_sparks(1, TRUE, this);
        await victim.dropItemToGround(this, force: TRUE, silent: TRUE);
        qdel(this);
    }

    // tg: code/game/objects/items/lighter.dm:91
    public override async DmTask cyborg_unequip(mob user = null)
    {
        if (!Dm.truthy(lit))
            return;
        set_lit(FALSE);
    }

    // tg: code/game/objects/items/lighter.dm:96
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (Dm.truthy(lit))
        {
            user.visible_message(span_suicide(I18n.T("[] begins holding \\the []'s flame up to [] face! It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));
            playsound(this, Dm.resource("sound/items/tools/welder.ogg"), 50, TRUE);
            return FIRELOSS;
        }
        user.visible_message(span_suicide(I18n.T("[] begins whacking []self with \\the []! It looks like [] trying to commit suicide!", user, user.p_them(), this, user.p_theyre())));
        return BRUTELOSS;
    }

    // tg: code/game/objects/items/lighter.dm:105
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]", Dm.initial(this, (obj_item_lighter p) => p.icon_state)) + (Dm.truthy(lit) ? "-on" : "");
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/lighter.dm:109
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        dot.Add(create_lighter_overlay());
        return dot;
    }

    /// Generates an overlay used by this lighter.
    // tg: code/game/objects/items/lighter.dm:114
    public virtual object create_lighter_overlay()
    {
        return mutable_appearance(icon, Dm.text("lighter_overlay_[]", overlay_state) + (Dm.truthy(lit) ? "-on" : ""));
    }

    // tg: code/game/objects/items/lighter.dm:117
    public override async DmTask<object> ignition_effect(atom A = null, mob user = null)
    {
        if (Dm.truthy(get_temperature()))
            return span_infoplain(span_rose(I18n.T("With a single flick of [] wrist, [] smoothly lights [] with []. Damn [] cool.", user.p_their(), user, A, this, user.p_theyre())));
        return null;
    }

    // tg: code/game/objects/items/lighter.dm:121
    public virtual void set_lit(object new_lit = null)
    {
        if (Dm.eq(lit, new_lit))
            return;

        lit = Dm.num(new_lit);
        if (Dm.truthy(lit))
        {
            force = 5;
            damtype = BURN;
            hitsound = Dm.resource("sound/items/tools/welder.ogg");
            attack_verb_continuous = string_list(Dm.list("burns", "singes")) as DmList;
            attack_verb_simple = string_list(Dm.list("burn", "singe")) as DmList;
            heat = heat_while_on;
            START_PROCESSING(SSobj, this);
            if (Dm.truthy(fancy))
                playsound(this.loc, Dm.resource("sound/items/lighter/zippo_on.ogg"), 100, 1);
            else
                playsound(this.loc, Dm.resource("sound/items/lighter/lighter_on.ogg"), 100, 1);
            if (isliving(loc))
            {
                mob_living male_model = (mob_living)loc;
                if (Dm.truthy(male_model.fire_stacks) && !Dm.truthy(male_model.on_fire))
                    male_model.ignite_mob();
            }
        }
        else
        {
            hitsound = SFX_SWING_HIT;
            force = 0;
            heat = 0;
            attack_verb_continuous = null; //human_defense.dm takes care of it
            attack_verb_simple = null;
            STOP_PROCESSING(SSobj, this);
            if (Dm.truthy(fancy))
                playsound(this.loc, Dm.resource("sound/items/lighter/zippo_off.ogg"), 100, 1);
            else
                playsound(this.loc, Dm.resource("sound/items/lighter/lighter_off.ogg"), 100, 1);
        }
        Waiting.NoSleep(set_light_on(lit));
        update_appearance();
    }

    // tg: code/game/objects/items/lighter.dm:156
    public override async DmTask<object> extinguish()
    {
        object dot = await base.extinguish();
        set_lit(FALSE);
        return dot;
    }

    // tg: code/game/objects/items/lighter.dm:160
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(user.is_holding(this)))
            return await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(lit))
        {
            set_lit(FALSE);
            if (Dm.truthy(fancy))
            {
                user.visible_message(
                    span_notice(I18n.T("You hear a quiet click, as [] shuts off [] without even looking at what [] doing. Wow.", user, this, user.p_theyre())),
                    span_notice(I18n.T("You quietly shut off [] without even looking at what you're doing. Wow.", this)));
            }
            else
            {
                user.visible_message(
                    span_notice(I18n.T("[] quietly shuts off [].", user, this)),
                    span_notice(I18n.T("You quietly shut off [].", this)));
            }
            return null;
        }

        if (Dm.num(get_fuel()) <= 0)
            return null;

        set_lit(TRUE);

        if (Dm.truthy(fancy))
        {
            user.visible_message(
                span_notice(I18n.T("Without even breaking stride, [] flips open and lights [] in one smooth movement.", user, this)),
                span_notice(I18n.T("Without even breaking stride, you flip open and light [] in one smooth movement.", this)));
            return null;
        }

        bool hand_protected = false;
        mob_living_carbon_human human_user = user as mob_living_carbon_human;
        if (human_user == null || HAS_TRAIT(human_user, TRAIT_RESISTHEAT) || HAS_TRAIT(human_user, TRAIT_RESISTHEATHANDS))
            hand_protected = true;
        else if (human_user.gloves is not obj_item_clothing_gloves)
            hand_protected = false;
        else
        {
            obj_item_clothing_gloves gloves = (obj_item_clothing_gloves)human_user.gloves;
            if (Dm.truthy(gloves.max_heat_protection_temperature))
                hand_protected = Dm.num(gloves.max_heat_protection_temperature) > 360;
        }

        if (hand_protected || prob(75))
        {
            user.visible_message(
                span_notice(I18n.T("After a few attempts, [] manages to light [].", user, this)),
                span_notice(I18n.T("After a few attempts, you manage to light [].", this)));
            return null;
        }

        object hitzone = Dm.eq(user.held_index_to_dir(user.active_hand_index), "r") ? BODY_ZONE_PRECISE_R_HAND : BODY_ZONE_PRECISE_L_HAND;
        await ((mob_living)user).apply_damage(5, BURN, hitzone);
        user.visible_message(
            span_warning(I18n.T("After a few attempts, [] manages to light [] - however, [] burn[] [] finger in the process.", user, this, user.p_they(), user.p_s(), user.p_their())),
            span_warning(I18n.T("You burn yourself while lighting the lighter!")));
        await ((mob_living)user).add_mood_event("burnt_thumb", typeof(datum_mood_event_burnt_thumb));
        return null;
    }

    // tg: code/game/objects/items/lighter.dm:215
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(lit))
        {
            await use(0.5f);
            if (Dm.truthy(target_mob.ignite_mob()))
            {
                message_admins(Dm.text("[] set [] on fire with [] at []", ADMIN_LOOKUPFLW(user), key_name_admin(target_mob), this, AREACOORD(user)));
                log_game(Dm.text("[] set [] on fire with [] at []", key_name(user), key_name(target_mob), this, AREACOORD(user)));
            }
        }
        obj_item_cigarette cig = help_light_cig(target_mob) as obj_item_cigarette;
        if (!Dm.truthy(lit) || cig == null || Dm.truthy(user.combat_mode))
            return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);

        if (Dm.truthy(cig.lit))
            to_chat(user, span_warning(I18n.T("\\The [] is already lit!", cig)));
        if (target_mob == user)
        {
            await cig.attempt_light(user, this);
            return null;
        }

        if (Dm.truthy(fancy))
            await cig.attempt_light(user, this, span_rose(I18n.T("[] whips \\the [] out and holds it for []. [] arm is as steady as the unflickering flame [] light[] \\the [] with.", user, this, target_mob, user.p_Their(), user.p_they(), user.p_s(), cig)));
        else
            await cig.attempt_light(user, this, span_notice(I18n.T("[] holds \\the [] out for [], and lights [] [].", user, this, target_mob, target_mob.p_their(), cig.name)));
        return null;
    }

    ///Checks if the lighter is able to perform a welding task.
    // tg: code/game/objects/items/lighter.dm:237
    public override object tool_use_check(mob_living user = null, object amount = null, object heat_required = null)
    {
        if (!Dm.truthy(lit))
        {
            to_chat(user, span_warning(I18n.T("[] has to be on to complete this task!", this)));
            return FALSE;
        }
        if (Dm.lt(get_fuel(), amount))
        {
            to_chat(user, span_warning(I18n.T("You need more welding fuel to complete this task!")));
            return FALSE;
        }
        if (heat < Dm.num(heat_required))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/objects/items/lighter.dm:248
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(lit))
        {
            open_flame(heat);
            burned_fuel_for += Dm.num(seconds_per_tick);
            if (burned_fuel_for >= TOOL_FUEL_BURN_INTERVAL)
                Waiting.NoSleep(use(used: 0.25f));
        }
        return null;
    }

    // tg: code/game/objects/items/lighter.dm:255
    public override object get_temperature()
    {
        return lit * heat;
    }

    /// Uses fuel from the lighter.
    // tg: code/game/objects/items/lighter.dm:259
    public override async DmTask<object> use(object used = null, params object[] __extra)
    {
        used ??= 0;
        if (!Dm.truthy(lit))
            return FALSE;

        datum_reagents holder = (datum_reagents)reagents;
        if (Dm.truthy(Dm.bitand(await holder.spark_act(0, SPARK_ACT_ENCLOSED, banned_reagents: typeof(datum_reagent_fuel)), SPARK_ACT_DESTRUCTIVE)))
        {
            qdel(this);
            return FALSE;
        }

        if (Dm.num(used) > 0)
            burned_fuel_for = 0;

        if (Dm.num(get_fuel()) >= Dm.num(used))
        {
            await holder.remove_reagent(typeof(datum_reagent_fuel), used);
            if (Dm.num(get_fuel()) <= 0)
                set_lit(FALSE);
            return TRUE;
        }
        return FALSE;
    }

    ///Returns the amount of fuel
    // tg: code/game/objects/items/lighter.dm:278
    public virtual object get_fuel()
    {
        datum_reagents holder = (datum_reagents)reagents;
        return Dm.num(holder.get_reagent_amount(typeof(datum_reagent_fuel))) + Dm.num(holder.get_reagent_amount(typeof(datum_reagent_toxin_plasma)));
    }
}

// tg: code/game/objects/items/lighter.dm:282
public partial class obj_item_lighter_greyscale : obj_item_lighter
{
    // tg: code/game/objects/items/lighter.dm:297  var/static/list/color_list
    private static DmList color_list() => Dm.list( //Same 16 color selection as electronic assemblies
        COLOR_ASSEMBLY_BLACK,
        COLOR_FLOORTILE_GRAY,
        COLOR_ASSEMBLY_BGRAY,
        COLOR_ASSEMBLY_WHITE,
        COLOR_ASSEMBLY_RED,
        COLOR_ASSEMBLY_ORANGE,
        COLOR_ASSEMBLY_BEIGE,
        COLOR_ASSEMBLY_BROWN,
        COLOR_ASSEMBLY_GOLD,
        COLOR_ASSEMBLY_YELLOW,
        COLOR_ASSEMBLY_GURKHA,
        COLOR_ASSEMBLY_LGREEN,
        COLOR_ASSEMBLY_GREEN,
        COLOR_ASSEMBLY_LBLUE,
        COLOR_ASSEMBLY_BLUE,
        COLOR_ASSEMBLY_PURPLE);

    // tg: code/game/objects/items/lighter.dm:316
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(lighter_color))
            lighter_color = pick(color_list());
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/lighter.dm:322
    public override object create_lighter_overlay()
    {
        mutable_appearance lighter_overlay = (mutable_appearance)base.create_lighter_overlay();
        lighter_overlay.color = lighter_color;
        return lighter_overlay;
    }

    // tg: code/game/objects/items/lighter.dm:327
    public override async DmTask<object> ignition_effect(atom A = null, mob user = null)
    {
        if (Dm.truthy(get_temperature()))
            return span_notice(I18n.T("After some fiddling, [] manages to light [] with [].", user, A, this));
        return null;
    }
}

// tg: code/game/objects/items/lighter.dm:333
public partial class obj_item_lighter_slime : obj_item_lighter
{
    // tg: code/game/objects/items/lighter.dm:340
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_iron), 1), Dm.kv(typeof(datum_reagent_fuel), 5), Dm.kv(typeof(datum_reagent_medicine_pyroxadone), 5));
    }
}

// tg: code/game/objects/items/lighter.dm:349
public partial class obj_item_lighter_mime : obj_item_lighter
{
    // tg: code/game/objects/items/lighter.dm:359
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_iron), 1), Dm.kv(typeof(datum_reagent_toxin_mutetoxin), 5), Dm.kv(typeof(datum_reagent_consumable_nothing), 10));
    }

    // tg: code/game/objects/items/lighter.dm:362
    public override async DmTask<object> ignition_effect(atom A = null, mob user = null)
    {
        return span_infoplain(I18n.T("[] lifts \\the [] to the [], which miraculously lights!", user, this, A));
    }
}

// tg: code/game/objects/items/lighter.dm:366
public partial class obj_item_lighter_bright : obj_item_lighter
{
    // tg: code/game/objects/items/lighter.dm:375
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_iron), 1), Dm.kv(typeof(datum_reagent_flash_powder), 10));
    }

    // tg: code/game/objects/items/lighter.dm:378
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        if (Dm.truthy(lit) && isliving(user))
        {
            mob_living current_viewer = (mob_living)user;
            await current_viewer.flash_act(4);
        }
        return dot;
    }

    // tg: code/game/objects/items/lighter.dm:385
    public override async DmTask<object> ignition_effect(atom A = null, mob user = null)
    {
        if (Dm.truthy(get_temperature()))
        {
            object dot = span_infoplain(span_rose(I18n.T("[] lifts the [] to the [], igniting it with a brilliant flash of light!", user, this, A)));
            mob_living current_viewer = (mob_living)user;
            await current_viewer.flash_act(4);
            return dot;
        }
        return null;
    }
}
