// Translated from tgstation (snapshot 1d99bd7f): code/modules/uplink/uplink_devices.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A2 (D316): the whole file (the pre-set uplinks for admin spawns: the radio-like uplink and its debug, nuclear, lone
// operative, clown operative and dusty variants, the multitool and the pen uplinks). The var blocks are generated.

// A collection of pre-set uplinks, for admin spawns.

// Radio-like uplink; not an actual radio because this uplink is most commonly
// used for nuke ops, for whom opening the radio GUI and the uplink GUI
// simultaneously is an annoying distraction.
// tg: code/modules/uplink/uplink_devices.dm:7
public partial class obj_item_uplink : obj_item
{
    // tg: code/modules/uplink/uplink_devices.dm:29
    [DmParams("mapload", "owner", "tc_amount", "uplink_handler_override")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object owner = Dm.arg(__extra, 0);
        object tc_amount = Dm.arg(__extra, 1) ?? 20;
        object uplink_handler_override = Dm.arg(__extra, 2);
        object dot = base.Initialize(mapload, Dm.forward(__extra, owner, tc_amount, uplink_handler_override));
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_uplink),
            Dm.named("owner", owner),
            Dm.named("lockable", lockable_uplink),
            Dm.named("enabled", TRUE),
            Dm.named("uplink_flag", uplink_flag),
            Dm.named("starting_tc", tc_amount),
            Dm.named("uplink_handler_override", uplink_handler_override)));
        return dot;
    }
}

// tg: code/modules/uplink/uplink_devices.dm:42
public partial class obj_item_uplink_debug : obj_item_uplink
{
    // tg: code/modules/uplink/uplink_devices.dm:44
    [DmParams("mapload", "owner", "tc_amount", "uplink_handler_override")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object owner = Dm.arg(__extra, 0);
        object tc_amount = Dm.arg(__extra, 1) ?? 9000;
        object uplink_handler_override = Dm.arg(__extra, 2);
        object dot = base.Initialize(mapload, Dm.forward(__extra, owner, tc_amount, uplink_handler_override));
        datum_component_uplink hidden_uplink = GetComponent(typeof(datum_component_uplink)) as datum_component_uplink;
        hidden_uplink.name = "debug uplink";
        hidden_uplink.uplink_handler.debug_mode = TRUE;
        return dot;
    }
}

// tg: code/modules/uplink/uplink_devices.dm:54
public partial class obj_item_uplink_nuclear_debug : obj_item_uplink_nuclear
{
    // tg: code/modules/uplink/uplink_devices.dm:57
    [DmParams("mapload", "owner", "tc_amount", "uplink_handler_override")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object owner = Dm.arg(__extra, 0);
        object tc_amount = Dm.arg(__extra, 1) ?? 9000;
        object uplink_handler_override = Dm.arg(__extra, 2);
        object dot = base.Initialize(mapload, Dm.forward(__extra, owner, tc_amount, uplink_handler_override));
        datum_component_uplink hidden_uplink = GetComponent(typeof(datum_component_uplink)) as datum_component_uplink;
        hidden_uplink.name = "debug nuclear uplink";
        hidden_uplink.uplink_handler.debug_mode = TRUE;
        return dot;
    }
}

// tg: code/modules/uplink/uplink_devices.dm:64
public partial class obj_item_uplink_nuclear_restricted : obj_item_uplink
{
    // tg: code/modules/uplink/uplink_devices.dm:66
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        datum_component_uplink hidden_uplink = GetComponent(typeof(datum_component_uplink)) as datum_component_uplink;
        hidden_uplink.allow_restricted = FALSE;
        return dot;
    }
}

// tg: code/modules/uplink/uplink_devices.dm:79
public partial class obj_item_uplink_old : obj_item_uplink
{
    // tg: code/modules/uplink/uplink_devices.dm:82
    [DmParams("mapload", "owner", "tc_amount", "uplink_handler_override")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object owner = Dm.arg(__extra, 0);
        object tc_amount = Dm.arg(__extra, 1) ?? 10;
        object uplink_handler_override = Dm.arg(__extra, 2);
        object dot = base.Initialize(mapload, Dm.forward(__extra, owner, tc_amount, uplink_handler_override));
        datum_component_uplink hidden_uplink = GetComponent(typeof(datum_component_uplink)) as datum_component_uplink;
        hidden_uplink.name = "dusty radio";
        return dot;
    }
}

// Multitool uplink
// tg: code/modules/uplink/uplink_devices.dm:88
public partial class obj_item_multitool_uplink : obj_item_multitool
{
    // tg: code/modules/uplink/uplink_devices.dm:88
    [DmParams("mapload", "owner", "tc_amount", "uplink_handler_override")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object owner = Dm.arg(__extra, 0);
        object tc_amount = Dm.arg(__extra, 1) ?? 20;
        object uplink_handler_override = Dm.arg(__extra, 2);
        object dot = base.Initialize(mapload, Dm.forward(__extra, owner, tc_amount, uplink_handler_override));
        Waiting.NoSleep(AddComponent(typeof(datum_component_uplink), owner, FALSE, TRUE, UPLINK_TRAITORS, tc_amount));
        return dot;
    }
}

// Pen uplink
// tg: code/modules/uplink/uplink_devices.dm:93
public partial class obj_item_pen_uplink : obj_item_pen
{
    // tg: code/modules/uplink/uplink_devices.dm:93
    [DmParams("mapload", "owner", "tc_amount", "uplink_handler_override")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object owner = Dm.arg(__extra, 0);
        object tc_amount = Dm.arg(__extra, 1) ?? 20;
        object uplink_handler_override = Dm.arg(__extra, 2);
        object dot = base.Initialize(mapload, Dm.forward(__extra, owner, tc_amount, uplink_handler_override));
        Waiting.NoSleep(AddComponent(typeof(datum_component_uplink), owner, TRUE, FALSE, UPLINK_TRAITORS, tc_amount));
        return dot;
    }
}
