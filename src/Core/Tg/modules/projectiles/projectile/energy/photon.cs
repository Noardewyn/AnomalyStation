// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/energy/photon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps GU1, D333): the photon cannon's bolt, a lit mini-sun that flashes and confuses whoever it passes through,
// slows as it flies and bursts into sparks and a flash at the end of its range. The var block is generated (D111).

// tg: code/modules/projectiles/projectile/energy/photon.dm:3
public partial class obj_projectile_energy_photon : obj_projectile_energy
{
    // tg: code/modules/projectiles/projectile/energy/photon.dm:1  #define MULTIPLY_SPEED 0.8 (file-local)
    private const float MULTIPLY_SPEED = 0.8f;

    // tg: code/modules/projectiles/projectile/energy/photon.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignals(this, Dm.list(COMSIG_MOVABLE_CROSS, COMSIG_MOVABLE_CROSS_OVER), nameof(blast_touched));
        RegisterSignal(this, COMSIG_ATOM_ENTERED, nameof(scorch_earth));
        Waiting.NoSleep(set_light_on(TRUE));
        return dot;
    }

    /**
     * Handle side effects for the phonon bolt.
     * behaves like a higher power direct flash if hit, and sparks silicons like they're getting microwaved.
     */
    // tg: code/modules/projectiles/projectile/energy/photon.dm:29
    [SignalHandler]
    public virtual void blast_touched(datum source = null, atom flashed = null)
    {
        if (isliving(flashed))
        {
            mob_living flashed_creature = flashed as mob_living;
            Waiting.NoSleep(flashed_creature.flash_act(intensity: 3, affect_silicon: TRUE, length: 6));
            Waiting.NoSleep(flashed_creature.adjust_confusion(1.5f * SECONDS));
        }
        if (issilicon(flashed))
            do_sparks(rand(1, 4), FALSE, this);
    }

    /**
     * When traveling to a new turf, throws a probability to generate a hotspot across its path.
     */
    // tg: code/modules/projectiles/projectile/energy/photon.dm:41
    [SignalHandler]
    public virtual void scorch_earth(turf_open_floor source = null, atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        if (prob(40))
            Dm.@new<obj_effect_hotspot>(arrived);
    }

    // tg: code/modules/projectiles/projectile/energy/photon.dm:46
    public override async DmTask<object> reduce_range()
    {
        object dot = await base.reduce_range();
        speed *= MULTIPLY_SPEED;
        return dot;
    }

    // tg: code/modules/projectiles/projectile/energy/photon.dm:50
    public override async DmTask on_range()
    {
        do_sparks(rand(4, 9), FALSE, this);
        playsound(loc, Dm.resource("sound/items/weapons/solarflare.ogg"), 100, FALSE, 8, 0.9f);
        foreach (mob_living flashed_mob in Dm.each<mob_living>(viewers(5, loc)))
            await flashed_mob.flash_act();
        await base.on_range();
    }
}
