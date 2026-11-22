// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/under/_under.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the vars and var overrides of /obj/item/clothing/under, Initialize, worn_overlays, separate_worn_overlays,
// item_interaction (attaching accessories; the sensor branches gaps CL1), visual_equipped, equipped, dropped, the accessory procs
// (attach_accessory, pop_accessory, remove_accessory, get_accessory_overlays, Exited, click_alt_secondary) and set_sensor_mode
// translated (M6 slice 2: dump_attachments and atom_destruction, a jumpsuit torn by wounds), and the type /obj/item/clothing/under/rank (M2 F1: a player's
// jumpsuit is made, equipped and drawn). Suit sensors (update_wearer_status and the sensor procs: the crew monitor and the
// medical HUD, M8; get_sensor_text and examine are X3's); gaps CL1 (D333): rolling down (click_alt, the adjust verb), crafting,
// washing, the right clicks and the damaged state (the last part of this file); screentips are a live-partial row (D291). [DmPath("/obj/item/clothing/under")] is on the skeleton part in
// game/machinery/washing_machine.cs, the earliest .dme file that declares the type (rules 2.3).

// tg: code/modules/clothing/under/_under.dm:1
public partial class obj_item_clothing_under : obj_item_clothing
{
    // tg: code/modules/clothing/under/_under.dm:2-17
    public obj_item_clothing_under()
    {
        name = "under";
        icon = Dm.resource("icons/obj/clothing/under/default.dmi");
        worn_icon = Dm.resource("icons/mob/clothing/under/default.dmi");
        lefthand_file = Dm.resource("icons/mob/inhands/clothing/suits_lefthand.dmi");
        righthand_file = Dm.resource("icons/mob/inhands/clothing/suits_righthand.dmi");
        abstract_type = typeof(obj_item_clothing_under);
        body_parts_covered = Dm.bitor(Dm.bitor(Dm.bitor(CHEST, GROIN), LEGS), ARMS);
        slot_flags = ITEM_SLOT_ICLOTHING;
        interaction_flags_click = Dm.bitor(NEED_DEXTERITY, ALLOW_RESTING);
        // armor_type = /datum/armor/clothing_under (:11; the datum at :46, bio 10, wound 5): armour is not declared
        // (atom_defense.dm's armor vars and /datum/armor, M5); the override is left out until then.
        supports_variations_flags = Dm.bitor(CLOTHING_DIGITIGRADE_MASK, CLOTHING_CERULEAN_MASK_LEGS);
        bodyshapes_with_variations = Dm.bitor(BODYSHAPE_DIGITIGRADE, BODYSHAPE_CERULEAN);
        equip_sound = Dm.resource("sound/items/equip/jumpsuit_equip.ogg");
        drop_sound = Dm.resource("sound/items/handling/cloth/cloth_drop1.ogg");
        pickup_sound = Dm.resource("sound/items/handling/cloth/cloth_pickup1.ogg");
        limb_integrity = 30;
    }

    /// Has this undersuit been freshly laundered and, as such, imparts a mood bonus for wearing
    // tg: code/modules/clothing/under/_under.dm:20
    public float freshly_laundered = FALSE;

    // Alt style handling
    /// Can this suit be adjustd up or down to an alt style
    // tg: code/modules/clothing/under/_under.dm:24
    public float can_adjust = TRUE;
    /// If adjusted what style are we currently using?
    // tg: code/modules/clothing/under/_under.dm:26
    public float adjusted = NORMAL_STYLE;
    /// For adjusted/rolled-down jumpsuits. FALSE = exposes chest and arms, TRUE = exposes arms only
    // tg: code/modules/clothing/under/_under.dm:28
    public float alt_covers_chest = FALSE;
    /// The variable containing the flags for how the woman uniform cropping is supposed to interact with the sprite.
    // tg: code/modules/clothing/under/_under.dm:30
    public float female_sprite_flags = FEMALE_UNIFORM_FULL;

    // Sensor handling
    /// Does this undersuit have suit sensors in general
    // tg: code/modules/clothing/under/_under.dm:34
    public float has_sensor = HAS_SENSORS;
    /// Does this undersuit spawn with a random sensor value
    // tg: code/modules/clothing/under/_under.dm:36
    public float random_sensor = TRUE;
    /// What is the active sensor mode of this udnersuit
    // tg: code/modules/clothing/under/_under.dm:38
    public float sensor_mode = SENSOR_OFF;

