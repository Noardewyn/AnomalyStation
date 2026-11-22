// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/temporary_visuals/projectiles/projectile_effects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M7 projectiles slice (D165): the whole file, the tracer, muzzle and impact effects' base and the hitscan's light. The var
// blocks are generated (D111).

// tg: code/game/objects/effects/temporary_visuals/projectiles/projectile_effects.dm:1
public partial class obj_effect_projectile : obj_effect
{
    // tg: code/game/objects/effects/temporary_visuals/projectiles/projectile_effects.dm:10
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        return null;
    }

    // tg: code/game/objects/effects/temporary_visuals/projectiles/projectile_effects.dm:13
    public override async DmTask<object> singularity_act()
    {
        return null;
    }

    // tg: code/game/objects/effects/temporary_visuals/projectiles/projectile_effects.dm:16
    public virtual void scale_to(object nx = null, object ny = null, float? @override = null)
    {
        @override ??= TRUE;
        matrix M = null;
        if (!Dm.truthy(@override))
            M = transform as matrix;
        else
            M = Dm.matrix();
        M.Scale(Dm.num(nx), Dm.num(ny));
        transform = M;
    }

    // tg: code/game/objects/effects/temporary_visuals/projectiles/projectile_effects.dm:25
    public virtual void turn_to(object angle = null, float? @override = null)
    {
        @override ??= TRUE;
        matrix M = null;
        if (!Dm.truthy(@override))
            M = transform as matrix;
        else
            M = Dm.matrix();
        M.Turn(Dm.num(angle));
        transform = M;
    }

    // tg: code/game/objects/effects/temporary_visuals/projectiles/projectile_effects.dm:34
    [DmParams("angle_override", "p_x", "p_y", "color_override", "scaling")]
    public override async DmTask New(params object[] __extra)
    {
        object angle_override = Dm.arg(__extra, 0);
        object p_x = Dm.arg(__extra, 1);
        object p_y = Dm.arg(__extra, 2);
        object color_override = Dm.arg(__extra, 3);
        float? scaling = Dm.arg<float?>(__extra, 4);
        scaling ??= 1;
        if (Dm.truthy(angle_override) && Dm.truthy(p_x) && Dm.truthy(p_y) && Dm.truthy(color_override) && Dm.truthy(scaling))
            apply_vars(angle_override, Dm.num_or_null(p_x), Dm.num_or_null(p_y), color_override, scaling);
        await base.New(Dm.forward(__extra, angle_override, p_x, p_y, color_override, scaling));
    }

    // tg: code/game/objects/effects/temporary_visuals/projectiles/projectile_effects.dm:39
    public virtual void apply_vars(object angle_override = null, float? p_x = null, float? p_y = null, object color_override = null, float? scaling = null, float? increment = null)
    {
        p_x ??= 0;
        p_y ??= 0;
        scaling ??= 1;
        increment ??= 0;
        pixel_x = Dm.num(p_x);
        pixel_y = Dm.num(p_y);
        if (Dm.truthy(color_override))
            color = color_override;
        scale_to(1, scaling, FALSE);
        turn_to(angle_override, FALSE);
        for (float i = 1, i__end = Dm.num(increment); i <= i__end; i++)
        {
            pixel_x += round((sin(Dm.num(angle_override)) + 16 * sin(Dm.num(angle_override)) * 2), 1);
            pixel_y += round((cos(Dm.num(angle_override)) + 16 * cos(Dm.num(angle_override)) * 2), 1);
        }
    }
}

// tg: code/game/objects/effects/temporary_visuals/projectiles/projectile_effects.dm:50
public partial class obj_effect_abstract_projectile_lighting : obj_effect_abstract
{
    // tg: code/game/objects/effects/temporary_visuals/projectiles/projectile_effects.dm:53
    [DmParams("mapload", "color", "range", "intensity")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object color = Dm.arg(__extra, 0);
        object range = Dm.arg(__extra, 1);
        object intensity = Dm.arg(__extra, 2);
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(set_light(range, intensity, color));
        return dot;
    }
}
