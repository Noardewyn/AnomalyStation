// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/mobs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only spawn_atom_to_turf (M5 slice 6: an airlock's removed shielding), random_human_blood_type, get_roundstart_blood_types (M8 slice C), random_eye_color, random_hair_color, GLOB.skin_tones and GLOB.skin_tone_names (M10x slice D4) (and M10x slice D1: the accessory pickers, random_underwear to random_facial_hairstyle; random_backpack waits for GLOB.backpacklist) (M2 P2: a human's
// randomize_human_normie, modules/admin/create_mob.dm:24), parse_zone and deprecise_zone (M2 P2: the bodyparts' names and
// zones), push_usr (/world/proc/push_usr, mobs.dm:709-716, which /datum/callback needs; by the datum package) and the
// macros ISADVANCEDTOOLUSER and GET_CLIENT translated, and get_emote_sound_from_list (M4a clothing: the masks' emote sounds); the rest of mobs.dm is translated with the mob code.
// M2 P1 (the living core) added the members of its paths, marked "M2 P1" below; M3 P3 added /mob/proc/do_after_count
// (the doors' delayed unrestricted latch); M4b slice 1 added get_temp_change_amount (the species' body temperature); M6
// slice 2 added /mob/living/proc/parse_zone_with_bodypart (the clothing's torn zones); M7 power helpers slice added dview, GLOB.dview_mob and /mob/dview
// (the bluespace light replacer's scan). M10x slice B added deadchat_broadcast (dead chat). Gaps P5 added create_random_mob
// (the life reactions' mobs). Syndicate A2 added active_ais (the traitor's destroy-the-AI objective); silicons SI2 the AI and free-borg pickers (:371-428). Gaps ST2 added
// check_target_facings with its FACING_* defines (:1-7, the basketball's steal). Gaps MO1 added parse_slot_flags (the MOD modules' slots).
// Gaps MS1 added add_personality_mood_to_viewers (the moods).

public partial class mob : atom_movable
{
    // tg: code/__HELPERS/mobs.dm:618
    public virtual async DmTask<object> apply_pref_name(object preference_type = null, client requesting_client = null)
    {
        requesting_client ??= client;
        string oldname = Dm.str(real_name);
        object newname = null;
        int loop = 1;
        int safety = 0;

        bool random = Dm.truthy(config.Get(typeof(datum_config_entry_flag_force_random_names))) || (requesting_client != null && Dm.truthy(is_banned_from(requesting_client.ckey, Dm.list("Appearance"))));

        while (loop != 0 && safety < 5)
        {
            if (safety == 0 && !random)
            {
                newname = requesting_client?.prefs?.read_preference(preference_type);
            }
            else
            {
                datum_preference preference = GLOB.preference_entries[preference_type] as datum_preference;
                if (requesting_client?.prefs != null)
                    newname = preference.create_informed_default_value(requesting_client.prefs);
                else
                    newname = preference.create_default_value();
            }

            foreach (mob_living checked_mob in Dm.each<mob_living>(GLOB.player_list))
            {
                if (checked_mob == this)
                    continue;
                if (!Dm.truthy(newname) || Dm.eq(checked_mob.real_name, newname))
                {
                    newname = null;
                    loop++; // name is already taken so we roll again
                    break;
                }
            }
            loop--;
            safety++;
        }

        if (Dm.truthy(newname))
        {
            await fully_replace_character_name(oldname, newname);
            return TRUE;
        }
        return FALSE;
    }
}

public static partial class GlobalProcs
{
    //check_target_facings() return defines
    /// Two mobs are facing the same direction
    // tg: code/__HELPERS/mobs.dm:3  #define FACING_SAME_DIR 1 (gaps ST2: the basketball's steal)
    public const float FACING_SAME_DIR = 1;
    /// Two mobs are facing each others
    // tg: code/__HELPERS/mobs.dm:5  #define FACING_EACHOTHER 2
    public const float FACING_EACHOTHER = 2;
    /// Two mobs one is facing a person, but the other is perpendicular
    // tg: code/__HELPERS/mobs.dm:7  #define FACING_INIT_FACING_TARGET_TARGET_FACING_PERPENDICULAR 3
    public const float FACING_INIT_FACING_TARGET_TARGET_FACING_PERPENDICULAR = 3; //Do I win the most informative but also most stupid define award?

    ///Returns the direction that the initiator and the target are facing
    // tg: code/__HELPERS/mobs.dm:590  (gaps ST2: the basketball's steal)
    public static object check_target_facings(mob_living initiator = null, mob_living target = null)
    {
        /*This can be used to add additional effects on interactions between mobs depending on how the mobs are facing each other, such as adding a crit damage to blows to the back of a guy's head.
        Given how click code currently works (Nov '13), the initiating mob will be facing the target mob most of the time
        That said, this proc should not be used if the change facing proc of the click code is overridden at the same time*/
        if (!isliving(target) || Dm.eq(target.body_position, LYING_DOWN))
            //Make sure we are not doing this for things that can't have a logical direction to the players given that the target would be on their side
            return FALSE;
        if (initiator.dir == target.dir) //mobs are facing the same direction
            return FACING_SAME_DIR;
        if (Dm.truthy(is_source_facing_target(initiator, target)) && Dm.truthy(is_source_facing_target(target, initiator))) //mobs are facing each other
            return FACING_EACHOTHER;
        if (initiator.dir + 2 == target.dir || initiator.dir - 2 == target.dir || initiator.dir + 6 == target.dir || initiator.dir - 6 == target.dir) //Initating mob is looking at the target, while the target mob is looking in a direction perpendicular to the 1st
            return FACING_INIT_FACING_TARGET_TARGET_FACING_PERPENDICULAR;
        return null;
    }