    // Accessory handling (Can be componentized eventually)
    /// The max number of accessories we can have on this suit.
    // tg: code/modules/clothing/under/_under.dm:42
    public float max_number_of_accessories = 5;
    /// A list of all accessories attached to us.
    // tg: code/modules/clothing/under/_under.dm:44
    public DmList attached_accessories;

    // tg: code/modules/clothing/under/_under.dm:50
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(random_sensor))
        {
            //make the sensor mode favor higher levels, except coords.
            set_sensor_mode(pick(SENSOR_VITALS, SENSOR_VITALS, SENSOR_VITALS, SENSOR_LIVING, SENSOR_LIVING, SENSOR_COORDS, SENSOR_COORDS, SENSOR_OFF));
        }
        //ensure we add or remove relevant clothing traits based on base sensor status and mode
        update_wearer_status();

        // register_context(): the screentip contexts (add_context, :74) are the screentips' (HUD, M9)
        Dm.Deferred("code/modules/clothing/under/_under.dm:58", "Initialize: register_context(): the jumpsuit's screentips (screentips, M10+)");
        // AddElement(/datum/element/update_icon_updates_onmob, flags = ..., body = TRUE): the element is not declared; it
        // redraws the wearer when the worn jumpsuit's icon changes (adjusting, damage, dyeing: clothing, M4a). An M2
        // jumpsuit's icon does not change once it is worn.
        Dm.Deferred("code/modules/clothing/under/_under.dm:59", "Initialize: AddElement(/datum/element/update_icon_updates_onmob): redrawing the wearer when the jumpsuit's icon changes (clothing, M4a)");
        return dot;
    }

    // tg: code/modules/clothing/under/_under.dm:110
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        isinhands ??= FALSE;
        bodyshape ??= NONE;
        DmList dot = null;
        dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape) as DmList;
        if (Dm.truthy(isinhands))
            return dot;
        if (Dm.truthy(damaged_clothes))
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/effects/item_damage.dmi"), "damageduniform"));
        Dm.append(ref dot, get_accessory_overlays());
        return dot;
    }

    // tg: code/modules/clothing/under/_under.dm:118
    public override async DmTask<object> separate_worn_overlays(mutable_appearance standing = null, mutable_appearance draw_target = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        isinhands ??= FALSE;
        bodyshape ??= NONE;
        DmList dot = null;
        dot = await base.separate_worn_overlays(standing, draw_target, isinhands, icon_file, bodyshape) as DmList;
        if (Dm.truthy(isinhands))
            return dot;
        object blood_overlay = await get_blood_overlay("uniform", bodyshape);
        if (Dm.truthy(blood_overlay))
            Dm.append(ref dot, blood_overlay);
        return dot;
    }

    // tg: code/modules/clothing/under/_under.dm:126
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_stack_cable_coil)))
        {
            // gaps CL1 (D333): repairing broken suit sensors with cable
            if (!Dm.truthy(repair_sensors(user)))
                return ITEM_INTERACT_BLOCKING;
            obj_item_stack_cable_coil cabling = (obj_item_stack_cable_coil)tool;
            await cabling.use(1);
            cabling.visible_message(span_notice(I18n.T("[] repairs the suit sensors on [] with [].", user, this, cabling)));
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_clothing_accessory)))
            return Dm.truthy(await attach_accessory(tool as obj_item_clothing_accessory, user)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;

        if (istype(tool, typeof(obj_item_suit_sensor)))
        {
            // gaps CL1 (D333): installing a suit sensor
            if (has_sensor != NO_SENSORS)
            {
                balloon_alert(user, I18n.T("already has sensors!"));
                return ITEM_INTERACT_BLOCKING;
            }
            balloon_alert(user, I18n.T("installing sensors..."));
            if (!Dm.truthy(await do_after(user, 5 * SECONDS, this)))
                return ITEM_INTERACT_BLOCKING;
            obj_item_suit_sensor sensor = (obj_item_suit_sensor)tool;
            if (Dm.truthy(sensor.broken))
                set_has_sensor(BROKEN_SENSORS);
            else
            {
                set_has_sensor(HAS_SENSORS);
                set_sensor_mode(sensor.sensor_mode);
            }
            qdel(tool);
            balloon_alert(user, I18n.T("sensors installed"));
            playsound(source: this, soundin: Dm.resource("sound/effects/sparks/sparks4.ogg"), vol: 50, vary: TRUE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE, ignore_walls: FALSE);
            return ITEM_INTERACT_SUCCESS;
        }

        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    // tg: code/modules/clothing/under/_under.dm:199
    [DmParams("user", "slot")]
    public override async DmTask<object> visual_equipped(mob user = null, object slot = null, float? initial = null)
    {
        object dot = null;
        dot = await base.visual_equipped(user, slot, initial);
        if (adjusted == ALT_STYLE)
            adjust_to_normal();

        if (Dm.truthy(Dm.bitand(supports_variations_flags, CLOTHING_DIGITIGRADE_VARIATION)) && ishuman(user))
        {
            mob_living_carbon_human wearer = user as mob_living_carbon_human;
            if (Dm.truthy(Dm.bitand(wearer.bodyshape, BODYSHAPE_DIGITIGRADE)))
            {
                adjusted = DIGITIGRADE_STYLE;
                update_appearance();
            }
        }
        return dot;
    }

    // tg: code/modules/clothing/under/_under.dm:225
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_ICLOTHING)))
        {
            if (Dm.truthy(freshly_laundered))
            {
                freshly_laundered = FALSE;
                await ((mob_living)user).add_mood_event("fresh_laundry", typeof(datum_mood_event_fresh_laundry));
            }
            update_wearer_status();
        }
    }

    // tg: code/modules/clothing/under/_under.dm:233
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.dropped(user, silent, __extra);
        if (Dm.eq(user.get_item_by_slot(ITEM_SLOT_ICLOTHING), this))
            Dm.remove(ref GLOB.suit_sensors_list, user);
        return dot;
    }

    // tg: code/modules/clothing/under/_under.dm:354
    public virtual async DmTask<object> attach_accessory(obj_item_clothing_accessory accessory = null, mob_living user = null, float? attach_message = null)
    {
        attach_message ??= TRUE;
        if (!(accessory is obj_item_clothing_accessory))
            return null;
        if (!Dm.truthy(accessory.can_attach_accessory(this, user)))
            return null;
        if (Dm.truthy(user) && !Dm.truthy(await user.temporarilyRemoveItemFromInventory(accessory)))
            return null;
        if (!Dm.truthy(await accessory.try_attach(this, user)))
            return null;

        // Allow for accessories to react to the acccessory list now
        await accessory.attach(this);

        if (Dm.truthy(user) && Dm.truthy(attach_message))
            balloon_alert(user, I18n.T("accessory attached"));

        update_appearance();
        return TRUE;
    }

    /// Removes (pops) the topmost accessory from the accessories list and puts it in the user's hands if supplied
    // tg: code/modules/clothing/under/_under.dm:374
    public virtual async DmTask pop_accessory(mob_living user = null, float? attach_message = null)
    {
        attach_message ??= TRUE;
        obj_item_clothing_accessory popped_accessory = attached_accessories[1] as obj_item_clothing_accessory;
        remove_accessory(popped_accessory);

        if (!Dm.truthy(user))
            return;

        await user.put_in_hands(popped_accessory);
        if (Dm.truthy(attach_message))
            popped_accessory.balloon_alert(user, I18n.T("accessory removed"));
    }

    /// Removes the passed accesory from our accessories list
    // tg: code/modules/clothing/under/_under.dm:386
    public virtual void remove_accessory(obj_item_clothing_accessory removed = null, float? update = null)
    {
        update ??= TRUE;
        // Remove it from the list before detaching
        LAZYREMOVE(ref attached_accessories, removed);
        removed.detach(this, update);
        update_appearance();
    }

    // tg: code/modules/clothing/under/_under.dm:405
    public virtual async DmTask dump_attachments(atom drop_to = null)
    {
        drop_to ??= drop_location() as atom;

        foreach (obj_item_clothing_accessory worn_accessory in Dm.each_cast<obj_item_clothing_accessory>(attached_accessories))
        {
            remove_accessory(worn_accessory, update: FALSE);
            await worn_accessory.forceMove(drop_to);
        }

        if (ishuman(loc))
        {
            mob_living_carbon_human wearer = (mob_living_carbon_human)loc;
            await wearer.update_clothing(slot_flags);
        }
    }

    // tg: code/modules/clothing/under/_under.dm:414
    public override async DmTask atom_destruction(object damage_flag = null)
    {
        await dump_attachments();
        await base.atom_destruction(damage_flag);
    }

    /// Get a list of all accessory overlays
    // tg: code/modules/clothing/under/_under.dm:393
    public virtual object get_accessory_overlays()
    {
        DmList dot = null;
        dot = Dm.list();
        foreach (obj_item_clothing_accessory accessory in Dm.each_cast<obj_item_clothing_accessory>(attached_accessories))
            Dm.append(ref dot, accessory.generate_accessory_overlay(this));
        return dot;
    }

    // tg: code/modules/clothing/under/_under.dm:398
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = null;
        dot = await base.Exited(gone, direction);
        // If one of our accessories was moved out, handle it
        if (Dm.@in(gone, attached_accessories))
            remove_accessory(gone as obj_item_clothing_accessory);
        return dot;
    }

    // tg: code/modules/clothing/under/_under.dm:516
    public override async DmTask<object> click_alt_secondary(mob user = null)
    {
        if (!Dm.truthy(LAZYLEN(attached_accessories)))
        {
            balloon_alert(user, I18n.T("no accessories to remove!"));
            return null;
        }
        await pop_accessory(user as mob_living);
        return null;
    }

    /// Changes the active sensor mode of the suit and handles updating mob status if applicable
    // tg: code/modules/clothing/under/_under.dm:281
    public virtual object set_sensor_mode(object new_sensor_mode = null)
    {
        if (Dm.eq(new_sensor_mode, sensor_mode))
            return FALSE;
        if (Dm.lt(new_sensor_mode, SENSOR_OFF) || Dm.gt(new_sensor_mode, SENSOR_COORDS))
        {
            stack_trace(Dm.text("Invalid sensor_mode value [] passed to set_sensor_mode()", new_sensor_mode), "code/modules/clothing/under/_under.dm", 285);
            return FALSE;
        }
        sensor_mode = Dm.num(new_sensor_mode);
        update_wearer_status();
        return TRUE;
    }

    /// Updates the sensor status and any mobs status if applicable
    // tg: code/modules/clothing/under/_under.dm:292
    public virtual void update_wearer_status()
    {
        if (has_sensor <= NO_SENSORS || sensor_mode <= SENSOR_VITALS)
            detach_clothing_traits(TRAIT_BASIC_HEALTH_HUD_VISIBLE);
        else
            attach_clothing_traits(TRAIT_BASIC_HEALTH_HUD_VISIBLE);

        if (!ishuman(loc) || isdummy(loc))
            return;

        mob_living_carbon_human wearer = (mob_living_carbon_human)loc;
        if (wearer.get_item_by_slot(ITEM_SLOT_ICLOTHING) != this)
            return;

        if (has_sensor >= HAS_SENSORS && sensor_mode >= SENSOR_LIVING)
            Dm.combine(ref GLOB.suit_sensors_list, wearer);
        else
            Dm.remove(ref GLOB.suit_sensors_list, wearer);

        wearer.med_hud_set_status();
    }

    /**
     * Gets the text for the suit sensor status, for the medical HUD's examine.
     *
     * Arguments:
     * * silent: If TRUE, will return blank if everything is fine
     */
    // tg: code/modules/clothing/under/_under.dm:334
    public virtual object get_sensor_text(object silent = null)
    {
        silent ??= TRUE;
        if (has_sensor == BROKEN_SENSORS)
            return I18n.T("<font color='#ffcc33'>Non-Functional: Repair with cable coil</font>");

        if (Dm.truthy(silent))
            return "";

        if (has_sensor == NO_SENSORS)
            return I18n.T("Not Present");
        if (has_sensor == LOCKED_SENSORS)
            return I18n.T("Functional, Locked");
        if (has_sensor == HAS_SENSORS)
            return I18n.T("Functional");
        return null;
    }

    /// Helper to toggle the jumpsuit style, if possible
    /// Returns the new state
    // tg: code/modules/clothing/under/_under.dm:541  (M8z slice B: the strip menu's "Adjust jumpsuit"; the adjust verb and rolldown are M9)
    public virtual object toggle_jumpsuit_adjust()
    {
        if (adjusted == DIGITIGRADE_STYLE)
            return null;

        if (adjusted == NORMAL_STYLE)
            adjust_to_alt();
        else if (adjusted == ALT_STYLE)
            adjust_to_normal();

        if (Dm.truthy(this._listen_lookup?[COMSIG_CLOTHING_UNDER_ADJUSTED]))
            this._SendSignal(COMSIG_CLOTHING_UNDER_ADJUSTED, Dm.list(this));
        return adjusted;
    }

    /// Helper to reset to normal jumpsuit state
    // tg: code/modules/clothing/under/_under.dm:556
    public virtual void adjust_to_normal()
    {
        adjusted = NORMAL_STYLE;
        female_sprite_flags = Dm.initial(this, (obj_item_clothing_under p) => p.female_sprite_flags);
        if (!Dm.truthy(alt_covers_chest))
        {
            Dm.combine(ref body_parts_covered, CHEST);
            Dm.combine(ref body_parts_covered, ARMS);
        }
        if (Dm.truthy(LAZYLEN(damage_by_parts)))
        {
            // ugly check to make sure we don't reenable protection on a disabled part
            foreach (object zone in Dm.each(Dm.list(BODY_ZONE_CHEST, BODY_ZONE_L_ARM, BODY_ZONE_R_ARM)))
            {
                if (Dm.gt(damage_by_parts[zone], limb_integrity))
                    Dm.mask(ref body_parts_covered, body_zone2cover_flags(zone));
            }
        }
    }

    /// Helper to adjust to alt jumpsuit state
    // tg: code/modules/clothing/under/_under.dm:569
    public virtual void adjust_to_alt()
    {
        adjusted = ALT_STYLE;
        if (!Dm.truthy(Dm.bitand(female_sprite_flags, FEMALE_UNIFORM_TOP_ONLY)))
            female_sprite_flags = NO_FEMALE_UNIFORM;
        if (!Dm.truthy(alt_covers_chest)) // for the special snowflake suits that expose the chest when adjusted (and also the arms, realistically)
        {
            Dm.mask(ref body_parts_covered, Dm.bitnot(CHEST));
            Dm.mask(ref body_parts_covered, Dm.bitnot(ARMS));
        }
    }
}

