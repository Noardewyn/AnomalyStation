// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/bodyparts/_bodyparts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the M2 subset translated (M2 P2, scope.md 2e "bodyparts": Initialize, Destroy, receive_damage, the damage
// setters, update_disabled, the ownership procs, the damage state, the bleed stacks, the bodypart traits, the biostates and
// the surgical states). The appearance procs are M2 P3's (_bodyparts.icons.cs). M6 slice 2: receive_damage's wound and
// dismemberment rolls, the dismemberability checks, the wounds' blood flow, check_for_injuries and the surgery self-check,
// the internal and external descriptions. M6 slice 4: drop_organs. M6 slice 6: apply_item and Exited (the gauze). M7 EMP: emp_act and emp_effect. Examine, surgery, embedding and butchering come with M4a, M6 and M8 (their
// roots below stay Dm.NotTranslated).
// Only the members that the list, sort and comparator helpers (code/__HELPERS/_lists.dm, cmp.dm, sorts/) name;
// bodies are Dm.NotTranslated until then.

// tg: code/modules/surgery/bodyparts/_bodyparts.dm:1
// [DmPath("/obj/item/bodypart")] is on the part in code/datums/wounds/loss.dm, the earliest .dme file that declares
// /obj/item/bodypart (rules 2.3).
// The var block (_bodyparts.dm:1-244) is translated in full by the M2 contracts step; the procs are still skeletons.
public partial class obj_item_bodypart : obj_item
{
    // The constant var overrides of the type block (rules 10.2), in DM declaration order.
    public obj_item_bodypart()
    {
        // tg: code/modules/surgery/bodyparts/_bodyparts.dm:2
        name = "limb";
        // tg: code/modules/surgery/bodyparts/_bodyparts.dm:3
        desc = "Why is it detached...";
        // tg: code/modules/surgery/bodyparts/_bodyparts.dm:4
        abstract_type = typeof(obj_item_bodypart);
        // tg: code/modules/surgery/bodyparts/_bodyparts.dm:5
        force = 3;
        // tg: code/modules/surgery/bodyparts/_bodyparts.dm:6
        throwforce = 3;
        // tg: code/modules/surgery/bodyparts/_bodyparts.dm:7
        w_class = WEIGHT_CLASS_SMALL;
        // tg: code/modules/surgery/bodyparts/_bodyparts.dm:8
        icon = Dm.resource("icons/mob/human/bodyparts.dmi");
        // tg: code/modules/surgery/bodyparts/_bodyparts.dm:9
        icon_state = ""; //Leave this blank! Bodyparts are built using overlays
        // tg: code/modules/surgery/bodyparts/_bodyparts.dm:10
        flags_1 = PREVENT_CONTENTS_EXPLOSION_1; //actually mindblowing
        // tg: code/modules/surgery/bodyparts/_bodyparts.dm:26
        layer = BELOW_MOB_LAYER; //so it isn't hidden behind objects when on the floor
    }

    // The object-creating defaults of the var block (rules 10.2), in DM declaration order.
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        //Damage messages used by examine(). the desc that is most common accross all bodyparts gets shown
        // tg: code/modules/surgery/bodyparts/_bodyparts.dm:137
        damage_examines = Dm.list(
            Dm.kv(BRUTE, DEFAULT_BRUTE_EXAMINE_TEXT),
            Dm.kv(BURN, DEFAULT_BURN_EXAMINE_TEXT));
        // tg: code/modules/surgery/bodyparts/_bodyparts.dm:177
        unarmed_attack_verbs = Dm.list("bump");
        // tg: code/modules/surgery/bodyparts/_bodyparts.dm:179
        unarmed_attack_verbs_continuous = Dm.list("bumps");
        // tg: code/modules/surgery/bodyparts/_bodyparts.dm:213
        feature_offsets = Dm.list();
    }

    /// The icon for Organic limbs using greyscale
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:12  VAR_PROTECTED/icon_greyscale = DEFAULT_BODYPART_ICON_ORGANIC
    // (a resource #define, expanded inline: rules 3.7)
    [DmProtected]
    public object icon_greyscale = Dm.resource("icons/mob/human/bodyparts_greyscale.dmi");
    ///The icon for non-greyscale limbs
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:14  VAR_PROTECTED
    [DmProtected]
    public object icon_static = Dm.resource("icons/mob/human/bodyparts.dmi");
    ///The icon for husked limbs
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:16  VAR_PROTECTED
    [DmProtected]
    public object icon_husk = Dm.resource("icons/mob/human/bodyparts.dmi");
    ///The icon for invisible limbs
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:18  VAR_PROTECTED
    [DmProtected]
    public object icon_invisible = Dm.resource("icons/mob/human/bodyparts.dmi");
    ///The type of husk for building an iconstate
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:20
    public string husk_type = "humanoid";
    ///The color to multiply the greyscaled husk sprites by. Can be null. Old husk sprite chest color is #A6A6A6
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:22
    public string husk_color = "#A6A6A6";
    /// The color to multiply the zombie husk sprites by.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:24
    public string zombie_color = "#006009";

    // (layer = BELOW_MOB_LAYER, _bodyparts.dm:26: an override, in the constructor above)
    /// The mob that "owns" this limb
    /// DO NOT MODIFY DIRECTLY. Use update_owner()
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:29
    public mob_living_carbon owner;

    /// If this limb can be scarred.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:32
    public float scarrable = TRUE;

    /**
     * A bitfield of biological states, exclusively used to determine which wounds this limb will get,
     * as well as how easily it will happen.
     * Set to BIO_STANDARD_UNJOINTED because most species have both flesh bone and blood in their limbs.
     */
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:39
    public float biological_state = BIO_STANDARD_UNJOINTED;
    ///A bitfield of bodytypes for surgery, and misc information
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:41
    public float bodytype = BODYTYPE_ORGANIC;
    ///A bitfield of bodyshapes for clothing and other sprite information
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:43
    public float bodyshape = BODYSHAPE_HUMANOID;
    ///Defines when a bodypart should not be changed. Example: BP_BLOCK_CHANGE_SPECIES prevents the limb from being overwritten on species gain
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:45
    public float change_exempt_flags = NONE;
    ///Random flags that describe this bodypart
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:47
    public float bodypart_flags = NONE;
    ///Does this part have an internal or external anatomy biostate? Assigned on init based on biological_state
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:49  VAR_FINAL/bio_status = NONE
    [DmFinal]
    public float bio_status = NONE;
    ///Mangling state (interior, exterior) of the bodypart
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:51
    public float mangled_state = BODYPART_MANGLED_NONE;

    ///Whether the bodypart (and the owner) is husked.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:54
    public float is_husked = NONE;
    ///Whether the bodypart (and the owner) is invisible through invisibleman trait.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:56
    public float is_invisible = FALSE;
    ///The ID of a species used to generate the icon. Needs to match the icon_state portion in the limbs file!
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:58
    public string limb_id = SPECIES_HUMAN;
    ///ID of a species to use as an override for species-based biological logic, such as what species to pull the meat from, if our limb_id doesn't match
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:60
    public object species_id = null;
    //Defines what sprite the limb should use if it is also sexually dimorphic.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:62
    public string limb_gender = "m";
    ///Is there a sprite difference between male and female?
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:64
    public float is_dimorphic = FALSE;
    ///The actual color a limb is drawn as, set by /proc/update_limb()
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:66
    public object draw_color; //NEVER. EVER. EDIT THIS VALUE OUTSIDE OF UPDATE_LIMB. I WILL FIND YOU. It ruins the limb icon pipeline.
    ///If this limb should have emissive overlays
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:68
    public float is_emissive = FALSE;

    /// BODY_ZONE_CHEST, BODY_ZONE_L_ARM, etc , used for def_zone
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:71
    public object body_zone;
    /// The body zone of this part in english ("chest", "left arm", etc) without the species attached to it
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:73
    public object plaintext_zone;
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:74
    public object aux_zone; // used for hands
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:75
    public object aux_layer;
    /// bitflag used to check which clothes cover this bodypart
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:77
    public object body_part;
    /// List of obj/item's embedded inside us. Managed by embedded components, do not modify directly
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:79
    public DmList embedded_objects;
    /// are we a hand? if so, which one!
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:81
    public float held_index = 0;

    // Limb disabling variables
    ///Whether it is possible for the limb to be disabled whatsoever. TRUE means that it is possible.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:85
    public float can_be_disabled = FALSE; //Defaults to FALSE, as only human limbs can be disabled, and only the appendages.
    ///Controls if the limb is disabled. TRUE means it is disabled (similar to being removed, but still present for the sake of targeted interactions).
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:87
    public float bodypart_disabled = FALSE;
    ///Handles limb disabling by damage. If LIMB_NO_DISABLE (-1), a limb can't be disabled via damage. If 1 (100%), it is disabled at max limb damage. Anything between is the percentage of damage against maximum limb damage needed to disable the limb.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:89
    public float disabling_threshold_percentage = LIMB_NO_DISABLE;

    // Damage variables
    ///A mutiplication of the burn and brute damage that the limb's stored damage contributes to its attached mob's overall wellbeing.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:93
    public float body_damage_coeff = LIMB_BODY_DAMAGE_COEFFICIENT_TOTAL;
    ///The current amount of brute damage the limb has
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:95
    public float brute_dam = 0;
    ///The current amount of burn damage the limb has
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:97
    public float burn_dam = 0;
    ///The maximum brute OR burn damage a bodypart can take. Once we hit this cap, no more damage of either type!
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:99
    public float max_damage = 0;

    //Used in determining overlays for limb damage states. As the mob receives more burn/brute damage, their limbs update to reflect.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:102
    public float brutestate = 0;
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:103
    public float burnstate = 0;

    //Multiplicative damage modifiers
    /// Brute damage gets multiplied by this on receive_damage()
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:107
    public float brute_modifier = 1;
    /// Burn damage gets multiplied by this on receive_damage()
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:109
    public float burn_modifier = 1;

    //Coloring and proper item icon update
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:112
    public string skin_tone = "";
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:113
    public string species_color = "";
    ///Limbs need this information as a back-up incase they are generated outside of a carbon (limbgrower)
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:115
    public float should_draw_greyscale = TRUE;
    /// An assoc list of priority (as a string because byond) -> color, used to override draw_color.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:117
    public DmList color_overrides;

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:119
    public float px_x = 0;
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:120
    public float px_y = 0;

    ///the type of damage overlay (if any) to use when this bodypart is bruised/burned.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:123
    public string dmg_overlay_type = "human";
    /// If we're bleeding, which icon are we displaying on this part
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:125
    public object bleed_overlay_icon;

    //Damage messages used by help_shake_act()
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:128
    public string light_brute_msg = "bruised and feels sore";
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:129
    public string medium_brute_msg = "battered";
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:130
    public string heavy_brute_msg = "mangled";

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:132
    public string light_burn_msg = "red and feels numb";
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:133
    public string medium_burn_msg = "blistered";
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:134
    public string heavy_burn_msg = "like its peeling away";

    //Damage messages used by examine(). the desc that is most common accross all bodyparts gets shown
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:137 (list(...): dm_init_vars above)
    public DmList damage_examines;

    // Wounds related variables
    /// The wounds currently afflicting this body part
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:144
    public DmList wounds;

    /// The scars currently afflicting this body part
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:147
    public DmList scars;
    /// Our current stored wound damage multiplier
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:149
    public float wound_damage_multiplier = 1;

    /// This number is added to the effective wound armor on this body part (as long as it isn't managled externally or internally), higher numbers mean more defense, negative means easier to wound
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:152
    public float wound_resistance = 0;
    /// When this bodypart hits max damage, this number is added to all wound rolls. Obviously only relevant for bodyparts that have damage caps.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:154
    public float disabled_wound_penalty = 15;

    /// A hat won't cover your face, but a shirt covering your chest will cover your... you know, chest
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:157
    public float scars_covered_by_clothes = TRUE;
    /// So we know if we need to scream if this limb hits max damage
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:159
    public object last_maxed;
    /// Our current bleed rate. Cached, update with refresh_bleed_rate()
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:161
    public float cached_bleed_rate = 0;
    /// How much generic bleedstacks we have on this bodypart
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:163
    public object generic_bleedstacks;
    /// If something is currently grasping this bodypart and trying to staunch bleeding (see [/obj/item/hand_item/self_grasp])
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:165
    public obj_item_hand_item_self_grasp grasped_by;
    /// Lazylist of category to item applied to this limb
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:167
    public DmList applied_items;

    ///A list of all bodypart overlays to draw
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:170
    public DmList bodypart_overlays;
    ///A list of all bodypart textures to apply
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:172
    public DmList bodypart_textures;

    /// Type of an attack from this limb does. Arms will do punches, Legs for kicks, and head for bites. (TO ADD: tactical chestbumps)
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:175
    public string attack_type = BRUTE;
    /// the verbs used for an unarmed attack when using this limb, such as arm.unarmed_attack_verbs = list("punch")
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:177 (list(...): dm_init_vars above)
    public DmList unarmed_attack_verbs;
    /// Continuous tense attack verbs for successful attacks
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:179 (list(...): dm_init_vars above)
    public DmList unarmed_attack_verbs_continuous;
    /// if we have a special attack verb for hitting someone who is grappled by us, it goes here.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:181
    public object grappled_attack_verb;
    /// Continuous tense grapple verb for successful attacks
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:183
    public object grappled_attack_verb_continuous;
    /// what visual effect is used when this limb is used to strike someone.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:185
    public string unarmed_attack_effect = ATTACK_EFFECT_PUNCH;
    /// Sounds when this bodypart is used in an umarmed attack
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:187  var/sound/unarmed_attack_sound: object, its default is a
    // resource (rules 3.2, "Typed vars that hold wider values"; 3.7)
    public object unarmed_attack_sound = Dm.resource("sound/items/weapons/punch1.ogg");
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:188  var/sound/unarmed_miss_sound: object, as above
    public object unarmed_miss_sound = Dm.resource("sound/items/weapons/punchmiss.ogg");
    /// Lowest possible punch damage this bodypart can give. If this is set to 0, unarmed attacks will always miss.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:190
    public float unarmed_damage_low = 1;
    /// Highest possible punch damage this bodypart can ive.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:192
    public float unarmed_damage_high = 1;
    /// Determines the accuracy bonus, armor penetration and knockdown probability.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:194
    public float unarmed_effectiveness = 10;
    /// Multiplier applied to effectiveness and damage when attacking a grabbed target.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:196
    public float unarmed_pummeling_bonus = 1;
    /// If this limb is used to grab (which is only arms right now), how much more effective is the limb? Every integer above or below 0 is one effective grab level. Higher is a bonus, lower is a malus.
    /// This is more powerful than adjusting grab escape chance. If the arm has both an increased grab level and a chance reduction, it is very hard to escape a grab.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:199
    public float unarmed_grab_state_bonus = 0;
    /// If this limb is used to grab (which is only arms right now), how much additional damage does it deal to the grabbed individual when they fail to escape the grab?
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:201
    public float unarmed_grab_damage_bonus = 0;
    /// If this limb is used to grab (which is only arms right now), how much more difficult is it to escape the grab, before accounting for grab state? Values above 0 is a malus, values below 0 is a bonus.
    /// This is less powerful than adjusting grab state. If the arm has both an increased grab level and a chance reduction, it is very hard to escape a grab.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:204
    public float unarmed_grab_escape_chance_bonus = 0;
    /// The 'sharpness' of the limb. Could indicate claws, teeth or spines. Should default to NONE, or blunt.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:206
    public float unarmed_sharpness = NONE;

    /// Traits that are given to the holder of the part. This does not update automatically on life(), only when the organs are initially generated or inserted!
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:209
    public DmList bodypart_traits;
    /// The name of the trait source that the organ gives. Should not be altered during the events of gameplay, and will cause problems if it is.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:211
    public string bodypart_trait_source = BODYPART_TRAIT;
    /// List of the above datums which have actually been instantiated, managed automatically
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:213 (list(): dm_init_vars above)
    public DmList feature_offsets;

    /// In the case we dont have dismemberable features, or literally cant get wounds, we will use this percent to determine when we can be dismembered.
    /// Compared to our ABSOLUTE maximum. Stored in decimal; 0.8 = 80%.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:217
    public float hp_percent_to_dismemberable = 0.8f;
    /// If true, we will use [hp_percent_to_dismemberable] even if we are dismemberable via wounds. Useful for things with extreme wound resistance.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:219
    public float use_alternate_dismemberment_calc_even_if_mangleable = FALSE;
    /// If false, no wound that can be applied to us can mangle our exterior. Used for determining if we should use [hp_percent_to_dismemberable] instead of normal dismemberment.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:221
    public object any_existing_wound_can_mangle_our_exterior;
    /// If false, no wound that can be applied to us can mangle our interior. Used for determining if we should use [hp_percent_to_dismemberable] instead of normal dismemberment.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:223
    public object any_existing_wound_can_mangle_our_interior;
    /// get_damage() / total_damage must surpass this to allow our limb to be disabled, even temporarily, by an EMP.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:225
    public float robotic_emp_paralyze_damage_percent_threshold = 0.3f;
    /// A potential texturing overlay to put on the limb
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:227
    public datum_bodypart_texture texture_bodypart_overlay;
    /// Lazylist of /datum/status_effect/grouped/bodypart_effect types. Instances of this are applied to the carbon when added the limb is attached, and merged with similair limbs
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:229
    public DmList bodypart_effects;
    /// The cached info about the blood this organ belongs to, set during on_removal()
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:231
    public DmList blood_dna_info;
    /// Lazy assoc list of [item type] = [amount] that we drop when butchered.
    /// Overrides whatever we would drop normally based on our species datum.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:234
    public DmList butcher_drops_override;
    /// What skeleton limb, if any, we replace ourselves with when butchered?
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:236  var/obj/item/bodypart/butcher_replacement = null: Type, the
    // subtypes assign typepaths to it (V3)
    public Type butcher_replacement = null;
    /// How much meat do we add to butcher_drops when automatically generating them from our species datum?
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:238
    public float base_meat_amount = 1;
    /// Init cache for our butcher drops for sanic speed
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:240  var/static/list/butcher_drop_cache = list()  (rules 10.6: a
    // TgState field, below)
    public static ref DmList butcher_drop_cache => ref TgState.Current.obj_item_bodypart__butcher_drop_cache;
    /// What state is the bodypart in for determining surgery availability
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:242  VAR_FINAL/surgery_state = NONE
    [DmFinal]
    public float surgery_state = NONE;
    /// Typepath of this limb as a stump
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:244  (V3: the subtypes assign typepaths)
    public Type stump_typepath;
}

