// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/internal/tongue/_tongue.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The tongue's insertion hooks (M2 P2, plan.md Q7) and its speech and taste (M9 slice A); the emote sounds are the emotes' (M9 slice B);
// the lizard's speech (gaps P3b: the lizard infusion); the alien's (gaps X5). Gaps OG1 (D339, D341): the rest of the file, the silverscale's
// statue, the abductor's, zombie's, bone, snail and jelly tongues.

// tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:1
[DmPath("/obj/item/organ/tongue")]
public partial class obj_item_organ_tongue : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:2-11
    public obj_item_organ_tongue()
    {
        name = "tongue";
        desc = "A fleshy muscle mostly used for lying.";
        icon_state = "tongue";

        zone = BODY_ZONE_PRECISE_MOUTH;
        slot = ORGAN_SLOT_TONGUE;
        voice_filter = "";
        visual = FALSE;
    }

    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:8-9  the object-creating defaults (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        attack_verb_continuous = Dm.list("licks", "slobbers", "slaps", "frenches", "tongues");
        attack_verb_simple = Dm.list("lick", "slobber", "slap", "french", "tongue");
    }

    /**
     * A cached list of paths of all the languages this tongue is capable of speaking
     *
     * Relates to a mob's ability to speak a language - a mob must be able to speak the language
     * and have a tongue able to speak the language (or omnitongue) in order to actually speak said language
     *
     * To modify this list for subtypes, see [/obj/item/organ/tongue/proc/get_possible_languages]. Do not modify directly.
     */
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:20  VAR_PRIVATE/list/languages_possible
    [DmPrivate]
    public DmList languages_possible;
    /**
     * A list of languages which are native to this tongue
     *
     * When these languages are spoken with this tongue, and modifies speech is true, no modifications will be made
     * (such as no accent, hissing, or whatever)
     */
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:27
    public DmList languages_native;
    ///changes the verbage of how you speak. (Permille -> says <-, "I just used a verb!")
    ///i hate to say it, but because of sign language, this may have to be a component. and we may have to do some insane shit like putting a component on a component
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:30
    public string say_mod = "says";
    ///for temporary overrides of the above variable.
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:32
    public string temp_say_mod = "";

    /// Whether the owner of this tongue can speak clearly. Being set to FALSE means they mumble and slur things
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:35
    public float speakable_with = TRUE;
    /// Whether the owner of this tongue can taste anything. Being set to FALSE will mean no taste feedback will be provided.
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:37
    public float sense_of_taste = TRUE;
    /// Determines how "sensitive" this tongue is to tasting things, lower is more sensitive.
    /// See [/mob/living/proc/get_taste_sensitivity].
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:40
    public float taste_sensitivity = 15;
    /// Foodtypes this tongue likes
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:42
    public float liked_foodtypes = Dm.bitor(JUNKFOOD, FRIED); //human tastes are default
    /// Foodtypes this tongue dislikes
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:44
    public float disliked_foodtypes = Dm.bitor(Dm.bitor(Dm.bitor(Dm.bitor(GROSS, RAW), CLOTH), BUGS), GORE); //human tastes are default
    /// Foodtypes this tongue HATES
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:46
    public float toxic_foodtypes = TOXIC; //human tastes are default
    /// Whether this tongue modifies speech via signal
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:48
    public float modifies_speech = FALSE;
    /// List of emote keys and sounds for overriding sounds from emotes
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:50  VAR_PROTECTED/emote_sounds
    [DmProtected]
    public object emote_sounds;
}

// tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:52 (interleaved types, rules 1.9)
public partial class obj_item_organ_tongue : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:52
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        // Setup the possible languages list
        // - get_possible_languages gives us a list of language paths
        // - then we cache it via string list
        // this results in tongues with identical possible languages sharing a cached list instance
        languages_possible = string_list(get_possible_languages() as DmList) as DmList;
        if (Dm.truthy(speakable_with))
            add_organ_trait(TRAIT_SPEAKS_CLEARLY);
        return dot;
    }
}

// tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:83
public partial class obj_item_organ_tongue : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:83
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:64
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (HAS_MIND_TRAIT(user, TRAIT_ENTRAILS_READER) || isobserver(user))
        {
            // FOOD_FLAGS_IC (__DEFINES/food.dm:76): a list literal, a new list at every use
            DmList FOOD_FLAGS_IC = Dm.list("Meat", "Vegetables", "Raw food", "Junk food", "Grain", "Fruits", "Dairy products", "Fried food", "Alcohol", "Sugary food", "Gross food", "Toxic food", "Pineapples", "Breakfast food", "Clothing", "Nuts", "Seafood", "Oranges", "Bugs", "Gore", "Rocks", "Eggs");
            if (Dm.truthy(liked_foodtypes))
                Dm.append(ref dot, span_info(I18n.T("This tongue has an affinity for the taste of [].", english_list(bitfield_to_list(liked_foodtypes, FOOD_FLAGS_IC) as DmList))));
            if (Dm.truthy(disliked_foodtypes))
                Dm.append(ref dot, span_info(I18n.T("This tongue has an aversion for the taste of [].", english_list(bitfield_to_list(disliked_foodtypes, FOOD_FLAGS_IC) as DmList))));
            if (Dm.truthy(toxic_foodtypes))
                Dm.append(ref dot, span_info(I18n.T("This tongue's physiology makes [] toxic.", english_list(bitfield_to_list(toxic_foodtypes, FOOD_FLAGS_IC) as DmList))));
        }
        return dot;
    }

    public virtual object get_possible_languages()
    {
        object dot = null;
        // This is the default list of languages most humans should be capable of speaking
        dot = Dm.list(
            typeof(datum_language_common),
            typeof(datum_language_uncommon),
            typeof(datum_language_spinwarder),
            typeof(datum_language_draconic),
            typeof(datum_language_codespeak),
            typeof(datum_language_monkey),
            typeof(datum_language_narsie),
            typeof(datum_language_beachbum),
            typeof(datum_language_aphasia),
            typeof(datum_language_piratespeak),
            typeof(datum_language_moffic),
            typeof(datum_language_sylvan),
            typeof(datum_language_shadowtongue),
            typeof(datum_language_terrum),
            typeof(datum_language_nekomimetic));
        if (Dm.truthy(languages_native))
        {
            DmList possible = dot as DmList;
            Dm.combine(ref possible, languages_native);
            dot = possible;
        }
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:174
    public override object could_speak_language(Type language_path = null)
    {
        return Dm.@in(language_path, languages_possible) ? TRUE : FALSE;
    }

    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:106
    [SignalHandler]
    public virtual void handle_speech(datum source = null, DmList speech_args = null)
    {
        if (Dm.truthy(should_modify_speech(source, speech_args)))
            modify_speech(source, speech_args);
    }

    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:112
    public virtual object should_modify_speech(datum source = null, DmList speech_args = null)
    {
        if (Dm.@in(speech_args[SPEECH_LANGUAGE], languages_native)) // Speaking a native language?
            return FALSE; // Don't modify speech
        if (HAS_TRAIT(source, TRAIT_SIGN_LANG)) // No modifiers for signers - I hate this but I simply cannot get these to combine into one statement
            return FALSE; // Don't modify speech
        return TRUE;
    }

    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:119
    public virtual object modify_speech(datum source = null, DmList speech_args = null)
    {
        return speech_args[SPEECH_MESSAGE];
    }

    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:129
    public virtual object get_food_taste_reaction(obj_item food = null, float? foodtypes = null)
    {
        foodtypes ??= NONE;
        object food_taste_reaction = null;
        if (Dm.truthy(Dm.bitand(foodtypes, toxic_foodtypes)))
            food_taste_reaction = FOOD_TOXIC;
        else if (Dm.truthy(Dm.bitand(foodtypes, disliked_foodtypes)))
            food_taste_reaction = FOOD_DISLIKED;
        else if (Dm.truthy(Dm.bitand(foodtypes, liked_foodtypes)))
            food_taste_reaction = FOOD_LIKED;
        return food_taste_reaction;
    }

    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:184
    [SignalHandler]
    public virtual void get_tongue_emote_sound(datum source = null, object key = null, DmList sounds = null)
    {
        object sound_override = get_emote_sound_from_list((emote_sounds as DmList)[key], owner);
        sounds[sound_override] = EMOTE_SOUND_TONGUE;
    }
}

// tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:139 (interleaved types, rules 1.9)
public partial class obj_item_organ_tongue : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:139
    [DmParams("receiver", "special", "movement_flags")]
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon receiver = organ_owner;
        object dot = null;
        dot = await base.on_mob_insert(receiver, special, movement_flags);
        foreach (object key in Dm.each(emote_sounds))
            RegisterSignal(receiver, COMSIG_MOB_EMOTE_SOUND(key), nameof(get_tongue_emote_sound));
        if (Dm.truthy(modifies_speech))
            RegisterSignal(receiver, COMSIG_MOB_SAY, nameof(handle_speech));
        receiver.voice_filter = voice_filter;
        /* This could be slightly simpler, by making the removal of the
        * NO_TONGUE_TRAIT conditional on the tongue's `sense_of_taste`, but
        * then you can distinguish between ageusia from no tongue, and
        * ageusia from having a non-tasting tongue.
        */
        REMOVE_TRAIT(receiver, TRAIT_AGEUSIA, NO_TONGUE_TRAIT);
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:153
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon organ_owner__carbon = organ_owner as mob_living_carbon;
        await base.on_mob_remove(organ_owner, special, movement_flags);

        temp_say_mod = "";
        foreach (object key in Dm.each(emote_sounds))
            UnregisterSignal(organ_owner__carbon, COMSIG_MOB_EMOTE_SOUND(key));
        UnregisterSignal(organ_owner__carbon, COMSIG_MOB_SAY);
        // Carbons by default start with NO_TONGUE_TRAIT caused TRAIT_AGEUSIA
        ADD_TRAIT(organ_owner__carbon, TRAIT_AGEUSIA, NO_TONGUE_TRAIT);
        organ_owner__carbon.voice_filter = Dm.initial(organ_owner__carbon, (atom_movable p) => p.voice_filter);
    }

    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:166
    public override async DmTask on_begin_failure()
    {
        remove_organ_trait(TRAIT_SPEAKS_CLEARLY);
        add_organ_trait(TRAIT_AGEUSIA);
    }

    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:170
    public override void on_failure_recovery()
    {
        if (Dm.truthy(speakable_with))
            add_organ_trait(TRAIT_SPEAKS_CLEARLY);
        if (Dm.truthy(sense_of_taste))
            remove_organ_trait(TRAIT_AGEUSIA);
    }

    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:177
    public override object get_availability(datum_species owner_species = null, mob_living owner_mob = null)
    {
        return owner_species.mutanttongue;
    }
}

// tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:190 (gaps P3b: the lizard infusion)
public partial class obj_item_organ_tongue_lizard : obj_item_organ_tongue
{
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:210  var/static/list/speech_replacements (a type static, rules 10.6)
    private static DmList speech_replacements => TgState.Current.obj_item_organ_tongue_lizard__speech_replacements ??= Dm.list(
        Dm.kv(Dm.@new<regex>("s+", "g"), "sss"),
        Dm.kv(Dm.@new<regex>("S+", "g"), "SSS"),
        Dm.kv(Dm.@new<regex>(@"(\w)x", "g"), "$1kss"),
        Dm.kv(Dm.@new<regex>(@"(\w)X", "g"), "$1KSSS"),
        Dm.kv(Dm.@new<regex>(@"\bx([\-|r|R]|\b)", "g"), "ecks$1"),
        Dm.kv(Dm.@new<regex>(@"\bX([\-|r|R]|\b)", "g"), "ECKS$1"));

    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:219
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_speechmod), Dm.named("replacements", speech_replacements), Dm.named("should_modify_speech", Dm.@new<datum_callback>(this, nameof(should_modify_speech)))));
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:210  var/static/list/speech_replacements
    internal DmList obj_item_organ_tongue_lizard__speech_replacements;
}

// tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:520  (gaps X5: the xenomorphs' tongue)
public partial class obj_item_organ_tongue_alien : obj_item_organ_tongue
{
    // Aliens can only speak alien and a few other languages.
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:536
    public override object get_possible_languages()
    {
        return Dm.list(
            typeof(datum_language_xenocommon),
            typeof(datum_language_common),
            typeof(datum_language_uncommon),
            typeof(datum_language_draconic), // Both hiss?
            typeof(datum_language_monkey));
    }

    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:545
    public override object modify_speech(datum source = null, DmList speech_args = null)
    {
        object hivemind = speech_args[SPEECH_SAYMODE]; // tg types it /datum/saymode/xeno; any say mode skips the hiss
        if (Dm.truthy(hivemind))
            return null;

        playsound(owner, SFX_HISS, 25, TRUE, TRUE);
        return null;
    }
}

// tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:602  (silicons SI10, D326: the android's voicebox)
public partial class obj_item_organ_tongue_robot : obj_item_organ_tongue
{
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:619
    public override object could_speak_language(Type language_path = null) => TRUE; // THE MAGIC OF ELECTRONICS

    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:622
    public override object modify_speech(datum source = null, DmList speech_args = null)
    {
        var spans = speech_args[SPEECH_SPANS] as DmList;
        if (spans != null && !Dm.@in(SPAN_ROBOT, spans))
            spans.Add(SPAN_ROBOT); // speech_args[SPEECH_SPANS] |= SPAN_ROBOT
        return null;
    }

    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:625
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        organ_owner.grant_language(typeof(datum_language_machine), source: LANGUAGE_TONGUE);
        to_chat(organ_owner, span_boldnotice(I18n.T("You gain a new understanding of [].", Dm.initial(typeof(datum_language_machine), (datum_language l) => l.name))));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:630
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        var owner = organ_owner as mob_living_carbon;
        if (QDELING(owner))
            return;
        owner.remove_language(typeof(datum_language_machine), source: LANGUAGE_TONGUE);
        to_chat(owner, span_boldnotice(I18n.T("You're not really sure what beeps and boops mean anymore.")));
    }
}

// tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:180 (gaps OG1)
public partial class obj_item_organ_tongue : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:180
    public override object feel_for_damage(object self_aware = null)
    {
        // No effect
        return "";
    }
}

// tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:230 (gaps OG1: the silverscale's statue)
public partial class datum_action_cooldown_turn_to_statue : datum_action_cooldown
{
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:241
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        object Target = Dm.arg(__extra, 0);
        await base.New(__extra);
        if (!istype(Target, typeof(obj_item_organ_tongue_lizard_silver)))
        {
            stack_trace("Non-silverscale tongue initialized a turn to statue action.", "code/modules/surgery/organs/internal/tongue/_tongue.dm", 244);
            qdel(this);
            return;
        }

