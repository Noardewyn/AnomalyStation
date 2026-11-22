// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/names.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the types /mob/living/carbon, /mob/living/silicon and /mob/living/basic/drone (their [DmPath]: names.dm is the
// earliest .dme file that declares them, rules 2.3), and the random names of humans (M2 F1): generate_random_name,
// generate_random_name_species_based and generate_random_mob_name of /mob, /mob/living/carbon and the bots. The silicon and drone
// overrides of generate_random_mob_name (silicons SI1, D326); the random adjectives and the hacker aliases come with their callers. M4b slice 2: assign_random_name (the machines'
// id tags: the air vents and scrubbers). M9 slice D2: station_name, set_station_name and new_station_name (the announcements that
// name the station). Syndicate A2: generate_code_phrase (the traitors' code words). Syndicate N1b: syndicate_name (the nuclear operatives'). Gaps P3b: odd_organ_name (the fly's infused organs). Gaps K1: hive_name (the changelings' hive). Gaps H2: tool_behaviour_name (the processable element's examine).

/**
 * Generate a random name based off of one of the roundstart languages
 *
 * * gender - What gender to pick from. Picks between male, female if not provided.
 * * unique - If the name should be unique, IE, avoid picking names that mobs already have.
 * * list/language_weights - A list of language weights to pick from.
 * If not provided, it will default to a list of roundstart languages, with common being the most likely.
 */
