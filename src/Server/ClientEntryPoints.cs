using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;

namespace Anomaly.Server;

/// <summary>
/// What the host knows about a connecting client while tg's <c>client/New</c> (and the <c>mob/Login</c> it runs) may still
/// be sleeping: <c>ClientSeam.Connect</c>'s <see cref="ClientConnection"/> as the host reads it (scope risk 11). Checked
/// after every <c>World.Step</c>; the Welcome goes out once it is <see cref="Done"/> and <see cref="Accepted"/>.
/// </summary>
public interface ILoginHandle
{
    /// <summary>The new client (registered before its <c>New</c> ran).</summary>
    client Client { get; }

    /// <summary>True once <c>client/New</c> has returned (or failed).</summary>
    bool Done { get; }

    /// <summary>The connected mob <c>client/New</c> returned, null when it refused the connection.</summary>
    mob Mob { get; }

    /// <summary>The runtime error that ended <c>client/New</c>, if any.</summary>
    Exception Error { get; }

    /// <summary>Done, without an error, with a mob.</summary>
    bool Accepted { get; }
}

/// <summary>
/// The host-to-engine half of the client seam (<see cref="ClientSeam"/>: <c>KeyMob</c>, <c>Connect</c>,
/// <c>SetHeldDirections</c>, <c>Disconnect</c>, M1 P1), called by the command phase inside <c>World.Step</c> with the
/// ambient world set. <see cref="TgClientEntryPoints"/> forwards to <see cref="ClientSeam"/>; the interface lets the host
/// tests stand in for the translated client layer (tests/Anomaly.Server.Tests/Server).
/// </summary>
public interface IClientEntryPoints
{
    /// <summary>BYOND's <c>mob.key = key</c> (<see cref="ClientSeam.KeyMob"/>).</summary>
    void KeyMob(mob avatar, string key);

    /// <summary>A player connects from <paramref name="address"/> (BYOND's <c>client.address</c>, which tg's <c>is_localhost</c> reads): the client is made and tg's <c>client/New</c> runs detached (<see cref="ClientSeam.Connect"/>).</summary>
    ILoginHandle Connect(string key, string address);

    /// <summary>The movement keys held now, as BYOND direction bits (<see cref="ClientSeam.SetHeldDirections"/>).</summary>
    void SetHeldDirections(client player, int dirs);

    /// <summary>The player's connection ended: tg's <c>client/Del</c> runs (<see cref="ClientSeam.Disconnect"/>).</summary>
    void Disconnect(client player);

    // ---- M2 (contracts; the host's ActionKind commands, M2 P6): default members forward to the client seam ----

    /// <summary>M2: rest or get up (<see cref="ClientSeam.Rest"/>).</summary>
    void Rest(client player) => ClientSeam.Rest(player);

    /// <summary>M2: walk or run (<see cref="ClientSeam.ToggleMoveIntent"/>).</summary>
    void ToggleMoveIntent(client player) => ClientSeam.ToggleMoveIntent(player);

    /// <summary>M2: combat mode (<see cref="ClientSeam.ToggleCombatMode"/>).</summary>
    void ToggleCombatMode(client player) => ClientSeam.ToggleCombatMode(player);

    /// <summary>M2: a click with tg's params text (<see cref="ClientSeam.Click"/>, <see cref="ClientSeam.ClickParams(string, bool, bool, bool)"/>); from M4a the target may also be a turf or a screen object of the player's <c>client.screen</c>.</summary>
    void Click(client player, atom target, string @params) => ClientSeam.Click(player, target, @params);

    /// <summary>Version 81 (D328): a throw at the target with a click's params text (<see cref="ClientSeam.Throw"/>).</summary>
    void Throw(client player, atom target, string @params) => ClientSeam.Throw(player, target, @params);

    /// <summary>Version 82 (D329): the drop key putting the held item down at the crosshair's point (<see cref="ClientSeam.PutDown"/>).</summary>
    void PutDown(client player, atom target, string @params) => ClientSeam.PutDown(player, target, @params);

    /// <summary>Version 51 (M10x slice C): BYOND's double click with tg's params text (<see cref="ClientSeam.DblClick"/>).</summary>
    void DblClick(client player, atom target, string @params) => ClientSeam.DblClick(player, target, @params);

    // ---- M4a (contracts; the host's version-4 actions, M4a P6) ----

    /// <summary>M4a: a tg keybinding pressed or released (<see cref="ClientSeam.Keybinding"/>).</summary>
    void Keybinding(client player, byte code, bool down) => ClientSeam.Keybinding(player, code, down);

    /// <summary>M4a: the crosshair's target changed (<see cref="ClientSeam.Hover"/>: tg's <c>MouseEntered</c>; version 14: BYOND's <c>MouseExited</c> on the last hovered thing first).</summary>
    void Hover(client player, atom target) => ClientSeam.Hover(player, target);

    /// <summary>Version 14: the pointer left the hovered thing over nothing (<see cref="ClientSeam.Unhover"/>: BYOND's <c>MouseExited</c>).</summary>
    void Unhover(client player) => ClientSeam.Unhover(player);

    /// <summary>Version 15: the player picked an element of a radial menu, or its centre (<see cref="ClientSeam.RadialPick"/>: the slice's or the centre's <c>Click</c>).</summary>
    void RadialPick(client player, long menu, int element) => ClientSeam.RadialPick(player, menu, element);

    /// <summary>Version 16: a message from the player's tgui window (tg's tgui_Topic: <see cref="ClientSeam.TguiMessage"/>).</summary>
    void TguiMessage(client player, string window, string type, DmList payload) => ClientSeam.TguiMessage(player, window, type, payload);

