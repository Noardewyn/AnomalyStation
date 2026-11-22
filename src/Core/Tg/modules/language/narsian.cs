// Translated from tgstation (snapshot 1d99bd7f): code/modules/language/narsian.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps L2 (D327): the procs of the file, Nar'Sian's syllables, mixed from its base syllables once a round; the vars are generated (D111).

// tg: code/modules/language/narsian.dm:1
public partial class datum_language_narsie : datum_language
{
    //the list of syllables we'll combine with itself to get a larger list of syllables
    // tg: code/modules/language/narsian.dm:11  var/static/list/base_syllables (a type static, rules 10.6)
    private static DmList base_syllables => TgState.Current.datum_language_narsie__base_syllables ??= Dm.list(
        "h", "v", "c", "e", "g", "d", "r", "n", "h", "o", "p",
        "ra", "so", "at", "il", "ta", "gh", "sh", "ya", "te", "sh", "ol", "ma", "om", "ig", "ni", "in",
        "sha", "mir", "sas", "mah", "zar", "tok", "lyr", "nqa", "nap", "olt", "val", "qha",
        "fwe", "ath", "yro", "eth", "gal", "gib", "bar", "jin", "kla", "atu", "kal", "lig",
        "yoka", "drak", "loso", "arta", "weyh", "ines", "toth", "fara", "amar", "nyag", "eske", "reth", "dedo", "btoh", "nikt", "neth",
        "kanas", "garis", "uloft", "tarat", "khari", "thnor", "rekka", "ragga", "rfikk", "harfr", "andid", "ethra", "dedol", "totum",
        "ntrath", "keriam");

    // tg: code/modules/language/narsian.dm:32
    public override async DmTask New(params object[] __extra)
    {
        foreach (object syllable in Dm.each(base_syllables)) //we only do this once, since there's only ever a single one of each language datum.
        {
            foreach (object target_syllable in Dm.each(base_syllables))
            {
                if (Dm.eq(syllable, target_syllable)) //don't combine with yourself
                    continue;
                if (length(syllable) + length(target_syllable) > 8) //if the resulting syllable would be very long, don't put anything between it
                    Dm.append(ref syllables, Dm.text("[][]", syllable, target_syllable));
                else if (prob(80)) //we'll be minutely different each round.
                    Dm.append(ref syllables, Dm.text("[]'[]", syllable, target_syllable));
                else if (prob(25)) //5% chance of - instead of '
                    Dm.append(ref syllables, Dm.text("[]-[]", syllable, target_syllable));
                else //15% chance of no ' or - at all
                    Dm.append(ref syllables, Dm.text("[][]", syllable, target_syllable));
            }
        }
        await base.New(__extra);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/language/narsian.dm:11  /datum/language/narsie's var/static/list/base_syllables
    internal DmList datum_language_narsie__base_syllables;
}
