// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/crossbreeding/recurring.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the recurring extracts, a core wrapped in a plain extract of its colour that it recharges a use at a time while
// the extract has uses left, and dies out once the extract is spent. The var blocks are generated (D111).

/*
Recurring extracts:
	Generates a new charge every few seconds.
	If depleted of its last charge, stops working.
*/
// tg: code/modules/research/xenobiology/crossbreeding/recurring.dm:7
public partial class obj_item_slimecross_recurring : obj_item_slimecross
{
    // tg: code/modules/research/xenobiology/crossbreeding/recurring.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        extract = Dm.@new(extract_type as Type, loc) as obj_item_slime_extract;
        visible_message(span_notice(I18n.T("[] wraps a layer of goo around itself!", this)));
        extract.name = name;
        extract.desc = desc;
        extract.icon = icon;
        extract.icon_state = icon_state;
        extract.color = color;
        extract.recurring = TRUE;
        Waiting.NoSleep(forceMove(extract));
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/recurring.dm:30
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (cooldown > 0)
            cooldown -= Dm.num(seconds_per_tick);
        else if (extract.extract_uses < 10 && extract.extract_uses > 0)
        {
            extract.extract_uses++;
            cooldown = max_cooldown;
        }
        else if (extract.extract_uses <= 0)
        {
            extract.visible_message(span_warning(I18n.T("The light inside [] flickers and dies out.", extract)));
            extract.desc = "A tiny, inert core, bleeding dark, cerulean-colored goo.";
            extract.icon_state = "prismatic";
            qdel(this);
        }
        return null;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/recurring.dm:42
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        STOP_PROCESSING(SSobj, this);
        return dot;
    }
}
