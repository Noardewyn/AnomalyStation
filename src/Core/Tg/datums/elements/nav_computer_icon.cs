// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/nav_computer_icon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1c, D316: the airlocks, shutters, turrets and consoles a navigation computer shows on the landing zone).
// The var block is generated (D111).

/**
 * element for atoms that have helper icons overlayed on their position in the shuttle navigation computer, such as airlocks
 */
// tg: code/datums/elements/nav_computer_icon.dm:4
public partial class datum_element_nav_computer_icon : datum_element
{
    // tg: code/datums/elements/nav_computer_icon.dm:11
    [DmParams("target", "use_icon", "use_icon_state", "only_show_on_shuttle_edge")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object use_icon = Dm.arg(__extra, 0);
        object use_icon_state = Dm.arg(__extra, 1);
        object only_show_on_shuttle_edge = Dm.arg(__extra, 2);
        object dot = await base.Attach(target, Dm.forward(__extra, use_icon, use_icon_state, only_show_on_shuttle_edge));
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;

        this.use_icon = use_icon;
        this.use_icon_state = use_icon_state;
        this.only_show_on_shuttle_edge = only_show_on_shuttle_edge;

        RegisterSignal(target, COMSIG_SHUTTLE_NAV_COMPUTER_IMAGE_REQUESTED, nameof(provide_image));
        return dot;
    }

    // tg: code/datums/elements/nav_computer_icon.dm:22
    [SignalHandler]
    public virtual void provide_image(datum source = null, DmList images_out = null)
    {
        var source_obj = source as obj;
        turf source_turf = get_turf(source_obj);
        if (source_turf == null)
            return;
        if (Dm.truthy(only_show_on_shuttle_edge))
        {
            bool isOnEdge = false;
            foreach (object direction in Dm.each(GLOB.cardinals))
            {
                var turf = get_step(source_obj, Dm.num(direction));
                if (!istype(turf?.loc, typeof(area_shuttle)))
                {
                    isOnEdge = true;
                    break;
                }
            }
            if (!isOnEdge)
                return;
        }

        image the_image = image(use_icon, source_turf, use_icon_state);
        the_image.dir = source_obj.dir;
        Dm.append(images_out, the_image);
    }

    // tg: code/datums/elements/nav_computer_icon.dm:42
    [DmParams("source")]
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_SHUTTLE_NAV_COMPUTER_IMAGE_REQUESTED);
        return dot;
    }
}