        await init_statue();
    }

    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:250
    public override object Destroy(float? force = null)
    {
        clean_up_statue();
        return base.Destroy(force);
    }

    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:254
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return FALSE;

        if (!isliving(owner))
            return FALSE;
        obj_item_organ_tongue_lizard_silver tongue_target = target as obj_item_organ_tongue_lizard_silver;
        if (tongue_target.owner != owner)
            return FALSE;

        if (statue == null)
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, I18n.T("you can't seem to statue-ize!"));
            return FALSE; // permanently bricked
        }
        if (IS_UNCONSCIOUS_OR_CRIT(owner))
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, I18n.T("you're too weak!"));
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:276
    public override async DmTask<object> Activate(atom target = null)
    {
        StartCooldown(3 * SECONDS);

        bool is_statue = owner.loc == statue;
        if (!is_statue)
        {
            owner.visible_message(
                span_notice(I18n.T("[] strikes a glorious pose.", owner)),
                span_notice(I18n.T("You strike a glorious pose as you become a statue!")));
        }

        owner.balloon_alert(owner, is_statue ? I18n.T("breaking free...") : I18n.T("striking a pose..."));
        if (!Dm.truthy(await do_after(owner, (is_statue ? 0.5f * SECONDS : 3 * SECONDS), target: get_turf(owner))))
        {
            owner.balloon_alert(owner, I18n.T("interrupted!"));
            return null;
        }

        StartCooldown();

        statue.name = I18n.T("statue of []", owner.real_name);
        statue.desc = I18n.T("statue depicting []", owner.real_name);

        if (is_statue)
        {
            statue.visible_message(span_danger(I18n.T("[] becomes animated!", statue)));
            await owner.forceMove(get_turf(statue));
            await statue.moveToNullspace();
            UnregisterSignal(owner, COMSIG_MOVABLE_MOVED);
        }
        else
        {
            owner.visible_message(
                span_notice(I18n.T("[] hardens into a silver statue.", owner)),
                span_notice(I18n.T("You have become a silver statue!")));
            statue.set_visuals(owner.appearance);
            await statue.forceMove(get_turf(owner));
            await owner.forceMove(statue);
            RegisterSignal(owner, COMSIG_MOVABLE_MOVED, nameof(human_left_statue));

            mob_living living_owner = owner as mob_living;
            statue.update_integrity(living_owner.health); // Statue has 100 health, humans have 100 health
        }
        return null;
    }

    /// Somehow they used an exploit/teleportation to leave statue, lets clean up
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:316
    [SignalHandler]
    public virtual void human_left_statue(atom_movable mover = null, atom oldloc = null, object direction = null)
    {
        Waiting.NoSleep(statue.moveToNullspace());
        UnregisterSignal(mover, COMSIG_MOVABLE_MOVED);
    }

    /// Statue was destroyed via IC means (destruction / deconstruction), dust the owner and drop their stuff
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:323
    [SignalHandler]
    public virtual void statue_destroyed(datum source = null)
    {
        if (statue.loc == null)
            return; // the statue ended up getting destroyed while in nullspace?

        mob_living_carbon carbon_owner = owner as mob_living_carbon;
        UnregisterSignal(carbon_owner, COMSIG_MOVABLE_MOVED);

        to_chat(carbon_owner, span_userdanger(I18n.T("Your existence as a living creature snaps as your statue form crumbles!")));
        Waiting.NoSleep(carbon_owner.forceMove(get_turf(statue)));
        Waiting.NoSleep(carbon_owner.dust(just_ash: TRUE, drop_items: TRUE));
        carbon_owner.investigate_log("has been dusted from having their Silverscale Statue deconstructed / destroyed.", INVESTIGATE_DEATHS);

        clean_up_statue(); // unregister signal before we can do further side effects.
    }

    /// Statue was qdeleted outright, do nothing but clear refs.
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:340
    [SignalHandler]
    public virtual void statue_deleted(datum source = null)
    {
        clean_up_statue(); // Note that if the lizard is in the statue when they're raw deleted, they too will be raw deleted. This is fine
    }

    /// Initializes the statue we're going to hang around inside
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:346
    public virtual async DmTask init_statue()
    {
        statue = Dm.@new<obj_structure_statue_custom>();
        await statue.set_custom_materials(Dm.list(Dm.kv(typeof(datum_material_silver), SHEET_MATERIAL_AMOUNT * 5)));
        statue.max_integrity = 100; // statues already have 100 max integrity, so this is a safety net
        statue.set_armor(typeof(datum_armor_obj_structure_silverscale_statue_armor));
        statue.flags_ricochet = Dm.bitor(statue.flags_ricochet, RICOCHET_SHINY);
        RegisterSignals(statue, Dm.list(COMSIG_OBJ_DECONSTRUCT, COMSIG_ATOM_DESTRUCTION), nameof(statue_destroyed));
        RegisterSignal(statue, COMSIG_QDELETING, nameof(statue_deleted));
    }

    /// Cleans up the reference to the statue and unregisters signals
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:356
    public virtual void clean_up_statue()
    {
        if (QDELETED(statue))
        {
            statue = null;
            return;
        }

        UnregisterSignal(statue, Dm.list(COMSIG_OBJ_DECONSTRUCT, COMSIG_ATOM_DESTRUCTION, COMSIG_QDELETING));
        QDEL_NULL(ref statue);
    }
}

// tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:381 (gaps OG1)
public partial class obj_item_organ_tongue_abductor : obj_item_organ_tongue
{
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:391
    [DmParams("tongue_holder")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        mob_living_carbon_human tongue_holder = user as mob_living_carbon_human;
        if (!istype(tongue_holder, typeof(mob_living_carbon_human)))
            return null;

        obj_item_organ_tongue_abductor tongue = tongue_holder.get_organ_slot(ORGAN_SLOT_TONGUE) as obj_item_organ_tongue_abductor;
        if (!istype(tongue, typeof(obj_item_organ_tongue_abductor)))
            return null;

        if (Dm.eq(tongue.mothership, mothership))
            to_chat(tongue_holder, span_notice(I18n.T("[] is already attuned to the same channel as your own.", this)));

        tongue_holder.visible_message(span_notice(I18n.T("[] holds [] in their hands, and concentrates for a moment.", tongue_holder, this)), span_notice(I18n.T("You attempt to modify the attenuation of [].", this)));
        if (Dm.truthy(await do_after(tongue_holder, delay: 15, target: this)))
        {
            to_chat(tongue_holder, span_notice(I18n.T("You attune [] to your own channel.", this)));
            mothership = tongue.mothership;
        }
        return null;
    }

    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:407
    [DmParams("examining_mob")]
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (HAS_MIND_TRAIT(user, TRAIT_ABDUCTOR_TRAINING) || isobserver(user))
        {
            Dm.append(ref dot, span_notice(I18n.T("It can be attuned to a different channel by using it inhand.")));
            if (!Dm.truthy(mothership))
                Dm.append(ref dot, span_notice(I18n.T("It is not attuned to a specific mothership.")));
            else
                Dm.append(ref dot, span_notice(I18n.T("It is attuned to [].", mothership)));
        }
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:416
    public override object modify_speech(datum source = null, DmList speech_args = null)
    {
        //Hacks
        object message = speech_args[SPEECH_MESSAGE];
        mob_living_carbon_human user = source as mob_living_carbon_human;
        string rendered = span_abductor(Dm.text("<b>[]:</b> []", user.real_name, message));
        user.log_talk(message, LOG_SAY, tag: SPECIES_ABDUCTOR);
        foreach (mob_living_carbon_human living_mob in Dm.each_cast<mob_living_carbon_human>(GLOB.alive_mob_list))
        {
            obj_item_organ_tongue_abductor tongue = living_mob.get_organ_slot(ORGAN_SLOT_TONGUE) as obj_item_organ_tongue_abductor;
            if (!istype(tongue, typeof(obj_item_organ_tongue_abductor)))
                continue;
            if (Dm.eq(mothership, tongue.mothership))
                to_chat(living_mob, rendered, type: MESSAGE_TYPE_RADIO, avoid_highlighting: Dm.num(user == living_mob));
        }

        foreach (mob dead_mob in Dm.each_cast<mob>(GLOB.dead_mob_list))
        {
            string link = FOLLOW_LINK(dead_mob, user);
            to_chat(dead_mob, Dm.text("[] []", link, rendered), type: MESSAGE_TYPE_RADIO);
        }

        speech_args[SPEECH_MESSAGE] = "";
        return null;
    }
}

// tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:435 (gaps OG1)
public partial class obj_item_organ_tongue_zombie : obj_item_organ_tongue
{
    // List of english words that translate to zombie phrases
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:450  var/static/list/english_to_zombie (a type static, rules 10.6)
    private static DmList english_to_zombie
    {
        get => TgState.Current.obj_item_organ_tongue_zombie__english_to_zombie ??= Dm.list();
        set => TgState.Current.obj_item_organ_tongue_zombie__english_to_zombie = value;
    }
    /// Spooky growls we sometimes play while alive
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:452  var/static/list/spooks
    private static DmList spooks => TgState.Current.obj_item_organ_tongue_zombie__spooks ??= Dm.list(
        Dm.resource("sound/effects/hallucinations/growl1.ogg"),
        Dm.resource("sound/effects/hallucinations/growl2.ogg"),
        Dm.resource("sound/effects/hallucinations/growl3.ogg"),
        Dm.resource("sound/effects/hallucinations/veryfar_noise.ogg"),
        Dm.resource("sound/effects/hallucinations/wail.ogg"));

    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:462
    public virtual void add_word_to_translations(object english_word = null, object zombie_word = null)
    {
        english_to_zombie[english_word] = zombie_word;
        // zombies don't care about grammar (any tense or form is all translated to the same word)
        english_to_zombie[Dm.add(english_word, plural_s(english_word))] = zombie_word;
        english_to_zombie[Dm.add(english_word, "ing")] = zombie_word;
        english_to_zombie[Dm.add(english_word, "ed")] = zombie_word;
    }

    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:469
    public virtual void load_zombie_translations()
    {
        DmList zombie_translation = strings("zombie_replacement.json", "zombie") as DmList;
        foreach (object zombie_word in Dm.each(zombie_translation))
        {
            // since zombie words are a reverse list, we gotta do this backwards
            DmList data = islist(zombie_translation[zombie_word]) ? zombie_translation[zombie_word] as DmList : Dm.list(zombie_translation[zombie_word]);
            foreach (object english_word in Dm.each(data))
                add_word_to_translations(english_word, zombie_word);
        }
        english_to_zombie = sort_list(english_to_zombie) as DmList; // Alphabetizes the list (for debugging)
    }

    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:478
    public override object modify_speech(datum source = null, DmList speech_args = null)
    {
        string message = speech_args[SPEECH_MESSAGE] as string;
        if (Dm.textchar(message, 1) != "*")
        {
            // setup the global list for translation if it hasn't already been done
            if (!Dm.truthy(length(english_to_zombie)))
                load_zombie_translations();

            // make a list of all words that can be translated
            DmList message_word_list = splittext(message, " ");
            DmList translated_word_list = Dm.list();
            foreach (object word__it in Dm.each(message_word_list))
            {
                object word = english_to_zombie[LOWER_TEXT(word__it)];
                Dm.append(translated_word_list, Dm.truthy(word) ? word : FALSE);
            }

            // all occurrences of characters "eiou" (case-insensitive) are replaced with "r"
            message = replacetext(message, Dm.regex(@"[eiou]", "ig"), "r");
            // all characters other than "zhrgbmna .!?-" (case-insensitive) are stripped
            message = replacetext(message, Dm.regex(@"[^zhrgbmna.!?-\s]", "ig"), "");
            // multiple spaces are replaced with a single (whitespace is trimmed)
            message = replacetext(message, Dm.regex(@"(\s+)", "g"), " ");

            DmList old_words = splittext(message, " ");
            DmList new_words = Dm.list();
            foreach (object word__it in Dm.each(old_words))
            {
                object word = word__it;
                // lower-case "r" at the end of words replaced with "rh"
                word = replacetext(word, Dm.regex(@"\lr\b"), "rh");
                // an "a" or "A" by itself will be replaced with "hra"
                word = replacetext(word, Dm.regex(@"\b[Aa]\b"), "hra");
                Dm.append(new_words, word);
            }

            // if words were not translated, then we apply our zombie speech patterns
            for (float i = 1, i__end = length(new_words); i <= i__end; i++)
                new_words[i] = Dm.truthy(translated_word_list[i]) ? translated_word_list[i] : new_words[i];

            message = new_words.Join(" ");
            message = capitalize(message) as string;
            speech_args[SPEECH_MESSAGE] = message;
        }
        return null;
    }

    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:516
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);
        if (!IS_UNCONSCIOUS_OR_CRIT(owner) && SPT_PROB(2, seconds_per_tick))
            playsoundtoken(owner, pick(spooks), 50, TRUE, 10);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:450, :452  the zombie tongue's type statics
    internal DmList obj_item_organ_tongue_zombie__english_to_zombie;
    internal DmList obj_item_organ_tongue_zombie__spooks;
}

// tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:552 (gaps OG1)
public partial class obj_item_organ_tongue_bone : obj_item_organ_tongue
{
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:568
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        phomeme_type = Dm.str(pick(phomeme_types));
        return dot;
    }

    // Bone tongues can speak all default + calcic
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:573
    public override object get_possible_languages()
    {
        return Dm.add(base.get_possible_languages(), typeof(datum_language_calcic));
    }

    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:576
    public override object modify_speech(datum source = null, DmList speech_args = null)
    {
        if (Dm.truthy(chattering))
            chatter(speech_args[SPEECH_MESSAGE], phomeme_type, source as atom);
        string sw__1 = phomeme_type;
        switch (sw__1)
        {
            case "sans":
                Dm.combine(speech_args[SPEECH_SPANS] as DmList, SPAN_SANS);
                break;
            case "papyrus":
                Dm.combine(speech_args[SPEECH_SPANS] as DmList, SPAN_PAPYRUS);
                break;
        }
        return null;
    }
}

// tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:637 (gaps OG1)
public partial class obj_item_organ_tongue_snail : obj_item_organ_tongue
{
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:644
    public override object modify_speech(datum source = null, DmList speech_args = null)
    {
        string new_message = null;
        string message = speech_args[SPEECH_MESSAGE] as string;
        for (float i = 1, i__end = length(message); i <= i__end; i++)
        {
            string ch = Dm.textchar(message, i);
            if (Dm.truthy(findtext("ABCDEFGHIJKLMNOPWRSTUVWXYZabcdefghijklmnopqrstuvwxyz", ch))) //Im open to suggestions
                new_message += ch + ch + ch; //aaalllsssooo ooopppeeennn tttooo sssuuuggggggeeessstttiiiooonsss
            else
                new_message += ch;
        }
        speech_args[SPEECH_MESSAGE] = new_message;
        return null;
    }
}

// tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:699 (gaps OG1)
public partial class obj_item_organ_tongue_jelly : obj_item_organ_tongue
{
    // tg: code/modules/surgery/organs/internal/tongue/_tongue.dm:708
    public override object get_food_taste_reaction(obj_item food = null, float? foodtypes = null)
    {
        foodtypes ??= NONE;
        // a silver slime created this? what a delicacy!
        if (HAS_TRAIT(food, TRAIT_FOOD_SILVER))
            return FOOD_LIKED;
        return base.get_food_taste_reaction(food, foodtypes);
    }
}