    /// Returns one of the human blood types at random, weighted by their rarity
    // tg: code/__HELPERS/mobs.dm:10
    public static object random_human_blood_type()
    {
        return get_blood_type(pick_weight(
            Dm.list(
                Dm.kv(typeof(datum_blood_type_human_o_minus), 4),
                Dm.kv(typeof(datum_blood_type_human_o_plus), 36),
                Dm.kv(typeof(datum_blood_type_human_a_minus), 3),
                Dm.kv(typeof(datum_blood_type_human_a_plus), 28),
                Dm.kv(typeof(datum_blood_type_human_b_minus), 1),
                Dm.kv(typeof(datum_blood_type_human_b_plus), 20),
                Dm.kv(typeof(datum_blood_type_human_ab_minus), 1),
                Dm.kv(typeof(datum_blood_type_human_ab_plus), 5))));
    }

    /// Returns a list of all blood types a roundstart species can have (M8 slice C: the random blood packs)
    // tg: code/__HELPERS/mobs.dm:24
    public static object get_roundstart_blood_types()
    {
        DmList cached_blood_types = TgState.Current.get_roundstart_blood_types__cached_blood_types;
        if (Dm.truthy(Dm.length(cached_blood_types)))
            return cached_blood_types;

        cached_blood_types = Dm.list();
        TgState.Current.get_roundstart_blood_types__cached_blood_types = cached_blood_types;
        Dm.combine(ref cached_blood_types, subtypesof(typeof(datum_blood_type_human)));
        foreach (object species_id in Dm.each(get_selectable_species()))
        {
            Type species_type = GLOB.species_list[species_id] as Type;
            Type exotic = Dm.initial(species_type, (datum_species p) => p.exotic_bloodtype);
            if (exotic != null)
                Dm.combine(ref cached_blood_types, exotic);
        }

        return cached_blood_types;
    }

    // tg: code/__HELPERS/mobs.dm:38
    public static object random_eye_color()
    {
        string sw__1 = Dm.pick_weighted((20, "brown"), (20, "hazel"), (20, "grey"), (15, "blue"), (15, "green"), (1, "amber"), (1, "albino")) as string;
        switch (sw__1)
        {
            case "brown":
                return COLOR_BROWNER_BROWN;
            case "hazel":
                return "#554422";
            case "grey":
                return pick("#666666", "#777777", "#888888", "#999999", "#aaaaaa", "#bbbbbb", "#cccccc");
            case "blue":
                return "#3366cc";
            case "green":
                return "#006600";
            case "amber":
                return "#ffcc00";
            case "albino":
                return Dm.add(Dm.add(Dm.add("#", pick("cc", "dd", "ee", "ff")), pick("00", "11", "22", "33", "44", "55", "66", "77", "88", "99")), pick("00", "11", "22", "33", "44", "55", "66", "77", "88", "99"));
            default:
                return COLOR_BLACK;
        }
    }

    // tg: code/__HELPERS/mobs.dm:354
    public static async DmTask dance_rotate(atom_movable AM = null, datum_callback callperrotate = null, float? set_original_dir = null)
    {
        set_original_dir ??= FALSE;
        object originaldir = AM.dir;
        foreach (float i in new[] { NORTH, SOUTH, EAST, WEST, EAST, SOUTH, NORTH, SOUTH, EAST, WEST, EAST, SOUTH })
        {
            if (AM == null)
                return;
            await AM.setDir(i);
            if (callperrotate != null)
                await callperrotate.Invoke();
            await Dm.sleep(0.1f * SECONDS);
        }
        if (Dm.truthy(set_original_dir))
            await AM.setDir(originaldir);
    }

    // tg: code/__HELPERS/mobs.dm:57
    public static object random_hair_color()
    {
        return pick(random_hair_color__natural_hair_colors);
    }

    // tg: code/__HELPERS/mobs.dm:58  var/static/list/natural_hair_colors (a proc-level static, rules 10.6)
    private static ref DmList random_hair_color__natural_hair_colors => ref TgState.Current.GlobalProcs__random_hair_color__natural_hair_colors;

    // tg: code/__HELPERS/mobs.dm:71
    public static object random_underwear(object gender = null)
    {
        if (Dm.length(SSaccessories.underwear_list) == 0)
            throw Dm.CRASH("No underwear to choose from!", "code/__HELPERS/mobs.dm", 73);
        switch (gender)
        {
            case MALE:
                return pick(SSaccessories.underwear_m);
            case FEMALE:
                return pick(SSaccessories.underwear_f);
            default:
                return pick(SSaccessories.underwear_list);
        }
    }

    // tg: code/__HELPERS/mobs.dm:82
    public static object random_undershirt(object gender = null)
    {
        if (Dm.length(SSaccessories.undershirt_list) == 0)
            throw Dm.CRASH("No undershirts to choose from!", "code/__HELPERS/mobs.dm", 84);
        switch (gender)
        {
            case MALE:
                return pick(SSaccessories.undershirt_m);
            case FEMALE:
                return pick(SSaccessories.undershirt_f);
            default:
                return pick(SSaccessories.undershirt_list);
        }
    }

    // tg: code/__HELPERS/mobs.dm:93
    public static object random_socks()
    {
        if (Dm.length(SSaccessories.socks_list) == 0)
            throw Dm.CRASH("No socks to choose from!", "code/__HELPERS/mobs.dm", 95);
        return pick(SSaccessories.socks_list);
    }

