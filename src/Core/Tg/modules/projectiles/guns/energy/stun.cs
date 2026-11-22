// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/energy/stun.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The file whole: the disabler (M7x slice B), the cyborg's (silicons SI2) and the disabler SMG's full auto and seclight point; gaps GU1
// (D333): the cyborg's hybrid taser (EMP-proof, no seclight mount). The taser and the hybrid taser declare no procs. The var blocks are
// generated (D111).

// tg: code/modules/projectiles/guns/energy/stun.dm:17
public partial class obj_item_gun_energy_e_gun_advtaser_cyborg : obj_item_gun_energy_e_gun_advtaser
{
    // tg: code/modules/projectiles/guns/energy/stun.dm:23
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), EMP_PROTECT_ALL));
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/stun.dm:27
    public override async DmTask add_seclight_point()
    {
        return;
    }
}

// tg: code/modules/projectiles/guns/energy/stun.dm:38
public partial class obj_item_gun_energy_disabler : obj_item_gun_energy
{
    // tg: code/modules/projectiles/guns/energy/stun.dm:38
    public override async DmTask add_seclight_point()
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_seclite_attachable), Dm.named("light_overlay_icon", Dm.resource("icons/obj/weapons/guns/flashlights.dmi")), Dm.named("light_overlay", "flight"), Dm.named("overlay_x", 15), Dm.named("overlay_y", 10)));
    }
}

// tg: code/modules/projectiles/guns/energy/stun.dm:45
public partial class obj_item_gun_energy_disabler_smg : obj_item_gun_energy_disabler
{
    // tg: code/modules/projectiles/guns/energy/stun.dm:52
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_automatic_fire), 0.15f * SECONDS, Dm.named("allow_akimbo", FALSE)));
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/stun.dm:56
    public override async DmTask add_seclight_point()
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_seclite_attachable), Dm.named("light_overlay_icon", Dm.resource("icons/obj/weapons/guns/flashlights.dmi")), Dm.named("light_overlay", "flight"), Dm.named("overlay_x", 15), Dm.named("overlay_y", 13)));
    }
}

// tg: code/modules/projectiles/guns/energy/stun.dm:65
public partial class obj_item_gun_energy_disabler_cyborg : obj_item_gun_energy_disabler
{
    // tg: code/modules/projectiles/guns/energy/stun.dm:71
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), EMP_PROTECT_ALL));
        return dot;
    }
}
