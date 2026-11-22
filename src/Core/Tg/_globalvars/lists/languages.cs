// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/languages.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M2 F1: a human's random name picks its language prototype from GLOB.language_datum_instances,
// __HELPERS/names.dm:16). valid_subtypesof(/datum/language) holds the languages M2 declares (common), as
// GLOB.species_prototypes holds the declared species (M2 P2). The common words lists read tg's strings/ files through the
// world's file seam; the host puts the snapshot's files there (docs/ai/specs/apps.md 3).

public partial class datum_controller_global_vars
{
    /// List of language prototypes to reference, assoc [type] = prototype
    // tg: code/_globalvars/lists/languages.dm:2  GLOBAL_LIST_INIT_TYPED(language_datum_instances, /datum/language, init_language_prototypes())
    public DmList language_datum_instances;

    // tg: code/_globalvars/lists/languages.dm:2  (GLOBAL_MANAGED(language_datum_instances, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGloballanguage_datum_instances()
    {
        language_datum_instances = init_language_prototypes() as DmList;
        Dm.append(ref gvars_datum_init_order, "language_datum_instances");
    }

    /// List if all language typepaths learnable, IE, those with keys
    // tg: code/_globalvars/lists/languages.dm:4  GLOBAL_LIST_INIT(all_languages, init_all_languages())
    public DmList all_languages;

    // tg: code/_globalvars/lists/languages.dm:4  (GLOBAL_MANAGED(all_languages, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalall_languages()
    {
        all_languages = init_all_languages() as DmList;
        Dm.append(ref gvars_datum_init_order, "all_languages");
    }

    /// /List of language prototypes to reference, assoc "name" = typepath
    // tg: code/_globalvars/lists/languages.dm:6  GLOBAL_LIST_INIT(language_types_by_name, init_language_types_by_name())
    public DmList language_types_by_name;

    // tg: code/_globalvars/lists/languages.dm:6  (GLOBAL_MANAGED(language_types_by_name, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGloballanguage_types_by_name()
    {
        language_types_by_name = init_language_types_by_name() as DmList;
        Dm.append(ref gvars_datum_init_order, "language_types_by_name");
    }

    /// ~1400 element long list containing the 1000 most common words in the English language in alphabetical order.
    /// Indexed by word, value is the rank of the word in the list. So accessing it is fasta.
    // tg: code/_globalvars/lists/languages.dm:9  GLOBAL_LIST_INIT(most_common_words_alphabetical, init_common_words_by_alphabetical())
    public DmList most_common_words_alphabetical;

    // tg: code/_globalvars/lists/languages.dm:9  (GLOBAL_MANAGED(most_common_words_alphabetical, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmost_common_words_alphabetical()
    {
        most_common_words_alphabetical = init_common_words_by_alphabetical() as DmList;
        Dm.append(ref gvars_datum_init_order, "most_common_words_alphabetical");
    }

    /// ~1000 element long list containing the 1000 most common words in the English language in frequency order.
    /// Indexed by word, value is the rank of the word in the list. So accessing it is fasta.
    // tg: code/_globalvars/lists/languages.dm:12  GLOBAL_LIST_INIT(most_common_words_frequency, init_common_words_by_frequency())
    public DmList most_common_words_frequency;

    // tg: code/_globalvars/lists/languages.dm:12  (GLOBAL_MANAGED(most_common_words_frequency, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmost_common_words_frequency()
    {
        most_common_words_frequency = init_common_words_by_frequency() as DmList;
        Dm.append(ref gvars_datum_init_order, "most_common_words_frequency");
    }
}

public static partial class GlobalProcs
{
    // tg: code/_globalvars/lists/languages.dm:14
    public static object init_language_prototypes()
    {
        DmList lang_list = Dm.list();
        foreach (Type lang_type in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_language))))
            lang_list[lang_type] = Dm.@new(lang_type);
        return lang_list;
    }

    // tg: code/_globalvars/lists/languages.dm:20
    public static object init_all_languages()
    {
        DmList lang_list = Dm.list();
        foreach (Type lang_type in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_language))))
            Dm.append(ref lang_list, lang_type);
        return lang_list;
    }

    // tg: code/_globalvars/lists/languages.dm:26
    public static object init_language_types_by_name()
    {
        DmList lang_list = Dm.list();
        foreach (Type lang_type in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_language))))
            lang_list[Dm.initial(lang_type, (datum_language p) => p.name)] = lang_type;
        return lang_list;
    }

    // tg: code/_globalvars/lists/languages.dm:32
    public static object init_common_words_by_alphabetical()
    {
        DmList word_to_commonness_list = Dm.list();
        float i = 1;
        foreach (object word in Dm.each(world.file2list("strings/1400_most_common_alpha.txt")))
        {
            word_to_commonness_list[word] = i;
            i += 1;
        }
        return word_to_commonness_list;
    }

    // tg: code/_globalvars/lists/languages.dm:40
    public static object init_common_words_by_frequency()
    {
        DmList word_to_commonness_list = Dm.list();
        float i = 1;
        foreach (object word in Dm.each(world.file2list("strings/1000_most_common_frequency.txt")))
        {
            word_to_commonness_list[word] = i;
            i += 1;
        }
        // alphabetical list has a few more entries than the frequency list, so
        // if there are any words we're missing, add them with a default "commonness" of 500
        foreach (object word in Dm.each(world.file2list("strings/1400_most_common_alpha.txt")))
        {
            if (!Dm.truthy(word_to_commonness_list[word]))
                word_to_commonness_list[word] = 500;
        }
        return word_to_commonness_list;
    }
}