public static partial class GlobalProcs
{
    // DM's \\Roman text macro, for 1 to 99 (the one use here)
    internal static string Roman(int n)
    {
        string[] tens = { "", "X", "XX", "XXX", "XL", "L", "LX", "LXX", "LXXX", "XC" };
        string[] ones = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };
        return tens[n / 10] + ones[n % 10];
    }

    // tg: code/__HELPERS/names.dm:398  (gaps J3: GLOB.station_numerals)
    public static DmList generate_number_strings()
    {
        DmList numbers = Dm.list();
        for (int i = 1; i <= 99; i++)
        {
            numbers.Add(Dm.text("[]", i));
            numbers.Add(Roman(i));
        }
        return numbers;
    }

    // tg: code/__HELPERS/names.dm:72  (M9 slice D: the announcements' sender)
    public static object command_name()
    {
        if (Dm.truthy(GLOB.command_name))
            return GLOB.command_name;

        string name = "Central Command";

        GLOB.command_name = name;
        return name;
    }

    // tg: code/__HELPERS/names.dm:87  (M9 slice D2: the announcements that name the station)
    public static object station_name()
    {
        if (!Dm.truthy(GLOB.station_name))
        {
            object newname;
            object config_station_name = config.Get(typeof(datum_config_entry_string_stationname));
            // DEVIATION(DV505): a map names its station (the map json's anomaly.station_name, MapSettings), before the config's name and tg's random one
            if (MapSettingsSeam.Current.StationName is { } map_station_name)
                newname = map_station_name;
            else if (Dm.truthy(config_station_name))
                newname = config_station_name;
            else
                newname = "Anomaly Station"; // DEVIATION: the port never rolls tg's random station name (new_station_name stays unused)

            set_station_name(newname);
        }

        return GLOB.station_name;
    }

    // tg: code/__HELPERS/names.dm:100
    public static void set_station_name(object new_name = null)
    {
        object old_name = GLOB.station_name;
        GLOB.station_name = new_name;

        object config_server_name = config.Get(typeof(datum_config_entry_string_servername));
        if (Dm.truthy(config_server_name))
            world.name = Dm.text("[][]", config_server_name, Dm.eq(config_server_name, GLOB.station_name) ? "" : Dm.text(": []", html_decode(GLOB.station_name)));
        else
            world.name = html_decode(GLOB.station_name);

        // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_STATION_NAME_CHANGED, new_name, old_name) (__DEFINES/dcs/helpers.dm:7), expanded inline (rules 11.1 (e))
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_STATION_NAME_CHANGED]))
            SSdcs._SendSignal(COMSIG_GLOB_STATION_NAME_CHANGED, Dm.list(SSdcs, new_name, old_name));
    }

    // tg: code/__HELPERS/names.dm:112
    public static string new_station_name()
    {
        float random = Dm.rand(1, 5);
        string new_station_name = "";

        object prefix = "";
        object main = "";

        //Rare: Pre-Prefix
        if (Dm.prob(10))
            prefix = pick(GLOB.station_prefixes);

        if (Dm.prob(0.1f))
            random = 999999999; //ridiculously long name in written numbers

        // Prefix
        object picked_holiday = Dm.length(GLOB.holidays) > 0 ? pick(GLOB.holidays) : null; // gaps R3
        if (Dm.truthy(picked_holiday))
        {
            var holiday = GLOB.holidays[picked_holiday] as datum_holiday;
            if (holiday is datum_holiday_friday_thirteenth)
                random = 13;
            object holiday_prefix = holiday.get_station_prefix();
            if (Dm.truthy(holiday_prefix))
                prefix = holiday_prefix;
            object holiday_base_name = holiday.get_station_name();
            if (Dm.truthy(holiday_base_name))
                main = holiday_base_name;
        }

        // Prefix
        if (Dm.truthy(prefix))
            new_station_name += Dm.text("[] ", prefix);

        // Normal name
        new_station_name += Dm.text("[] ", Dm.truthy(main) ? main : pick(GLOB.station_names));

        // Suffix
        new_station_name += Dm.text("[] ", pick(GLOB.station_suffixes));

        // ID Number
        switch (random)
        {
            case 1:
                new_station_name += Dm.text("[]", Dm.rand(1, 99));
                break;
            case 2:
                new_station_name += Dm.text("[]", pick(GLOB.greek_letters));
                break;
            case 3:
                new_station_name += Dm.text("\\Roman[]", Dm.rand(1, 99));
                break;
            case 4:
                new_station_name += Dm.text("[]", pick(GLOB.phonetic_alphabet));
                break;
            case 5:
                new_station_name += Dm.text("[]", convert_integer_to_words(Dm.rand(-1, 99), capitalise: TRUE));
                break;
            case 13:
                new_station_name += Dm.text("[]", pick("13", "XIII", "Thirteen"));
                break;
            case 999999999:
                new_station_name += Dm.text("[]", convert_integer_to_words(Dm.rand(111111111, 999999999), capitalise: TRUE));
                break;
        }
        return new_station_name;
    }

    // tg: code/__HELPERS/names.dm:208  (syndicate A2: the traitors' code words, SStraitor's Initialize)
    public static object generate_code_phrase(float? return_list = null)//Proc is used for phrase and response in master_controller.dm
    {
        return_list ??= FALSE;
        object dot;
        if (!Dm.truthy(return_list))
            dot = "";
        else
            dot = Dm.list();

        float words = Dm.num(Dm.pick_weighted(//How many words there will be. Minimum of two. 2, 4 and 5 have a lesser chance of being selected. 3 is the most likely.
            (50, 2),
            (200, 3),
            (50, 4),
            (25, 5)));

        DmList safety = Dm.list(1, 2, 3);//Tells the proc which options to remove later on.
        DmList nouns = strings(ION_FILE, "ionabstract") as DmList;
        DmList objects = strings(ION_FILE, "ionobjects") as DmList;
        DmList adjectives = strings(ION_FILE, "ionadjectives") as DmList;
        DmList threats = strings(ION_FILE, "ionthreats") as DmList;
        DmList foods = strings(ION_FILE, "ionfood") as DmList;
        DmList drinks = strings(ION_FILE, "iondrinks") as DmList;
        DmList locations = Dm.list();
        foreach (Type area_type in Dm.each<Type>(Dm.bitor(Dm.list(typeof(area_space)), GLOB.the_station_areas)))
            Dm.combine(ref locations, format_text(Dm.initial(area_type, (area p) => p.name)));

        DmList names = Dm.list();
        foreach (datum_record_crew target in Dm.each<datum_record_crew>(GLOB.manifest.general))//Picks from crew manifest.
            Dm.append(names, target.name);

        float maxwords = words;//Extra var to check for duplicates.

        void Add(object word)
        {
            if (dot is DmList list)
                Dm.append(list, word);
            else
                dot = Dm.str(dot) + Dm.str(word);
        }

        for (; words > 0; words--)//Randomly picks from one of the choices below.
        {
            if (words == 1 && Dm.@in(1, safety) && Dm.@in(2, safety))//If there is only one word remaining and choice 1 or 2 have not been selected.
                safety = Dm.list(pick(1, 2));//Select choice 1 or 2.
            else if (words == 1 && maxwords == 2)//Else if there is only one word remaining (and there were two originally), and 1 or 2 were chosen,
                safety = Dm.list(3);//Default to list 3

            switch (Dm.num(pick(safety)))//Chance based on the safety list.
            {
                case 1://1 and 2 can only be selected once each to prevent more than two specific names/places/etc.
                    switch (rand(1, 2))//Mainly to add more options later.
                    {
                        case 1:
                            if (Dm.truthy(length(names)) && prob(70))
                                Add(pick(names));
                            else
                                Add(generate_random_name());
                            break;

                        case 2:
                            datum_job job = pick(SSjob.joinable_occupations) as datum_job;
                            if (job != null)
                                Add(job.title); //Returns a job.
                            else
                            {
                                stack_trace("Failed to pick(SSjob.joinable_occupations) on generate_code_phrase()", "code/__HELPERS/names.dm", 260);
                                Add("Bug");
                            }
                            break;
                    }
                    Dm.remove(safety, 1);
                    break;
                case 2:
                    switch (rand(1, 3))//Food, drinks, or places. Only selectable once.
                    {
                        case 1:
                            Add(LOWER_TEXT(pick(drinks)));
                            break;
                        case 2:
                            Add(LOWER_TEXT(pick(foods)));
                            break;
                        case 3:
                            Add(LOWER_TEXT(pick(locations)));
                            break;
                    }
                    Dm.remove(safety, 2);
                    break;
                case 3:
                    switch (rand(1, 4))//Abstract nouns, objects, adjectives, threats. Can be selected more than once.
                    {
                        case 1:
                            Add(LOWER_TEXT(pick(nouns)));
                            break;
                        case 2:
                            Add(LOWER_TEXT(pick(objects)));
                            break;
                        case 3:
                            Add(LOWER_TEXT(pick(adjectives)));
                            break;
                        case 4:
                            Add(LOWER_TEXT(pick(threats)));
                            break;
                    }
                    break;
            }
            if (!Dm.truthy(return_list))
            {
                if (words == 1)
                    Add(".");
                else
                    Add(", ");
            }
        }
        return dot;
    }

    // tg: code/__HELPERS/names.dm:9
    public static object generate_random_name(object gender = null, object unique = null, DmList language_weights = null)
    {
        if (language_weights == null)
        {
            language_weights = Dm.list();
            // M10 slice F: GLOB.uncommon_roundstart_languages holds the selectable species' languages (generate_selectable_species_and_languages;
            // the Draconic and the Nekomimetic since gaps SP1 made the lizard and the felinid selectable)
            foreach (object lang_type in Dm.each(GLOB.uncommon_roundstart_languages))
                language_weights[lang_type] = 1;
            language_weights[typeof(datum_language_common)] = 20;
        }

        datum_language picked = GLOB.language_datum_instances[pick_weight(language_weights)] as datum_language;
        if (Dm.truthy(unique))
            return picked.get_random_unique_name(gender);
        return picked.get_random_name((string)gender);
    }

    /**
     * Generate a random name based off of a species
     * This will pick a name from the species language, and avoid picking common if there are alternatives
     *
     * * gender - What gender to pick from. Picks between male, female if not provided.
     * * unique - If the name should be unique, IE, avoid picking names that mobs already have.
     * * datum/species/species_type - The species to pick from
     * * include_all - Makes the generated name a mix of all the languages the species can speak rather than just one of them
     * Does this on a per-name basis, IE "Lizard first name, uncommon last name".
     */
    // tg: code/__HELPERS/names.dm:31
    public static object generate_random_name_species_based(object gender = null, object unique = null, object species_type = null, float? include_all = null)
    {
        include_all ??= FALSE;
        if (!(ispath(species_type, typeof(datum_species))))
            throw Dm.CRASH(assertion_message("code/__HELPERS/names.dm", 32, "ispath(species_type, /datum/species)"), "code/__HELPERS/names.dm", 32);
        datum_language_holder holder = GLOB.prototype_language_holders[Dm.initial((Type)species_type, (datum_species p) => p.species_language_holder)] as datum_language_holder;

        DmList languages_to_pick_from = Dm.list();
        foreach (object language in Dm.each(holder.spoken_languages))
            languages_to_pick_from[language] = 1;

        if (length(languages_to_pick_from) >= 2)
        {
            // Basically, if we have alternatives, don't pick common it's boring
            Dm.remove(ref languages_to_pick_from, typeof(datum_language_common));
        }

        if (!Dm.truthy(include_all) || length(languages_to_pick_from) <= 1)
            return generate_random_name(gender, unique, languages_to_pick_from);

        DmList name_parts = Dm.list();
        foreach (object lang_type in Dm.each(shuffle(languages_to_pick_from)))
            Dm.append(ref name_parts, ((datum_language)GLOB.language_datum_instances[lang_type]).get_random_name((string)gender, name_count: 1, force_use_syllables: TRUE));
        return jointext(name_parts, " ");
    }

    /**
     * Generate a game-world name from a random string
     *
     * args:
     * * len (int)(Optional) Default=5 The length of the name
     * * prefix (string)(Optional) static text in front of the random name
     * * postfix (string)(Optional) static text in back of the random name
     * Returns (string) The generated name
     */
    // tg: code/__HELPERS/names.dm:307
    public static object assign_random_name(float? len = null, string prefix = null, string postfix = null)
    {
        len ??= 5;
        prefix ??= "";
        postfix ??= "";
        //DO NOT REMOVE NAMES HERE UNLESS YOU KNOW WHAT YOU'RE DOING
        //All names already used
        assign_random_name__used_names ??= Dm.list();

        string valid_chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789"; // var/static/valid_chars (a constant text)
        DmList new_name = Dm.list();
        string text;
        // machine id's should be fun random chars hinting at a larger world
        do
        {
            new_name.Cut();
            Dm.append(ref new_name, prefix);
            for (float i = 1, i__end = len.Value; i <= i__end; i++)
            {
                float n__1 = Dm.num(rand(1, length(valid_chars)));
                Dm.append(ref new_name, copytext(valid_chars, n__1, n__1 + 1)); // valid_chars[n]: DM 516 text indexing (one character)
            }
            Dm.append(ref new_name, postfix);
            text = new_name.Join("");
        }
        while (Dm.truthy(assign_random_name__used_names[text]));
        assign_random_name__used_names[text] = TRUE;
        return text;
    }

    // tg: code/__HELPERS/names.dm:310  var/static/list/used_names = list() (rules 10.6: a TgState field)
    private static ref DmList assign_random_name__used_names => ref TgState.Current.GlobalProcs__assign_random_name__used_names;
}