    // tg: code/__HELPERS/mobs.dm:101
    public static object random_hairstyle(object gender = null)
    {
        switch (gender)
        {
            case MALE:
                return pick(SSaccessories.hairstyles_male_list);
            case FEMALE:
                return pick(SSaccessories.hairstyles_female_list);
            default:
                return pick(SSaccessories.hairstyles_list);
        }
    }

    // tg: code/__HELPERS/mobs.dm:110
    public static object random_facial_hairstyle(object gender = null)
    {
        switch (gender)
        {
            case MALE:
                return pick(SSaccessories.facial_hairstyles_male_list);
            case FEMALE:
                return pick(SSaccessories.facial_hairstyles_female_list);
            default:
                return pick(SSaccessories.facial_hairstyles_list);
        }
    }
}

// tg: code/__HELPERS/mobs.dm:119 (interleaved types, rules 1.9)
public partial class datum_controller_global_vars
{
    // tg: code/__HELPERS/mobs.dm:119  GLOBAL_LIST_INIT(skin_tones, sort_list(list(...)))
    public DmList skin_tones;

    // tg: code/__HELPERS/mobs.dm:119  (GLOBAL_MANAGED(skin_tones, sort_list(list(...))), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalskin_tones()
    {
        skin_tones = sort_list(Dm.list(
            "albino",
            "caucasian1",
            "caucasian2",
            "caucasian3",
            "latino",
            "mediterranean",
            "asian1",
            "asian2",
            "arab",
            "indian",
            "mixed1",
            "mixed2",
            "mixed3",
            "mixed4",
            "african1",
            "african2"
            )) as DmList;
        Dm.append(ref gvars_datum_init_order, "skin_tones");
    }

    // tg: code/__HELPERS/mobs.dm:138  GLOBAL_LIST_INIT(skin_tone_names, list(...))
    public DmList skin_tone_names;

    // tg: code/__HELPERS/mobs.dm:138  (GLOBAL_MANAGED(skin_tone_names, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalskin_tone_names()
    {
        skin_tone_names = Dm.list();
        skin_tone_names["african1"] = "Medium brown";
        skin_tone_names["african2"] = "Dark brown";
        skin_tone_names["albino"] = "Albino";
        skin_tone_names["arab"] = "Light brown";
        skin_tone_names["asian1"] = "Ivory";
        skin_tone_names["asian2"] = "Beige";
        skin_tone_names["caucasian1"] = "Porcelain";
        skin_tone_names["caucasian2"] = "Light peach";
        skin_tone_names["caucasian3"] = "Peach";
        skin_tone_names["indian"] = "Brown";
        skin_tone_names["latino"] = "Light beige";
        skin_tone_names["mediterranean"] = "Olive";
        skin_tone_names["mixed1"] = "Chestnut";
        skin_tone_names["mixed2"] = "Walnut";
        skin_tone_names["mixed3"] = "Coffee";
        skin_tone_names["mixed4"] = "Macadamia";
        Dm.append(ref gvars_datum_init_order, "skin_tone_names");
    }
}

