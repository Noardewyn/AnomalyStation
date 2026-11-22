// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/apc_frame.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice 3 (the APC): the whole file. The var block is generated (D111).

// APC HULL
// tg: code/game/objects/items/apc_frame.dm:2
public partial class obj_item_wallframe_apc : obj_item_wallframe
{
    // tg: code/game/objects/items/apc_frame.dm:8
    [DmParams("on_wall", "user")]
    public override async DmTask<object> try_build(atom support = null, mob user = null)
    {
        turf T = get_turf(support); //the user is not where it needs to be.
        area A = get_area(user) as area;
        if (Dm.truthy(A.apc))
        {
            to_chat(user, span_warning(I18n.T("This area already has an APC!")));
            return FALSE; //only one APC per area
        }
        if (!Dm.truthy(A.requires_power) || Dm.truthy(A.always_unpowered))
        {
            to_chat(user, span_warning(I18n.T("You cannot place [] in this area!", this)));
            return FALSE; //can't place apcs in areas with no power requirement
        }
        foreach (obj_machinery_power_terminal E in Dm.each<obj_machinery_power_terminal>(T))
        {
            if (Dm.truthy(E.master))
            {
                to_chat(user, span_warning(I18n.T("There is another network terminal here!")));
                return FALSE;
            }
        }
        return await base.try_build(support, user);
    }

    // tg: code/game/objects/items/apc_frame.dm:23
    public override void after_attach(obj attached_to = null)
    {
        foreach (obj_machinery_power_terminal E in Dm.each<obj_machinery_power_terminal>(attached_to.loc))
        {
            ((obj_machinery_power_apc)attached_to).make_terminal();
            return;
        }
    }
}