/**
 * Generates a random name for the mob based on their gender or species (for humans)
 *
 * * unique - If the name should be unique, IE, avoid picking names that mobs already have.
 */
// tg: code/__HELPERS/names.dm:56
public partial class mob : atom_movable
{
    // tg: code/__HELPERS/names.dm:56
    public virtual object generate_random_mob_name(object unique = null)
    {
        return generate_random_name_species_based(gender, unique, typeof(datum_species_human));
    }
}

// tg: code/__HELPERS/names.dm:59
[DmPath("/mob/living/carbon")]
public partial class mob_living_carbon : mob_living
{
    // tg: code/__HELPERS/names.dm:59
    public override object generate_random_mob_name(object unique = null)
    {
        return generate_random_name_species_based(gender, unique, (((datum)dna?.species)?.type is var or__1 && Dm.truthy(or__1) ? or__1 : typeof(datum_species_human)));
    }
}

// tg: code/__HELPERS/names.dm:68
public partial class mob_living_basic_bot : mob_living_basic
{
    // tg: code/__HELPERS/names.dm:68
    public override object generate_random_mob_name(object unique = null)
    {
        return generate_random_name(gender, unique, Dm.list(Dm.kv(typeof(datum_language_machine), 1)));
    }
}

// tg: code/__HELPERS/names.dm:62
[DmPath("/mob/living/silicon")]
public partial class mob_living_silicon : mob_living
{
    // tg: code/__HELPERS/names.dm:62
    public override object generate_random_mob_name(object unique = null)
    {
        return generate_random_name(gender, unique, Dm.list(Dm.kv(typeof(datum_language_machine), 1)));
    }
}

