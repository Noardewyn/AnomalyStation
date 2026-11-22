// Translated from tgstation (snapshot 1d99bd7f): code/modules/uplink/uplink_items/device_tools.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A2 (D316): the entries' procs (the entries and their categories are the generated var blocks).

// tg: code/modules/uplink/uplink_items/device_tools.dm:146
public partial class datum_uplink_item_device_tools_failsafe : datum_uplink_item_device_tools
{
    // tg: code/modules/uplink/uplink_items/device_tools.dm:154
    public override async DmTask<object> spawn_item(object spawn_path = null, mob user = null, datum_uplink_handler uplink_handler = null, atom_movable source = null)
    {
        datum_component_uplink uplink = source.GetComponent(typeof(datum_component_uplink)) as datum_component_uplink;
        if (uplink == null)
            return null;
        if (!Dm.truthy(uplink.unlock_note)) //no note means it can't be locked (typically due to being an implant.)
        {
            to_chat(user, span_warning(I18n.T("This device doesn't support code entry!")));
            return null;
        }

        uplink.failsafe_code = uplink.generate_code();
        string code = Dm.text("[]", islist(uplink.failsafe_code) ? english_list(uplink.failsafe_code as DmList) : uplink.failsafe_code);
        datum_antagonist_traitor traitor_datum = user.mind?.has_antag_datum(typeof(datum_antagonist_traitor)) as datum_antagonist_traitor;
        if (traitor_datum != null)
        {
            traitor_datum.antag_memory += Dm.text("<b>Uplink Failsafe Code:</b> []", code) + "<br>";
            await traitor_datum.update_static_data_for_all_viewers();
        }
        to_chat(user, span_warning(I18n.T("The new failsafe code for this uplink is now: [].[]", code, traitor_datum != null ? I18n.T(" You may check your antagonist info to recall this.") : null)));
        return source; //For log icon
    }
}
