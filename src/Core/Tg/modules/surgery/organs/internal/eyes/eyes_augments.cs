// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/internal/eyes/eyes_augments.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI3 (D326): the robotic eyes' EMP (the exosuit fabricator prints the basic ones). Gaps OG1 (D339, D341): the rest of the file,
// the flashlight, shielded, high luminosity, maintenance adapted eyes and the tactical IFF visor. The var blocks are generated (D111).

// tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:1
public partial class obj_item_organ_eyes_robotic : obj_item_organ_eyes
{
    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:11
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)) || owner == null)
            return dot;
        if (prob(100 / Dm.num(severity)))
            return dot;
        to_chat(owner, span_warning(I18n.T("Static obfuscates your vision!")));
        await owner.flash_act(visual: 1);
        return dot;
    }
}

// tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:20
public partial class obj_item_organ_eyes_robotic_basic : obj_item_organ_eyes_robotic
{
    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:31
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        if (prob(10 * Dm.num(severity)))
        {
            await apply_organ_damage(20 * Dm.num(severity));
            to_chat(owner, span_warning(I18n.T("Your eyes start to fizzle in their sockets!")));
            do_sparks(2, TRUE, owner);
            await owner.emote("scream");
        }
        return dot;
    }
}

// tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:69 (gaps OG1)
public partial class obj_item_organ_eyes_robotic_flashlight : obj_item_organ_eyes_robotic
{
    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:83
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), EMP_PROTECT_ALL));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:87
    [DmParams("victim")]
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        mob_living_carbon victim = organ_owner;
        if (eye == null)
            eye = Dm.@new<obj_item_flashlight_eyelight>();
        await eye.set_light_on(TRUE);
        await eye.forceMove(victim);
        eye.update_brightness();
        await victim.become_blind(FLASHLIGHT_EYES);
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:96
    [DmParams("victim")]
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        mob_living_carbon victim = organ_owner as mob_living_carbon;
        await eye.set_light_on(FALSE);
        eye.update_brightness();
        await eye.forceMove(this);
        victim.cure_blind(FLASHLIGHT_EYES);
    }
}

// Welding shield implant
// tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:105 (gaps OG1)
public partial class obj_item_organ_eyes_robotic_shield : obj_item_organ_eyes_robotic
{
    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:116
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), EMP_PROTECT_ALL));
        return dot;
    }
}

// tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:127 (gaps OG1)
public partial class obj_item_organ_eyes_robotic_glow : obj_item_organ_eyes_robotic
{
    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:121-125  (file-local defines, #undef after the glowing eyes)
    private const float MATCH_LIGHT_COLOR = 1;
    private const float USE_CUSTOM_COLOR = 0;
    private const float UPDATE_LIGHT = 0;
    private const float UPDATE_EYES_LEFT = 1;
    private const float UPDATE_EYES_RIGHT = 2;

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:149
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        eye = Dm.@new<obj_item_flashlight_eyelight_glow>();
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:153
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        Waiting.NoSleep(deactivate(close_ui: TRUE));
        QDEL_NULL(ref eye);
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:158
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (!Dm.truthy(eye.light_on) || Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        await deactivate(close_ui: TRUE);
        return dot;
    }

    /// Set the initial color of the eyes on insert to be the mob's previous eye color.
    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:165
    [DmParams("eye_recipient", "special", "movement_flags")]
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        special ??= FALSE;
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        left_eye_color_string = eye_color_left;
        right_eye_color_string = eye_color_right;
        await update_mob_eye_color(organ_owner);
        await deactivate(close_ui: TRUE);
        await eye.forceMove(organ_owner);
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:173
    [DmParams("eye_owner")]
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await deactivate(organ_owner as mob_living_carbon, close_ui: TRUE);
        if (!QDELETED(eye))
            await eye.forceMove(this);
        await base.on_mob_remove(organ_owner, special, movement_flags);
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:179
    public override object ui_state(mob user = null)
    {
        return GLOB.default_state;
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:182
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (!QDELETED(owner))
        {
            if (owner == user)
                return Math.Min(
                    Dm.num(ui_status_user_is_abled(user, this)),
                    Dm.num(ui_status_only_living(user)));
            else
                return UI_CLOSE;
        }
        return await base.ui_status(user, state);
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:192
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "HighLuminosityEyesMenu");
            ui.autoupdate = FALSE;
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:199
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();

        data["eyeColor"] = Dm.list(
            Dm.kv("mode", eye_color_mode),
            Dm.kv("hasOwner", owner != null ? TRUE : FALSE),
            Dm.kv("left", left_eye_color_string),
            Dm.kv("right", right_eye_color_string));
        data["lightColor"] = light_color_string;
        data["range"] = eye.light_range;

        return data;
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:213
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        string sw__1 = action as string;
        switch (sw__1)
        {
            case "set_range":
            {
                object new_range = @params["new_range"];
                set_beam_range(new_range);
                return TRUE;
            }
            case "pick_color":
            {
                object new_color = await tgui_color_picker(
                    usr,
                    I18n.T("Choose eye color color:"),
                    I18n.T("High Luminosity Eyes Menu"),
                    light_color_string);
                if (Dm.truthy(new_color))
                {
                    object to_update = @params["to_update"];
                    await set_beam_color(new_color, Dm.num_or_null(to_update));
                    return TRUE;
                }
                break;
            }
            case "enter_color":
            {
                object new_color = LOWER_TEXT(@params["new_color"]);
                object to_update = @params["to_update"];
                await set_beam_color(new_color, Dm.num_or_null(to_update), sanitize: TRUE);
                return TRUE;
            }
            case "random_color":
            {
                object to_update = @params["to_update"];
                await randomize_color(Dm.num_or_null(to_update));
                return TRUE;
            }
            case "toggle_eye_color":
                await toggle_eye_color_mode();
                return TRUE;
        }
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:247
    [DmParams("user", "action")]
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        if (istype(actiontype, typeof(datum_action_item_action_organ_action_toggle)))
            await toggle_active();
        else if (istype(actiontype, typeof(datum_action_item_action_organ_action_use)))
            await ui_interact(user);
        return null;
    }