// tg: code/modules/clothing/under/_under.dm:422
public partial class obj_item_clothing_under : obj_item_clothing
{
    // tg: code/modules/clothing/under/_under.dm:422
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(can_adjust))
            Dm.append(ref dot, I18n.T("Alt-click on [] to wear it [].", this, adjusted == ALT_STYLE ? I18n.T("normally") : I18n.T("casually")));
        if (has_sensor == BROKEN_SENSORS)
            Dm.append(ref dot, span_warning(I18n.T("The medical sensors appear to be shorted out. You could repair it with some cabling.")));
        else if (has_sensor > NO_SENSORS)
        {
            if (sensor_mode == SENSOR_OFF)
                Dm.append(ref dot, I18n.T("Its sensors appear to be disabled."));
            else if (sensor_mode == SENSOR_LIVING)
                Dm.append(ref dot, I18n.T("Its binary life sensors appear to be enabled."));
            else if (sensor_mode == SENSOR_VITALS)
                Dm.append(ref dot, I18n.T("Its vital tracker appears to be enabled."));
            else if (sensor_mode == SENSOR_COORDS)
                Dm.append(ref dot, I18n.T("Its vital tracker and tracking beacon appear to be enabled."));
        }
        else
            Dm.append(ref dot, span_tooltip(I18n.T("You can always get new suit sensors to install from a lathe."), I18n.T("It isn't equipped with medical sensors.")));

        if (Dm.truthy(LAZYLEN(attached_accessories)))
        {
            object accessories = list_accessories_with_icon(user);
            Dm.append(ref dot, I18n.T("It has [] attached.", english_list(accessories as DmList)));
            Dm.append(ref dot, I18n.T("Alt-Right-Click to remove [].", attached_accessories[1]));
        }
        return dot;
    }

    /// Helper to list out all accessories with an icon besides it, for use in examine
    // tg: code/modules/clothing/under/_under.dm:447  (M9 slice G: the human's examine)
    public virtual object list_accessories_with_icon(mob user = null)
    {
        DmList all_accessories = Dm.list();
        foreach (obj_item_clothing_accessory attached in Dm.each_cast<obj_item_clothing_accessory>(attached_accessories))
            Dm.append(ref all_accessories, attached.examine_title(user));

        return all_accessories;
    }
}

