// Translated from tgstation (snapshot 1d99bd7f): code/modules/lighting/lighting_corner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 lumcount: the whole file but the debug display (display, /datum/lighting_corner/dummy/display, display_corners:
// docs/ai/specs/not-translated.md section 42). LIGHTING_SOFT_THRESHOLD is 0, so update_objects takes the #else branch (:142-146).

// Because we can control each corner of every lighting object.
// And corners get shared between multiple turfs (unless you're on the corners of the map, then 1 corner doesn't).
// For the record: these should never ever ever be deleted, even if the turf doesn't have dynamic lighting.

// tg: code/modules/lighting/lighting_corner.dm:5
[DmPath("/datum/lighting_corner")]
public partial class datum_lighting_corner : datum
{
    // Takes as an argument the coords to use as the bottom left (south west) of our corner
    // tg: code/modules/lighting/lighting_corner.dm:34
    [DmParams("x", "y", "z")]
    public override async DmTask New(params object[] __extra)
    {
        float x = Dm.num(Dm.arg<object>(__extra, 0));
        float y = Dm.num(Dm.arg<object>(__extra, 1));
        float z = Dm.num(Dm.arg<object>(__extra, 2));
        await base.New();

        this.x = x + 0.5f;
        this.y = y + 0.5f;
        this.z = z;

        // Alright. We're gonna take a set of coords, and from them do a loop clockwise
        // To build out the turfs adjacent to us. This is pretty fast
        turf process_next = locate(x, y, z);
        if (process_next != null)
        {
            master_SW = process_next;
            process_next.lighting_corner_NE = this;
            // Now, we go north!
            process_next = get_step(process_next, NORTH);
        }
        else
            // Yes this is slightly slower then having a guarenteeed turf, but there aren't many null turfs
            // So this is pretty damn fast
            process_next = locate(x, y + 1, z);

        // Ok, if we have a north turf, go there. otherwise, onto the next
        if (process_next != null)
        {
            master_NW = process_next;
            process_next.lighting_corner_SE = this;
            // Now, TO THE EAST
            process_next = get_step(process_next, EAST);
        }
        else
            process_next = locate(x + 1, y + 1, z);

        // Etc etc
        if (process_next != null)
        {
            master_NE = process_next;
            process_next.lighting_corner_SW = this;
            // Now, TO THE SOUTH AGAIN (SE)
            process_next = get_step(process_next, SOUTH);
        }
        else
            process_next = locate(x + 1, y, z);

        // anddd the last tile
        if (process_next != null)
        {
            master_SE = process_next;
            process_next.lighting_corner_NW = this;
        }
    }

    // tg: code/modules/lighting/lighting_corner.dm:83
    public virtual void self_destruct_if_idle()
    {
        if (!Dm.truthy(LAZYLEN(affecting)))
            qdel(this, force: TRUE);
    }

    // tg: code/modules/lighting/lighting_corner.dm:87
    public virtual void vis_update()
    {
        foreach (datum_light_source light_source in Dm.each<datum_light_source>(affecting))
            light_source.vis_update();
    }

    // tg: code/modules/lighting/lighting_corner.dm:91
    public virtual void full_update()
    {
        foreach (datum_light_source light_source in Dm.each<datum_light_source>(affecting))
            light_source.recalc_corner(this);
    }

    // God that was a mess, now to do the rest of the corner code! Hooray!
    // tg: code/modules/lighting/lighting_corner.dm:96
    public virtual void update_lumcount(object delta_r = null, object delta_g = null, object delta_b = null)
    {
        float dr = Dm.num(delta_r), dg = Dm.num(delta_g), db = Dm.num(delta_b);
        // tg: :98-104  the VISUALIZE_LIGHT_UPDATES branch is a debug define, off
        if (!(dr != 0 || dg != 0 || db != 0)) // 0 is falsey ok
            return;

        lum_r += dr;
        lum_g += dg;
        lum_b += db;

        if (!Dm.truthy(needs_update))
        {
            needs_update = TRUE;
            Dm.append(ref datum_controller_subsystem_lighting.corners_queue, this);
        }
    }

    // tg: code/modules/lighting/lighting_corner.dm:114
    public virtual object update_objects()
    {
        // Cache these values ahead of time so 4 individual lighting objects don't all calculate them individually.
        float lum_r = this.lum_r;
        float lum_g = this.lum_g;
        float lum_b = this.lum_b;
        float largest_color_luminosity = max(lum_r, lum_g, lum_b); // Scale it so one of them is the strongest lum, if it is above 1.
        float dot = 1; // factor
        if (largest_color_luminosity > 1)
            dot = 1 / largest_color_luminosity;

        float old_r = cache_r;
        float old_g = cache_g;
        float old_b = cache_b;

        // #if LIGHTING_SOFT_THRESHOLD != 0 ... #else (LIGHTING_SOFT_THRESHOLD is 0)
        cache_r = round(lum_r * dot, LIGHTING_ROUND_VALUE);
        cache_g = round(lum_g * dot, LIGHTING_ROUND_VALUE);
        cache_b = round(lum_b * dot, LIGHTING_ROUND_VALUE);

        this.largest_color_luminosity = round(largest_color_luminosity, LIGHTING_ROUND_VALUE);
        if (old_r == cache_r && old_g == cache_g && old_b == cache_b)
            return dot;

        // tg: :156-174  the four masters' lighting objects queued on SSlighting.objects_queue for their visual update():
        // DEVIATION(DV85): no lighting objects exist on the server (the client draws the light, D24), so nothing is queued.

        self_destruct_if_idle();
        return dot;
    }

    // tg: code/modules/lighting/lighting_corner.dm:178
    [DmParams()]
    public override object Destroy(float? force = null)
    {
        if (!Dm.truthy(force))
            return QDEL_HINT_LETMELIVE;

        foreach (datum_light_source light_source in Dm.each<datum_light_source>(affecting))
            LAZYREMOVE(ref light_source.effect_str, this);
        affecting = null;

        if (master_NE != null)
        {
            master_NE.lighting_corner_SW = null;
            master_NE.lighting_corners_initialised = FALSE;
        }
        if (master_SE != null)
        {
            master_SE.lighting_corner_NW = null;
            master_SE.lighting_corners_initialised = FALSE;
        }
        if (master_SW != null)
        {
            master_SW.lighting_corner_NE = null;
            master_SW.lighting_corners_initialised = FALSE;
        }
        if (master_NW != null)
        {
            master_NW.lighting_corner_SE = null;
            master_NW.lighting_corners_initialised = FALSE;
        }
        if (Dm.truthy(needs_update))
            Dm.remove(ref datum_controller_subsystem_lighting.corners_queue, this);

        return base.Destroy(force);
    }

    // tg: code/modules/lighting/lighting_corner.dm:205-225  display, /datum/lighting_corner/dummy/display and /proc/display_corners:
    // debug overlays, not translated (docs/ai/specs/not-translated.md section 42).
}
