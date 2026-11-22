// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/strings.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice D2: the station name's greek letters and phonetic alphabet, and the station numerals). The lists read
// tg's strings/ files through the world's file seam (world.file2list), as the name lists do.

public partial class datum_controller_global_vars
{
    // tg: code/_globalvars/lists/strings.dm:1  GLOBAL_LIST_INIT(adjectives, world.file2list("strings/names/adjectives.txt"))
    public DmList adjectives;

    // tg: code/_globalvars/lists/strings.dm:1  (GLOBAL_MANAGED(adjectives, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaladjectives()
    {
        adjectives = world.file2list("strings/names/adjectives.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "adjectives");
    }

    // tg: code/_globalvars/lists/strings.dm:2  GLOBAL_LIST_INIT(adverbs, world.file2list("strings/names/adverbs.txt"))
    public DmList adverbs;

    // tg: code/_globalvars/lists/strings.dm:2  (GLOBAL_MANAGED(adverbs, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaladverbs()
    {
        adverbs = world.file2list("strings/names/adverbs.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "adverbs");
    }

    // tg: code/_globalvars/lists/strings.dm:3  GLOBAL_LIST_INIT(dream_strings, world.file2list("strings/dreamstrings.txt"))
    public DmList dream_strings;

    // tg: code/_globalvars/lists/strings.dm:3  (GLOBAL_MANAGED(dream_strings, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldream_strings()
    {
        dream_strings = world.file2list("strings/dreamstrings.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "dream_strings");
    }

    // tg: code/_globalvars/lists/strings.dm:4  GLOBAL_LIST_INIT(fishing_tips, world.file2list("strings/fishing_tips.txt"))
    public DmList fishing_tips;

    // tg: code/_globalvars/lists/strings.dm:4  (GLOBAL_MANAGED(fishing_tips, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalfishing_tips()
    {
        fishing_tips = world.file2list("strings/fishing_tips.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "fishing_tips");
    }

    // tg: code/_globalvars/lists/strings.dm:5  GLOBAL_LIST_INIT(greek_letters, world.file2list("strings/greek_letters.txt"))
    public DmList greek_letters;

    // tg: code/_globalvars/lists/strings.dm:5  (GLOBAL_MANAGED(greek_letters, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalgreek_letters()
    {
        greek_letters = world.file2list("strings/greek_letters.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "greek_letters");
    }

    // tg: code/_globalvars/lists/strings.dm:6  GLOBAL_LIST_INIT(gross_adjectives, world.file2list("strings/names/gross_adjectives.txt"))
    public DmList gross_adjectives;

    // tg: code/_globalvars/lists/strings.dm:6  (GLOBAL_MANAGED(gross_adjectives, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalgross_adjectives()
    {
        gross_adjectives = world.file2list("strings/names/gross_adjectives.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "gross_adjectives");
    }

    // tg: code/_globalvars/lists/strings.dm:7  GLOBAL_LIST_INIT(ing_verbs, world.file2list("strings/names/ing_verbs.txt"))
    public DmList ing_verbs;

    // tg: code/_globalvars/lists/strings.dm:7  (GLOBAL_MANAGED(ing_verbs, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaling_verbs()
    {
        ing_verbs = world.file2list("strings/names/ing_verbs.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "ing_verbs");
    }

    // tg: code/_globalvars/lists/strings.dm:8  GLOBAL_LIST_INIT(junkmail_messages, world.file2list("strings/junkmail.txt"))
    public DmList junkmail_messages;

    // tg: code/_globalvars/lists/strings.dm:8  (GLOBAL_MANAGED(junkmail_messages, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaljunkmail_messages()
    {
        junkmail_messages = world.file2list("strings/junkmail.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "junkmail_messages");
    }

    // tg: code/_globalvars/lists/strings.dm:9  GLOBAL_LIST_INIT(numbers_as_words, world.file2list("strings/numbers_as_words.txt"))
    public DmList numbers_as_words;

    // tg: code/_globalvars/lists/strings.dm:9  (GLOBAL_MANAGED(numbers_as_words, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalnumbers_as_words()
    {
        numbers_as_words = world.file2list("strings/numbers_as_words.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "numbers_as_words");
    }

    // tg: code/_globalvars/lists/strings.dm:10  GLOBAL_LIST_INIT(phonetic_alphabet, world.file2list("strings/phonetic_alphabet.txt"))
    public DmList phonetic_alphabet;

    // tg: code/_globalvars/lists/strings.dm:10  (GLOBAL_MANAGED(phonetic_alphabet, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalphonetic_alphabet()
    {
        phonetic_alphabet = world.file2list("strings/phonetic_alphabet.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "phonetic_alphabet");
    }

    // tg: code/_globalvars/lists/strings.dm:11  GLOBAL_LIST_INIT(verbs, world.file2list("strings/names/verbs.txt"))
    public DmList verbs;

    // tg: code/_globalvars/lists/strings.dm:11  (GLOBAL_MANAGED(verbs, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalverbs()
    {
        verbs = world.file2list("strings/names/verbs.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "verbs");
    }

    // tg: code/_globalvars/lists/strings.dm:12  GLOBAL_LIST_INIT(wisdoms, world.file2list("strings/wisdoms.txt"))
    public DmList wisdoms;

    // tg: code/_globalvars/lists/strings.dm:12  (GLOBAL_MANAGED(wisdoms, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalwisdoms()
    {
        wisdoms = world.file2list("strings/wisdoms.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "wisdoms");
    }
}
