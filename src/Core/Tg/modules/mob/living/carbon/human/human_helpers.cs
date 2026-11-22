// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/human_helpers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the M2 subset (the types this file declares first in .dme order, rules 2.3, and the roots of its slots, from the
// M2 contracts step; P2's translations), and the visible name (M2 F1): update_visible_name, get_visible_name, get_face_name
// and get_id_name of the human; the assignment and authentication names (M9 slice G); randomize_human_appearance (syndicate N1c,
// D316: the lone operative); calculate_fitness (gaps SL1, D333). The other helpers stay Dm.NotTranslated
// with their milestone.

// tg: code/modules/mob/living/carbon/human/human_helpers.dm:9
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:2  (M7x slice E)
    public override object canBeHandcuffed()
    {
        if (num_hands < 2)
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:9
    public virtual object get_assignment(string if_no_id = null, string if_no_job = null, float? hand_first = null)
    {
        if_no_id ??= "No id";
        if_no_job ??= "No job";
        hand_first ??= TRUE;
        object dot = null;
        obj_item_card_id id = get_idcard(hand_first) as obj_item_card_id;
        if (HAS_TRAIT(this, TRAIT_UNKNOWN_APPEARANCE))
            return if_no_id;
        if (id != null)
            dot = id.assignment;
        else
        {
            if (wear_id is obj_item_modular_computer pda)
                dot = pda.saved_job;
            else
                return if_no_id;
        }
        if (!Dm.truthy(dot))
            return if_no_job;
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:26
    public virtual object get_authentification_name(string if_no_id = null)
    {
        if_no_id ??= "Unknown";
        obj_item_card_id id = get_idcard(FALSE) as obj_item_card_id;
        if (HAS_TRAIT(this, TRAIT_UNKNOWN_APPEARANCE))
            return if_no_id;
        if (id != null)
            return id.registered_name;
        if (wear_id is obj_item_modular_computer pda)
            return pda.saved_identification;
        return if_no_id;
    }

    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:38
    [SignalHandler]
    public virtual void update_visible_name()
    {
        name = (string)get_visible_name();
    }

    /// Combines get_id_name() and get_face_name() to determine a mob's name variable. Made into a separate proc as it'll be useful elsewhere
    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:43
    public override object get_visible_name(float? add_id_name = null, float? force_real_name = null)
    {
        add_id_name ??= TRUE;
        force_real_name ??= FALSE;
        DmList identity = Dm.list(null, null, null);
        if (Dm.truthy(this._listen_lookup?[COMSIG_HUMAN_GET_VISIBLE_NAME]))
            this._SendSignal(COMSIG_HUMAN_GET_VISIBLE_NAME, Dm.list(this, identity));
        object signal_face = LAZYACCESS(identity, VISIBLE_NAME_FACE);
        object signal_id = LAZYACCESS(identity, VISIBLE_NAME_ID);
        object force_set = LAZYACCESS(identity, VISIBLE_NAME_FORCED);
        if (Dm.truthy(force_set)) // our name is overriden by something
            return signal_face; // no need to null-check, because force_set will always set a signal_face

        object face_name = (signal_face == null ? get_face_name("") : signal_face);
        object id_name = (signal_id == null ? get_id_name("", honorifics: add_id_name) : signal_id);

        // We need to account for real name
        if (Dm.truthy(force_real_name))
        {
            object disguse_name = get_visible_name(add_id_name: add_id_name, force_real_name: FALSE);
            return Dm.text("[][]", real_name, Dm.eq(disguse_name, real_name) ? "" : Dm.text(" (as [])", disguse_name));
        }

        // We're just some unknown guy
        if (HAS_TRAIT(this, TRAIT_UNKNOWN_APPEARANCE) || HAS_TRAIT(this, TRAIT_INVISIBLE_MAN))
            return "Unknown";

        // We have a face and an ID
        if (Dm.truthy(face_name) && Dm.truthy(id_name))
        {
            object normal_id_name = get_id_name(""); // need to check base ID name to avoid "John (as Captain John)"
            if (Dm.eq(normal_id_name, face_name))
                return id_name; // (this turns "John" into "Captain John")
            if (Dm.truthy(add_id_name))
                return Dm.text("[] (as [])", face_name, id_name);
        }

        // Just go down the list of stuff we recorded
        return (face_name is var or__1 && Dm.truthy(or__1) ? or__1 : (id_name is var or__2 && Dm.truthy(or__2) ? or__2 : "Unknown"));
    }

    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:83
    public override object get_face_name(string if_no_face = null)
    {
        if_no_face ??= "Unknown";
        if (!Dm.truthy(real_name) || Dm.truthy(is_face_obscured()))
            return if_no_face;
        return real_name;
    }

    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:107
    public override object get_id_name(string if_no_id = null, float? honorifics = null)
    {
        if_no_id ??= "Unknown";
        honorifics ??= FALSE;
        if (HAS_TRAIT(this, TRAIT_UNKNOWN_APPEARANCE))
        {
            DmList identity = Dm.list(null, null, null);
            if (Dm.truthy(this._listen_lookup?[COMSIG_HUMAN_GET_FORCED_NAME]))
                this._SendSignal(COMSIG_HUMAN_GET_FORCED_NAME, Dm.list(this, identity));
            return (Dm.truthy(identity[VISIBLE_NAME_FORCED]) ? identity[VISIBLE_NAME_FACE] : if_no_id);
        }

        // either results in an ID card, a generic card, or null
        obj_item_card id = (wear_id?.GetID() ?? (wear_id as obj_item_card)) as obj_item_card;
        object name = id?.get_displayed_name(honorifics);
        return Dm.truthy(name) ? name : if_no_id;
    }

    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:88
    public virtual object is_face_obscured()
    {
        if (HAS_TRAIT(this, TRAIT_UNKNOWN_APPEARANCE))
            return TRUE; //We're Unknown, no face information for you
        if (Dm.truthy(Dm.bitand(obscured_slots, HIDEFACE)))
            return TRUE;
        obj_item_bodypart head = get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;
        if (head == null || HAS_TRAIT(head, TRAIT_DISFIGURED) || HAS_TRAIT(this, TRAIT_INVISIBLE_MAN)) //disfigured. use id-name if possible
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:124  (M7x slice B: the gun's trigger check)
    public override object can_use_guns(obj_item G = null)
    {
        object dot = base.can_use_guns(G);
        if (G.trigger_guard == TRIGGER_GUARD_NORMAL)
        {
            if (Dm.truthy(check_chunky_fingers()))
            {
                balloon_alert(this, I18n.T("fingers are too big!"));
                return FALSE;
            }
        }
        if (HAS_TRAIT(this, TRAIT_NOGUNS))
        {
            to_chat(this, span_warning(I18n.T("You can't bring yourself to use a ranged weapon!")));
            return FALSE;
        }
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:134
    public virtual object check_chunky_fingers()
    {
        if (HAS_TRAIT_NOT_FROM(this, TRAIT_CHUNKYFINGERS, RIGHT_ARM_TRAIT) && HAS_TRAIT_NOT_FROM(this, TRAIT_CHUNKYFINGERS, LEFT_ARM_TRAIT))
            return TRUE;
        return IS_LEFT_INDEX(active_hand_index) ? HAS_TRAIT_FROM(this, TRAIT_CHUNKYFINGERS, LEFT_ARM_TRAIT) : HAS_TRAIT_FROM(this, TRAIT_CHUNKYFINGERS, RIGHT_ARM_TRAIT);
    }

    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:143  (gaps ST2: the eyepatch's tint reads it)
    public virtual object get_eye_scars()
    {
        obj_item_organ_eyes eyes = get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        if (eyes != null)
            return eyes.scarring;
        return null;
    }

    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:149
    public virtual void increment_scar_slot()
    {
        // M10 slice F (the round start and the late join call it): the persistent scars' slot in the player's BYOND savefile
        // (data/player_saves/.../scars.sav, /savefile: a shim gap). The persistent scars are not kept (not-translated.md 79), so the slot
        // is not counted.
        Dm.Deferred("code/modules/mob/living/carbon/human/human_helpers.dm:149", "increment_scar_slot: the persistent scars' savefile (BYOND's /savefile)");
    }

    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:166
    public virtual object format_scars()
    {
        Dm.NotTranslated("code/modules/mob/living/carbon/human/human_helpers.dm:166", "format_scars: not in M2 (M10+ identity, M8 scars)");
        return default;
    }

    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:180
    public virtual object load_scar(object scar_line = null, object specified_char_index = null)
    {
        Dm.NotTranslated("code/modules/mob/living/carbon/human/human_helpers.dm:180", "load_scar: not in M2 (M10+ identity, M8 scars)");
        return default;
    }

    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:196
    public virtual object load_persistent_scars()
    {
        // M10 slice F (the round start and the late join call it): the scars saved in the player's BYOND savefile (scars.sav,
        // /savefile: a shim gap). The persistent scars are not kept (not-translated.md 79), so none are loaded.
        Dm.Deferred("code/modules/mob/living/carbon/human/human_helpers.dm:196", "load_persistent_scars: the persistent scars' savefile (BYOND's /savefile)");
        return null;
    }

    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:222
    public virtual async DmTask save_persistent_scars(float? nuke = null)
    {
        Dm.NotTranslated("code/modules/mob/living/carbon/human/human_helpers.dm:222", "save_persistent_scars: not in M2 (M10+ identity, M8 scars)");
    }

    ///copies over clothing preferences like underwear to another human
    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:244  (the syndicate campaign G1c, D316: the fake identity kit's manifest picture)
    public virtual void copy_clothing_prefs(mob_living_carbon_human destination = null)
    {
        destination.underwear = underwear;
        destination.underwear_color = underwear_color;
        destination.undershirt = undershirt;
        destination.socks = socks;
        destination.jumpsuit_style = jumpsuit_style;
    }

    /// Fully randomizes everything according to the given flags.
    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:253  (syndicate N1c, D316: the lone operative's body)
    public virtual async DmTask randomize_human_appearance(float? randomize_flags = null)
    {
        randomize_flags ??= ALL;
        var preferences = await Dm.new_async<datum_preferences>(await Dm.new_async<datum_client_interface>());

        foreach (datum_preference preference in Dm.each<datum_preference>(get_preferences_in_priority_order()))
        {
            if (!Dm.truthy(preference.included_in_randomization_flags(randomize_flags)))
                continue;

            if (Dm.truthy(preference.is_randomizable()))
                preference.apply_to_human(this, preference.create_random_value(preferences), preferences);
        }

        await fully_replace_character_name(real_name, generate_random_mob_name());
    }

    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:271
    public virtual async DmTask set_mob_height(float? new_height = null, float? update_dna = null)
    {
        new_height ??= HUMAN_HEIGHT_MEDIUM;
        update_dna ??= TRUE;
        base_mob_height = new_height.Value;
        await update_mob_height();
        if (Dm.truthy(update_dna))
            dna?.update_ui_block(typeof(datum_dna_block_identity_height));
    }

    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:284
    public virtual async DmTask update_mob_height()
    {
        float old_height = mob_height;
        obj_item_bodypart_chest chest = get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart_chest;
        mob_height = Dm.num((chest?.update_mob_heights(this) is var or__1 && Dm.truthy(or__1) ? or__1 : base_mob_height));
        if (old_height != mob_height)
        {
            await regenerate_icons();
            readjust_atom_huds();
        }
        // SEND_SIGNAL(src, COMSIG_HUMAN_HEIGHT_UPDATED, old_height) (rules 11.1 (e), a statement)
        if (Dm.truthy(this._listen_lookup?[COMSIG_HUMAN_HEIGHT_UPDATED]))
            this._SendSignal(COMSIG_HUMAN_HEIGHT_UPDATED, Dm.list(this, old_height));
    }

    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:297
    public virtual object get_base_mob_height()
    {
        return base_mob_height;
    }

    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:308
    public virtual async DmTask<object> make_full_human_copy(turf location = null, client quirk_client = null)
    {
        Dm.NotTranslated("code/modules/mob/living/carbon/human/human_helpers.dm:308", "make_full_human_copy: not in M2 (M10+ identity, M8 scars)");
        return default;
    }

    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:332  (gaps SL1, D333: the fitness a novice athlete sees on examine)
    public override object calculate_fitness()
    {
        float fitness_modifier = 1;
        if (HAS_TRAIT(this, TRAIT_HULK))
            fitness_modifier *= 2;
        if (HAS_TRAIT(this, TRAIT_STRENGTH))
            fitness_modifier *= 1.5f;
        if (HAS_TRAIT(this, TRAIT_ROD_SUPLEX))
            fitness_modifier *= 2; // To be able to suplex a rod, you must possess an incredible amount of power
        if (HAS_TRAIT(this, TRAIT_EASILY_WOUNDED))
            fitness_modifier /= 2;
        if (HAS_TRAIT(this, TRAIT_GAMER))
            fitness_modifier /= 1.5f;
        if (HAS_TRAIT(this, TRAIT_GRABWEAKNESS))
            fitness_modifier /= 1.5f;

        object athletics_level = (mind?.get_skill_level(typeof(datum_skill_athletics)) is var or__1 && Dm.truthy(or__1) ? or__1 : 1);

        float min_damage = 0;
        float max_damage = 0;
        foreach (object body_zone in Dm.each(GLOB.limb_zones))
        {
            obj_item_bodypart part = get_bodypart((string)body_zone) as obj_item_bodypart;
            if (part == null || part.unarmed_damage_high <= 0 || HAS_TRAIT(part, TRAIT_PARALYSIS))
                continue;
            min_damage += part.unarmed_damage_low;
            max_damage += part.unarmed_damage_high;
        }

        float damage = ((min_damage / 4) + (max_damage / 4)) / 2; // We expect you to have 4 functional limbs- if you have fewer you're probably not going to be so good at lifting

        return ceil(damage * (ceil(Dm.num(athletics_level) / 2)) * fitness_modifier * maxHealth);
    }

    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:362
    public virtual async DmTask<object> item_heal(mob user = null, object brute_heal = null, object burn_heal = null, object heal_message_brute = null, object heal_message_burn = null, object required_bodytype = null)
    {
        Dm.NotTranslated("code/modules/mob/living/carbon/human/human_helpers.dm:362", "item_heal: not in M2 (M10+ identity, M8 scars)");
        return default;
    }

    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:391
    public virtual async DmTask set_eye_color(object color_left = null, object color_right = null)
    {
        if (!Dm.truthy(color_right))
            color_right = color_left;
        eye_color_left = (string)color_left;
        eye_color_right = (string)color_right;
        // Doesn't assign eye color if they already have one from their type
        obj_item_organ_eyes eyes = get_organ_by_type(typeof(obj_item_organ_eyes)) as obj_item_organ_eyes;
        if (eyes is obj_item_organ_eyes && !Dm.truthy(Dm.initial(eyes, (obj_item_organ_eyes p) => p.eye_color_left)) && !Dm.truthy(Dm.initial(eyes, (obj_item_organ_eyes p) => p.eye_color_right)))
        {
            eyes.eye_color_left = color_left;
            eyes.eye_color_right = color_right;
            await eyes.refresh(this, FALSE);
        }
    }
}

// tg: code/modules/mob/living/carbon/human/human_helpers.dm:80
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:80
    public virtual object get_face_name(string if_no_face = null)
    {
        if_no_face ??= "Unknown";
        return real_name;
    }

    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:104
    public virtual object get_id_name(string if_no_id = null, float? honorifics = null)
    {
        if_no_id ??= "Unknown";
        honorifics ??= FALSE;
        return null;
    }
}

// M4a P5: tg's human get_idcard (the access chain is /mob/living/get_access's COMSIG_MOB_RETRIEVE_ACCESS, answered by the
// worn or held ID card, cards_ids.dm:245; M3's dev access list, DV23, is retired).
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/human_helpers.dm:117
    public override object get_idcard(object hand_first = null)
    {
        hand_first ??= TRUE;
        object dot = base.get_idcard(hand_first);
        if (Dm.truthy(dot) && Dm.truthy(hand_first))
            return dot;
        //Check inventory slots
        object worn = wear_id?.GetID();
        return Dm.truthy(worn) ? worn : belt?.GetID();
    }
}