// tg: code/__HELPERS/names.dm:65
[DmPath("/mob/living/basic/drone")]
public partial class mob_living_basic_drone : mob_living_basic
{
    // tg: code/__HELPERS/names.dm:65
    public override object generate_random_mob_name(object unique = null)
    {
        return generate_random_name(gender, unique, Dm.list(Dm.kv(typeof(datum_language_machine), 1)));
    }
}

public sealed partial class TgState
{
    // tg: code/__HELPERS/names.dm:310  /proc/assign_random_name var/static/list/used_names
    internal DmList GlobalProcs__assign_random_name__used_names;
}

public static partial class GlobalProcs
{
    ///Find the first name of a mob from a passed string with regex
    // tg: code/__HELPERS/names.dm:379  (the proc-level static regex is built per call: it holds no state a caller reads)
    public static object first_name(object given_name = null)
    {
        regex firstname = Dm.regex("^[^\\s-]+"); //First word before whitespace or "-"
        firstname.Find(given_name);
        return firstname.match;
    }

    /// Find the last name of a mob from a passed string with regex
    // tg: code/__HELPERS/names.dm:385
    public static object last_name(object given_name = null)
    {
        regex lasttname = Dm.regex("[^\\s-]+$"); //First word before whitespace or "-"
        lasttname.Find(given_name);
        return lasttname.match;
    }

