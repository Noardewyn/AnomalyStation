// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/bodyparts/head.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only Initialize, Destroy, get_face_name, update_limb, update_bodypart_damage_state and update_owner_name
// translated (M2 P2, scope.md 2e "bodyparts"); M6 slice 4: can_dismember and drop_organs; X3: examine. Butchering, the dropped head's icon and its voice come with M6, M8
// and M9.

// tg: code/modules/surgery/bodyparts/head.dm:1
// [DmPath("/obj/item/bodypart/head")] is on the part in code/modules/mob/living/carbon/carbon_update_icons.dm, the
// earliest .dme file that declares /obj/item/bodypart/head (rules 2.3).
// The type block (head.dm:1-87) is translated in full by the M2 contracts step; the procs are still skeletons.
public partial class obj_item_bodypart_head : obj_item_bodypart
{
    // The constant var overrides of the type block (rules 10.2), in DM declaration order.
    public obj_item_bodypart_head()
    {
        // tg: code/modules/surgery/bodyparts/head.dm:2
        name = BODY_ZONE_HEAD;
        // tg: code/modules/surgery/bodyparts/head.dm:3
        desc = "Didn't make sense not to live for fun, your brain gets smart but your head gets dumb.";
        // tg: code/modules/surgery/bodyparts/head.dm:4
        icon = Dm.resource("icons/mob/human/bodyparts.dmi");
        // tg: code/modules/surgery/bodyparts/head.dm:5
        icon_state = "default_human_head";
        // tg: code/modules/surgery/bodyparts/head.dm:6
        max_damage = LIMB_MAX_HP_CORE;
        // tg: code/modules/surgery/bodyparts/head.dm:7
        body_zone = BODY_ZONE_HEAD;
        // tg: code/modules/surgery/bodyparts/head.dm:8
        body_part = HEAD;
        // tg: code/modules/surgery/bodyparts/head.dm:9
        plaintext_zone = "head";
        // tg: code/modules/surgery/bodyparts/head.dm:10
        w_class = WEIGHT_CLASS_BULKY; //Quite a hefty load
        // tg: code/modules/surgery/bodyparts/head.dm:11
        slowdown = 1; //Balancing measure
        // tg: code/modules/surgery/bodyparts/head.dm:12
        throw_range = 2; //No head bowling
        // tg: code/modules/surgery/bodyparts/head.dm:13
        px_x = 0;
        // tg: code/modules/surgery/bodyparts/head.dm:14
        px_y = -8;
        // tg: code/modules/surgery/bodyparts/head.dm:15
        wound_resistance = 5;
        // tg: code/modules/surgery/bodyparts/head.dm:16
        disabled_wound_penalty = 25;
        // tg: code/modules/surgery/bodyparts/head.dm:17
        scars_covered_by_clothes = FALSE;
        // tg: code/modules/surgery/bodyparts/head.dm:18
        is_dimorphic = TRUE;
        // (unarmed_attack_verbs, :19, and unarmed_attack_verbs_continuous, :20, create lists: dm_init_vars below)
        // tg: code/modules/surgery/bodyparts/head.dm:21
        unarmed_attack_effect = ATTACK_EFFECT_BITE;
        // tg: code/modules/surgery/bodyparts/head.dm:22
        unarmed_attack_sound = Dm.resource("sound/items/weapons/bite.ogg");
        // tg: code/modules/surgery/bodyparts/head.dm:23
        unarmed_miss_sound = Dm.resource("sound/items/weapons/bite.ogg");
        // tg: code/modules/surgery/bodyparts/head.dm:24
        unarmed_damage_low = 1; // Yeah, biting is pretty weak, blame the monkey super-nerf
        // tg: code/modules/surgery/bodyparts/head.dm:25
        unarmed_damage_high = 3;
        // tg: code/modules/surgery/bodyparts/head.dm:26
        unarmed_effectiveness = 0;
        // tg: code/modules/surgery/bodyparts/head.dm:27
        bodypart_trait_source = HEAD_TRAIT;
        // tg: code/modules/surgery/bodyparts/head.dm:28
        butcher_replacement = typeof(obj_item_bodypart_head_skeleton_nonfunctional);
        // tg: code/modules/surgery/bodyparts/head.dm:29
        base_meat_amount = 0;
        // tg: code/modules/surgery/bodyparts/head.dm:30
        stump_typepath = typeof(obj_item_bodypart_head_stump);
    }

