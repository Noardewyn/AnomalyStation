using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

// Protocol version 16 (M5 slice 3, D148, DV44): the client's messages from a tgui window. tg's page calls Topic with
// href_list["tgui"], ["type"], ["window_id"] and the payload's JSON; tgui_Topic (tgui/external.dm:201) finds the window by its
// id among client.tgui_windows (an unknown id force-closes it) and passes the message to the window's on_message. The native
// window sends the same three things (an act as "act/<action>" with its params, or "close"); this is BYOND's side of that call.

public static partial class ClientSeam
{
    /// <summary>
    /// The player's window <paramref name="windowId"/> (tg's <c>"tgui-window-N"</c>) sent the message <paramref name="type"/> with
    /// <paramref name="payload"/> (tg's decoded JSON payload, or null): tg's <c>tgui_Topic</c> from <c>:226</c>, with <c>usr</c> the
    /// client's mob. A window the client does not have is force-closed (<c>SStgui.force_close_window</c>), as tg does. Detached, as
    /// BYOND starts a Topic.
    /// </summary>
    public static void TguiMessage(client player, string windowId, string type, DmList payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.dm_connected || player.gc_destroyed != null || string.IsNullOrEmpty(windowId) || string.IsNullOrEmpty(type))
            return;
        World world = World.Current;
        object caller_usr = world.usr;
        try
        {
            world.usr = player.mob;
            // tg: code/modules/tgui/external.dm:226  Locate window
            datum_tgui_window window = player.tgui_windows?[windowId] as datum_tgui_window;
            if (window == null)
            {
                // log_tgui(usr, "Error: Couldn't find the window datum, force closing.", context = window_id): the tgui log (M10)
                if (player.mob != null)
                    GlobalProcs.SStgui.force_close_window(player.mob, windowId);
                return;
            }
            // tg: :243  Pass message to window
            DmList href_list = Dm.list(Dm.kv("type", type), Dm.kv("tgui", Dm.TRUE), Dm.kv("window_id", windowId));
            if (payload != null)
                href_list["payload"] = Dm.json_encode(payload);
            Waiting.Detach(window.on_message(type, payload, href_list));
        }
        finally
        {
            world.usr = caller_usr;
        }
    }
}
