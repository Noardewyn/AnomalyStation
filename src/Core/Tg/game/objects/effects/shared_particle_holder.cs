// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/shared_particle_holder.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2, D326: a damaged cyborg's smoke and sparks). The holders sit in vis_contents as in tg; the client
// does not draw BYOND particles yet, so they carry no visible image.

// tg: code/game/objects/effects/shared_particle_holder.dm:4
public partial class datum_controller_global_vars
{
    // Assoc list of particle type/key -> list(list of particle holders, number of particle users)
    // tg: code/game/objects/effects/shared_particle_holder.dm:4  GLOBAL_LIST_EMPTY(shared_particles)
    public DmList shared_particles;

    // tg: code/game/objects/effects/shared_particle_holder.dm:4  (GLOBAL_MANAGED(shared_particles, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalshared_particles()
    {
        shared_particles = Dm.list();
        Dm.append(ref gvars_datum_init_order, "shared_particles");
    }
}

//A more abstract version of particle holder not bound to a specific object
// tg: code/game/objects/effects/shared_particle_holder.dm:7
public partial class obj_effect_abstract_shared_particle_holder : obj_effect_abstract
{
    // tg: code/game/objects/effects/shared_particle_holder.dm:19
    [DmParams("mapload", "particle_path", "particle_flags")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Type particle_path = Dm.arg(__extra, 0) as Type ?? typeof(particles_smoke);
        object particle_flags = Dm.arg(__extra, 1) ?? NONE;
        object dot = base.Initialize(mapload, __extra);
        // Shouldn't exist outside of nullspace
        loc = null;
        this.particle_flags = Dm.num(particle_flags);
        particles = Dm.@new(particle_path);
        return dot;
    }

    // tg: code/game/objects/effects/shared_particle_holder.dm:26
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref particles);
        return base.Destroy(force);
    }
}

// tg: code/game/objects/effects/shared_particle_holder.dm:47
public partial class atom
{
    private const int SHARED_PARTICLE_HOLDER_INDEX = 1;
    private const int SHARED_PARTICLE_USER_NUM_INDEX = 2;

    /**
     * Adds (or creates and adds) a shared particle holder
     *
     * Shared particle holders are held in nullspace and added to vis_contents of all atoms using it
     * in order to save clientside performance by making clients only render 3-5 particle holders
     * for 400 objects using them. This should be prioritized over normal particles when possible if it is known
     * that there will be a lot of objects using certain particles.
     *
     * Args
     * * particle_type - the type of particle to add
     * * custom_key - can be used to create a new pool of already existing particle type in case you're planning to edit holder's color or properties
     * * particle_flags - flags to pass to the particle holder. Not included in the key automatically, so you need to provide a custom key in that case
     * * pool_size - controls how many particle holders per type are created. Any objects over this cap will pick an existing holder from the pool.
     *
     * Now, this code seems fucked up, that's because this is meant to support both objects (and mobs) and turfs, *however* areas are special
     * and don't have vis_contents, so to avoid copypaste code we do this weirdness
     */
    // tg: code/game/objects/effects/shared_particle_holder.dm:47
    public virtual object add_shared_particles(object particle_type = null, object custom_key = null, float? particle_flags = null, float? pool_size = null)
    {
        particle_flags ??= NONE;
        pool_size ??= 3;
        if (Dm.truthy(particle_flags) && custom_key == null)
        {
            // nb: particle flags are not included in the key automatically to make remove_shared_particles easier to use...
            // however, if you set particle flags without a custom key, there's a good chance you accidentally just made every source of that particle use those flags
            throw Dm.CRASH(Dm.text("add_shared_particles was called with flags, but without a key - you must provide a custom key if you want to use flags! (atom: [] particle: [])", GetType(), particle_type), "code/game/objects/effects/shared_particle_holder.dm", 51);
        }

        atom play_pretend = this;
        bool is_floor_plane = Dm.eq(PLANE_TO_TRUE(plane), FLOOR_PLANE);
        string particle_key = Dm.text("[][]", Dm.truthy(custom_key) ? custom_key : particle_type, is_floor_plane ? "-floor" : "");
        if (!Dm.truthy(GLOB.shared_particles[particle_key]))
        {
            obj_effect_abstract_shared_particle_holder new_holder = Dm.@new<obj_effect_abstract_shared_particle_holder>(null, particle_type, particle_flags);
            if (is_floor_plane) // Keeps us off the floor plane, we'll just sit on game plane
                new_holder.vis_flags = Dm.bitand(new_holder.vis_flags, Dm.bitnot(VIS_INHERIT_PLANE));
            GLOB.shared_particles[particle_key] = Dm.list(Dm.list(new_holder), 1);
            Dm.append(play_pretend.vis_contents, ((GLOB.shared_particles[particle_key] as DmList)[SHARED_PARTICLE_HOLDER_INDEX] as DmList)[1]);
            return ((GLOB.shared_particles[particle_key] as DmList)[SHARED_PARTICLE_HOLDER_INDEX] as DmList)[1];
        }

        DmList entry = GLOB.shared_particles[particle_key] as DmList;
        DmList type_holders = entry[SHARED_PARTICLE_HOLDER_INDEX] as DmList;
        foreach (obj_effect_abstract_shared_particle_holder particle_holder in Dm.each_cast<obj_effect_abstract_shared_particle_holder>(type_holders))
        {
            if (Dm.@in(particle_holder, play_pretend.vis_contents))
                return particle_holder;
        }

        if (length(type_holders) < pool_size)
        {
            obj_effect_abstract_shared_particle_holder new_holder = Dm.@new<obj_effect_abstract_shared_particle_holder>(null, particle_type, particle_flags);
            if (is_floor_plane) // See above, we don't want to be on floor
                new_holder.vis_flags = Dm.bitand(new_holder.vis_flags, Dm.bitnot(VIS_INHERIT_PLANE));
            Dm.append(ref type_holders, new_holder);
            Dm.append(play_pretend.vis_contents, new_holder);
            entry[SHARED_PARTICLE_USER_NUM_INDEX] = Dm.num(entry[SHARED_PARTICLE_USER_NUM_INDEX]) + 1;
            return new_holder;
        }

        obj_effect_abstract_shared_particle_holder picked_holder = pick(type_holders) as obj_effect_abstract_shared_particle_holder;
        Dm.append(play_pretend.vis_contents, picked_holder);
        entry[SHARED_PARTICLE_USER_NUM_INDEX] = Dm.num(entry[SHARED_PARTICLE_USER_NUM_INDEX]) + 1;
        return picked_holder;
    }

