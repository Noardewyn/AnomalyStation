// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/energy/pulse.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps GU1 (D333): the file whole. The pulse rifle (EMP-proof), the arcade's prize pulse rifle (a point of interest the ghosts are told
// about), the pulse carbine's seclight mount and the pulse destroyer's one setting. The loyalty-pinned, lethal, taserless, pistol and
// M1911-P variants declare no procs. The var blocks are generated (D111).

// tg: code/modules/projectiles/guns/energy/pulse.dm:1
public partial class obj_item_gun_energy_pulse : obj_item_gun_energy
{
    // tg: code/modules/projectiles/guns/energy/pulse.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), EMP_PROTECT_ALL));
        return dot;
    }
}

// tg: code/modules/projectiles/guns/energy/pulse.dm:20
public partial class obj_item_gun_energy_pulse_prize : obj_item_gun_energy_pulse
{
    // tg: code/modules/projectiles/guns/energy/pulse.dm:23
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(SSpoints_of_interest.make_point_of_interest(this));
        turf T = get_turf(this);

        message_admins(Dm.text("A pulse rifle prize has been created at []", ADMIN_VERBOSEJMP(T)));
        log_game(Dm.text("A pulse rifle prize has been created at []", AREACOORD(T)));

        notify_ghosts(
            I18n.T("Someone won a pulse rifle as a prize!"),
            source: this,
            header: "Pulse rifle prize"
        );
        return dot;
    }
}

// tg: code/modules/projectiles/guns/energy/pulse.dm:40
public partial class obj_item_gun_energy_pulse_carbine : obj_item_gun_energy_pulse
{
    // tg: code/modules/projectiles/guns/energy/pulse.dm:50
    public override async DmTask add_seclight_point()
    {
        await AddComponent(typeof(datum_component_seclite_attachable), Dm.named("light_overlay_icon", Dm.resource("icons/obj/weapons/guns/flashlights.dmi")), Dm.named("light_overlay", "flight"), Dm.named("overlay_x", 18), Dm.named("overlay_y", 12));
    }
}

// tg: code/modules/projectiles/guns/energy/pulse.dm:66
public partial class obj_item_gun_energy_pulse_destroyer : obj_item_gun_energy_pulse
{
    // tg: code/modules/projectiles/guns/energy/pulse.dm:73
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        to_chat(user, span_danger(I18n.T("[] has three settings, and they are all DESTROY.", name)));
        return null;
    }
}
