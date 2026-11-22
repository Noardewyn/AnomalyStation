// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/screen_objects/ghost.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the ghost button's hover flick, the Re-enter Corpse button (the ghosts, 2026-10-16, D151) and the Orbit and
// Teleport buttons (M10x slice C, D281) translated; the other buttons and the scanner and HUD boxes (:9-17, :36-149) are out of
// the scope and never put on the HUD (_onclick/hud/ghost.cs; docs/ai/specs/not-translated.md section 16).

// tg: code/_onclick/hud/screen_objects/ghost.dm:1
[DmPath("/atom/movable/screen/ghost")]
public partial class atom_movable_screen_ghost : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/ghost.dm:5
    public override async DmTask MouseEntered(object location = null, object control = null, object @params = null)
    {
        await base.MouseEntered(location, control, @params);
        flick(Dm.add(icon_state, "_anim"), this);
    }
}

// tg: code/_onclick/hud/screen_objects/ghost.dm:19
[DmPath("/atom/movable/screen/ghost/orbit")]
public partial class atom_movable_screen_ghost_orbit : atom_movable_screen_ghost
{
    // tg: code/_onclick/hud/screen_objects/ghost.dm:24
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        await GLOB.orbit_menu.show(usr);
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/ghost.dm:26
[DmPath("/atom/movable/screen/ghost/reenter_corpse")]
public partial class atom_movable_screen_ghost_reenter_corpse : atom_movable_screen_ghost
{
    // tg: code/_onclick/hud/screen_objects/ghost.dm:31
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        mob_dead_observer G = usr as mob_dead_observer;
        G.reenter_corpse();
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/ghost.dm:45
[DmPath("/atom/movable/screen/ghost/teleport")]
public partial class atom_movable_screen_ghost_teleport : atom_movable_screen_ghost
{
    // tg: code/_onclick/hud/screen_objects/ghost.dm:50
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        mob_dead_observer G = usr as mob_dead_observer;
        G.dead_tele();
        await DmTask.CompletedTask;
        return null;
    }
}