    /**
     * Activates the light
     *
     * Turns on the attached flashlight object, updates the mob overlay to be added.
     */
    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:258
    public virtual async DmTask activate()
    {
        if (Dm.truthy(eye.light_range))
            await eye.set_light_on(TRUE);
        else
            eye.light_on = TRUE; // at range 0 we are just going to make the eyes glow emissively, no light overlay
        await update_mob_eye_color();
    }

    /**
     * Deactivates the light
     *
     * Turns off the attached flashlight object, closes UIs, updates the mob overlay to be removed.
     * Arguments:
     * * mob/living/carbon/eye_owner - the mob who the eyes belong to
     * * close_ui - whether or not to close the ui
     */
    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:273
    public virtual async DmTask deactivate(mob_living_carbon eye_owner = null, float? close_ui = null)
    {
        eye_owner ??= owner;
        close_ui ??= FALSE;
        if (Dm.truthy(close_ui))
            SStgui?.close_uis(this); // rules 10.6: a Destroy in a world with no SStgui
        await eye.set_light_on(FALSE);
        await update_mob_eye_color(eye_owner);
    }

    /**
     * Randomizes the light color
     *
     * Picks a random color and sets the beam color to that
     * Arguments:
     * * to_update - whether we are setting the color for the light beam itself, or the individual eyes
     */
    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:286
    public virtual async DmTask randomize_color(float? to_update = null)
    {
        to_update ??= UPDATE_LIGHT;
        string new_color = "#";
        for (float i = 1; i <= 3; i++)
            new_color += num2hex(rand(0, 255), 2);
        await set_beam_color(new_color, to_update);
    }

    /**
     * Setter function for the light's range
     *
     * Sets the light range of the attached flashlight object
     * Includes some 'unique' logic to accomodate for some quirks of the lighting system
     * Arguments:
     * * new_range - the new range to set
     */
    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:300
    public virtual void set_beam_range(object new_range = null)
    {
        object old_light_range = eye.light_range;
        if (Dm.num(old_light_range) == 0 && Dm.num(new_range) > 0 && Dm.truthy(eye.light_on)) // turn bring back the light overlay if we were previously at 0 (aka emissive eyes only)
        {
            eye.light_on = FALSE; // this is stupid, but this has to be FALSE for set_light_on() to work.
            Waiting.NoSleep(eye.set_light_on(TRUE));
        }
        eye.set_light_range(clamp(Dm.num(new_range), 0, max_light_beam_distance));
    }

    /**
     * Setter function for the light's color
     *
     * Sets the light color of the attached flashlight object. Sets the eye color vars of this eye organ as well and then updates the mob's eye color.
     * Arguments:
     * * newcolor - the new color hex string to set
     * * to_update - whether we are setting the color for the light beam itself, or the individual eyes
     * * sanitize - whether the hex string should be sanitized
     */
    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:316
    public virtual async DmTask set_beam_color(object newcolor = null, float? to_update = null, float? sanitize = null)
    {
        to_update ??= UPDATE_LIGHT;
        sanitize ??= FALSE;
        object newcolor_string = null;
        if (Dm.truthy(sanitize))
            newcolor_string = sanitize_hexcolor(newcolor);
        else
            newcolor_string = newcolor;
        float sw__1 = Dm.num(to_update);
        if (sw__1 == UPDATE_LIGHT)
        {
            light_color_string = Dm.str(newcolor_string);
            eye.set_light_color(newcolor_string);
        }
        else if (sw__1 == UPDATE_EYES_LEFT)
            left_eye_color_string = newcolor_string;
        else if (sw__1 == UPDATE_EYES_RIGHT)
            right_eye_color_string = newcolor_string;

        await update_mob_eye_color();
    }

