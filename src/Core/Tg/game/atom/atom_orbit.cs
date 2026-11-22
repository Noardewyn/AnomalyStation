// Translated from tgstation (snapshot 1d99bd7f): code/game/atom/atom_orbit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the var /atom/var/orbiters (wave 3, the atoms package; orbit_target is generated) and get_all_orbiters (M10x slice C,
// D281: the orbit menu's orbiter counts).

// tg: code/game/atom/atom_orbit.dm:1
public partial class atom : datum
{
    ///The orbiter component, if there's anything orbiting this atom
    // tg: code/game/atom/atom_orbit.dm:5
    public datum_component_orbiter orbiters;
}

// tg: code/game/atom/atom_orbit.dm:16
public partial class atom : datum
{
    /**
     * Recursive getter method to return a list of all ghosts orbitting this atom
     *
     * This will work fine without manually passing arguments.
     * * processed - The list of atoms we've already convered
     * * source - Is this the atom for who we're counting up all the orbiters?
     * * ignored_stealthed_admins - If TRUE, don't count admins who are stealthmoded and orbiting this
     */
    // tg: code/game/atom/atom_orbit.dm:16
    public virtual object get_all_orbiters(DmList processed = null, float? source = null, float? ignore_stealthed_admins = null)
    {
        source ??= TRUE;
        ignore_stealthed_admins ??= TRUE;
        DmList output = Dm.list();
        if (!Dm.truthy(processed))
            processed = Dm.list();
        else if (Dm.@in(this, processed))
            return output;

        if (!Dm.truthy(source))
            Dm.append(ref output, this);

        Dm.append(ref processed, this);
        foreach (atom atom_orbiter in Dm.each_cast<atom>(orbiters?.orbiter_list))
            Dm.append(ref output, atom_orbiter.get_all_orbiters(processed, source: FALSE));
        return output;
    }
}

// tg: code/game/atom/atom_orbit.dm:31
public partial class mob : atom_movable
{
    // tg: code/game/atom/atom_orbit.dm:31
    public override object get_all_orbiters(DmList processed = null, float? source = null, float? ignore_stealthed_admins = null)
    {
        source ??= TRUE;
        ignore_stealthed_admins ??= TRUE;
        if (!Dm.truthy(source) && Dm.truthy(ignore_stealthed_admins) && Dm.truthy(client?.holder?.fakekey))
            return Dm.list();
        return base.get_all_orbiters(processed, source, ignore_stealthed_admins);
    }
}
