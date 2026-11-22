// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/unusual_effect.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N2b, D316: the lucky hat of the clown operatives' crate of hats glows in an outline and sheds pollen). The
// var block and the particles' are generated (D111).

/// Creates a cool looking effect on the movable.
/// In the future, this could be expanded to have more interesting particles and effects.
// tg: code/datums/components/unusual_effect.dm:21
public partial class datum_component_unusual_effect : datum_component
{
    // tg: code/datums/components/unusual_effect.dm:31
    [DmParams("color", "include_particles")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object color = Dm.arg(__extra, 0);
        object include_particles = Dm.arg(__extra, 1) ?? FALSE;
        var parent_movable = parent as atom_movable;
        if (parent_movable == null)
            return COMPONENT_INCOMPATIBLE;

        this.color = color;
        parent_movable.add_filter("unusual_effect", 2, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", color), Dm.kv("size", 2)));
        if (Dm.truthy(include_particles))
            special_effects = Dm.@new<obj_effect_abstract_particle_holder>(parent_movable, typeof(particles_unusual_effect));
        START_PROCESSING(SSobj, this);
        return null;
    }

    // tg: code/datums/components/unusual_effect.dm:42
    public override object Destroy(float? force = null)
    {
        var parent_movable = parent as atom_movable;
        if (parent_movable != null)
            parent_movable.remove_filter("unusual_effect");
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/datums/components/unusual_effect.dm:49
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        var parent_movable = parent as atom_movable;
        object filter = parent_movable.get_filter("unusual_effect");
        if (filter == null)
        {
            parent_movable.add_filter("unusual_effect", 2, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", color), Dm.kv("size", 2)));
            return null;
        }
        if (!(glow_cooldown < world.time)) // COOLDOWN_FINISHED(src, glow_cooldown)
            return null;

        Dm.animate(filter, Dm.named("alpha", 110), Dm.named("time", 1.5f * SECONDS), Dm.named("loop", -1));
        Dm.animate(Dm.named("alpha", 40), Dm.named("time", 2.5f * SECONDS));
        glow_cooldown = world.time + 4 * SECONDS; // COOLDOWN_START(src, glow_cooldown, 4 SECONDS)
        return null;
    }
}
