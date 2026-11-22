// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/vis_overlays.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice T3 (D164): the whole file, for the holosigns, whose picture is a vis_contents overlay of the sign (holosign.dm:create_vis_overlay).
// A shared overlay is one /obj/effect/overlay/vis in the cache, put in the vis_contents of every thing that shows it; the client draws an
// entity's vis_contents (the replica's VisChildren). The cogbar's overlay (cogbar.dm:60) is still behind its deferred `New` (DV13).

// tg: code/controllers/subsystem/vis_overlays.dm:1 SUBSYSTEM_DEF(vis_overlays)
[DmPath("/datum/controller/subsystem/vis_overlays")]
public partial class datum_controller_subsystem_vis_overlays : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/vis_overlays.dm:2
    public datum_controller_subsystem_vis_overlays()
    {
        name = "Vis contents overlays";
        wait = 1 * MINUTES;
        ss_flags = SS_NO_INIT;
        priority = FIRE_PRIORITY_VIS;
    }

    // tg: code/controllers/subsystem/vis_overlays.dm:7 (an object-creating default, rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        vis_overlay_cache = Dm.list();
    }

    // The New() that SUBSYSTEM_DEF(vis_overlays) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSvis_overlays) (__DEFINES/MC.dm:29)
    // expanded inline (it reads src).
    // tg: code/controllers/subsystem/vis_overlays.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSvis_overlays != this)
        {
            if (SSvis_overlays is datum_controller_subsystem_vis_overlays)
            {
                Recover();
                qdel(SSvis_overlays);
            }
            SSvis_overlays = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/vis_overlays.dm:7
    public DmList vis_overlay_cache;
    // tg: code/controllers/subsystem/vis_overlays.dm:8
    public DmList currentrun;

    // tg: code/controllers/subsystem/vis_overlays.dm:11
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        resumed ??= FALSE;
        if (!Dm.truthy(resumed))
            currentrun = vis_overlay_cache.Copy();
        DmList current_run = currentrun;

        while (Dm.truthy(current_run.len))
        {
            object key = current_run[current_run.len];
            obj_effect_overlay_vis overlay = current_run[key] as obj_effect_overlay_vis;
            current_run.len--;
            if (!Dm.truthy(overlay.unused) && !Dm.truthy(length(overlay.vis_locs)))
                overlay.unused = world.time;
            else if (Dm.truthy(overlay.unused) && overlay.unused + overlay.cache_expiration < world.time)
            {
                Dm.remove(vis_overlay_cache, key);
                qdel(overlay);
            }
            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                return;
        }
    }

    //the "thing" var can be anything with vis_contents which includes images - in the future someone should totally allow vis overlays to be passed in as an arg instead of all this bullshit
    // tg: code/controllers/subsystem/vis_overlays.dm:28
    public virtual object add_vis_overlay(atom_movable thing = null, object icon = null, object iconstate = null, object layer = null, object plane = null, object dir = null, float? alpha = null, float? add_appearance_flags = null, float? unique = null)
    {
        alpha ??= 255;
        add_appearance_flags ??= NONE;
        unique ??= FALSE;
        object dot = null;
        obj_effect_overlay_vis overlay;
        if (!Dm.truthy(unique))
        {
            dot = Dm.text("[]|[]|[]|[]|[]|[]|[]", icon, iconstate, layer, plane, dir, alpha, add_appearance_flags);
            overlay = vis_overlay_cache[dot] as obj_effect_overlay_vis;
            if (!Dm.truthy(overlay))
            {
                overlay = _create_new_vis_overlay(icon, iconstate, layer, plane, dir, alpha, add_appearance_flags);
                vis_overlay_cache[dot] = overlay;
            }
            else
                overlay.unused = 0;
        }
        else
        {
            overlay = _create_new_vis_overlay(icon, iconstate, layer, plane, dir, alpha, add_appearance_flags);
            overlay.cache_expiration = -1;
            string cache_id = Dm.text("[]@{[]}", text_ref(overlay), world.time);
            vis_overlay_cache[cache_id] = overlay;
            dot = overlay;
        }
        Dm.append(thing.vis_contents, overlay);

        if (!isatom(thing)) // Automatic rotation is not supported on non atoms
            return overlay;

        if (!Dm.truthy(thing.managed_vis_overlays))
            thing.managed_vis_overlays = Dm.list(overlay);
        else
            Dm.append(thing.managed_vis_overlays, overlay);
        return overlay;
    }

    // tg: code/controllers/subsystem/vis_overlays.dm:55
    public virtual obj_effect_overlay_vis _create_new_vis_overlay(object icon = null, object iconstate = null, object layer = null, object plane = null, object dir = null, object alpha = null, float? add_appearance_flags = null)
    {
        obj_effect_overlay_vis overlay = Dm.@new<obj_effect_overlay_vis>();
        overlay.icon = icon;
        overlay.icon_state = Dm.str(iconstate);
        overlay.layer = Dm.num(layer);
        overlay.plane = Dm.num(plane);
        overlay.dir = Dm.num(dir);
        overlay.alpha = Dm.num(alpha);
        overlay.appearance_flags = Dm.num(Dm.bitor(overlay.appearance_flags, add_appearance_flags));
        return overlay;
    }

    // tg: code/controllers/subsystem/vis_overlays.dm:67
    public virtual void remove_vis_overlay(atom_movable thing = null, DmList overlays = null)
    {
        // DM's `-=` on a null list or of a null list is a no-op (a thing with no managed overlays passes its null list)
        if (overlays != null)
            Dm.remove(thing.vis_contents, overlays);
        if (!isatom(thing))
            return;
        if (overlays != null && thing.managed_vis_overlays != null)
            Dm.remove(thing.managed_vis_overlays, overlays);
        if (!Dm.truthy(length(thing.managed_vis_overlays)))
            thing.managed_vis_overlays = null;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/vis_overlays.dm:1  SUBSYSTEM_DEF(vis_overlays): GLOBAL_REAL(SSvis_overlays, /datum/controller/subsystem/vis_overlays)
    internal datum_controller_subsystem_vis_overlays SSvis_overlays;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_vis_overlays SSvis_overlays => ref TgState.Current.SSvis_overlays;
}
