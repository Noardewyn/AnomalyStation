using System.Diagnostics;
using System.Globalization;
using System.Text;
using Anomaly.Core.DmShim;
using Anomaly.Core.Motion;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Admin;
using Anomaly.Server.Replication;

namespace Anomaly.Server;

// The network half of the host (scope 2e "Validation and limits", D40): everything here runs on the host thread inside
// IServerTransport.Poll (step 2 of the tick) or after World.Step (step 5). Nothing a client sends reaches the world unless
// it passed every check below and became a QueuedCommand; a rejected message never becomes one. In version 2 a client sends
// two kinds of command: MotionInput frames (Input channel, unreliable, into the session's jitter buffer, one taken per tick)
// and Actions (Control channel, held until the tick of their frame).
public sealed partial class ServerHost
{
    /// <summary>Commands (MotionInput messages and actions) dropped by the token buckets within <see cref="Limits.StrikeWindowTicks"/> that close a session with <see cref="DisconnectReason.Flooding"/>: a sustained 60 a second, a script and not a person on a hitching connection (the owner, 2026-11-01).</summary>
    public const int FloodDropLimit = 600;

    // ---- connection requests: the Hello ----

    private void OnConnectionRequested(IConnectionRequest request)
    {
        _requestsThisPoll++;
        if (_quitQueued || _stopRequested || Finished)
        {
            Reject(request, DisconnectReason.ServerShutdown, "The server is shutting down.");
            return;
        }
        if (_requestsThisPoll > Limits.MaxPendingConnectionRequests)
        {
            Reject(request, DisconnectReason.ServerFull, "Too many connection requests; try again.");
            return;
        }
        ReadOnlySpan<byte> data = request.Data;
        if (data.Length > Codec.MaxHelloBytes)
        {
            Reject(request, DisconnectReason.BadHello, "The connection request is too long.");
            return;
        }
        if (!_codec.TryDecode(data, out Hello hello, out ProtocolError error))
        {
            Reject(request, DisconnectReason.BadHello, "The connection request holds no valid Hello (" + error + ").");
            return;
        }
        if (hello.Protocol != ProtocolVersion.Current)
        {
            Reject(request, DisconnectReason.WrongVersion,
                string.Create(CultureInfo.InvariantCulture, $"The server speaks protocol {ProtocolVersion.Current}, the client {hello.Protocol}."));
            return;
        }
        // D335: a server that holds the hub's key takes players through the launcher, and the login token names the player. A
        // Hello without a good one passes only with the reconnect token of a session that logged in since this server started
        // (a drop, a round restart: the two-minute login is over by then).
        string login = LoginName(hello.LoginToken, out long loginExpires);
        if (_hubKey != null && login == null && hello.ReconnectToken.IsNone)
        {
            Reject(request, DisconnectReason.LoginRequired, hello.LoginToken == null
                ? "This server takes players through the launcher."
                : "The login is not valid here or has expired; join through the launcher again.");
            return;
        }
        string name = login ?? hello.Name;
        if (!ValidateName(name, out string why))
        {
            Reject(request, DisconnectReason.InvalidName, why);
            return;
        }
        if (!JoinsOpen)
        {
            Reject(request, DisconnectReason.NotReady, "The world is still starting; try again in a moment.");
            return;
        }

        string ckey = Dm.ckey(name);
        // M10 slice I: tg's world/IsBanned, a server ban of the key or the address
        string address = AddressOf(request.RemoteEndPoint);
        if (BanRefusal(ckey, address) is string banned)
        {
            Reject(request, DisconnectReason.Banned, banned);
            return;
        }
        Session live = LiveSession(ckey);
        _players.TryGetValue(ckey, out PlayerRecord player);
        // D275: a key the database knows from an earlier run is that key's, with its token
        ServerDatabase.PlayerRow seen = _db?.FindPlayer(ckey);
        if (player == null && seen != null)
        {
            player = new PlayerRecord(seen.Key, seen.Token);
            _players[ckey] = player;
        }
        string key = name;
        if (player == null)
        {
            if (login == null && !hello.ReconnectToken.IsNone)
            {
                Reject(request, DisconnectReason.BadToken, "This server has no session of that name to take back.");
                return;
            }
            if (LiveSessionCount() >= _config.MaxPlayers)
            {
                Reject(request, DisconnectReason.ServerFull, "The server is full.");
                return;
            }
            player = new PlayerRecord(key, _newToken());
            _players[ckey] = player;
        }
        else
        {
            // A known key: only its token takes it back (the mob kept its key, tg's SSD); a wrong token is refused whether
            // or not the owner is connected (scope 2j), so nobody takes over another player's mob. D335: the account's login
            // takes it back from any computer, and where logins are taken a token kept from before this start does not.
            if (login == null && hello.ReconnectToken.IsNone)
            {
                Reject(request, DisconnectReason.NameInUse, "That name is taken on this server.");
                return;
            }
            if (login == null && hello.ReconnectToken != player.Token)
            {
                Reject(request, DisconnectReason.BadToken, "The reconnect token does not match that name.");
                return;
            }
            if (login == null && _hubKey != null && !player.LoggedIn)
            {
                Reject(request, DisconnectReason.LoginRequired, "This server takes players through the launcher.");
                return;
            }
            if (live != null)
                CloseSession(live, DisconnectReason.Replaced, "A newer connection took this session over.");
            else if (LiveSessionCount() >= _config.MaxPlayers)
            {
                Reject(request, DisconnectReason.ServerFull, "The server is full.");
                return;
            }
            key = player.Key; // the player's first spelling of the name: the mob's key
        }

        IConnection connection = request.Accept();
        if (login != null)
        {
            player.LoggedIn = true;
            MarkLoginUsed(hello.LoginToken, loginExpires);
        }
        var session = new Session(++_lastOrdinal, key, player.Token, connection) { State = SessionState.Joining, Address = address, LastSeenBefore = seen?.LastSeen };
        // tg's player row (client_procs.dm:719-791): the key's first connection claims it with its token (D275), a later one is seen again
        if (seen == null)
            _db?.AddPlayer(ckey, key, player.Token, address, RoundId);
        else
            _db?.UpdatePlayerSeen(ckey, address, RoundId);
        AddSession(session);
        _byConnection[connection.Id] = session;
        _sessionsServed++;
        _queue.Enqueue(new QueuedCommand(World.Tick + 1, session.Ordinal, QueuedCommand.JoinSeq, CommandKind.Join, key));
        _out.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"join #{session.Ordinal} {key}{(live != null ? " (replaces #" + live.Ordinal.ToString(CultureInfo.InvariantCulture) + ")" : "")} at tick {World.Tick}"));
    }

    private void AddSession(Session session)
    {
        _sessions.Add(session);
        _byOrdinal[session.Ordinal] = session;
    }

    private void Reject(IConnectionRequest request, DisconnectReason reason, string text)
    {
        _rejectedRequests++;
        if (_rejectLinesThisWindow++ < 10)
            _out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"refused a connection at tick {World.Tick}: {reason} ({text})"));
        request.Reject(_codec.Encode(new DisconnectInfo(reason, Truncate(text, Limits.MaxDisconnectTextBytes))));
    }

    /// <summary>
    /// The name rules of a Hello (scope 2e, D40): the protocol's <see cref="NameRules.IsValid"/> (not empty, at most
    /// <see cref="Limits.MaxNameChars"/> characters, tg's <c>MAX_NAME_LEN</c>, counted as Unicode scalar values; no control,
    /// format, separator, private-use or unassigned characters; not only spaces), and the host's own two: no spaces at
    /// either end, and at least one letter or digit, so that its canonical key (<c>ckey</c>, the player's identity) is not
    /// empty.
    /// </summary>
    public static bool ValidateName(string name, out string why)
    {
        why = null;
        if (string.IsNullOrEmpty(name)) { why = "The name is empty."; return false; }
        if (!NameRules.IsValid(name))
        {
            int runes = 0;
            foreach (Rune _ in name.EnumerateRunes()) runes++;
            why = runes > Limits.MaxNameChars
                ? string.Create(CultureInfo.InvariantCulture, $"The name is longer than {Limits.MaxNameChars} characters.")
                : "The name has control characters or is not valid text.";
            return false;
        }
        if (char.IsWhiteSpace(name[0]) || char.IsWhiteSpace(name[^1])) { why = "The name starts or ends with a space."; return false; }
        if (string.IsNullOrEmpty(Dm.ckey(name))) { why = "The name needs at least one letter or digit."; return false; }
        return true;
    }

    private void OnConnected(IConnection connection)
    {
        // The session exists since the accept (its Join is queued); nothing to do.
    }

    // ---- received messages: MotionInput and Action ----

    private void OnReceived(IConnection connection, Channel channel, ReadOnlySpan<byte> message)
    {
        if (!_byConnection.TryGetValue(connection.Id, out Session s) || s.State is SessionState.Leaving or SessionState.Closed)
            return;
        s.BytesReceived += message.Length;
        _tickBytesIn += message.Length;
        _totalBytesIn += message.Length;

        if (!_codec.TryPeekId(message, out MessageId id)) { Strike(s, "an unknown message id"); return; }
        // version 38: a say action carries up to 1024 characters, every other message keeps the 256-byte cap
        // version 53: a bug report carries its screenshot
        int cap = id switch { MessageId.Action => Limits.MaxSayActionBytes, MessageId.BugReport => Limits.MaxBugReportPartMessageBytes, _ => Limits.MaxInboundMessageBytes };
        if (message.Length > cap) { Strike(s, "an oversized message"); return; }
        if (_waiting.Contains(s))
            return; // D306: a session waiting between rounds has no world to command (the old round's last inputs may still come)
        switch (id)
        {
            case MessageId.MotionInput when channel == Channel.Input:
                ReceiveMotion(s, message);
                return;
            case MessageId.Action when channel == Channel.Control:
                ReceiveAction(s, message);
                return;
            case MessageId.BugReport when channel == Channel.State:
                ReceiveBugReport(s, message);
                return;
            default:
                // A second Hello, a server message, the version-1 InputFrame, or a right message on the wrong channel.
                Strike(s, string.Create(CultureInfo.InvariantCulture, $"{id} is not a client command on the {channel} channel"));
                return;
        }
    }

    // A MotionInputMessage: the newest frames, repeated (scope 2d). Valid ones go to the session's jitter buffer, which keeps
    // each frame once; the host takes one per tick (FormInputs).
    private void ReceiveMotion(Session s, ReadOnlySpan<byte> message)
    {
        if (s.State != SessionState.Joined) { Strike(s, "a command before Welcome"); return; }
        if (message.Length > Limits.MaxMotionInputMessageBytes) { Strike(s, "an oversized MotionInput"); return; }
        if (!_codec.TryDecode(message, out MotionInputMessage input, out ProtocolError error)) { Strike(s, "a malformed MotionInput (" + error + ")"); return; }
        if (input.Frames.Count == 0 || input.Frames.Count > Limits.MaxInputFramesPerMessage) { Strike(s, "a MotionInput with a bad frame count"); return; }
        foreach (Anomaly.Core.Motion.MotionInput frame in input.Frames)
        {
            if (frame.Flags != Anomaly.Core.Motion.MotionInputFlags.None) { Strike(s, "a MotionInput frame with unknown flags"); return; }
        }
        if (!s.MotionBucket.Take(World.Tick)) { RateLimited(s); return; }
        if (!s.Jitter.Offer(input)) { Strike(s, "a MotionInput frame whose seq is out of range"); return; }
        s.LastReceivedInputSeq = s.Jitter.NewestSeq;
    }

    // An ActionMessage (scope 2b, 2f, 2k): validated here, rate-limited by kind, then held until the tick that applies its
    // frame (FormInputs queues it as a command of that tick). Version 4 (interaction.md section 2): the stateless checks of the
    // new kinds are here (a malformed message is a strike); whether a target exists, is shown and is in range is the world's
    // check in the command phase (ApplyClick, ApplyHover), where the world is at the tick the click belongs to.
    private void ReceiveAction(Session s, ReadOnlySpan<byte> message)
    {
        if (s.State != SessionState.Joined) { Strike(s, "a command before Welcome"); return; }
        if (message.Length > Limits.MaxSayActionBytes) { Strike(s, "an oversized Action"); return; } // the decoder holds every other kind to MaxActionMessageBytes
        if (!_codec.TryDecode(message, out ActionMessage action, out ProtocolError error)) { Strike(s, "a malformed Action (" + error + ")"); return; }
        s.ActionsReceived++; // version 43: whatever happens to it below, it is settled once it is not pending
        if (!ValidAction(action, out string why)) { Strike(s, "an invalid Action (" + why + ")"); return; }
        if (action.FrameSeq > (long)s.Jitter.LastTakenSeq + Limits.MaxInputSeqLead) { Strike(s, "an Action whose frame seq is out of range"); return; }
        if (action.Kind is ActionKind.Say or ActionKind.Emote)
        {
            // version 38: the say window's messages (typing ones come as keys are typed) have their own bucket, outside the overall one;
            // version 39: an emote is a say of "*key" in tg, so the wheel's picks share it
            if (!s.SayBucket.Take(World.Tick)) { RateLimited(s); return; }
            if (s.PendingActions.Count >= ActionLimits.MaxPendingActions) { RateLimited(s); return; }
            s.PendingActions.Add(action);
            s.PendingStamps.Add(Stopwatch.GetTimestamp());
            return;
        }
        if (action.Kind == ActionKind.MouseDrag)
        {
            // version 34: the held button's aim, outside the overall bucket; over its rate dropped silently, and only the newest pending
            // drag is kept (BYOND sends one per mouse move, tg keeps the last params: drag_drop.dm:184)
            if (!s.MouseDragBucket.Take(World.Tick)) { s.MouseDragsDropped++; return; }
            for (int i = s.PendingActions.Count - 1; i >= 0; i--)
            {
                if (s.PendingActions[i].Kind != ActionKind.MouseDrag) continue;
                s.PendingActions.RemoveAt(i);
                s.PendingStamps.RemoveAt(i);
                s.MouseDragsDropped++;
            }
            if (s.PendingActions.Count >= ActionLimits.MaxPendingActions) { s.MouseDragsDropped++; return; }
            s.PendingActions.Add(action);
            s.PendingStamps.Add(Stopwatch.GetTimestamp());
            return;
        }
        switch (TakeActionTokens(s, action.Kind))
        {
            case ActionTake.OverallLimit:
                RateLimited(s);
                return;
            case ActionTake.KindLimit when action.Kind == ActionKind.Hover:
                s.HoversDropped++; // a hover over its rate is dropped silently: sweeping the view over a crowd is normal (ActionLimits.Hover)
                return;
            case ActionTake.KindLimit:
                RateLimited(s);
                return;
        }
        if (action.Kind == ActionKind.DevGasProbe && !_config.DevCommandsEnabled && !HasRights(s, Anomaly.Core.Tg.Defines.R_DEBUG))
        {
            // the dev menu's gas overlay asks twice a second: told once that the server has no dev tools, then dropped; an admin
            // with tg's debug right sees it, as it types the gas dev commands (air, gas, ignite)
            if (!s.DevProbeRefused) ReplyTo(s, "Dev commands are off on this server: no gas overlay.");
            s.DevProbeRefused = true;
            return;
        }
        if (action.Kind == ActionKind.DevCommand)
        {
            // Off, or a line that does not parse: answered to the player, never queued (not a violation: a client may be set up
            // for a server with dev commands on).
            // M10 slice I (D275): an admin (tg's client.holder) types them too, with the rights of tg's matching verbs
            bool admin = IsAdmin(s);
            if (!_config.DevCommandsEnabled && !_config.ClientConsole && !admin) { ReplyTo(s, "Dev commands are off on this server."); return; }
            // "help" (or "?") from a player's console: the dev commands' syntax, as the server's own console prints it (and
            // the console's own commands when CLIENT_CONSOLE is on, or for an admin)
            string trimmed = action.Text?.Trim();
            if (string.Equals(trimmed, "help", StringComparison.OrdinalIgnoreCase) || trimmed == "?"
                || string.Equals(trimmed, "dev-help", StringComparison.OrdinalIgnoreCase)) // the client's own help asks with dev-help
            {
                if (_config.ClientConsole || admin) ReplyLines(s, ConsoleCommand.Help);
                if (_config.DevCommandsEnabled || admin) ReplyLines(s, DevCommandLine.Help);
                return;
            }
            bool dev = DevCommandLine.TryParse(action.Text, out _, out string devError);
            if (dev && !_config.DevCommandsEnabled && admin)
            {
                if (!CheckRights(s, RightsFor(ConsoleVerb.Dev, action.Text))) return;
            }
            else if (!dev || !_config.DevCommandsEnabled)
            {
                // 1.6b: a console command from a player (CLIENT_CONSOLE, or an admin's): the host-only ones answer now, the world
                // ones are queued as this DevCommand action, so the replay log records them with the player's other actions
                if (!TakeClientConsoleLine(s, action.Text, out bool queue)) { ReplyTo(s, dev ? "Dev commands are off on this server." : devError); return; }
                if (!queue) return;
            }
        }
        if (action.Kind == ActionKind.Topic && !s.ChatLinks.Contains(action.Text) && !(action.Text?.StartsWith(Anomaly.Core.Tg.ClientSeam.WinsetLinkPrefix, StringComparison.Ordinal) ?? false)) // DV364: a winset command runs a verb of the own mob
        {
            // version 24 (DV59): only a link the server sent to this session's chat; a forged or forgotten one is dropped (not a
            // violation: a player may click an old line whose link was forgotten)
            s.TopicsRefused++;
            return;
        }
        if (action.Kind is ActionKind.Hover or ActionKind.Unhover)
        {
            // tg's SSmouse_entered keeps the last hovered atom of a client per tick: only the newest pending hover is kept, so
            // a sweep of the crosshair costs one slot and at most one MouseEntered runs per session and tick. Version 14: an
            // Unhover is the newest hover too (the server exits the last hovered thing whichever of them it applies).
            for (int i = s.PendingActions.Count - 1; i >= 0; i--)
            {
                if (s.PendingActions[i].Kind is not (ActionKind.Hover or ActionKind.Unhover)) continue;
                s.PendingActions.RemoveAt(i);
                s.PendingStamps.RemoveAt(i);
                s.HoversDropped++;
            }
        }
        if (s.PendingActions.Count >= ActionLimits.MaxPendingActions) { RateLimited(s); return; }
        s.PendingActions.Add(action);
        s.PendingStamps.Add(Stopwatch.GetTimestamp());
    }

    // The checks of an action beyond what the decoder guarantees (a decoded value satisfies the caps, but the host does not
    // rely on the codec for what it queues). Version 4: the target of a click or hover by kind, the icon pixel, the keybinding
    // code, and no field a kind does not use.
    private static bool ValidAction(in ActionMessage action, out string why)
    {
        why = null;
        bool targeted = action.IsTargeted;
        bool clicked = action.Kind is ActionKind.Click or ActionKind.Throw or ActionKind.PutDown; // versions 81 and 82: a throw and a put-down have a click's fields
        bool keyed = action.Kind == ActionKind.Keybinding;
        if (!targeted && (action.Target != 0 || action.TargetKind != TargetKind.None || action.TurfX != 0 || action.TurfY != 0 || action.TurfZ != 0)) { why = "a target on an action that has none"; return false; }
        bool dropped = action.Kind == ActionKind.Drop;
        bool mouse = action.IsMouse || action.Kind == ActionKind.DblClick; // version 51: a double click has a mouse action's fields
        if (!clicked && !dropped && !mouse && (action.Button != ClickButton.None || action.Modifiers != ClickModifiers.None || action.IconX != 0 || action.IconY != 0)) { why = "fields that this kind does not use"; return false; }
        if (!clicked && !mouse && action.Aim.HasValue) { why = "an aim on an action that is not a click or a mouse action"; return false; }
        if (action.Aim is ShotAim aim && !aim.InRange) { why = "an aim out of range"; return false; }
        if (!clicked && action.Kind != ActionKind.Hover && action.AimZone != AimZone.None) { why = "an aim zone on an action that is not a click or a hover"; return false; }
        if (!dropped && (action.OverKind != TargetKind.None || action.OverTarget != 0 || action.OverX != 0 || action.OverY != 0 || action.OverZ != 0 || action.DragTime != 0)) { why = "a drop's fields on another action"; return false; }
        if (!keyed && (action.Keybinding != KeybindingCode.None || action.KeyUp)) { why = "a keybinding on an action that is not one"; return false; }
        if (action.Kind is not (ActionKind.DevCommand or ActionKind.Topic or ActionKind.Say or ActionKind.Emote or ActionKind.Verb or ActionKind.PopupClose or ActionKind.ContextPick) && action.Text != null) { why = "text on an action that has none"; return false; }
        if (action.Kind != ActionKind.Say && (action.SayType != null || action.SayChannel != null || action.SayVisible)) { why = "a say's fields on another action"; return false; }
        if (action.Kind != ActionKind.RadialPick && (action.RadialMenu != 0 || action.RadialElement != 0)) { why = "a radial pick's fields on another action"; return false; }
        bool ui = action.Kind is ActionKind.UiAct or ActionKind.UiClose;
        if (!ui && (action.UiWindow != 0 || action.UiAction != null || action.UiParams != null)) { why = "a UI action's fields on another action"; return false; }
        switch (action.Kind)
        {
            case ActionKind.UiAct: // version 16: the window, the action, the params; whether the window is the player's is tgui_Topic's check
            case ActionKind.UiClose:
                if (action.UiWindow < 1 || action.UiWindow > Limits.MaxUiWindows) { why = "a UI action on a window out of range"; return false; }
                if (action.Kind == ActionKind.UiAct && string.IsNullOrEmpty(action.UiAction)) { why = "a UI act without an action"; return false; }
                if (action.Kind == ActionKind.UiClose && (action.UiAction != null || action.UiParams != null)) { why = "a UI close with an action"; return false; }
                if (action.UiParams != null && action.UiParams.Kind != UiValueKind.Object) { why = "a UI act whose params are not an object"; return false; }
                return true;
            case ActionKind.Rest:
            case ActionKind.ToggleMoveIntent:
            case ActionKind.ToggleCombatMode:
            case ActionKind.Unhover: // version 14: untargeted
                return true;
            case ActionKind.RadialPick: // version 15: the menu and the element; whether the menu is shown to the player is ClientSeam's check
                if (action.RadialMenu < 1) { why = "a radial pick without a menu"; return false; }
                if (action.RadialElement < 0 || action.RadialElement > Limits.MaxRadialElements) { why = "a radial pick of an element out of range"; return false; }
                return true;
            case ActionKind.Drop: // version 17: two targets, a button, modifiers and an icon pixel; whether both are the player's to see is the command phase's check
                if (!ValidTarget(action.EffectiveTargetKind, action.Target, action.TurfX, action.TurfY, action.TurfZ, out why)) return false;
                if (!ValidTarget(action.OverKind, action.OverTarget, action.OverX, action.OverY, action.OverZ, out why)) return false;
                if (action.Button is not (ClickButton.Left or ClickButton.Right or ClickButton.Middle)) { why = "a drop without a button"; return false; }
                if ((action.Modifiers & ~ClickModifiers.All) != 0) { why = "unknown drop modifiers"; return false; }
                if (action.IconX > Limits.MaxIconPixel || action.IconY > Limits.MaxIconPixel) { why = "an icon pixel over " + Limits.MaxIconPixel; return false; }
                return true;
            case ActionKind.Throw: // version 81: a plain left click's fields on the world
            case ActionKind.PutDown: // version 82: the same
                if (action.Button != ClickButton.Left || action.Modifiers != ClickModifiers.None) { why = "a throw or a put-down that is not a plain left click"; return false; }
                if (action.EffectiveTargetKind == TargetKind.Screen) { why = "a throw or a put-down at the screen"; return false; }
                goto case ActionKind.Click;
            case ActionKind.Click:
            case ActionKind.Hover:
                switch (action.EffectiveTargetKind)
                {
                    case TargetKind.Entity:
                    case TargetKind.Screen:
                        if (action.Target <= 0) { why = "a target that is not a positive id"; return false; }
                        if (action.TurfX != 0 || action.TurfY != 0 || action.TurfZ != 0) { why = "turf coordinates on an id target"; return false; }
                        break;
                    case TargetKind.Ceiling:
                    case TargetKind.Turf:
                        if (action.Target != 0) { why = "an id on a turf target"; return false; }
                        if (action.TurfX < 1 || action.TurfX > Limits.MaxMapSize || action.TurfY < 1 || action.TurfY > Limits.MaxMapSize || action.TurfZ < 1 || action.TurfZ > Limits.MaxMapLevels) { why = "a turf outside the map's range"; return false; }
                        break;
                    default:
                        why = "a click or hover without a target";
                        return false;
                }
                if (action.AimZone > AimZone.Last) { why = "an unknown aim zone"; return false; }
                if (action.Kind == ActionKind.Hover)
                {
                    // version 95 (slice T4b): the zone looked at, on an entity only
                    if (action.AimZone != AimZone.None && action.EffectiveTargetKind != TargetKind.Entity) { why = "an aim zone on a hover that is not on an entity"; return false; }
                    return true;
                }
                if (action.Button is not (ClickButton.Left or ClickButton.Right or ClickButton.Middle)) { why = "a click without a button"; return false; }
                if ((action.Modifiers & ~ClickModifiers.All) != 0) { why = "unknown click modifiers"; return false; }
                if (action.IconX > Limits.MaxIconPixel || action.IconY > Limits.MaxIconPixel) { why = "an icon pixel over " + Limits.MaxIconPixel; return false; }
                return true;
            case ActionKind.MouseDown: // version 34: a target or none, a button, modifiers, an icon pixel, an aim
            case ActionKind.MouseDrag:
            case ActionKind.MouseUp:
            case ActionKind.MouseWheel: // version 70: the button byte is the wheel's turn
                if (action.EffectiveTargetKind != TargetKind.None && !ValidTarget(action.EffectiveTargetKind, action.Target, action.TurfX, action.TurfY, action.TurfZ, out why)) return false;
                if (action.EffectiveTargetKind == TargetKind.None && (action.Target != 0 || action.TurfX != 0 || action.TurfY != 0 || action.TurfZ != 0)) { why = "a mouse action over nothing with a target"; return false; }
                if (action.Button is not (ClickButton.Left or ClickButton.Right or ClickButton.Middle)) { why = "a mouse action without a button"; return false; }
                if ((action.Modifiers & ~ClickModifiers.All) != 0) { why = "unknown mouse modifiers"; return false; }
                if (action.IconX > Limits.MaxIconPixel || action.IconY > Limits.MaxIconPixel) { why = "an icon pixel over " + Limits.MaxIconPixel; return false; }
                return true;
            case ActionKind.DblClick: // version 51: BYOND's DblClick, a target, a button, modifiers, an icon pixel, no aim
                if (!ValidTarget(action.EffectiveTargetKind, action.Target, action.TurfX, action.TurfY, action.TurfZ, out why)) return false;
                if (action.Aim.HasValue) { why = "an aim on a double click"; return false; }
                if (action.Button is not (ClickButton.Left or ClickButton.Right or ClickButton.Middle)) { why = "a double click without a button"; return false; }
                if ((action.Modifiers & ~ClickModifiers.All) != 0) { why = "unknown double click modifiers"; return false; }
                if (action.IconX > Limits.MaxIconPixel || action.IconY > Limits.MaxIconPixel) { why = "an icon pixel over " + Limits.MaxIconPixel; return false; }
                return true;
            case ActionKind.DevGasProbe:
                if (action.EffectiveTargetKind != TargetKind.Turf) { why = "a dev gas probe whose target is not a turf"; return false; }
                if (action.TurfX < 1 || action.TurfX > Limits.MaxMapSize || action.TurfY < 1 || action.TurfY > Limits.MaxMapSize || action.TurfZ < 1 || action.TurfZ > Limits.MaxMapLevels) { why = "a turf outside the map's range"; return false; }
                return true;
            case ActionKind.Keybinding:
                if (action.Keybinding < KeybindingCode.SwapHands || action.Keybinding > KeybindingCode.Last) { why = "a keybinding code out of range"; return false; }
                return true;
            case ActionKind.DevCommand:
                if (string.IsNullOrEmpty(action.Text) || Encoding.UTF8.GetByteCount(action.Text) > Limits.MaxDevCommandBytes) { why = "a dev command with no text or too much"; return false; }
                return true;
            case ActionKind.Topic: // version 24: a chat link's href; whether the session was sent it is ReceiveAction's check
                if (string.IsNullOrEmpty(action.Text) || Encoding.UTF8.GetByteCount(action.Text) > Limits.MaxTopicHrefBytes) { why = "a chat link with no href or too long a one"; return false; }
                return true;
            case ActionKind.Say: // version 38: tgui-say's message type, its channel, the entry; what they mean is tgui_say.on_message's check
                if (!SayTypes.IsKnown(action.SayType)) { why = "a say of an unknown type"; return false; }
                if (action.SayChannel != null && Encoding.UTF8.GetByteCount(action.SayChannel) > Limits.MaxSayWordBytes) { why = "a say channel too long"; return false; }
                if (action.Text != null && (action.Text.Length > Limits.MaxSayEntryChars || Encoding.UTF8.GetByteCount(action.Text) > Limits.MaxSayEntryBytes)) { why = "a say entry too long"; return false; }
                return true;
            case ActionKind.Emote: // version 39: an emote key; whether an emote has it is /mob/proc/emote's check
                if (!EmoteKeys.IsValid(action.Text)) { why = "an emote with no valid key"; return false; }
                return true;
            case ActionKind.Verb: // version 78: a verb path; whether the mob has it is ClientSeam.RunVerb's check
                if (string.IsNullOrEmpty(action.Text) || Encoding.UTF8.GetByteCount(action.Text) > Limits.MaxVerbPathBytes) { why = "a verb with no path or too long a one"; return false; }
                return true;
            case ActionKind.ContextPick: // version 86 (DV503): a target and a label; whether the entry is offered for it now is RadialExtras.Pick's check
                if (!ValidTarget(action.EffectiveTargetKind, action.Target, action.TurfX, action.TurfY, action.TurfZ, out why)) return false;
                if (string.IsNullOrEmpty(action.Text) || Encoding.UTF8.GetByteCount(action.Text) > Limits.MaxContextLabelBytes) { why = "a context pick with no label or too long a one"; return false; }
                return true;
            case ActionKind.PopupClose: // version 78 (DV387): a popup's name; whether the client has it open is ClientSeam.ClosePopup's check
                if (string.IsNullOrEmpty(action.Text) || Encoding.UTF8.GetByteCount(action.Text) > Limits.MaxPopupNameBytes) { why = "a popup close with no name or too long a one"; return false; }
                return true;
            default:
                why = "an unknown kind";
                return false;
        }
    }

    // Version 17: one target of a drop by its kind (the rule of a click's target above).
    private static bool ValidTarget(TargetKind kind, long id, int x, int y, int z, out string why)
    {
        why = null;
        switch (kind)
        {
            case TargetKind.Entity:
            case TargetKind.Screen:
                if (id <= 0) { why = "a target that is not a positive id"; return false; }
                if (x != 0 || y != 0 || z != 0) { why = "turf coordinates on an id target"; return false; }
                return true;
            case TargetKind.Ceiling:
            case TargetKind.Turf:
                if (id != 0) { why = "an id on a turf target"; return false; }
                if (x < 1 || x > Limits.MaxMapSize || y < 1 || y > Limits.MaxMapSize || z < 1 || z > Limits.MaxMapLevels) { why = "a turf outside the map's range"; return false; }
                return true;
            default:
                why = "a drop without a target";
                return false;
        }
    }

    private enum ActionTake : byte
    {
        Ok,
        OverallLimit,
        KindLimit,
    }

    // The overall action bucket and the kind's own (D40: per-type rate limits).
    private ActionTake TakeActionTokens(Session s, ActionKind kind)
    {
        long tick = World.Tick;
        if (!s.ActionBucket.Take(tick)) return ActionTake.OverallLimit;
        bool taken = kind switch
        {
            ActionKind.Click or ActionKind.Throw or ActionKind.PutDown => s.ClickBucket.Take(tick),
            ActionKind.DevCommand => s.DevBucket.Take(tick),
            ActionKind.DevGasProbe => s.DevProbeBucket.Take(tick),
            ActionKind.Keybinding => s.KeybindingBucket.Take(tick),
            ActionKind.Hover => s.HoverBucket.Take(tick),
            // Version 14: an Unhover takes only the overall bucket. Dropping it would leave tg's hover state on (the suit display's
            // percentage); the client sends hovers and unhovers at most HoverInterval apart, and one a tick is applied.
            ActionKind.Unhover => true,
            // Version 15: a radial pick takes the click bucket (it is BYOND's click on a slice or the centre).
            ActionKind.RadialPick => s.ClickBucket.Take(tick),
            // Version 16: a tgui act takes the click bucket (it is a click on a window's button); a close only the overall one (as Unhover).
            ActionKind.UiAct => s.ClickBucket.Take(tick),
            ActionKind.UiClose => true,
            // Version 17: a drop takes the click bucket (it is BYOND's release of a press, like a click's).
            ActionKind.Drop => s.ClickBucket.Take(tick),
            // Version 24: a chat link takes the click bucket (it is a click on a link; tg's own topic limits are per minute and second).
            ActionKind.Topic => s.ClickBucket.Take(tick),
            // Version 86: a context pick takes the click bucket (it stands for one).
            ActionKind.ContextPick => s.ClickBucket.Take(tick),
            // Version 51: a double click takes only the overall bucket (it always comes with its press's click, which the click bucket counts).
            ActionKind.DblClick => true,
            // Version 34: a press and a release take only the overall bucket (each comes with a click or ends one, which the click bucket counts).
            ActionKind.MouseDown or ActionKind.MouseUp => true,
            // Version 70: a wheel turn takes the click bucket (a fast spin sends many).
            ActionKind.MouseWheel => s.ClickBucket.Take(tick),
            _ => s.ToggleBucket.Take(tick),
        };
        return taken ? ActionTake.Ok : ActionTake.KindLimit;
    }

    // A protocol violation: Limits.MaxStrikes within Limits.StrikeWindowTicks close the session.
    private void Strike(Session s, string why)
    {
        long tick = World.Tick;
        s.Strikes++;
        s.StrikeTicks[s.StrikeCursor] = tick;
        s.StrikeCursor = (s.StrikeCursor + 1) % s.StrikeTicks.Length;
        long oldest = s.StrikeTicks[s.StrikeCursor]; // the oldest of the last MaxStrikes strikes
        if (s.Strikes >= Limits.MaxStrikes && tick - oldest < Limits.StrikeWindowTicks)
        {
            CloseSession(s, DisconnectReason.ProtocolViolation,
                string.Create(CultureInfo.InvariantCulture, $"{Limits.MaxStrikes} protocol violations within {Limits.StrikeWindowTicks / Anomaly.Core.Kernel.World.Fps} s (the last: {why})."));
        }
    }

    /// <summary>The most ticks a stall credits (<see cref="CreditStall"/>): the strike window; a longer stall times the connections out anyway.</summary>
    public const long MaxStallCreditTicks = Limits.StrikeWindowTicks;

    /// <summary>Ticks credited by the stall credits so far (a metric; 0 without stalls).</summary>
    public long StallCreditedTicks { get; private set; }

    // A tick that starts latenessMs late by whole ticks (the previous tick overran: a GC pause or a slow step) handles the
    // commands that the clients sent in real time during the stall: each session's buckets get those ticks' tokens for this
    // tick's poll (TokenBucket.CreditStall), so a stall does not kick its players for flooding.
    private void CreditStall(double latenessMs)
    {
        long delayed = Math.Min(MaxStallCreditTicks, (long)Math.Floor(latenessMs / (Anomaly.Core.Kernel.World.TickLag * 100.0)));
        if (delayed <= 0) return;
        StallCreditedTicks += delayed;
        long tick = World.Tick;
        foreach (Session s in _byConnection.Values)
        {
            s.MotionBucket.CreditStall(tick, delayed);
            s.ActionBucket.CreditStall(tick, delayed);
            s.ToggleBucket.CreditStall(tick, delayed);
            s.ClickBucket.CreditStall(tick, delayed);
            s.DevBucket.CreditStall(tick, delayed);
            s.KeybindingBucket.CreditStall(tick, delayed);
            s.HoverBucket.CreditStall(tick, delayed);
        }
    }

    // A command over its bucket is dropped; FloodDropLimit drops within the strike window close the session.
    private void RateLimited(Session s)
    {
        long tick = World.Tick;
        s.RateLimited++;
        s.FloodTicks.Enqueue(tick);
        while (s.FloodTicks.Count > 0 && tick - s.FloodTicks.Peek() >= Limits.StrikeWindowTicks)
            s.FloodTicks.Dequeue();
        if (s.FloodTicks.Count >= FloodDropLimit)
            CloseSession(s, DisconnectReason.Flooding, "Too many commands: the rate limit was exceeded.");
    }

    // ---- disconnects ----

    private void OnDisconnected(IConnection connection, TransportDisconnect info)
    {
        if (!_byConnection.Remove(connection.Id, out Session s)) return;
        if (s.State is SessionState.Leaving or SessionState.Closed) return;
        _out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"leave #{s.Ordinal} {s.Key} at tick {World.Tick} ({info.Kind})"));
        SessionGone(s);
    }

    /// <summary>
    /// Closes <paramref name="s"/> from the server's side (strikes, flooding, a replaced session, a kick, a refused login,
    /// the world deleting the client): the client gets <paramref name="reason"/>, and the session leaves the world in the
    /// next tick.
    /// </summary>
    private void CloseSession(Session s, DisconnectReason reason, string text)
    {
        if (s.State == SessionState.Closed) return;
        CloseConnection(s, reason, text);
        _out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"closed #{s.Ordinal} {s.Key} at tick {World.Tick}: {reason} ({text})"));
        SessionGone(s);
    }

    private void CloseConnection(Session s, DisconnectReason reason, string text)
    {
        if (s.CloseReason == DisconnectReason.None)
        {
            s.CloseReason = reason;
            s.CloseText = text;
        }
        if (s.Connection != null && s.Connection.IsConnected)
            s.Connection.Disconnect(_codec.Encode(new DisconnectInfo(s.CloseReason, Truncate(s.CloseText, Limits.MaxDisconnectTextBytes))));
    }

    // The session's connection is gone: a Join not applied yet is dropped; a session in the world gets its Leave.
    private void SessionGone(Session s)
    {
        switch (s.State)
        {
            case SessionState.Joining:
                _queue.DropSession(s.Ordinal);
                s.State = SessionState.Closed;
                break;
            case SessionState.LoggingIn:
            case SessionState.Joined:
                _queue.DropSession(s.Ordinal);
                _queue.Enqueue(new QueuedCommand(World.Tick + 1, s.Ordinal, QueuedCommand.LeaveSeq, CommandKind.Leave));
                s.State = SessionState.Leaving;
                break;
        }
    }

    // Closes requested from inside the world during the tick (ClientDeleted, a kick, a refused login).
    private void ProcessCloses()
    {
        foreach (Session s in _sessions)
        {
            if (!s.CloseAfterTick) continue;
            s.CloseAfterTick = false;
            if (_replay) continue; // a replay's commands come only from the log, which holds the Leave the live run queued here
            if (s.State is SessionState.Joining or SessionState.LoggingIn or SessionState.Joined)
                CloseSession(s, s.CloseReason == DisconnectReason.None ? DisconnectReason.ClientDeleted : s.CloseReason,
                    s.CloseText ?? "The world ended the connection.");
        }
    }

    // ---- sending ----

    private void SendDeltas(DeltaFrame frame)
    {
        foreach (Session s in _sessions) // version 79: the tick's new runtime resources go before the deltas that may name them
        {
            if (s.State == SessionState.Joined)
                SendRuntimeResources(s);
        }
        SendDeltaMessages(frame);
        RecordClickLatencies();
    }

    // Version 4, the server's part of click_ms (beside the client's ack_ms): from the Click's arrival on the host to the end of
    // the Delta sends of the tick that applied it (the ticks it waited for its frame, the step with tg's chain, the replication,
    // the encode and the send). The click's consequences (the item in the hand, the opened door) are in that tick's Delta.
    private void RecordClickLatencies()
    {
        if (_clickStamps.Count == 0) return;
        long now = Stopwatch.GetTimestamp();
        foreach (long stamp in _clickStamps)
        {
            ClickLatency.Record(Math.Max(0, (now - stamp) * 1_000_000 / Stopwatch.Frequency));
            ClicksTimed++;
        }
        _clickStamps.Clear();
    }

    /// <summary>Version 4: the server's part of click_ms, the latency histogram of the clicks the world applied (microseconds from arrival to the end of the Delta sends of their tick; run totals).</summary>
    public LatencyHistogram ClickLatency { get; } = new();

    /// <summary>Version 4: clicks in <see cref="ClickLatency"/>.</summary>
    public long ClicksTimed { get; private set; }

    private readonly List<long> _clickStamps = new();

    private void SendDeltaMessages(DeltaFrame frame)
    {
        if (frame.Body.IsEmpty) return; // no replicator
        foreach (Session s in _sessions)
        {
            if (s.State != SessionState.Joined || s.JoinedTick >= frame.Tick) continue;
            // The session's private block (D40: its own motion state, the jitter buffer depth, its client.screen), built after
            // the tick outside the world, appended after the shared body.
            PrivateFrame own = _replicator.BuildPrivate(frame.Tick, PrivateInputsOf(s));
            int need = _codec.MaxDeltaHeaderBytes + frame.Body.Length + own.Bytes.Length;
            if (_deltaBuffer.Length < need) _deltaBuffer = new byte[Math.Max(need, _deltaBuffer.Length * 2)];
            int n = _codec.EncodeDelta(frame.Tick, s.LastAppliedInputSeq, _locomotion.OwnBody(World, s.OwnEntity), frame.Body.Span, own.Bytes.Span, _deltaBuffer);
            Send(s, Channel.State, _deltaBuffer.AsSpan(0, n));
            SendMotion(s);
            SendRadials(s);
            SendUiWindows(s);
            SendTooltip(s);
            SendCameraShake(s);
            SendHeldMotion(s);
            SendRadioChannels(s);
            SendMinimap(s); // version 67
            SendMinimapMobs(s, frame.Tick); // version 68
            SendBuildPreview(s); // version 70
            SendGunSight(s); // version 92
            SendWeather(s); // version 84
        }
    }

    // Version 42: the capsule mobs' positions of the tick on the unreliable channel, right after the Delta.
    private readonly List<MotionMessage> _motion = new();
    private readonly byte[] _motionBuffer = new byte[Limits.MaxMotionBytes];

    private void SendMotion(Session s)
    {
        _replicator.BuildMotion(s.Ordinal, _motion);
        foreach (MotionMessage message in _motion)
            Send(s, Channel.Input, _motionBuffer.AsSpan(0, Codec.Encode(message, _motionBuffer)));
    }

    // Version 40: the prefixes of the session's worn headset's channels when they changed this tick (the chat window's Radio list).
    private void SendRadioChannels(Session s)
    {
        RadioChannelsMessage channels = _replicator.TakeRadioChannels(s.Ordinal);
        if (channels == null) return;
        Send(s, Channel.State, Codec.Encode(channels));
    }

    // Version 15 (M4c slice 4, DV40): the session's radial menus that opened, turned their page or closed, right after the Delta or
    // Snapshot whose private tables carry their pictures (the same State channel keeps the order).
    // Version 16 (M5 slice 3, DV44): the session's tgui windows that updated or closed this tick, after the Delta or Snapshot.
    private void SendUiWindows(Session s)
    {
        foreach (UiWindowMessage window in _replicator.TakeUiWindows(s.Ordinal))
        {
            if (window.Interface == Limits.BrowseUiInterface && window.Data?.Get("html") is UiValue html)
                s.ChatLinks.Record(string.Concat(System.Linq.Enumerable.Select(html.Items, p => p.Text))); // DV216: a browse() page's links are tg's chat links (DV59)
            byte[] bytes;
            try
            {
                bytes = Codec.Encode(window);
            }
            catch (ProtocolException)
            {
                s.UiWindowsDropped++; // a payload over the caps (Limits.MaxUiWindowBytes, MaxUiValues): the window misses this update
                continue;
            }
            Send(s, Channel.State, bytes);
            s.UiWindowsSent++;
        }
    }

    // Version 19 (DV47): the session's tooltip shown or hidden this tick, after the Delta or Snapshot.
    private void SendTooltip(Session s)
    {
        TooltipMessage tooltip = _replicator.TakeTooltip(s.Ordinal);
        if (tooltip == null) return;
        Send(s, Channel.State, Codec.Encode(tooltip));
        s.TooltipsSent++;
    }

    // Version 21: the view's shake path of this tick (tg's shake_camera on the player's client), after the Delta.
    private void SendCameraShake(Session s)
    {
        CameraShakeMessage shake = _replicator.TakeCameraShake(s.Ordinal);
        if (shake == null) return;
        Send(s, Channel.State, Codec.Encode(shake));
        s.CameraShakesSent++;
    }

    // Version 54: the own gun's load, unload or rack of this tick, after the Delta.
    private void SendHeldMotion(Session s)
    {
        HeldMotionMessage held = _replicator.TakeHeldMotion(s.Ordinal);
        if (held != null)
            Send(s, Channel.State, Codec.Encode(held));
    }

    private void SendRadials(Session s)
    {
        foreach (RadialMenuMessage radial in _replicator.TakeRadials(s.Ordinal))
        {
            Send(s, Channel.State, Codec.Encode(radial));
            s.RadialsSent++;
        }
    }

    // What the replicator needs of a session for its private block: the capsule state from the locomotion seam (read outside
    // the world, after the step) and the jitter buffer's depth.
    private PrivateInputs PrivateInputsOf(Session s)
    {
        MotionState motion = default;
        MotionParams parameters = default;
        bool has = s.OwnEntity != 0 && _locomotion.TryGetState(World, s.OwnEntity, out motion, out parameters);
        long clientId = s.Client != null ? s.Client.dm_id : 0;
        return new PrivateInputs(s.Ordinal, clientId, s.OwnEntity, s.LastAppliedInputSeq, has, motion, parameters, (byte)Math.Min(s.Jitter.Depth, byte.MaxValue))
        {
            ActionsDone = s.ActionsReceived - (uint)s.PendingActions.Count,
            Access = s.Access,
            HandsBlocked = s.HandsBlocked,
            Body = s.OwnEntity != 0 ? _locomotion.OwnBody(World, s.OwnEntity) : 0,
        };
    }

    // Version 79 (silicons SI4d): the world's runtime resources the session's client has not had yet, before any message that names them.
    private void SendRuntimeResources(Session s)
    {
        var state = Anomaly.Core.DmShim.DmWorldState.Of(World);
        List<string> order = state.RuntimeResourceOrder;
        while (s.RuntimeResourcesSent < order.Count)
        {
            string path = order[s.RuntimeResourcesSent++];
            byte[] png = state.RuntimeResources[path];
            if (png.Length <= Limits.MaxRuntimeResourceBytes)
                Send(s, Channel.State, Codec.Encode(new RuntimeResourceMessage(path, png)));
            else
                _err.WriteLine("warning: a runtime resource of " + png.Length + " bytes is too big to send: " + path);
        }
    }

    private void SendJoin(Session s, JoinMessages messages)
    {
        if (s.State != SessionState.Joined) return;
        Send(s, Channel.State, _codec.Encode(new Welcome(s.Ordinal, s.Token, s.JoinedTick, (byte)Anomaly.Core.Kernel.World.Fps, s.OwnEntity)));
        s.RuntimeResourcesSent = 0; // version 79: a join's client has none; they go before the snapshot that may name them
        SendRuntimeResources(s);
        if (messages.MapGrid is { Length: > 0 }) Send(s, Channel.State, messages.MapGrid);
        if (messages.Snapshot is { Length: > 0 }) Send(s, Channel.State, messages.Snapshot);
        SendRadials(s); // version 15: the menus open at the join
        SendUiWindows(s); // version 16: the tgui windows open at the join
        SendTooltip(s); // version 19: the tooltip shown at the join
        s.MinimapLevels.Clear(); // version 67: a join's client has no minimap yet
        s.BuildPreview = BuildPreviewMessage.None; // version 70: nor a build preview
        s.GunSight = GunSightMessage.None; // version 92: nor a gun sight
        s.WeatherKey = null; // version 84: nor the weather
        s.Sounds.Restart(); // version 13: the client's MapGrid cleared its sound path table; the join tick's sounds follow (SendSounds)
        _out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"welcome #{s.Ordinal} {s.Key} mob #{s.OwnEntity} at tick {s.JoinedTick}"));
    }

    private void SendChat()
    {
        foreach (Session s in _sessions)
        {
            if (s.State != SessionState.Joined) continue;
            while (s.Outbox.Count > 0)
                Send(s, Channel.Control, _codec.Encode(s.Outbox.Dequeue()));
            while (s.ControlOutbox.Count > 0)
                Send(s, Channel.Control, s.ControlOutbox.Dequeue());
            while (s.SpeechOutbox.Count > 0)
                Send(s, Channel.Control, s.SpeechOutbox.Dequeue());
        }
    }

    private void Send(Session s, Channel channel, ReadOnlySpan<byte> message)
    {
        if (s.Connection == null || !s.Connection.IsConnected) return;
        s.Connection.Send(channel, message);
        s.BytesSent += message.Length;
        _tickBytesOut += message.Length;
        _totalBytesOut += message.Length;
    }

    // Cuts text to a UTF-8 byte cap at a character boundary (an encoder refuses text over its cap: a sender bug).
    internal static string Truncate(string text, int maxBytes)
    {
        if (text == null || Encoding.UTF8.GetByteCount(text) <= maxBytes) return text;
        int len = Math.Min(text.Length, maxBytes);
        while (len > 0 && Encoding.UTF8.GetByteCount(text.AsSpan(0, len)) > maxBytes) len--;
        if (len > 0 && char.IsHighSurrogate(text[len - 1])) len--;
        return text.Substring(0, len);
    }
}
