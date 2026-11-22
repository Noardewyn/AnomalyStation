// Translated from tgstation (snapshot 1d99bd7f): code/datums/request_message.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SC1: the whole file. A message a requests console receives: its sender, time, text, authentication, priority, radio channel and
// request type, its alert line and its window data. The var block is generated (D111), but for `priority`.

// tg: code/datums/request_message.dm:1
public partial class datum_request_message : datum
{
    /// The priority of the message
    // tg: code/datums/request_message.dm:14  var/priority = ""
    // Hand-written: tg's default is text, but every sender gives it a number (REQ_*_MESSAGE_PRIORITY, the window's priority), which a
    // generated string would not hold.
    public object priority = "";

    // tg: code/datums/request_message.dm:22
    [DmParams("data")]
    public override async DmTask New(params object[] __extra)
    {
        DmList data = Dm.arg<DmList>(__extra, 0);
        await base.New(__extra);
        sender_department = Dm.str(data["sender_department"]);
        received_time = round_timestamp();
        content = Dm.str(data["message"]);
        message_verified_by = Dm.str(data["verified"]);
        message_stamped_by = Dm.str(data["stamped"]);
        priority = data["priority"];
        radio_channel = data["notify_channel"];
        request_type = Dm.truthy(data["ore_update"]) ? ORE_UPDATE_REQUEST : Dm.str(data["request_type"]);
        DmList data_appended_list = data["appended_list"] as DmList;
        if (data_appended_list != null && data_appended_list.len > 0)
            appended_list = data_appended_list;
    }

    /// Retrieves the alert spoken/blared by the requests console that receives this message
    // tg: code/datums/request_message.dm:35
    public virtual string get_alert()
    {
        string authenticated = "";
        if (Dm.truthy(message_verified_by))
            authenticated = I18n.T(", Verified by [] (Authenticated)", message_verified_by);
        else if (Dm.truthy(message_stamped_by))
            authenticated = I18n.T(", Stamped by [] (Authenticated)", message_stamped_by);

        return I18n.T("Message from [][]", sender_department, authenticated);
    }

    /// Converts the message into a format for the tgui ui_data json
    // tg: code/datums/request_message.dm:46
    public virtual DmList message_ui_data()
    {
        DmList ui_data = Dm.list();
        ui_data["sender_department"] = sender_department;
        ui_data["received_time"] = received_time;
        ui_data["content"] = content;
        ui_data["message_verified_by"] = message_verified_by;
        ui_data["message_stamped_by"] = message_stamped_by;
        ui_data["priority"] = priority;
        ui_data["request_type"] = request_type;
        ui_data["appended_list"] = appended_list;

        return ui_data;
    }
}
