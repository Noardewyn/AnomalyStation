using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

// Protocol version 24 (D154, DV59): a click on a link of tg's chat. tg writes links into its chat lines (<a href='byond://?src=[ref];
// key=value'>); BYOND sends a clicked link to the server as client/Topic(href, href_list, hsrc), where hsrc is the datum the src key
// names, and tg's /client/Topic (client_procs.dm:37) ends in ..(), BYOND's default, which calls hsrc.Topic(href, href_list). The host
// takes only a link that it sent to that player (the host's check, DV59) and calls this.

public static partial class ClientSeam
{
    /// <summary>The scheme and query start of a BYOND link (<c>byond://?</c>); the rest is the query, BYOND's <c>params2list</c> text.</summary>
    public const string TopicLinkPrefix = "byond://?";

    /// <summary>
    /// The start of BYOND's client command link (<c>byond://winset?command=vote</c>, tg's vote announcement): the client runs the command as
    /// if typed in its input bar, a verb of the player's own mob, so it names no object and needs no sent-link check (DV364).
    /// </summary>
    public const string WinsetLinkPrefix = "byond://winset?";

    /// <summary>Whether <paramref name="href"/> is a BYOND link the client may send back: a Topic link or a winset command link.</summary>
    public static bool IsLink(string href) => href != null && (href.StartsWith(TopicLinkPrefix, StringComparison.Ordinal) || href.StartsWith(WinsetLinkPrefix, StringComparison.Ordinal));

    /// <summary>
    /// The player clicked the chat link <paramref name="href"/> (<c>byond://?src=[ref];key=value;...</c>): tg's <c>/client/Topic</c> with
    /// <c>usr</c> the client's mob, as far as this port's links reach it. Its first check (<c>usr != mob</c>) holds by construction; the
    /// topic rate limits are the host's (the click bucket, D40); its tgui, asset cache, admin, ticket, message and command bar keys
    /// belong to transports and tools the port replaces (translation-rules 13: tgui is D148's own action, the admin tools D73's), so
    /// a link with them reaches nothing; then <c>hsrc</c>, the live datum the <c>src</c> key names, gets <c>Topic(href, href_list)</c>
    /// (BYOND's default, which tg's <c>..()</c> reaches; the verb queue of <c>DEFAULT_TRY_QUEUE_VERB</c> is the host's command phase).
    /// Detached, as BYOND starts a Topic.
    /// </summary>
    public static void Topic(client player, string href)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.dm_connected || player.gc_destroyed != null || string.IsNullOrEmpty(href))
            return;
        if (href.StartsWith(WinsetLinkPrefix, StringComparison.Ordinal))
        {
            WinsetCommand(player, Dm.params2list(href.Substring(WinsetLinkPrefix.Length))["command"] as string);
            return;
        }
        string query = href.StartsWith(TopicLinkPrefix, StringComparison.Ordinal) ? href.Substring(TopicLinkPrefix.Length)
            : href.StartsWith('?') ? href.Substring(1) : href;
        DmList href_list = Dm.params2list(query);
        // BYOND: hsrc is the object the src parameter references (locate), null when it names nothing alive
        datum hsrc = Dm.locate_ref(href_list["src"]) as datum;
        // tg: client_procs.dm:133  switch(href_list["_src_"]): "usr" is the client's mob, "holder" its admin holder (M10x slice G6:
        // the tickets' links; the holder's other keys are not translated), "vars" the view variables (D73)
        if (Dm.eq(href_list["_src_"], "usr"))
            hsrc = player.mob;
        else if (Dm.eq(href_list["_src_"], "holder"))
            hsrc = player.holder;
        World world = World.Current;
        object caller_usr = world.usr;
        try
        {
            world.usr = player.mob;
            // tg: client_procs.dm:107-127  M10x slice G6: the admin PM, ticket and tgui-less admin help links
            if (Dm.truthy(href_list["priv_msg"]))
            {
                Waiting.Detach(player.cmd_admin_pm(href_list["priv_msg"], null));
                return;
            }
            if (Dm.truthy(href_list["player_ticket_panel"]))
            {
                player.view_latest_ticket();
                return;
            }
            if (Dm.truthy(href_list["tguiless_adminhelp"]))
            {
                Waiting.Detach(TguilessAdminhelp(player));
                return;
            }
            if (hsrc == null || GlobalProcs.QDELETED(hsrc))
                return; // tg: client_procs.dm:153  if(QDELETED(real_src)) return
            Waiting.Detach(hsrc.Topic(href, href_list));
        }
        finally
        {
            world.usr = caller_usr;
        }
    }

    // BYOND's winset command (DV364): the command a client runs as typed in its input bar, a verb of its mob, with usr the mob, detached.
    // shortcut: only tg's "vote" (the vote announcement's link); add a verb here when a tg chat line links another command
    private static void WinsetCommand(client player, string command)
    {
        if (command != "vote" || player.mob == null)
            return;
        World world = World.Current;
        object caller_usr = world.usr;
        try
        {
            world.usr = player.mob;
            player.mob.vote();
        }
        finally
        {
            world.usr = caller_usr;
        }
    }

    // tg: client_procs.dm:123  no_tgui_adminhelp(input(src, "Enter your ahelp", "Ahelp") as null|message): BYOND's input is tgui's
    // multiline text prompt (DV217)
    private static async DmTask TguilessAdminhelp(client player)
    {
        object message = await GlobalProcs.tgui_input_text(player.mob, "Enter your ahelp", "Ahelp", multiline: Dm.TRUE);
        player.no_tgui_adminhelp(message);
    }
}
