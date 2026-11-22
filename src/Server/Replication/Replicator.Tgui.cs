using System.Linq;
using System.Text.Json;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Replication;

// Protocol version 16 (M5 slice 3, D148, DV44): the player's tgui windows. tg's windows write into the player's BYOND client (the port's
// client.dm_tgui_output: tgui_window's browse and output records); this turns them into UiWindow messages: a message of type "update"
// becomes a window update (tg's payload: the config, the data, the static data, parsed from tg's JSON), "suspend" and browse(null)
// become a close, the rest (ping and the like) has nothing for the client. SStgui sends a window's whole ui_data each 0.9 s; an update
// whose config and data equal the last one sent and that has no static data changes nothing the client merges (tgui's update handler
// sets the same fields again), so it is not sent: only changes cross the wire. A join sends every open window again, whole.

public sealed partial class Replicator
{
    /// <summary>M10 slice F: tg's lobby menu's window id (lobby_menu.dm:41, <c>new(client, "lobby_menu")</c>).</summary>
    public const string LobbyWindowId = "lobby_menu";

    /// <summary>M10x slice G4: tg's escape menu's window id (escape_menu.dm:40, <c>new(client, "escape_menu")</c>).</summary>
    public const string EscapeMenuWindowId = "escape_menu";

    private sealed partial class SessionState
    {
        // What the client has of each open window: the merged state, and the last update's config and data (for the repeats).
        private sealed class UiShadow
        {
            public string Interface;
            public string Title;
            public int Status;
            public bool Refreshing;
            public string LastDataJson;
            public UiValue Data = UiValue.ObjectOf();
            public UiValue StaticData = UiValue.ObjectOf();
        }

        private readonly SortedDictionary<int, UiShadow> _uiShadow = new();

        // M10x slice G6 (DV216): the window name of each open browse() page and its index, oldest first
        private readonly List<(string Name, int Index)> _browse = new();

        // browse() windows the player closed: the commands run before Update, which clears PendingUi, so their closes wait for it
        private readonly List<int> _closedBrowse = new();

        /// <summary>The UiWindow messages of the last <see cref="Update"/>, in the order tg wrote them.</summary>
        public List<UiWindowMessage> PendingUi { get; } = new();

        private void UpdateUiWindows(client player, bool full)
        {
            PendingUi.Clear();
            foreach (int closed in _closedBrowse)
                CloseUi(closed);
            _closedBrowse.Clear();
            if (full)
            {
                foreach ((int index, UiShadow shadow) in _uiShadow)
                {
                    PendingUi.Add(new UiWindowMessage
                    {
                        Kind = UiWindowKind.Update, Window = index, Interface = shadow.Interface, Title = shadow.Title, Status = shadow.Status,
                        Refreshing = shadow.Refreshing, Data = shadow.Data, StaticData = shadow.StaticData,
                    });
                }
            }
            if (player == null || player.dm_tgui_output.Count == 0)
                return;
            foreach (TguiOutput output in player.dm_tgui_output)
            {
                int index;
                if (output.WindowId == LobbyWindowId)
                    index = Limits.LobbyUiWindow;
                else if (output.WindowId == EscapeMenuWindowId)
                    index = Limits.EscapeMenuUiWindow;
                else if (output.Kind == TguiOutputKind.Browse || !output.WindowId.StartsWith("tgui-window-", StringComparison.Ordinal))
                {
                    TakeBrowse(output);
                    continue;
                }
                else
                {
                    float? at = datum_tgui_window.TGUI_WINDOW_INDEX(output.WindowId);
                    if (at is not float found || found < 1 || found >= Limits.BrowseUiWindowFirst || found != MathF.Floor(found))
                        continue;
                    index = (int)found;
                }
                switch (output.Kind)
                {
                    case TguiOutputKind.Initialize:
                    case TguiOutputKind.Close:
                        CloseUi(index); // a new page (initialize) or browse(null): what the client showed is gone
                        break;
                    case TguiOutputKind.Message:
                        TakeUiMessage(index, output.Json);
                        break;
                }
            }
            player.dm_tgui_output.Clear();
        }