public sealed partial class TgState
{
    // tg: code/__HELPERS/mobs.dm:58  var/static/list/natural_hair_colors = list(...)
    internal DmList GlobalProcs__random_hair_color__natural_hair_colors = Dm.list(
        "#111111", "#362925", "#3B3831", "#41250C", "#412922",
        "#544C49", "#583322", "#593029", "#703b30", "#714721",
        "#744729", "#74482a", "#7b746e", "#855832", "#863019",
        "#8c4734", "#9F550E", "#A29A96", "#A4381C", "#B17B41",
        "#C0BAB7", "#EFE5E4", "#F7F3F1", "#FFF2D6", "#a15537",
        "#a17e61", "#b38b67", "#ba673c", "#c89f73", "#d9b380",
        "#dbc9b8", "#e1621d", "#e17d17", "#e1af93", "#f1cc8f",
        "#fbe7a1");
}

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/mobs.dm:203
    public static atom spawn_atom_to_turf(object spawn_type = null, object target = null, object amount = null, float? admin_spawn = null, DmList extra_args = null)
    {
        admin_spawn ??= FALSE;
        turf T = get_turf(target as atom);
        if (T == null)
            throw Dm.CRASH(Dm.text("attempt to spawn atom type: [] in nullspace", spawn_type), "code/__HELPERS/mobs.dm", 206);

        DmList new_args = Dm.list(T);
        if (Dm.truthy(extra_args))
            Dm.append(ref new_args, extra_args);
        atom X = null;
        for (float j = 1; j <= Dm.num(amount); j++)
        {
            X = Dm.@new((Type)spawn_type, Dm.arglist(new_args)) as atom;
            if (Dm.truthy(admin_spawn))
                X.flags_1 = Dm.num(Dm.bitor(X.flags_1, ADMIN_SPAWNED_1));
        }
        return X; //return the last mob spawned
    }

    // tg: code/__HELPERS/mobs.dm:218  (the syndicate campaign G2: the badmin bomb cores)
    public static async DmTask<object> spawn_and_random_walk(object spawn_type = null, object target = null, object amount = null, float? walk_chance = null, float? max_walk = null, float? always_max_walk = null, float? admin_spawn = null, float? cardinals_only = null)
    {
        walk_chance ??= 100;
        max_walk ??= 3;
        always_max_walk ??= FALSE;
        admin_spawn ??= FALSE;
        cardinals_only ??= TRUE;
        turf T = get_turf(target as atom);
        float step_count = 0;
        if (T == null)
            throw Dm.CRASH(Dm.text("attempt to spawn atom type: [] in nullspace", spawn_type), "code/__HELPERS/mobs.dm", 222);

        DmList spawned_mobs = Dm.list_n(Dm.num(amount));

        for (float j = 1; j <= Dm.num(amount); j++)
        {
            atom_movable X;

            if (islist(spawn_type))
            {
                object mob_type = pick(spawn_type as DmList);
                X = Dm.@new((Type)mob_type, T) as atom_movable;
            }
            else
            {
                X = Dm.@new((Type)spawn_type, T) as atom_movable;
            }

            if (Dm.truthy(admin_spawn))
                X.flags_1 = Dm.num(Dm.bitor(X.flags_1, ADMIN_SPAWNED_1));

            spawned_mobs[j] = X;

            if (Dm.truthy(always_max_walk) || prob(walk_chance.Value))
            {
                if (Dm.truthy(always_max_walk))
                    step_count = max_walk.Value;
                else
                    step_count = rand(1, max_walk.Value);

                for (float i = 1; i <= step_count; i++)
                    await step(X, Dm.num(Dm.truthy(cardinals_only) ? pick(GLOB.cardinals) : pick(GLOB.alldirs)));
            }
        }

        return spawned_mobs;
    }

    ///Return a string for the specified body zone. Should be used for parsing non-instantiated bodyparts, otherwise use [/obj/item/bodypart/var/plaintext_zone]
    // tg: code/__HELPERS/mobs.dm:501
    public static object parse_zone(object zone = null)
    {
        object sw__1 = zone;
        if (Dm.eq(sw__1, BODY_ZONE_CHEST))
            return "chest";
        else if (Dm.eq(sw__1, BODY_ZONE_HEAD))
            return "head";
        else if (Dm.eq(sw__1, BODY_ZONE_PRECISE_R_HAND))
            return "right hand";
        else if (Dm.eq(sw__1, BODY_ZONE_PRECISE_L_HAND))
            return "left hand";
        else if (Dm.eq(sw__1, BODY_ZONE_L_ARM))
            return "left arm";
        else if (Dm.eq(sw__1, BODY_ZONE_R_ARM))
            return "right arm";
        else if (Dm.eq(sw__1, BODY_ZONE_L_LEG))
            return "left leg";
        else if (Dm.eq(sw__1, BODY_ZONE_R_LEG))
            return "right leg";
        else if (Dm.eq(sw__1, BODY_ZONE_PRECISE_L_FOOT))
            return "left foot";
        else if (Dm.eq(sw__1, BODY_ZONE_PRECISE_R_FOOT))
            return "right foot";
        else if (Dm.eq(sw__1, BODY_ZONE_PRECISE_GROIN))
            return "groin";
        else
            return zone;
    }

    ///Takes a zone and returns its "parent" zone, if it has one.
    // tg: code/__HELPERS/mobs.dm:529
    public static object deprecise_zone(object precise_zone = null)
    {
        object sw__1 = precise_zone;
        if (Dm.eq(sw__1, BODY_ZONE_PRECISE_GROIN))
            return BODY_ZONE_CHEST;
        else if (Dm.eq(sw__1, BODY_ZONE_PRECISE_EYES))
            return BODY_ZONE_HEAD;
        else if (Dm.eq(sw__1, BODY_ZONE_PRECISE_MOUTH))
            return BODY_ZONE_HEAD;
        else if (Dm.eq(sw__1, BODY_ZONE_PRECISE_R_HAND))
            return BODY_ZONE_R_ARM;
        else if (Dm.eq(sw__1, BODY_ZONE_PRECISE_L_HAND))
            return BODY_ZONE_L_ARM;
        else if (Dm.eq(sw__1, BODY_ZONE_PRECISE_L_FOOT))
            return BODY_ZONE_L_LEG;
        else if (Dm.eq(sw__1, BODY_ZONE_PRECISE_R_FOOT))
            return BODY_ZONE_R_LEG;
        else
            return precise_zone;
    }

    ///Returns a list of strings for a given slot flag.
    // tg: code/__HELPERS/mobs.dm:549 (gaps MO1: the MOD modules' required slots and the MODsuit window's part slots)
    public static object parse_slot_flags(object slot_flags = null)
    {
        DmList slot_strings = Dm.list();
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_BACK)))
            Dm.append(ref slot_strings, I18n.T("back"));
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_MASK)))
            Dm.append(ref slot_strings, I18n.T("mask"));
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_NECK)))
            Dm.append(ref slot_strings, I18n.T("neck"));
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_HANDCUFFED)))
            Dm.append(ref slot_strings, I18n.T("handcuff"));
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_LEGCUFFED)))
            Dm.append(ref slot_strings, I18n.T("legcuff"));
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_BELT)))
            Dm.append(ref slot_strings, I18n.T("belt"));
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_ID)))
            Dm.append(ref slot_strings, I18n.T("id"));
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_EARS)))
            Dm.append(ref slot_strings, I18n.T("ear"));
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_EYES)))
            Dm.append(ref slot_strings, I18n.T("glasses"));
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_GLOVES)))
            Dm.append(ref slot_strings, I18n.T("glove"));
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_HEAD)))
            Dm.append(ref slot_strings, I18n.T("head"));
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_FEET)))
            Dm.append(ref slot_strings, I18n.T("shoe"));
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_OCLOTHING)))
            Dm.append(ref slot_strings, I18n.T("oversuit"));
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_ICLOTHING)))
            Dm.append(ref slot_strings, I18n.T("undersuit"));
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_SUITSTORE)))
            Dm.append(ref slot_strings, I18n.T("suit storage"));
        if (Dm.truthy(Dm.bitand(slot_flags, Dm.bitor(ITEM_SLOT_LPOCKET, ITEM_SLOT_RPOCKET))))
            Dm.append(ref slot_strings, I18n.T("pocket"));
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_HANDS)))
            Dm.append(ref slot_strings, I18n.T("hand"));
        if (Dm.truthy(Dm.bitand(slot_flags, ITEM_SLOT_DEX_STORAGE)))
            Dm.append(ref slot_strings, I18n.T("dextrous storage"));
        return slot_strings;
    }
}

