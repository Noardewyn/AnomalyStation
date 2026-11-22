// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/mecha_parts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/////////////////////////
////// Mecha Parts //////
/////////////////////////

// tg: code/modules/vehicles/mecha/mecha_parts.dm:5
public partial class obj_item_mecha_parts : obj_item
{
    //For attaching parts to a finished mech
    // tg: code/modules/vehicles/mecha/mecha_parts.dm:16
    public virtual async DmTask<object> try_attach_part(mob user = null, obj_vehicle_sealed_mecha M = null, object attach_right = null)
    {
        attach_right ??= FALSE;
        if (!Dm.truthy(await user.transferItemToLoc(this, M)))
        {
            to_chat(user, span_warning(I18n.T("\\The [] is stuck to your hand, you cannot put it in \\the []!", this, M)));
            return ITEM_INTERACT_BLOCKING;
        }
        user.visible_message(span_notice(I18n.T("[] attaches [] to [].", user, this, M)), span_notice(I18n.T("You attach [] to [].", this, M)));
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/vehicles/mecha/mecha_parts.dm:23
public partial class obj_item_mecha_parts_part : obj_item_mecha_parts
{
    // tg: code/modules/vehicles/mecha/mecha_parts.dm:23
    public override DmTask<object> try_attach_part(mob user = null, obj_vehicle_sealed_mecha M = null, object attach_right = null) => DmTask<object>.FromResult(ITEM_INTERACT_SUCCESS);
}

// tg: code/modules/vehicles/mecha/mecha_parts.dm:26
public partial class obj_item_mecha_parts_chassis : obj_item_mecha_parts
{
    // tg: code/modules/vehicles/mecha/mecha_parts.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (construct_type != null)
            Waiting.NoSleep(AddComponent(construct_type));
        return dot;
    }
}
