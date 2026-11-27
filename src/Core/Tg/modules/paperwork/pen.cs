// Translated from tgstation (snapshot 1d99bd7f): code/modules/paperwork/pen.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S3, D298) with the captain's pen's skins (setup_reskins and the /datum/atom_skin/cap_pen datums). The foam dart insert component is inert, so a pen
// never goes into a dart yet: its dart procs are translated and wait for it. The var blocks are generated.

/* Pens!
 * Contains:
 * Pens
 * Sleepy Pens
 * Parapens
 * Edaggers
 */

/*
 * Pens
 */
// tg: code/modules/paperwork/pen.dm:13
public partial class obj_item_pen : obj_item
{
    // tg: code/modules/paperwork/pen.dm:43
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_dart_insert),
            dart_insert_icon,
            dart_insert_casing_icon_state,
            dart_insert_icon,
            dart_insert_projectile_icon_state,
            Dm.@new<datum_callback>(this, nameof(get_dart_var_modifiers))));
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_renaming)));
        RegisterSignal(this, COMSIG_DART_INSERT_ADDED, nameof(on_inserted_into_dart));
        RegisterSignal(this, COMSIG_DART_INSERT_REMOVED, nameof(on_removed_from_dart));
        if (!Dm.truthy(can_click))
            return dot;
        Waiting.NoSleep(create_transform_component());
        RegisterSignal(this, COMSIG_TRANSFORMING_ON_TRANSFORM, nameof(on_transform));
        return dot;
    }

    // tg: code/modules/paperwork/pen.dm:60
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_iron), 2), Dm.kv(typeof(datum_reagent_iodine), 1));
    }

    /// Proc that child classes can override to have custom transforms, like edaggers or pendrivers
    // tg: code/modules/paperwork/pen.dm:64
    public virtual async DmTask create_transform_component()
    {
        await AddComponent(typeof(datum_component_transforming),
            Dm.named("sharpness_on", NONE),
            Dm.named("inhand_icon_change", FALSE),
            Dm.named("w_class_on", w_class));
    }

    /*
     * Signal proc for [COMSIG_TRANSFORMING_ON_TRANSFORM].
     *
     * Clicks the pen to make an annoying sound. Clickity clickery click!
     */
    // tg: code/modules/paperwork/pen.dm:77
    public virtual object on_transform(obj_item source = null, mob user = null, object active = null)
    {
        if (user != null)
            balloon_alert(user, I18n.T("clicked"));
        playsound(this, Dm.resource("sound/items/pen_click.ogg"), 30, TRUE, -3);
        icon_state = Dm.text("[][]", Dm.initial(this, nameof(icon_state)), Dm.truthy(active) ? "_retracted" : "");
        update_appearance(UPDATE_ICON);

        return COMPONENT_NO_DEFAULT_MESSAGE;
    }

    // tg: code/modules/paperwork/pen.dm:88  (dart is object, Appendix A: the dart insert component passes the casing, the sleepy and
    // survival pens read it as one)
    public virtual void on_inserted_into_dart(datum source = null, object dart = null, mob user = null, float? embedded = null)
    {
    }

    // tg: code/modules/paperwork/pen.dm:91
    public virtual async DmTask<object> get_dart_var_modifiers(obj_projectile projectile = null)
    {
        return Dm.list(
            Dm.kv("damage", max(5, throwforce)),
            Dm.kv("speed", max(0, throw_speed - 3)),
            Dm.kv("embedding", (get_embed() as datum_embedding)?.create_copy()),
            Dm.kv("armour_penetration", armour_penetration),
            Dm.kv("wound_bonus", wound_bonus),
            Dm.kv("exposed_wound_bonus", exposed_wound_bonus),
            Dm.kv("demolition_mod", demolition_mod));
    }

    // tg: code/modules/paperwork/pen.dm:102
    public virtual void on_removed_from_dart(datum source = null, object dart = null, object user = null, params object[] __extra)
    {
    }

    // tg: code/modules/paperwork/pen.dm:105
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is scribbling numbers all over []self with []! It looks like [] trying to commit sudoku...", user, user.p_them(), this, user.p_theyre())));
        return BRUTELOSS;
    }

    // tg: code/modules/paperwork/pen.dm:245
    public override async DmTask<object> item_ctrl_click(mob user = null)
    {
        if (loc != user)
        {
            to_chat(user, span_warning(I18n.T("You must be holding the pen to continue!")));
            return CLICK_ACTION_BLOCKING;
        }
        object deg = await tgui_input_number(user, I18n.T("What angle would you like to rotate the pen head to? (0-360)"), I18n.T("Rotate Pen Head"), max_value: 360);
        if (deg == null || QDELETED(user) || QDELETED(this) || !Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)) || loc != user)
            return CLICK_ACTION_BLOCKING;
        degrees = Dm.num(deg);
        to_chat(user, span_notice(I18n.T("You rotate the top of the pen to [] degrees.", deg)));
        if (Dm.truthy(this._listen_lookup?[COMSIG_PEN_ROTATED]))
            this._SendSignal(COMSIG_PEN_ROTATED, Dm.list(this, deg, user));
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/paperwork/pen.dm:258
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(force)) // If the pen has a force value, call the normal attack procs. Used for e-daggers and captain's pen mostly.
            return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        if (!Dm.truthy(target_mob.try_inject(user, injection_flags: INJECT_TRY_SHOW_ERROR_MESSAGE)))
            return FALSE;
        to_chat(user, span_warning(I18n.T("You stab [] with the pen.", target_mob)));
        to_chat(target_mob, span_danger(I18n.T("You feel a tiny prick!")));
        log_combat(user, target_mob, "stabbed", this);
        return TRUE;
    }

    // tg: code/modules/paperwork/pen.dm:268
    public override async DmTask<object> get_writing_implement_details()
    {
        if (HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
            return null;
        return Dm.list(
            Dm.kv("interaction_mode", MODE_WRITING),
            Dm.kv("font", font),
            Dm.kv("color", colour),
            Dm.kv("use_bold", FALSE));
    }
}

