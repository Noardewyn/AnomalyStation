// Translated from tgstation (snapshot 1d99bd7f): code/modules/balloon_alert/balloon_alert.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P1: the whole file (D128: the player sees why an action failed). balloon_alert_perform ends at the message sink's
// Balloon (presentation-seam.md 3: tg's client image, its measured maptext and its animate are the client's). M9 slice A: the
// to_viewers and to_hearers forms, and the runechat preference check (the preferences' defaults, DV161; the chat line it falls
// back to has no icon: icon2html's chat icons are the 3D client's, as in the scanners).

// tg: code/modules/balloon_alert/balloon_alert.dm:18
public partial class atom : datum
{
    /**
     * Creates text that will float from the atom upwards to the viewer.
     *
     * Args:
     * * mob/viewer: The mob the text will be shown to. Nullable (But only in the form of it won't runtime).
     * * text: The text to be shown to viewer. Must not be null.
     */
    // tg: code/modules/balloon_alert/balloon_alert.dm:18
    [ShouldNotSleep]
    public virtual object balloon_alert(mob viewer = null, object text = null)
    {
        Waiting.Detach(balloon_alert_perform(viewer, text));
        return null;
    }

    /// Create balloon alerts (text that floats up) to everything within range.
    /// Will only display to people who can see.
    // tg: code/modules/balloon_alert/balloon_alert.dm:25
    public virtual void balloon_alert_to_viewers(object message = null, object self_message = null, float? vision_distance = null, DmList ignored_mobs = null)
    {
        vision_distance ??= DEFAULT_MESSAGE_RANGE;
        DmList hearers = get_hearers_in_view(vision_distance, this, RECURSIVE_CONTENTS_CLIENT_MOBS) as DmList;
        Dm.remove(ref hearers, ignored_mobs);

        foreach (mob hearer in Dm.each<mob>(hearers))
        {
            if (Dm.truthy(hearer.is_blind()))
                continue;

            balloon_alert(hearer, hearer == this && Dm.truthy(self_message) ? self_message : message);
        }
    }

    /// Create balloon alerts (text that floats up) to everything within range.
    /// Will only display to people who can hear.
    // tg: code/modules/balloon_alert/balloon_alert.dm:39
    public virtual void balloon_alert_to_hearers(object message = null, object self_message = null, float? hearing_distance = null, DmList ignored_mobs = null)
    {
        hearing_distance ??= DEFAULT_MESSAGE_RANGE;
        DmList hearers = get_hearers_in_view(hearing_distance, this, RECURSIVE_CONTENTS_CLIENT_MOBS) as DmList;
        Dm.remove(ref hearers, ignored_mobs);

        foreach (mob hearer in Dm.each<mob>(hearers))
        {
            if (HAS_TRAIT(hearer, TRAIT_DEAF))
                continue;

            balloon_alert(hearer, hearer == this && Dm.truthy(self_message) ? self_message : message);
        }
    }

    // Do not use.
    // MeasureText blocks. I have no idea for how long.
    // I would've made the maptext_height update on its own, but I don't know
    // if this would look bad on laggy clients.
    // tg: code/modules/balloon_alert/balloon_alert.dm:55
    public virtual async DmTask balloon_alert_perform(mob viewer = null, object text = null)
    {
        client viewer_client = viewer?.client;
        if (viewer_client == null)
            return;

        if (!Dm.truthy(runechat_prefs_check(viewer, EMOTE_MESSAGE)))
        {
            to_chat(viewer, span_emote(Dm.text("[]: []", name, text))); // tg: "[icon2html(src, viewer)] [src.name]: [text]" (see the file comment)
            return;
        }

        // tg: :65-102  the client image above the atom (or its turf holder), its maptext measured on the client, the rise
        // and fade animate, update_on_z and the two timers that forget it and remove it from the client.
        // Presentation (presentation-seam.md 3): the message sink's Balloon draws the floating text for that viewer.
        DmWorldState.Current.Sinks.Messages.Balloon(viewer, this, Dm.str(text));
    }

    // tg: code/modules/balloon_alert/balloon_alert.dm:105
    public virtual void forget_balloon_alert(image balloon_alert = null)
    {
        LAZYREMOVE(ref update_on_z, balloon_alert);
    }
}
