// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/machinery/components/unary_devices/unary_devices.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4b slice 2): set_init_directions, assign_uid_vents and the pipe (dis)connection helpers. M5 slice 7: on_construction.
// Gaps X5: the ventcrawl alt-click (click_alt). The var block is generated.

// tg: code/modules/atmospherics/machinery/components/unary_devices/unary_devices.dm:1
public partial class obj_machinery_atmospherics_components_unary : obj_machinery_atmospherics_components
{
    // tg: code/modules/atmospherics/machinery/components/unary_devices/unary_devices.dm:13
    public override void set_init_directions(object init_dir = null)
    {
        initialize_directions = dir;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/unary_devices.dm:16  (M5 slice 7)
    public override async DmTask on_construction(mob user = null, params object[] __extra)
    {
        await base.on_construction(user, __extra);
        update_appearance();
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/unary_devices.dm:25
    public virtual object assign_uid_vents()
    {
        uid = num2text(gl_uid++);
        return uid;
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/unary_devices.dm:11  var/static/gl_uid = 1 (rules 10.6: a TgState field)
    public static ref float gl_uid => ref TgState.Current.obj_machinery_atmospherics_components_unary__gl_uid;

    // tg: code/modules/atmospherics/machinery/components/unary_devices/unary_devices.dm:29
    public virtual void change_pipe_connection(object disconnect = null)
    {
        if (Dm.truthy(disconnect))
        {
            disconnect_pipes();
            return;
        }
        connect_pipes();
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/unary_devices.dm:35
    public virtual void connect_pipes()
    {
        obj_machinery_atmospherics node1 = nodes[1] as obj_machinery_atmospherics;
        atmos_init();
        node1 = nodes[1] as obj_machinery_atmospherics;
        if (node1 != null)
        {
            node1.atmos_init();
            node1.add_member(this);
        }
        SSair.add_to_rebuild_queue(this);
    }

    // tg: code/modules/atmospherics/machinery/components/unary_devices/unary_devices.dm:44
    public virtual void disconnect_pipes()
    {
        obj_machinery_atmospherics node1 = nodes[1] as obj_machinery_atmospherics;
        if (node1 != null)
        {
            if (Dm.@in(this, node1.nodes)) //Only if it's actually connected. On-pipe version would is one-sided.
                node1.disconnect(this);
            nodes[1] = null;
        }
        if (Dm.truthy(parents[1]))
            nullify_pipenet(parents[1] as datum_pipeline);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/atmospherics/machinery/components/unary_devices/unary_devices.dm:11  /obj/machinery/atmospherics/components/unary/var/static/gl_uid = 1
    internal float obj_machinery_atmospherics_components_unary__gl_uid = 1;
}

// tg: code/modules/atmospherics/machinery/components/unary_devices/unary_devices.dm:20  (gaps X5: ventcrawling)
public partial class obj_machinery_atmospherics_components_unary : obj_machinery_atmospherics_components
{
    // tg: code/modules/atmospherics/machinery/components/unary_devices/unary_devices.dm:20
    [DmParams("beno")]
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        mob_living beno = user as mob_living;
        await beno.handle_ventcrawl(this);
        return CLICK_ACTION_SUCCESS;
    }
}