// tg: code/modules/paperwork/pen.dm:111
public partial class obj_item_pen_fourcolor : obj_item_pen
{
    // tg: code/modules/paperwork/pen.dm:133
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        string chosen_color = "black";
        switch (colour)
        {
            case COLOR_BLACK:
                colour = COLOR_RED;
                chosen_color = "red";
                throw_speed++;
                break;
            case COLOR_RED:
                colour = COLOR_VIBRANT_LIME;
                chosen_color = "green";
                throw_speed--;
                break;
            case COLOR_VIBRANT_LIME:
                colour = COLOR_BLUE;
                chosen_color = "blue";
                break;
            default:
                colour = COLOR_BLACK;
                break;
        }
        to_chat(user, span_notice(I18n.T("\\The [] will now write in [].", this, chosen_color)));
        desc = I18n.T("It's a fancy four-color ink pen, set to [].", chosen_color);
        balloon_alert(user, I18n.T("clicked"));
        playsound(this, Dm.resource("sound/machines/click.ogg"), 30, TRUE, -3);
        return dot;
    }
}

// tg: code/modules/paperwork/pen.dm:164
public partial class obj_item_pen_charcoal : obj_item_pen
{
    // tg: code/modules/paperwork/pen.dm:174
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_ash), 5), Dm.kv(typeof(datum_reagent_cellulose), 10));
    }
}

// tg: code/modules/paperwork/pen.dm:213
public partial class obj_item_pen_fountain_captain : obj_item_pen_fountain
{
    // tg: code/modules/paperwork/pen.dm:239
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering),
            Dm.named("speed", 20 * SECONDS),
            Dm.named("effectiveness", 115)));
        //the pen is mightier than the sword
        RegisterSignal(this, COMSIG_DART_INSERT_PARENT_RESKINNED, nameof(reskin_dart_insert));
        return dot;
    }

    // tg: code/modules/paperwork/pen.dm:249
    public override async DmTask setup_reskins()
    {
        await AddComponent(typeof(datum_component_reskinable_item), typeof(datum_atom_skin_cap_pen));
    }

    // tg: code/modules/paperwork/pen.dm:252
    public virtual void reskin_dart_insert(datum_component_dart_insert insert_comp = null, object skin = null)
    {
        if (insert_comp == null) //You really shouldn't be sending this signal from anything other than a dart_insert component
            return;
        insert_comp.casing_overlay_icon_state = overlay_reskin[skin] as string;
        insert_comp.projectile_overlay_icon_state = Dm.text("[]_proj", overlay_reskin[skin]);
    }
}

/*
 * Sleepypens
 */