public static partial class GlobalProcs
{
    // Rules 1.5 and object-model.md section 9 (v1.2): a tg proc on /world is a C# extension method of the shim's facade
    // DmWorld, declared in GlobalProcs in the mirror file of its .dm file, so world.push_usr(...) is written as in DM.
    // It cannot be virtual (tg never subtypes /world); a call by name on world (Dm.Call(world, ...), the arglist form
    // in datums/callback.cs) reaches it through the binder, which passes world first.

    ///Makes a call in the context of a different usr. Use sparingly
    // tg: code/__HELPERS/mobs.dm:709
    public static async DmTask<object> push_usr(this DmWorld world, mob user_mob = null, datum_callback invoked_callback = null, params object[] __extra)
    {
        object dot = null;
        object temp = usr;
        usr = user_mob;
        DmList args = Dm.args(user_mob, invoked_callback, __extra);
        if (length(args) > 2)
            dot = await Dm.Call(invoked_callback, nameof(datum_callback.Invoke), Dm.arglist(args.Copy(3)));
        else
            dot = await invoked_callback.Invoke();
        usr = temp as mob;
        return dot;
    }

    // M2 integration (P4's click chain and interactions expanded it inline):
    // tg: code/__HELPERS/mobs.dm:443  #define ISADVANCEDTOOLUSER(mob) (HAS_TRAIT(mob, TRAIT_ADVANCEDTOOLUSER) && !HAS_TRAIT(mob, TRAIT_DISCOORDINATED_TOOL_USER))
    public static bool ISADVANCEDTOOLUSER(object mob) => HAS_TRAIT(mob, TRAIT_ADVANCEDTOOLUSER) && !HAS_TRAIT(mob, TRAIT_DISCOORDINATED_TOOL_USER);

    // M2 P1 (living core):
    // tg: code/__HELPERS/mobs.dm:447  #define GET_CLIENT(mob) (##mob.client || ##mob.mock_client)
    public static object GET_CLIENT(object mob) => (((mob)mob).client is var or__1 && Dm.truthy(or__1) ? or__1 : (object)((mob)mob).mock_client);
}

// tg: code/__HELPERS/mobs.dm:181  (M3 P3)
public partial class mob : atom_movable
{
    // tg: code/__HELPERS/mobs.dm:181
    public virtual object do_after_count()
    {
        float count = 0;
        foreach (object key in Dm.each(do_afters))
            count += Dm.num(do_afters[key]);
        return count;
    }
}

// tg: code/__HELPERS/mobs.dm:187  (M6 slice 7b: ismonkey of a human's height filters)
public static partial class GlobalProcs
{
    // tg: code/__HELPERS/mobs.dm:187
    public static object is_species(object A = null, Type species_datum = null)
    {
        object dot = FALSE;
        if (ishuman(A))
        {
            var H = (mob_living_carbon_human)A;
            if (Dm.truthy(H.dna) && istype(H.dna.species, species_datum))
                dot = TRUE;
        }
        return dot;
    }

    // tg: code/__DEFINES/is_helpers.dm:152  #define ismonkey(A) (is_species(A, /datum/species/monkey))
    public static bool ismonkey(object A) => Dm.truthy(is_species(A, typeof(datum_species_monkey)));
}

// tg: code/__HELPERS/mobs.dm:438  (interleaved types, rules 1.9)
public static partial class GlobalProcs
{
    /**
     * Used to get the amount of change between two body temperatures
     *
     * When passed the difference between two temperatures returns the amount of change to temperature to apply.
     * The change rate should be kept at a low value tween 0.16 and 0.02 for optimal results.
     * vars:
     * * temp_diff (required) The difference between two temperatures
     * * change_rate (optional)(Default: 0.06) The rate of range multiplier
     */
    // tg: code/__HELPERS/mobs.dm:438
    public static float get_temp_change_amount(float temp_diff, float? change_rate = null)
    {
        change_rate ??= BODYTEMP_STANDARD_CHANGE_RATE;
        if (temp_diff < 0)
            return -(BODYTEMP_AUTORECOVERY_DIVISOR / 2) * log(1 - (temp_diff * Dm.num(change_rate)));
        return (BODYTEMP_AUTORECOVERY_DIVISOR / 2) * log(1 + (temp_diff * Dm.num(change_rate)));
    }
}

// tg: code/__HELPERS/mobs.dm:196 (M9 slice B: the scream's rare sound)
public static partial class GlobalProcs
{
    /// Returns if the given target is a human. Like, a REAL human.
    /// Not a moth, not a felinid (which are human subtypes), but a human.
    // tg: code/__HELPERS/mobs.dm:196
    public static bool ishumanbasic(object target)
    {
        if (!ishuman(target))
            return false;

        var human_target = (mob_living_carbon_human)target;
        return human_target.dna?.species?.GetType() == typeof(datum_species_human);
    }
}

