// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/ballistic/automatic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice D: the WT-550 autorifle (:66-91) and the NT BR-38 battle rifle (:342-470), the two automatic guns of the station set. Syndicate
// N2a (D316): the operatives' C-20r, smartgun, M-90gl and L6 SAW. The Saber SMG's full auto (the owner, 2026-11-12) is at the end of the file;
// the others keep generated var blocks (docs/ai/specs/not-translated.md section 49). The var blocks are generated
// (D111). The seclight and bayonet points are live-partial rows; the lore text is the examine lore element (D291). Syndicate G4c: the
// Thompson's automatic fire (:210, the monkey gun case's Typewriter).

// tg: code/modules/projectiles/guns/ballistic/automatic.dm:66
public partial class obj_item_gun_ballistic_automatic_wt550 : obj_item_gun_ballistic_automatic
{
    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:85
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_automatic_fire), 0.3f * SECONDS));
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:89
    public override async DmTask add_bayonet_point()
    {
        Dm.Deferred("code/modules/projectiles/guns/ballistic/automatic.dm:90", "add_bayonet_point: AddComponent(/datum/component/bayonet_attachable, ...), the bayonet mount (M10+)");
    }
}

// tg: code/modules/projectiles/guns/ballistic/automatic.dm:37
public partial class obj_item_gun_ballistic_automatic_c20r : obj_item_gun_ballistic_automatic
{
    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:51
    public override async DmTask add_bayonet_point()
    {
        Dm.Deferred("code/modules/projectiles/guns/ballistic/automatic.dm:52", "add_bayonet_point: AddComponent(/datum/component/bayonet_attachable, ...), the bayonet mount (M10+)");
    }

    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:54
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (chambered == null && Dm.truthy(empty_indicator)) //this is duplicated due to a layering issue with the select fire icon.
            Dm.append(ref dot, Dm.text("[]_empty", icon_state));
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:62
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        return dot;
    }
}

// tg: code/modules/projectiles/guns/ballistic/automatic.dm:92
public partial class obj_item_gun_ballistic_automatic_smartgun : obj_item_gun_ballistic_automatic
{
    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:116
    public override void fire_sounds()
    {
        object picked_fire_sound = pick(firing_sound_list);
        playsound(this, picked_fire_sound, fire_sound_volume, vary_fire_sound);
    }
}

// tg: code/modules/projectiles/guns/ballistic/automatic.dm:142
public partial class obj_item_gun_ballistic_automatic_m90 : obj_item_gun_ballistic_automatic
{
    /// Attached underbarrel grenade launcher
    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:160  GAP(types): typed /obj/item/gun/ballistic/revolver/grenadelauncher/underbarrel,
    // but the unrestricted carbine puts a plain unrestricted grenade launcher there (:176), so the var is the launcher's base type (hand-written,
    // with the type's constructor)
    public obj_item_gun_ballistic_revolver_grenadelauncher underbarrel;

    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:143  (the type's overrides, hand-written with the var above)
    public obj_item_gun_ballistic_automatic_m90()
    {
        name = Dm.text("\\improper M-90gl Carbine");
        desc = "A three-round burst .223 toploading carbine, designated 'M-90gl'. Has an attached underbarrel grenade launcher.";
        desc_controls = "Right-click to use grenade launcher.";
        icon_state = "m90";
        w_class = 4;
        inhand_icon_state = "m90";
        selector_switch_icon = 1;
        accepted_magazine_type = typeof(obj_item_ammo_box_magazine_m223);
        can_suppress = 0;
        burst_size = 3;
        burst_delay = 2;
        spread = 5;
        pin = typeof(obj_item_firing_pin_implant_pindicate);
        mag_display = 1;
        empty_indicator = 1;
        fire_sound = Dm.resource("sound/items/weapons/gun/smg/shot_alt.ogg");
    }

    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:162
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        underbarrel = Dm.@new<obj_item_gun_ballistic_revolver_grenadelauncher_underbarrel>(this);
        update_appearance();
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:167
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref underbarrel);
        return base.Destroy(force);
    }

    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:179
    public override async DmTask<object> try_fire_gun(atom target = null, mob_living user = null, object @params = null)
    {
        if (Dm.truthy(LAZYACCESS(params2list(@params), RIGHT_CLICK)))
            return await underbarrel.try_fire_gun(target, user, @params);
        return await base.try_fire_gun(target, user, @params);
    }

    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:184
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (isammocasing(tool))
        {
            if (istype(tool, (underbarrel.magazine as obj_item_ammo_box)?.ammo_type))
                await underbarrel.item_interaction(user, tool, modifiers);
            return ITEM_INTERACT_BLOCKING;
        }
        return await base.item_interaction(user, tool, modifiers, __extra);
    }
}

