// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/cleaning.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H4 (D333, the janitor): the whole file: a movable that scrubs every tile it moves onto, with the small items lying there and
// the humans lying down (the janicart's floor buffer; the janitor key's suicide).

// tg: code/datums/elements/cleaning.dm:1
public partial class datum_element_cleaning : datum_element
{
    // tg: code/datums/elements/cleaning.dm:3
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!ismovable(target))
            return ELEMENT_INCOMPATIBLE;
        RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(clean));
        return dot;
    }

    // tg: code/datums/elements/cleaning.dm:9
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_MOVABLE_MOVED);
        return dot;
    }

    // tg: code/datums/elements/cleaning.dm:13
    [SignalHandler]
    public virtual void clean(datum source = null)
    {
        var atom_movable = source as atom_movable;
        var tile = atom_movable.loc as turf;
        if (tile == null)
            return;

        Waiting.NoSleep(tile.wash(CLEAN_SCRUB));
        foreach (atom cleaned in Dm.each_cast<atom>(tile))
        {
            // Clean small items that are lying on the ground
            if (cleaned is obj_item cleaned_item)
            {
                if (Dm.num(cleaned_item.w_class) <= WEIGHT_CLASS_SMALL)
                    Waiting.NoSleep(cleaned_item.wash(CLEAN_SCRUB));
                continue;
            }
            // Clean humans that are lying down
            if (cleaned is not mob_living_carbon_human cleaned_human)
                continue;
            if (cleaned_human.body_position == LYING_DOWN)
            {
                Waiting.NoSleep(cleaned_human.wash(CLEAN_SCRUB));
                Waiting.NoSleep(cleaned_human.regenerate_icons());
                to_chat(cleaned_human, span_danger(I18n.T("[] cleans your face!", atom_movable)));
            }
        }
    }
}
