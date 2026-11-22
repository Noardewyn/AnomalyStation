using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

// Protocol version 38 (M9 slice A): a message of the player's say window (D250, the client's port of tgui-say). tg's window sends
// window.send_message(type, payload) to the server, which tgui hands to the subscribed /datum/tgui_say/proc/on_message (modal.dm:117).

public static partial class ClientSeam
{
    /// <summary>
    /// The say window sent <paramref name="type"/> (open, close, thinking, typing, entry, force, save) with tg's payload keys: the channel,
    /// the entry and the thinking indicator's visibility. <c>usr</c> is the client's mob, as in a tgui message. The text's length is
    /// tg's own check (handle_entry); the window's datum is made at login (client New), so a message before it is dropped.
    /// </summary>
    public static void Say(client player, string type, string channel, string entry, bool visible)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.dm_connected || player.gc_destroyed != null || player.tgui_say is not datum_tgui_say window)
            return;
        DmList payload = Dm.list();
        if (channel != null) payload["channel"] = channel;
        if (entry != null) payload["entry"] = entry;
        if (type == "thinking") payload["visible"] = visible ? 1f : 0f;
        World world = World.Current;
        object caller_usr = world.usr;
        try
        {
            world.usr = player.mob;
            // a CRASH in handle_entry (a null or overlong entry) ends the call as a runtime, as BYOND's would
            Waiting.DetachSync(() => window.on_message(type, payload));
        }
        finally
        {
            world.usr = caller_usr;
        }
    }
}
