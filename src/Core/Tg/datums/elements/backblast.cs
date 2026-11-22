// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/backblast.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N2a, D316: the rocket launcher's backblast). The var block is generated (D111).

/**
 * When attached to a gun and the gun is successfully fired, this element creates a "backblast", like you'd find in a rocket launcher or recoilless rifle
 *
 * The backblast is simulated by a directional explosion 180 degrees from the direction of the fired projectile.
 */
// tg: code/datums/elements/backblast.dm:6
public partial class datum_element_backblast : datum_element
{
    // tg: code/datums/elements/backblast.dm:21
    [DmParams("target", "dev_range", "heavy_range", "light_range", "flame_range", "blast_angle")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        float dev_range = Dm.num(Dm.arg(__extra, 0) ?? 0);
        float heavy_range = Dm.num(Dm.arg(__extra, 1) ?? 0);
        float light_range = Dm.num(Dm.arg(__extra, 2) ?? 6);
        float flame_range = Dm.num(Dm.arg(__extra, 3) ?? 6);
        float blast_angle = Dm.num(Dm.arg(__extra, 4) ?? 60);
        object dot = await base.Attach(target, Dm.forward(__extra, dev_range, heavy_range, light_range, flame_range, blast_angle));
        if (!isgun(target) || dev_range < 0 || heavy_range < 0 || light_range < 0 || flame_range < 0 || blast_angle < 1)
            return ELEMENT_INCOMPATIBLE;

        this.dev_range = dev_range;
        this.heavy_range = heavy_range;
        this.light_range = light_range;
        this.flame_range = flame_range;
        this.blast_angle = blast_angle;

        RegisterSignal(target, COMSIG_GUN_FIRED, nameof(pew));
        return dot;
    }

    // tg: code/datums/elements/backblast.dm:34
    [DmParams("source")]
    public override object Detach(datum source = null, params object[] __extra)
    {
        if (source != null)
            UnregisterSignal(source, COMSIG_GUN_FIRED);
        return base.Detach(source, __extra);
    }

    /// For firing an actual backblast pellet
    // tg: code/datums/elements/backblast.dm:40
    [SignalHandler]
    public virtual void pew(obj_item_gun weapon = null, mob_living user = null, atom target = null)
    {
        if (HAS_TRAIT(user, TRAIT_PACIFISM))
            return;

        turf origin = get_turf(weapon);
        float backblast_angle = Dm.num(get_angle(target, origin));
        Waiting.Detach(explosion(weapon, devastation_range: Dm.num(dev_range), heavy_impact_range: Dm.num(heavy_range), light_impact_range: Dm.num(light_range),
            flame_range: flame_range, adminlog: FALSE, protect_epicenter: TRUE, explosion_direction: backblast_angle, explosion_arc: Dm.num(blast_angle)));
    }
}
