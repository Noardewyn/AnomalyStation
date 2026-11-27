// Translated from tgstation (snapshot 1d99bd7f): code/modules/holodeck/turfs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339): the whole file, the holographic floors the virtual domains (grasslands hunt's basalt) and the holodeck lay:
// the floor that ignores tools and does not burn or break, and the subtypes' random states. The holographic water is a
// fishing spot. The var blocks are generated (D111).

// tg: code/modules/holodeck/turfs.dm:1
public partial class turf_open_floor_holofloor : turf_open_floor
{
    // tg: code/modules/holodeck/turfs.dm:8
    public override async DmTask<object> attackby(obj_item I = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        await DmTask.CompletedTask;
        return null; // HOLOFLOOR DOES NOT GIVE A FUCK
    }

    // tg: code/modules/holodeck/turfs.dm:11
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        return ITEM_INTERACT_BLOCKING; // Fuck you
    }

    // tg: code/modules/holodeck/turfs.dm:14
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_blocker), TOOL_CROWBAR, TOOL_ACT_PRIMARY));
        return dot;
    }

    // tg: code/modules/holodeck/turfs.dm:18
    public override object burn_tile()
    {
        return null; //you can't burn a hologram!
    }

    // tg: code/modules/holodeck/turfs.dm:21
    public override object break_tile()
    {
        return null; //you can't break a hologram!
    }
}

// tg: code/modules/holodeck/turfs.dm:28
public partial class turf_open_floor_holofloor_chapel : turf_open_floor_holofloor
{
    // tg: code/modules/holodeck/turfs.dm:43
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.eq(direction, SOUTH))
            Waiting.NoSleep(setDir(direction));
        return dot;
    }
}

// tg: code/modules/holodeck/turfs.dm:61
public partial class turf_open_floor_holofloor_grass : turf_open_floor_holofloor
{
    // tg: code/modules/holodeck/turfs.dm:69
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("grass[]", rand(0, 3));
        return dot;
    }
}

// tg: code/modules/holodeck/turfs.dm:91
public partial class turf_open_floor_holofloor_beach_water : turf_open_floor_holofloor_beach
{
    // tg: code/modules/holodeck/turfs.dm:98
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_fishing_spot), typeof(datum_fish_source_holographic)));
        return dot;
    }
}

// tg: code/modules/holodeck/turfs.dm:102
public partial class turf_open_floor_holofloor_asteroid : turf_open_floor_holofloor
{
    // tg: code/modules/holodeck/turfs.dm:109
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        icon_state = Dm.text("asteroid[]", rand(0, 12));
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/holodeck/turfs.dm:113
public partial class turf_open_floor_holofloor_basalt : turf_open_floor_holofloor
{
    // tg: code/modules/holodeck/turfs.dm:120
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(15))
        {
            icon_state = Dm.text("basalt[]", rand(0, 12));
            switch (icon_state)
            {
                case "basalt1":
                case "basalt2":
                case "basalt3":
                    Waiting.NoSleep(set_light(BASALT_LIGHT_RANGE_BRIGHT, BASALT_LIGHT_POWER, LIGHT_COLOR_LAVA));
                    break;
                case "basalt5":
                case "basalt9":
                    Waiting.NoSleep(set_light(BASALT_LIGHT_RANGE_DIM, BASALT_LIGHT_POWER, LIGHT_COLOR_LAVA));
                    break;
            }
        }
        return dot;
    }
}

// tg: code/modules/holodeck/turfs.dm:138
public partial class turf_open_floor_holofloor_hyperspace : turf_open_floor_holofloor
{
    // tg: code/modules/holodeck/turfs.dm:146
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        icon_state = Dm.text("speedspace_ns_[]", (Dm.num(x) + 5 * Dm.num(y) + (Dm.num(y) % 2 + 1) * 7) % 15 + 1);
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/holodeck/turfs.dm:146 (the type has no declaration of its own)
public partial class turf_open_floor_holofloor_hyperspace_ns : turf_open_floor_holofloor_hyperspace
{
    // tg: code/modules/holodeck/turfs.dm:150
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("speedspace_ns_[]", (Dm.num(x) + 5 * Dm.num(y) + (Dm.num(y) % 2 + 1) * 7) % 15 + 1);
        return dot;
    }
}

// tg: code/modules/holodeck/turfs.dm:154
public partial class turf_open_floor_holofloor_carpet : turf_open_floor_holofloor
{
    // tg: code/modules/holodeck/turfs.dm:167
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        _addtimer(Dm.@new<datum_callback>(this, nameof(update_appearance)), 0.1f * SECONDS, file: "code/modules/holodeck/turfs.dm", line: 169);
        return dot;
    }

    // tg: code/modules/holodeck/turfs.dm:171
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        updates ??= ALL;
        object dot = await base.update_icon(updates, __extra);
        if (Dm.truthy(Dm.bitand(updates, UPDATE_SMOOTHING)) && Dm.truthy(overfloor_placed) && Dm.truthy(Dm.bitand(smoothing_flags, USES_SMOOTHING)))
            QUEUE_SMOOTH(this);
        return dot;
    }
}
