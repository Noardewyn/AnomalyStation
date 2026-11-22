// Engine ("BYOND builtin") part of BYOND's /particles (docs/ai/translation-rules.md 1.1, 2.3; docs/ai/specs/object-model.md
// sections 2 and 9). Not translated from a tg file: BYOND declares /particles (a child of /datum in the DM type tree,
// TypeOrder.g.cs); tg subtypes it in code/game/objects/effects/particles/ and adds procs to it
// (code/modules/admin/view_variables/particle_editor.dm).
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tg;

// SKELETON: completed with the presentation work that needs particle sets (M3 appearance or later). Written by the M2
// contracts step because the /obj/item var block names /particles/smoke/burning/small (burning_particles, items.dm:7);
// only the type is declared. Open for that work: BYOND's vars of /particles (width, height, count, spawning, icon,
// position, ...) and whether a particle set is a builtin value type without a world id (translation-rules.md 3.7).
[DmPath("/particles")]
public partial class particles : datum
{
    /// <summary>
    /// BYOND <c>/particles/var/color</c>: the particles' colour. Engineering E16: the portable gravity unit's field tile takes its
    /// particle type's colour (<c>color = particle_type::color</c>, fields/gravity.dm:96); the particles themselves are not drawn
    /// (particles are not ported).
    /// </summary>
    public object color;

    /// <summary>
    /// BYOND <c>/particles/var/spawning</c>: the particles spawned per tick. Gaps E2a: the particle weathers animate it with their
    /// severity (<c>/particles/weather/proc/animate_severity</c>, particle_weather.dm:151); the client draws the weather from the
    /// weather datum, not from this set.
    /// </summary>
    public object spawning = 1;

    /// <summary>BYOND <c>/particles/var/gravity</c>: the particles' acceleration (a vector list). Gaps E2a: a weather's wind (x).</summary>
    public object gravity;

    /// <summary>
    /// BYOND <c>/particles/var/position</c>: where the particles spawn (a vector list, or a generator). Gaps SL1: a particle holder's
    /// <c>set_particle_position</c> (particle_holder.dm:72), the Spinesthetics chip's smoking head; the particles are not drawn.
    /// </summary>
    public object position;
}