// tg: code/modules/paperwork/pen.dm:282
public partial class obj_item_pen_sleepy : obj_item_pen
{
    // tg: code/modules/paperwork/pen.dm:282
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        object dot = await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        if (!Dm.truthy(dot))
            return dot;
        datum_reagents pen_reagents = reagents as datum_reagents;
        if (!Dm.truthy(pen_reagents.total_volume))
            return dot;
        if (target_mob.reagents == null)
            return dot;
        await pen_reagents.trans_to(target_mob, pen_reagents.total_volume, transferred_by: user, methods: INJECT);
        return dot;
    }

    // tg: code/modules/paperwork/pen.dm:294
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(45, OPENCONTAINER);
        datum_reagents pen_reagents = reagents as datum_reagents;
        Waiting.NoSleep(pen_reagents.add_reagent(typeof(datum_reagent_toxin_chloralhydrate), 20));
        Waiting.NoSleep(pen_reagents.add_reagent(typeof(datum_reagent_toxin_mutetoxin), 15));
        Waiting.NoSleep(pen_reagents.add_reagent(typeof(datum_reagent_toxin_staminatoxin), 10));
        return dot;
    }

    // tg: code/modules/paperwork/pen.dm:301
    public override void on_inserted_into_dart(datum source = null, object dart = null, mob user = null, float? embedded = null)
    {
        base.on_inserted_into_dart(source, dart, user, embedded);
        obj_projectile proj = ((obj_item_ammo_casing)dart).loaded_projectile as obj_projectile;
        RegisterSignal(proj, COMSIG_PROJECTILE_SELF_ON_HIT, nameof(on_dart_hit));
    }

    // tg: code/modules/paperwork/pen.dm:306  (source, dart, proj, user)
    public override void on_removed_from_dart(datum source = null, object dart = null, object user = null, params object[] __extra)
    {
        object proj = user; // tg's third argument is the projectile
        base.on_removed_from_dart(source, dart, user, __extra);
        if (proj is obj_projectile projectile)
            UnregisterSignal(projectile, COMSIG_PROJECTILE_SELF_ON_HIT);
    }

    // tg: code/modules/paperwork/pen.dm:311
    public virtual void on_dart_hit(datum source = null, atom_movable firer = null, atom target = null, object angle = null, object hit_limb = null, object blocked = null)
    {
        mob_living_carbon carbon_target = target as mob_living_carbon;
        if (carbon_target == null || Dm.eq(blocked, 100))
            return;
        if (Dm.truthy(carbon_target.can_inject(target_zone: hit_limb)))
            Waiting.NoSleep(((datum_reagents)reagents).trans_to(carbon_target, ((datum_reagents)reagents).total_volume, transferred_by: firer as mob, methods: INJECT));
    }
}

/*
 * (Alan) Edaggers
 */