    /* Removes shared particles from object's vis_contents and disposes of it if nothing uses that type/key of particle
     * particle_key can be either a type (if no custom_key was passed) or said custom_key
     */
    // tg: code/game/objects/effects/shared_particle_holder.dm:90
    public virtual void remove_shared_particles(object particle_key = null, float? delete_on_empty = null)
    {
        delete_on_empty ??= TRUE;
        if (!Dm.truthy(particle_key))
            return;

        if (ispath(particle_key))
            particle_key = Dm.text("[]", particle_key);

        if (Dm.eq(PLANE_TO_TRUE(plane), FLOOR_PLANE))
            particle_key = Dm.text("[]-floor", particle_key); // nb: unfortunately if the plane changes between the particles being removed, this will break.

        if (!Dm.truthy(GLOB.shared_particles[particle_key]))
            return;

        atom play_pretend = this;
        DmList entry = GLOB.shared_particles[particle_key] as DmList;
        DmList type_holders = entry[SHARED_PARTICLE_HOLDER_INDEX] as DmList;
        foreach (obj_effect_abstract_shared_particle_holder particle_holder in Dm.each_cast<obj_effect_abstract_shared_particle_holder>(type_holders))
        {
            if (!Dm.@in(particle_holder, play_pretend.vis_contents))
                continue;

            Dm.remove(play_pretend.vis_contents, particle_holder);
            entry[SHARED_PARTICLE_USER_NUM_INDEX] = Dm.num(entry[SHARED_PARTICLE_USER_NUM_INDEX]) - 1;

            if (Dm.truthy(delete_on_empty) && Dm.num(entry[SHARED_PARTICLE_USER_NUM_INDEX]) <= 0)
            {
                QDEL_LIST(type_holders);
                Dm.remove(ref GLOB.shared_particles, particle_key);
            }
            return;
        }
    }
}

// tg: code/game/objects/effects/shared_particle_holder.dm:91
public partial class area
{
    // tg: code/game/objects/effects/shared_particle_holder.dm:84
    public override object add_shared_particles(object particle_type = null, object custom_key = null, float? particle_flags = null, float? pool_size = null)
    {
        throw Dm.CRASH(Dm.text("add_shared_particles was called on an area [] ([]) trying to add []! Only turfs and movables support shared particles.", this, GetType(), particle_type), "code/game/objects/effects/shared_particle_holder.dm", 92);
    }

    // tg: code/game/objects/effects/shared_particle_holder.dm:117
    public override void remove_shared_particles(object particle_key = null, float? delete_on_empty = null)
    {
        throw Dm.CRASH(Dm.text("remove_shared_particles was called on an area [] ([]) trying to add []! Only turfs and movables support shared particles.", this, GetType(), particle_key), "code/game/objects/effects/shared_particle_holder.dm", 121);
    }
}
