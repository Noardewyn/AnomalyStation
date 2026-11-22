// Translated from tgstation (snapshot 1d99bd7f): code/modules/tooltip/tooltip.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Tooltips (protocol version 19, DV47): the whole file, with its BYOND output replaced by the native tooltip's. tg's tooltip is
// a browser window of the player's BYOND client (SKIN_MAINWINDOW_TOOLTIP) running tooltip.html: New writes the page into it
// with browse() (and sends jQuery), show writes the tile size once (tooltip.init) and then the cursor params, the view size,
// the HTML text, the theme and the special case (tooltip.update) with output(), and the page's JavaScript sizes the box, places
// it under the hovered tile and shows the window; do_hide hides the window with winshow. Here the client draws the box itself
// (DV47):
// - output(..., "[control]:tooltip.update") and winshow(owner, control, FALSE) become records in client.dm_tooltip_output
//   (TooltipOutput: shown with tg's text, theme and special case, or hidden), which the replicator sends to the player's client
//   as protocol 19 Tooltip messages;
// - New's browse() of the page and its jQuery asset, and show's tooltip.init (the tile size), have nothing to write: there is no
//   page, and the client knows the tile size;
// - the cursor params and the view size that the page placed the box with are kept in the record but not sent: the client
//   places the box next to its pointer (DV47).
// The datum's state (showing, queueHide, init, last_target and its qdel signal) and the hide's 0.1 s delay are tg's.

/// <summary>DV47: what tg's tooltip datum writes into the player's BYOND client, for the replicator to send.</summary>
/// <param name="Kind">Shown or hidden.</param>
/// <param name="Text">For a show: tg's HTML text (<c>"[title][content]"</c>, as <c>show</c> wraps them); else null.</param>
/// <param name="Theme">For a show: tg's theme (a class of <c>tooltip.html</c>); else null.</param>
/// <param name="Special">For a show: tg's special case (<c>"none"</c>; the page has no case for any other); else null.</param>
/// <param name="Params">For a show: tg's param object (<c>{ "cursor": params, "screenLoc": screen_loc }</c>); else null.</param>
public sealed record TooltipOutput(TooltipOutputKind Kind, string Text, string Theme, string Special, string Params);

/// <summary>DV47: the kinds of <see cref="TooltipOutput"/>.</summary>
public enum TooltipOutputKind : byte
{
    /// <summary><c>owner &lt;&lt; output(list2params(...), "[control]:tooltip.update")</c>: the page shows the box.</summary>
    Show = 0,

    /// <summary><c>winshow(owner, control, FALSE)</c>: the window is hidden.</summary>
    Hide = 1,
}

public partial class client
{
    /// <summary>
    /// DV47: what tg's tooltip datum wrote into this client since the replicator last took it, oldest first (BYOND's output to the
    /// tooltip window and its winshow). Presentation output, like the tgui and sound sinks: not world state.
    /// </summary>
    [NotHashed]
    public readonly List<TooltipOutput> dm_tooltip_output = new();

    /// <summary>The most records <see cref="dm_tooltip_output"/> keeps for a client nothing takes them from (the oldest go).</summary>
    public const int DmTooltipOutputCap = 64;

    internal void dm_tooltip_write(TooltipOutput output)
    {
        if (dm_tooltip_output.Count >= DmTooltipOutputCap)
            dm_tooltip_output.RemoveAt(0);
        dm_tooltip_output.Add(output);
    }
}

/*
Usage:
- Define mouse event procs on your (probably HUD) object and simply call the show and hide procs respectively:
	/atom/movable/screen/hud
		MouseEntered(location, control, params)
			usr.client.tooltip.show(params, title = src.name, content = src.desc)

		MouseExited()
			usr.client.tooltip.hide()

Customization:
- Theming can be done by passing the theme var to show() and using css in the html file to change the look
- For your convenience some pre-made themes are included

Notes:
- You may have noticed 90% of the work is done via javascript on the client. Gotta save those cycles man.
*/

// tg: code/modules/tooltip/tooltip.dm:20
[DmPath("/datum/tooltip")]
public partial class datum_tooltip : datum
{
    // tg: code/modules/tooltip/tooltip.dm:29
    [DmParams("C")]
    public override async DmTask New(params object[] __extra)
    {
        client C = Dm.arg(__extra, 0) as client;
        if (Dm.truthy(C))
        {
            owner = C;
            // DEVIATION(DV47): var/datum/asset/stuff = get_asset_datum(/datum/asset/simple/jquery); stuff.send(owner) and
            // owner << browse(file2text('code/modules/tooltip/tooltip.html'), "window=[control]") (:31-34): the client draws the
            // tooltip itself; there is no page to write or script to send.
        }

        await base.New(__extra);
    }

