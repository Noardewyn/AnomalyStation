// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/equipment/monster_organs/brimdust_sac.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * Gives you three stacks of Brimdust Coating, when you get hit by anything it will make a short ranged explosion.
 * If this happens on the station it does much less damage, and slows down the bearer.
 * If implanted, you can shake off a cloud of brimdust to give this buff to people around you.area
 * It will also automatically grant you one stack every 30 seconds if you are on lavaland.
 */
// tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:16
public partial class obj_item_organ_monster_core_brimdust_sac : obj_item_organ_monster_core
{
    /// Interval between passively gaining stacks on lavaland if organ is implanted
    // tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:2  #define BRIMDUST_LIFE_APPLY_COOLDOWN (30 SECONDS) (file-local)
    internal const float BRIMDUST_LIFE_APPLY_COOLDOWN = 30 * SECONDS;
    /// Number of stacks to add over time
    // tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:4  #define BRIMDUST_STACKS_ON_LIFE 1
    internal const float BRIMDUST_STACKS_ON_LIFE = 1;
    /// Number of stacks to add if you activate the item in hand
    // tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:6  #define BRIMDUST_STACKS_ON_USE 3
    internal const float BRIMDUST_STACKS_ON_USE = 3;
    /// Overlay alpha per effect stack
    // tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:8  #define BRIMDUST_ALPHA_PER_STACK 75
    internal const float BRIMDUST_ALPHA_PER_STACK = 75;

    // tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:29
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_explodable), Dm.named("light_impact_range", 1)));
        return dot;
    }

    // tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:33
    public override void apply_to(mob_living target = null, mob user = null)
    {
        Waiting.NoSleep(target.apply_status_effect(user_status, BRIMDUST_STACKS_ON_USE));
        qdel(this);
    }

    // Every x seconds, if on lavaland, add one stack
    // tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:38
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);
        if (!(brimdust_auto_apply_cooldown <= world.time)) // COOLDOWN_FINISHED(src, brimdust_auto_apply_cooldown)
            return;
        if (!Dm.truthy(lavaland_equipment_pressure_check(get_turf(owner))))
            return;
        brimdust_auto_apply_cooldown = world.time + BRIMDUST_LIFE_APPLY_COOLDOWN; // COOLDOWN_START
        await owner.apply_status_effect(user_status, BRIMDUST_STACKS_ON_LIFE);
    }

    /// Make a cloud which applies brimdust to everyone nearby
    // tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:48
    public override void on_triggered_internal()
    {
        turf origin_turf = get_turf(owner);
        Waiting.NoSleep(do_smoke(2, owner, origin_turf, effect_type: typeof(obj_effect_particle_effect_fluid_smoke_bad_brimdust)));
    }
}

/// Smoke which applies brimdust to you, and is also bad for your lungs
// tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:53
public partial class obj_effect_particle_effect_fluid_smoke_bad_brimdust : obj_effect_particle_effect_fluid_smoke_bad
{
    // tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:57
    public override async DmTask<object> smoke_mob(mob_living_carbon smoker = null, object seconds_per_tick = null)
    {
        if (smoker == null)
            return FALSE;
        if (Dm.num(lifetime) < 1)
            return FALSE;
        if (Dm.truthy(smoker.smoke_delay))
            return FALSE;
        await smoker.apply_status_effect(typeof(datum_status_effect_stacking_brimdust_coating), obj_item_organ_monster_core_brimdust_sac.BRIMDUST_STACKS_ON_LIFE);
        return await base.smoke_mob(smoker, seconds_per_tick);
    }
}

/**
 * If you take brute damage with this buff, hurt and push everyone next to you.
 * If you catch fire and or on the space station, detonate all remaining stacks in a way which hurts you.
 * Washes off if you get wet.
 */
// tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:72
public partial class datum_status_effect_stacking_brimdust_coating : datum_status_effect_stacking
{
    // tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:129
    public override async DmTask refresh(object effect = null, params object[] __extra)
    {
        object stacks_to_add = Dm.arg(__extra, 0);
        await base.refresh(effect, __extra);
        add_stacks(stacks_to_add);
    }

    // tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:133
    public override object add_stacks(object stacks_added = null)
    {
        object dot = base.add_stacks(stacks_added);
        if (stacks == 0)
            return dot;
        linked_alert.overlay_state = Dm.text("brimdemon_[]", stacks);
        linked_alert.update_appearance(UPDATE_OVERLAYS);
        if (dust_overlay != null)
        {
            owner.cut_overlay(dust_overlay);
            dust_overlay.alpha = stacks * obj_item_organ_monster_core_brimdust_sac.BRIMDUST_ALPHA_PER_STACK;
            owner.add_overlay(dust_overlay);
        }
        return dot;
    }

    // tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:144
    [DmParams("new_owner", "stacks_to_apply")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object dot = await base.on_creation(new_owner, __extra);
        if (linked_alert != null)
            linked_alert.overlay_state = Dm.text("brimdemon_[]", stacks);
        return dot;
    }

    // tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:148
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        float target_width = max(Dm.num(owner.get_cached_width()), ICON_SIZE_X);
        float target_height = max(Dm.num(owner.get_cached_height()), ICON_SIZE_Y);
        if (target_width == ICON_SIZE_X && target_height == ICON_SIZE_Y)
        {
            dust_overlay = mutable_appearance(Dm.resource("icons/effects/weather_effects.dmi"), "ash_storm") as mutable_appearance;
        }
        else
        {
            DmList dust_icons = TgState.Current.datum_status_effect_stacking_brimdust_coating__on_apply__dust_icons ??= Dm.list();
            icon dust_icon = dust_icons[Dm.text("[]-[]", target_width, target_height)] as icon;
            if (dust_icon == null)
            {
                dust_icon = icon(Dm.resource("icons/effects/weather_effects.dmi"), "ash_storm");
                icon base_icon = icon(Dm.resource("icons/effects/weather_effects.dmi"), "ash_storm");
                dust_icon.Crop(1, 1, target_width, target_height);
                icon column = icon(Dm.resource("icons/effects/weather_effects.dmi"), "ash_storm");
                column.Crop(1, 1, ICON_SIZE_X, target_height);
                for (int i = 1; i <= ceil(target_height / ICON_SIZE_Y); i++)
                    column.Blend(base_icon, ICON_OVERLAY, 1, 1 + (i - 1) * ICON_SIZE_Y);
                for (int i = 1; i <= ceil(target_width / ICON_SIZE_X); i++)
                    dust_icon.Blend(column, ICON_OVERLAY, 1 + (i - 1) * ICON_SIZE_X, 1);
                dust_icons[Dm.text("[]-[]", target_width, target_height)] = dust_icon;
            }
            dust_overlay = mutable_appearance(dust_icon) as mutable_appearance;
        }
        dust_overlay.alpha = stacks * obj_item_organ_monster_core_brimdust_sac.BRIMDUST_ALPHA_PER_STACK;
        dust_overlay.color = COLOR_RED_LIGHT;
        dust_overlay.blend_mode = BLEND_INSET_OVERLAY;
        if (ishuman(owner))
        {
            mob_living_carbon_human as_human = owner as mob_living_carbon_human;
            if (Dm.truthy(as_human.cached_body_min_x_offset) && target_width > ICON_SIZE_X)
                dust_overlay.pixel_w = as_human.cached_body_min_x_offset;
            if (Dm.truthy(as_human.cached_body_min_y_offset) && target_height > ICON_SIZE_Y)
                dust_overlay.pixel_z = as_human.cached_body_min_y_offset;
        }
        ADD_KEEP_TOGETHER(owner, REF(this));
        owner.add_overlay(dust_overlay);
        obj_effect holder = owner.add_shared_particles(typeof(particles_brimdust), Dm.text("brimdust_coating-[]", owner.base_pixel_w)) as obj_effect;
        holder.pixel_w = -owner.base_pixel_w;
        RegisterSignal(owner, COMSIG_COMPONENT_CLEAN_ACT, nameof(on_cleaned));
        RegisterSignal(owner, COMSIG_MOB_APPLY_DAMAGE, nameof(on_take_damage));
        return dot;
    }

    // tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:185
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        owner.cut_overlay(dust_overlay);
        REMOVE_KEEP_TOGETHER(owner, REF(this));
        owner.remove_shared_particles(Dm.text("brimdust_coating-[]", owner.base_pixel_w));
        UnregisterSignal(owner, Dm.list(COMSIG_MOB_APPLY_DAMAGE, COMSIG_COMPONENT_CLEAN_ACT));
        return dot;
    }

    /// When you are cleaned, wash off the buff
    // tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:193
    [SignalHandler]
    public virtual object on_cleaned()
    {
        object dot = NONE;
        if (Dm.truthy(owner.remove_status_effect(typeof(datum_status_effect_stacking_brimdust_coating))))
            dot = Dm.bitor(dot, COMPONENT_CLEANED);
        return dot;
    }

    /// When you take brute damage, schedule an explosion
    // tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:201
    [SignalHandler]
    public virtual void on_take_damage(datum source = null, object damage = null, object damagetype = null, params object[] __extra)
    {
        if (!Dm.eq(damagetype, BRUTE))
            return;
        if (!(explosion_cooldown <= world.time)) // COOLDOWN_FINISHED(src, explosion_cooldown)
            return;
        owner.visible_message(span_boldwarning(I18n.T("The brimstone dust surrounding [] ignites!", owner)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(explode)), 0.25f * SECONDS, file: "code/modules/mining/equipment/monster_organs/brimdust_sac.dm", line: 208);
        explosion_cooldown = world.time + delay_between_explosions; // COOLDOWN_START(src, explosion_cooldown, delay_between_explosions)
    }

    /**
     * Hurts everything in a circle around you. Hurts less if in a pressurised environment.
     */
    // tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:214
    public virtual void explode()
    {
        turf origin_turf = get_turf(owner);
        playsound(origin_turf, Dm.resource("sound/effects/pop_expl.ogg"), 50);
        Dm.@new<obj_effect_temp_visual_explosion_fast>(origin_turf);

        float damage_dealt = blast_damage;
        object safe_explosion = lavaland_equipment_pressure_check(origin_turf);
        if (!Dm.truthy(safe_explosion))
        {
            damage_dealt *= pressure_modifier;
            Waiting.NoSleep(owner.apply_status_effect(typeof(datum_status_effect_brimdust_concussion)));
        }

        DmList targets = Dm.sub(range(1, origin_turf), Dm.truthy(safe_explosion) ? Dm.list(owner, owner.buckled) : null) as DmList;
        foreach (mob_living target in Dm.each<mob_living>(targets))
        {
            if (Dm.truthy(safe_explosion) && Dm.truthy(owner.faction_check_atom(target)))
                continue;
            object armor = target.run_armor_check(attack_flag: BOMB);
            Waiting.NoSleep(target.apply_damage(damage_dealt, damagetype: BURN, blocked: Dm.num(armor), spread_damage: TRUE));
        }

        if (Dm.truthy(owner._listen_lookup?[COMSIG_BRIMDUST_EXPLOSION]))
            owner._SendSignal(COMSIG_BRIMDUST_EXPLOSION, Dm.list(owner));
        add_stacks(-1);
    }
}

// tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:91
public partial class atom_movable_screen_alert_status_effect_brimdust_coating : atom_movable_screen_alert_status_effect
{
    // tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:99
    public override async DmTask MouseEntered(object location = null, object control = null, object @params = null)
    {
        desc = Dm.initial(type, (atom_movable_screen_alert_status_effect_brimdust_coating a) => a.desc);
        datum_status_effect_stacking_brimdust_coating dust = attached_effect as datum_status_effect_stacking_brimdust_coating;
        string dust_amount_string = null;
        switch (dust.stacks)
        {
            case 3:
                dust_amount_string = "are heavily caked in";
                break;
            case 2:
                dust_amount_string = "have a generous coating of";
                break;
            case 1:
                dust_amount_string = "are lightly sprinkled with";
                break;
        }

        desc = replacetext(desc, "%STACKS%", I18n.T(dust_amount_string)) as string;
        await base.MouseEntered(location, control, @params);
    }

    // tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:114
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = await base.Click(location, control, @params);
        if (!Dm.truthy(dot))
            return dot;
        mob_living living_owner = owner as mob_living;
        if (living_owner == null || !Dm.truthy(living_owner.can_resist()))
            return dot;
        owner.balloon_alert(owner, I18n.T("shaking off the dust..."));
        datum_status_effect_stacking_brimdust_coating dust = attached_effect as datum_status_effect_stacking_brimdust_coating;
        if (!Dm.truthy(await do_after(owner, dust.stacks * 1.5f * SECONDS, owner)))
            return dot;
        dust.explode();
        // The effect can get deleted from exploding and reducing stacks
        if (!QDELETED(dust))
            qdel(dust);
        return dot;
    }
}

/// Slowdown applied when you are detonated on the space station
// tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:235
public partial class datum_status_effect_brimdust_concussion : datum_status_effect
{
    // tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:240
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        await owner.add_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_brimdust_concussion));
        to_chat(owner, span_warning(I18n.T("You are knocked off balance by the explosion!")));
        return dot;
    }

    // tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:245
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_status_effect_brimdust_concussion));
        to_chat(owner, span_notice(I18n.T("You find your balance.")));
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mining/equipment/monster_organs/brimdust_sac.dm:155 (var/static/list/dust_icons)
    internal DmList datum_status_effect_stacking_brimdust_coating__on_apply__dust_icons;
}
