// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/internal/eyes/_eyes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M2 P2 (scope.md 2e and plan.md Q7): the eyes as a type with its insert hooks; later slices the vision, the overlays and blinking.
// Gaps H3: on_face_wash translated (a face wash at a sink reaches it, tg's wash of the eyes).
// Gaps RG3: on_shiny_eyes_trait_update translated (the luminescent fluid's glowing eyes redraw them).
// Gaps OG1: the rest of the file: the eye scars and their nearsightedness, the iris and scar overlays of the organ, the atom colour
// overrides, the eye puncture of a head shot, the wardrobe reset.

// tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:1
[DmPath("/obj/item/organ/eyes")]
public partial class obj_item_organ_eyes : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:2-21
    public obj_item_organ_eyes()
    {
        name = BODY_ZONE_PRECISE_EYES;
        icon_state = "eyes";
        desc = "I see you!";
        visual = TRUE;
        zone = BODY_ZONE_PRECISE_EYES;
        slot = ORGAN_SLOT_EYES;
        gender = PLURAL;

        healing_factor = STANDARD_ORGAN_HEALING;
        decay_factor = STANDARD_ORGAN_DECAY;
        maxHealth = 0.5f * STANDARD_ORGAN_THRESHOLD; //half the normal health max since we go blind at 30, a permanent blindness at 50 therefore makes sense unless medicine is administered
        high_threshold = 0.3f * STANDARD_ORGAN_THRESHOLD; //threshold at 30
        low_threshold = 0.2f * STANDARD_ORGAN_THRESHOLD; //threshold at 20

        low_threshold_passed = span_info("Distant objects become somewhat less tangible.");
        high_threshold_passed = span_info("Everything starts to look a lot less clear.");
        now_failing = span_warning("Darkness envelopes you, as your eyes go blind!");
        now_fixed = span_info("Color and shapes are once again perceivable.");
        high_threshold_cleared = span_info("Your vision functions passably once more.");
        low_threshold_cleared = span_info("Your vision is cleared of any ailment.");
    }

    /// How much innate tint these eyes have
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:24
    public float tint = 0;
    /// How much innare flash protection these eyes have, usually paired with tint
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:26
    public float flash_protect = FLASH_PROTECTION_NONE;
    /// What level of invisibility these eyes can see
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:28
    public float see_invisible = SEE_INVISIBLE_LIVING;
    /// How much darkness to cut out of your view (basically, night vision)
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:30
    public object lighting_cutoff = null;
    /// List of color cutoffs from eyes, or null if not applicable
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:32
    public DmList color_cutoffs = null;
    /// Are these eyes immune to pepperspray?
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:34
    public float pepperspray_protect = FALSE;

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:36
    public object eye_color_left = null; // set to a hex code to override a mob's left eye color
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:37
    public object eye_color_right = null; // set to a hex code to override a mob's right eye color
    /// The icon file of that eyes as its applied to the mob
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:39
    public object eye_icon = Dm.resource("icons/mob/human/human_eyes.dmi");
    /// The icon state of that eyes as its applied to the mob
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:41
    public string eye_icon_state = "eyes";
    /// Do these eyes have blinking animations
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:43
    public float blink_animation = TRUE;
    /// Icon state for iris overlays
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:45
    public string iris_overlay = "eyes_iris";
    /// Should our blinking be synchronized or can separate eyes have (slightly) separate blinking times
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:47
    public float synchronized_blinking = TRUE;
    // A pair of abstract eyelid objects (yes, really) used to animate blinking
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:49
    public obj_effect_abstract_eyelid_effect eyelid_left;
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:50
    public obj_effect_abstract_eyelid_effect eyelid_right;

    /// Glasses cannot be worn over these eyes.
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:53
    public float no_glasses = FALSE;
    /// Native FOV that will be applied if a config is enabled
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:55
    public float native_fov = FOV_90_DEGREES;
    /// Scarring on this organ
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:57
    public float scarring = NONE;

    /// The (custom, sometimes) messages we get when we use a flashlight or penlight on these eyes.
    /// Completely optional but good if you wanna be FANCY

    /// this message should never show up for default eyes, do not change on default eyes.
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:63
    public string penlight_message = "useless default please report";
    /// what are the pupils called? eg. pupils, apertures, etc.
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:65
    public string pupils_name = "pupils";
    /// do these eyes have pupils (or equivalent) that react to light when penlighted.
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:67
    public float light_reactive = TRUE;
}

// tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:69 (interleaved types, rules 1.9)
public partial class obj_item_organ_eyes : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:69
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(blink_animation))
        {
            eyelid_left = Dm.@new<obj_effect_abstract_eyelid_effect>(this, Dm.text("[]_l", eye_icon_state));
            eyelid_right = Dm.@new<obj_effect_abstract_eyelid_effect>(this, Dm.text("[]_r", eye_icon_state));
        }
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:75
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref eyelid_left);
        QDEL_NULL(ref eyelid_right);
        return base.Destroy(force);
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:80
    [DmParams("receiver", "special", "movement_flags")]
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon receiver = organ_owner;
        object dot = null;
        dot = await base.on_mob_insert(receiver, special, movement_flags);
        if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)))
            await receiver.become_blind(EYE_DAMAGE);
        if (Dm.num(damage) >= low_threshold)
            await receiver.assign_nearsightedness(EYE_DAMAGE, Dm.num(damage) >= high_threshold ? 3 : 2, TRUE);

        receiver.cure_blind(NO_EYES);

        // Ensures that non-player mobs get their eye colors assigned, as players get them from prefs
        if (ishuman(receiver))
        {
            mob_living_carbon_human as_human = receiver as mob_living_carbon_human;
            if (!Dm.truthy(eye_color_left))
                eye_color_left = as_human.eye_color_left;
            if (!Dm.truthy(eye_color_right))
                eye_color_right = as_human.eye_color_right;
            RegisterSignals(receiver, Dm.list(
                SIGNAL_ADDTRAIT(TRAIT_LUMINESCENT_EYES),
                SIGNAL_REMOVETRAIT(TRAIT_LUMINESCENT_EYES),
                SIGNAL_ADDTRAIT(TRAIT_REFLECTIVE_EYES),
                SIGNAL_REMOVETRAIT(TRAIT_REFLECTIVE_EYES)), nameof(on_shiny_eyes_trait_update));
        }

        await refresh(receiver, call_update: TRUE);
        RegisterSignal(receiver, COMSIG_ATOM_BULLET_ACT, nameof(on_bullet_act));
        RegisterSignal(receiver, COMSIG_COMPONENT_CLEAN_FACE_ACT, nameof(on_face_wash));

        if (Dm.truthy(scarring))
            await apply_scarring_effects();
        return dot;
    }
}

// tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:112
public partial class obj_item_organ_eyes : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:112
    public virtual async DmTask refresh(mob_living_carbon eye_owner = null, float? call_update = null)
    {
        eye_owner ??= owner;
        call_update ??= TRUE;
        if (eye_owner == null)
            return;

        eye_owner.update_sight();
        await eye_owner.update_tint();

        if (!ishuman(eye_owner))
            return;

        mob_living_carbon_human affected_human = eye_owner as mob_living_carbon_human;
        if (Dm.truthy(eye_color_left))
            await affected_human.add_eye_color_left(eye_color_left, EYE_COLOR_ORGAN_PRIORITY, update_body: FALSE);
        if (Dm.truthy(eye_color_right))
            await affected_human.add_eye_color_right(eye_color_right, EYE_COLOR_ORGAN_PRIORITY, update_body: FALSE);
        await refresh_atom_color_overrides();

        if (Dm.truthy(config.Get(typeof(datum_config_entry_flag_native_fov))) && Dm.truthy(native_fov)) /* CONFIG_GET(flag/native_fov) */
            // affected_human.add_fov_trait(type, native_fov): /mob/living/proc/add_fov_trait (living_fov.dm) has no mirror; the 2D field
            // of view is replaced by the 3D view (the flag is off by default, as on tg's servers)
            Dm.Deferred("code/modules/surgery/organs/internal/eyes/_eyes.dm:130", "refresh: affected_human.add_fov_trait(type, native_fov): the 2D field of view");

        if (Dm.truthy(call_update))
        {
            await affected_human.update_eyes();
        }
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:193
    [SignalHandler]
    public virtual void on_shiny_eyes_trait_update(mob_living_carbon_human source = null)
    {
        Waiting.NoSleep(source.update_eyes()); // gaps RG3: the luminescent fluid's glowing eyes
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:204
    public virtual async DmTask refresh_atom_color_overrides()
    {
        if (!Dm.truthy(atom_colours))
            return;

        mob_living_carbon_human human_owner = owner as mob_living_carbon_human;
        for (float i = 1, i__end = COLOUR_PRIORITY_AMOUNT; i <= i__end; i++)
        {
            DmList checked_color = atom_colours[i] as DmList;
            if (!Dm.truthy(checked_color))
            {
                await human_owner.remove_eye_color(EYE_COLOR_ATOM_COLOR_PRIORITY + i, update_body: FALSE);
                continue;
            }

            object left_color = COLOR_WHITE;
            object right_color = COLOR_WHITE;

            if (Dm.truthy(eye_color_left))
                left_color = eye_color_left;
            if (Dm.truthy(eye_color_right))
                right_color = eye_color_right;

            if (Dm.eq(checked_color[ATOM_COLOR_TYPE_INDEX], ATOM_COLOR_TYPE_FILTER))
            {
                DmList color_filter = checked_color[ATOM_COLOR_VALUE_INDEX] as DmList;
                left_color = apply_matrix_to_color(left_color, color_filter["color"] as DmList, Dm.num_or_null(color_filter["space"] is var or__1 && Dm.truthy(or__1) ? or__1 : COLORSPACE_RGB));
                right_color = apply_matrix_to_color(right_color, color_filter["color"] as DmList, Dm.num_or_null(color_filter["space"] is var or__2 && Dm.truthy(or__2) ? or__2 : COLORSPACE_RGB));
            }
            else
            {
                DmList target_color = color_transition_filter(checked_color[ATOM_COLOR_VALUE_INDEX], SATURATION_OVERRIDE) as DmList;
                left_color = apply_matrix_to_color(left_color, target_color["color"] as DmList, COLORSPACE_HSL);
                right_color = apply_matrix_to_color(right_color, target_color["color"] as DmList, COLORSPACE_HSL);
            }

            await human_owner.add_eye_color_left(left_color, EYE_COLOR_ATOM_COLOR_PRIORITY + i, update_body: FALSE);
            await human_owner.add_eye_color_right(right_color, EYE_COLOR_ATOM_COLOR_PRIORITY + i, update_body: FALSE);
        }
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:197 (gaps OG1)
    public override async DmTask update_atom_colour()
    {
        await base.update_atom_colour();
        if (ishuman(owner))
        {
            await refresh_atom_color_overrides();
            await owner.update_eyes();
        }
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:235
    public virtual void on_bullet_act(mob_living_carbon source = null, obj_projectile proj = null, object def_zone = null, object piercing_hit = null, object blocked = null)
    {
        // The M7 projectiles slice: the shot's chance and the eye's pain and damage; the puncture and the scar wait for vision (M9)

        // Once-a-dozen-rounds level of rare
        if (!Dm.eq(def_zone, BODY_ZONE_HEAD) || !prob(Dm.num(proj.damage) * 0.1f) || !(proj.damage_type == BRUTE || proj.damage_type == BURN))
            return;

        if (Dm.truthy(blocked) && Dm.truthy(source.is_eyes_covered()))
        {
            if (!Dm.truthy(proj.armour_penetration) || prob(Dm.num(blocked) - proj.armour_penetration))
                return;
        }

        object valid_sides = Dm.list();
        if (!Dm.truthy(Dm.bitand(scarring, RIGHT_EYE_SCAR)))
            Dm.append(ref valid_sides, RIGHT_EYE_SCAR);
        if (!Dm.truthy(Dm.bitand(scarring, LEFT_EYE_SCAR)))
            Dm.append(ref valid_sides, LEFT_EYE_SCAR);
        if (!Dm.truthy(length(valid_sides)))
            return;

        object picked_side = pick(valid_sides);
        to_chat(owner, span_userdanger(I18n.T("You feel searing pain shoot though your [] eye!", (Dm.eq(picked_side, RIGHT_EYE_SCAR) ? "right" : "left"))));
        // oof ouch my eyes
        Waiting.NoSleep(apply_organ_damage(rand((maxHealth - high_threshold) * 0.5f, maxHealth - low_threshold)));
        datum_wound_pierce_bleed_severe_eye eye_puncture = Dm.@new<datum_wound_pierce_bleed_severe_eye>();
        Waiting.NoSleep(eye_puncture.apply_wound(bodypart_owner, null, null, null, null, "bullet impact", null, picked_side)); // gaps OG1
        Waiting.NoSleep(apply_scar(picked_side));
    }

    /// When our owner washes their face. The idea that spessmen wash their eyeballs is highly disturbing but this is the easiest way to get rid of cursed crayon eye coloring
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:263 (gaps H3: a face wash at a sink reaches it)
    [SignalHandler]
    public virtual void on_face_wash()
    {
        Waiting.NoSleep(wash(CLEAN_WASH));
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:306
    // M10x slice D2: translated (the eyes drawn on the head)
    public virtual object generate_body_overlay(obj_item_bodypart_head my_head = null)
    {
        if (!Dm.truthy(eye_icon_state) || my_head == null)
            return Dm.list();

        object eye_dir = my_head.owner != null ? null : SOUTH;
        var eye_left = (mutable_appearance)mutable_appearance(eye_icon, Dm.text("[]_l", eye_icon_state), -EYES_LAYER);
        var eye_right = (mutable_appearance)mutable_appearance(eye_icon, Dm.text("[]_r", eye_icon_state), -EYES_LAYER);
        if (eye_dir != null) // a null dir keeps the default, as image(dir = null) does for the lips
        {
            eye_left.dir = Dm.num(eye_dir);
            eye_right.dir = Dm.num(eye_dir);
        }

        DmList overlays = Dm.list(eye_left, eye_right);

        if (my_head.owner != null && !Dm.truthy(Dm.bitand(my_head.owner.obscured_slots, HIDEEYES)))
            Dm.append(overlays, get_emissive_overlays(eye_left, eye_right, my_head));

        if (Dm.truthy(Dm.bitand(my_head.head_flags, HEAD_EYECOLOR)) && my_head.is_husked != HUSKED_ZOMBIE)
        {
            eye_right.color = my_head.owner?.get_right_eye_color() is var right && Dm.truthy(right) ? right : eye_color_right;
            eye_left.color = my_head.owner?.get_left_eye_color() is var left && Dm.truthy(left) ? left : eye_color_left;
            var eyelids = get_eyelid_overlays(eye_left, eye_right, my_head) as DmList;
            if (LAZYLEN(eyelids) != 0)
                Dm.append(overlays, eyelids);
        }

        if (Dm.truthy(Dm.bitand(scarring, RIGHT_EYE_SCAR)))
        {
            var right_scar = (mutable_appearance)mutable_appearance(Dm.resource("icons/mob/human/human_eyes.dmi"), "eye_scar_right", -EYES_LAYER);
            right_scar.color = my_head.draw_color;
            Dm.append(overlays, right_scar);
        }

        if (Dm.truthy(Dm.bitand(scarring, LEFT_EYE_SCAR)))
        {
            var left_scar = (mutable_appearance)mutable_appearance(Dm.resource("icons/mob/human/human_eyes.dmi"), "eye_scar_left", -EYES_LAYER);
            left_scar.color = my_head.draw_color;
            Dm.append(overlays, left_scar);
        }

        if (my_head.worn_face_offset != null)
        {
            foreach (object overlay in Dm.each(overlays)) // gaps OG1
                my_head.worn_face_offset.apply_offset(overlay as image);
        }

        return overlays;
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:345
    public virtual object get_emissive_overlays(mutable_appearance eye_left = null, mutable_appearance eye_right = null, atom spokesman = null)
    {
        // DEVIATION(DV12): the luminescent and reflective eyes' emissive appearances and the others' emissive blockers (:346-360)
        // draw on the emissive plane, which the port does not have (D24): no overlays.
        return Dm.list();
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:389
    // gaps OG1: the eye scars
    public virtual async DmTask apply_scar(object side = null)
    {
        if (Dm.truthy(Dm.bitand(scarring, side)))
            return;
        scarring = Dm.num(Dm.bitor(scarring, side));
        maxHealth -= 15;
        update_appearance();
        await apply_scarring_effects();
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:397
    public virtual async DmTask apply_scarring_effects()
    {
        if (owner == null)
            return;
        // Even if eyes have enough health, our owner still becomes nearsighted
        if (Dm.truthy(Dm.bitand(scarring, RIGHT_EYE_SCAR)))
            await owner.assign_nearsightedness(TRAIT_RIGHT_EYE_SCAR, 1, FALSE);
        if (Dm.truthy(Dm.bitand(scarring, LEFT_EYE_SCAR)))
            await owner.assign_nearsightedness(TRAIT_LEFT_EYE_SCAR, 1, FALSE);
        if (Dm.truthy(Dm.bitand(scarring, RIGHT_EYE_SCAR)) && Dm.truthy(Dm.bitand(scarring, LEFT_EYE_SCAR)))
            await owner.become_blind(EYE_SCARRING_TRAIT);
        await owner.update_eyes();
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:409
    public virtual async DmTask fix_scar(object side = null)
    {
        if (!Dm.truthy(Dm.bitand(scarring, side)))
            return;
        scarring = Dm.bitand(scarring, ~(int)Dm.num(side));
        maxHealth += 15;
        update_appearance();
        if (owner == null)
            return;
        owner.cure_nearsighted(Dm.eq(side, RIGHT_EYE_SCAR) ? TRAIT_RIGHT_EYE_SCAR : TRAIT_LEFT_EYE_SCAR);
        owner.cure_blind(EYE_SCARRING_TRAIT);
        await owner.update_eyes();
    }

    //Gotta reset the eye color, because that persists
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:425 (gaps OG1)
    public override async DmTask enter_wardrobe()
    {
        await base.enter_wardrobe();
        eye_color_left = Dm.initial(this, (obj_item_organ_eyes p) => p.eye_color_left);
        eye_color_right = Dm.initial(this, (obj_item_organ_eyes p) => p.eye_color_right);
        await fix_scar(LEFT_EYE_SCAR);
        await fix_scar(RIGHT_EYE_SCAR);
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:362 (gaps OG1: the scars and the coloured irises on the organ's sprite)
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(Dm.bitand(scarring, RIGHT_EYE_SCAR)))
        {
            mutable_appearance right_scar = mutable_appearance(Dm.resource("icons/obj/medical/organs/organs.dmi"), "eye_scar_right") as mutable_appearance;
            right_scar.blend_mode = BLEND_INSET_OVERLAY;
            Dm.append(ref dot, right_scar);
        }

        if (Dm.truthy(Dm.bitand(scarring, LEFT_EYE_SCAR)))
        {
            mutable_appearance left_scar = mutable_appearance(Dm.resource("icons/obj/medical/organs/organs.dmi"), "eye_scar_left") as mutable_appearance;
            left_scar.blend_mode = BLEND_INSET_OVERLAY;
            Dm.append(ref dot, left_scar);
        }

        if (Dm.truthy(iris_overlay) && Dm.truthy(eye_color_left) && Dm.truthy(eye_color_right))
        {
            mutable_appearance left_iris = mutable_appearance(icon, Dm.text("[]_l", iris_overlay)) as mutable_appearance;
            mutable_appearance right_iris = mutable_appearance(icon, Dm.text("[]_r", iris_overlay)) as mutable_appearance;
            DmList color_left = Dm.rgb2num(Dm.str(eye_color_left), COLORSPACE_HSL);
            DmList color_right = Dm.rgb2num(Dm.str(eye_color_right), COLORSPACE_HSL);
            // Ugly as sin? Indeed it is! But otherwise eyeballs turn out to be super dark, and this way even lighter colors are mostly preserved
            if (Dm.truthy(color_left[3]))
                color_left[3] = Dm.num(color_left[3]) / sqrt(Dm.num(color_left[3]) * 0.01f);
            if (Dm.truthy(color_right[3]))
                color_right[3] = Dm.num(color_right[3]) / sqrt(Dm.num(color_right[3]) * 0.01f);
            left_iris.color = Dm.rgb(Dm.num(color_left[1]), Dm.num(color_left[2]), Dm.num(color_left[3]), null, COLORSPACE_HSL);
            right_iris.color = Dm.rgb(Dm.num(color_right[1]), Dm.num(color_right[2]), Dm.num(color_right[3]), null, COLORSPACE_HSL);
            Dm.append(ref dot, left_iris);
            Dm.append(ref dot, right_iris);
        }
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:432
    public override async DmTask on_low_damage_received()
    {
        if (Dm.num(damage) >= high_threshold)
            return;
        if (owner != null)
            await owner.assign_nearsightedness(EYE_DAMAGE, 2, TRUE);
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:437
    public override async DmTask on_high_damage_received()
    {
        if (owner != null)
            await owner.assign_nearsightedness(EYE_DAMAGE, 3, TRUE);
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:440
    public override async DmTask on_begin_failure()
    {
        if (owner != null)
            await owner.become_blind(EYE_DAMAGE);
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:443
    public override void on_failure_recovery()
    {
        owner?.cure_blind(EYE_DAMAGE);
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:446
    public override async DmTask on_high_damage_healed()
    {
        if (Dm.num(damage) <= low_threshold)
            return;
        if (owner != null)
            await owner.assign_nearsightedness(EYE_DAMAGE, 2, TRUE);
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:451
    public override void on_low_damage_healed()
    {
        // clear nearsightedness from damage
        owner?.cure_nearsighted(EYE_DAMAGE);
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:465
    public virtual object get_eyelid_overlays(mutable_appearance eye_left = null, mutable_appearance eye_right = null, obj_item_bodypart_head my_head = null)
    {
        mob_living_carbon_human parent = my_head.owner as mob_living_carbon_human;
        // Robotic eyes or colorless heads don't get the privelege of having eyelids
        if (parent == null || Dm.truthy(IS_ROBOTIC_ORGAN(this)) || !Dm.truthy(my_head.draw_color) || HAS_TRAIT(parent, TRAIT_NO_EYELIDS))
            return null;

        DmList base_color = Dm.rgb2num((string)my_head.draw_color, COLORSPACE_HSL);
        base_color[2] = Dm.num(base_color[2]) * 0.85f;
        base_color[3] = Dm.num(base_color[3]) * 0.85f;
        string eyelid_color = Dm.rgb(Dm.num(base_color[1]), Dm.num(base_color[2]), Dm.num(base_color[3]), Dm.length(base_color) >= 4 ? Dm.num(base_color[4]) : null, COLORSPACE_HSL);
        // If we're knocked out, just color the eyes
        if (IS_DEAD_OR_FAKING(parent) || IS_UNCONSCIOUS(parent))
        {
            eye_right.color = eyelid_color;
            eye_left.color = eyelid_color;
            return null;
        }

        if (!Dm.truthy(blink_animation) || HAS_TRAIT(parent, TRAIT_PREVENT_BLINKING))
            return null;

        eyelid_left.color = eyelid_color;
        eyelid_right.color = eyelid_color;
        eyelid_left.render_target = Dm.text("*[]_eyelid_left", REF(parent));
        eyelid_right.render_target = Dm.text("*[]_eyelid_right", REF(parent));
        Dm.append(parent.vis_contents, eyelid_left);
        Dm.append(parent.vis_contents, eyelid_right);
        animate_eyelids(parent);
        // DEVIATION(DV207): the client draws no render_source: it draws the eyelid effects themselves in the holder's picture (protocol.md
        // 19.3), so these two overlays show nothing there.
        var left_eyelid_overlay = (mutable_appearance)mutable_appearance(layer: -EYES_LAYER, offset_spokesman: parent);
        var right_eyelid_overlay = (mutable_appearance)mutable_appearance(layer: -EYES_LAYER, offset_spokesman: parent);
        left_eyelid_overlay.render_source = Dm.text("*[]_eyelid_left", REF(parent));
        right_eyelid_overlay.render_source = Dm.text("*[]_eyelid_right", REF(parent));
        return Dm.list(left_eyelid_overlay, right_eyelid_overlay);
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:459-462  (file-local defines)
    private const float BASE_BLINKING_DELAY = 5 * SECONDS;
    private const float RAND_BLINKING_DELAY = 1 * SECONDS;
    private const float BLINK_DURATION = 0.15f * SECONDS;
    private const int BLINK_LOOPS = 5;

    /// Animates one eyelid at a time, thanks BYOND and thanks animation chains
    // M9 slice B (the blink emotes): animate_eyelid, blink and animate_eyelids translated; M10x: the eyelid effects they animate.
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:498
    public virtual object animate_eyelid(obj_effect_abstract_eyelid_effect eyelid = null, mob_living_carbon_human parent = null, float? sync_blinking = null, DmList anim_times = null)
    {
        sync_blinking ??= TRUE;
        DmList dot = Dm.list();
        if (eyelid == null) // Can't blink if we don't have an eyelid
            return dot;
        bool prevent_loops = HAS_TRAIT(parent, TRAIT_PREVENT_BLINK_LOOPS);
        Dm.animate(eyelid, Dm.named("alpha", 0), Dm.named("time", 0), Dm.named("loop", prevent_loops ? 0 : -1));

        float wait_time = Dm.num(rand(BASE_BLINKING_DELAY - RAND_BLINKING_DELAY, BASE_BLINKING_DELAY + RAND_BLINKING_DELAY));
        if (anim_times != null)
        {
            if (Dm.truthy(sync_blinking))
                wait_time = Dm.num(anim_times[1]);
            else
                wait_time = Dm.num(rand(Dm.max(BASE_BLINKING_DELAY - RAND_BLINKING_DELAY, Dm.num(anim_times[1]) - RAND_BLINKING_DELAY), Dm.num(anim_times[1])));
        }

        Dm.animate(Dm.named("time", wait_time));
        Dm.append(ref dot, wait_time);

        int cycles = prevent_loops ? 1 : BLINK_LOOPS;
        for (int i = 1; i <= cycles; i++)
        {
            if (anim_times != null)
            {
                if (Dm.truthy(sync_blinking))
                    wait_time = Dm.num(anim_times[i + 1]);
                else
                    wait_time = Dm.num(rand(Dm.max(BASE_BLINKING_DELAY - RAND_BLINKING_DELAY, Dm.num(anim_times[i + 1]) - RAND_BLINKING_DELAY), Dm.num(anim_times[i + 1])));
            }
            else
                wait_time = Dm.num(rand(BASE_BLINKING_DELAY - RAND_BLINKING_DELAY, BASE_BLINKING_DELAY + RAND_BLINKING_DELAY));
            Dm.append(ref dot, wait_time);
            if (anim_times != null && !Dm.truthy(sync_blinking))
            {
                // Make sure that we're somewhat in sync with the other eye
                float offset_time = Dm.num(anim_times[i + 1]) - wait_time;
                if (offset_time != 0) // For some reason having time == 0 in this case breaks animate
                    Dm.animate(Dm.named("time", offset_time));
            }
            Dm.animate(Dm.named("alpha", 255), Dm.named("time", 0));
            Dm.animate(Dm.named("time", BLINK_DURATION));
            if (i != cycles)
            {
                Dm.animate(Dm.named("alpha", 0), Dm.named("time", 0));
                Dm.animate(Dm.named("time", wait_time));
            }
        }
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:536
    public virtual void blink(float? duration = null, float? restart_animation = null)
    {
        duration ??= BLINK_DURATION;
        restart_animation ??= TRUE;
        if (!Dm.truthy(blink_animation))
            return;
        bool left_delayed = prob(50);
        // Storing blink delay so mistimed blinks of lizards don't get cut short
        bool sync_blinking = Dm.truthy(synchronized_blinking) && (Dm.num(owner.get_organ_loss(ORGAN_SLOT_BRAIN)) < BRAIN_DAMAGE_ASYNC_BLINKING);
        float blink_delay = sync_blinking ? 0 : Dm.num(rand(0, RAND_BLINKING_DELAY));
        Dm.animate(eyelid_left, Dm.named("alpha", 0), Dm.named("time", 0));
        if (!sync_blinking && left_delayed)
            Dm.animate(Dm.named("time", blink_delay));
        Dm.animate(Dm.named("alpha", 255), Dm.named("time", 0));
        Dm.animate(Dm.named("time", duration));
        Dm.animate(Dm.named("alpha", 0), Dm.named("time", 0));
        Dm.animate(eyelid_right, Dm.named("alpha", 0), Dm.named("time", 0));
        if (!sync_blinking && !left_delayed)
            Dm.animate(Dm.named("time", blink_delay));
        Dm.animate(Dm.named("alpha", 255), Dm.named("time", 0));
        Dm.animate(Dm.named("time", duration));
        Dm.animate(Dm.named("alpha", 0), Dm.named("time", 0));
        if (Dm.truthy(restart_animation))
            _addtimer(Dm.@new<datum_callback>(this, nameof(animate_eyelids), owner), blink_delay + Dm.num(duration), file: "code/modules/surgery/organs/internal/eyes/_eyes.dm", line: 555);
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:558
    public virtual void animate_eyelids(mob_living_carbon_human parent = null)
    {
        bool sync_blinking = Dm.truthy(synchronized_blinking) && (Dm.num(parent.get_organ_loss(ORGAN_SLOT_BRAIN)) < BRAIN_DAMAGE_ASYNC_BLINKING);
        // Randomize order for unsynched animations
        if (sync_blinking || prob(50))
        {
            DmList anim_times = animate_eyelid(eyelid_left, parent, Dm.num(sync_blinking)) as DmList;
            animate_eyelid(eyelid_right, parent, Dm.num(sync_blinking), anim_times);
        }
        else
        {
            DmList anim_times = animate_eyelid(eyelid_right, parent, Dm.num(sync_blinking)) as DmList;
            animate_eyelid(eyelid_left, parent, Dm.num(sync_blinking), anim_times);
        }
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:584
    public virtual async DmTask<object> penlight_examine(mob_living viewer = null, params object[] __extra)
    {
        return span_notice(I18n.T("[] eyes [].", owner.p_Their(), penlight_message));
    }
}

// tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:568
[DmPath("/obj/effect/abstract/eyelid_effect")]
public partial class obj_effect_abstract_eyelid_effect : obj_effect_abstract
{
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:574
    [DmParams("mapload", "new_state")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_state = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, Dm.forward(__extra, new_state));
        icon_state = (string)new_state;
        return dot;
    }
}

// tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:135 (interleaved types, rules 1.9)
public partial class obj_item_organ_eyes : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:135
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon organ_owner__carbon = organ_owner as mob_living_carbon;
        await base.on_mob_remove(organ_owner, special, movement_flags);
        // Don't forget about the eyelids
        Dm.remove(organ_owner__carbon.vis_contents, eyelid_left);
        Dm.remove(organ_owner__carbon.vis_contents, eyelid_right);

        if (ishuman(organ_owner))
        {
            mob_living_carbon_human human_owner = organ_owner as mob_living_carbon_human;
            await human_owner.remove_eye_color(EYE_COLOR_ORGAN_PRIORITY, update_body: FALSE);
            for (float i = 1, i__end = COLOUR_PRIORITY_AMOUNT; i <= i__end; i++)
                await human_owner.remove_eye_color(EYE_COLOR_ATOM_COLOR_PRIORITY + i, update_body: FALSE);
            if (Dm.truthy(native_fov))
            {
                // organ_owner.remove_fov_trait(type): GAP(lead): /mob/living/proc/remove_fov_trait (living_fov.dm) has no mirror;
                // the 2D field of view is replaced by the 3D view (never added: see refresh).
                Dm.Deferred("code/modules/surgery/organs/internal/eyes/_eyes.dm:146", "on_mob_remove: organ_owner.remove_fov_trait(type): the 2D field of view");
            }
            if (!Dm.truthy(special))
                await human_owner.update_eyes(refresh: FALSE);
        }

        // become blind (if not special)
        if (!Dm.truthy(special))
            await organ_owner__carbon.become_blind(NO_EYES);

        // Cure blindness from eye damage
        organ_owner__carbon.cure_blind(EYE_DAMAGE);
        organ_owner__carbon.cure_nearsighted(EYE_DAMAGE); // gaps OG1
        // Eye blind and temp blind go to, even if this is a bit of cheesy way to clear blindness
        organ_owner__carbon.remove_status_effect(typeof(datum_status_effect_eye_blur));
        organ_owner__carbon.remove_status_effect(typeof(datum_status_effect_temporary_blindness));

        if (Dm.truthy(scarring))
        {
            organ_owner__carbon.cure_nearsighted(TRAIT_RIGHT_EYE_SCAR);
            organ_owner__carbon.cure_nearsighted(TRAIT_LEFT_EYE_SCAR);
            organ_owner__carbon.cure_blind(EYE_SCARRING_TRAIT);
        }

        await organ_owner__carbon.update_tint();
        organ_owner__carbon.update_sight();
        UnregisterSignal(organ_owner__carbon, Dm.list(
            COMSIG_ATOM_BULLET_ACT,
            COMSIG_COMPONENT_CLEAN_FACE_ACT,
            SIGNAL_ADDTRAIT(TRAIT_LUMINESCENT_EYES),
            SIGNAL_REMOVETRAIT(TRAIT_LUMINESCENT_EYES),
            SIGNAL_ADDTRAIT(TRAIT_REFLECTIVE_EYES),
            SIGNAL_REMOVETRAIT(TRAIT_REFLECTIVE_EYES)));
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:178
    public override async DmTask on_bodypart_insert(obj_item_bodypart limb = null)
    {
        await base.on_bodypart_insert(limb);
        if (ishuman(limb.owner))
            await limb.owner.update_eyes(refresh: FALSE);
        else
            await limb.update_icon_dropped();
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:185
    public override async DmTask on_bodypart_remove(obj_item_bodypart limb = null, object movement_flags = null)
    {
        await base.on_bodypart_remove(limb, movement_flags);
        if (ishuman(limb.owner))
            await limb.owner.update_eyes(refresh: FALSE);
        else
            await limb.update_icon_dropped();
    }
}

// tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:455 (M6 slice 2: the self-check's organ lines)
public partial class obj_item_organ_eyes : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:455
    public override object feel_for_damage(object self_aware = null)
    {
        // Eye damage has visual effects, so we don't really need to "feel" it when self-examining
        return "";
    }
}

// tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:271 (interleaved types, rules 1.9; M8 slice D)
public partial class obj_item_organ_eyes : obj_item_organ
{
    /// Similar to get_status_text, but appends the text after the damage report, for additional status info
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:271
    public override object get_status_appendix(object scanpower = null, object add_tooltips = null)
    {
        if (owner.stat == DEAD || IS_UNCONSCIOUS(owner))
            return null; // you're blind when dead or unconscious so it's redundant to show it

        if (Dm.truthy(owner.is_blind()))
        {
            if (Dm.num(scanpower) >= SCANPOWER_ADVANCED)
            {
                if (Dm.truthy(owner.is_blind_from(QUIRK_TRAIT)))
                    return conditional_tooltip(I18n.T("Subject is permanently blind."), I18n.T("Irreparable under normal circumstances."), add_tooltips);
                if (Dm.truthy(owner.is_blind_from(EYE_SCARRING_TRAIT)))
                    return conditional_tooltip(I18n.T("Subject is blind from widespread ocular scarring."), I18n.T("Surgically replace eyes, irreparable otherwise."), add_tooltips);
                if (Dm.truthy(owner.is_blind_from(TRAUMA_TRAIT)))
                    return conditional_tooltip(I18n.T("Subject is blind from mental trauma."), I18n.T("Repair via treatment of associated trauma."), add_tooltips);
                if (Dm.truthy(owner.is_blind_from(GENETIC_MUTATION)))
                    return conditional_tooltip(I18n.T("Subject is genetically blind."), I18n.T("Use medication such as [].", Dm.initial(typeof(datum_reagent_medicine_mutadone), (datum_reagent_medicine_mutadone r) => r.name)), add_tooltips);
                if (Dm.truthy(owner.is_blind_from(EYE_DAMAGE)))
                    return conditional_tooltip(I18n.T("Subject is blind from eye damage."), I18n.T("Repair surgically, use medication such as [], or protect eyes with a blindfold.", Dm.initial(typeof(datum_reagent_medicine_oculine), (datum_reagent_medicine_oculine r) => r.name)), add_tooltips);
            }
            return I18n.T("Subject is blind.");
        }
        if (Dm.truthy(owner.is_nearsighted()))
        {
            if (Dm.num(scanpower) >= SCANPOWER_ADVANCED)
            {
                if (Dm.truthy(owner.is_nearsighted_from(QUIRK_TRAIT)))
                    return conditional_tooltip(I18n.T("Subject is permanently nearsighted."), I18n.T("Irreparable under normal circumstances. Prescription glasses will assuage the effects."), add_tooltips);
                if (Dm.truthy(owner.is_nearsighted_from(TRAIT_RIGHT_EYE_SCAR)) || Dm.truthy(owner.is_nearsighted_from(TRAIT_LEFT_EYE_SCAR)))
                    return conditional_tooltip(I18n.T("Subject is nearsighted from severe ocular scarring."), I18n.T("Surgically replace eyes, irreparable otherwise."), add_tooltips);
                if (Dm.truthy(owner.is_nearsighted_from(GENETIC_MUTATION)))
                    return conditional_tooltip(I18n.T("Subject is genetically nearsighted."), I18n.T("Use medication such as []. Prescription glasses will assuage the effects.", Dm.initial(typeof(datum_reagent_medicine_mutadone), (datum_reagent_medicine_mutadone r) => r.name)), add_tooltips);
                if (Dm.truthy(owner.is_nearsighted_from(EYE_DAMAGE)))
                    return conditional_tooltip(I18n.T("Subject is nearsighted from eye damage."), I18n.T("Repair surgically or use medication such as []. Prescription glasses will assuage the effects.", Dm.initial(typeof(datum_reagent_medicine_oculine), (datum_reagent_medicine_oculine r) => r.name)), add_tooltips);
            }
            return I18n.T("Subject is nearsighted.");
        }
        return "";
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:301
    public override object show_on_condensed_scans()
    {
        // Always show if we have an appendix
        object dot = base.show_on_condensed_scans();
        return Dm.truthy(dot) ? dot : Dm.num(owner.stat != DEAD && !IS_UNCONSCIOUS(owner) && (Dm.truthy(owner.is_blind()) || Dm.truthy(owner.is_nearsighted())));
    }
}

// tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:593 (gaps P3b: the night vision eyes the rat's and the goliath's infusions grow)
public partial class obj_item_organ_eyes_night_vision : obj_item_organ_eyes
{
    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:588-591  #define NIGHTVISION_LIGHT_OFF/LOW/MID/HIG (file-local, #undef at the
    // end of the night vision block)
    private const float NIGHTVISION_LIGHT_OFF = 0;
    private const float NIGHTVISION_LIGHT_LOW = 1;
    private const float NIGHTVISION_LIGHT_MID = 2;
    private const float NIGHTVISION_LIGHT_HIG = 3;

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:603
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // PERFORM_ALL_TESTS(focus_only/nightvision_color_cutoffs) is FALSE in production (rules 1.7): its stack trace is a unit test's
        if (low_light_cutoff != null)
            color_cutoffs = low_light_cutoff.Copy();
        light_level = NIGHTVISION_LIGHT_LOW;
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/eyes/_eyes.dm:612
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        await DmTask.CompletedTask;
        if (light_level == NIGHTVISION_LIGHT_OFF)
        {
            color_cutoffs = low_light_cutoff.Copy();
            light_level = NIGHTVISION_LIGHT_LOW;
        }
        else if (light_level == NIGHTVISION_LIGHT_LOW)
        {
            color_cutoffs = medium_light_cutoff.Copy();
            light_level = NIGHTVISION_LIGHT_MID;
        }
        else if (light_level == NIGHTVISION_LIGHT_MID)
        {
            color_cutoffs = high_light_cutoff.Copy();
            light_level = NIGHTVISION_LIGHT_HIG;
        }
        else
        {
            color_cutoffs = null;
            light_level = NIGHTVISION_LIGHT_OFF;
        }
        owner.update_sight();
        return null;
    }
}