// tg: code/modules/projectiles/guns/ballistic/automatic.dm:171
public partial class obj_item_gun_ballistic_automatic_m90_unrestricted : obj_item_gun_ballistic_automatic_m90
{
    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:174
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        underbarrel = Dm.@new<obj_item_gun_ballistic_revolver_grenadelauncher_unrestricted>(this);
        update_appearance();
        return dot;
    }
}

// tg: code/modules/projectiles/guns/ballistic/automatic.dm:240
public partial class obj_item_gun_ballistic_automatic_l6_saw : obj_item_gun_ballistic_automatic
{
    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:268
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_automatic_fire), 0.2f * SECONDS));
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:273
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("<b>alt + click</b> to [] the dust cover.", Dm.truthy(cover_open) ? I18n.T("close") : I18n.T("open")));
        if (Dm.truthy(cover_open) && magazine != null)
            Dm.append(ref dot, span_notice(I18n.T("It seems like you could use an <b>empty hand</b> to remove the magazine.")));
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:280
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        cover_open = Dm.truthy(cover_open) ? FALSE : TRUE;
        balloon_alert(user, I18n.T("cover []", Dm.truthy(cover_open) ? I18n.T("opened") : I18n.T("closed")));
        playsound(this, "sound/items/weapons/gun/l6/l6_door.ogg", 60, TRUE);
        update_appearance();
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:287
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        inhand_icon_state = Dm.text("[][][]", base_icon_state, Dm.truthy(cover_open) ? "open" : "closed", magazine != null ? "mag" : "nomag");
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:291
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.append(ref dot, Dm.text("l6_door_[]", Dm.truthy(cover_open) ? "open" : "closed"));
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:296
    public override async DmTask<object> try_fire_gun(atom target = null, mob_living user = null, object @params = null)
    {
        if (Dm.truthy(cover_open))
        {
            balloon_alert(user, I18n.T("close the cover!"));
            return FALSE;
        }

        object dot = await base.try_fire_gun(target, user, @params);
        if (Dm.truthy(dot))
            update_appearance();
        return dot;
    }

    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:307
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (loc != user)
        {
            await base.attack_hand(user, modifiers, __extra);
            return null;
        }
        if (!Dm.truthy(cover_open))
        {
            balloon_alert(user, I18n.T("open the cover!"));
            return null;
        }
        await base.attack_hand(user, modifiers, __extra);
        return null;
    }

    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:316
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(cover_open) && istype(tool, accepted_magazine_type))
        {
            balloon_alert(user, I18n.T("open the cover!"));
            return ITEM_INTERACT_BLOCKING;
        }
        return await base.item_interaction(user, tool, modifiers, __extra);
    }
}

