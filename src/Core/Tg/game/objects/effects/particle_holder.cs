// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/particle_holder.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file (the Spinesthetics chip's smoking head). The holder sits in its parent's vis_contents as in tg; the
// client does not draw BYOND particles yet, so it carries no visible image (as the shared particle holders). The var block is
// generated (D111).

///objects can only have one particle on them at a time, so we use these abstract effects to hold and display the effects. You know, so multiple particle effects can exist at once.
///also because some objects do not display particles due to how their visuals are built
// tg: code/game/objects/effects/particle_holder.dm:3
[DmPath("/obj/effect/abstract/particle_holder")]
public partial class obj_effect_abstract_particle_holder : obj_effect_abstract
{
    // tg: code/game/objects/effects/particle_holder.dm:17
    [DmParams("mapload", "particle_path", "particle_flags")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Type particle_path = Dm.arg(__extra, 0) as Type ?? typeof(particles_smoke);
        object particle_flags = Dm.arg(__extra, 1) ?? NONE;
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(loc))
        {
            stack_trace("particle holder was created with no loc!", "code/game/objects/effects/particle_holder.dm", 20);
            return INITIALIZE_HINT_QDEL;
        }

        if (Dm.eq(PLANE_TO_TRUE(loc.plane), FLOOR_PLANE))
            vis_flags = Dm.bitand(vis_flags, Dm.bitnot(VIS_INHERIT_PLANE)); // don't yoink the floor plane. we'll just sit on game plane, it's fine

        // We nullspace ourselves because some objects use their contents (e.g. storage) and some items may drop everything in their contents on deconstruct.
        parent = loc;
        loc = null;

        // Mouse opacity can get set to opaque by some objects when placed into the object's contents (storage containers).
        mouse_opacity = MOUSE_OPACITY_TRANSPARENT;
        this.particle_flags = Dm.num(particle_flags);
        particles = Dm.@new(particle_path);
        // /atom doesn't have vis_contents, /turf and /atom/movable do
        atom lie_about_areas = parent;
        Dm.append(lie_about_areas.vis_contents, this);
        RegisterSignal(parent, COMSIG_QDELETING, nameof(parent_deleted));

        if (Dm.truthy(Dm.bitand(particle_flags, PARTICLE_ATTACH_MOB)))
            RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(on_move));
        on_move(parent as atom_movable, null, NORTH);
        return dot;
    }

    // tg: code/game/objects/effects/particle_holder.dm:43
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref particles);
        parent = null;
        return base.Destroy(force);
    }

    /// Non movables don't delete contents on destroy, so we gotta do this
    // tg: code/game/objects/effects/particle_holder.dm:49
    [SignalHandler]
    public virtual void parent_deleted(datum source = null)
    {
        qdel(this);
    }

    /// signal called when a parent that's been hooked into this moves
    /// does a variety of checks to ensure overrides work out properly
    // tg: code/game/objects/effects/particle_holder.dm:55
    [SignalHandler]
    public virtual void on_move(atom_movable attached = null, atom oldloc = null, object direction = null)
    {
        if (!Dm.truthy(Dm.bitand(particle_flags, PARTICLE_ATTACH_MOB)))
            return;

        //remove old
        if (ismob(oldloc))
        {
            mob particle_mob = oldloc as mob;
            Dm.remove(particle_mob.vis_contents, this);
        }

        // If we're sitting in a mob, we want to emit from it too, for vibes and shit
        if (ismob(attached.loc))
        {
            mob particle_mob = attached.loc as mob;
            Dm.append(particle_mob.vis_contents, this);
        }
    }

    /// Sets the particles position to the passed coordinates
    // tg: code/game/objects/effects/particle_holder.dm:72
    public virtual void set_particle_position(float? x = null, float? y = null, float? z = null)
    {
        x ??= 0;
        y ??= 0;
        z ??= 0;
        ((global::Anomaly.Core.Tg.particles)particles).position = Dm.list(x, y, z);
    }
}
