// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/supplypod_beacon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4d (D298): the whole file, the supply pod beacon with its lights (the linked, ready and launched overlays). The var block is
// generated (D111).

// tg: code/modules/cargo/supplypod_beacon.dm:1
public partial class obj_item_supplypod_beacon : obj_item
{
    // tg: code/modules/cargo/supplypod_beacon.dm:27
    public virtual void update_status(object consoleStatus = null)
    {
        switch ((int)Dm.num(consoleStatus))
        {
            case SP_LINKED:
                linked = TRUE;
                playsound(this, Dm.resource("sound/machines/beep/twobeep.ogg"), 50, FALSE);
                break;
            case SP_READY:
                ready = TRUE;
                break;
            case SP_LAUNCH:
                launched = TRUE;
                playsound(this, Dm.resource("sound/machines/beep/triple_beep.ogg"), 50, FALSE);
                playsound(this, Dm.resource("sound/machines/warning-buzzer.ogg"), 50, FALSE);
                _addtimer(Dm.@new<datum_callback>(this, nameof(end_launch)), 33, file: "code/modules/cargo/supplypod_beacon.dm", line: 38); //wait 3.3 seconds (time it takes for supplypod to land), then update icon
                break;
            case SP_UNLINK:
                linked = FALSE;
                playsound(this, Dm.resource("sound/machines/synth/synth_no.ogg"), 50, FALSE);
                break;
            case SP_UNREADY:
                ready = FALSE;
                break;
        }
        update_appearance();
    }

    // tg: code/modules/cargo/supplypod_beacon.dm:46
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (Dm.truthy(launched))
        {
            Dm.append(ref dot, "sp_green");
            return dot;
        }
        if (Dm.truthy(ready))
        {
            Dm.append(ref dot, "sp_yellow");
            return dot;
        }
        if (Dm.truthy(linked))
        {
            Dm.append(ref dot, "sp_orange");
            return dot;
        }
        return dot;
    }

    // tg: code/modules/cargo/supplypod_beacon.dm:58
    public virtual void end_launch()
    {
        launched = FALSE;
        update_status();
    }

    // tg: code/modules/cargo/supplypod_beacon.dm:62
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("It looks like it has a few anchoring bolts.")));
        if (express_console == null)
            Dm.append(ref dot, span_notice(I18n.T("[] is not currently linked to an Express Supply console.", this)));
        else
            Dm.append(ref dot, span_notice(I18n.T("Alt-click to unlink it from the Express Supply console.")));
        return dot;
    }

    // tg: code/modules/cargo/supplypod_beacon.dm:70
    public override object Destroy(float? force = null)
    {
        if (express_console != null)
            express_console.beacon = null;
        return base.Destroy(force);
    }

    // tg: code/modules/cargo/supplypod_beacon.dm:75
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        if (Dm.eq(await default_unfasten_wrench(user, tool), SUCCESSFUL_UNFASTEN))
        {
            pixel_x = 0;
            pixel_y = 0;
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/cargo/supplypod_beacon.dm:82
    public virtual void unlink_console()
    {
        if (express_console != null)
        {
            express_console.beacon = null;
            express_console = null;
        }
        update_status(SP_UNLINK);
        update_status(SP_UNREADY);
    }

    // tg: code/modules/cargo/supplypod_beacon.dm:89
    public virtual void link_console(obj_machinery_computer_cargo_express C = null, mob_living user = null)
    {
        if (C.beacon != null) //if new console has a beacon, then...
            C.beacon.unlink_console(); //unlink the old beacon from new console
        if (express_console != null) //if this beacon has an express console
            express_console.beacon = null; //remove the connection the expressconsole has from beacons
        express_console = C; //set the linked console var to the console
        express_console.beacon = this; //out with the old in with the news
        update_status(SP_LINKED);
        if (Dm.truthy(express_console.using_beacon))
            update_status(SP_READY);
        to_chat(user, span_notice(I18n.T("[] linked to [].", this, C)));
    }

    // tg: code/modules/cargo/supplypod_beacon.dm:101
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (express_console == null)
        {
            to_chat(user, span_alert(I18n.T("There is no linked console.")));
            return CLICK_ACTION_BLOCKING;
        }
        unlink_console();
        return CLICK_ACTION_SUCCESS;
    }
}
