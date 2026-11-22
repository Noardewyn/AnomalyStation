// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/squish.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package S2: the whole file (a player a vendor fell on is squished flat for a while). The var block is generated.

/**
 * squish.dm
 *
 * It's an element that squishes things. After the duration passes, it reverses the transformation it squished with, taking into account if they are a different orientation than they started (read: rotationally-fluid)
*/
// tg: code/datums/elements/squish.dm:11
public partial class datum_element_squish : datum_element
{
    // tg: code/datums/elements/squish.dm:1-2  #define SHORT 5/7, #define TALL 7/5 (file-local)
    private const float SHORT = 5f / 7f;
    private const float TALL = 7f / 5f;

    // tg: code/datums/elements/squish.dm:14
    [DmParams("target", "duration", "horizontal_squish")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0) ?? 20 * SECONDS;
        object horizontal_squish = Dm.arg(__extra, 1) ?? FALSE;
        object dot = await base.Attach(target, __extra);

        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;

        atom target_atom = (atom)target;
        float was_lying = FALSE;
        if (iscarbon(target))
        {
            mob_living_carbon carboniucus = (mob_living_carbon)target;
            was_lying = Dm.eq(carboniucus.body_position, LYING_DOWN) ? TRUE : FALSE;
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(Detach), target, was_lying, horizontal_squish), Dm.num(duration), file: "code/datums/elements/squish.dm", line: 24);

        if (Dm.truthy(horizontal_squish))
            target_atom.transform = ((matrix)target_atom.transform).Scale(SHORT, TALL);
        else
            target_atom.transform = ((matrix)target_atom.transform).Scale(TALL, SHORT);
        return dot;
    }

    // tg: code/datums/elements/squish.dm:31
    [DmParams("target", "was_lying", "horizontal_squish")]
    public override object Detach(datum source = null, params object[] __extra)
    {
        atom target = source as atom;
        object was_lying = Dm.arg(__extra, 0);
        object horizontal_squish = Dm.arg(__extra, 1);
        object dot = base.Detach(source, __extra);
        bool is_lying = false;
        if (iscarbon(target))
        {
            mob_living_carbon carboniucus = (mob_living_carbon)target;
            is_lying = Dm.eq(carboniucus.body_position, LYING_DOWN);
        }

        if (Dm.truthy(horizontal_squish))
            is_lying = !is_lying;

        if (Dm.truthy(was_lying) == is_lying)
            target.transform = ((matrix)target.transform).Scale(SHORT, TALL);
        else
            target.transform = ((matrix)target.transform).Scale(TALL, SHORT);
        return dot;
    }
}