    // tg: code/__HELPERS/names.dm:169  (syndicate N1b, D316: the nuclear operatives' team name)
    public static string syndicate_name()
    {
        string name = "";

        // Prefix
        name += Dm.str(pick("Clandestine", "Prima", "Blue", "Zero-G", "Max", "Blasto", "North", "Omni", "Newton", "Cyber", "Bonk", "Gene", "Gib"));

        // Suffix
        if (prob(80))
        {
            name += " ";

            // Full
            if (prob(60))
                name += Dm.str(pick("Syndicate", "Consortium", "Collective", "Corporation", "Group", "Holdings", "Biotech", "Industries", "Systems", "Products", "Chemicals", "Enterprises", "Family", "Creations", "International", "Intergalactic", "Interplanetary", "Foundation", "Positronics", "Hive"));
            // Broken
            else
            {
                name += Dm.str(pick("Syndi", "Corp", "Bio", "System", "Prod", "Chem", "Inter", "Hive"));
                name += Dm.str(pick("", "-"));
                name += Dm.str(pick("Tech", "Co", "Tek", "X", "Inc", "Code"));
            }
        }
        // Small
        else
        {
            name += Dm.str(pick("-", "*", ""));
            name += Dm.str(pick("Tech", "Co", "Tek", "X", "Inc", "Gen", "Star", "Dyne", "Code", "Hive"));
        }

        return name;
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/__HELPERS/names.dm:71  GLOBAL_VAR(command_name)
    public object command_name;

    // tg: code/__HELPERS/names.dm:71  GLOBAL_VAR(command_name): GLOBAL_UNMANAGED (__DEFINES/__globals.dm:11)
    public virtual void InitGlobalcommand_name()
    {
        return;
    }

}

// tg: code/__HELPERS/names.dm:288 (gaps P3b)
public static partial class GlobalProcs
{
    // tg: code/__HELPERS/names.dm:288
    public static string odd_organ_name()
    {
        return Dm.text("[], [] organ", pick(GLOB.gross_adjectives), pick(GLOB.gross_adjectives));
    }
}

// tg: code/__HELPERS/names.dm:291 (gaps K1)
public static partial class GlobalProcs
{
    // tg: code/__HELPERS/names.dm:291
    public static string hive_name()
    {
        return Dm.text("[]-hive", pick(GLOB.hive_names));
    }
}

// tg: code/__HELPERS/names.dm:333 (gaps H2: the processable element's examine)
public static partial class GlobalProcs
{
    /**
     * returns an ic name of the tool needed
     * Arguments:
     * * tool_behaviour: the tool described!
     */
    // tg: code/__HELPERS/names.dm:333
    public static string tool_behaviour_name(object tool_behaviour = null)
    {
        object sw__1 = tool_behaviour;
        if (Dm.eq(sw__1, TOOL_CROWBAR))
            return I18n.T("a crowbar");
        else if (Dm.eq(sw__1, TOOL_MULTITOOL))
            return I18n.T("a multitool");
        else if (Dm.eq(sw__1, TOOL_SCREWDRIVER))
            return I18n.T("a screwdriver");
        else if (Dm.eq(sw__1, TOOL_WIRECUTTER))
            return I18n.T("a pair of wirecutters");
        else if (Dm.eq(sw__1, TOOL_WRENCH))
            return I18n.T("a wrench");
        else if (Dm.eq(sw__1, TOOL_WELDER))
            return I18n.T("a welder");
        else if (Dm.eq(sw__1, TOOL_ANALYZER))
            return I18n.T("an analyzer tool");
        else if (Dm.eq(sw__1, TOOL_MINING))
            return I18n.T("a mining implement");
        else if (Dm.eq(sw__1, TOOL_SHOVEL))
            return I18n.T("a digging tool");
        else if (Dm.eq(sw__1, TOOL_RETRACTOR))
            return I18n.T("a retractor");
        else if (Dm.eq(sw__1, TOOL_HEMOSTAT))
            return I18n.T("something to clamp bleeding");
        else if (Dm.eq(sw__1, TOOL_CAUTERY))
            return I18n.T("a cautery");
        else if (Dm.eq(sw__1, TOOL_DRILL))
            return I18n.T("a drilling tool");
        else if (Dm.eq(sw__1, TOOL_SCALPEL))
            return I18n.T("a fine cutting tool");
        else if (Dm.eq(sw__1, TOOL_SAW))
            return I18n.T("a saw");
        else if (Dm.eq(sw__1, TOOL_BONESET))
            return I18n.T("a bone setter");
        else if (Dm.eq(sw__1, TOOL_KNIFE))
            return I18n.T("a cutting tool");
        else if (Dm.eq(sw__1, TOOL_BLOODFILTER))
            return I18n.T("a blood filter");
        else if (Dm.eq(sw__1, TOOL_ROLLINGPIN))
            return I18n.T("a rolling pin");
        else if (Dm.eq(sw__1, TOOL_RUSTSCRAPER))
            return I18n.T("a rust scraper");
        else
            return I18n.T("something... but the gods didn't set this up right (Please report this bug)");
    }
}
