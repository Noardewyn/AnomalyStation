// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/overlays.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the /atom overlay procs build_appearance_list, cut_overlays, cut_overlay and add_overlay (:40-86) and
// the two appearance helpers they call (:28-38) translated (package B, wave 4: /turf/Initialize, /atom/update_icon and
// /atom/movable/on_changed_z_level call them). SSoverlays itself (:1-14) is not declared (a live subsystem would change
// the boot; its stats are profiling, rules 13); copy_overlays, the /image procs and the debug procs (:88-212) come with
// the presentation work (M3).

public static partial class GlobalProcs
{
    // M3 R4 (the station's gravity generator adds an icon state as an overlay): BYOND's read of an image's appearance var is
    // the image's interned snapshot (DmAppearance.Of, as the engine's overlay lists convert an image).
    // tg: code/controllers/subsystem/overlays.dm:28
    public static object iconstate2appearance(object icon = null, object iconstate = null)
    {
        stringbro ??= Dm.@new<image>();
        stringbro.icon = icon;
        stringbro.icon_state = (string)iconstate;
        return DmAppearance.Of(stringbro);
    }

    // tg: code/controllers/subsystem/overlays.dm:29  var/static/image/stringbro = new()
    private static ref image stringbro => ref TgState.Current.GlobalProcs__iconstate2appearance__stringbro;

    // tg: code/controllers/subsystem/overlays.dm:34
    public static object icon2appearance(object icon = null)
    {
        iconbro ??= Dm.@new<image>();
        iconbro.icon = icon;
        return DmAppearance.Of(iconbro);
    }

    // tg: code/controllers/subsystem/overlays.dm:35  var/static/image/iconbro = new()
    private static ref image iconbro => ref TgState.Current.GlobalProcs__icon2appearance__iconbro;
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/overlays.dm:29  /proc/iconstate2appearance: var/static/image/stringbro = new() (created at the first call)
    internal image GlobalProcs__iconstate2appearance__stringbro;

    // tg: code/controllers/subsystem/overlays.dm:35  /proc/icon2appearance: var/static/image/iconbro = new() (created at the first call)
    internal image GlobalProcs__icon2appearance__iconbro;
}

// tg: code/controllers/subsystem/overlays.dm:40
public partial class atom : datum
{
    // tg: code/controllers/subsystem/overlays.dm:40
    public virtual object build_appearance_list(object build_overlays = null)
    {
        if (!islist(build_overlays))
            build_overlays = Dm.list(build_overlays);
        foreach (object overlay in Dm.each(build_overlays))
        {
            if (!Dm.truthy(overlay))
            {
                Dm.remove(ref build_overlays, overlay);
                continue;
            }
            if (istext(overlay))
            {
                // This is too expensive to run normally but running it during CI is a good test
                if (Dm.truthy(FALSE)) // PERFORM_ALL_TESTS(focus_only/invalid_overlays) is FALSE in production (rules 1.7)
                {
                    if (!Dm.truthy(icon_exists(icon, overlay)))
                    {
                        string icon_file = Dm.truthy(Dm.str(icon)) ? Dm.str(icon) : "Unknown Generated Icon";
                        stack_trace($"Invalid overlay: Icon object '{icon_file}' {REF(icon)} used in '{Dm.str(this)}' {type} is missing icon state {overlay}.", "code/controllers/subsystem/overlays.dm", 51);
                        continue;
                    }
                }

                float index = ((DmList)build_overlays).Find(overlay);
                ((DmList)build_overlays)[index] = iconstate2appearance(icon, overlay);
            }
            else if (isicon(overlay))
            {
                float index = ((DmList)build_overlays).Find(overlay);
                ((DmList)build_overlays)[index] = icon2appearance(overlay);
            }
        }
        return build_overlays;
    }

    // tg: code/controllers/subsystem/overlays.dm:62
    public virtual void cut_overlays()
    {
        // STAT_START_STOPWATCH (__DEFINES/stat_tracking.dm:6): profiling, not translated (rules 13)
        overlays = null;
        // POST_OVERLAY_CHANGE(src) (__DEFINES/overlays.dm:17), expanded inline (rules 11.1 (d): it reads src's alternate_appearances)
        if (Dm.truthy(alternate_appearances))
            foreach (object I in Dm.each(alternate_appearances.Copy())) // POST_OVERLAY_CHANGE (M8 slice G: the alternate appearances are translated)
            {
                datum_atom_hud_alternate_appearance AA = alternate_appearances[I] as datum_atom_hud_alternate_appearance;
                if (Dm.truthy(AA.transfer_overlays))
                    AA.copy_overlays(this);
            }
        // STAT_STOP_STOPWATCH, STAT_LOG_ENTRY(SSoverlays.stats, type) (__DEFINES/stat_tracking.dm:7-8): profiling into
        // SSoverlays, which is not declared (rules 13)
        Dm.Deferred("code/controllers/subsystem/overlays.dm:67", "cut_overlays: STAT_LOG_ENTRY(SSoverlays.stats, type), overlay profiling (rules 13)");
    }

