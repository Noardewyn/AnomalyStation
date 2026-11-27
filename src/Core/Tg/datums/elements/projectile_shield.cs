// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/projectile_shield.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file.

///Bullet shield element, spawns an anti-toolbox shield when hit by a bullet.
// tg: code/datums/elements/projectile_shield.dm:2
public partial class datum_element_projectile_shield : datum_element
{
    // tg: code/datums/elements/projectile_shield.dm:2
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!ismob(target))
            return ELEMENT_INCOMPATIBLE;

        RegisterSignal(target, COMSIG_ATOM_BULLET_ACT, nameof(on_bullet_act));
        return dot;
    }

    // tg: code/datums/elements/projectile_shield.dm:9
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_ATOM_BULLET_ACT);
        return dot;
    }

    // tg: code/datums/elements/projectile_shield.dm:13
    [SignalHandler]
    public virtual void on_bullet_act(datum source = null, obj_projectile proj = null)
    {
        mob movable_mob = source as mob;
        turf current_turf = movable_mob.loc as turf;
        if (!isturf(current_turf))
            return;
        if (movable_mob.stat == DEAD)
            return;

        obj_effect_temp_visual_at_shield new_atshield = Dm.@new<obj_effect_temp_visual_at_shield>(current_turf, movable_mob);

        float random_x = rand(-32, 32);
        new_atshield.pixel_x += random_x;

        float random_y = rand(0, 72);
        new_atshield.pixel_y += random_y;
    }
}