// tg: code/__HELPERS/mobs.dm:727 (gaps MS1: the moods' personality moods of the viewers; nobody has a personality yet, D332)
public static partial class GlobalProcs
{
    // tg: code/__HELPERS/mobs.dm:727
    public static async DmTask add_personality_mood_to_viewers(atom_movable source = null, object mood_key = null, DmList personality_to_mood = null, object range = null, params object[] __extra)
    {
        DmList args = Dm.args(source, mood_key, personality_to_mood, range, __extra);
        foreach (mob_living nearby in Dm.each<mob_living>(viewers(range, source)))
        {
            if (nearby == source || IS_UNCONSCIOUS(nearby) || Dm.truthy(nearby.is_blind()))
                continue;
            foreach ((object personality, object moodlet) in Dm.pairs(personality_to_mood))
            {
                if (Dm.truthy(HAS_PERSONALITY(nearby, personality)))
                {
                    await Dm.Call(nearby, nameof(mob_living.add_mood_event), Dm.arglist(Dm.add(Dm.list(Dm.text("[]_[]", mood_key, personality), moodlet), args.Copy(4)))); // tg passes range on as the moodlet's first extra (kept)
                    break;
                }
            }
        }
    }
}

// tg: code/__HELPERS/mobs.dm:737 (M4a clothing: the masks' emote sound overrides)
public static partial class GlobalProcs
{
    ///Gets an emote sound from a specific list of sounds. Supports lists and genders. Used by emote datums for default sounds, and tongues, masks etc. for overrides.
    // tg: code/__HELPERS/mobs.dm:737
    public static object get_emote_sound_from_list(object sound = null, mob_living user = null)
    {
        if (islist(sound))
        {
            DmList sounds = sound as DmList;
            object possible_sounds = sounds.Copy();
            string gender = ((user as mob_living_carbon_human)?.physique is var or__1 && Dm.truthy(or__1) ? or__1 : user.gender);
            if (Dm.@in(gender, possible_sounds))
            {
                possible_sounds = ((DmList)possible_sounds)[gender];
                if (!islist(possible_sounds))
                    return possible_sounds; //it's a single sound
            }
            else
            {
                Dm.remove(ref possible_sounds, Dm.list(MALE, FEMALE, PLURAL, NEUTER));
                if (!Dm.truthy(length(possible_sounds)))
                    return null;
            }
            sound = pick(possible_sounds);
        }
        return sound;
    }
}

// tg: code/__HELPERS/mobs.dm:495
public partial class mob_living : mob
{
    // tg: code/__HELPERS/mobs.dm:495
    public virtual object parse_zone_with_bodypart(object zone = null)
    {
        obj_item_bodypart part = get_bodypart(zone as string) as obj_item_bodypart;

        return part?.plaintext_zone is var or__1 && Dm.truthy(or__1) ? or__1 : parse_zone(zone);
    }
}

// tg: code/__HELPERS/mobs.dm:661 (interleaved types, rules 1.9)
public partial class datum_controller_global_vars
{
    // tg: code/__HELPERS/mobs.dm:661  GLOBAL_DATUM_INIT(dview_mob, /mob/dview, new)
    public mob_dview dview_mob;

    // tg: code/__HELPERS/mobs.dm:661  (GLOBAL_MANAGED(dview_mob, new), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldview_mob()
    {
        dview_mob = Dm.@new<mob_dview>();
        Dm.append(ref gvars_datum_init_order, "dview_mob");
    }
}

public static partial class GlobalProcs
{
    ///Version of view() which ignores darkness, because BYOND doesn't have it (I actually suggested it but it was tagged redundant, BUT HEARERS IS A T- /rant).
    // tg: code/__HELPERS/mobs.dm:664
    public static DmList dview(object range = null, object center = null, object invis_flags = null)
    {
        range ??= Dm.world.view;
        invis_flags ??= 0;
        DmList dot = null;
        if (!Dm.truthy(center))
            return dot;

        GLOB.dview_mob.loc = center as atom;

        GLOB.dview_mob.set_invis_see(invis_flags);

        dot = view(range, GLOB.dview_mob);
        GLOB.dview_mob.loc = null;
        return dot;
    }
}

// tg: code/__HELPERS/mobs.dm:676
public partial class mob_dview : mob
{
    // tg: code/__HELPERS/mobs.dm:682
    public override object Initialize(object mapload = null, params object[] __extra) // SHOULD_CALL_PARENT(FALSE), rules 7.9: properly prevents this mob from gaining huds or joining any global lists
    {
        if (Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
            stack_trace(Dm.text("Warning: []([]) initialized multiple times!", this, type), "code/__HELPERS/mobs.dm", 684);
        Dm.combine(ref flags_1, INITIALIZED_1);
        return INITIALIZE_HINT_NORMAL;
    }

    // tg: code/__HELPERS/mobs.dm:688
    public override object Destroy(float? force = null)
    {
        if (!Dm.truthy(ready_to_die))
        {
            stack_trace("ALRIGHT WHICH FUCKER TRIED TO DELETE *MY* DVIEW?", "code/__HELPERS/mobs.dm", 690);

            if (!Dm.truthy(force))
                return QDEL_HINT_LETMELIVE;

            log_world("EVACUATE THE SHITCODE IS TRYING TO STEAL MUH JOBS");
            GLOB.dview_mob = Dm.@new<mob_dview>();
        }
        return base.Destroy(force);
    }
}

public sealed partial class TgState
{
    // tg: code/__HELPERS/mobs.dm:25  get_roundstart_blood_types' var/static/list/cached_blood_types (a proc-level static, rules 10.6)
    internal DmList get_roundstart_blood_types__cached_blood_types;
}

// tg: code/__HELPERS/mobs.dm:654 (interleaved types, rules 1.9; M10 slice F: the popcap of the lobby and the late join)
public static partial class GlobalProcs
{
    // tg: code/__HELPERS/mobs.dm:654
    public static float living_player_count()
    {
        float living_player_count = 0;
        foreach (object mob in Dm.each(GLOB.player_list))
        {
            if (Dm.@in(mob, GLOB.alive_mob_list))
                living_player_count += 1;
        }
        return living_player_count;
    }

