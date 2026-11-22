// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/names.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (station_numerals with the station charter, gaps J3). M2 F1: a human's random name reads first_names_male, first_names_female and last_names (the language's
// default_name, modules/language/_language.dm). The lists read tg's strings/ files through the world's file seam
// (world.file2list, __HELPERS/type2type.dm); the host puts the snapshot's files there (docs/ai/specs/apps.md 3).

public partial class datum_controller_global_vars
{
    // tg: code/_globalvars/lists/names.dm:1  GLOBAL_LIST_INIT(ai_names, world.file2list("strings/names/ai.txt"))
    public DmList ai_names;

    // tg: code/_globalvars/lists/names.dm:1  (GLOBAL_MANAGED(ai_names, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalai_names()
    {
        ai_names = world.file2list("strings/names/ai.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "ai_names");
    }

    // tg: code/_globalvars/lists/names.dm:2  GLOBAL_LIST_INIT(cargorilla_names, world.file2list("strings/names/cargorilla.txt"))
    public DmList cargorilla_names;

    // tg: code/_globalvars/lists/names.dm:2  (GLOBAL_MANAGED(cargorilla_names, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcargorilla_names()
    {
        cargorilla_names = world.file2list("strings/names/cargorilla.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "cargorilla_names");
    }

    // tg: code/_globalvars/lists/names.dm:3  GLOBAL_LIST_INIT(carp_names, world.file2list("strings/names/carp.txt"))
    public DmList carp_names;

    // tg: code/_globalvars/lists/names.dm:3  (GLOBAL_MANAGED(carp_names, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcarp_names()
    {
        carp_names = world.file2list("strings/names/carp.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "carp_names");
    }

    // tg: code/_globalvars/lists/names.dm:4  GLOBAL_LIST_INIT(clown_names, world.file2list("strings/names/clown.txt"))
    public DmList clown_names;

    // tg: code/_globalvars/lists/names.dm:4  (GLOBAL_MANAGED(clown_names, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalclown_names()
    {
        clown_names = world.file2list("strings/names/clown.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "clown_names");
    }

    // tg: code/_globalvars/lists/names.dm:5  GLOBAL_LIST_INIT(commando_names, world.file2list("strings/names/death_commando.txt"))
    public DmList commando_names;

    // tg: code/_globalvars/lists/names.dm:5  (GLOBAL_MANAGED(commando_names, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcommando_names()
    {
        commando_names = world.file2list("strings/names/death_commando.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "commando_names");
    }

    // tg: code/_globalvars/lists/names.dm:6  GLOBAL_LIST_INIT(cyberauth_names, world.file2list("strings/names/cyberauth.txt"))
    public DmList cyberauth_names;

    // tg: code/_globalvars/lists/names.dm:6  (GLOBAL_MANAGED(cyberauth_names, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcyberauth_names()
    {
        cyberauth_names = world.file2list("strings/names/cyberauth.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "cyberauth_names");
    }

    // tg: code/_globalvars/lists/names.dm:7  GLOBAL_LIST_INIT(ethereal_names, world.file2list("strings/names/ethereal.txt"))
    public DmList ethereal_names;

    // tg: code/_globalvars/lists/names.dm:7  (GLOBAL_MANAGED(ethereal_names, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalethereal_names()
    {
        ethereal_names = world.file2list("strings/names/ethereal.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "ethereal_names");
    }

    // tg: code/_globalvars/lists/names.dm:8  GLOBAL_LIST_INIT(first_names_female, world.file2list("strings/names/first_female.txt"))
    public DmList first_names_female;

    // tg: code/_globalvars/lists/names.dm:8  (GLOBAL_MANAGED(first_names_female, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalfirst_names_female()
    {
        first_names_female = world.file2list("strings/names/first_female.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "first_names_female");
    }

    // tg: code/_globalvars/lists/names.dm:9  GLOBAL_LIST_INIT(first_names_male, world.file2list("strings/names/first_male.txt"))
    public DmList first_names_male;

    // tg: code/_globalvars/lists/names.dm:9  (GLOBAL_MANAGED(first_names_male, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalfirst_names_male()
    {
        first_names_male = world.file2list("strings/names/first_male.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "first_names_male");
    }

    // tg: code/_globalvars/lists/names.dm:10  GLOBAL_LIST_INIT(first_names, world.file2list("strings/names/first.txt"))
    public DmList first_names;

    // tg: code/_globalvars/lists/names.dm:10  (GLOBAL_MANAGED(first_names, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalfirst_names()
    {
        first_names = world.file2list("strings/names/first.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "first_names");
    }

    // tg: code/_globalvars/lists/names.dm:11  GLOBAL_LIST_INIT(golem_names, world.file2list("strings/names/golem.txt"))
    public DmList golem_names;

    // tg: code/_globalvars/lists/names.dm:11  (GLOBAL_MANAGED(golem_names, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalgolem_names()
    {
        golem_names = world.file2list("strings/names/golem.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "golem_names");
    }

    // tg: code/_globalvars/lists/names.dm:12  GLOBAL_LIST_INIT(guardian_fantasy_surnames, world.file2list("strings/names/guardian_tarot.txt"))
    public DmList guardian_fantasy_surnames;

    // tg: code/_globalvars/lists/names.dm:12  (GLOBAL_MANAGED(guardian_fantasy_surnames, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalguardian_fantasy_surnames()
    {
        guardian_fantasy_surnames = world.file2list("strings/names/guardian_tarot.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "guardian_fantasy_surnames");
    }

    // tg: code/_globalvars/lists/names.dm:13  GLOBAL_LIST_INIT(guardian_first_names, world.file2list("strings/names/guardian_descriptions.txt"))
    public DmList guardian_first_names;

    // tg: code/_globalvars/lists/names.dm:13  (GLOBAL_MANAGED(guardian_first_names, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalguardian_first_names()
    {
        guardian_first_names = world.file2list("strings/names/guardian_descriptions.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "guardian_first_names");
    }

    // tg: code/_globalvars/lists/names.dm:14  GLOBAL_LIST_INIT(guardian_tech_surnames, world.file2list("strings/names/guardian_gamepieces.txt"))
    public DmList guardian_tech_surnames;

    // tg: code/_globalvars/lists/names.dm:14  (GLOBAL_MANAGED(guardian_tech_surnames, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalguardian_tech_surnames()
    {
        guardian_tech_surnames = world.file2list("strings/names/guardian_gamepieces.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "guardian_tech_surnames");
    }

    // tg: code/_globalvars/lists/names.dm:15  GLOBAL_LIST_INIT(hacker_aliases, world.file2list("strings/names/hackers.txt"))
    public DmList hacker_aliases;

    // tg: code/_globalvars/lists/names.dm:15  (GLOBAL_MANAGED(hacker_aliases, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalhacker_aliases()
    {
        hacker_aliases = world.file2list("strings/names/hackers.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "hacker_aliases");
    }

    // tg: code/_globalvars/lists/names.dm:16  GLOBAL_LIST_INIT(hive_names, world.file2list("strings/names/hive_names.txt"))
    public DmList hive_names;

    // tg: code/_globalvars/lists/names.dm:16  (GLOBAL_MANAGED(hive_names, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalhive_names()
    {
        hive_names = world.file2list("strings/names/hive_names.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "hive_names");
    }

    // tg: code/_globalvars/lists/names.dm:17  GLOBAL_LIST_INIT(last_names, world.file2list("strings/names/last.txt"))
    public DmList last_names;

    // tg: code/_globalvars/lists/names.dm:17  (GLOBAL_MANAGED(last_names, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGloballast_names()
    {
        last_names = world.file2list("strings/names/last.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "last_names");
    }

    // tg: code/_globalvars/lists/names.dm:18  GLOBAL_LIST_INIT(lizard_names_female, world.file2list("strings/names/lizard_female.txt"))
    public DmList lizard_names_female;

    // tg: code/_globalvars/lists/names.dm:18  (GLOBAL_MANAGED(lizard_names_female, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGloballizard_names_female()
    {
        lizard_names_female = world.file2list("strings/names/lizard_female.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "lizard_names_female");
    }

    // tg: code/_globalvars/lists/names.dm:19  GLOBAL_LIST_INIT(lizard_names_male, world.file2list("strings/names/lizard_male.txt"))
    public DmList lizard_names_male;

    // tg: code/_globalvars/lists/names.dm:19  (GLOBAL_MANAGED(lizard_names_male, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGloballizard_names_male()
    {
        lizard_names_male = world.file2list("strings/names/lizard_male.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "lizard_names_male");
    }

    // tg: code/_globalvars/lists/names.dm:20  GLOBAL_LIST_INIT(martial_prefix, world.file2list("strings/names/martial_prefix.txt"))
    public DmList martial_prefix;

    // tg: code/_globalvars/lists/names.dm:20  (GLOBAL_MANAGED(martial_prefix, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmartial_prefix()
    {
        martial_prefix = world.file2list("strings/names/martial_prefix.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "martial_prefix");
    }

    // tg: code/_globalvars/lists/names.dm:21  GLOBAL_LIST_INIT(megacarp_first_names, world.file2list("strings/names/megacarp1.txt"))
    public DmList megacarp_first_names;

    // tg: code/_globalvars/lists/names.dm:21  (GLOBAL_MANAGED(megacarp_first_names, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmegacarp_first_names()
    {
        megacarp_first_names = world.file2list("strings/names/megacarp1.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "megacarp_first_names");
    }

    // tg: code/_globalvars/lists/names.dm:22  GLOBAL_LIST_INIT(megacarp_last_names, world.file2list("strings/names/megacarp2.txt"))
    public DmList megacarp_last_names;

    // tg: code/_globalvars/lists/names.dm:22  (GLOBAL_MANAGED(megacarp_last_names, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmegacarp_last_names()
    {
        megacarp_last_names = world.file2list("strings/names/megacarp2.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "megacarp_last_names");
    }

    // tg: code/_globalvars/lists/names.dm:23  GLOBAL_LIST_INIT(mime_names, world.file2list("strings/names/mime.txt"))
    public DmList mime_names;

    // tg: code/_globalvars/lists/names.dm:23  (GLOBAL_MANAGED(mime_names, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmime_names()
    {
        mime_names = world.file2list("strings/names/mime.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "mime_names");
    }

    // tg: code/_globalvars/lists/names.dm:24  GLOBAL_LIST_INIT(mirror_maid_names, world.file2list("strings/names/mirror_maid.txt"))
    public DmList mirror_maid_names;

    // tg: code/_globalvars/lists/names.dm:24  (GLOBAL_MANAGED(mirror_maid_names, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmirror_maid_names()
    {
        mirror_maid_names = world.file2list("strings/names/mirror_maid.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "mirror_maid_names");
    }

    // tg: code/_globalvars/lists/names.dm:25  GLOBAL_LIST_INIT(moth_first, world.file2list("strings/names/moth_first.txt"))
    public DmList moth_first;

    // tg: code/_globalvars/lists/names.dm:25  (GLOBAL_MANAGED(moth_first, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmoth_first()
    {
        moth_first = world.file2list("strings/names/moth_first.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "moth_first");
    }

    // tg: code/_globalvars/lists/names.dm:26  GLOBAL_LIST_INIT(moth_last, world.file2list("strings/names/moth_last.txt"))
    public DmList moth_last;

    // tg: code/_globalvars/lists/names.dm:26  (GLOBAL_MANAGED(moth_last, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmoth_last()
    {
        moth_last = world.file2list("strings/names/moth_last.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "moth_last");
    }

    // tg: code/_globalvars/lists/names.dm:27  GLOBAL_LIST_INIT(nightmare_names, world.file2list("strings/names/nightmare.txt"))
    public DmList nightmare_names;

    // tg: code/_globalvars/lists/names.dm:27  (GLOBAL_MANAGED(nightmare_names, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalnightmare_names()
    {
        nightmare_names = world.file2list("strings/names/nightmare.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "nightmare_names");
    }

    // tg: code/_globalvars/lists/names.dm:28  GLOBAL_LIST_INIT(ninja_names, world.file2list("strings/names/ninjaname.txt"))
    public DmList ninja_names;

    // tg: code/_globalvars/lists/names.dm:28  (GLOBAL_MANAGED(ninja_names, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalninja_names()
    {
        ninja_names = world.file2list("strings/names/ninjaname.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "ninja_names");
    }

    // tg: code/_globalvars/lists/names.dm:29  GLOBAL_LIST_INIT(ninja_titles, world.file2list("strings/names/ninjatitle.txt"))
    public DmList ninja_titles;

    // tg: code/_globalvars/lists/names.dm:29  (GLOBAL_MANAGED(ninja_titles, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalninja_titles()
    {
        ninja_titles = world.file2list("strings/names/ninjatitle.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "ninja_titles");
    }

    // tg: code/_globalvars/lists/names.dm:30  GLOBAL_LIST_INIT(operative_aliases, world.file2list("strings/names/operative_alias.txt"))
    public DmList operative_aliases;

    // tg: code/_globalvars/lists/names.dm:30  (GLOBAL_MANAGED(operative_aliases, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaloperative_aliases()
    {
        operative_aliases = world.file2list("strings/names/operative_alias.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "operative_aliases");
    }

    // tg: code/_globalvars/lists/names.dm:31  GLOBAL_LIST_INIT(plasmaman_names, world.file2list("strings/names/plasmaman.txt"))
    public DmList plasmaman_names;

    // tg: code/_globalvars/lists/names.dm:31  (GLOBAL_MANAGED(plasmaman_names, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalplasmaman_names()
    {
        plasmaman_names = world.file2list("strings/names/plasmaman.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "plasmaman_names");
    }

    // tg: code/_globalvars/lists/names.dm:32  GLOBAL_LIST_INIT(posibrain_names, world.file2list("strings/names/posibrain.txt"))
    public DmList posibrain_names;

    // tg: code/_globalvars/lists/names.dm:32  (GLOBAL_MANAGED(posibrain_names, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalposibrain_names()
    {
        posibrain_names = world.file2list("strings/names/posibrain.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "posibrain_names");
    }

    // tg: code/_globalvars/lists/names.dm:33  GLOBAL_LIST_INIT(religion_names, world.file2list("strings/names/religion.txt"))
    public DmList religion_names;

    // tg: code/_globalvars/lists/names.dm:33  (GLOBAL_MANAGED(religion_names, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalreligion_names()
    {
        religion_names = world.file2list("strings/names/religion.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "religion_names");
    }

    // tg: code/_globalvars/lists/names.dm:34  GLOBAL_LIST_INIT(station_names, world.file2list("strings/station_names.txt"))
    public DmList station_names;

    // tg: code/_globalvars/lists/names.dm:34  (GLOBAL_MANAGED(station_names, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalstation_names()
    {
        station_names = world.file2list("strings/station_names.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "station_names");
    }

    // tg: code/_globalvars/lists/names.dm:36  GLOBAL_LIST_INIT(station_prefixes, world.file2list("strings/station_prefixes.txt"))
    public DmList station_prefixes;

    // tg: code/_globalvars/lists/names.dm:36  (GLOBAL_MANAGED(station_prefixes, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalstation_prefixes()
    {
        station_prefixes = world.file2list("strings/station_prefixes.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "station_prefixes");
    }

    // tg: code/_globalvars/lists/names.dm:37  GLOBAL_LIST_INIT(station_suffixes, world.file2list("strings/station_suffixes.txt"))
    public DmList station_suffixes;

    // tg: code/_globalvars/lists/names.dm:37  (GLOBAL_MANAGED(station_suffixes, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalstation_suffixes()
    {
        station_suffixes = world.file2list("strings/station_suffixes.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "station_suffixes");
    }

    // tg: code/_globalvars/lists/names.dm:38  GLOBAL_LIST_INIT(syndicate_monkey_names, world.file2list("strings/names/syndicate_monkey.txt"))
    public DmList syndicate_monkey_names;

    // tg: code/_globalvars/lists/names.dm:38  (GLOBAL_MANAGED(syndicate_monkey_names, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsyndicate_monkey_names()
    {
        syndicate_monkey_names = world.file2list("strings/names/syndicate_monkey.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "syndicate_monkey_names");
    }

    // tg: code/_globalvars/lists/names.dm:39  GLOBAL_LIST_INIT(vampire_house_names, world.file2list("strings/names/vampire_house_names.txt"))
    public DmList vampire_house_names;

    // tg: code/_globalvars/lists/names.dm:39  (GLOBAL_MANAGED(vampire_house_names, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalvampire_house_names()
    {
        vampire_house_names = world.file2list("strings/names/vampire_house_names.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "vampire_house_names");
    }

    // tg: code/_globalvars/lists/names.dm:40  GLOBAL_LIST_INIT(voidwalker_names, world.file2list("strings/names/voidwalker.txt"))
    public DmList voidwalker_names;

    // tg: code/_globalvars/lists/names.dm:40  (GLOBAL_MANAGED(voidwalker_names, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalvoidwalker_names()
    {
        voidwalker_names = world.file2list("strings/names/voidwalker.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "voidwalker_names");
    }

    // tg: code/_globalvars/lists/names.dm:41  GLOBAL_LIST_INIT(wizard_first, world.file2list("strings/names/wizardfirst.txt"))
    public DmList wizard_first;

    // tg: code/_globalvars/lists/names.dm:41  (GLOBAL_MANAGED(wizard_first, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalwizard_first()
    {
        wizard_first = world.file2list("strings/names/wizardfirst.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "wizard_first");
    }

    // tg: code/_globalvars/lists/names.dm:42  GLOBAL_LIST_INIT(wizard_second, world.file2list("strings/names/wizardsecond.txt"))
    public DmList wizard_second;

    // tg: code/_globalvars/lists/names.dm:42  (GLOBAL_MANAGED(wizard_second, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalwizard_second()
    {
        wizard_second = world.file2list("strings/names/wizardsecond.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "wizard_second");
    }


    // tg: code/_globalvars/lists/names.dm:35  GLOBAL_LIST_INIT(station_numerals, greek_letters + phonetic_alphabet + numbers_as_words + generate_number_strings())  (gaps J3: the station charter)
    public DmList station_numerals;

    // tg: code/_globalvars/lists/names.dm:35  (GLOBAL_MANAGED(station_numerals, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalstation_numerals()
    {
        // the three string lists initialize after this one (typesof order, ProcOrder), so in tg they are still null here
        station_numerals = Dm.list();
        foreach (DmList part in new[] { greek_letters, phonetic_alphabet, numbers_as_words, generate_number_strings() })
        {
            if (part != null)
                station_numerals = Dm.add(station_numerals, part) as DmList;
        }
        Dm.append(ref gvars_datum_init_order, "station_numerals");
    }
}
