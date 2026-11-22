// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/skin.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111).

/*Polyvitiligo
 * Slight reduction to stealth
 * Greatly increases resistance
 * Slightly increases stage speed
 * Increases transmissibility
 * Critical level
 * Bonus: Makes the mob gain a random crayon powder colorful reagent.
*/
// tg: code/datums/diseases/advance/symptoms/skin.dm:9
public partial class datum_symptom_polyvitiligo : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/skin.dm:28  var/static/list/banned_reagents (proc-level static, rules 10.6)
    private static DmList Activate__banned_reagents => TgState.Current.datum_symptom_polyvitiligo__Activate__banned_reagents ??= Dm.list(typeof(datum_reagent_colorful_reagent_powder_invisible), typeof(datum_reagent_colorful_reagent_powder_white));

    // tg: code/datums/diseases/advance/symptoms/skin.dm:22
    [DmParams("A")]
    public override async DmTask<object> Activate(datum_disease_advance A = null)
    {
        object dot = null;
        dot = await base.Activate(A);
        if (!Dm.truthy(dot))
            return dot;
        mob_living M = A.affected_mob;
        if (A.stage == 5)
        {
            DmList banned_reagents = Activate__banned_reagents;
            object color = pick(Dm.sub(subtypesof(typeof(datum_reagent_colorful_reagent_powder)), banned_reagents) as DmList);
            if (((datum_reagents)M.reagents).total_volume <= (((datum_reagents)M.reagents).maximum_volume / 10)) // no flooding humans with 1000 units of colorful reagent
                await ((datum_reagents)M.reagents).add_reagent(color, 5);
        }
        else
        {
            if (prob(50)) // spam
                M.visible_message(span_warning(I18n.T("[] looks rather vibrant...", M)), span_notice(I18n.T("The colors, man, the colors...")));
        }
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/datums/diseases/advance/symptoms/skin.dm:28
    internal DmList datum_symptom_polyvitiligo__Activate__banned_reagents;
}