    // Displays a message in deadchat, sent by source. source is not linkified, message is, to avoid stuff like character names to be linkified.
    // Automatically gives the class deadsay to the whole message (message + source)
    // tg: code/__HELPERS/mobs.dm:255
    public static void deadchat_broadcast(object message = null, object source = null, mob follow_target = null, turf turf_target = null, object speaker_key = null, object message_type = null, object admin_only = null, object original_message = null)
    {
        message_type ??= DEADCHAT_REGULAR;
        const int SEE_DEADCHAT_ADMIN = 1 << 0, SEE_DEADCHAT_NORMAL = 1 << 1; // tg: :250-251
        message = span_deadsay(Dm.text("[][]", source, span_linkify(Dm.str(message))));

        if (Dm.truthy(admin_only))
            message = Dm.text("[][]", message, span_deadsay(I18n.T(" (This is viewable to admins only).")));

        foreach (mob M in Dm.each<mob>(GLOB.player_list))
        {
            object chat_toggles = TOGGLES_DEFAULT_CHAT;
            object toggles = TOGGLES_DEFAULT;
            DmList ignoring = null;
            if (M.client?.prefs != null)
            {
                datum_preferences prefs = M.client?.prefs;
                chat_toggles = prefs.chat_toggles;
                toggles = prefs.toggles;
                ignoring = prefs.ignoring;
            }
            if (Dm.truthy(admin_only))
            {
                if (M.client?.holder == null)
                    continue;
            }
            int @override = NONE;
            if (M.client?.holder != null && Dm.truthy(Dm.bitand(chat_toggles, CHAT_DEAD)))
                @override = SEE_DEADCHAT_ADMIN;
            if (HAS_TRAIT(M, TRAIT_SIXTHSENSE) && Dm.eq(message_type, DEADCHAT_REGULAR))
                @override = SEE_DEADCHAT_NORMAL;
            if (Dm.eq(SSticker.current_state, GAME_STATE_FINISHED))
                @override = SEE_DEADCHAT_NORMAL;
            if (Dm.truthy(isnewplayer(M)) && @override == NONE)
                continue;
            if (!Dm.eq(M.stat, DEAD) && @override == NONE)
                continue;
            if (Dm.truthy(speaker_key) && Dm.@in(speaker_key, ignoring))
                continue;

            if (Dm.eq(message_type, DEADCHAT_DEATHRATTLE))
            {
                if (Dm.truthy(Dm.bitand(toggles, DISABLE_DEATHRATTLE)))
                    continue;
            }
            else if (Dm.eq(message_type, DEADCHAT_ARRIVALRATTLE))
            {
                if (Dm.truthy(Dm.bitand(toggles, DISABLE_ARRIVALRATTLE)))
                    continue;
            }
            else if (Dm.eq(message_type, DEADCHAT_LAWCHANGE))
            {
                if (!Dm.truthy(Dm.bitand(chat_toggles, CHAT_GHOSTLAWS)))
                    continue;
            }
            else if (Dm.eq(message_type, DEADCHAT_LOGIN_LOGOUT))
            {
                if (!Dm.truthy(Dm.bitand(chat_toggles, CHAT_LOGIN_LOGOUT)))
                    continue;
            }

            if (Dm.truthy(isobserver(M)))
            {
                object rendered_message = message;
                @override = SEE_DEADCHAT_NORMAL;

                if (follow_target != null)
                {
                    object F;
                    if (turf_target != null)
                        F = FOLLOW_OR_TURF_LINK(M, follow_target, turf_target);
                    else
                        F = FOLLOW_LINK(M, follow_target);
                    rendered_message = Dm.text("[] []", F, message);
                }
                else if (turf_target != null)
                {
                    object turf_link = TURF_LINK(M, turf_target);
                    rendered_message = Dm.text("[] []", turf_link, message);
                }

                to_chat(M, rendered_message, avoid_highlighting: Dm.eq(speaker_key, M.key) ? TRUE : FALSE);
            }
            else
                to_chat(M, message, avoid_highlighting: Dm.eq(speaker_key, M.key) ? TRUE : FALSE);

            // Ghost runechat
            // tg: :308  (!SSlag_switch.measures[DISABLE_DEAD_RUNECHAT] || HAS_TRAIT(M, TRAIT_BYPASS_MEASURES)): the lag switch is not declared (its measures are off)
            if (Dm.truthy(original_message) && (Dm.truthy(Dm.bitand(@override, SEE_DEADCHAT_NORMAL)) || Dm.num(M.see_invisible) >= Dm.num(follow_target.invisibility)) && Dm.truthy(M.runechat_prefs_check(M)))
                M.create_chat_message(follow_target, typeof(datum_language_common), original_message, Dm.list(SPAN_ITALICS));
        }
    }
}

public static partial class GlobalProcs
{
    //Used in chemical_mob_spawn. Generates a random mob based on a given gold_core_spawnable value.
    // tg: code/__HELPERS/mobs.dm:327
    public static object create_random_mob(object spawn_location = null, object mob_class = null)
    {
        mob_class ??= HOSTILE_SPAWN;
        ref DmList mob_spawn_meancritters = ref TgState.Current.GlobalProcs__create_random_mob__mob_spawn_meancritters; // list of possible hostile mobs
        ref DmList mob_spawn_nicecritters = ref TgState.Current.GlobalProcs__create_random_mob__mob_spawn_nicecritters; // and possible friendly mobs