public sealed partial class TgState
{
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:240  var/static/list/butcher_drop_cache = list()
    internal DmList obj_item_bodypart__butcher_drop_cache = Dm.list();
}

// tg: code/modules/surgery/bodyparts/_bodyparts.dm:262
public partial class obj_item_bodypart : obj_item
{
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:262
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        Dm.combine(ref bodypart_flags, BODYPART_VIRGIN);

        if (Dm.truthy(can_be_disabled))
        {
            RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_PARALYSIS), nameof(on_paralysis_trait_gain));
            RegisterSignal(this, SIGNAL_REMOVETRAIT(TRAIT_PARALYSIS), nameof(on_paralysis_trait_loss));
        }

        RegisterSignal(this, COMSIG_ATOM_RESTYLE, nameof(on_attempt_feature_restyle)); // gaps OG1: restyling.dm

        if (texture_bodypart_overlay != null)
        {
            // texture_bodypart_overlay = new texture_bodypart_overlay(); add_bodypart_overlay(texture_bodypart_overlay,
            // update = FALSE): limb textures (the appearance, M2 P3); a human's limbs have none.
            Dm.NotTranslated("code/modules/surgery/bodyparts/_bodyparts.dm:273", "Initialize: the limb's texture overlay (appearance, M2 P3)");
        }

        if (Dm.truthy(IS_ORGANIC_LIMB(this)))
            blood_dna_info = Dm.list(Dm.kv("Unknown DNA", get_blood_type(typeof(datum_blood_type_human_o_plus))));

        set_bio_state_status();

        float innate_state = NONE;
        if (!Dm.truthy(LIMB_HAS_SKIN(this)))
            Dm.combine(ref innate_state, SKINLESS_SURGERY_STATES);
        if (!Dm.truthy(LIMB_HAS_BONES(this)))
            Dm.combine(ref innate_state, BONELESS_SURGERY_STATES);
        if (!Dm.truthy(LIMB_HAS_VESSELS(this)))
            Dm.combine(ref innate_state, VESSELLESS_SURGERY_STATES);
        if (Dm.truthy(innate_state))
            Waiting.NoSleep(add_surgical_state(innate_state));

        name = Dm.text("[] []", limb_id, parse_zone(body_zone));
        Waiting.NoSleep(update_limb(TRUE));
        Waiting.NoSleep(update_icon_dropped());
        refresh_bleed_rate();
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:296
    public override object Destroy(float? force = null)
    {
        if (owner != null && !QDELETED(owner))
        {
            Waiting.NoSleep(forced_removal(special: FALSE, dismembered: TRUE, move_to_floor: FALSE));
            Waiting.NoSleep(update_owner(null));
        }
        foreach (object wound in Dm.each(wounds))
            qdel(wound); // wounds is a lazylist, and each wound removes itself from it on deletion.
        if (Dm.truthy(length(wounds)))
        {
            stack_trace(Dm.text("[] qdeleted with [] uncleared wounds", type, length(wounds)), "code/modules/surgery/bodyparts/_bodyparts.dm", 303);
            wounds.Cut();
        }

        owner = null;

        if (Dm.truthy(LAZYLEN(applied_items)))
            QDEL_LIST_ASSOC_VAL(applied_items);
        QDEL_LAZYLIST(ref scars);

        // Overlays and textures may be owned by something else like a status effect,
        // so we'll just remove them all rather than delete them
        // Worst case scenario they'll just get swept up by GC and which is fine
        foreach (datum_bodypart_overlay remaining_overlay in Dm.each<datum_bodypart_overlay>(bodypart_overlays))
            Waiting.NoSleep(remove_bodypart_overlay(remaining_overlay, update: FALSE));
        foreach (datum_bodypart_texture remaining_texture in Dm.each<datum_bodypart_texture>(bodypart_textures))
            Waiting.NoSleep(remove_bodypart_texture(remaining_texture, update: FALSE));

        foreach (atom_movable movable in Dm.each<atom_movable>(contents))
            qdel(movable);

        QDEL_LIST_ASSOC_VAL(feature_offsets);

        return base.Destroy(force);
    }
}

