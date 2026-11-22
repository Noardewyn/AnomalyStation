// Translated from tgstation (snapshot 1d99bd7f): code/datums/cogbar.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file as a seam (lead decision D4, DEVIATION DV13): the busy indicator is drawn by the 3D client from
// replicated state, so New is a deferred leaf and add_cog_to_user, which only New calls, is not translated. remove() and
// Destroy are translated: with no cog, remove() deletes the cogbar at once (tg's own path, cogbar.dm:84-86), so a
// timed action ends as in tg.

/**
 * ### Cogbar
 * Represents that the user is busy doing something.
 */
// tg: code/datums/cogbar.dm:7
[DmPath("/datum/cogbar")]
public partial class datum_cogbar : datum
{
    // tg: code/datums/cogbar.dm:1  #define COGBAR_ANIMATION_TIME (0.5 SECONDS) (file-local, #undef at :100)
    private const float COGBAR_ANIMATION_TIME = (0.5f * SECONDS);

    /// Who's doing the thing
    // tg: code/datums/cogbar.dm:9
    public mob user;
    /// The user client
    // tg: code/datums/cogbar.dm:11
    public client user_client;
    /// The visible element to other players
    // tg: code/datums/cogbar.dm:13
    public obj_effect_overlay_vis cog;
    /// The blank image that overlaps the cog - hides it from the source user
    // tg: code/datums/cogbar.dm:15
    public image blank;
    /// The offset of the icon
    // tg: code/datums/cogbar.dm:17
    public object offset_y;
    /// Icon path of the cog
    // tg: code/datums/cogbar.dm:19
    public object cogicon;
    /// The icon state
    // tg: code/datums/cogbar.dm:21
    public object cogiconstate;

    // tg: code/datums/cogbar.dm:24
    [DmParams("user", "cogicon", "cogiconstate")]
    public override async DmTask New(params object[] __extra)
    {
        // DEVIATION(DV13): the cog that tg shows above a busy mob (SSvis_overlays, image, animate) is drawn by the 3D client
        // from replicated state; the cogbar keeps no user, so remove() and Destroy take tg's no-cog path.
        Dm.Deferred("code/datums/cogbar.dm:24", "New: the busy cog is drawn by the 3D client from replicated state (DV13)");
    }

    // tg: code/datums/cogbar.dm:45
    [DmParams()]
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(user))
        {
            SSvis_overlays.remove_vis_overlay(user, user.managed_vis_overlays);
            if (user_client != null)
                Dm.remove(user_client.images, blank);
        }

        user = null;
        user_client = null;
        cog = null;
        QDEL_NULL(ref blank);

        return base.Destroy(force);
    }

    /// Adds the cog to the user, visible by other players
    // tg: code/datums/cogbar.dm:59
    public virtual void add_cog_to_user()
    {
        Dm.NotTranslated("code/datums/cogbar.dm:59", "add_cog_to_user: the busy cog is drawn by the 3D client (DV13); only the deferred New calls it");
    }

    /// Removes the cog from the user
    // tg: code/datums/cogbar.dm:83
    public virtual void remove()
    {
        if (cog == null)
        {
            qdel(this);
            return;
        }

        animate(cog, Dm.named("alpha", 0), Dm.named("time", COGBAR_ANIMATION_TIME));

        // QDEL_IN(src, COGBAR_ANIMATION_TIME) (__DEFINES/qdel.dm:59), expanded inline (rules 11.1 (f))
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), (COGBAR_ANIMATION_TIME > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this)), COGBAR_ANIMATION_TIME, file: "code/datums/cogbar.dm", line: 90);
    }

    /// When the user is deleted, remove the cog
    // tg: code/datums/cogbar.dm:94
    [SignalHandler]
    public virtual void on_user_delete(datum source = null)
    {
        qdel(this);
    }
}
