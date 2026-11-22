// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/drone/extra_drone_types.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI7 (D326): the whole file (the syndrones, the snowflake, polymorphed, classic and derelict drones; their var blocks are
// generated, D111).

// tg: code/modules/mob/living/basic/drone/extra_drone_types.dm:5
public partial class mob_living_basic_drone_syndrone : mob_living_basic_drone
{
    // tg: code/modules/mob/living/basic/drone/extra_drone_types.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        var hidden_uplink = internal_storage.GetComponent(typeof(datum_component_uplink)) as datum_component_uplink;
        hidden_uplink.uplink_handler.set_telecrystals(telecrystal_count);
        return dot;
    }
}

// tg: code/modules/mob/living/basic/drone/extra_drone_types.dm:55
public partial class mob_living_basic_drone_syndrone_badass : mob_living_basic_drone_syndrone
{
    // tg: code/modules/mob/living/basic/drone/extra_drone_types.dm:60
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        var weapon_implant = Dm.@new<obj_item_implant_weapons_auth>(this);
        Waiting.NoSleep(weapon_implant.implant(this, force: TRUE));
        return dot;
    }
}

/// A free drone that people can be turned into via wabbajack.
// tg: code/modules/mob/living/basic/drone/extra_drone_types.dm:85
public partial class mob_living_basic_drone_polymorphed : mob_living_basic_drone
{
    // tg: code/modules/mob/living/basic/drone/extra_drone_types.dm:91
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        liberate();
        visualAppearance = Dm.str(pick(MAINTDRONE, REPAIRDRONE, SCOUTDRONE));
        if (visualAppearance == MAINTDRONE)
        {
            object colour = pick("grey", "blue", "red", "green", "pink", "orange");
            icon_state = Dm.text("[]_[]", visualAppearance, colour);
        }
        else
            icon_state = visualAppearance;

        icon_living = icon_state;
        icon_dead = Dm.text("[]_dead", visualAppearance);
        return dot;
    }
}

/// Derelict drones, a ghost role tasked with repairing KS13. Get gibbed if they leave.
// tg: code/modules/mob/living/basic/drone/extra_drone_types.dm:114
public partial class mob_living_basic_drone_derelict : mob_living_basic_drone
{
    // tg: code/modules/mob/living/basic/drone/extra_drone_types.dm:132
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_stationstuck), PUNISHMENT_GIB, "01000110 01010101 01000011 01001011 00100000 01011001 01001111 01010101<br>WARNING: Dereliction of KS13 detected. Self-destruct activated."));
        return dot;
    }
}

// tg: code/modules/mob_spawn/ghost_roles/drone_roles.dm:1
public partial class obj_effect_mob_spawn_ghost_role_drone : obj_effect_mob_spawn_ghost_role
{
    // tg: code/modules/mob_spawn/ghost_roles/drone_roles.dm:1
    public override void name_mob(mob_living spawned_mob = null, object forced_name = null)
    {
        if (!Dm.truthy(forced_name))
        {
            object designation = pick(GLOB.posibrain_names);
            forced_name = Dm.text("Drone ([]-[])", designation, rand(100, 999));
        }
        base.name_mob(spawned_mob, forced_name);
    }
}