    /**
     * Toggle the attached flashlight object on or off
     */
    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:336
    public virtual async DmTask toggle_active()
    {
        if (Dm.truthy(eye.light_on))
            await deactivate();
        else
            await activate();
    }

    /**
     * Toggles for the eye color mode
     *
     * Toggles the eye color mode on or off and then calls an update on the mob's eye color
     */
    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:347
    public virtual async DmTask toggle_eye_color_mode()
    {
        eye_color_mode = Dm.num(!Dm.truthy(eye_color_mode));
        await update_mob_eye_color();
    }

    /**
     * Updates the mob eye color
     *
     * Updates the eye color to reflect on the mob's body if it's possible to do so
     * Arguments:
     * * mob/living/carbon/eye_owner - the mob to update the eye color appearance of
     */
    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:358
    public virtual async DmTask update_mob_eye_color(mob_living_carbon eye_owner = null)
    {
        eye_owner ??= owner;
        float sw__1 = eye_color_mode;
        if (sw__1 == MATCH_LIGHT_COLOR)
        {
            eye_color_left = light_color_string;
            eye_color_right = light_color_string;
        }
        else if (sw__1 == USE_CUSTOM_COLOR)
        {
            eye_color_left = left_eye_color_string;
            eye_color_right = right_eye_color_string;
        }

        if (QDELETED(eye_owner) || !ishuman(eye_owner)) //Other carbon mobs don't have eye color.
            return;

        obj_item_bodypart_head head = eye_owner.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head; //if we have eyes we definently have a head anyway
        float previous_flags = head.head_flags;
        head.head_flags = Dm.bitor(head.head_flags, HEAD_EYECOLOR);

        ///enabling and disabling the TRAIT_LUMINESCENT_EYES trait already calls handle_eyes(), in that case, let's skip that call
        bool skip_call = false;
        if (!Dm.truthy(eye.light_on))
        {
            eye_icon_state = Dm.initial(this, (obj_item_organ_eyes_robotic_glow p) => p.eye_icon_state);
            skip_call = HAS_TRAIT_FROM_ONLY(eye_owner, TRAIT_LUMINESCENT_EYES, REF(this));
            remove_organ_trait(TRAIT_LUMINESCENT_EYES);
        }
        else
        {
            skip_call = !HAS_TRAIT(eye_owner, TRAIT_LUMINESCENT_EYES);
            add_organ_trait(TRAIT_LUMINESCENT_EYES);
            eye_icon_state = base_eye_state;
        }

        if (!skip_call && ishuman(eye_owner))
        {
            mob_living_carbon_human humie = eye_owner as mob_living_carbon_human;
            await humie.update_eyes();
        }

        head.head_flags = previous_flags;
    }
}

// Chaplain's special boy
// tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:466 (gaps OG1)
public partial class obj_item_organ_eyes_night_vision_maintenance_adapted : obj_item_organ_eyes_night_vision
{
    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:480
    [DmParams("viewer", "examtool")]
    public override async DmTask<object> penlight_examine(mob_living viewer = null, params object[] __extra)
    {
        obj_item examtool = Dm.arg(__extra, 0) as obj_item;
        if (!Dm.truthy(owner.is_blind()))
        {
            to_chat(owner, span_danger(I18n.T("Your eyes sizzle agonizingly as light is shone on them!")));
            await apply_organ_damage(20 * Dm.num(examtool.light_power)); //that's 0.5 lightpower for a penlight, so one penlight shining is equivalent to two seconds in a lit area
        }
        return span_danger(I18n.T("[] eyes []", owner.p_Their(), penlight_message));
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:486
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (Dm.num(owner.get_eye_protection()) <= FLASH_PROTECTION_SENSITIVE && !Dm.truthy(owner.is_blind()) && isturf(owner.loc) && Dm.truthy(owner.has_light_nearby(light_amount: 0.5f))) //we allow a little more than usual so we can produce light from the adapted eyes
        {
            to_chat(owner, span_danger(I18n.T("Your eyes! They burn in the light!")));
            await apply_organ_damage(10); //blind quickly
            playsound(owner, Dm.resource("sound/machines/grill/grillsizzle.ogg"), 50);
        }
        else
            await apply_organ_damage(-10); //heal quickly
        await base.on_life(seconds_per_tick);
    }
}

// tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:510 (gaps OG1)
public partial class obj_item_organ_eyes_robotic_tacvisor : obj_item_organ_eyes_robotic
{
    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:496-509  (file-local defines, #undef at the end of the file)
    internal const string VISOR_DISPLAY_CROSS = "Cross";
    internal const string VISOR_DISPLAY_EYES = "Eyes";
    internal const string VISOR_DISPLAY_LINE = "Line";

    internal const float IFF_HOSTILE = 1;
    internal const float IFF_NEUTRAL = 2;
    internal const float IFF_FRIENDLY = 3;

    internal const string IFF_FACTION_NONE = "None";
    internal const string IFF_FACTION_SYNDICATE = "Syndicate";
    internal const string IFF_FACTION_CREW = "Crew";
    internal const string IFF_FACTION_SEC_COMMAND = "Security & Command";
    internal const string IFF_FACTION_CENTCOM = "CentCom";
    internal const string IFF_FACTION_EVERYONE = "Non-Allies";

    /// Valid options for friendly factions
    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:545  var/static/list/valid_friendly_factions (a type static, rules 10.6)
    private static DmList valid_friendly_factions => TgState.Current.obj_item_organ_eyes_robotic_tacvisor__valid_friendly_factions ??= Dm.list(
        IFF_FACTION_NONE,
        IFF_FACTION_CREW,
        IFF_FACTION_SEC_COMMAND,
        IFF_FACTION_CENTCOM,
        IFF_FACTION_SYNDICATE);
    /// Valid options for hostile factions
    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:553  var/static/list/valid_hostile_factions
    private static DmList valid_hostile_factions => TgState.Current.obj_item_organ_eyes_robotic_tacvisor__valid_hostile_factions ??= Dm.list(
        IFF_FACTION_NONE,
        IFF_FACTION_CREW,
        IFF_FACTION_SEC_COMMAND,
        IFF_FACTION_CENTCOM,
        IFF_FACTION_SYNDICATE,
        IFF_FACTION_EVERYONE);
    /// Threat flags and their tooltips
    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:562  var/static/list/threat_flag_options
    private static DmList threat_flag_options => TgState.Current.obj_item_organ_eyes_robotic_tacvisor__threat_flag_options ??= Dm.list(
        Dm.kv("Valid ID", JUDGE_IDCHECK),
        Dm.kv("Weapon Permit", JUDGE_WEAPONCHECK),
        Dm.kv("Security Status", JUDGE_RECORDCHECK));

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:568
    [DmParams("limb")]
    public override object generate_body_overlay(obj_item_bodypart_head my_head = null)
    {
        obj_item_bodypart_head limb = my_head;
        mutable_appearance visor_overlay = mutable_appearance(eye_icon, eye_icon_state, -EYES_LAYER) as mutable_appearance;
        DmList eye_overlays = Dm.list(visor_overlay);
        mob_living_carbon_human parent = limb?.owner as mob_living_carbon_human; // tg reads limb.owner before its null check (a runtime for no limb)
        if (parent != null && !IS_DEAD_OR_FAKING(parent) && !IS_UNCONSCIOUS(parent))
        {
            mutable_appearance display_overlay = mutable_appearance(eye_icon, Dm.text("[]_[]", eye_icon_state, LOWER_TEXT(visor_display)), -EYES_LAYER) as mutable_appearance;
            Dm.append(eye_overlays, display_overlay);
            // if(!(parent.obscured_slots & HIDEEYES)) eye_overlays += emissive_appearance(...): DEVIATION(DV12): the emissive plane is not
            // drawn (D24); the display overlay above shows the screen.
        }

        if (limb == null)
            return eye_overlays;

        obj_item_bodypart_head head = limb;
        if (head?.worn_face_offset != null)
        {
            foreach (object overlay in Dm.each(eye_overlays))
                head?.worn_face_offset.apply_offset(overlay as image);
        }

        return eye_overlays;
    }

    // Hides and mutes all people on the screen
    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:589
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        balloon_alert(user, I18n.T("perceptual scanners overriden"));
        return TRUE;
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:596
    [DmParams("receiver", "special", "movement_flags")]
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        mob_living_carbon receiver = organ_owner;
        receiver.add_client_colour(typeof(datum_client_colour_tacvisor), REF(this));
        await receiver.apply_status_effect(typeof(datum_status_effect_grouped_see_no_names), REF(this));
        proximity_monitor = Dm.@new<datum_proximity_monitor_tacvisor>(receiver, 9);
        proximity_monitor.owner = this;

        RegisterSignal(receiver, COMSIG_MOB_CLIENT_LOGIN, nameof(on_login));
        RegisterSignal(receiver, COMSIG_MOVABLE_PRE_HEAR, nameof(on_hear));
        RegisterSignal(receiver, COMSIG_MOB_EXAMINING, nameof(on_examine));
        if (receiver.client != null)
            create_illusions(receiver);
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:609
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        mob_living_carbon organ_owner__carbon = organ_owner as mob_living_carbon;
        UnregisterSignal(organ_owner__carbon, Dm.list(COMSIG_MOB_CLIENT_LOGIN, COMSIG_MOVABLE_PRE_HEAR, COMSIG_MOB_EXAMINING));
        organ_owner__carbon.remove_client_colour(REF(this));
        organ_owner__carbon.remove_status_effect(typeof(datum_status_effect_grouped_see_no_names), REF(this));
        QDEL_NULL(ref proximity_monitor);
        if (organ_owner__carbon.client != null)
            Dm.remove(organ_owner__carbon.client.images, assoc_to_values(mob_overlays));
        foreach (object thing in Dm.each(mob_overlays))
        {
            UnregisterSignal(thing as datum, Dm.list(
                COMSIG_MOVABLE_Z_CHANGED,
                COMSIG_QDELETING,
                COMSIG_ATOM_UPDATE_APPEARANCE,
                COMSIG_LIVING_POST_UPDATE_TRANSFORM,
                COMSIG_MOB_EQUIPPED_ITEM,
                COMSIG_MOB_UNEQUIPPED_ITEM,
                COMSIG_LIVING_UPDATE_OFFSETS));
        }
        mob_overlays.Cut();
        direct_view_tracking.Cut();
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:629
    [SignalHandler]
    public virtual object on_hear(datum source = null, DmList hearing_args = null)
    {
        if (hearing_args[HEARING_SPEAKER] == owner || !isliving(hearing_args[HEARING_SPEAKER]))
            return null;

        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return COMSIG_MOVABLE_CANCEL_HEARING;

        DmList message_mods = hearing_args[HEARING_MESSAGE_MODE] as DmList;
        message_mods[MODE_SPEAKER_NAME_OVERRIDE] = "Unknown";
        return null;
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:641
    [SignalHandler]
    public virtual void on_login(mob_living source = null, client user_client = null)
    {
        if (!Dm.truthy(length(mob_overlays)))
            create_illusions(source);
        else
            Dm.combine(user_client.images, assoc_to_values(mob_overlays));
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:649
    public virtual void create_illusions(mob_living user = null)
    {
        foreach (object target__it in Dm.each(Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) ? GLOB.mob_living_list : GLOB.carbon_list))
        {
            mob_living target = target__it as mob_living;
            if (target == user)
                continue;

            if (get_dist(user, target) <= Dm.num(proximity_monitor.current_range))
                on_entered(target);
            else
                refresh_overlay(target);
        }
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:659
    [SignalHandler]
    public virtual void on_mob_delete(mob_living source = null)
    {
        if (owner?.client != null)
            Dm.remove(owner.client.images, mob_overlays[source]);
        Dm.remove(mob_overlays, source);
        Dm.remove(direct_view_tracking, source);
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:665
    [SignalHandler]
    public virtual void refresh_overlay(mob_living source = null)
    {
        if (Dm.truthy(mob_overlays[source]))
        {
            if (owner?.client != null)
                Dm.remove(owner.client.images, mob_overlays[source]);
        }
        else
        {
            RegisterSignal(source, COMSIG_QDELETING, nameof(on_mob_delete));
            RegisterSignal(source, COMSIG_ATOM_UPDATE_APPEARANCE, nameof(refresh_overlay));
            RegisterSignal(source, COMSIG_MOVABLE_Z_CHANGED, nameof(on_z_change));
            // Lying down/being pushed
            RegisterSignal(source, COMSIG_LIVING_POST_UPDATE_TRANSFORM, nameof(refresh_overlay));
            RegisterSignal(source, COMSIG_LIVING_UPDATE_OFFSETS, nameof(refresh_overlay));
        }

        mob_overlays[source] = make_overlay(source);
        if (owner?.client != null)
            Dm.combine(owner.client.images, mob_overlays[source]);
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:681
    [SignalHandler]
    public virtual void on_z_change(mob_living source = null)
    {
        image overlay = mob_overlays[source] as image;
        SET_PLANE_EXPLICIT(overlay, ABOVE_GAME_PLANE, source);
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:686
    public virtual object make_overlay(mob_living target = null)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            image overlay_image__1 = image(mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "nothing"), target);
            overlay_image__1.name = "Unknown";
            overlay_image__1.@override = TRUE;
            overlay_image__1.mouse_opacity = MOUSE_OPACITY_TRANSPARENT;
            SET_PLANE_EXPLICIT(overlay_image__1, ABOVE_GAME_PLANE, target);
            return overlay_image__1;
        }

        mutable_appearance appearance_copy = Dm.@new<mutable_appearance>(target.appearance);
        appearance_copy.appearance_flags = Dm.bitor(appearance_copy.appearance_flags, Dm.bitor(KEEP_APART, KEEP_TOGETHER));
        string outline_color = "#FFD500CC";
        float sw__1 = Dm.num(get_iff_signature(target));
        if (sw__1 == IFF_FRIENDLY)
            outline_color = "#0099FFCC";
        else if (sw__1 == IFF_HOSTILE)
            outline_color = "#FF0022CC";
        appearance_copy.add_filter("target_lock_outline", 2, outline_filter(1, outline_color) as DmList);
        mutable_appearance static_effect = mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "static_base") as mutable_appearance;
        static_effect.color = "#373642";
        static_effect.blend_mode = BLEND_INSET_OVERLAY;
        Dm.append(appearance_copy.overlays, static_effect);
        appearance_copy.@override = TRUE;
        image overlay_image = image(appearance_copy, target);
        overlay_image.name = "Unknown";
        overlay_image.@override = TRUE;
        SET_PLANE_EXPLICIT(overlay_image, ABOVE_GAME_PLANE, target);
        return overlay_image;
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:715
    public virtual object get_iff_signature(mob_living target = null)
    {
        object dot = IFF_NEUTRAL;
        if (hostile_faction == IFF_FACTION_EVERYONE)
            dot = IFF_HOSTILE;

        if (!istype(target, typeof(mob_living)))
            return dot;

        // This hasn't been used for like, over a decade, but by god will I make it great again
        string lasercolor = laser_tag_colour();

        if (Dm.truthy(threat_flags) && Dm.num(target.assess_threat(threat_flags, lasercolor)) >= THREAT_ASSESS_DANGEROUS)
            return IFF_HOSTILE;

        obj_item_card_id idcard = target.get_idcard() as obj_item_card_id;
        if (!istype(idcard, typeof(obj_item_card_id)))
            return dot;

        // Ignores RETA access so we directly access access instead of using the wrapper
        if (Dm.@in(ACCESS_SYNDICATE, idcard.access))
        {
            if (hostile_faction == IFF_FACTION_SYNDICATE)
                return IFF_HOSTILE;
            if (friendly_faction == IFF_FACTION_SYNDICATE)
                return IFF_FRIENDLY;
        }

        if (istype(idcard.trim, typeof(datum_id_trim_job)))
        {
            // Cham cards get a pass
            if (hostile_faction == IFF_FACTION_CREW && Dm.num((idcard.trim as datum_id_trim)?.threat_modifier) >= 0)
                return IFF_HOSTILE;
            if (friendly_faction == IFF_FACTION_CREW)
                return IFF_FRIENDLY;
        }

        if (Dm.@in(ACCESS_COMMAND, idcard.access) || Dm.@in(ACCESS_SECURITY, idcard.access))
        {
            if (hostile_faction == IFF_FACTION_SEC_COMMAND)
                return IFF_HOSTILE;
            if (friendly_faction == IFF_FACTION_SEC_COMMAND)
                return IFF_FRIENDLY;
        }

        if (Dm.@in(ACCESS_CENT_GENERAL, idcard.access))
        {
            if (hostile_faction == IFF_FACTION_CENTCOM)
                return IFF_HOSTILE;
            if (friendly_faction == IFF_FACTION_CENTCOM)
                return IFF_FRIENDLY;
        }

        return dot;
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:725-728 and :776-779  the laser tag colour check, written twice in tg
    // (get_iff_signature and on_examine), kept as one helper with tg's lines (rules 2.5)
    private string laser_tag_colour()
    {
        if (istype(owner.get_item_by_slot(ITEM_SLOT_OCLOTHING), typeof(obj_item_clothing_suit_redtag)) || istype(owner.get_item_by_slot(ITEM_SLOT_BELT), typeof(obj_item_gun_energy_laser_redtag)) || Dm.truthy(owner.is_holding_item_of_type(typeof(obj_item_gun_energy_laser_redtag))))
            return "r";
        else if (istype(owner.get_item_by_slot(ITEM_SLOT_OCLOTHING), typeof(obj_item_clothing_suit_bluetag)) || istype(owner.get_item_by_slot(ITEM_SLOT_BELT), typeof(obj_item_gun_energy_laser_bluetag)) || Dm.truthy(owner.is_holding_item_of_type(typeof(obj_item_gun_energy_laser_bluetag))))
            return "b";
        return null;
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:765
    [SignalHandler]
    public virtual void on_examine(mob source = null, atom target = null, DmList examine_strings = null, DmList examine_overrides = null)
    {
        if (target == owner || !iscarbon(target) && !(isliving(target) && Dm.truthy(Dm.bitand(obj_flags, EMAGGED))))
            return;

        DmList override_strings = Dm.list(span_warning(I18n.T("You're struggling to make out any details...")));

        if (!Dm.truthy(threat_flags) && !Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            examine_overrides[EXAMINE_OVERRIDE_PRIORITY_IFF] = override_strings;
            return;
        }

        string lasercolor = laser_tag_colour();
        mob_living victim = target as mob_living;

        float threat_level = Dm.num(victim.assess_threat(threat_flags, lasercolor));
        if (threat_level >= THREAT_ASSESS_MAXIMUM)
            Dm.append(override_strings, span_boldwarning(I18n.T("Assessed threat level of []! Extreme danger of criminal activity!", threat_level)));
        else if (threat_level >= THREAT_ASSESS_DANGEROUS)
            Dm.append(override_strings, span_warning(I18n.T("Assessed threat level of []. Criminal scum detected!", threat_level)));
        else if (threat_level >= 1)
            Dm.append(override_strings, span_notice(I18n.T("Assessed threat level of []. Probably not dangerous... yet.", threat_level)));
        else
            Dm.append(override_strings, span_notice(I18n.T("Seems to be a trustworthy individual.")));

        examine_overrides[EXAMINE_OVERRIDE_PRIORITY_IFF] = override_strings;
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:797
    public override object ui_state(mob user = null)
    {
        return GLOB.default_state;
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:800
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (!QDELETED(owner))
        {
            if (owner == user && Dm.truthy(user_controls))
                return Math.Min(
                    Dm.num(ui_status_user_is_abled(user, this)),
                    Dm.num(ui_status_only_living(user)));
            return UI_CLOSE;
        }
        return await base.ui_status(user, state);
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:810
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "TacVisorEyesMenu");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:816
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        data["friendlyFaction"] = friendly_faction;
        data["hostileFaction"] = hostile_faction;
        data["visorDisplay"] = visor_display;
        data["threatFlags"] = threat_flags;
        return data;
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:824
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        data["validFriendlyFactions"] = valid_friendly_factions;
        data["validHostileFactions"] = valid_hostile_factions;
        data["visorOptions"] = Dm.list(VISOR_DISPLAY_CROSS, VISOR_DISPLAY_EYES, VISOR_DISPLAY_LINE);
        data["threatOptions"] = threat_flag_options;
        return data;
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:832
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        string sw__1 = action as string;
        switch (sw__1)
        {
            case "set_friendly":
            {
                object chosen_faction = @params["faction"];
                if (!Dm.@in(chosen_faction, valid_friendly_factions))
                    chosen_faction = IFF_FACTION_NONE;
                friendly_faction = Dm.str(chosen_faction);
                update_mob_overlays();
                break;
            }
            case "set_hostile":
            {
                object chosen_faction = @params["faction"];
                if (!Dm.@in(chosen_faction, valid_hostile_factions))
                    chosen_faction = IFF_FACTION_NONE;
                hostile_faction = Dm.str(chosen_faction);
                update_mob_overlays();
                break;
            }
            case "set_threat_flags":
                threat_flags = Dm.num(text2num(@params["threat_flags"]));
                update_mob_overlays();
                break;
            case "set_display":
                visor_display = @params["display"] as string;
                if (visor_display != VISOR_DISPLAY_EYES && visor_display != VISOR_DISPLAY_LINE && visor_display != VISOR_DISPLAY_CROSS)
                    visor_display = VISOR_DISPLAY_CROSS;
                if (owner != null)
                    await owner.update_body();
                break;
        }
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:862
    public virtual void update_mob_overlays()
    {
        foreach (object target in Dm.each(direct_view_tracking))
            refresh_overlay(target as mob_living);
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:866
    [DmParams("user", "action")]
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        if (istype(actiontype, typeof(datum_action_item_action_organ_action_use)) && Dm.truthy(user_controls))
            await ui_interact(user);
        return null;
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:870
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        await ui_interact(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:874
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        user_controls = Dm.num(!Dm.truthy(user_controls));
        balloon_alert(user, I18n.T("user controls []", Dm.truthy(user_controls) ? I18n.T("enabled") : I18n.T("disabled")));
        if (Dm.truthy(user_controls))
            await add_item_action(typeof(datum_action_item_action_organ_action_use));
        else
        {
            foreach (object action in Dm.each(actions))
                remove_item_action(action as datum_action);
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:884
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Its settings can be changed with a [].", EXAMINE_HINT(I18n.T("multitool")))));
        Dm.append(ref dot, span_notice(I18n.T("User configuration switch is currently in the [] position, and could be flipped wtih a [].", Dm.truthy(user_controls) ? I18n.T("on") : I18n.T("off"), EXAMINE_HINT(I18n.T("screwdriver")))));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:889
    public virtual void on_entered(mob_living source = null)
    {
        if (Dm.@in(source, direct_view_tracking))
            return;

        refresh_overlay(source);
        Dm.append(direct_view_tracking, source);
        // Track equipping/unequipping items for threat levels/ID identity
        RegisterSignal(source, COMSIG_MOB_EQUIPPED_ITEM, nameof(check_equippped_item));
        RegisterSignal(source, COMSIG_MOB_UNEQUIPPED_ITEM, nameof(refresh_overlay)); // Can't see the slot of the dropped item so we need to blanket update
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:899
    [SignalHandler]
    public virtual void check_equippped_item(mob_living source = null, obj_item equipped_item = null, object slot = null)
    {
        // Anywhere where an ID or a gun would count
        if (Dm.truthy(Dm.bitand(slot, Dm.bitor(Dm.bitor(ITEM_SLOT_BELT, ITEM_SLOT_ID), Dm.bitor(ITEM_SLOT_HANDS, ITEM_SLOT_BACK)))))
            refresh_overlay(source);
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:906
    public virtual void on_exited(mob_living source = null)
    {
        if (!Dm.@in(source, direct_view_tracking))
            return;

        Dm.remove(direct_view_tracking, source);
        UnregisterSignal(source, Dm.list(
            COMSIG_MOB_EQUIPPED_ITEM,
            COMSIG_MOB_UNEQUIPPED_ITEM));
    }
}

public sealed partial class TgState
{
    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:545, :553, :562  the tactical visor's type statics
    internal DmList obj_item_organ_eyes_robotic_tacvisor__valid_friendly_factions;
    internal DmList obj_item_organ_eyes_robotic_tacvisor__valid_hostile_factions;
    internal DmList obj_item_organ_eyes_robotic_tacvisor__threat_flag_options;
}

// tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:916 (gaps OG1)
public partial class datum_proximity_monitor_tacvisor : datum_proximity_monitor
{
    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:919
    public override object Destroy(float? force = null)
    {
        owner = null;
        return base.Destroy(force);
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:923
    public override void on_moved(atom_movable source = null, atom old_loc = null)
    {
        return;
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:926
    public override void on_entered(atom source = null, atom_movable arrived = null, turf old_loc = null)
    {
        if (arrived != host && (iscarbon(arrived) || isliving(arrived) && Dm.truthy(Dm.bitand(owner.obj_flags, EMAGGED))))
            owner.on_entered(arrived as mob_living);
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:934
    public override void on_initialized(turf location = null, atom created = null, object init_flags = null)
    {
        if (created != host && (iscarbon(created) || isliving(created) && Dm.truthy(Dm.bitand(owner.obj_flags, EMAGGED))))
            owner.on_entered(created as mob_living);
    }
}

// tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:930 (gaps OG1). tg's typo: "/datum/proximity_monitor/tacvisor/on_uncrossed/on_uncrossed"
// defines a subtype named on_uncrossed with the override on it, so the visor itself never untracks a mob that walks out of range (a tg bug,
// kept: the subtype is never made).
public partial class datum_proximity_monitor_tacvisor_on_uncrossed : datum_proximity_monitor_tacvisor
{
    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:930
    [DmParams("old_location", "exited", "direction")]
    public override void on_uncrossed(atom source = null, atom_movable gone = null, object direction = null)
    {
        mob exited = gone as mob;
        if (exited != host && (iscarbon(exited) || isliving(exited) && Dm.truthy(Dm.bitand(owner.obj_flags, EMAGGED))) && get_dist(exited, host) > Dm.num(current_range))
            owner.on_exited(exited as mob_living);
    }
}

// tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:939 (gaps OG1)
public partial class datum_client_colour_tacvisor : datum_client_colour
{
    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:941
    [DmParams("owner")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        color = color_matrix_filter(Dm.list(
            1, 0, 0,
            0, 1.75f, 0,
            0, 0, 0.75f,
            0, -0.75f, 0), COLORSPACE_HSL);
    }
}

// tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:951 (gaps OG1)
public partial class obj_item_organ_eyes_robotic_tacvisor_deathsquad : obj_item_organ_eyes_robotic_tacvisor
{
    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:958
    public override void on_examine(mob source = null, atom target = null, DmList examine_strings = null, DmList examine_overrides = null)
    {
        if (target == owner || !iscarbon(target) && !(isliving(target) && Dm.truthy(Dm.bitand(obj_flags, EMAGGED))))
            return;

        if (Dm.num(get_iff_signature(target as mob_living)) == IFF_FRIENDLY)
            examine_overrides[EXAMINE_OVERRIDE_PRIORITY_IFF] = span_notice(I18n.T("CentCom personnel. Do not attack."));
        else
            examine_overrides[EXAMINE_OVERRIDE_PRIORITY_IFF] = span_boldwarning(I18n.T("KILL KILL KILL KILL KILL KILL!!!"));
    }

    // tg: code/modules/surgery/organs/internal/eyes/eyes_augments.dm:968
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        await DmTask.CompletedTask;
        return UI_CLOSE;
    }
}
