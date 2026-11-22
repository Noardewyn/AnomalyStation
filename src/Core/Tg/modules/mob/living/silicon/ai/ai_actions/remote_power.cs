// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/ai/ai_actions/remote_power.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI8, D326: the power transfer upgrade's ability). The var blocks are generated (D111).

// tg: code/modules/mob/living/silicon/ai/ai_actions/remote_power.dm:8
public partial class datum_action_innate_ai_ranged_power_apc : datum_action_innate_ai_ranged
{
    // tg: code/modules/mob/living/silicon/ai/ai_actions/remote_power.dm:17
    public override async DmTask<object> do_ability(mob_living clicker = null, atom clicked_on = null)
    {
        if (!isAI(clicker))
            return FALSE;
        mob_living_silicon_ai ai_clicker = (mob_living_silicon_ai)clicker;

        if (Dm.truthy(clicker.incapacitated))
        {
            unset_ranged_ability(clicker);
            return FALSE;
        }

        if (clicked_on is not obj_machinery_power_apc) // isapc
        {
            clicked_on.balloon_alert(ai_clicker, "not an APC!");
            return FALSE;
        }

        if (ai_clicker.battery - 50 <= 0)
        {
            to_chat(ai_clicker, span_warning(I18n.T("You do not have the battery to charge an APC!")));
            return FALSE;
        }

        obj_machinery_power_apc apc = (obj_machinery_power_apc)clicked_on;
        obj_item_stock_parts_power_store cell = apc.get_cell() as obj_item_stock_parts_power_store;
        await cell.give(STANDARD_BATTERY_CHARGE);
        ai_clicker.battery -= 50;
        return null;
    }
}