    // The object-creating overrides of the type block (rules 10.2), in DM declaration order.
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        // tg: code/modules/surgery/bodyparts/head.dm:19
        unarmed_attack_verbs = Dm.list("bite", "chomp");
        // tg: code/modules/surgery/bodyparts/head.dm:20
        unarmed_attack_verbs_continuous = Dm.list("bites", "chomps");
    }

    /// Do we show the information about missing organs upon being examined? Defaults to TRUE, useful for Dullahan heads.
    // tg: code/modules/surgery/bodyparts/head.dm:33
    public float show_organs_on_examine = TRUE;

    //Limb appearance info:
    /// Replacement name
    // tg: code/modules/surgery/bodyparts/head.dm:37
    public string real_name = "";
    /// Flags related to appearance, such as hair, lips, etc
    // tg: code/modules/surgery/bodyparts/head.dm:39
    public float head_flags = HEAD_DEFAULT_FEATURES;

    /// Hair style
    // tg: code/modules/surgery/bodyparts/head.dm:42
    public string hairstyle = "Bald";
    /// Hair colour and style
    // tg: code/modules/surgery/bodyparts/head.dm:44
    public string hair_color = COLOR_BLACK;
    /// Hair alpha
    // tg: code/modules/surgery/bodyparts/head.dm:46
    public float hair_alpha = 255;

    ///Facial hair style
    // tg: code/modules/surgery/bodyparts/head.dm:49
    public string facial_hairstyle = "Shaved";
    ///Facial hair color
    // tg: code/modules/surgery/bodyparts/head.dm:51
    public string facial_hair_color = COLOR_BLACK;
    ///Facial hair alpha
    // tg: code/modules/surgery/bodyparts/head.dm:53
    public float facial_hair_alpha = 255;

    /// Gradient styles, if any
    // tg: code/modules/surgery/bodyparts/head.dm:56
    public DmList gradient_styles;
    /// Gradient colors, if any
    // tg: code/modules/surgery/bodyparts/head.dm:58
    public DmList gradient_colors;

    /// An override color that can be cleared later, affects both hair and facial hair
    // tg: code/modules/surgery/bodyparts/head.dm:61
    public object override_hair_color = null;
    /// An override that cannot be cleared under any circumstances, affects both hair and facial hair
    // tg: code/modules/surgery/bodyparts/head.dm:63
    public object fixed_hair_color = null;

    ///Type of lipstick being used, basically
    // tg: code/modules/surgery/bodyparts/head.dm:66
    public object lip_style;
    ///Lipstick color
    // tg: code/modules/surgery/bodyparts/head.dm:68
    public object lip_color;
    ///Current lipstick trait, if any (such as TRAIT_KISS_OF_DEATH)
    // tg: code/modules/surgery/bodyparts/head.dm:70
    public object stored_lipstick_trait;

    /// How many teeth the head's species has, humans have 32 so that's the default. Used for a limit to dental pill implants.
    // tg: code/modules/surgery/bodyparts/head.dm:73
    public float teeth_count = 32;

    /// Offset to apply to equipment worn on the ears
    // tg: code/modules/surgery/bodyparts/head.dm:76
    public datum_worn_feature_offset worn_ears_offset;
    /// Offset to apply to equipment worn on the eyes
    // tg: code/modules/surgery/bodyparts/head.dm:78
    public datum_worn_feature_offset worn_glasses_offset;
    /// Offset to apply to equipment worn on the mouth
    // tg: code/modules/surgery/bodyparts/head.dm:80
    public datum_worn_feature_offset worn_mask_offset;
    /// Offset to apply to equipment worn on the head
    // tg: code/modules/surgery/bodyparts/head.dm:82
    public datum_worn_feature_offset worn_head_offset;
    /// Offset to apply to overlays placed on the face
    // tg: code/modules/surgery/bodyparts/head.dm:84
    public datum_worn_feature_offset worn_face_offset;

    /// Can this head be dismembered normally?
    // tg: code/modules/surgery/bodyparts/head.dm:87  (the proc /obj/item/bodypart/proc/can_dismember is
    // can_dismember__proc, rules 2.4)
    public float can_dismember = FALSE;
}

// tg: code/modules/surgery/bodyparts/head.dm:89 (interleaved types, rules 1.9)
public partial class obj_item_bodypart_head : obj_item_bodypart
{
    // tg: code/modules/surgery/bodyparts/head.dm:89
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_toy_talk)));
        RegisterSignals(this, Dm.list(SIGNAL_ADDTRAIT(TRAIT_DISFIGURED), SIGNAL_REMOVETRAIT(TRAIT_DISFIGURED)), nameof(update_owner_name));
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/head.dm:94
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref worn_ears_offset);
        QDEL_NULL(ref worn_glasses_offset);
        QDEL_NULL(ref worn_mask_offset);
        QDEL_NULL(ref worn_head_offset);
        QDEL_NULL(ref worn_face_offset);
        return base.Destroy(force);
    }
}

