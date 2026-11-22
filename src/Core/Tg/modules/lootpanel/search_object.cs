// Translated from tgstation (snapshot 1d99bd7f): code/modules/lootpanel/search_object.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package G1 (D291): the whole file. The icon of an item is its own icon file and icon state, sent as they are (the 3D client
// draws them from its copy of tg's .dmi files: DV150's way, as the strip menu's items); tg's three conditions for the cheap icon keep
// their order, and the version condition (BYOND older than 515.1635) cannot arise. A mob or an item with more than two overlays is
// queued for generate_icon as in tg, whose costly_icon2html (the item's whole appearance as an inline picture) the port has not: it
// gives the same icon file and state, so the window shows the base picture (docs/ai/specs/not-translated.md section 98).

/**
 * ## Search Object
 * An object for content lists. Compacted item data.
 */
// tg: code/modules/lootpanel/search_object.dm:6
public partial class datum_search_object : datum
{
    // tg: code/modules/lootpanel/search_object.dm:19
    [DmParams("owner", "item")]
    public override async DmTask New(params object[] __extra)
    {
        client owner = Dm.arg(__extra, 0) as client;
        atom item = Dm.arg(__extra, 1) as atom;
        await base.New(__extra);

        this.item = item;
        name = item.name;
        if (isobj(item))
            path = item.type;

        if (isturf(item))
            RegisterSignal(item, COMSIG_TURF_CHANGE, nameof(on_turf_change));
        else
        {
            // Lest we find ourselves here again, this is intentionally stupid.
            // It tracks items going out and user actions, otherwise they can refresh the lootpanel.
            // If this is to be made to track everything, we'll need to make a new signal to specifically create/delete a search object
            RegisterSignals(item, Dm.list(
                COMSIG_ITEM_PICKUP,
                COMSIG_MOVABLE_MOVED,
                COMSIG_QDELETING
            ), nameof(on_item_moved));
        }

        // Icon generation conditions //////////////
        // Condition 1: Icon is complex
        if (ismob(item) || length(item.overlays) > 2)
            return;

        // Condition 2: Can't get icon path
        if (!isfile(item.icon) || !Dm.truthy(length(Dm.text("[]", item.icon))))
            return;

        // Condition 3: Using opendream (#if defined(OPENDREAM) || defined(UNIT_TESTS)): inactive in production, not translated
        // Condition 4: Using older byond version: the 3D client has no BYOND version, it always passes

        icon = Dm.text("[]", item.icon);
        icon_state = item.icon_state;
    }

    // tg: code/modules/lootpanel/search_object.dm:55
    public override object Destroy(float? force = null)
    {
        item = null;
        icon = null;

        return base.Destroy(force);
    }

    /// Generates the icon for the search object. This is the expensive part.
    // tg: code/modules/lootpanel/search_object.dm:70
    public virtual void generate_icon(client owner = null)
    {
        // icon = costly_icon2html(item, owner, sourceonly = TRUE): the item's whole appearance as an inline picture is the chat icons'
        // gap (section 76); the window draws the item's icon file and state, "n/a" (tgui's dumpster fire) when it has none
        Dm.Deferred("code/modules/lootpanel/search_object.dm:72", "generate_icon: costly_icon2html, the item's whole appearance as an inline picture (the chat icons; the window draws the base icon file and state)");
        icon = isfile(item.icon) ? Dm.text("[]", item.icon) : "n/a";
        icon_state = item.icon_state;
    }

    /// Parent item has been altered, search object no longer valid
    // tg: code/modules/lootpanel/search_object.dm:75
    [SignalHandler]
    public virtual void on_item_moved(atom source = null)
    {
        if (QDELETED(this))
            return;

        qdel(this);
    }

    /// Parent tile has been altered, entire search needs reset
    // tg: code/modules/lootpanel/search_object.dm:85
    [SignalHandler]
    public virtual void on_turf_change(turf source = null, object path = null, DmList new_baseturfs = null, object flags = null, DmList post_change_callbacks = null)
    {
        // tg's ChangeTurf runs each callback with the new turf as its last argument (callback.InvokeAsync(W)), which qdel takes as its force
        // (a turf is true in DM): the port's qdel types force as a number, so the true value is given here and the turf is dropped.
        Dm.append(post_change_callbacks, Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), this, TRUE));
    }
}