        // M10 slice F (DV44): tg's lobby page takes "init" (its whole state) and "state" (the fields that changed, merged), its
        // reducer's serverInit and serverUpdate (tgui-lobby LobbyMenu.tsx); both become an update of the lobby's window, its data the
        // payload, interactive (the lobby has no ui status). M10x slice G4: the escape menu's page takes the same two messages.
        private void TakePageMessage(int index, string @interface, string type, UiValue payload)
        {
            if (type is not ("init" or "state"))
                return;
            UiValue data = ObjectOrNull(payload);
            if (data == null)
                return;
            data = Sanitize(data, 0);
            bool fresh = !_uiShadow.TryGetValue(index, out UiShadow shadow);
            if (fresh)
            {
                if (type != "init")
                    return; // a state before the page's init: tg's reducer drops it (serverUpdate without a serverState)
                shadow = new UiShadow { Interface = @interface, Title = "", Status = 2 };
                _uiShadow[index] = shadow;
            }
            else if (type == "init")
                shadow.Data = UiValue.ObjectOf();
            shadow.Data = shadow.Data.MergedWith(data);
            PendingUi.Add(new UiWindowMessage
            {
                Kind = UiWindowKind.Update, Window = index, Interface = shadow.Interface, Title = shadow.Title, Status = shadow.Status,
                Refreshing = false, Data = type == "init" ? shadow.Data : data, StaticData = null,
            });
        }

        private void CloseUi(int index)
        {
            if (_uiShadow.Remove(index))
                PendingUi.Add(new UiWindowMessage { Kind = UiWindowKind.Close, Window = index });
        }

        // M10x slice G6 (DV216): tg's browse(html, "window=NAME") shows the page in that window, a new one taking the first free index
        // (or the oldest page's); browse(null, "window=NAME") closes it. The page goes whole each time (tg sends it whole).
        private void TakeBrowse(TguiOutput output)
        {
            int at = _browse.FindIndex(b => b.Name == output.WindowId);
            if (output.Kind != TguiOutputKind.Browse)
            {
                if (at >= 0)
                {
                    CloseUi(_browse[at].Index);
                    _browse.RemoveAt(at);
                }
                return;
            }
            UiValue page;
            try
            {
                page = UiValue.FromJson(output.Json);
            }
            catch (JsonException)
            {
                return;
            }
            int index;
            if (at >= 0)
            {
                index = _browse[at].Index;
                _browse.RemoveAt(at);
            }
            else
            {
                index = Enumerable.Range(Limits.BrowseUiWindowFirst, Limits.BrowseUiWindows).FirstOrDefault(i => !_browse.Exists(b => b.Index == i));
                if (index == 0)
                {
                    index = _browse[0].Index;
                    _browse.RemoveAt(0);
                }
            }
            _browse.Add((output.WindowId, index));
            string html = page.Get("html")?.Text ?? "";
            var pieces = new List<UiValue>();
            for (int from = 0; from < html.Length;)
            {
                string piece = ServerHost.Truncate(html.Substring(from), Limits.MaxUiStringBytes);
                if (piece.Length == 0)
                    break;
                pieces.Add(UiValue.Of(piece));
                from += piece.Length;
            }
            var shadow = new UiShadow
            {
                Interface = Limits.BrowseUiInterface, Title = Clip(Text(page.Get("title")), Limits.MaxUiTitleBytes), Status = 2,
                Data = UiValue.ObjectOf(("html", UiValue.ListOf(pieces)), ("width", page.Get("width")), ("height", page.Get("height"))),
            };
            _uiShadow[index] = shadow;
            PendingUi.Add(new UiWindowMessage
            {
                Kind = UiWindowKind.Update, Window = index, Interface = shadow.Interface, Title = shadow.Title, Status = shadow.Status,
                Refreshing = false, Data = shadow.Data, StaticData = null,
            });
        }

        // M10x slice G6: the player closed a browse() window (BYOND's window close): it is forgotten
        public void ForgetBrowse(int index)
        {
            int at = _browse.FindIndex(b => b.Index == index);
            if (at < 0)
                return;
            _browse.RemoveAt(at);
            _closedBrowse.Add(index);
        }

