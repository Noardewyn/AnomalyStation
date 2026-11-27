// Engine ("BYOND builtin") part of BYOND's /particles (docs/ai/translation-rules.md 1.1, 2.3; docs/ai/specs/object-model.md
// sections 2 and 9). Not translated from a tg file: BYOND declares /particles (a child of /datum in the DM type tree,
// TypeOrder.g.cs); tg subtypes it in code/game/objects/effects/particles/ and adds procs to it
// (code/modules/admin/view_variables/particle_editor.dm).
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tg;

// Written by the M2 contracts step because the /obj/item var block names /particles/smoke/burning/small (burning_particles,
// items.dm:7). The MetaStation leftovers (protocol version 99, DV687): BYOND's vars of a particle set, which tg's /particles
// subtypes set and the appearance replicator sends with an atom's particles for the client to draw (BYOND ref /particles). A
// generator value (generator()) is not made (the converter defers it), so a set whose var is one draws that var's default.
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

    /// <summary>BYOND <c>width</c>: the size of the particles' image in pixels, around the atom's centre (100 by default).</summary>
    public object width = 100;

    /// <summary>BYOND <c>height</c> (100 by default).</summary>
    public object height = 100;

    /// <summary>BYOND <c>count</c>: the most particles alive at once (100 by default).</summary>
    public object count = 100;

    /// <summary>BYOND <c>bound1</c>: the lower corner (a vector) past which a particle dies.</summary>
    public object bound1;

    /// <summary>BYOND <c>bound2</c>: the upper corner.</summary>
    public object bound2;

    /// <summary>BYOND <c>gradient</c>: the colour gradient a particle's colour runs along (a list).</summary>
    public object gradient;

    /// <summary>BYOND <c>transform</c>: the particles' transform.</summary>
    public object transform;

    /// <summary>BYOND <c>lifespan</c>: how long a particle lives, in ticks.</summary>
    public object lifespan;

    /// <summary>BYOND <c>fade</c>: the ticks a particle fades out over at the end of its life.</summary>
    public object fade;

    /// <summary>BYOND <c>fadein</c>: the ticks a particle fades in over at its start.</summary>
    public object fadein;

    /// <summary>BYOND <c>icon</c>: the particles' icon.</summary>
    public object icon;

    /// <summary>BYOND <c>icon_state</c>: the icon state, or a weighted list of states a particle picks from.</summary>
    public object icon_state;

    /// <summary>BYOND <c>color_change</c>: how fast a particle's colour moves along the gradient.</summary>
    public object color_change;

    /// <summary>BYOND <c>velocity</c>: a particle's starting velocity in pixels a tick (a vector).</summary>
    public object velocity;

    /// <summary>BYOND <c>scale</c>: a particle's starting scale (a number or a vector).</summary>
    public object scale;

    /// <summary>BYOND <c>grow</c>: the scale a particle gains each tick (a number or a vector).</summary>
    public object grow;

    /// <summary>BYOND <c>rotation</c>: a particle's starting rotation in degrees.</summary>
    public object rotation;

    /// <summary>BYOND <c>spin</c>: the degrees a particle turns each tick.</summary>
    public object spin;

    /// <summary>BYOND <c>friction</c>: the share of its velocity a particle loses each tick (0 to 1).</summary>
    public object friction;

    /// <summary>BYOND <c>drift</c>: the random velocity a particle gains each tick (a vector).</summary>
    public object drift;
}