    // tg: code/controllers/subsystem/overlays.dm:69
    // (remove_overlays is object, not tg's declared list: callers pass one overlay as often as a list, which
    // build_appearance_list wraps, :41-42; rules 3.5, a "-" cell; decided slot parameter, reported to the lead)
    public virtual void cut_overlay(object remove_overlays = null)
    {
        if (!Dm.truthy(overlays))
            return;
        // STAT_START_STOPWATCH (__DEFINES/stat_tracking.dm:6): profiling, not translated (rules 13)
        Dm.remove(overlays, build_appearance_list(remove_overlays));
        // POST_OVERLAY_CHANGE(src) (__DEFINES/overlays.dm:17), expanded inline (rules 11.1 (d))
        if (Dm.truthy(alternate_appearances))
            foreach (object I in Dm.each(alternate_appearances.Copy())) // POST_OVERLAY_CHANGE (M8 slice G: the alternate appearances are translated)
            {
                datum_atom_hud_alternate_appearance AA = alternate_appearances[I] as datum_atom_hud_alternate_appearance;
                if (Dm.truthy(AA.transfer_overlays))
                    AA.copy_overlays(this);
            }
        // STAT_STOP_STOPWATCH, STAT_LOG_ENTRY(SSoverlays.stats, type): profiling into SSoverlays (rules 13)
        Dm.Deferred("code/controllers/subsystem/overlays.dm:76", "cut_overlay: STAT_LOG_ENTRY(SSoverlays.stats, type), overlay profiling (rules 13)");
    }

    // tg: code/controllers/subsystem/overlays.dm:78
    // (add_overlays is object, as cut_overlay's remove_overlays; decided slot parameter, reported to the lead)
    public virtual void add_overlay(object add_overlays = null)
    {
        if (!Dm.truthy(overlays))
            return;
        // STAT_START_STOPWATCH (__DEFINES/stat_tracking.dm:6): profiling, not translated (rules 13)
        Dm.append(overlays, build_appearance_list(add_overlays));
        // VALIDATE_OVERLAY_LIMIT(src) (__DEFINES/overlays.dm:6), expanded inline (rules 11.1 (c), (f))
        if (length(this.overlays) >= MAX_ATOM_OVERLAYS)
            Dm.NotTranslated("code/__DEFINES/overlays.dm:7", "VALIDATE_OVERLAY_LIMIT: overlays2text and the error overlay (a /mutable_appearance, D2) are translated with the presentation work (M3)");
        // POST_OVERLAY_CHANGE(src) (__DEFINES/overlays.dm:17), expanded inline (rules 11.1 (d))
        if (Dm.truthy(alternate_appearances))
            foreach (object I in Dm.each(alternate_appearances.Copy())) // POST_OVERLAY_CHANGE (M8 slice G: the alternate appearances are translated)
            {
                datum_atom_hud_alternate_appearance AA = alternate_appearances[I] as datum_atom_hud_alternate_appearance;
                if (Dm.truthy(AA.transfer_overlays))
                    AA.copy_overlays(this);
            }
        // STAT_STOP_STOPWATCH, STAT_LOG_ENTRY(SSoverlays.stats, type): profiling into SSoverlays (rules 13)
        Dm.Deferred("code/controllers/subsystem/overlays.dm:86", "add_overlay: STAT_LOG_ENTRY(SSoverlays.stats, type), overlay profiling (rules 13)");
    }
}

// Gaps package C1 (the copy mimic).
public partial class atom
{
    // tg: code/controllers/subsystem/overlays.dm:88
    public virtual void copy_overlays(atom other = null, object cut_old = null) //copys our_overlays from another atom
    {
        if (other == null)
        {
            if (Dm.truthy(cut_old))
                cut_overlays();
            return;
        }

        DmList cached_other = other.overlays.Copy();
        if (Dm.truthy(cut_old))
        {
            if (Dm.truthy(cached_other))
                overlays = cached_other;
            else
                overlays = null;
            // VALIDATE_OVERLAY_LIMIT, POST_OVERLAY_CHANGE, STAT_*: the overlay limit's debug check and profiling (rules 13)
        }
        else if (Dm.truthy(cached_other))
            Dm.append(overlays, cached_other);
    }

    // tg: code/controllers/subsystem/overlays.dm:103  (silicons SI4: tg's `other` may be any appearance, an /image or a mutable
    // appearance, as the holopads' holograms copy an AI's hologram appearance or a recorded caller's image; BYOND reads its overlays
    // all the same)
    public virtual void copy_overlays(IDmAppearance other, object cut_old = null)
    {
        if (other == null)
        {
            if (Dm.truthy(cut_old))
                cut_overlays();
            return;
        }

        DmList cached_other = other.overlays.Copy();
        if (Dm.truthy(cut_old))
        {
            if (Dm.truthy(cached_other))
                overlays = cached_other;
            else
                overlays = null;
        }
        else if (Dm.truthy(cached_other))
            Dm.append(overlays, cached_other);
    }
}

// Syndicate G3c (D316): tg's /image/proc/copy_overlays (the cardboard cutout's disguise copies the cutout's overlays); the port's
// /image is the DmShim's, so the proc is an extension of it.
public static partial class GlobalProcs
{
    // tg: code/controllers/subsystem/overlays.dm:122
    public static void copy_overlays(this image self, atom other = null, object cut_old = null)
    {
        if (other == null)
        {
            if (Dm.truthy(cut_old))
                self.overlays.Cut();
            return;
        }

        DmList cached_other = other.overlays.Copy();
        if (Dm.truthy(cached_other))
        {
            if (Dm.truthy(cut_old) || self.overlays.len == 0)
                self.overlays = cached_other;
            else
                Dm.combine(ref self.overlays, cached_other);
        }
        else if (Dm.truthy(cut_old))
            self.overlays.Cut();
    }
}
