// Translated from tgstation (snapshot 1d99bd7f): code/modules/station_goals/station_goal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R1 (D327): the whole file: the station goal datum, which SSstation keeps by type (station.dm), the round-start command report
// lists and unlocks (communications.dm) and the round-end report scores (roundend.dm). The var block is generated (D111).

// tg: code/modules/station_goals/station_goal.dm:1
public partial class datum_station_goal : datum
{
    // tg: code/modules/station_goals/station_goal.dm:9
    public virtual async DmTask send_report()
    {
        priority_announce(I18n.T("Priority Nanotrasen directive received. Project \"[]\" details inbound.", name), I18n.T("Incoming Priority Message"), (SSstation.announcer as datum_centcom_announcer)?.get_rand_report_sound());
        print_command_report(get_report(), I18n.T("Nanotrasen Directive [] \\Roman[]", pick(GLOB.phonetic_alphabet), rand(1, 50)), announce: FALSE);
        on_report();
    }

    // tg: code/modules/station_goals/station_goal.dm:14
    public virtual void on_report()
    {
        //Additional unlocks/changes go here
        return;
    }

    // tg: code/modules/station_goals/station_goal.dm:18
    public virtual object get_report()
    {
        return report_message;
    }

    // tg: code/modules/station_goals/station_goal.dm:21
    public virtual object check_completion()
    {
        return completed;
    }

    // tg: code/modules/station_goals/station_goal.dm:24
    public virtual object get_result()
    {
        if (Dm.truthy(check_completion()))
            return I18n.T("<li>[] : []</li>", name, span_greentext(I18n.T("Completed!")));
        else
            return I18n.T("<li>[] : []</li>", name, span_redtext(I18n.T("Failed!")));
    }

    // tg: code/modules/station_goals/station_goal.dm:30
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        await base.Topic(href, href_list);
        if (!Dm.truthy(check_rights(R_ADMIN)) || !usr.client.holder.CheckAdminHref(href, href_list))
            return null;

        if (Dm.truthy(href_list["announce"]))
        {
            on_report();
            await send_report();
        }
        else if (Dm.truthy(href_list["remove"]))
            qdel(this);
        return null;
    }

    // tg: code/modules/station_goals/station_goal.dm:41
    public override async DmTask New(params object[] __extra)
    {
        if (Dm.@in(type, SSstation.goals_by_type))
            stack_trace(Dm.text("Creating a new station_goal of type [] when one already exists in SSstation.goals_by_type this is not supported anywhere. I trust you tho", type), "code/modules/station_goals/station_goal.dm", 43);
        else
            SSstation.goals_by_type[type] = this;
        await base.New(__extra);
    }

    // tg: code/modules/station_goals/station_goal.dm:48
    public override object Destroy(float? force = null)
    {
        if (SSstation.goals_by_type[type] == this)
            Dm.remove(ref SSstation.goals_by_type, type);
        return base.Destroy(force);
    }
}