// tg: code/modules/surgery/bodyparts/_bodyparts.dm:336
public partial class obj_item_bodypart : obj_item
{
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:336
    public virtual object get_butcher_drops()
    {
        float meat_to_spawn = max(base_meat_amount, Dm.values_sum(butcher_drops_override));
        if (meat_to_spawn <= 0)
            return null;
        if (Dm.eq(is_husked, HUSKED_ZOMBIE))
            return Dm.list(Dm.kv(typeof(obj_item_food_meat_slab_human_mutant_zombie), meat_to_spawn));
        if (Dm.truthy(length(butcher_drops_override)))
            return butcher_drops_override;
        // GLOB.species_list holds the species' typepaths (rules 3.2: typepaths read from a list), so species?.meat is the type's initial meat
        Type species = GLOB.species_list[Dm.truthy(species_id) ? species_id : limb_id] as Type;
        if (species != null && Dm.initial(species, (datum_species o) => o.meat) != null)
            return Dm.list(Dm.kv(Dm.initial(species, (datum_species o) => o.meat), meat_to_spawn));
        return null;
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:349
    [SignalHandler]
    public virtual void on_forced_removal(atom old_loc = null, object dir = null, object forced = null, DmList old_locs = null)
    {
        Waiting.NoSleep(forced_removal(special: FALSE, dismembered: TRUE, move_to_floor: FALSE));
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:355
    public virtual async DmTask forced_removal(object special = null, object dismembered = null, object move_to_floor = null)
    {
        await drop_limb(special, dismembered, Dm.num_or_null(move_to_floor));

        await update_icon_dropped();
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:360
    [ShouldCallParent]
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (brute_dam > DAMAGE_PRECISION)
            Dm.append(ref dot, span_warning(I18n.T("This limb has [] bruising.", brute_dam > 30 ? I18n.T("severe") : I18n.T("minor"))));
        if (burn_dam > DAMAGE_PRECISION)
            Dm.append(ref dot, span_warning(I18n.T("This limb has [] burns.", burn_dam > 30 ? I18n.T("severe") : I18n.T("minor"))));

        foreach (datum_wound wound in Dm.each_cast<datum_wound>(wounds))
        {
            object wound_desc = wound.get_limb_examine_description();
            if (Dm.truthy(wound_desc))
                Dm.append(ref dot, wound_desc);
        }

        object surgery_examine = get_surgery_examine();
        if (Dm.truthy(surgery_examine))
            Dm.append(ref dot, surgery_examine);
        return dot;
    }

    /**
     * Called when a bodypart is checked for injuries.
     */
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:381
    public virtual object check_for_injuries(mob_living_carbon_human examiner = null)
    {
        DmList check_list = Dm.list();
        DmList limb_damage = Dm.list(Dm.kv(BRUTE, brute_dam), Dm.kv(BURN, burn_dam));

        // SEND_SIGNAL(src, COMSIG_BODYPART_CHECKED_FOR_INJURY, examiner, check_list, limb_damage) (rules 11.1 (e), a statement)
        if (Dm.truthy(this._listen_lookup?[COMSIG_BODYPART_CHECKED_FOR_INJURY]))
            this._SendSignal(COMSIG_BODYPART_CHECKED_FOR_INJURY, Dm.list(this, examiner, check_list, limb_damage));
        // SEND_SIGNAL(examiner, COMSIG_CARBON_CHECKING_BODYPART, src, check_list, limb_damage) (rules 11.1 (e), a statement)
        if (Dm.truthy(examiner._listen_lookup?[COMSIG_CARBON_CHECKING_BODYPART]))
            examiner._SendSignal(COMSIG_CARBON_CHECKING_BODYPART, Dm.list(examiner, this, check_list, limb_damage));

        float shown_brute = Dm.num(limb_damage[BRUTE]);
        float shown_burn = Dm.num(limb_damage[BURN]);
        string status = "";
        bool self_aware = HAS_TRAIT(examiner, TRAIT_SELF_AWARE);

        if (self_aware)
        {
            if (!Dm.truthy(shown_brute) && !Dm.truthy(shown_burn))
                status = I18n.T("no damage");
            else
                status = I18n.T("[] brute damage and [] burn damage", shown_brute, shown_burn);
        }

        else
        {
            if (shown_brute > (max_damage * 0.8f))
                status += heavy_brute_msg;
            else if (shown_brute > (max_damage * 0.4f))
                status += medium_brute_msg;
            else if (shown_brute > DAMAGE_PRECISION)
                status += light_brute_msg;

            if (shown_brute > DAMAGE_PRECISION && shown_burn > DAMAGE_PRECISION)
                status += I18n.T(" and ");

            if (shown_burn > (max_damage * 0.8f))
                status += heavy_burn_msg;
            else if (shown_burn > (max_damage * 0.2f))
                status += medium_burn_msg;
            else if (shown_burn > DAMAGE_PRECISION)
                status += light_burn_msg;

            if (status == "")
                status = I18n.T("OK");
        }

        bool no_damage = false;
        if (status == I18n.T("OK") || status == I18n.T("no damage"))
            no_damage = true;

        string is_disabled = "";
        if (Dm.truthy(bodypart_disabled))
        {
            is_disabled = I18n.T(" is disabled");
            if (no_damage)
                is_disabled += I18n.T(" but otherwise");
            else
                is_disabled += I18n.T(" and");
        }

        Dm.append(ref check_list, I18n.T("<span class='[]'>Your [][][][].</span>", no_damage ? "notice" : "warning", plaintext_zone, is_disabled, self_aware ? I18n.T(" has ") : I18n.T(" looks "), status));

        bool adept_organ_feeler = owner == examiner && HAS_TRAIT(examiner, TRAIT_SELF_AWARE);
        foreach (obj_item_organ organ in Dm.each<obj_item_organ>(this))
        {
            if (Dm.truthy(Dm.bitand(organ.organ_flags, ORGAN_HIDDEN)))
                continue;
            object feeling = organ.feel_for_damage(adept_organ_feeler ? TRUE : FALSE);
            if (Dm.truthy(feeling))
                Dm.append(ref check_list, I18n.T("\t[]", feeling));
        }

        foreach (datum_wound wound in Dm.each_cast<datum_wound>(wounds))
        {
            object wound_desc = wound.get_self_check_description(adept_organ_feeler ? TRUE : FALSE);
            if (Dm.truthy(wound_desc))
                Dm.append(ref check_list, I18n.T("\t[]", wound_desc));
        }

        object surgery_check = get_surgery_self_check();
        if (Dm.truthy(surgery_check))
            Dm.append(ref check_list, I18n.T("\t[]", surgery_check));

        foreach (obj_item embedded_thing in Dm.each_cast<obj_item>(embedded_objects))
        {
            datum_embedding thing_embed = (datum_embedding)embedded_thing.get_embed();
            if (Dm.truthy(thing_embed.stealthy_embed))
                continue;
            bool harmless = Dm.truthy(thing_embed.is_harmless());
            string stuck_wordage = harmless ? I18n.T("stuck to") : I18n.T("embedded in");
            // [icon2html(embedded_thing, examiner)]: the item's picture in the chat line (chat icons, M9), as the tourniquet's below
            Dm.Deferred("code/modules/surgery/bodyparts/_bodyparts.dm:451", "check_for_injuries: icon2html of an embedded object, its picture in the chat line (chat icons, M10+)");
            string embed_text = I18n.T("\t<a href='byond://?src=[];embedded_object=[];embedded_limb=[]'>There is [] \\a [] [] your []!</a>", REF(examiner), REF(embedded_thing), REF(this), "", embedded_thing, stuck_wordage, plaintext_zone);
            if (harmless)
                Dm.append(ref check_list, span_italics(span_notice(embed_text)));
            else
                Dm.append(ref check_list, span_boldwarning(embed_text));
        }

        obj_item_stack_medical_wrap current_gauze = LAZYACCESS(applied_items, LIMB_ITEM_GAUZE) as obj_item_stack_medical_wrap;
        obj_item_tourniquet current_tourniquet = LAZYACCESS(applied_items, LIMB_ITEM_TOURNIQUET) as obj_item_tourniquet;
        if (current_tourniquet != null)
        {
            // [icon2html(current_tourniquet, examiner)]: the item's picture in the chat line (the 3D client's chat is text: chat icons, M9)
            Dm.Deferred("code/modules/surgery/bodyparts/_bodyparts.dm:457", "check_for_injuries: icon2html of the tourniquet, its picture in the chat line (chat icons, M10+)");
            string tourniquet_href = I18n.T("<a href='byond://?src=[];remove_tourniquet=[]'>[] \\a []</a>", REF(owner), REF(this), "", current_tourniquet);
            string tourniquet_text = I18n.T("\tThere is [] tightly secured around []", tourniquet_href, Dm.eq(body_zone, BODY_ZONE_HEAD) ? I18n.T("your neck!") : I18n.T("it."));
            if (Dm.eq(body_zone, BODY_ZONE_HEAD))
                Dm.append(ref check_list, span_boldwarning(tourniquet_text));
            else
                Dm.append(ref check_list, span_warning(tourniquet_text));
        }
        if (current_gauze != null)
        {
            // [icon2html(current_gauze, examiner)]: as the tourniquet's (chat icons, M9)
            Dm.Deferred("code/modules/surgery/bodyparts/_bodyparts.dm:464", "check_for_injuries: icon2html of the gauze, its picture in the chat line (chat icons, M10+)");
            string gauze_href = I18n.T("<a href='byond://?src=[];gauze_limb=[]'>[] \\a []</a>", REF(examiner), REF(this), "", current_gauze);
            string gauze_text = I18n.T("\tThere is [] wrapped around your [].", gauze_href, name);
            Dm.append(ref check_list, span_notice(gauze_text));
        }

        else if (Dm.truthy(can_bleed()))
        {
            string bleed_text = "";
            float sw__1 = cached_bleed_rate;
            if (0.2f <= sw__1 && sw__1 <= 1)
                bleed_text = span_warning(I18n.T("It's lightly bleeding."));
            else if (1 <= sw__1 && sw__1 <= 2)
                bleed_text = span_warning(I18n.T("It's bleeding."));
            else if (3 <= sw__1 && sw__1 <= 4)
                bleed_text = span_warning(I18n.T("It's bleeding heavily!"));
            else if (4 <= sw__1)
                bleed_text = span_warning(I18n.T("It's bleeding profusely!"));

            if (Dm.truthy(bleed_text))
                Dm.append(ref check_list, I18n.T("\t[]", span_tooltip(I18n.T("You are loosing blood. You should wrap your limb in gauze or apply pressure to it by grabbing yourself (while targeting the limb) to stem the flow."), bleed_text)));
        }

        return jointext(check_list, "<br>");
    }

    /// Returns all surgical states, filtering out stuff which should not be reported
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:496
    public virtual object get_reported_surgery_state()
    {
        float reported_state = surgery_state;
        if (!Dm.truthy(LIMB_HAS_SKIN(this)))
            reported_state = Dm.bitand(reported_state, Dm.bitnot(SKINLESS_SURGERY_STATES));
        if (!Dm.truthy(LIMB_HAS_BONES(this)))
            reported_state = Dm.bitand(reported_state, Dm.bitnot(BONELESS_SURGERY_STATES));
        if (!Dm.truthy(LIMB_HAS_VESSELS(this)))
            reported_state = Dm.bitand(reported_state, Dm.bitnot(VESSELLESS_SURGERY_STATES));

        // hide surgical states applied by wounds if the limb isn't being operated on, to keep it simple
        if (!HAS_TRAIT(this, TRAIT_READY_TO_OPERATE))
        {
            foreach (datum_wound wound in Dm.each_cast<datum_wound>(wounds))
                reported_state = Dm.bitand(reported_state, Dm.bitnot(wound.surgery_states));
        }

        return reported_state;
    }

    /// Returns surgery self-check information for this bodypart
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:513
    public virtual object get_surgery_self_check()
    {
        DmList surgery_message = Dm.list();
        float reported_state = Dm.num(get_reported_surgery_state());

        if (HAS_SURGERY_STATE(reported_state, SURGERY_SKIN_CUT))
            Dm.append(ref surgery_message, I18n.T("skin has been incised"));
        if (HAS_SURGERY_STATE(reported_state, SURGERY_SKIN_OPEN))
            Dm.append(ref surgery_message, I18n.T("skin is opened"));

        // We can only see these if the skin is open
        // And we check the real state rather than reported_state
        if (Dm.truthy(LIMB_HAS_ANY_SURGERY_STATE(this, ALL_SURGERY_SKIN_STATES)))
        {
            if (HAS_SURGERY_STATE(reported_state, SURGERY_VESSELS_UNCLAMPED))
                Dm.append(ref surgery_message, I18n.T("blood vessels are unclamped and bleeding"));
            if (HAS_SURGERY_STATE(reported_state, SURGERY_VESSELS_CLAMPED))
                Dm.append(ref surgery_message, I18n.T("blood vessels are clamped shut"));
            if (HAS_SURGERY_STATE(reported_state, SURGERY_ORGANS_CUT))
                Dm.append(ref surgery_message, I18n.T("organs have been incised"));
            if (HAS_SURGERY_STATE(reported_state, SURGERY_BONE_SAWED))
                Dm.append(ref surgery_message, I18n.T("bones have been sawed apart"));
            if (HAS_SURGERY_STATE(reported_state, SURGERY_BONE_DRILLED))
                Dm.append(ref surgery_message, I18n.T("bones have been drilled through"));
        }

        if (HAS_SURGERY_STATE(reported_state, SURGERY_PROSTHETIC_UNSECURED))
            Dm.append(ref surgery_message, I18n.T("prosthetic item is unsecured"));
        if (HAS_SURGERY_STATE(reported_state, SURGERY_PLASTIC_APPLIED))
            Dm.append(ref surgery_message, I18n.T("got a layer of plastic applied to it"));
        if (HAS_SURGERY_STATE(reported_state, SURGERY_CAVITY_WIDENED))
            Dm.append(ref surgery_message, I18n.T("chest cavity is wide open"));

        if (Dm.truthy(length(surgery_message)))
            return span_tooltip(I18n.T("Your limb is undergoing surgery. If no doctors are around, you could suture or cauterize yourself to cancel it."), span_smalldanger(I18n.T("Its []!", english_list(surgery_message))));
        return "";
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:549
    public virtual object get_surgery_examine()
    {
        object lowercase_zone = owner != null ? Dm.text("[] []", owner.p_their(), plaintext_zone) : Dm.text("[]", this);
        object capital_zone = owner != null ? Dm.text("[] []", owner.p_Their(), plaintext_zone) : capitalize(Dm.text("[]", this));
        string single_message = "";
        DmList sub_messages = Dm.list();
        float reported_state = Dm.num(get_reported_surgery_state());

        if (HAS_SURGERY_STATE(reported_state, SURGERY_SKIN_CUT))
        {
            Dm.append(ref sub_messages, I18n.T("skin has been incised"));
            single_message = I18n.T("The skin on [] has been incised.", lowercase_zone);
        }
        if (HAS_SURGERY_STATE(reported_state, SURGERY_SKIN_OPEN))
        {
            Dm.append(ref sub_messages, I18n.T("skin is opened"));
            single_message = I18n.T("The skin on [] is opened.", lowercase_zone);
        }

        // We can only see these if the skin is open
        // And we check the real state rather than reported_state
        if (Dm.truthy(LIMB_HAS_ANY_SURGERY_STATE(this, ALL_SURGERY_SKIN_STATES)))
        {
            if (HAS_SURGERY_STATE(reported_state, SURGERY_VESSELS_UNCLAMPED))
            {
                Dm.append(ref sub_messages, I18n.T("blood vessels are unclamped[]", Dm.truthy(cached_bleed_rate) ? I18n.T(" and bleeding") : ""));
                single_message = I18n.T("The blood vessels in [] are unclamped[]", lowercase_zone, Dm.truthy(cached_bleed_rate) ? I18n.T(" and bleeding!") : ".");
            }
            if (HAS_SURGERY_STATE(reported_state, SURGERY_VESSELS_CLAMPED))
            {
                Dm.append(ref sub_messages, I18n.T("blood vessels are clamped shut"));
                single_message = I18n.T("The blood vessels in [] are clamped shut.", lowercase_zone);
            }
            if (HAS_SURGERY_STATE(reported_state, SURGERY_ORGANS_CUT))
            {
                Dm.append(ref sub_messages, I18n.T("the organs within have been incised"));
                single_message = I18n.T("The organs in [] have been incised.", lowercase_zone);
            }
            if (HAS_SURGERY_STATE(reported_state, SURGERY_BONE_SAWED))
            {
                Dm.append(ref sub_messages, I18n.T("the bones within have been sawed apart"));
                single_message = I18n.T("The bones in [] have been sawed apart.", lowercase_zone);
            }
            if (HAS_SURGERY_STATE(reported_state, SURGERY_BONE_DRILLED))
            {
                Dm.append(ref sub_messages, I18n.T("the bones within have been drilled through"));
                single_message = I18n.T("The bones in [] have been drilled through.", lowercase_zone);
            }
        }

        if (HAS_SURGERY_STATE(reported_state, SURGERY_PROSTHETIC_UNSECURED))
        {
            Dm.append(ref sub_messages, I18n.T("prosthetic item is unsecured"));
            single_message = I18n.T("[] is unsecured and loose!", capital_zone);
        }
        if (HAS_SURGERY_STATE(reported_state, SURGERY_PLASTIC_APPLIED))
        {
            Dm.append(ref sub_messages, I18n.T("got a layer of plastic applied to it"));
            single_message = I18n.T("A layer of plastic has been applied to [].", lowercase_zone);
        }
        if (HAS_SURGERY_STATE(reported_state, SURGERY_CAVITY_WIDENED))
        {
            Dm.append(ref sub_messages, I18n.T("the chest cavity is wide open"));
            single_message = I18n.T("[] chest cavity is wide open!", owner != null ? owner.p_Their() : "The");
        }

        if (Dm.length(sub_messages) >= 2)
            return span_smalldanger(I18n.T("[]'s [].", capital_zone, english_list(sub_messages)));
        if (Dm.truthy(single_message))
            return span_smalldanger(single_message);
        return "";
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:601
    [ShouldCallParent]
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        mob_living_carbon victim = target_mob as mob_living_carbon;
        if (ishuman(victim))
        {
            mob_living_carbon_human human_victim = (mob_living_carbon_human)victim;
            if (HAS_TRAIT(victim, TRAIT_LIMBATTACHMENT) || HAS_TRAIT(this, TRAIT_EASY_ATTACH))
            {
                if (!Dm.truthy(human_victim.get_bodypart((string)body_zone)))
                {
                    await user.temporarilyRemoveItemFromInventory(this, TRUE);
                    if (!Dm.truthy(await try_attach_limb(victim)))
                    {
                        to_chat(user, span_warning(I18n.T("[]'s body rejects []!", human_victim, this)));
                        await forceMove(human_victim.loc);
                        return null;
                    }
                    if (Dm.truthy(check_for_frankenstein(victim as mob_living_carbon_human)))
                        Dm.combine(ref bodypart_flags, BODYPART_IMPLANTED);
                    if (human_victim == user)
                        human_victim.visible_message(span_warning(I18n.T("[] jams [] into [] empty socket!", human_victim, this, human_victim.p_their())),
                            span_notice(I18n.T("You force [] into your empty socket, and it locks into place!", this)));
                    else
                        human_victim.visible_message(span_warning(I18n.T("[] jams [] into []'s empty socket!", user, this, human_victim)),
                            span_notice(I18n.T("[] forces [] into your empty socket, and it locks into place!", user, this)));
                    return null;
                }
            }
        }
        return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:633
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;

        object operation_zone = user.zone_selected;
        if (!Dm.eq(deprecise_zone(operation_zone), body_zone))
            operation_zone = body_zone;
        return await user.perform_surgery(this, tool, Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)) ? TRUE : FALSE, operation_zone);
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:624  (D296's sound audit: a thrown or severed limb lands with a splort)
    [ShouldCallParent]
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        object dot = base.throw_impact(hit_atom, throwingdatum, __extra);
        if (Dm.truthy(IS_ORGANIC_LIMB(this)))
            playsound(get_turf(this), Dm.resource("sound/misc/splort.ogg"), 50, TRUE, -1);
        pixel_x = rand(-3, 3);
        pixel_y = rand(-3, 3);
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:642
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);
        DmList operations = (DmList)GLOB.operations.get_instances_from(GLOB.operations.unlocked);
        object operation_zone = user.zone_selected;
        if (!Dm.eq(deprecise_zone(operation_zone), body_zone))
            operation_zone = body_zone;
        foreach (datum_surgery_operation operation in Dm.each_cast<datum_surgery_operation>(operations))
        {
            if (Dm.truthy(Dm.bitand(operation.operation_flags, OPERATION_NO_PATIENT_REQUIRED)) && Dm.truthy(operation.show_as_next_step(this, operation_zone)))
                Dm.append(ref dot, span_notice(I18n.T("You could perform [] on [] with \\a []...", operation, this, operation.get_recommended_tool())));
        }
        return dot;
    }

    //empties the bodypart from its organs and other things inside it
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:653
    [ShouldCallParent]
    public virtual async DmTask drop_organs(mob user = null, object violent_removal = null)
    {
        atom drop_loc = drop_location() as atom;
        bool play_sfx = false;

        foreach (obj_item_organ bodypart_organ in Dm.each<obj_item_organ>(contents))
        {
            if (Dm.truthy(Dm.bitand(bodypart_organ.organ_flags, ORGAN_UNREMOVABLE)))
                continue;

            if (Dm.truthy(violent_removal))
                await bodypart_organ.apply_organ_damage(bodypart_organ.maxHealth * 0.5f);

            if (owner != null)
                await bodypart_organ.Remove(bodypart_organ.owner);
            else if (!Dm.truthy(await bodypart_organ.bodypart_remove(this)))
                continue;

            if (drop_loc != null) //can be null if being deleted
            {
                await bodypart_organ.forceMove(get_turf(drop_loc));
                play_sfx = true;
            }
        }

        if (drop_loc != null) //can be null during deletion
        {
            foreach (atom_movable movable in Dm.each_cast<atom_movable>(contents))
            {
                await movable.forceMove(drop_loc);
                play_sfx = true;
            }
        }

        if (play_sfx && Dm.truthy(IS_ORGANIC_LIMB(this)))
            playsound(drop_loc, Dm.resource("sound/misc/splort.ogg"), 50, TRUE, -1);

        await update_icon_dropped();
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:704
    [ShouldCallParent]
    public virtual async DmTask<object> receive_damage(float? brute = null, float? burn = null, float? blocked = null, float? updating_health = null, float? forced = null, object required_bodytype = null, float? wound_bonus = null, float? exposed_wound_bonus = null, float? sharpness = null, object attack_direction = null, object damage_source = null, float? wound_clothing = null)
    {
        brute ??= 0;
        burn ??= 0;
        blocked ??= 0;
        updating_health ??= TRUE;
        forced ??= FALSE;
        wound_bonus ??= 0;
        exposed_wound_bonus ??= 0;
        sharpness ??= NONE;
        wound_clothing ??= TRUE;

        float hit_percent = (Dm.truthy(forced) ? 1 : (100 - Dm.num(blocked)) / 100);
        if ((!Dm.truthy(brute) && !Dm.truthy(burn)) || hit_percent <= 0)
            return FALSE;
        if (!Dm.truthy(forced))
        {
            if (owner != null)
            {
                if (HAS_TRAIT(owner, TRAIT_GODMODE))
                    return FALSE;
                // SEND_SIGNAL(owner, COMSIG_CARBON_LIMB_DAMAGED, src, brute, burn) (rules 11.1 (e), a value)
                if (Dm.truthy(Dm.bitand((!Dm.truthy(owner._listen_lookup?[COMSIG_CARBON_LIMB_DAMAGED]) ? NONE : owner._SendSignal(COMSIG_CARBON_LIMB_DAMAGED, Dm.list(owner, this, brute, burn))), COMPONENT_PREVENT_LIMB_DAMAGE)))
                    return FALSE;
            }
            if (Dm.truthy(required_bodytype) && !Dm.truthy(Dm.bitand(bodytype, required_bodytype)))
                return FALSE;
        }

        float dmg_multi = Dm.num(config.Get(typeof(datum_config_entry_number_damage_multiplier))) * hit_percent;
        brute = round(max(Dm.num(brute) * dmg_multi * brute_modifier, 0), DAMAGE_PRECISION);
        burn = round(max(Dm.num(burn) * dmg_multi * burn_modifier, 0), DAMAGE_PRECISION);

        if (!Dm.truthy(brute) && !Dm.truthy(burn))
            return FALSE;

        brute = Dm.num(brute) * wound_damage_multiplier;
        burn = Dm.num(burn) * wound_damage_multiplier;

        /*
        // START WOUND HANDLING
        */

        // what kind of wounds we're gonna roll for, take the greater between brute and burn, then if it's brute, we subdivide based on sharpness
        string wounding_type = (Dm.num(brute) > Dm.num(burn) ? WOUND_BLUNT : WOUND_BURN);
        float wounding_dmg = max(Dm.num(brute), Dm.num(burn));

        if (wounding_type == WOUND_BLUNT && Dm.truthy(sharpness))
        {
            if (Dm.truthy(Dm.bitand(sharpness, SHARP_EDGED)))
                wounding_type = WOUND_SLASH;
            else if (Dm.truthy(Dm.bitand(sharpness, SHARP_POINTY)))
                wounding_type = WOUND_PIERCE;
        }

        if (owner != null) // i tried to modularize the below, but the modifications to wounding_dmg and wounding_type cant be extracted to a proc
        {
            if (!Dm.truthy(forced))
            {
                brute = Dm.num(brute) * Dm.num(GET_PHYSIOLOGY(owner, BRUTE));
                burn = Dm.num(burn) * Dm.num(GET_PHYSIOLOGY(owner, BURN));
            }
            bool easy_dismember = HAS_TRAIT(owner, TRAIT_EASYDISMEMBER); // if we have easydismember, we don't reduce damage when redirecting damage to different types (slashing weapons on mangled/skinless limbs attack at 100% instead of 50%)

            float has_exterior = Dm.bitand(bio_status, ANATOMY_EXTERIOR);
            float has_interior = Dm.bitand(bio_status, ANATOMY_INTERIOR);

            bool exterior_ready_to_dismember = (!Dm.truthy(has_exterior) || Dm.truthy(Dm.bitand(mangled_state, BODYPART_MANGLED_EXTERIOR)));
            bool interior_ready_to_dismember = (!Dm.truthy(has_interior) || Dm.truthy(Dm.bitand(mangled_state, BODYPART_MANGLED_INTERIOR)));

            // if we're bone only, all cutting attacks go straight to the bone
            if (!Dm.truthy(has_exterior) && Dm.truthy(has_interior))
            {
                if (wounding_type == WOUND_SLASH)
                {
                    wounding_type = WOUND_BLUNT;
                    wounding_dmg *= (easy_dismember ? 1 : 0.6f);
                }
                else if (wounding_type == WOUND_PIERCE)
                {
                    wounding_type = WOUND_BLUNT;
                    wounding_dmg *= (easy_dismember ? 1 : 0.75f);
                }
            }
            else
            {
                // if we've already mangled the skin (critical slash or piercing wound), then the bone is exposed, and we can damage it with sharp weapons at a reduced rate
                // So a big sharp weapon is still all you need to destroy a limb
                if (Dm.truthy(has_interior) && exterior_ready_to_dismember && !Dm.truthy(Dm.bitand(mangled_state, BODYPART_MANGLED_INTERIOR)) && Dm.truthy(sharpness))
                {
                    if (wounding_type == WOUND_SLASH && !easy_dismember)
                        wounding_dmg *= 0.6f; // edged weapons pass along 60% of their wounding damage to the bone since the power is spread out over a larger area
                    if (wounding_type == WOUND_PIERCE && !easy_dismember)
                        wounding_dmg *= 0.75f; // piercing weapons pass along 75% of their wounding damage to the bone since it's more concentrated
                    wounding_type = WOUND_BLUNT;
                }
            }

            if (((exterior_ready_to_dismember && interior_ready_to_dismember) || Dm.truthy(dismemberable_by_total_damage())) && Dm.truthy(await try_dismember(wounding_type, wounding_dmg, wound_bonus, exposed_wound_bonus)))
                return null;
            // now we have our wounding_type and are ready to carry on with wounds and dealing the actual damage
            if (wounding_dmg >= WOUND_MINIMUM_DAMAGE && wound_bonus != CANT_WOUND)
                await check_wounding(wounding_type, wounding_dmg, wound_bonus, exposed_wound_bonus, attack_direction, damage_source: damage_source, wound_clothing: wound_clothing);
        }

        foreach (datum_wound iter_wound in Dm.each_cast<datum_wound>(wounds))
            await iter_wound.receive_damage(wounding_type, wounding_dmg, wound_bonus, attack_direction, damage_source);

        /*
        // END WOUND HANDLING
        */

        //back to our regularly scheduled program, we now actually apply damage if there's room below limb damage cap
        float can_inflict = max_damage - Dm.num(get_damage());
        float total_damage = Dm.num(brute) + Dm.num(burn);
        if (total_damage > can_inflict && total_damage > 0) // TODO: the second part of this check should be removed once disabling is all done
        {
            brute = round(Dm.num(brute) * (can_inflict / total_damage), DAMAGE_PRECISION);
            burn = round(Dm.num(burn) * (can_inflict / total_damage), DAMAGE_PRECISION);
        }

        if (can_inflict <= 0)
            return FALSE;
        if (Dm.truthy(brute))
            set_brute_dam(brute_dam + Dm.num(brute));
        if (Dm.truthy(burn))
            set_burn_dam(burn_dam + Dm.num(burn));

        if (owner != null)
        {
            if (Dm.truthy(can_be_disabled))
                await update_disabled();
            if (Dm.truthy(updating_health))
                await owner.updatehealth();
        }
        return update_bodypart_damage_state();
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:808
    public virtual void set_bio_state_status()
    {
        if (Dm.truthy(bio_status))
            throw Dm.CRASH(Dm.text("set_bio_state_status() called on [] ([]) bodypart with bio_status already set! Most likely bio_status was set on the type itself", this, type), "code/modules/surgery/bodyparts/_bodyparts.dm", 810);

        foreach (object state in Dm.each(GLOB.bio_state_anatomy))
        {
            object flag = text2num(state);
            if (!Dm.truthy(Dm.bitand(biological_state, flag)))
                continue;

            object value = GLOB.bio_state_anatomy[state];
            if (Dm.truthy(Dm.bitand(value, ANATOMY_EXTERIOR)))
                Dm.combine(ref bio_status, ANATOMY_EXTERIOR);
            if (Dm.truthy(Dm.bitand(value, ANATOMY_INTERIOR)))
                Dm.combine(ref bio_status, ANATOMY_INTERIOR);

            if (Dm.eq(Dm.bitand(bio_status, ANATOMY_EXTERIOR_AND_INTERIOR), ANATOMY_EXTERIOR_AND_INTERIOR))
                break;
        }
    }

    /// Returns if our current mangling status allows us to be dismembered. Requires both no exterior/mangled exterior and no interior/mangled interior.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:827
    public virtual object dismemberable_by_wound()
    {
        float has_exterior = Dm.bitand(bio_status, ANATOMY_EXTERIOR);
        float has_interior = Dm.bitand(bio_status, ANATOMY_INTERIOR);

        bool exterior_ready_to_dismember = (!Dm.truthy(has_exterior) || Dm.truthy(Dm.bitand(mangled_state, BODYPART_MANGLED_EXTERIOR)));
        bool interior_ready_to_dismember = (!Dm.truthy(has_interior) || Dm.truthy(Dm.bitand(mangled_state, BODYPART_MANGLED_INTERIOR)));

        return (exterior_ready_to_dismember && interior_ready_to_dismember) ? TRUE : FALSE;
    }

    /// Returns TRUE if our total percent damage is more or equal to our dismemberable percentage, but FALSE if a wound can cause us to be dismembered.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:839
    public virtual object dismemberable_by_total_damage()
    {
        update_wound_theory();

        float has_interior = Dm.bitand(bio_status, ANATOMY_INTERIOR);
        bool can_theoretically_be_dismembered_by_wound = (Dm.truthy(any_existing_wound_can_mangle_our_interior) || (Dm.truthy(any_existing_wound_can_mangle_our_exterior) && Dm.truthy(has_interior)));

        if (Dm.truthy(use_alternate_dismemberment_calc_even_if_mangleable) || (!Dm.truthy(dismemberable_by_wound()) && !can_theoretically_be_dismembered_by_wound))
        {
            float percent_to_total_max = (Dm.num(get_damage()) / max_damage);
            if (percent_to_total_max >= Dm.num(hp_percent_to_dismemberable))
                return TRUE;
        }

        return FALSE;
    }

    /// Updates our "can be theoretically dismembered by wounds" variables by iterating through all wound static data.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:853
    public virtual void update_wound_theory()
    {
        // We put this here so we dont increase init time by doing this all at once on initialization
        // Effectively, we "lazy load"
        if (any_existing_wound_can_mangle_our_interior != null && any_existing_wound_can_mangle_our_exterior != null)
            return;

        any_existing_wound_can_mangle_our_interior = FALSE;
        any_existing_wound_can_mangle_our_exterior = FALSE;

        if (!Dm.truthy(is_woundable()))
            return;

        foreach (Type wound_type in Dm.each_cast<Type>(GLOB.all_wound_pregen_data))
        {
            datum_wound_pregen_data pregen_data = (datum_wound_pregen_data)GLOB.all_wound_pregen_data[wound_type];
            /// We only consider randoms because non-randoms are usually really specific
            if (!Dm.truthy(pregen_data.can_be_applied_to(this, random_roll: TRUE, duplicates_allowed: TRUE, care_about_existing_wounds: FALSE)))
                continue;
            if (Dm.truthy(Dm.bitand(Dm.initial(pregen_data.wound_path_to_generate, (datum_wound p) => p.wound_flags), MANGLES_EXTERIOR)))
                any_existing_wound_can_mangle_our_exterior = TRUE;
            if (Dm.truthy(Dm.bitand(Dm.initial(pregen_data.wound_path_to_generate, (datum_wound p) => p.wound_flags), MANGLES_INTERIOR)))
                any_existing_wound_can_mangle_our_interior = TRUE;
            if (Dm.truthy(any_existing_wound_can_mangle_our_interior) && Dm.truthy(any_existing_wound_can_mangle_our_exterior))
                break;
        }
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:878
    public virtual object is_woundable()
    {
        if (owner == null)
            return FALSE;
        if (HAS_TRAIT(owner, TRAIT_NEVER_WOUNDED) || HAS_TRAIT(owner, TRAIT_GODMODE))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:888
    [ShouldCallParent]
    public virtual async DmTask<object> heal_damage(object brute = null, object burn = null, float? updating_health = null, float? forced = null, object required_bodytype = null)
    {
        updating_health ??= TRUE;
        forced ??= FALSE;

        if (!Dm.truthy(forced) && Dm.truthy(required_bodytype) && !Dm.truthy(Dm.bitand(bodytype, required_bodytype))) //So we can only heal certain kinds of limbs, ie robotic vs organic.
            return null;

        if (Dm.truthy(brute))
            set_brute_dam(round(max(brute_dam - Dm.num(brute), 0), DAMAGE_PRECISION));
        if (Dm.truthy(burn))
            set_burn_dam(round(max(burn_dam - Dm.num(burn), 0), DAMAGE_PRECISION));

        if (owner != null)
        {
            if (Dm.truthy(can_be_disabled))
                await update_disabled();
            if (Dm.truthy(updating_health))
                await owner.updatehealth();
        }
        return update_bodypart_damage_state();
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:907
    public virtual void set_initial_damage(object brute_damage = null, object burn_damage = null)
    {
        set_brute_dam(brute_damage);
        set_burn_dam(burn_damage);
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:912
    [DmProtected]
    public virtual object set_brute_dam(object new_value = null)
    {
        object dot = null;
        if (Dm.eq(brute_dam, new_value))
            return dot;
        dot = brute_dam;
        brute_dam = Dm.num(new_value);
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:921
    [DmProtected]
    public virtual object set_burn_dam(object new_value = null)
    {
        object dot = null;
        if (Dm.eq(burn_dam, new_value))
            return dot;
        dot = burn_dam;
        burn_dam = Dm.num(new_value);
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:930
    public virtual object get_damage()
    {
        return brute_dam + burn_dam;
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:934
    [ShouldCallParent]
    public virtual async DmTask update_disabled(float? update_limbs = null)
    {
        update_limbs ??= TRUE;

        if (owner == null)
            return;

        if (!Dm.truthy(can_be_disabled))
        {
            await set_disabled(FALSE, update_limbs);
            throw Dm.CRASH("update_disabled called with can_be_disabled false", "code/modules/surgery/bodyparts/_bodyparts.dm", 942);
        }

        if (HAS_TRAIT(this, TRAIT_PARALYSIS))
        {
            await set_disabled(TRUE, update_limbs);
            return;
        }

        float total_damage = brute_dam + burn_dam;

        // this block of checks is for limbs that can be disabled, but not through pure damage (AKA limbs that suffer wounds, human/monkey parts and such)
        if (disabling_threshold_percentage == LIMB_NO_DISABLE)
        {
            if (total_damage < max_damage)
                last_maxed = FALSE;
            else
            {
                if (!Dm.truthy(last_maxed) && !IS_UNCONSCIOUS(owner))
                {
                    Waiting.Detach(owner.emote("scream")); // INVOKE_ASYNC
                }
                last_maxed = TRUE;
            }
            await set_disabled(FALSE, update_limbs); // we only care about the paralysis trait
            return;
        }

        // we're now dealing solely with limbs that can be disabled through pure damage, AKA robot parts
        if (total_damage >= max_damage * disabling_threshold_percentage)
        {
            if (!Dm.truthy(last_maxed))
            {
                if (!IS_UNCONSCIOUS(owner))
                    Waiting.Detach(owner.emote("scream")); // INVOKE_ASYNC
                last_maxed = TRUE;
            }
            await set_disabled(TRUE, update_limbs);
            return;
        }

        if (Dm.truthy(bodypart_disabled) && total_damage < max_damage * disabling_threshold_percentage * 0.5f) // reenable the limb at 50% of the threshold
        {
            last_maxed = FALSE;
            await set_disabled(FALSE, update_limbs);
        }
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:975
    [ShouldCallParent, DmProtected]
    public virtual async DmTask<object> set_disabled(object new_disabled = null, float? update_limbs = null)
    {
        update_limbs ??= TRUE;
        object dot = null;

        if (Dm.eq(bodypart_disabled, new_disabled))
            return dot;
        dot = bodypart_disabled;
        bodypart_disabled = Dm.num(new_disabled);

        if (owner == null)
            return dot;
        owner.update_health_hud(); //update the healthdoll
        Waiting.NoSleep(owner.update_body());
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:990
    [ShouldNotOverride]
    public virtual async DmTask<object> update_owner(object new_owner = null)
    {
        object dot = null;
        if (Dm.eq(owner, new_owner))
            return FALSE; //`null` is a valid option, so we need to use a num var to make it clear no change was made.

        if (owner != null)
        {
            dot = owner; //return value is old owner
            await clear_ownership(owner);
        }
        if (Dm.truthy(new_owner))
            await apply_ownership(new_owner as mob_living_carbon);

        // SEND_SIGNAL(src, COMSIG_BODYPART_CHANGED_OWNER, new_owner, owner) (rules 11.1 (e), a statement)
        if (Dm.truthy(this._listen_lookup?[COMSIG_BODYPART_CHANGED_OWNER]))
            this._SendSignal(COMSIG_BODYPART_CHANGED_OWNER, Dm.list(this, new_owner, owner));

        refresh_bleed_rate();
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1008
    [ShouldCallParent]
    public virtual async DmTask clear_ownership(mob_living_carbon old_owner = null)
    {
        owner = null;

        if (Dm.truthy(LAZYLEN(bodypart_traits)))
            old_owner.remove_traits(bodypart_traits, bodypart_trait_source);

        UnregisterSignal(old_owner, Dm.list(
            SIGNAL_REMOVETRAIT(TRAIT_NOLIMBDISABLE),
            SIGNAL_ADDTRAIT(TRAIT_NOLIMBDISABLE),
            SIGNAL_REMOVETRAIT(TRAIT_NOBLOOD),
            SIGNAL_ADDTRAIT(TRAIT_NOBLOOD)));

        UnregisterSignal(old_owner, Dm.list(COMSIG_ATOM_RESTYLE, COMSIG_COMPONENT_CLEAN_ACT, COMSIG_LIVING_SET_BODY_POSITION));

        if (LIMB_HAS_SURGERY_STATE(this, ALL_SURGERY_FISH_STATES(body_zone)))
        {
            // qdel(old_owner.GetComponent(/datum/component/fishing_spot)): fishing in an open body is surgery (M8); a limb only
            // gets there with the surgical states of an operation.
            Dm.Deferred("code/modules/surgery/bodyparts/_bodyparts.dm:1026", "clear_ownership: qdel(old_owner.GetComponent(/datum/component/fishing_spot)), the opened body's fishing spot (surgery and fishing, M8; never added, :1060)");
        }
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1029
    [ShouldCallParent]
    public virtual async DmTask apply_ownership(mob_living_carbon new_owner = null)
    {
        owner = new_owner;

        if (Dm.truthy(LAZYLEN(bodypart_traits)))
            owner.add_traits(bodypart_traits, bodypart_trait_source);

        if (Dm.truthy(Dm.initial(this, (obj_item_bodypart p) => p.can_be_disabled)))
        {
            if (HAS_TRAIT(owner, TRAIT_NOLIMBDISABLE))
                await set_can_be_disabled(FALSE);

            // Listen to disable traits being added
            RegisterSignal(owner, SIGNAL_REMOVETRAIT(TRAIT_NOLIMBDISABLE), nameof(on_owner_nolimbdisable_trait_loss));
            RegisterSignal(owner, SIGNAL_ADDTRAIT(TRAIT_NOLIMBDISABLE), nameof(on_owner_nolimbdisable_trait_gain));

            // Listen to no blood traits being added
            RegisterSignal(owner, SIGNAL_REMOVETRAIT(TRAIT_NOBLOOD), nameof(on_owner_nobleed_loss));
            RegisterSignal(owner, SIGNAL_ADDTRAIT(TRAIT_NOBLOOD), nameof(on_owner_nobleed_gain));
        }

        if (Dm.truthy(can_be_disabled))
            await update_disabled(FALSE);

        RegisterSignal(owner, COMSIG_ATOM_RESTYLE, nameof(on_attempt_feature_restyle_mob)); // gaps OG1: restyling.dm
        RegisterSignal(owner, COMSIG_COMPONENT_CLEAN_ACT, nameof(on_owner_clean));
        RegisterSignal(owner, COMSIG_LIVING_SET_BODY_POSITION, nameof(refresh_bleed_rate));

        await forceMove(owner);
        RegisterSignal(this, COMSIG_MOVABLE_MOVED, nameof(on_forced_removal)); //this must be set after we moved, or we insta gib

        if (LIMB_HAS_SURGERY_STATE(this, ALL_SURGERY_FISH_STATES(body_zone)))
        {
            // owner.AddComponent(/datum/component/fishing_spot, /datum/fish_source/surgery): surgery (M8).
            Dm.Deferred("code/modules/surgery/bodyparts/_bodyparts.dm:1060", "apply_ownership: owner.AddComponent(/datum/component/fishing_spot, /datum/fish_source/surgery), fishing in an opened body (surgery and fishing, M8)");
        }
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1063
    [ShouldCallParent]
    public virtual void on_adding(mob_living_carbon new_owner = null)
    {
        Dm.combine(ref item_flags, ABSTRACT);
        ADD_TRAIT(this, TRAIT_NODROP, ORGAN_INSIDE_BODY_TRAIT);
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1070
    [ShouldCallParent]
    public virtual async DmTask on_removal(mob_living_carbon old_owner = null)
    {
        UnregisterSignal(this, COMSIG_MOVABLE_MOVED);

        Dm.mask(ref item_flags, Dm.bitnot(ABSTRACT));
        REMOVE_TRAIT(this, TRAIT_NODROP, ORGAN_INSIDE_BODY_TRAIT);

        if (!Dm.truthy(LAZYLEN(bodypart_traits)))
            return;

        owner.remove_traits(bodypart_traits, bodypart_trait_source);
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1084
    [DmProtected, ShouldCallParent]
    public virtual async DmTask<object> set_can_be_disabled(object new_can_be_disabled = null)
    {
        object dot = null;

        if (Dm.eq(can_be_disabled, new_can_be_disabled))
            return dot;
        dot = can_be_disabled;
        can_be_disabled = Dm.num(new_can_be_disabled);
        if (Dm.truthy(can_be_disabled))
        {
            if (owner != null)
            {
                if (HAS_TRAIT(owner, TRAIT_NOLIMBDISABLE))
                    throw Dm.CRASH("set_can_be_disabled to TRUE with for limb whose owner has TRAIT_NOLIMBDISABLE", "code/modules/surgery/bodyparts/_bodyparts.dm", 1094);
                RegisterSignal(owner, SIGNAL_ADDTRAIT(TRAIT_PARALYSIS), nameof(on_paralysis_trait_gain));
                RegisterSignal(owner, SIGNAL_REMOVETRAIT(TRAIT_PARALYSIS), nameof(on_paralysis_trait_loss));
            }
            await update_disabled();
        }
        else if (Dm.truthy(dot))
        {
            if (owner != null)
            {
                UnregisterSignal(owner, Dm.list(
                    SIGNAL_ADDTRAIT(TRAIT_PARALYSIS),
                    SIGNAL_REMOVETRAIT(TRAIT_PARALYSIS)));
            }
            await set_disabled(FALSE);
        }
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1108
    [DmProtected, SignalHandler]
    public virtual void on_paralysis_trait_gain(obj_item_bodypart source = null)
    {
        if (Dm.truthy(can_be_disabled))
            Waiting.NoSleep(set_disabled(TRUE));
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1116
    [DmProtected, SignalHandler]
    public virtual void on_paralysis_trait_loss(obj_item_bodypart source = null)
    {
        if (Dm.truthy(can_be_disabled))
            Waiting.NoSleep(update_disabled());
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1124
    [DmProtected, SignalHandler]
    public virtual void on_owner_nolimbdisable_trait_gain(mob_living_carbon source = null)
    {
        Waiting.NoSleep(set_can_be_disabled(FALSE));
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1131
    [DmProtected, SignalHandler]
    public virtual void on_owner_nolimbdisable_trait_loss(mob_living_carbon source = null)
    {
        Waiting.NoSleep(set_can_be_disabled(Dm.initial(this, (obj_item_bodypart p) => p.can_be_disabled)));
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1139
    [ShouldCallParent]
    public virtual object update_bodypart_damage_state()
    {
        float tbrute = round((brute_dam / max_damage) * 3, 1);
        float tburn = round((burn_dam / max_damage) * 3, 1);
        if ((tbrute != brutestate) || (tburn != burnstate))
        {
            brutestate = tbrute;
            burnstate = tburn;
            return TRUE;
        }
        return FALSE;
    }

    /// Called when limb's current owner gets washed
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1276  (syndicate G4b, D316: a wash cleans the pie off a face)
    [SignalHandler]
    public virtual object on_owner_clean(mob_living_carbon source = null, object clean_types = null)
    {
        object dot = NONE;

        if (Dm.truthy(Waiting.NoSleep(wash(clean_types))))
            dot = Dm.bitor(dot, COMPONENT_CLEANED);
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1284
    public override async DmTask<object> wash(object clean_types = null, params object[] __extra)
    {
        object dot = await base.wash(clean_types, __extra);
        if (!Dm.truthy(dot)) // Already clean. Nothing to do here.
            return dot;
        // always add the original dna to the organ after it's washed
        if (Dm.truthy(IS_ORGANIC_LIMB(this)) && Dm.truthy(Dm.bitand(clean_types, CLEAN_TYPE_BLOOD)))
            await add_blood_DNA(blood_dna_info);
        return dot;
    }

    /// INTERNAL PROC, DO NOT USE
    /// Properly sets us up to manage an inserted embeded object
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1621
    public virtual void _embed_object(obj_item embed = null)
    {
        if (Dm.@in(embed, embedded_objects)) // go away
            return;
        // We don't need to do anything with projectile embedding, because it will never reach this point
        LAZYADD(ref embedded_objects, embed);
        RegisterSignal(embed, COMSIG_ITEM_EMBEDDING_UPDATE, nameof(embedded_object_changed));
        refresh_bleed_rate();
    }

    /// INTERNAL PROC, DO NOT USE
    /// Cleans up any attachment we have to the embedded object, removes it from our list
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1631
    public virtual void _unembed_object(obj_item unembed = null)
    {
        LAZYREMOVE(ref embedded_objects, unembed);
        UnregisterSignal(unembed, COMSIG_ITEM_EMBEDDING_UPDATE);
        refresh_bleed_rate();
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1636
    [SignalHandler]
    public virtual void embedded_object_changed(obj_item embedded_source = null)
    {
        /// Embedded objects effect bleed rate, gotta refresh lads
        refresh_bleed_rate();
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1642
    [ShouldCallParent]
    public virtual void setBleedStacks(object set_to = null)
    {
        adjustBleedStacks(Dm.sub(set_to, generic_bleedstacks));
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1648
    public virtual void adjustBleedStacks(object adjust_by = null, float? minimum = null)
    {
        minimum ??= -INFINITY;
        if (!Dm.truthy(adjust_by))
            return;
        object old_bleedstacks = generic_bleedstacks;
        generic_bleedstacks = max(Dm.num(Dm.add(generic_bleedstacks, adjust_by)), Dm.num(minimum));

        // If we've started or stopped bleeding, we need to refresh our bleed rate
        if ((Dm.le(old_bleedstacks, 0) && Dm.gt(generic_bleedstacks, 0))
            || Dm.gt(old_bleedstacks, 0) && Dm.le(generic_bleedstacks, 0))
            refresh_bleed_rate();
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1659
    [SignalHandler]
    public virtual void on_owner_nobleed_loss(datum source = null)
    {
        refresh_bleed_rate();
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1663
    [SignalHandler]
    public virtual void on_owner_nobleed_gain(datum source = null)
    {
        refresh_bleed_rate();
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1669
    [SignalHandler, ShouldNotOverride]
    public virtual object refresh_bleed_rate()
    {
        float old_bleed_rate = cached_bleed_rate;
        cached_bleed_rate = 0;
        if (owner == null)
            return null;

        if (!Dm.truthy(can_bleed()))
        {
            if (cached_bleed_rate != old_bleed_rate)
                Waiting.NoSleep(update_part_wound_overlay());
            return null;
        }

        if (Dm.gt(generic_bleedstacks, 0))
            cached_bleed_rate += 0.5f;

        // In 99% of situations we won't get to this point if we aren't wired or blooded
        // But I'm covering my ass in case someone adds some weird new species
        float surgery_bloodloss = 0;
        if (Dm.truthy(Dm.bitand(biological_state, BIOSTATE_HAS_VESSELS)))
        {
            // better clamp those up quick
            if (Dm.truthy(HAS_ANY_SURGERY_STATE(surgery_state, SURGERY_VESSELS_UNCLAMPED)))
                surgery_bloodloss += UNCLAMPED_VESSELS_BLEEDING;
            // better, but still not exactly ideal
            else if (Dm.truthy(HAS_ANY_SURGERY_STATE(surgery_state, Dm.bitor(SURGERY_VESSELS_CLAMPED, SURGERY_ORGANS_CUT))))
                surgery_bloodloss += CLAMPED_VESSELS_BLEEDING;

            // modify rate so cutting everything open won't nuke people
            if (Dm.eq(body_zone, BODY_ZONE_HEAD))
                surgery_bloodloss *= 0.5f;
            else if (!Dm.eq(body_zone, BODY_ZONE_CHEST))
                surgery_bloodloss *= 0.25f;
            // bonus for being gauzed up
            if (Dm.truthy(LAZYACCESS(applied_items, LIMB_ITEM_GAUZE)))
                surgery_bloodloss *= 0.4f;

            cached_bleed_rate += surgery_bloodloss;
        }

        foreach (obj_item embeddies in Dm.each_cast<obj_item>(embedded_objects))
        {
            if (!Dm.truthy(((datum_embedding)embeddies.get_embed()).is_harmless()))
                cached_bleed_rate += 0.25f;
        }

        foreach (datum_wound iter_wound in Dm.each_cast<datum_wound>(wounds))
        {
            cached_bleed_rate += Dm.num(iter_wound.blood_flow);
            if (!Dm.truthy(Dm.bitand(iter_wound.surgery_states, SURGERY_VESSELS_UNCLAMPED)) || !Dm.truthy(surgery_bloodloss))
                continue;
            // Consider it contirubuted by the wound itself
            // Not -surgery_bloodloss as this way clamping the vessels reduces the overall bleeding
            cached_bleed_rate -= UNCLAMPED_VESSELS_BLEEDING;
            surgery_bloodloss = 0;
        }

        if (owner.body_position == LYING_DOWN)
            cached_bleed_rate *= 0.75f;

        if (grasped_by != null)
            cached_bleed_rate *= 0.7f;

        if (Dm.truthy(LAZYACCESS(applied_items, LIMB_ITEM_TOURNIQUET)))
            cached_bleed_rate *= 0.1f;

        // Our bleed overlay is based directly off bleed_rate, so go aheead and update that would you?
        if (cached_bleed_rate != old_bleed_rate)
            Waiting.NoSleep(update_part_wound_overlay());

        return cached_bleed_rate;
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1771
    [ShouldBePure]
    public virtual object can_bleed()
    {
        return Dm.num((Dm.truthy(Dm.bitand(biological_state, BIO_BLOODED)) && (owner == null || Dm.truthy(owner.can_bleed()))));
    }

    /**
     * Inserts an item into the applied items list for this bodypart
     *
     * Note: Forcemoves the item to the bodypart's contents when called!
     *
     * Arguments:
     * * applying_item - The item to apply
     * * category - The category of item being applied (e.g. LIMB_ITEM_GAUZE). If null, defaults to the REF of the applying_item
     * * override - If TRUE, will force the application of the item even if an item of the same category is already applied
     *
     * Returns TRUE if the item was successfully applied
     * Returns FALSE if the item was not applied (e.g. an item of the same category is already applied and override is FALSE)
     */
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1789  (M6 slice 6: gauze)
    public virtual async DmTask<object> apply_item(obj_item applying_item = null, object category = null, float? @override = null)
    {
        @override ??= FALSE;
        if (category == null)
            category = REF(applying_item);

        if (!Dm.truthy(@override) && Dm.truthy(LAZYACCESS(applied_items, category)))
            return FALSE;

        await applying_item.forceMove(this);
        LAZYSET(ref applied_items, category, applying_item);
        if (Dm.truthy(applying_item._listen_lookup?[COMSIG_ITEM_APPLIED_TO_LIMB]))
            applying_item._SendSignal(COMSIG_ITEM_APPLIED_TO_LIMB, Dm.list(applying_item, this));
        return TRUE;
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1801  (M6 slice 6: gauze)
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        foreach ((object category, object item) in Dm.pairs(applied_items?.Copy()))
        {
            if (item != gone)
                continue;
            LAZYREMOVE(ref applied_items, category);
            if (Dm.truthy(gone._listen_lookup?[COMSIG_ITEM_UNAPPLIED_FROM_LIMB]))
                gone._SendSignal(COMSIG_ITEM_UNAPPLIED_FROM_LIMB, Dm.list(gone, this));
        }
        return dot;
    }

    // Note: For effects on subtypes, use the emp_effect() proc instead
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1851
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object protection = await base.emp_act(severity);
        // If the limb doesn't protect contents, strike them first
        if (!Dm.truthy(Dm.bitand(protection, EMP_PROTECT_CONTENTS)))
        {
            foreach (atom content in Dm.each_cast<atom>(contents))
                await content.emp_act(severity);
        }

        if (Dm.truthy(Dm.bitand(protection, Dm.bitor(EMP_PROTECT_WIRES, EMP_PROTECT_SELF))))
            return protection;

        await emp_effect(severity, protection);
        return protection;
    }

    /// The actual effect of EMPs on the limb. Allows children to override it however they want
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1865
    public virtual async DmTask<object> emp_effect(object severity = null, object protection = null)
    {
        if (!Dm.truthy(IS_ROBOTIC_LIMB(this)))
            return FALSE;
        // with defines at the time of writing, this is 2 brute and 1.5 burn
        // 2 + 1.5 = 3,5, with 6 limbs thats 21, on a heavy 42
        // 42 * 0.8 = 33.6
        // 3 hits to crit with an ion rifle on someone fully augged at a total of 100.8 damage, although im p sure mood can boost max hp above 100
        // dont forget emps pierce armor, debilitate augs, and usually comes with splash damage e.g. ion rifles or grenades
        float time_needed = AUGGED_LIMB_EMP_PARALYZE_TIME;
        float brute_damage = AUGGED_LIMB_EMP_BRUTE_DAMAGE;
        float burn_damage = AUGGED_LIMB_EMP_BURN_DAMAGE;
        if (Dm.eq(severity, EMP_HEAVY))
        {
            time_needed *= 2;
            brute_damage *= 2;
            burn_damage *= 2;
        }

        await receive_damage(brute_damage, burn_damage);
        do_sparks(number: 1, cardinal_only: FALSE, source: owner ?? (atom)this);

        if (Dm.truthy(can_be_disabled) && (Dm.num(get_damage()) / max_damage) >= robotic_emp_paralyze_damage_percent_threshold)
        {
            ADD_TRAIT(this, TRAIT_PARALYSIS, EMP_TRAIT);
            _addtimer(TRAIT_CALLBACK_REMOVE(this, TRAIT_PARALYSIS, EMP_TRAIT), time_needed, file: "code/modules/surgery/bodyparts/_bodyparts.dm", line: 1886);
            owner?.visible_message(span_danger(I18n.T("[]'s [] seems to malfunction!", owner, plaintext_zone)));
        }

        return TRUE;
    }

    /// Returns the generic description of our BIO_EXTERNAL feature(s), prioritizing certain ones over others. Returns error on failure.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1892
    public virtual object get_external_description()
    {
        if (Dm.truthy(Dm.bitand(biological_state, BIO_FLESH)))
            return "flesh";
        if (Dm.truthy(Dm.bitand(biological_state, BIO_WIRED)))
            return "wiring";
        if (Dm.truthy(Dm.bitand(biological_state, BIO_CHITIN)))
            return "chitin";

        return "error";
    }

    /// Returns the generic description of our BIO_INTERNAL feature(s), prioritizing certain ones over others. Returns error on failure.
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1903
    public virtual object get_internal_description()
    {
        if (Dm.truthy(Dm.bitand(biological_state, BIO_STONE)))
            return "bedrock";
        if (Dm.truthy(Dm.bitand(biological_state, BIO_BONE)))
            return "bone";
        if (Dm.truthy(Dm.bitand(biological_state, BIO_METAL)))
            return "metal";
        if (Dm.truthy(Dm.bitand(biological_state, BIO_FLESH)))
            return "shreds of ligaments";
        if (Dm.truthy(Dm.bitand(biological_state, BIO_WOOD)))
            return "splinters of poorly manufactured wood";
        if (Dm.truthy(Dm.bitand(biological_state, BIO_CHITIN)))
            return "fragments of chitin";

        return "error";
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1920
    public virtual void add_bodypart_trait(object new_trait = null)
    {
        LAZYOR(ref bodypart_traits, new_trait);
        if (owner == null)
            return;
        ADD_TRAIT(owner, new_trait, bodypart_trait_source);
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1927
    public virtual void remove_bodypart_trait(object old_trait = null)
    {
        LAZYREMOVE(ref bodypart_traits, old_trait);
        if (owner == null)
            return;
        REMOVE_TRAIT(owner, old_trait, bodypart_trait_source);
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1934
    public virtual void add_bodyshape(object new_shape = null)
    {
        if (Dm.truthy(Dm.bitand(bodyshape, new_shape)))
            return;

        Dm.combine(ref bodyshape, new_shape);
        owner?.synchronize_bodyshapes();
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1942
    public virtual void remove_bodyshape(object old_shape = null)
    {
        if (!Dm.truthy(Dm.bitand(bodyshape, old_shape)))
            return;

        Dm.mask(ref bodyshape, Dm.bitnot(old_shape));
        owner?.synchronize_bodyshapes();
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1950
    public virtual async DmTask add_surgical_state(object new_states = null)
    {
        if (!Dm.truthy(new_states))
            throw Dm.CRASH("add_surgical_state called with no new states to add", "code/modules/surgery/bodyparts/_bodyparts.dm", 1952);
        float old_states = surgery_state;
        Dm.combine(ref surgery_state, new_states);
        await update_surgical_state(old_states, new_states);
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1958
    public virtual async DmTask remove_surgical_state(object removing_states = null)
    {
        if (!Dm.truthy(removing_states))
            throw Dm.CRASH("remove_surgical_state called with no states to remove", "code/modules/surgery/bodyparts/_bodyparts.dm", 1960);
        if (!Dm.truthy(Dm.bitand(surgery_state, removing_states)))
            return;

        // inherent to the biostate, don't remove them
        if (!Dm.truthy(LIMB_HAS_SKIN(this)))
            Dm.mask(ref removing_states, Dm.bitnot(SKINLESS_SURGERY_STATES));
        if (!Dm.truthy(LIMB_HAS_BONES(this)))
            Dm.mask(ref removing_states, Dm.bitnot(BONELESS_SURGERY_STATES));
        if (!Dm.truthy(LIMB_HAS_VESSELS(this)))
            Dm.mask(ref removing_states, Dm.bitnot(VESSELLESS_SURGERY_STATES));
        if (!Dm.truthy(removing_states))
            return;

        float old_states = surgery_state;
        Dm.mask(ref surgery_state, Dm.bitnot(removing_states));
        await update_surgical_state(old_states, removing_states);
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:1979
    public virtual async DmTask update_surgical_state(object old_state = null, object changed_states = null)
    {
        // SEND_SIGNAL(src, COMSIG_BODYPART_UPDATING_SURGERY_STATE, old_state, surgery_state, changed_states) (rules 11.1 (e))
        if (Dm.truthy(this._listen_lookup?[COMSIG_BODYPART_UPDATING_SURGERY_STATE]))
            this._SendSignal(COMSIG_BODYPART_UPDATING_SURGERY_STATE, Dm.list(this, old_state, surgery_state, changed_states));
        if (Dm.eq(Dm.bitand(surgery_state, changed_states), changed_states))
            return;

        if (Dm.truthy(HAS_ANY_SURGERY_STATE(Dm.num(changed_states), Dm.bitor(SURGERY_ORGANS_CUT, ALL_SURGERY_VESSEL_STATES))))
            refresh_bleed_rate();

        if (owner == null)
            return;

        // SEND_SIGNAL(owner, COMSIG_LIVING_UPDATING_SURGERY_STATE, old_state, surgery_state, changed_states) (rules 11.1 (e))
        if (Dm.truthy(owner._listen_lookup?[COMSIG_LIVING_UPDATING_SURGERY_STATE]))
            owner._SendSignal(COMSIG_LIVING_UPDATING_SURGERY_STATE, Dm.list(owner, old_state, surgery_state, changed_states));
        if (HAS_SURGERY_STATE(surgery_state, ALL_SURGERY_FISH_STATES(body_zone)))
            Dm.Deferred("code/modules/surgery/bodyparts/_bodyparts.dm:1991", "update_surgical_state: owner.AddComponent(/datum/component/fishing_spot, /datum/fish_source/surgery), fishing in an opened body (the fishing spot is inert; fishing)");
        else if (HAS_SURGERY_STATE(Dm.num(old_state), ALL_SURGERY_FISH_STATES(body_zone)))
            Dm.Deferred("code/modules/surgery/bodyparts/_bodyparts.dm:1993", "update_surgical_state: qdel(owner.GetComponent(/datum/component/fishing_spot)) (never added, :1991; fishing)");
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:2005
    public virtual async DmTask add_biostate(object new_biostate = null)
    {
        if (Dm.truthy(Dm.bitand(biological_state, new_biostate)))
            return;

        float had_skin = LIMB_HAS_SKIN(this);
        float had_bones = LIMB_HAS_BONES(this);
        float had_vessels = LIMB_HAS_VESSELS(this);

        Dm.combine(ref biological_state, new_biostate);

        if (!Dm.truthy(had_skin) && Dm.truthy(LIMB_HAS_SKIN(this)))
            await remove_surgical_state(SKINLESS_SURGERY_STATES);
        if (!Dm.truthy(had_bones) && Dm.truthy(LIMB_HAS_BONES(this)))
            await remove_surgical_state(BONELESS_SURGERY_STATES);
        if (!Dm.truthy(had_vessels) && Dm.truthy(LIMB_HAS_VESSELS(this)))
            await remove_surgical_state(VESSELLESS_SURGERY_STATES);
    }

    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:2023
    public virtual async DmTask remove_biostate(object old_biostate = null)
    {
        if (!Dm.truthy(Dm.bitand(biological_state, old_biostate)))
            return;

        Dm.mask(ref biological_state, Dm.bitnot(old_biostate));
        if (!Dm.truthy(LIMB_HAS_SKIN(this)))
            await add_surgical_state(SKINLESS_SURGERY_STATES);
        if (!Dm.truthy(LIMB_HAS_BONES(this)))
            await add_surgical_state(BONELESS_SURGERY_STATES);
        if (!Dm.truthy(LIMB_HAS_VESSELS(this)))
            await add_surgical_state(VESSELLESS_SURGERY_STATES);
    }
}

// tg: code/modules/surgery/bodyparts/_bodyparts.dm:330 (interleaved types, rules 1.9; M5x slice 3)
public partial class obj_item_bodypart : obj_item
{
    // tg: code/modules/surgery/bodyparts/_bodyparts.dm:330  (set waitfor = FALSE, inherited from /atom/ex_act)
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        if (owner != null) //trust me bro you dont want this
            return FALSE;
        return base.ex_act(severity, target, __extra);
    }
}