// tg: code/modules/paperwork/pen.dm:335
public partial class obj_item_pen_edagger : obj_item_pen
{
    // tg: code/modules/paperwork/pen.dm:357
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        alt_continuous = string_list(alt_continuous) as DmList;
        alt_simple = string_list(alt_simple) as DmList;
        Waiting.NoSleep(AddComponent(typeof(datum_component_alternative_sharpness), SHARP_POINTY, alt_continuous, alt_simple, -5, TRAIT_TRANSFORM_ACTIVE));
        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering),
            Dm.named("speed", 6 * SECONDS),
            Dm.named("butcher_sound", Dm.resource("sound/items/weapons/blade1.ogg"))));
        RegisterSignal(this, COMSIG_DETECTIVE_SCANNED, nameof(on_scan));
        return dot;
    }

    // tg: code/modules/paperwork/pen.dm:368
    public override async DmTask create_transform_component()
    {
        await AddComponent(typeof(datum_component_transforming),
            Dm.named("force_on", 18),
            Dm.named("throwforce_on", 35),
            Dm.named("throw_speed_on", 4),
            Dm.named("sharpness_on", SHARP_EDGED),
            Dm.named("w_class_on", WEIGHT_CLASS_NORMAL),
            Dm.named("inhand_icon_change", FALSE));
    }

    // tg: code/modules/paperwork/pen.dm:379
    public override void on_inserted_into_dart(datum source = null, object dart = null, mob user = null, float? embedded = null)
    {
        base.on_inserted_into_dart(source, dart, user, embedded);
        datum_component_transforming transform_comp = GetComponent(typeof(datum_component_transforming)) as datum_component_transforming;
        if (HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
            transform_comp.do_transform(this, user);
        obj_item_ammo_casing casing = (obj_item_ammo_casing)dart;
        RegisterSignal(casing.loaded_projectile as datum, COMSIG_PROJECTILE_FIRE, nameof(on_containing_dart_fired));
        RegisterSignal(casing.loaded_projectile as datum, COMSIG_PROJECTILE_ON_SPAWN_DROP, nameof(on_containing_dart_drop));
        RegisterSignal(casing.loaded_projectile as datum, COMSIG_PROJECTILE_ON_SPAWN_EMBEDDED, nameof(on_containing_dart_embedded));
    }

    // tg: code/modules/paperwork/pen.dm:388  (source, dart, projectile, user)
    public override void on_removed_from_dart(datum source = null, object dart = null, object user = null, params object[] __extra)
    {
        object projectile = user; // tg's third argument is the projectile
        base.on_removed_from_dart(source, dart, user, __extra);
        if (dart is obj_item_ammo_casing casing)
            UnregisterSignal(casing, Dm.list(COMSIG_ITEM_UNEMBEDDED, COMSIG_ITEM_FAILED_EMBED));
        if (projectile is obj_projectile proj)
            UnregisterSignal(proj, Dm.list(COMSIG_PROJECTILE_FIRE, COMSIG_PROJECTILE_ON_SPAWN_DROP, COMSIG_PROJECTILE_ON_SPAWN_EMBEDDED));
    }

    // tg: code/modules/paperwork/pen.dm:395
    public override async DmTask<object> get_dart_var_modifiers(obj_projectile projectile = null)
    {
        DmList dot = await base.get_dart_var_modifiers(projectile) as DmList;
        datum_component_transforming transform_comp = GetComponent(typeof(datum_component_transforming)) as datum_component_transforming;
        dot["damage"] = max(5, transform_comp.throwforce_on);
        dot["speed"] = max(0, Dm.num(transform_comp.throw_speed_on) - 3);
        datum_embedding data = dot["embedding"] as datum_embedding;
        data.embed_chance = 100;
        return dot;
    }

    // tg: code/modules/paperwork/pen.dm:403
    public virtual void on_containing_dart_fired(obj_projectile source = null)
    {
        playsound(source, Dm.resource("sound/items/weapons/saberon.ogg"), 5, TRUE);
        datum_component_transforming transform_comp = GetComponent(typeof(datum_component_transforming)) as datum_component_transforming;
        source.hitsound = transform_comp.hitsound_on;
        Waiting.NoSleep(source.set_light(light_range, light_power, light_color, l_on: TRUE));
    }

    // tg: code/modules/paperwork/pen.dm:410
    public virtual void on_containing_dart_drop(datum source = null, obj_item_ammo_casing new_casing = null)
    {
        playsound(new_casing, Dm.resource("sound/items/weapons/saberoff.ogg"), 5, TRUE);
    }

    // tg: code/modules/paperwork/pen.dm:414
    public virtual void on_containing_dart_embedded(datum source = null, obj_item_ammo_casing new_casing = null)
    {
        RegisterSignal(new_casing, COMSIG_ITEM_UNEMBEDDED, nameof(on_embedded_removed));
        RegisterSignal(new_casing, COMSIG_ITEM_FAILED_EMBED, nameof(on_containing_dart_failed_embed));
    }

    // tg: code/modules/paperwork/pen.dm:419
    public virtual void on_containing_dart_failed_embed(obj_item_ammo_casing source = null)
    {
        playsound(source, Dm.resource("sound/items/weapons/saberoff.ogg"), 5, TRUE);
        UnregisterSignal(source, Dm.list(COMSIG_ITEM_UNEMBEDDED, COMSIG_ITEM_FAILED_EMBED));
    }

    // tg: code/modules/paperwork/pen.dm:424
    public virtual void on_embedded_removed(obj_item_ammo_casing source = null, mob_living_carbon victim = null)
    {
        playsound(source, Dm.resource("sound/items/weapons/saberoff.ogg"), 5, TRUE);
        UnregisterSignal(source, Dm.list(COMSIG_ITEM_UNEMBEDDED, COMSIG_ITEM_FAILED_EMBED));
        victim.visible_message(
            message: span_warning(I18n.T("The blade of the [] retracts as \\the [] is removed from []!", hidden_name, source, victim)),
            self_message: span_warning(I18n.T("The blade of the [] retracts as \\the [] is removed from you!", hidden_name, source)),
            blind_message: span_warning(I18n.T("You hear an energy blade retract!")),
            vision_distance: 1);
    }

    // tg: code/modules/paperwork/pen.dm:435
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
            user.visible_message(span_suicide(I18n.T("[] forcefully rams the pen into their mouth!", user)));
        else
        {
            user.visible_message(span_suicide(I18n.T("[] is holding a pen up to their mouth! It looks like [] trying to commit suicide!", user, user.p_theyre())));
            await attack_self(user);
        }
        return BRUTELOSS;
    }

    /*
     * Signal proc for [COMSIG_TRANSFORMING_ON_TRANSFORM].
     *
     * Handles swapping their icon files to edagger related icon files -
     * as they're supposed to look like a normal pen.
     */
    // tg: code/modules/paperwork/pen.dm:449
    public override object on_transform(obj_item source = null, mob user = null, object active = null)
    {
        if (Dm.truthy(active))
        {
            name = hidden_name;
            desc = hidden_desc;
            icon_state = hidden_icon;
            inhand_icon_state = hidden_icon;
            lefthand_file = Dm.resource("icons/mob/inhands/weapons/swords_lefthand.dmi");
            righthand_file = Dm.resource("icons/mob/inhands/weapons/swords_righthand.dmi");
            set_embed(typeof(datum_embedding_edagger_active));
        }
        else
        {
            name = (string)Dm.initial(this, nameof(name));
            desc = (string)Dm.initial(this, nameof(desc));
            icon_state = (string)Dm.initial(this, nameof(icon_state));
            inhand_icon_state = (string)Dm.initial(this, nameof(inhand_icon_state));
            lefthand_file = Dm.initial(this, nameof(lefthand_file));
            righthand_file = Dm.initial(this, nameof(righthand_file));
            set_embed(embed_type);
        }

        if (user != null)
            balloon_alert(user, I18n.T("[] []", hidden_name, Dm.truthy(active) ? I18n.T("active") : I18n.T("concealed")));
        playsound(this, Dm.truthy(active) ? Dm.resource("sound/items/weapons/saberon.ogg") : Dm.resource("sound/items/weapons/saberoff.ogg"), 5, TRUE);
        Waiting.NoSleep(set_light_on(active));
        return COMPONENT_NO_DEFAULT_MESSAGE;
    }

    // tg: code/modules/paperwork/pen.dm:479
    public virtual void on_scan(datum source = null, mob user = null, datum_detective_scanner_log entry = null)
    {
        entry.add_data_entry(DETSCAN_CATEGORY_ILLEGAL, "Hard-light generator detected.");
    }
}