// tg: code/modules/clothing/under/_under.dm:582
[DmPath("/obj/item/clothing/under/rank")]
public partial class obj_item_clothing_under_rank : obj_item_clothing_under
{
    // tg: code/modules/clothing/under/_under.dm:583-584
    public obj_item_clothing_under_rank()
    {
        abstract_type = typeof(obj_item_clothing_under_rank);
        dying_key = DYE_REGISTRY_UNDER;
    }
}

// tg: code/modules/clothing/under/_under.dm  (gaps J5, D298: the suit sensors' cutting, breaking, repairing and setting)
public partial class obj_item_clothing_under : obj_item_clothing
{
    // tg: code/modules/clothing/under/_under.dm:152
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.eq(has_sensor, NO_SENSORS))
        {
            balloon_alert(user, I18n.T("doesn't have sensors!"));
            return ITEM_INTERACT_BLOCKING;
        }
        balloon_alert(user, I18n.T("cutting out sensors..."));
        if (!Dm.truthy(await do_after(user, 5 * SECONDS, this)))
            return ITEM_INTERACT_BLOCKING;
        obj_item_suit_sensor sensor = Dm.@new<obj_item_suit_sensor>(drop_location());
        if (Dm.truthy(await sensor.IsReachableBy(user)))
            await user.put_in_hands(sensor);
        if (Dm.eq(has_sensor, BROKEN_SENSORS))
            sensor.broken = TRUE;
        else
            sensor.set_mode(sensor_mode);
        set_has_sensor(NO_SENSORS);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/clothing/under/_under.dm:280
    public virtual void break_sensors()
    {
        if (Dm.eq(has_sensor, BROKEN_SENSORS) || Dm.eq(has_sensor, NO_SENSORS))
            return;

        visible_message(span_warning(I18n.T("[]'s medical sensors short out!", this)), blind_message: span_warning(I18n.T("The [] makes an electronic sizzling sound!", this)), vision_distance: COMBAT_MESSAGE_RANGE);
        set_has_sensor(BROKEN_SENSORS);
        sensor_malfunction();
    }

    // tg: code/modules/clothing/under/_under.dm:288
    public virtual object repair_sensors(mob user = null)
    {
        if (!Dm.eq(has_sensor, BROKEN_SENSORS))
        {
            if (user != null)
                balloon_alert(user, I18n.T("sensors []!", Dm.eq(has_sensor, NO_SENSORS) ? "missing" : "not broken"));
            return FALSE;
        }
        playsound(this, Dm.resource("sound/effects/sparks/sparks4.ogg"), 100, TRUE, SHORT_RANGE_SOUND_EXTRARANGE, ignore_walls: FALSE);
        set_has_sensor(HAS_SENSORS);
        return TRUE;
    }

    // tg: code/modules/clothing/under/_under.dm:298
    public virtual void sensor_malfunction()
    {
        if (!QDELETED(this) && Dm.eq(has_sensor, BROKEN_SENSORS) && ishuman(loc))
        {
            do_sparks(2, FALSE, this);
            _addtimer(Dm.@new<datum_callback>(this, nameof(sensor_malfunction)), rand(BROKEN_SPARKS_MIN, BROKEN_SPARKS_MAX * 0.5f), Dm.bitor(TIMER_UNIQUE, TIMER_NO_HASH_WAIT), file: "code/modules/clothing/under/_under.dm", line: 301);
        }
    }

    // tg: code/modules/clothing/under/_under.dm:304
    public virtual object set_has_sensor(object new_has_sensor = null)
    {
        if (Dm.eq(new_has_sensor, has_sensor))
            return FALSE;
        if (Dm.num(new_has_sensor) < BROKEN_SENSORS || Dm.num(new_has_sensor) > LOCKED_SENSORS)
        {
            stack_trace(Dm.text("Invalid has_sensor value [] passed to set_has_sensor()", new_has_sensor), "code/modules/clothing/under/_under.dm", 308);
            return FALSE;
        }
        has_sensor = Dm.num(new_has_sensor);
        update_wearer_status();
        return TRUE;
    }

    // tg: code/modules/clothing/under/_under.dm:330
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        if (Dm.eq(has_sensor, NO_SENSORS) || Dm.eq(has_sensor, BROKEN_SENSORS))
            return dot;

        if (Dm.num(severity) <= EMP_HEAVY)
        {
            break_sensors();
            return dot;
        }

        set_sensor_mode(pick(SENSOR_OFF, SENSOR_OFF, SENSOR_OFF, SENSOR_LIVING, SENSOR_LIVING, SENSOR_VITALS, SENSOR_VITALS, SENSOR_COORDS));
        playsound(this, Dm.resource("sound/effects/sparks/sparks3.ogg"), 75, TRUE, SHORT_RANGE_SOUND_EXTRARANGE, ignore_walls: FALSE);
        visible_message(span_warning(I18n.T("The []'s medical sensors flash and change rapidly!", this)), blind_message: span_warning(I18n.T("The [] makes an electronic sizzling sound!", this)), vision_distance: COMBAT_MESSAGE_RANGE);
        return dot;
    }

    // tg: code/modules/clothing/under/_under.dm:454  GAME_VERB_SRC(/obj/item/clothing/under, toggle, usr, "Adjust Suit Sensors", null)
    public virtual async DmTask<object> __gvb_toggle(DmList structured_args = null)
    {
        mob user_mob = usr;
        if (!Dm.truthy(await can_toggle_sensors(user_mob)))
            return null;

        object current_mode_text = GLOB.suit_sensor_mode_to_defines[Dm.num(sensor_mode) + 1];
        object new_mode = await tgui_input_list(user_mob, I18n.T("Select a sensor mode"), I18n.T("Suit Sensors"), GLOB.suit_sensor_mode_to_defines, current_mode_text);
        if (new_mode == null)
            return null;
        if (!Dm.truthy(await can_toggle_sensors(user_mob)))
            return null;

        set_sensor_mode(GLOB.suit_sensor_mode_to_defines[new_mode]);
        if (loc == user_mob)
        {
            switch ((int)Dm.num(sensor_mode))
            {
                case SENSOR_OFF:
                    to_chat(user_mob, span_notice(I18n.T("You disable your suit's remote sensing equipment.")));
                    break;
                case SENSOR_LIVING:
                    to_chat(user_mob, span_notice(I18n.T("Your suit will now only report whether you are alive or dead.")));
                    break;
                case SENSOR_VITALS:
                    to_chat(user_mob, span_notice(I18n.T("Your suit will now only report your exact vital lifesigns.")));
                    break;
                case SENSOR_COORDS:
                    to_chat(user_mob, span_notice(I18n.T("Your suit will now report your exact vital lifesigns as well as your coordinate position.")));
                    break;
            }
        }
        return null;
    }

    // tg: code/modules/clothing/under/_under.dm:454
    public virtual void toggle(params object[] __extra)
    {
        Waiting.Detach(__gvb_toggle());
    }

    // tg: code/modules/clothing/under/_under.dm:479
    public override async DmTask<object> item_ctrl_click(mob user = null)
    {
        if (!Dm.truthy(await can_toggle_sensors(user)))
            return CLICK_ACTION_BLOCKING;

        set_sensor_mode(SENSOR_COORDS);
        balloon_alert(user, I18n.T("set to tracking"));
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/clothing/under/_under.dm:487
    public virtual async DmTask<object> can_toggle_sensors(mob toggler = null)
    {
        if (!Dm.truthy(await can_use(toggler)) || Dm.eq(toggler.stat, DEAD)) //make sure they didn't hold the window open.
            return FALSE;
        if (Dm.num(get_dist(toggler, this)) > 1)
        {
            balloon_alert(toggler, I18n.T("too far!"));
            return FALSE;
        }

        switch ((int)Dm.num(has_sensor))
        {
            case LOCKED_SENSORS:
                balloon_alert(toggler, I18n.T("sensor controls locked!"));
                return FALSE;
            case BROKEN_SENSORS:
                balloon_alert(toggler, I18n.T("sensors shorted!"));
                return FALSE;
            case NO_SENSORS:
                balloon_alert(toggler, I18n.T("no sensors to adjust!"));
                return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/clothing/under/_under.dm:577
    public override async DmTask<object> can_use(mob user = null)
    {
        if (ismob(user) && !Dm.truthy(await user.can_perform_action(this, Dm.bitor(Dm.bitor(NEED_DEXTERITY, NEED_HANDS), ALLOW_RESTING))))
            return FALSE;
        return await base.can_use(user);
    }
}

// Gaps CL1 (D333): the rest of the jumpsuit's procs (the sensors kept through crafting, the right click that sets the sensors, the
// sensors broken by shredding and repaired with the suit, the fresh laundry, rolling down by the alt-click and the verb). The
// screentip (add_context) is a live-partial row (D291) and the digitigrade icon a deferred leaf (not-translated.md 61).
// tg: code/modules/clothing/under/_under.dm  (gaps CL1)
public partial class obj_item_clothing_under : obj_item_clothing
{
    // tg: code/modules/clothing/under/_under.dm:61
    public override async DmTask on_craft_completion(DmList components = null, datum_crafting_recipe current_recipe = null, atom crafter = null)
    {
        await base.on_craft_completion(components, current_recipe, crafter);
        obj_item_clothing_under any_original = Dm.locate_type(typeof(obj_item_clothing_under), components) as obj_item_clothing_under;
        if (any_original == null)
        {
            set_has_sensor(NO_SENSORS);
            return;
        }
        set_has_sensor(any_original.has_sensor);
        set_sensor_mode(any_original.sensor_mode);
    }

    // tg: code/modules/clothing/under/_under.dm:70
    public override async DmTask used_in_craft(atom result = null, datum_crafting_recipe current_recipe = null)
    {
        await base.used_in_craft(result, current_recipe);
        await dump_attachments();
    }

    // tg: code/modules/clothing/under/_under.dm:175
    [DmParams("user", "params")]
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;

        toggle();
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/clothing/under/_under.dm:183
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        object dot = await base.attack_self_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;

        toggle();
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/clothing/under/_under.dm:191
    public override async DmTask update_clothes_damaged_state(float? damaged_state = null)
    {
        damaged_state ??= CLOTHING_DAMAGED;
        await base.update_clothes_damaged_state(damaged_state);
        if (damaged_state == CLOTHING_SHREDDED && has_sensor > NO_SENSORS)
            break_sensors();
        else if (damaged_state == CLOTHING_PRISTINE && has_sensor == BROKEN_SENSORS)
            repair_sensors();
        update_appearance();
    }

    // tg: code/modules/clothing/under/_under.dm:210
    public override object generate_digitigrade_icons(icon base_icon = null, object greyscale_colors = null)
    {
        icon new_legs = Dm.icon(SSgreyscale.GetColoredIconByType(typeof(datum_greyscale_config_digitigrade), greyscale_colors), "jumpsuit_worn");
        return apply_icon_mask(base_icon, LEGS_MASK, new_legs);
    }

    // tg: code/modules/clothing/under/_under.dm:214
    [DmParams()]
    public override async DmTask machine_wash(obj_machinery_washing_machine washer = null, params object[] __extra)
    {
        await base.machine_wash(washer, __extra);
        if (Dm.truthy(stubborn_stains))
            return;

        object fresh_mood = await AddComponent(
            typeof(datum_component_onwear_mood),
            Dm.named("saved_event_type", typeof(datum_mood_event_fresh_laundry))
        );
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 2 * MINUTES > GC_FILTER_QUEUE ? (object)WEAKREF(fresh_mood as datum) : fresh_mood), 2 * MINUTES, file: "code/modules/clothing/under/_under.dm", line: 222); // QDEL_IN(fresh_mood, 2 MINUTES), inline (rules 11.1 (f))
    }

    // tg: code/modules/clothing/under/_under.dm:418
    public override object Destroy(float? force = null)
    {
        QDEL_LAZYLIST(ref attached_accessories);
        return base.Destroy(force);
    }

    // tg: code/modules/clothing/under/_under.dm:507
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(can_adjust))
        {
            balloon_alert(user, I18n.T("can't be adjusted!"));
            return CLICK_ACTION_BLOCKING;
        }
        if (!Dm.truthy(await can_use(user)))
            return NONE;
        rolldown();
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/clothing/under/_under.dm:522  GAME_VERB_SRC(/obj/item/clothing/under, jumpsuit_adjust, usr, "Adjust Jumpsuit Style", null)
    public virtual async DmTask __gvb_jumpsuit_adjust(DmList structured_args = null)
    {
        if (!Dm.truthy(can_adjust))
        {
            balloon_alert(usr, I18n.T("can't be adjusted!"));
            return;
        }
        if (!Dm.truthy(await can_use(usr)))
            return;
        rolldown();
    }

    // tg: code/modules/clothing/under/_under.dm:522
    public virtual void jumpsuit_adjust(params object[] __extra)
    {
        Waiting.Detach(__gvb_jumpsuit_adjust());
    }

    // tg: code/modules/clothing/under/_under.dm:531
    public virtual void rolldown()
    {
        if (Dm.truthy(toggle_jumpsuit_adjust()))
            to_chat(usr, span_notice(I18n.T("You adjust the suit to wear it more casually.")));
        else
            to_chat(usr, span_notice(I18n.T("You adjust the suit back to normal.")));

        update_appearance();
    }
}