        private void TakeUiMessage(int index, string json)
        {
            UiValue message;
            try
            {
                message = UiValue.FromJson(json);
            }
            catch (JsonException)
            {
                return; // tg's json_encode always writes JSON; nothing to show otherwise
            }
            string type = message.Get("type")?.Text;
            if (type == "suspend")
            {
                CloseUi(index);
                return;
            }
            if (index == Limits.LobbyUiWindow || index == Limits.EscapeMenuUiWindow)
            {
                TakePageMessage(index, index == Limits.LobbyUiWindow ? Limits.LobbyUiInterface : Limits.EscapeMenuUiInterface, type, message.Get("payload"));
                return;
            }
            if (type != "update")
                return;
            UiValue payload = message.Get("payload");
            UiValue config = payload?.Get("config");
            string @interface = config?.Get("interface")?.Get("name")?.Text;
            if (string.IsNullOrEmpty(@interface))
                return;
            string title = Clip(Text(config.Get("title")), Limits.MaxUiTitleBytes);
            int status = (int)Math.Clamp(config.Get("status")?.Number ?? 0, -1, 2);
            bool refreshing = config.Get("refreshing")?.Truthy ?? false;
            UiValue data = ObjectOrNull(payload.Get("data"));
            UiValue statics = ObjectOrNull(payload.Get("static_data"));
            if (data != null) data = Sanitize(data, 0);
            if (statics != null) statics = Sanitize(statics, 0);
            string dataJson = data?.ToJson();

            bool fresh = !_uiShadow.TryGetValue(index, out UiShadow shadow) || shadow.Interface != Clip(@interface, Limits.MaxUiInterfaceBytes);
            if (fresh)
            {
                shadow = new UiShadow { Interface = Clip(@interface, Limits.MaxUiInterfaceBytes) };
                _uiShadow[index] = shadow;
            }
            else if (statics == null && shadow.Title == title && shadow.Status == status && shadow.Refreshing == refreshing
                && (data == null || dataJson == shadow.LastDataJson))
            {
                return; // SStgui's periodic update repeated what the client has
            }
            shadow.Title = title;
            shadow.Status = status;
            shadow.Refreshing = refreshing;
            if (data != null)
            {
                shadow.LastDataJson = dataJson;
                shadow.Data = shadow.Data.MergedWith(data);
            }
            if (statics != null)
                shadow.StaticData = shadow.StaticData.MergedWith(statics);
            PendingUi.Add(new UiWindowMessage
            {
                Kind = UiWindowKind.Update, Window = index, Interface = shadow.Interface, Title = title, Status = status, Refreshing = refreshing,
                Data = data, StaticData = statics,
            });
        }

        private static string Text(UiValue v) => v == null ? "" : v.Kind switch
        {
            UiValueKind.String => v.Text,
            UiValueKind.Number => v.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => "",
        };

        private static UiValue ObjectOrNull(UiValue v)
        {
            if (v == null || v.Kind == UiValueKind.Null)
                return null;
            // tg's json_encode writes an empty DM list as [] (no keys): an empty object for the client.
            if (v.Kind == UiValueKind.Array && v.Items.Count == 0)
                return UiValue.ObjectOf();
            return v.Kind == UiValueKind.Object ? v : null;
        }

        // The wire's caps (Limits): over-long strings and keys are cut, values nested too deep become null.
        private static UiValue Sanitize(UiValue v, int depth)
        {
            if (depth >= Limits.MaxUiDepth && (v.Kind == UiValueKind.Array || v.Kind == UiValueKind.Object))
                return UiValue.Null;
            switch (v.Kind)
            {
                case UiValueKind.String:
                    return System.Text.Encoding.UTF8.GetByteCount(v.Text) > Limits.MaxUiStringBytes ? UiValue.Of(Clip(v.Text, Limits.MaxUiStringBytes)) : v;
                case UiValueKind.Array:
                    return UiValue.ListOf(v.Items.Select(i => Sanitize(i, depth + 1)));
                case UiValueKind.Object:
                    return UiValue.ObjectOf(v.Fields.Select(f => new KeyValuePair<string, UiValue>(Clip(f.Key, Limits.MaxUiKeyBytes), Sanitize(f.Value, depth + 1))));
                default:
                    return v;
            }
        }

        private static string Clip(string text, int maxBytes) => ServerHost.Truncate(text ?? "", maxBytes);
    }
}