    /// <summary>Version 17: the player dragged <paramref name="dragged"/> and released it over <paramref name="over"/> (<see cref="ClientSeam.Drop"/>: tg's <c>/client/MouseDrag</c> and <c>/client/MouseDrop</c>).</summary>
    void Drop(client player, atom dragged, atom over, string dragParams, string dropParams, int dragDeciseconds) => ClientSeam.Drop(player, dragged, over, dragParams, dropParams, dragDeciseconds);

    /// <summary>Version 24 (D154): the player clicked the chat link <paramref name="href"/> (<see cref="ClientSeam.Topic"/>: tg's <c>/client/Topic</c>).</summary>
    void Topic(client player, string href) => ClientSeam.Topic(player, href);

    /// <summary>Version 38: a message of the player's say window (<see cref="ClientSeam.Say"/>: tg's <c>/datum/tgui_say/proc/on_message</c>).</summary>
    void Say(client player, string type, string channel, string entry, bool visible) => ClientSeam.Say(player, type, channel, entry, visible);

    /// <summary>Version 39: the emote wheel's pick (<see cref="ClientSeam.Emote"/>: tg's emote keybinding, <c>mob.emote(key, intentional = TRUE)</c>).</summary>
    void Emote(client player, string key) => ClientSeam.Emote(player, key);

    /// <summary>Version 86 (DV503): the radial's entry that is no click (<see cref="RadialExtras.Pick"/>: the table's Flip).</summary>
    void ContextPick(client player, atom target, string label) => RadialExtras.Pick(player, target, label);

    /// <summary>Version 100 also (DV697): the world interact key on <paramref name="target"/> (<see cref="WorldInteract.Use"/>: tg's empty-hand click, also with an item held).</summary>
    void Interact(client player, atom target) => WorldInteract.Use(player, target);

    /// <summary>Version 100 also (DV693): a press of hotbar cell <paramref name="cell"/> (<see cref="BeltHotbar.Press"/>: the own human's cells, by tg's procs).</summary>
    void HotbarPress(client player, int cell) => BeltHotbar.Press(player, cell);

    /// <summary>Version 100 also (DV696): an inventory move (<see cref="CarriedStorages.Move"/>: the own item to a carried storage, a worn slot, a hand or the floor, by tg's procs).</summary>
    void InventoryMove(client player, long item, MoveTarget to, long storage, int index) => CarriedStorages.Move(player, item, to, storage, index);

    /// <summary>Version 100 also (DV696): the Tab inventory was opened (<see cref="CarriedStorages.Opened"/>: tg's rustle of the back storage).</summary>
    void InventoryOpen(client player) => CarriedStorages.Opened(player);

    /// <summary>Version 78: the player clicked a command verb of the stat panel (<see cref="ClientSeam.RunVerb"/>: BYOND runs the verb with usr the mob).</summary>
    void Verb(client player, string path) => ClientSeam.RunVerb(player, path);

    /// <summary>Version 78 (DV387): the player closed a popup window (<see cref="ClientSeam.ClosePopup"/>: tg's <c>handle_popup_close</c>).</summary>
    void ClosePopup(client player, string name) => ClientSeam.ClosePopup(player, name);

    /// <summary>Version 34: a button went down (<see cref="ClientSeam.MouseDown"/>: tg's <c>/client/MouseDown</c>).</summary>
    void MouseDown(client player, atom target, string @params) => ClientSeam.MouseDown(player, target, @params);

    /// <summary>Version 34: the mouse moved with a button held (<see cref="ClientSeam.MouseDrag"/>: tg's <c>/client/MouseDrag</c>).</summary>
    void MouseDrag(client player, atom over, string @params) => ClientSeam.MouseDrag(player, over, @params);

    /// <summary>Version 34: the button went up (<see cref="ClientSeam.MouseUp"/>: tg's <c>/client/MouseUp</c>).</summary>
    void MouseUp(client player, atom target, string @params) => ClientSeam.MouseUp(player, target, @params);

    /// <summary>Version 70: the wheel turned (<see cref="ClientSeam.MouseWheel"/>: BYOND's <c>MouseWheel</c>, tg's <c>MouseWheelOn</c>).</summary>
    void MouseWheel(client player, atom target, int deltaY, string @params) => ClientSeam.MouseWheel(player, target, deltaY, @params);
}

/// <summary>The translated client layer's entry points (<see cref="ClientSeam"/>, M1 P1).</summary>
public sealed class TgClientEntryPoints : IClientEntryPoints
{
    /// <summary>The one instance (stateless).</summary>
    public static readonly TgClientEntryPoints Instance = new();

    private TgClientEntryPoints()
    {
    }

    public void KeyMob(mob avatar, string key) => ClientSeam.KeyMob(avatar, key);

    // The host never passes the player's real address or a computer id into the world (D67, and a replay must give the
    // same world): every client gets ClientSeam.Connect's defaults.
    public ILoginHandle Connect(string key, string address) => new Handle(ClientSeam.Connect(key, address));

    public void SetHeldDirections(client player, int dirs) => ClientSeam.SetHeldDirections(player, dirs);

    public void Disconnect(client player) => ClientSeam.Disconnect(player);

    private sealed class Handle : ILoginHandle
    {
        private readonly ClientConnection _connection;

        public Handle(ClientConnection connection) => _connection = connection ?? throw new InvalidOperationException("ClientSeam.Connect returned no connection");

        public client Client => _connection.Client;
        public bool Done => _connection.Done;
        public mob Mob => _connection.Mob;
        public Exception Error => _connection.Error;
        public bool Accepted => _connection.Accepted;
    }
}
