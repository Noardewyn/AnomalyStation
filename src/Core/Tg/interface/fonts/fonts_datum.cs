// Translated from tgstation (snapshot 1d99bd7f): interface/fonts/fonts_datum.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps W1 (2026-11-07): the font datum's metrics, which tg's status display measures its lines with (whether one scrolls). The fonts'
// data is generated (Generated/Types/datum.g.cs: tiny_unicode.dm and the others).

/// A font datum, it exists to define a custom font to use in a span style later.
// tg: interface/fonts/fonts_datum.dm:2
public partial class datum_font : datum
{
    /// Get font metrics
    /// From Lummox's dmifontsplus (https://www.byond.com/developer/LummoxJR/DmiFontsPlus)
    // tg: interface/fonts/fonts_datum.dm:39
    public virtual object get_metrics(object text = null, object flags = null, object first_line = null)
    {
        float dot = 0;
        float longest = 0;
        if (!Dm.truthy(length(text)))
            return dot;

        float i = 1;
        float idx = 0;
        while (i <= length(text))
        {
            float character = text2ascii(text, i++);
            if (character <= 10)
            {
                if (character <= 7)
                    dot += character; // spacers for justification

                if (character <= 9)
                    continue; // soft-break chars

                if (dot != 0 && idx != 0 && !Dm.truthy(Dm.bitand(flags, INCLUDE_AC)))
                    dot -= max(Dm.num(metrics[idx + 3]), 0);

                longest = max(longest, dot + Dm.num(first_line));
                dot = 0;
                first_line = 0;
                idx = 0;
                continue;
            }

            idx = (character - Dm.num(start)) * 3;
            if (idx <= 0 || idx >= metrics.Count)
                idx = (Dm.num(default_character) - Dm.num(start)) * 3;

            if (dot == 0 && !Dm.truthy(Dm.bitand(flags, INCLUDE_AC)))
                dot -= Dm.num(metrics[idx + 1]);
            dot += Dm.num(metrics[idx + 1]) + Dm.num(metrics[idx + 2]) + Dm.num(metrics[idx + 3]);
        }

        if (dot != 0 && idx != 0 && !Dm.truthy(Dm.bitand(flags, INCLUDE_AC)))
            dot -= max(Dm.num(metrics[idx + 3]), 0);

        dot = max(dot + Dm.num(first_line), longest);
        if (dot > 0)
            dot += Dm.num(overhang);
        return dot;
    }
}