    // tg: code/modules/tooltip/tooltip.dm:39
    public virtual float show(atom_movable thing = null, object @params = null, object title = null, object content = null, object theme = null, object special = null)
    {
        theme ??= "default";
        special ??= "none";
        if (!Dm.truthy(thing) || !Dm.truthy(@params) || (!Dm.truthy(title) && !Dm.truthy(content)) || !Dm.truthy(owner) || !isnum(ICON_SIZE_ALL))
            return FALSE;

        if (last_target != null)
            UnregisterSignal(last_target, COMSIG_QDELETING);

        RegisterSignal(thing, COMSIG_QDELETING, nameof(on_target_qdel));

        last_target = thing;

        if (!Dm.truthy(init))
        {
            //Initialize some vars
            init = 1;
            // DEVIATION(DV47): owner << output(list2params(list(ICON_SIZE_ALL, control)), "[control]:tooltip.init") (:53): the
            // page's tile size; the client knows its own.
        }

        showing = 1;

        if (Dm.truthy(title) && Dm.truthy(content))
        {
            title = Dm.text("<h1>[]</h1>", title);
            content = Dm.text("<p>[]</p>", content);
        }
        else if (Dm.truthy(title) && !Dm.truthy(content))
            title = Dm.text("<p>[]</p>", title);
        else if (!Dm.truthy(title) && Dm.truthy(content))
            content = Dm.text("<p>[]</p>", content);

        // Strip macros from item names
        title = replacetext(title, Dm.text("\\proper"), "");
        title = replacetext(title, Dm.text("\\improper"), "");

        //Make our dumb param object
        @params = Dm.text("{ \"cursor\": \"[]\", \"screenLoc\": \"[]\" }", @params, thing.screen_loc);

        //Send stuff to the tooltip
        // var/view_size = getviewsize(owner.view); owner << output(list2params(list(params, view_size[1], view_size[2],
        // "[title][content]", theme, special)), "[control]:tooltip.update") (:73-74)
        // DEVIATION(DV47): the update becomes a record for the replicator (protocol 19's Tooltip); the view size placed the box
        // under the hovered tile in the page, and the client places it at its pointer, so it is not kept.
        owner.dm_tooltip_write(new TooltipOutput(TooltipOutputKind.Show, Dm.text("[][]", title, content), Dm.str(theme), Dm.str(special), Dm.str(@params)));

        //If a hide() was hit while we were showing, run hide() again to avoid stuck tooltips
        showing = 0;
        if (Dm.truthy(queueHide))
            hide();

        return TRUE;
    }

    // tg: code/modules/tooltip/tooltip.dm:84
    public virtual float hide()
    {
        queueHide = Dm.truthy(showing) ? TRUE : FALSE;

        if (Dm.truthy(queueHide))
            _addtimer(Dm.@new<datum_callback>(this, nameof(do_hide)), 0.1f * SECONDS, file: "code/modules/tooltip/tooltip.dm", line: 88);
        else
            do_hide();

        return TRUE;
    }

    // tg: code/modules/tooltip/tooltip.dm:94
    [SignalHandler]
    public virtual void on_target_qdel(datum source = null)
    {
        // INVOKE_ASYNC(src, PROC_REF(hide)): hide does not sleep.
        Waiting.DetachSync(() => hide());
        last_target = null;
    }

    // tg: code/modules/tooltip/tooltip.dm:100
    public virtual void do_hide()
    {
        // DEVIATION(DV47): winshow(owner, control, FALSE): the window's hide becomes a record for the replicator.
        owner?.dm_tooltip_write(new TooltipOutput(TooltipOutputKind.Hide, null, null, null, null));
    }

    // tg: code/modules/tooltip/tooltip.dm:103
    public override object Destroy(float? force = null)
    {
        last_target = null;
        return base.Destroy(force);
    }
}

public static partial class GlobalProcs
{
    //Open a tooltip for user, at a location based on params
    //Theme is a CSS class in tooltip.html, by default this wrapper chooses a CSS class based on the user's UI_style (Midnight, Plasmafire, Retro, etc)
    //Includes sanity.checks
    // tg: code/modules/tooltip/tooltip.dm:110
    public static void openToolTip(mob user = null, atom_movable tip_src = null, object @params = null, object title = null, object content = null, object theme = null)
    {
        title ??= "";
        content ??= "";
        theme ??= "";
        if (!istype(user, typeof(mob)) || !Dm.truthy(user.client?.tooltips))
            return;
        object ui_style = user.client?.prefs?.read_preference(typeof(datum_preference_choiced_ui_style));
        if (!Dm.truthy(theme) && Dm.truthy(ui_style))
            theme = LOWER_TEXT(ui_style);
        if (!Dm.truthy(theme))
            theme = "default";
        user.client.tooltips.show(tip_src, @params, title, content, theme);
    }

    //Arbitrarily close a user's tooltip
    //Includes sanity checks.
    // tg: code/modules/tooltip/tooltip.dm:123
    public static void closeToolTip(mob user = null)
    {
        if (!istype(user, typeof(mob)) || !Dm.truthy(user.client?.tooltips))
            return;
        user.client.tooltips.hide();
    }
}