        if (Dm.num(length(mob_spawn_meancritters)) <= 0 || Dm.num(length(mob_spawn_nicecritters)) <= 0)
        {
            foreach (Type T in Dm.each_cast<Type>(typesof(typeof(mob_living_simple_animal))))
            {
                float spawnable = Dm.initial(T, (mob_living_simple_animal SA) => SA.gold_core_spawnable);
                if (spawnable == HOSTILE_SPAWN)
                    mob_spawn_meancritters.Add(T);
                else if (spawnable == FRIENDLY_SPAWN)
                    mob_spawn_nicecritters.Add(T);
            }
            foreach (Type basic_mob in Dm.each_cast<Type>(typesof(typeof(mob_living_basic))))
            {
                float spawnable = Dm.initial(basic_mob, (mob_living_basic B) => B.gold_core_spawnable);
                if (spawnable == HOSTILE_SPAWN)
                    mob_spawn_meancritters.Add(basic_mob);
                else if (spawnable == FRIENDLY_SPAWN)
                    mob_spawn_nicecritters.Add(basic_mob);
            }
        }

        object chosen;
        if (Dm.eq(mob_class, FRIENDLY_SPAWN))
            chosen = pick(mob_spawn_nicecritters);
        else
            chosen = pick(mob_spawn_meancritters);
        mob_living spawned_mob = Dm.@new((Type)chosen, spawn_location) as mob_living;
        return spawned_mob;
    }

    // tg: code/__HELPERS/mobs.dm:383
    public static object active_ais(float? check_mind = null, object z = null, float? skip_syndicate = null, float? only_syndicate = null)
    {
        check_mind ??= FALSE;
        skip_syndicate ??= FALSE;
        only_syndicate ??= FALSE;
        DmList dot = Dm.list();
        foreach (mob_living_silicon_ai ai in Dm.each<mob_living_silicon_ai>(GLOB.ai_list))
        {
            if (ai.stat == DEAD)
                continue;
            if (Dm.truthy(ai.control_disabled))
                continue;
            bool syndie_ai = ai is mob_living_silicon_ai_weak_syndie;
            if (Dm.truthy(skip_syndicate) && syndie_ai)
                continue;
            if (Dm.truthy(only_syndicate) && !syndie_ai)
                continue;
            if (Dm.truthy(check_mind) && ai.mind == null)
                continue;
            if (z != null && !Dm.eq(z, ai.z) && (!Dm.truthy(is_station_level(z)) || !Dm.truthy(is_station_level(ai.z)))) //if a Z level was specified, AND the AI is not on the same level, AND either is off the station...
                continue;
            Dm.append(dot, ai);
        }
        return dot;
    }

    //Returns a list of unslaved cyborgs
    // tg: code/__HELPERS/mobs.dm:371
    public static object active_free_borgs()
    {
        DmList dot = Dm.list();
        foreach (mob_living_silicon_robot borg in Dm.each<mob_living_silicon_robot>(GLOB.silicon_mobs))
        {
            if (borg.connected_ai != null || Dm.truthy(borg.shell))
                continue;
            if (borg.stat == DEAD)
                continue;
            if (Dm.truthy(borg.emagged) || Dm.truthy(borg.scrambledcodes))
                continue;
            Dm.append(dot, borg);
        }
        return dot;
    }

    //Find an active ai with the least borgs. VERBOSE PROCNAME HUH!
    // tg: code/__HELPERS/mobs.dm:402
    public static object select_active_ai_with_fewest_borgs(object z = null)
    {
        mob_living_silicon_ai selected = null;
        DmList active = active_ais(FALSE, z) as DmList;
        foreach (mob_living_silicon_ai A in Dm.each<mob_living_silicon_ai>(active))
        {
            if (selected == null || (selected.connected_robots.len > A.connected_robots.len))
                selected = A;
        }

        return selected;
    }

    // tg: code/__HELPERS/mobs.dm:411
    public static async DmTask<object> select_active_free_borg(mob user = null)
    {
        DmList borgs = active_free_borgs() as DmList;
        object dot = null;
        if (Dm.truthy(borgs.len))
        {
            if (user != null)
                dot = await input(user, I18n.T("Unshackled cyborg signals detected:"), I18n.T("Cyborg Selection"), borgs[1], sort_list(borgs)); // input(...) in sort_list(borgs)
            else
                dot = pick(borgs);
        }
        return dot;
    }

    // tg: code/__HELPERS/mobs.dm:420
    public static async DmTask<object> select_active_ai(mob user = null, object z = null, object skip_syndicate = null, object only_syndicate = null)
    {
        DmList ais = active_ais(FALSE, z, Dm.num_or_null(skip_syndicate), Dm.num_or_null(only_syndicate)) as DmList;
        object dot = null;
        if (Dm.truthy(ais.len))
        {
            if (user != null)
                dot = await input(user, I18n.T("AI signals detected:"), I18n.T("AI Selection"), ais[1], sort_list(ais)); // input(...) in sort_list(ais)
            else
                dot = pick(ais);
        }
        return dot;
    }
}

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/mobs.dm:483
    public static object get_mob_by_ckey(object key = null)
    {
        if (!Dm.truthy(key))
            return null;
        mob persistent_mob = (GLOB.persistent_clients_by_ckey[key] as datum_persistent_client)?.mob;
        if (persistent_mob != null)
            return persistent_mob;
        // hopefully the above will always handle it, but any time a coder thinks "no way this will happen", murphy's law guarantees it somehow will
        foreach (mob mob in Dm.each<mob>(GLOB.mob_list))
        {
            if (Dm.eq(mob.ckey, key))
                return mob;
        }
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/__HELPERS/mobs.dm:328  create_random_mob's var/static/list/mob_spawn_meancritters and mob_spawn_nicecritters (rules 10.6)
    internal DmList GlobalProcs__create_random_mob__mob_spawn_meancritters = Dm.list();
    internal DmList GlobalProcs__create_random_mob__mob_spawn_nicecritters = Dm.list();
}