// tg: code/modules/paperwork/pen.dm:484
public partial class obj_item_pen_survival : obj_item_pen
{
    // tg: code/modules/paperwork/pen.dm:502
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_iron), 2), Dm.kv(typeof(datum_reagent_iodine), 1));
    }

    // tg: code/modules/paperwork/pen.dm:505
    public override void on_inserted_into_dart(datum source = null, object dart = null, mob user = null, float? embedded = null)
    {
        base.on_inserted_into_dart(source, dart, user, embedded);
        RegisterSignal(((obj_item_ammo_casing)dart).loaded_projectile as datum, COMSIG_PROJECTILE_SELF_ON_HIT, nameof(on_dart_hit));
    }

    // tg: code/modules/paperwork/pen.dm:509  (source, dart, proj, user)
    public override void on_removed_from_dart(datum source = null, object dart = null, object user = null, params object[] __extra)
    {
        object proj = user; // tg's third argument is the projectile
        base.on_removed_from_dart(source, dart, user, __extra);
        if (proj is obj_projectile projectile)
            UnregisterSignal(projectile, COMSIG_PROJECTILE_SELF_ON_HIT);
    }

    // tg: code/modules/paperwork/pen.dm:514
    public virtual async DmTask on_dart_hit(obj_projectile source = null, atom_movable firer = null, atom target = null)
    {
        turf target_turf = get_turf(target);
        if (target_turf == null)
            target_turf = get_turf(this);
        if (ismineralturf(target_turf))
        {
            turf_closed_mineral mineral_turf = (turf_closed_mineral)target_turf;
            await mineral_turf.gets_drilled(firer as mob, 1);
        }
    }
}