// tg: code/modules/projectiles/guns/ballistic/automatic.dm:342
public partial class obj_item_gun_ballistic_automatic_battle_rifle : obj_item_gun_ballistic_automatic
{
    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:391
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_scope), Dm.named("range_modifier", 2)));
        Waiting.NoSleep(AddElement(typeof(datum_element_examine_lore), Dm.named("lore", I18n.T("The BR-38 is Nanotrasen's latest foray into entirely in-house, standard-issue-ready, accelerator-assisted ballistic firearms.<br><br>The acceleration rail built into the barrel assembly boosts fired projectiles to higher velocities than unassisted bullets, allowing even less powerful cartridges, such as the venerable .38 Special, to have improved ballistic performance and stopping power. Even though the chambering makes this more of a pistol-caliber carbine than a battle rifle, countless arguments in both the marketing office and the corporate boardroom about the name meant that something had to give; in this case, the slightly misleading designation.<br><br>It's hard to cover up everything about its troubled development, though.<br><br>In a sour twist of irony for Nanotrasen's historical issues with ballistics-based security weapons, the BR-38 has one significant flaw. It is possible for the weapons system to suffer from unintended discombulations due to closed heat distribution systems, should the weapon be tampered with. Nanotrasen's weapons R&D teams are still working on this issue, while also trying to work out why the weapon's onboard computation systems suffer from so many calculation errors, before moving onto a full commercial rollout."))));
        register_context();
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:411
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);

        if (Dm.eq(held_item?.tool_behaviour, TOOL_MULTITOOL) && shots_before_degradation < max_shots_before_degradation)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Reset System";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:418
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (shots_before_degradation != 0)
            Dm.append(ref dot, span_notice(Dm.text("[] can fire [] more times before risking system degradation.", this, shots_before_degradation)));
        else
            Dm.append(ref dot, span_notice(Dm.text("[] is in the process of system degradation. It is currently at stage [] of []. Use a multitool on [] to recalibrate. Alternatively, insert it into a weapon recharger.", this, degradation_stage, degradation_stage_max, this)));
        Dm.append(ref dot, span_notice(Dm.text("You can [] to learn a little more about [].", EXAMINE_HINT("look closer"), this)));
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:428
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (shots_before_degradation == 0)
            inhand_icon_state = Dm.text("[]-empty", base_icon_state);
        else
            inhand_icon_state = Dm.text("[]", base_icon_state);
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:435
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (degradation_stage != 0)
            Dm.append(ref dot, Dm.text("[]_empty", base_icon_state));
        else if (shots_before_degradation != 0)
        {
            float ratio_for_overlay = CEILING(clamp(shots_before_degradation / max_shots_before_degradation, 0, 1) * 3, 1);
            Dm.append(ref dot, Dm.text("[]_stage_[]", icon_state, ratio_for_overlay));
        }
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:443
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (!Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)) && prob(50 / Dm.num(severity)))
        {
            shots_before_degradation = 0;
            emp_malfunction = TRUE;
            attempt_degradation(TRUE);
        }
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:450
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        object dot = await base.emag_act(user, emag_card, __extra);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        Dm.combine(ref obj_flags, EMAGGED);
        projectile_damage_multiplier = emagged_projectile_damage_multiplier;
        balloon_alert(user, "heat distribution systems deactivated");
        return TRUE;
    }

    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:459
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(await tool.use_tool(this, user, 20 * SECONDS, volume: 50)))
        {
            balloon_alert(user, "interrupted!");
            return ITEM_INTERACT_BLOCKING;
        }

        emp_malfunction = FALSE;
        shots_before_degradation = Dm.initial(this, (obj_item_gun_ballistic_automatic_battle_rifle p) => p.shots_before_degradation);
        degradation_stage = Dm.initial(this, (obj_item_gun_ballistic_automatic_battle_rifle p) => p.degradation_stage);
        projectile_speed_multiplier = Dm.initial(this, (obj_item_gun p) => p.projectile_speed_multiplier);
        fire_delay = Dm.initial(this, (obj_item_gun p) => p.fire_delay);
        update_appearance();
        balloon_alert(user, "system reset");
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:473
    public override async DmTask<object> process_fire(atom target = null, mob_living user = null, float? message = null, object @params = null, string zone_override = null, float? bonus_spread = null)
    {
        message ??= TRUE;
        zone_override ??= "";
        bonus_spread ??= 0;
        if (chambered?.loaded_projectile != null && prob(75) && (Dm.truthy(emp_malfunction) || degradation_stage == degradation_stage_max))
        {
            balloon_alert_to_hearers("*click*");
            playsound(this, dry_fire_sound, dry_fire_sound_volume, TRUE);
            return null;
        }

        object dot = await base.process_fire(target, user, message, @params, zone_override, bonus_spread);
        if (!Dm.truthy(dot))
            return dot;

        else if (shots_before_degradation != 0)
            shots_before_degradation--;

        else if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) && degradation_stage == degradation_stage_max && explosion_timer == null)
            perform_extreme_malfunction(user);

        else
            attempt_degradation(FALSE);
        return dot;
    }

    /// Proc to handle weapon degradation. Called when attempting to fire or immediately after an EMP takes place.
    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:493
    public virtual void attempt_degradation(object force_increment = null)
    {
        force_increment ??= FALSE;
        if (!prob(degradation_probability) && !Dm.truthy(force_increment) || degradation_stage == degradation_stage_max)
            return; //Only update if we actually increment our degradation stage

        degradation_stage = clamp(degradation_stage + (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) ? 2 : 1), 0, degradation_stage_max);
        projectile_speed_multiplier = clamp(Dm.initial(this, (obj_item_gun p) => p.projectile_speed_multiplier) + degradation_stage * 0.1f, Dm.initial(this, (obj_item_gun p) => p.projectile_speed_multiplier), maximum_speed_malus);
        fire_delay = Dm.initial(this, (obj_item_gun p) => p.fire_delay) + (degradation_stage * 0.5f);
        do_sparks(1, TRUE, this);
        update_appearance();
    }

    /// Called by /obj/machinery/recharger while inserted: attempts to recalibrate our gun but reducing degradation.
    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:504
    public virtual void attempt_recalibration(object restoring_shots_before_degradation = null, float? recharge_rate = null)
    {
        restoring_shots_before_degradation ??= FALSE;
        recharge_rate ??= 1;
        emp_malfunction = FALSE;

        if (Dm.truthy(restoring_shots_before_degradation))
        {
            shots_before_degradation = clamp(round(shots_before_degradation + recharge_rate.Value, 1), 0, max_shots_before_degradation);
        }
        else
        {
            degradation_stage = clamp(degradation_stage - 1, 0, degradation_stage_max);
            if (degradation_stage != 0)
            {
                projectile_speed_multiplier = clamp(Dm.initial(this, (obj_item_gun p) => p.projectile_speed_multiplier) - degradation_stage * 0.1f, maximum_speed_malus, Dm.initial(this, (obj_item_gun p) => p.projectile_speed_multiplier));
                fire_delay = Dm.initial(this, (obj_item_gun p) => p.fire_delay) + (degradation_stage * 0.5f);
            }
            else
            {
                projectile_speed_multiplier = Dm.initial(this, (obj_item_gun p) => p.projectile_speed_multiplier);
                fire_delay = Dm.initial(this, (obj_item_gun p) => p.fire_delay);
            }
        }

        update_appearance();
    }

    /// Proc to handle the countdown for our detonation
    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:522
    public virtual void perform_extreme_malfunction(mob_living user = null)
    {
        balloon_alert(user, "gun is exploding, throw it!");
        explosion_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(fucking_explodes_you)), 5 * SECONDS, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/modules/projectiles/guns/ballistic/automatic.dm", line: 527);
        playsound(this, Dm.resource("sound/items/weapons/gun/general/empty_alarm.ogg"), 50, FALSE);
    }

    /// proc to handle our detonation
    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:528
    public virtual async DmTask fucking_explodes_you()
    {
        await explosion(this, devastation_range: 1, heavy_impact_range: 3, light_impact_range: 6, explosion_cause: this);
    }

    //component for seclight attachment
    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:532
    public override async DmTask add_seclight_point()
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_seclite_attachable), Dm.named("light_overlay_icon", Dm.resource("icons/obj/weapons/guns/flashlights.dmi")), Dm.named("light_overlay", "flight"), Dm.named("overlay_x", 28), Dm.named("overlay_y", 12)));
    }
}

// The other full-auto ballistic guns (the owner, 2026-11-12): the Saber SMG.

// tg: code/modules/projectiles/guns/ballistic/automatic.dm:17
public partial class obj_item_gun_ballistic_automatic_proto : obj_item_gun_ballistic_automatic
{
    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_automatic_fire), 0.2f * SECONDS));
        return dot;
    }
}

// tg: code/modules/projectiles/guns/ballistic/automatic.dm:191
public partial class obj_item_gun_ballistic_automatic_tommygun : obj_item_gun_ballistic_automatic
{
    // tg: code/modules/projectiles/guns/ballistic/automatic.dm:210
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_automatic_fire), rof));
        return dot;
    }
}
