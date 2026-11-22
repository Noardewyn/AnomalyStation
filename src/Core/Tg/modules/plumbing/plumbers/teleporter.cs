// Translated from tgstation (snapshot 1d99bd7f): code/modules/plumbing/plumbers/teleporter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file. The var blocks are generated (D111).

///A bluespace input pipe for plumbing
// tg: code/modules/plumbing/plumbers/teleporter.dm:2
public partial class obj_machinery_plumbing_sender : obj_machinery_plumbing
{
    // tg: code/modules/plumbing/plumbers/teleporter.dm:11
    [DmParams("mapload", "layer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object layer = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_simple_demand), layer));
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/teleporter.dm:15
    [DmParams("user", "M")]
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item M = null)
    {
        obj_item_multitool multitool = (obj_item_multitool)M;
        if (!istype(multitool.buffer, typeof(obj_machinery_plumbing_receiver)))
        {
            to_chat(user, span_warning(I18n.T("Invalid buffer.")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (target != null)
            lose_teleport_target();

        set_teleport_target(multitool.buffer);

        to_chat(user, span_green(I18n.T("You successfully link [] to the [].", this, multitool.buffer)));
        return ITEM_INTERACT_SUCCESS;
    }

    ///Lose our previous target and make our previous target lose us. Seperate proc because I feel like I'll need this again
    // tg: code/modules/plumbing/plumbers/teleporter.dm:29
    public virtual void lose_teleport_target()
    {
        target.senders.Remove(this);
        target = null;
        icon_state = Dm.initial(this, (atom p) => p.icon_state);
    }

    ///Set a receiving plumbing object
    // tg: code/modules/plumbing/plumbers/teleporter.dm:35
    public virtual void set_teleport_target(object new_target = null)
    {
        target = (obj_machinery_plumbing_receiver)new_target;
        target.senders.Add(this);
        icon_state = Dm.initial(this, (atom p) => p.icon_state) + "_idle";
    }

    ///Transfer reagents and display a flashing icon
    // tg: code/modules/plumbing/plumbers/teleporter.dm:41
    public virtual async DmTask teleport_chemicals(obj_machinery_plumbing_receiver R = null, object amount = null)
    {
        flick(Dm.initial(this, (atom p) => p.icon_state) + "_flash", this);
        await ((datum_reagents)reagents).trans_to(R, Dm.num(amount));
    }
}

///A bluespace output pipe for plumbing. Supports multiple recipients. Must be constructed with a circuit board
// tg: code/modules/plumbing/plumbers/teleporter.dm:46
public partial class obj_machinery_plumbing_receiver : obj_machinery_plumbing
{
    // tg: code/modules/plumbing/plumbers/teleporter.dm:62
    [DmParams("mapload", "bolt")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object bolt = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_simple_supply), bolt));
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/teleporter.dm:66
    [DmParams("user", "M")]
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item M = null)
    {
        ((obj_item_multitool)M).set_buffer(this);
        balloon_alert(user, "saved to multitool buffer");
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/plumbing/plumbers/teleporter.dm:71
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(is_operational) || Dm.truthy(panel_open))
            return null;

        if (Dm.truthy(senders.len))
        {
            if (senders.len < next_index)
                next_index = 1;

            obj_machinery_plumbing_sender S = senders[next_index] as obj_machinery_plumbing_sender;
            if (QDELETED(S))
            {
                senders.Remove(S);
                return null;
            }

            Waiting.NoSleep(S.teleport_chemicals(this, pull_amount));

            flick(Dm.initial(this, (atom p) => p.icon_state) + "_flash", this);

            next_index++;

            Waiting.NoSleep(use_energy(active_power_usage * Dm.num(seconds_per_tick)));
        }
        return null;
    }

    ///Notify all senders to forget us
    // tg: code/modules/plumbing/plumbers/teleporter.dm:93
    public virtual void lose_senders()
    {
        foreach (object A in Dm.each(senders))
        {
            obj_machinery_plumbing_sender S = A as obj_machinery_plumbing_sender;
            if (S == null)
                continue;
            S.lose_teleport_target();
        }

        senders = Dm.list();
    }

    // tg: code/modules/plumbing/plumbers/teleporter.dm:102
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/plumbing/plumbers/teleporter.dm:105
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_pry_open(user, tool, deconstruct_on_fail: TRUE);
    }

    // tg: code/modules/plumbing/plumbers/teleporter.dm:108
    [DmParams("user", "I")]
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item I = null)
    {
        if (!Dm.truthy(panel_open))
            return NONE;
        lose_senders();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/plumbing/plumbers/teleporter.dm:114
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(panel_open) ? Dm.text("[]_open", base_icon_state) : base_icon_state as string;
        return dot;
    }
}