// screwdriver pen!
// tg: code/modules/paperwork/pen.dm:532
public partial class obj_item_pen_screwdriver : obj_item_pen
{
    // tg: code/modules/paperwork/pen.dm:538
    public override object get_all_tool_behaviours()
    {
        return Dm.list(TOOL_SCREWDRIVER);
    }

    // tg: code/modules/paperwork/pen.dm:541
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        return dot;
    }

    // tg: code/modules/paperwork/pen.dm:545
    public override async DmTask create_transform_component()
    {
        await AddComponent(typeof(datum_component_transforming),
            Dm.named("throwforce_on", 5),
            Dm.named("w_class_on", WEIGHT_CLASS_SMALL),
            Dm.named("sharpness_on", TRUE),
            Dm.named("inhand_icon_change", FALSE));
    }

    // tg: code/modules/paperwork/pen.dm:554
    public override object on_transform(obj_item source = null, mob user = null, object active = null)
    {
        if (user != null)
            balloon_alert(user, Dm.truthy(active) ? I18n.T("extended") : I18n.T("retracted"));
        playsound(this, Dm.resource("sound/items/weapons/batonextend.ogg"), 50, TRUE);

        if (!Dm.truthy(active))
        {
            tool_behaviour = Dm.initial(this, nameof(tool_behaviour)) as string;
            RemoveElement(typeof(datum_element_eyestab));
        }
        else
        {
            tool_behaviour = TOOL_SCREWDRIVER;
            Waiting.NoSleep(AddElement(typeof(datum_element_eyestab)));
        }

        update_appearance(UPDATE_ICON);
        return COMPONENT_NO_DEFAULT_MESSAGE;
    }

    // tg: code/modules/paperwork/pen.dm:569
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][]", Dm.initial(this, nameof(icon_state)), HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE) ? "_out" : null);
        inhand_icon_state = (string)Dm.initial(this, nameof(inhand_icon_state)); //since transforming component switches the icon.
        return dot;
    }
}

//The Security holopen
// tg: code/modules/paperwork/pen.dm:574
public partial class obj_item_pen_red_security : obj_item_pen_red
{
    // tg: code/modules/paperwork/pen.dm:581
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("To initiate the surrender prompt, simply click on an individual within your proximity.")));
        return dot;
    }

    //Code from the medical penlight
    // tg: code/modules/paperwork/pen.dm:586
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!(holosign_cooldown <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline
        {
            balloon_alert(user, I18n.T("not ready!"));
            return ITEM_INTERACT_BLOCKING;
        }

        turf target_turf = get_turf(interacting_with);
        mob_living living_target = Dm.locate<mob_living>(target_turf);

        if (living_target == null || living_target == user)
            return ITEM_INTERACT_BLOCKING;

        await living_target.apply_status_effect(typeof(datum_status_effect_surrender_timed));
        to_chat(living_target, span_userdanger(I18n.T("[] requests your immediate surrender! You are given 30 seconds to comply!", user)));
        Dm.@new<obj_effect_temp_visual_security_holosign>(target_turf, user); //produce a holographic glow
        holosign_cooldown = world.time + 30 * SECONDS; // COOLDOWN_START
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/paperwork/pen.dm:603
public partial class obj_effect_temp_visual_security_holosign : obj_effect_temp_visual
{
    // tg: code/modules/paperwork/pen.dm:609
    [DmParams("mapload", "creator")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object creator = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        playsound(loc as atom, Dm.resource("sound/machines/chime.ogg"), 50, FALSE); //make some noise!
        if (Dm.truthy(creator))
            visible_message(span_danger(I18n.T("[] created a security hologram!", creator)));
        return dot;
    }
}

// Skins for captain's fountain pen
// tg: code/modules/paperwork/pen.dm:185
public partial class datum_atom_skin_cap_pen : datum_atom_skin
{
    // tg: code/modules/paperwork/pen.dm:188
    public override void apply(atom apply_to = null, mob user = null)
    {
        base.apply(apply_to, user);
        apply_to.desc = I18n.T("It's an expensive [] fountain pen. The nib is quite sharp.", preview_name);
        apply_to.update_desc();
    }

    // tg: code/modules/paperwork/pen.dm:193
    public override void clear_skin(atom clear_from = null, mob user = null)
    {
        base.clear_skin(clear_from, user);
        clear_from.desc = Dm.initial(clear_from.type, (atom a) => a.desc);
    }
}
