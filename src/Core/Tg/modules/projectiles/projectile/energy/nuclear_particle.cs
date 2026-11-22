// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/energy/nuclear_particle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M7 supermatter slice (G5, D163): the whole file, the nuclear particle that the supermatter fires from BZ
// (supermatter_gas.dm:157). The var block is generated (D111).

//Nuclear particle projectile - a deadly side effect of fusion
// tg: code/modules/projectiles/projectile/energy/nuclear_particle.dm:2
public partial class obj_projectile_energy_nuclear_particle : obj_projectile_energy
{
    // tg: code/modules/projectiles/projectile/energy/nuclear_particle.dm:12  var/static/list/particle_colors (a type-level static, rules 10.6)
    public static ref DmList particle_colors => ref TgState.Current.obj_projectile_energy_nuclear_particle__particle_colors;

    // tg: code/modules/projectiles/projectile/energy/nuclear_particle.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        //Random color time!
        object our_color = pick(particle_colors);
        Waiting.NoSleep(add_atom_colour(particle_colors[our_color], FIXED_COLOUR_PRIORITY));
        Waiting.NoSleep(set_light(4, 3, particle_colors[our_color])); //Range of 4, brightness of 3 - Same range as a flashlight
        return dot;
    }

    // tg: code/modules/projectiles/projectile/energy/nuclear_particle.dm:28
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        if (ishuman(target))
            SSradiation.irradiate(target);

        return await base.on_hit(target, blocked, pierce_hit);
    }
}

public partial class atom : datum
{
    // tg: code/modules/projectiles/projectile/energy/nuclear_particle.dm:34
    public virtual async DmTask fire_nuclear_particle(object angle = null) //used by fusion to fire random nuclear particles. Fires one particle in a random direction.
    {
        angle ??= rand(0, 360);
        obj_projectile_energy_nuclear_particle P = Dm.@new<obj_projectile_energy_nuclear_particle>(this);
        await P.fire(angle);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/projectiles/projectile/energy/nuclear_particle.dm:12  var/static/list/particle_colors = list(...)
    internal DmList obj_projectile_energy_nuclear_particle__particle_colors = Dm.list(
        Dm.kv("red", COLOR_RED),
        Dm.kv("blue", COLOR_VIBRANT_LIME),
        Dm.kv("green", COLOR_BLUE),
        Dm.kv("yellow", COLOR_YELLOW),
        Dm.kv("cyan", COLOR_CYAN),
        Dm.kv("purple", COLOR_MAGENTA));
}