// tg: code/modules/surgery/bodyparts/head.dm:119
public partial class obj_item_bodypart_head : obj_item_bodypart
{
    // tg: code/modules/surgery/bodyparts/head.dm:119
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(show_organs_on_examine) || !Dm.truthy(IS_ORGANIC_LIMB(this)))
            return dot;
        object shown_name = get_face_name();
        obj_item_organ_brain brain = Dm.locate_type(typeof(obj_item_organ_brain), this) as obj_item_organ_brain;
        if (brain == null)
            Dm.append(ref dot, span_info(I18n.T("The brain has been removed from [].", this)));
        else if (Dm.truthy(brain.suicided) || (brain.brainmob != null && HAS_TRAIT(brain.brainmob, TRAIT_SUICIDED)))
            Dm.append(ref dot, span_info(I18n.T("There's a miserable expression on []'s face; they must have really hated life. There's no hope of recovery.", shown_name)));
        else if (brain.brainmob != null)
        {
            if (brain.brainmob?.health <= brain.brainmob?.dead_threshold)
                Dm.append(ref dot, span_info(I18n.T("It's leaking some kind of... clear fluid? The brain inside must be in pretty bad shape.")));
            if (Dm.truthy(brain.brainmob.key) || Dm.truthy(brain.brainmob.get_ghost(FALSE, TRUE)))
                Dm.append(ref dot, span_info(I18n.T("Its muscles are twitching slightly... It seems to have some life still in it.")));
            else
                Dm.append(ref dot, span_info(I18n.T("It's completely lifeless. Perhaps there'll be a chance for them later.")));
        }
        else if (Dm.truthy(brain?.decoy_override))
            Dm.append(ref dot, span_info(I18n.T("It's completely lifeless. Perhaps there'll be a chance for them later.")));
        else
            Dm.append(ref dot, span_info(I18n.T("It's completely lifeless.")));

        if (Dm.locate_type(typeof(obj_item_organ_eyes), this) == null)
            Dm.append(ref dot, span_info(I18n.T("[]'s eyes have been removed.", shown_name)));

        if (Dm.locate_type(typeof(obj_item_organ_ears), this) == null)
            Dm.append(ref dot, span_info(I18n.T("[]'s ears have been removed.", shown_name)));

        if (Dm.locate_type(typeof(obj_item_organ_tongue), this) == null)
            Dm.append(ref dot, span_info(I18n.T("[]'s tongue has been removed.", shown_name)));
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/head.dm:150
    public virtual object get_face_name()
    {
        if (HAS_TRAIT(this, TRAIT_DISFIGURED))
            return "Unknown";
        return real_name;
    }

    // tg: code/modules/surgery/bodyparts/head.dm:155
    public override object can_dismember__proc(obj_item item = null)
    {
        if (!Dm.truthy(can_dismember) && !HAS_TRAIT(owner, TRAIT_ALWAYS_ALLOW_DECAPITATION))
            return FALSE;

        if (!HAS_TRAIT(owner, TRAIT_CURSED) && Dm.num(owner.stat) < HARD_CRIT && !HAS_TRAIT(owner, TRAIT_ALWAYS_ALLOW_DECAPITATION))
            return FALSE;

        return base.can_dismember__proc(item);
    }

    // tg: code/modules/surgery/bodyparts/head.dm:164
    public override async DmTask drop_organs(mob user = null, object violent_removal = null)
    {
        if (user != null)
            user.visible_message(span_warning(I18n.T("[] saws [] open and pulls out a brain!", user, this)), span_notice(I18n.T("You saw [] open and pull out a brain.", this)));
        obj_item_organ_brain brain = Dm.locate<obj_item_organ_brain>(this);
        if (brain != null && Dm.truthy(violent_removal) && prob(90)) //ghetto surgery can damage the brain.
        {
            to_chat(user, span_warning(I18n.T("[] was damaged in the process!", brain)));
            await brain.set_organ_damage(brain.maxHealth);
        }
        await base.drop_organs(user, violent_removal);
    }

    // tg: code/modules/surgery/bodyparts/head.dm:210
    [SignalHandler]
    public virtual void update_owner_name(datum source = null)
    {
        if (ishuman(owner))
        {
            mob_living_carbon_human as_human = owner as mob_living_carbon_human;
            as_human?.update_visible_name();
        }
    }
}

// tg: code/modules/surgery/bodyparts/head.dm:177 (interleaved types, rules 1.9)
public partial class obj_item_bodypart_head : obj_item_bodypart
{
    // tg: code/modules/surgery/bodyparts/head.dm:177
    public override async DmTask<object> update_limb(float? dropping_limb = null, float? is_creating = null)
    {
        object dot = null;
        dot = await base.update_limb(dropping_limb, is_creating);
        if (owner == null)
            return dot;
        if (Dm.truthy(is_husked) && is_husked != HUSKED_ZOMBIE)
            ADD_TRAIT(this, TRAIT_DISFIGURED, HUSK_TRAIT);
        else
            REMOVE_TRAIT(this, TRAIT_DISFIGURED, HUSK_TRAIT);
        if (Dm.truthy(is_creating))
        {
            real_name = Dm.str(owner.real_name);
            copy_appearance_from(owner as mob_living_carbon_human);
        }
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/head.dm:200
    public override object update_bodypart_damage_state()
    {
        object dot = null;
        if (Dm.truthy(Dm.bitand(head_flags, HEAD_NO_DISFIGURE)))
            return base.update_bodypart_damage_state();
        float old_states = brutestate + burnstate;
        dot = base.update_bodypart_damage_state();
        float new_states = brutestate + burnstate;
        if (new_states >= HUMAN_DISFIGURATION_HEAD_DAMAGE_STATES)
            ADD_TRAIT(this, TRAIT_DISFIGURED, BODYPART_TRAIT);
        else if (old_states >= HUMAN_DISFIGURATION_HEAD_DAMAGE_STATES)
            REMOVE_TRAIT(this, TRAIT_DISFIGURED, BODYPART_TRAIT);
        return dot;
    }
}
