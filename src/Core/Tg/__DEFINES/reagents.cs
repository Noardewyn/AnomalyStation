// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/reagents.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only REACTION_TAG_READABLE (gaps P5: the chem dispenser's and the reaction lookup's tag filters); the file's
// constants are generated (Defines).

public static partial class GlobalProcs
{
    /// Readable list of reagent reaction tags (in the same order as they are defined!)
    // tg: code/__DEFINES/reagents.dm:220  #define REACTION_TAG_READABLE list(...): a new list at each use (rules 11.1)
    public static DmList REACTION_TAG_READABLE => Dm.list(
        Dm.kv("BRUTE", REACTION_TAG_BRUTE),
        Dm.kv("BURN", REACTION_TAG_BURN),
        Dm.kv("TOXIN", REACTION_TAG_TOXIN),
        Dm.kv("OXY", REACTION_TAG_OXY),
        Dm.kv("HEALING", REACTION_TAG_HEALING),
        Dm.kv("DAMAGING", REACTION_TAG_DAMAGING),
        Dm.kv("EXPLOSIVE", REACTION_TAG_EXPLOSIVE),
        Dm.kv("OTHER", REACTION_TAG_OTHER),
        Dm.kv("DANGEROUS", REACTION_TAG_DANGEROUS),
        Dm.kv("EASY", REACTION_TAG_EASY),
        Dm.kv("MODERATE", REACTION_TAG_MODERATE),
        Dm.kv("HARD", REACTION_TAG_HARD),
        Dm.kv("ORGAN", REACTION_TAG_ORGAN),
        Dm.kv("DRINK", REACTION_TAG_DRINK),
        Dm.kv("FOOD", REACTION_TAG_FOOD),
        Dm.kv("SLIME", REACTION_TAG_SLIME),
        Dm.kv("DRUG", REACTION_TAG_DRUG),
        Dm.kv("UNIQUE", REACTION_TAG_UNIQUE),
        Dm.kv("CHEMICAL", REACTION_TAG_CHEMICAL),
        Dm.kv("PLANT", REACTION_TAG_PLANT),
        Dm.kv("COMPETITIVE", REACTION_TAG_COMPETITIVE),
        Dm.kv("COMPONENT", REACTION_TAG_COMPONENT),
        Dm.kv("ACTIVE", REACTION_TAG_ACTIVE));
}
