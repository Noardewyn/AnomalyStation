using System.Linq;
using System.Globalization;
using System.Text;
using Anomaly.Client.Shared.World;

namespace Anomaly.Client.Shared;

// Owner: M2 P8 (client play). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>What a key or a mouse button can be bound to (D53, D70: every key is remappable).</summary>
/// <summary>D308: tg's sound_tts_radio choices.</summary>
public enum RadioSpeech : byte
{
    All,
    Departmental,
    Off,
}

public enum PlayAction : byte
{
    MoveForward,
    MoveBack,
    MoveLeft,
    MoveRight,

    /// <summary>tg's <c>/datum/keybinding/living/rest</c> (<c>U</c> in tg): lie down, or get up [C, SS14's ToggleKnockdown].</summary>
    Rest,

    /// <summary>
    /// tg's <c>toggle_move_intent_alternative</c>: press to switch between walking and running [Shift, ui-overhaul.md section 3; C before]. On Shift
    /// alone it acts when Shift is let go with nothing else pressed meanwhile (<see cref="ModifierTap"/>), so Shift+left click and the Shift pairs
    /// do not switch it.
    /// </summary>
    ToggleMoveIntent,

    /// <summary>tg's <c>/datum/keybinding/living/toggle_combat_mode</c> (<c>F</c>).</summary>
    ToggleCombatMode,

    /// <summary>M4a (D124; was <c>Help</c> in M2): tg's left click on whatever is under the crosshair (<c>ClickOn</c>: an empty hand's help on a mob, take, use, open).</summary>
    PrimaryClick,

    /// <summary>M4a (D124; was <c>Shove</c> in M2): tg's right click. A tap sends it (the shove on a mob with an empty hand, weld shut, open a bag); a hold opens the radial.</summary>
    SecondaryClick,

    /// <summary>Opens and closes the dev console (D73).</summary>
    Console,

    /// <summary>Opens and closes the options panel (D70).</summary>
    Options,

    /// <summary>M4b slice 3 (the owner, 2026-10-13): opens and closes the ImGui dev menu (the gas overlay, view switches) [\, the | key; F8 before D137].</summary>
    DevMenu,

    /// <summary>F1: first person, free fly, orbit.</summary>
    CameraMode,

    /// <summary>F5 (the owner, 2026-10-16): first person, the third person behind the mob, the third person's free look (the mob walks apart from the camera).</summary>
    ThirdPersonView,

    /// <summary>F2: the ceiling.</summary>
    Ceiling,

    /// <summary>F3: the names overlay.</summary>
    Names,

    /// <summary>F4: the FPS and the connection's numbers at the top right (D138; the debug HUD's numbers are the dev menu's Stats tab, D137).</summary>
    DebugHud,

    /// <summary>M3: the camera flashlight, a client-side dev toggle until the flashlight item (M4a).</summary>
    Flashlight,

    /// <summary>M4a (D124): tg's middle click, <c>MiddleClickOn</c>: swap hands.</summary>
    MiddleClick,

    /// <summary>M4a (D124): tg's <c>/datum/keybinding/mob/drop_item</c> [Q].</summary>
    DropItem,

    /// <summary>
    /// [Ctrl+Q, SS14's ThrowItemInHand; R before] D328: throws the held thing at once where the crosshair points (tg's hold throw mode for that one click). It
    /// was tg's <c>/datum/keybinding/living/toggle_throw_mode</c> (M4a, D124); the HUD's throw button and the held <see cref="HoldThrow"/> still turn
    /// throw mode on. The name stays, so saved key layouts keep their key.
    /// </summary>
    ThrowMode,

    /// <summary>M4a (D124): tg's <c>/datum/keybinding/mob/swap_hands</c> [X].</summary>
    SwapHand,

    /// <summary>M4a (D124): tg's <c>/datum/keybinding/mob/activate_inhand</c> [Z].</summary>
    ActivateInHand,

    /// <summary>M4a (D124): tg's <c>/datum/keybinding/human/quick_equip</c> [E].</summary>
    QuickEquip,

    /// <summary>M4a (D124): tg's <c>/datum/keybinding/living/hold_throw_mode</c> [unbound since DV422: Space is it for a mob that cannot change level].</summary>
    HoldThrow,

    /// <summary>M4a (D70, interaction.md 4.4): cursor mode, the mouse over the HUD and the windows [Tab].</summary>
    CursorMode,

    /// <summary>
    /// M4a: examine what is under the crosshair: tg's shift-click (<c>ShiftClickOn</c>, examinate) [Shift+left click,
    /// SS14's ExamineEntity; V before]. Bound to a mouse button, it is that button's press as a left click with Shift (GameScene.OnMouseButton).
    /// </summary>
    Examine,

    /// <summary>D137: the window fullscreen or windowed [F11, SS14's ToggleFullscreen; F12 before].</summary>
    Fullscreen,

    /// <summary>Pulling (protocol version 20): tg's <c>/datum/keybinding/mob/stop_pulling</c> [H, SS14's ReleasePulledObject, and Delete, as in tg].</summary>
    StopPulling,

    /// <summary>M8z (protocol version 37): tg's <c>/datum/keybinding/living/resist</c> [B].</summary>
    Resist,

    /// <summary>M9 (protocol version 38): tg's <c>/datum/keybinding/client/communication/say</c> [T]: the say window on Say.</summary>
    Say,

    /// <summary>M9: tg's <c>communication/radio</c> [;, SS14's FocusRadioWindow; Y before]: the say window on Radio.</summary>
    Radio,

    /// <summary>M9: tg's <c>communication/me</c> [Shift+', SS14's FocusEmote; M before]: the say window on Me.</summary>
    Me,

    /// <summary>M9: tg's <c>communication/ooc</c> [[, SS14's FocusOOCWindow; O before]: the say window on OOC.</summary>
    Ooc,

    /// <summary>M9: tg's <c>communication/pray</c> [P]: the say window on Pray.</summary>
    Pray,

    /// <summary>M9 slice B (protocol version 39, D256): the emote wheel [Y, SS14's OpenEmotesMenu; J before] (tg has no emote key).</summary>
    EmoteWheel,

    /// <summary>M9 slice C (D247): the big chat window [Shift+T].</summary>
    ChatWindow,

    /// <summary>
    /// ui-overhaul.md section 6, slice E: tg's crafting menu [G, SS14's OpenCraftingMenu]: the click of tg's crafting button, which the human HUD no
    /// longer draws (<see cref="LiveHud.ButtonEntry"/>). It took the place of M9 slice F's give key (<c>give_item</c>, read and ignored in an old file);
    /// "Offer item" is in the radial now.
    /// </summary>
    CraftingMenu,

    /// <summary>Slice E: tg's language menu [L, SS14's OpenLanguageMenu]: the click of tg's language button.</summary>
    LanguageMenu,

    /// <summary>Slice E: tg's navigate [M; SS14 has no such function]: the click of tg's navigate button.</summary>
    Navigate,

    /// <summary>Gaps C2 (protocol version 59): tg's <c>/datum/keybinding/living/view_pet_data</c> [J; Shift before, which is Walk now]: held, hovering a friendly pet opens its commands' radial.</summary>
    ViewPetCommands,

    /// <summary>D292 (the owner, 2026-10-29): closes every open window, the machines' and the storage [Shift+Esc] (tg has no such key).</summary>
    CloseWindows,

    /// <summary>The owner (2026-10-29): opens and closes the inventory panel [I] (it shows in cursor mode, so this is cursor mode too; tg has no such key).</summary>
    Inventory,

    /// <summary>
    /// ui-overhaul.md section 10 (DV693, protocol version 100's HotbarPress): the ten hotbar cells [1 to 9 and 0, the number row and the keypad]: 1 to 7
    /// the belt, 8 and 9 the pockets, 0 (Hotbar10) the ID slot. A press is sent to the server, which decides what it does (tg has no such keys).
    /// </summary>
    Hotbar1,
    Hotbar2,
    Hotbar3,
    Hotbar4,
    Hotbar5,
    Hotbar6,
    Hotbar7,
    Hotbar8,
    Hotbar9,
    Hotbar10,

    /// <summary>
    /// Shift and the number keys click the shown item action buttons at the top left, in reading order [Shift+1 to Shift+9; the plain number keys
    /// are the hotbar's, ui-overhaul.md section 10] (tg has no such keys).
    /// </summary>
    ActionButton1,
    ActionButton2,
    ActionButton3,
    ActionButton4,
    ActionButton5,
    ActionButton6,
    ActionButton7,
    ActionButton8,
    ActionButton9,

    /// <summary>D307: hold to speak, release to type what was said into the chat input (speech to text; tg has no such key) [N].</summary>
    PushToTalk,

    /// <summary>D309 (the owner, 2026-11-03): push to talk while typing in the chat or the say window, a key that types nothing [Alt].</summary>
    PushToTalkTyping,

    /// <summary>
    /// D325, DV422 (the owner, 2026-11-13): tg's <c>/datum/keybinding/movement/zlevel_upwards</c> [Space]: <c>mob.up()</c> for a ghost, the AI, a
    /// flyer, a jetpack without gravity, one on a ladder; for any other mob the server takes it as tg's hold throw mode.
    /// </summary>
    MoveUpLevel,

    /// <summary>
    /// D325, DV422: tg's <c>zlevel_downwards</c> [Ctrl]: <c>mob.down()</c> for the same mobs. Ctrl alone acts when it is let go with nothing
    /// else pressed meanwhile (<see cref="ModifierTap"/>), so Ctrl+Q and Ctrl+left click do not also go down a level.
    /// </summary>
    MoveDownLevel,

    /// <summary>Hides or shows the whole interface (the console's <c>ui toggle</c>) [Shift+F4, SS14's HideUI; F6 before].</summary>
    HideInterface,

    /// <summary>
    /// tg's Ctrl-click on what the crosshair points at (pull, a stronger grab of the one pulled, a machine's Ctrl-click; with Shift tg's
    /// Ctrl-Shift-click) [Ctrl+left click, SS14's TryPullObject; H before, DV422]. Bound to a mouse button, it is that button's press as a left
    /// click with Ctrl (GameScene.OnMouseButton); bound to a key, with nothing there it is tg's stop_pulling.
    /// </summary>
    Pull,

    /// <summary>DV585 (protocol version 93): the deathmatch scoreboard, held [K]: the kills and deaths of a deathmatch map's players.</summary>
    Scoreboard,

    /// <summary>
    /// ui-overhaul.md section 12, slice 4B-2: the quick transfer of the Tab inventory [Ctrl+left click]: on an item in a storage panel, a bar cell or a
    /// worn slot it goes to the active hand, on a held item into the backpack (an InventoryMove). It shares its default with <see cref="Pull"/>: it acts
    /// only on an item of the inventory under Tab, Pull everywhere else (<see cref="KeyBindings.SharesKeys"/>).
    /// </summary>
    QuickTransfer,
}

/// <summary>The colourblind palettes of the health HUD (D70: the colours of medical parameters are never the only carrier).</summary>
/// <summary>M4c slice 7 (D70): how strong a zero-gravity camera effect is: off, at half strength, or full.</summary>
public enum ZeroGLevel : byte
{
    Off = 0,
    Reduced = 1,
    On = 2,
}

public enum ColorblindMode : byte
{
    /// <summary>No colourblind palette: the standard ramp, green to red (<see cref="HealthZones.StandardColor"/>).</summary>
    Off,

    /// <summary>For red-green deficiencies (deuteranopia, protanopia): blue to orange.</summary>
    RedGreen,

    /// <summary>For blue-yellow deficiencies (tritanopia): teal to magenta.</summary>
    BlueYellow,

    /// <summary>A grey ramp: only light and the hatching tell the levels apart.</summary>
    Mono,
}

/// <summary>
/// The key bindings (D53: a key layout scheme and mandatory remapping; D70: full key remapping). A binding is the name of
/// a Godot physical key (<c>W</c>, <c>Up</c>, <c>Shift</c>, <c>Quoteleft</c>: the names of <c>Godot.Key</c>) or of a mouse
/// button (<c>MouseLeft</c>, <c>MouseRight</c>, <c>MouseMiddle</c>); an action may have several. A key serves one action:
/// binding it elsewhere takes it away from the action that had it (<see cref="Bind"/> says which).
/// </summary>
public sealed class KeyBindings
{
    private readonly Dictionary<PlayAction, List<string>> _keys = new();

    public KeyBindings()
    {
        foreach (PlayAction action in Enum.GetValues<PlayAction>())
            _keys[action] = new List<string>();
    }

    /// <summary>
    /// The default layout (ui-overhaul.md section 3): Space Station 14's key where SS14 has the same function (Shift to switch walk and run, C to lie
    /// down, Ctrl+Q to throw, Shift and Ctrl with the left click to examine and pull), and tg's or the port's own keys for the rest.
    /// </summary>
    public static KeyBindings Defaults()
    {
        var b = new KeyBindings();
        b.Set(PlayAction.MoveForward, "W", "Up");
        b.Set(PlayAction.MoveBack, "S", "Down");
        b.Set(PlayAction.MoveLeft, "A", "Left");
        b.Set(PlayAction.MoveRight, "D", "Right");
        b.Set(PlayAction.Rest, "C");
        b.Set(PlayAction.ToggleMoveIntent, "Shift");
        b.Set(PlayAction.ToggleCombatMode, "F");
        b.Set(PlayAction.PrimaryClick, "MouseLeft");
        b.Set(PlayAction.SecondaryClick, "MouseRight");
        b.Set(PlayAction.MiddleClick, "MouseMiddle");
        b.Set(PlayAction.DropItem, "Q");
        b.Set(PlayAction.ThrowMode, "Ctrl_Q");
        b.Set(PlayAction.SwapHand, "X");
        b.Set(PlayAction.ActivateInHand, "Z");
        b.Set(PlayAction.QuickEquip, "E");
        b.Set(PlayAction.CursorMode, "Tab");
        b.Set(PlayAction.Examine, "Shift_MouseLeft");
        b.Set(PlayAction.StopPulling, "H", "Delete");
        b.Set(PlayAction.Pull, "Ctrl_MouseLeft");
        b.Set(PlayAction.Resist, "B");
        b.Set(PlayAction.Say, "T");
        b.Set(PlayAction.Radio, "Semicolon");
        b.Set(PlayAction.Me, "Shift_Apostrophe");
        b.Set(PlayAction.Ooc, "Bracketleft");
        b.Set(PlayAction.Pray, "P");
        b.Set(PlayAction.EmoteWheel, "Y");
        b.Set(PlayAction.ChatWindow, "Shift_T");
        b.Set(PlayAction.CraftingMenu, "G");
        b.Set(PlayAction.LanguageMenu, "L");
        b.Set(PlayAction.Navigate, "M");
        b.Set(PlayAction.ViewPetCommands, "J");
        b.Set(PlayAction.CloseWindows, "Shift_Escape");
        b.Set(PlayAction.Inventory, "I");
        b.Set(PlayAction.PushToTalk, "N");
        b.Set(PlayAction.PushToTalkTyping, "Alt");
        b.Set(PlayAction.MoveUpLevel, "Space");
        b.Set(PlayAction.MoveDownLevel, "Ctrl");
        for (int n = 1; n <= 10; n++)
            b.Set((PlayAction)((int)PlayAction.Hotbar1 + n - 1), "Key" + n % 10, "Kp" + n % 10); // section 10: the hotbar on 1 to 9 and 0
        for (int n = 1; n <= 9; n++)
            b.Set((PlayAction)((int)PlayAction.ActionButton1 + n - 1), "Shift_Key" + n); // section 10: tg's action buttons on Shift+1 to Shift+9
        b.Set(PlayAction.Console, "Quoteleft");
        b.Set(PlayAction.Options, "F10");
        b.Set(PlayAction.DevMenu, "Backslash");
        b.Set(PlayAction.CameraMode, "F1");
        b.Set(PlayAction.ThirdPersonView, "F5");
        b.Set(PlayAction.Ceiling, "F2");
        b.Set(PlayAction.Names, "F4");
        b.Set(PlayAction.DebugHud, "F3");
        b.Set(PlayAction.HideInterface, "Shift_F4");
        b.Set(PlayAction.Flashlight, "U"); // slice E: L is the language menu
        b.Set(PlayAction.Scoreboard, "K");
        b.Set(PlayAction.Fullscreen, "F11");
        b.Set(PlayAction.QuickTransfer, "Ctrl_MouseLeft"); // slice 4B-2: on an inventory item under Tab; Pull keeps the same press elsewhere
        return b;
    }

    /// <summary>The keys of <paramref name="action"/>, in the order they were bound.</summary>
    public IReadOnlyList<string> KeysOf(PlayAction action) => _keys[action];

    /// <summary>Replaces the keys of <paramref name="action"/> (names are not checked here: <see cref="IsValidKeyName"/>).</summary>
    public void Set(PlayAction action, params string[] keys)
    {
        _keys[action] = new List<string>(keys);
    }

    /// <summary>
    /// Slice 4B-2: whether two actions may hold the same key, since they act in different places: <see cref="PlayAction.Pull"/> (tg's Ctrl-click on
    /// the world and the HUD) and <see cref="PlayAction.QuickTransfer"/> (on an item of the Tab inventory only). Every other key serves one action.
    /// </summary>
    public static bool SharesKeys(PlayAction a, PlayAction b) => (a, b) is (PlayAction.Pull, PlayAction.QuickTransfer) or (PlayAction.QuickTransfer, PlayAction.Pull);

    /// <summary>Sets the keys of <paramref name="action"/> and takes each of them from any other action that had it (a key serves one action).</summary>
    public void Claim(PlayAction action, params string[] keys)
    {
        foreach (string key in keys)
        {
            foreach (PlayAction other in Enum.GetValues<PlayAction>())
            {
                if (other != action && !SharesKeys(action, other))
                    _keys[other].Remove(key);
            }
        }
        _keys[action] = new List<string>(keys);
    }

    /// <summary>
    /// Makes <paramref name="key"/> the only key of <paramref name="action"/>, and takes it from any other action that had it.
    /// Returns the action that lost it, or null.
    /// </summary>
    public PlayAction? Bind(PlayAction action, string key)
    {
        if (!IsValidKeyName(key))
            throw new ArgumentException("not a key name: " + key, nameof(key));
        PlayAction? displaced = null;
        foreach (PlayAction other in Enum.GetValues<PlayAction>())
        {
            if (other != action && !SharesKeys(action, other) && _keys[other].Remove(key))
                displaced = other;
        }
        _keys[action] = new List<string> { key };
        return displaced;
    }

    /// <summary>The first action <paramref name="key"/> is bound to, or null.</summary>
    public PlayAction? ActionOf(string key)
    {
        foreach (PlayAction action in Enum.GetValues<PlayAction>())
        {
            if (_keys[action].Contains(key))
                return action;
        }
        return null;
    }

    /// <summary>
    /// M9 slice C: the action a key or mouse button press does: pressed with Shift or Ctrl held it is first looked up with them
    /// (<see cref="NameOf"/>), with both held then with Ctrl alone and with Shift alone, and last alone (a modifier held out of habit keeps the keys working).
    /// </summary>
    public PlayAction? ActionOfPress(string key, bool shift, bool ctrl = false) => MatchPress(key, shift, ctrl).Action;

    /// <summary>
    /// <see cref="ActionOfPress"/> with the modifiers that are part of the binding it matched (<c>Shift_MouseLeft</c>: Shift); a modifier held and
    /// not among them is one held on top of the binding (ui-overhaul.md section 5, <see cref="ModifiedClick"/>).
    /// </summary>
    public (PlayAction? Action, bool Shift, bool Ctrl) MatchPress(string key, bool shift, bool ctrl = false)
    {
        (bool Shift, bool Ctrl)[] tries = shift && ctrl ? new[] { (true, true), (false, true), (true, false), (false, false) } : new[] { (shift, ctrl), (false, false) };
        foreach ((bool s, bool c) in tries)
        {
            if (ActionOf(NameOf(key, s, c)) is PlayAction action)
                return (action, s && key != "Shift", c && key != "Ctrl");
        }
        return (null, false, false);
    }

    /// <summary>
    /// M9 slice C: a key's binding name, <c>Shift_</c> before it when Shift is held and <c>Ctrl_</c> before that when Ctrl is (<c>Ctrl_Shift_T</c>);
    /// a modifier key itself is not named twice (Shift stays <c>Shift</c>, Ctrl <c>Ctrl</c>).
    /// </summary>
    public static string NameOf(string key, bool shift, bool ctrl = false)
    {
        string name = shift && key != "Shift" ? "Shift_" + key : key;
        return ctrl && key != "Ctrl" ? "Ctrl_" + name : name;
    }

    /// <summary>
    /// The actions the game acts on with a mouse button pressed with Shift or Ctrl (GameScene.OnMouseButton: tg's left click with that modifier);
    /// for any other action such a binding would do nothing.
    /// </summary>
    public static bool TakesModifiedMouse(PlayAction? action) => action is PlayAction.Examine or PlayAction.Pull;

    /// <summary>
    /// ui-overhaul.md section 5: the binding name the options panel records for <paramref name="button"/> pressed for <paramref name="action"/>, or
    /// null when the panel does not take that press (it keeps waiting): a plain button for the three click actions only (a modifier held then is
    /// not recorded); for Examine and Pull the left button with Shift, Ctrl or both, the one whose whole click (press, release, cursor mode's
    /// drag) the game handles as tg's left click, so they never take a button from a click action; for any other action nothing from the mouse.
    /// </summary>
    public static string MouseBindingName(PlayAction action, string button, bool shift, bool ctrl) => action switch
    {
        PlayAction.PrimaryClick or PlayAction.SecondaryClick or PlayAction.MiddleClick => button,
        PlayAction.Examine or PlayAction.Pull or PlayAction.QuickTransfer when button == "MouseLeft" && (shift || ctrl) => NameOf(button, shift, ctrl),
        _ => null,
    };

    /// <summary>
    /// ui-overhaul.md section 5: the modifiers of tg's left click that a press bound to Examine or Pull sends: Shift for Examine, Ctrl for Pull, and
    /// a modifier held on top of the binding (<paramref name="extraShift"/>, <paramref name="extraCtrl"/>: held and not part of the binding matched,
    /// <see cref="MatchPress"/>), so Pull bound to Shift+left click is tg's Ctrl-click and Ctrl+left click with Shift held tg's Ctrl-Shift-click.
    /// </summary>
    public static Anomaly.Protocol.ClickModifiers ModifiedClick(PlayAction action, bool extraShift, bool extraCtrl)
        => (action == PlayAction.Pull ? Anomaly.Protocol.ClickModifiers.Ctrl : Anomaly.Protocol.ClickModifiers.Shift)
            | (extraShift ? Anomaly.Protocol.ClickModifiers.Shift : Anomaly.Protocol.ClickModifiers.None)
            | (extraCtrl ? Anomaly.Protocol.ClickModifiers.Ctrl : Anomaly.Protocol.ClickModifiers.None);

    /// <summary>True when <paramref name="key"/> is one of the keys of <paramref name="action"/>.</summary>
    public bool Has(PlayAction action, string key) => _keys[action].Contains(key);

    /// <summary>A key or mouse button name: letters, digits and underscores, up to 24.</summary>
    public static bool IsValidKeyName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 24)
            return false;
        foreach (char c in name)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c == '_'))
                return false;
        }
        return true;
    }

    /// <summary>True for the names of mouse buttons (<c>MouseLeft</c>, <c>MouseRight</c>, <c>MouseMiddle</c>).</summary>
    public static bool IsMouseButton(string name) => name is "MouseLeft" or "MouseRight" or "MouseMiddle";

    /// <summary>The snake_case id of an action (the option file's <c>bind.ID</c> and <c>--bind=ID=KEY</c>).</summary>
    public static string IdOf(PlayAction action)
    {
        var sb = new StringBuilder();
        string name = action.ToString();
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
                sb.Append('_');
            sb.Append(char.ToLowerInvariant(name[i]));
        }
        return sb.ToString();
    }

    /// <summary>
    /// M4a (risk 11): the ids M2's option files and <c>--bind</c> arguments use for the two actions that were renamed; they still read, and the file
    /// is written with the new ids from then on.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, PlayAction> LegacyIds = new Dictionary<string, PlayAction>
    {
        ["help"] = PlayAction.PrimaryClick,
        ["shove"] = PlayAction.SecondaryClick,
    };

    /// <summary>The ids of actions the client no longer has, which an old options file may still name: read and ignored (slice E: the give key; the readout panel's key, gone with the panel, D184).</summary>
    public static readonly IReadOnlySet<string> RetiredIds = new HashSet<string>(StringComparer.Ordinal) { "give_item", "analyzer" };

    /// <summary>The action of an id (<see cref="IdOf"/>, or a <see cref="LegacyIds"/> one), or null.</summary>
    public static PlayAction? ActionOfId(string id)
    {
        if (id != null && LegacyIds.TryGetValue(id, out PlayAction legacy))
            return legacy;
        foreach (PlayAction action in Enum.GetValues<PlayAction>())
        {
            if (string.Equals(IdOf(action), id, StringComparison.Ordinal))
                return action;
        }
        return null;
    }

    /// <summary>The words for the options panel.</summary>
    public static string TitleOf(PlayAction action) => action switch
    {
        PlayAction.MoveForward => "Move forward",
        PlayAction.MoveBack => "Move back",
        PlayAction.MoveLeft => "Move left",
        PlayAction.MoveRight => "Move right",
        PlayAction.Rest => "Rest / get up",
        PlayAction.ToggleMoveIntent => "Walk / run (switch)",
        PlayAction.ToggleCombatMode => "Combat mode",
        PlayAction.PrimaryClick => "Primary click (tg's left click)",
        PlayAction.SecondaryClick => "Secondary click (a tap is tg's right click, a hold the radial)",
        PlayAction.MiddleClick => "Middle click (swap hands)",
        PlayAction.DropItem => "Put the item in hand down at the crosshair",
        PlayAction.ThrowMode => "Throw the held item at the crosshair",
        PlayAction.SwapHand => "Swap hands",
        PlayAction.ActivateInHand => "Use the item in hand",
        PlayAction.QuickEquip => "Quick equip",
        PlayAction.HoldThrow => "Throw mode (hold)",
        PlayAction.CursorMode => "Cursor mode (the mouse over the HUD)",
        PlayAction.Examine => "Examine (what is under the crosshair)",
        PlayAction.StopPulling => "Stop pulling",
        PlayAction.Resist => "Resist",
        PlayAction.Say => "Say",
        PlayAction.Radio => "Say on the radio",
        PlayAction.Me => "Me (a custom emote)",
        PlayAction.Ooc => "OOC",
        PlayAction.Pray => "Pray",
        PlayAction.EmoteWheel => "Emote wheel",
        PlayAction.CraftingMenu => "Crafting menu",
        PlayAction.LanguageMenu => "Language menu",
        PlayAction.Navigate => "Navigate",
        PlayAction.ViewPetCommands => "View pet commands",
        PlayAction.CloseWindows => "Close all windows",
        PlayAction.Inventory => "Inventory",
        >= PlayAction.Hotbar1 and <= PlayAction.Hotbar7 => "Hotbar " + (action - PlayAction.Hotbar1 + 1) + " (belt)",
        PlayAction.Hotbar8 => "Hotbar 8 (left pocket)",
        PlayAction.Hotbar9 => "Hotbar 9 (right pocket)",
        PlayAction.Hotbar10 => "Hotbar 0 (ID slot)",
        >= PlayAction.ActionButton1 and <= PlayAction.ActionButton9 => "Action button " + (action - PlayAction.ActionButton1 + 1),
        PlayAction.ChatWindow => "Chat window",
        PlayAction.Console => "Dev console",
        PlayAction.Options => "Settings",
        PlayAction.CameraMode => "Camera mode",
        PlayAction.ThirdPersonView => "View: first person, third person, free look",
        PlayAction.Ceiling => "Ceiling",
        PlayAction.Names => "Names overlay",
        PlayAction.DevMenu => "Dev menu",
        PlayAction.DebugHud => "FPS and server stats (top right)",
        PlayAction.HideInterface => "Hide the interface",
        PlayAction.Fullscreen => "Fullscreen",
        PlayAction.Flashlight => "Dev flashlight",
        PlayAction.PushToTalk => "Push to talk (speech to text)",
        PlayAction.PushToTalkTyping => "Push to talk while typing in the chat",
        PlayAction.MoveUpLevel => "Up a level (flying; else throw mode while held)",
        PlayAction.MoveDownLevel => "Down a level (flying)",
        PlayAction.Pull => "Pull what the crosshair points at",
        PlayAction.Scoreboard => "Scoreboard (deathmatch, while held)",
        PlayAction.QuickTransfer => "Quick transfer (an item of the Tab inventory to the hand, a held one into the backpack)",
        _ => action.ToString(),
    };

    /// <summary>A copy.</summary>
    public KeyBindings Clone()
    {
        var copy = new KeyBindings();
        foreach (PlayAction action in Enum.GetValues<PlayAction>())
            copy._keys[action] = new List<string>(_keys[action]);
        return copy;
    }
}

/// <summary>
/// The player's own settings (D70, D68's client profile): head bob and camera shake with an off switch, the colourblind
/// palette and the limit on fullscreen effects of the health HUD, its text and shape cues, the field of view, the mouse
/// sensitivity, the key bindings. Saved as a small text file (<see cref="ToText"/>, <see cref="Parse"/>) in Godot's user
/// folder; a missing or damaged file gives the defaults.
/// </summary>
public sealed class PlayOptions
{
    /// <summary>The camera bobs while walking (D70: off switch).</summary>
    public bool HeadBob { get; set; } = true;

    /// <summary>The other mobs' names over their heads (the owner, 2026-10-09; tg has no such tags: a 3D view shows no name on hover over a crowd).</summary>
    public bool PlayerNames { get; set; } = true;

    /// <summary>D315: the minimap of the own level (tg's minimap picture), on by default; Settings > View turns it off.</summary>
    public bool Minimap { get; set; } = true;

    /// <summary>D315: the minimap's zoom, screen pixels a tile in per cent (100 to 800; the mouse wheel over it in cursor mode).</summary>
    public int MinimapZoomPercent { get; set; } = 200;

    /// <summary>D315: the minimap's size, per cent of its 200 x 200 pixels on a 1080-line screen (50 to 300; Settings > View; 200 by default, the owner).</summary>
    public int MinimapSizePercent { get; set; } = 200;

    /// <summary>The minimap's area codes and hover text, per cent of their size (50 to 400; Settings > Interface): in a small window they shrink with the panel (a player's report, 2026-11-14).</summary>
    public int MinimapTextPercent { get; set; } = 100;

    /// <summary>D138: the FPS and the connection's numbers at the top right (F4, the dev menu's View tab); on by default (the owner, 2026-10-18).</summary>
    public bool ShowStats { get; set; } = true;

    /// <summary>The floor or wall tile under the crosshair is outlined (the owner, 2026-10-09: off by default).</summary>
    public bool HighlightTile { get; set; }

    /// <summary>The thing under the crosshair is outlined (the owner, 2026-10-13: off by default, as for tiles).</summary>
    public bool HighlightThing { get; set; }

    /// <summary>The port status overlay: a ball over every thing coloured by how much of it is ported, and the window of its procs (the owner, 2026-10-28: off by default).</summary>
    public bool PortStatus { get; set; }

    /// <summary>The third-person view (F1) shades what the own mob cannot see, as Space Station 14 does (the owner, 2026-10-16: on by default).</summary>
    public bool ThirdPersonFov { get; set; } = false;

    /// <summary>
    /// The owner (2026-11-20): seconds the own mob keeps facing the crosshair in the third person's free look after a shot, a hit or a throw
    /// at the world (it turns there at once, and the keys strafe meanwhile); 0 to 5, 1 by default.
    /// </summary>
    public float AimFacingSeconds { get; set; } = 1f;

    public const float MaxAimFacingSeconds = 5f;

    /// <summary>The camera shakes when shoved, knocked down or hurt (D70: off switch).</summary>
    public bool CameraShake { get; set; } = true;

    /// <summary>M4c slice 7 (D70, DV41): the eye floats up and down while weightless, as tg's floating animation bobs the body (2 pixels, 2 seconds a cycle).</summary>
    public ZeroGLevel ZeroGBob { get; set; } = ZeroGLevel.On;

    /// <summary>M4c slice 7 (D70, DV41): the view leans into a sideways glide while weightless (a port addition; off by default).</summary>
    public ZeroGLevel ZeroGRoll { get; set; } = ZeroGLevel.Off;

    /// <summary>The palette of the doll and the health indicator.</summary>
    public ColorblindMode Colorblind { get; set; } = ColorblindMode.Off;

    /// <summary>Fullscreen effects (the damage and crit overlays) are drawn at most this strong, 0 to 100 per cent (D70: limit flashes).</summary>
    public int FullscreenStrengthPercent { get; set; } = 100;

    /// <summary>A text label per doll zone with its level and name (D70); off by default (the owner, 2026-10-18), an option to turn on.</summary>
    public bool ZoneLabels { get; set; }

    /// <summary>Hatching over a doll zone by its level (D70: a shape cue besides the colour).</summary>
    public bool HatchCues { get; set; } = true;

    /// <summary>D152 (block windows) (<see cref="WorldScale.BlockPieces"/>): windows, grilles and girders as tile-sized blocks. Read when the world is built.</summary>
    public bool BlockWindows { get; set; } = true;

    /// <summary>The HUD's scale, 50 to 300 per cent.</summary>
    public int UiScalePercent { get; set; } = 125;

    /// <summary>
    /// tg's windows (tgui, the crafting menu, the input windows) and the tooltips: 50 to 300 per cent of their size on a 1080-pixel-high
    /// screen (<see cref="WindowScaleFor"/>; the owner, 2026-10-16: they were too small on a large screen). Ctrl+= and Ctrl+- change it in game.
    /// </summary>
    public int WindowScalePercent { get; set; } = 80;

    /// <summary>The owner (2026-11-06): the chat window's own scale, apart from <see cref="WindowScalePercent"/> (<see cref="ChatWindowScaleFor"/>), the same range.</summary>
    public int ChatScalePercent { get; set; } = 80;

    /// <summary>
    /// The owner (2026-11-19): the size of the chat lines drawn over the view while the chat window is closed (Hud/ChatLines.cs), a share of the
    /// HUD's text size, apart from the chat window's <see cref="ChatScalePercent"/>: 50 by default (they were twice too large), 25 to 200.
    /// </summary>
    public int ChatLinesScalePercent { get; set; } = 50;

    /// <summary>The range of <see cref="ChatLinesScalePercent"/>.</summary>
    public const int MinChatLinesScale = 25, MaxChatLinesScale = 200;

    /// <summary><see cref="ChatLinesScalePercent"/> as a factor of the HUD's text size.</summary>
    public float ChatLinesScale => Math.Clamp(ChatLinesScalePercent, MinChatLinesScale, MaxChatLinesScale) / 100f;

    /// <summary>
    /// The owner (2026-10-29): how opaque the backgrounds of tg's windows (the machines', the items') and the chat window are, 10 to 100 per cent;
    /// 50 by default. Their text and controls stay solid. The Esc menu and the Settings window keep their own look.
    /// </summary>
    public int WindowOpacityPercent { get; set; } = 50;

    /// <summary><see cref="WindowOpacityPercent"/> as a factor.</summary>
    public float WindowOpacity => Math.Clamp(WindowOpacityPercent, 10, 100) / 100f;

    /// <summary>The owner (2026-11-01): how opaque the HUD's plain text is (the version tag, the stats and the screentip at the top right, the camera mode's name), 10 to 100 per cent.</summary>
    public int HudTextOpacityPercent { get; set; } = 60;

    /// <summary><see cref="HudTextOpacityPercent"/> as a factor.</summary>
    public float HudTextOpacity => Math.Clamp(HudTextOpacityPercent, 10, 100) / 100f;

    /// <summary>D296 (DV226): every game sound's volume, 0 to 100 per cent (the dev menu's master volume is the engine's, under it).</summary>
    public int MasterVolumePercent { get; set; } = 100;

    /// <summary>D296 (DV226): the world's sounds (steps, doors, weapons, machines: every channel the others do not take), 0 to 100 per cent.</summary>
    public int EffectsVolumePercent { get; set; } = 100;

    /// <summary>D296 (DV226): the music (the lobby's, the jukebox, the boss and antagonist music), 0 to 100 per cent.</summary>
    public int MusicVolumePercent { get; set; } = 100;

    /// <summary>D296 (DV226): the client's own interface sounds (the Esc menu, the lobby), 0 to 100 per cent.</summary>
    public int InterfaceVolumePercent { get; set; } = 100;

    /// <summary>D296 (DV226): the area ambience's volume, 0 to 100 per cent (tg's sound_ambience_volume preference).</summary>
    public int AmbienceVolumePercent { get; set; } = 30;

    /// <summary>D296 (DV226): the ship hum's volume, 0 to 100 per cent (tg's sound_ship_ambience_volume preference).</summary>
    public int ShipHumVolumePercent { get; set; } = 40;

    /// <summary>DV598: the soft ping of another player's OOC line, 0 (off) to 100 per cent.</summary>
    public int OocPingVolumePercent { get; set; } = 60;

    /// <summary>D296: the area reverb (tg's sound_environment) on.</summary>
    public bool Reverb { get; set; } = true;

    /// <summary>D307: the client voices what it hears (D13, D16; tg's sound_tts preference); its model is downloaded when this is turned on.</summary>
    public bool TextToSpeech { get; set; }

    /// <summary>D307: the voices' volume, 0 to 100 per cent (tg's sound_tts_volume preference).</summary>
    public int SpeechVolumePercent { get; set; } = 100;

    /// <summary>D307: push to talk types what the player says into the chat input; its model is downloaded when this is turned on.</summary>
    public bool SpeechToText { get; set; }

    /// <summary>D307: the languages voiced and recognized (<see cref="Speech.SpeechLanguages"/> codes); a line in another language is not voiced.</summary>
    public List<string> SpeechLanguageCodes { get; set; } = new(Speech.SpeechLanguages.Defaults);

    /// <summary>D313: the one language speech to text writes in, one of <see cref="SpeechLanguageCodes"/>; empty: the closest of them (D312).</summary>
    public string SpeechToTextLanguage { get; set; } = "";

    /// <summary>D313: the languages a phrase is recognized in: the chosen one while it is still picked, else every picked one.</summary>
    public List<string> RecognitionLanguages() =>
        SpeechLanguageCodes.Contains(SpeechToTextLanguage) ? new() { SpeechToTextLanguage }
        : SpeechLanguageCodes.Count > 0 ? new(SpeechLanguageCodes) : new(Speech.SpeechLanguages.Defaults);

    /// <summary>D319: other players' words in the chat are translated into <see cref="ChatTranslationLanguage"/>; its models are downloaded when this is turned on.</summary>
    public bool ChatTranslation { get; set; }

    /// <summary>D319: the language the chat is translated into (<see cref="Speech.ChatTranslation.Targets"/>).</summary>
    public string ChatTranslationLanguage { get; set; } = "en";

    /// <summary>D308: which radio lines are voiced (tg's sound_tts_radio preference: all, departmental only, none; all by default).</summary>
    public RadioSpeech RadioSpeech { get; set; } = RadioSpeech.All;

    /// <summary>D308: the radio voices' volume, 0 to 100 per cent (tg's sound_tts_radio_volume preference, 75 by default).</summary>
    public int RadioSpeechVolumePercent { get; set; } = 75;

    /// <summary>D308: the player hears their own radio lines (tg's sound_tts_hear_self_radio preference, off by default).</summary>
    public bool HearOwnRadio { get; set; }

    /// <summary>D298: Godot's tonemapper (<see cref="PostLook.Tonemaps"/>: 0 linear, 1 filmic, 2 AgX).</summary>
    public int Tonemap { get; set; }

    /// <summary>D298: the colour grade's saturation, 0 to 200 per cent.</summary>
    public int SaturationPercent { get; set; } = 100;

    /// <summary>D298: the colour grade's contrast, 50 to 150 per cent.</summary>
    public int ContrastPercent { get; set; } = 100;

    /// <summary>D298: the colour grade's split toning (cool shadows, warm lights), 0 to 100 per cent.</summary>
    public int SplitTonePercent { get; set; }

    // the owner's defaults (2026-10-31): far darkness, 40 per cent grain, a 30 per cent vignette, half the bloom, thin outlines at 40 per cent

    /// <summary>D298: the dark distance fog, where it is whole in tiles (<see cref="PostLook.Fogs"/>; 0 off).</summary>
    public int FogTiles { get; set; } = 40;

    /// <summary>D298: the film grain, 0 to 100 per cent.</summary>
    public int GrainPercent { get; set; } = 40;

    /// <summary>D298: the vignette, 0 to 100 per cent.</summary>
    public int VignettePercent { get; set; } = 30;

    /// <summary>D298: the bloom's strength, 0 to 300 per cent of the scene's own.</summary>
    public int BloomPercent { get; set; } = 50;

    /// <summary>D298: the bloom's threshold in per cent of full white (<see cref="PostLook.BloomThresholds"/>).</summary>
    public int BloomThresholdPercent { get; set; } = 130;

    /// <summary>D298: the edge outlines' width (<see cref="PostLook.Outlines"/>; 0 off).</summary>
    public int OutlineWidth { get; set; } = 1;

    /// <summary>D298: the edge outlines' strength, 0 to 100 per cent.</summary>
    public int OutlinePercent { get; set; } = 40;

    /// <summary>The owner (2026-10-30): how opaque the radial menus' backgrounds and buttons are, 10 to 100 per cent; 85 by default. Their text and icons stay solid.</summary>
    public int RadialOpacityPercent { get; set; } = 85;

    /// <summary><see cref="RadialOpacityPercent"/> as a factor.</summary>
    public float RadialOpacity => Math.Clamp(RadialOpacityPercent, 10, 100) / 100f;

    /// <summary>The dev windows' scale (the console, the analyzer, the debug HUD's text), 50 to 400 per cent of their M1 size; 200 by default (the owner, 2026-10-09).</summary>
    public int DevUiScalePercent { get; set; } = 160;

    /// <summary>The crosshair's size, 50 to 400 per cent of its default.</summary>
    public int CrosshairScalePercent { get; set; } = 200;

    /// <summary>The camera's vertical field of view in degrees (D70: adjustable FOV).</summary>
    public float FovDegrees { get; set; } = 75f;

    /// <summary>Radians of view turn per pixel of mouse movement.</summary>
    public float MouseSensitivity { get; set; } = 0.0025f;

    /// <summary>D288: the graphics preset, low, medium, high or max, or <see cref="RenderPreset.CustomName"/> for <see cref="CustomGraphics"/>.</summary>
    public string Quality { get; set; } = "medium";

    /// <summary>D288: the player's own graphics settings, used while <see cref="Quality"/> is custom; the file's <c>gfx.ID</c> lines (<see cref="GraphicsSetting"/>).</summary>
    public RenderPreset CustomGraphics { get; set; } = RenderPreset.Medium with { Name = RenderPreset.CustomName };

    /// <summary>
    /// The owner (2026-10-30): the resolution the whole frame (the world and the interface) is drawn at in fullscreen, then scaled
    /// to the monitor as a monitor's own resolution change would be; (0, 0) is the monitor's own.
    /// </summary>
    public (int Width, int Height) FullscreenResolution { get; set; }

    /// <summary>The settings in force: the named preset, else <see cref="CustomGraphics"/>.</summary>
    public RenderPreset Graphics => RenderPreset.TryParse(Quality, out RenderPreset preset) ? preset : CustomGraphics;

    /// <summary>Changes one graphics setting: the settings in force with it changed become the custom ones.</summary>
    public void SetGraphics(GraphicsSetting setting, int value)
    {
        CustomGraphics = setting.Set(Graphics, value) with { Name = RenderPreset.CustomName };
        Quality = RenderPreset.CustomName;
    }

    /// <summary>
    /// M3 R2 (D120, D157): the standing eye's height in metres (<see cref="WorldScale.EyeHeight"/>), 1.3 by default; lying it is 0.4. Set once at the start
    /// from the file, <c>--option=eye_height=M</c> or <c>--eye-height=M</c>, like the wall height (the options panel has neither).
    /// </summary>
    public float EyeHeight { get; set; } = WorldScale.DefaultEyeHeight;

    /// <summary>
    /// M3 R3 (D121): the scale items (<c>/obj/item</c>) are drawn at, a share of the tile scale (<see cref="WorldScale.ItemScale"/>), 0.5 by default. Read when the world is
    /// built, from the file, <c>--option=item_scale=S</c> or <c>--item-scale=S</c> (the options panel has none).
    /// </summary>
    public float ItemScale { get; set; } = WorldScale.DefaultItemScale;

    /// <summary>The key bindings.</summary>
    public KeyBindings Bindings { get; set; } = KeyBindings.Defaults();

    /// <summary>The dev menu's settings (<c>dev.NAME</c> lines; the owner, 2026-10-14).</summary>
    public DevMenuOptions Dev { get; set; } = new();

    /// <summary>
    /// M5 slice 3 (D148): where the player dragged each kind of tgui window (tg's interface name to its top left in screen pixels),
    /// saved as <c>window.NAME=x,y</c>; a window of that kind opens there next time.
    /// </summary>
    public Dictionary<string, (int X, int Y)> WindowPositions { get; } = new(StringComparer.Ordinal);

    /// <summary>The size the player resized each kind of tgui window to (pixels at 100 %), saved as <c>window_size.NAME=w,h</c> (the owner, 2026-10-20).</summary>
    public Dictionary<string, (int Width, int Height)> WindowSizes { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The owner (2026-10-28): the screen size in pixels the saved window places and sizes (<see cref="WindowPositions"/>, the chrome windows'
    /// <see cref="WindowSizes"/>) are for, saved as <c>window_screen=w,h</c>. A window keeps its share of any other screen size, so going back
    /// to a size brings every window back where it was on it. Unset (an older file): the first screen the windows are placed on.
    /// </summary>
    public (int Width, int Height) WindowScreen { get; set; }

    /// <summary>A saved place or size (pixels on <see cref="WindowScreen"/>) on a screen of <paramref name="width"/> x <paramref name="height"/>.</summary>
    public (double X, double Y) WindowOnScreen(double x, double y, double width, double height)
    {
        (int w, int h) = WindowScreenFor(width, height);
        return (x * width / w, y * height / h);
    }

    /// <summary><see cref="WindowScreen"/>, set to the current screen when unset.</summary>
    public (int Width, int Height) WindowScreenFor(double width, double height)
    {
        if (WindowScreen.Width <= 0 || WindowScreen.Height <= 0)
            WindowScreen = (Math.Max(1, (int)Math.Round(width)), Math.Max(1, (int)Math.Round(height)));
        return WindowScreen;
    }

    /// <summary>D282: the scale the player gave a window drawn as cells (the storage, the inventory), percent, saved as <c>window_scale.NAME=p</c>.</summary>
    public Dictionary<string, int> WindowScales { get; } = new(StringComparer.Ordinal);

    /// <summary>The fullscreen limit as a factor, 0 to 1.</summary>
    public float FullscreenStrength => Math.Clamp(FullscreenStrengthPercent, 0, 100) / 100f;

    /// <summary>The HUD scale as a factor.</summary>
    public float UiScale => Math.Clamp(UiScalePercent, 50, 300) / 100f;

    /// <summary>The smallest and largest <see cref="WindowScalePercent"/>, and its step for the keys.</summary>
    public const int MinWindowScale = 50, MaxWindowScale = 300, WindowScaleStep = 10;

    /// <summary>
    /// The window scale after Ctrl with <paramref name="key"/> (an engine key name) from <paramref name="now"/>: = or the
    /// keypad's + grow it a step, - or the keypad's - shrink it, 0 or the keypad's 0 resets it to 100; -1 for any other key (GameScene.WindowScaleKey).
    /// </summary>
    public static int WindowScaleAfterKey(string key, int now) => key switch
    {
        "Equal" or "KpAdd" => now + WindowScaleStep,
        "Minus" or "KpSubtract" => now - WindowScaleStep,
        "Key0" or "Kp0" => 100,
        _ => -1,
    };

    /// <summary>
    /// ui-overhaul.md section 5: Ctrl (with Shift or not) and <paramref name="key"/> is a window scale key, which the game takes before the bindings
    /// (<see cref="WindowScaleAfterKey"/>), so the options panel refuses it as a binding.
    /// </summary>
    public static bool IsWindowScalePair(string key, bool ctrl) => ctrl && WindowScaleAfterKey(key, 100) >= 0;

    /// <summary>
    /// The scale tg's windows and the tooltips are drawn at on a screen <paramref name="screenHeight"/> pixels high: tgui's pixel sizes hold
    /// on a 1080-pixel-high screen and grow with taller ones (a 4K or Retina screen, where the client draws in physical pixels: 2x at 2160),
    /// never below 1, times <see cref="WindowScalePercent"/>.
    /// </summary>
    public float WindowScaleFor(float screenHeight)
        => Math.Max(1f, screenHeight / 1080f) * Math.Clamp(WindowScalePercent, MinWindowScale, MaxWindowScale) / 100f;

    /// <summary>The owner (2026-11-02): the chat window keeps its share of the screen at every height, also below 1080, unlike <see cref="WindowScaleFor"/>, and is drawn twice as large (tg's chat font is small for a first-person screen).</summary>
    public float ChatWindowScaleFor(float screenHeight)
        => screenHeight / 540f * Math.Clamp(ChatScalePercent, MinWindowScale, MaxWindowScale) / 100f;

    /// <summary>The dev windows' scale as a factor of their M1 size.</summary>
    public float DevUiScale => Math.Clamp(DevUiScalePercent, 50, 400) / 100f;

    public const float MinFov = 50f;
    public const float MaxFov = 110f;

    /// <summary>
    /// The options file's version (<c>options_version</c>): 4 since D315's minimap size (3 since D157, 2 since D137). A file without it (or older)
    /// that still has a default that was changed since gets the new one: the eye height of D120 (1.2 m) or D137 (1.5 m) becomes D157's 1.3 m,
    /// the dev menu on F8 moves to the | key, the minimap's first size (100 per cent) becomes 200.
    /// </summary>
    public const int FileVersion = 5;

    /// <summary>The version the file was read with (<see cref="FileVersion"/> when new).</summary>
    public int ReadVersion { get; private set; } = FileVersion;

    private bool _versionSeen;

    private bool IsOnly(PlayAction action, string key) => Bindings.KeysOf(action) is [var only] && only == key;

    // A file older than FileVersion: the defaults it kept from then are today's.
    private void Upgrade()
    {
        if (!_versionSeen)
            ReadVersion = 1;
        if (ReadVersion >= FileVersion)
            return;
        if (ReadVersion < 2)
        {
            if (Math.Abs(EyeHeight - WorldScale.OldDefaultEyeHeight) < 0.001f)
                EyeHeight = WorldScale.DefaultEyeHeight;
            IReadOnlyList<string> menu = Bindings.KeysOf(PlayAction.DevMenu);
            if (menu.Count == 1 && menu[0] == "F8" && Bindings.ActionOf("Backslash") == null)
                Bindings.Set(PlayAction.DevMenu, "Backslash");
        }
        if (ReadVersion < 3 && Math.Abs(EyeHeight - WorldScale.D137EyeHeight) < 0.001f)
            EyeHeight = WorldScale.DefaultEyeHeight;
        if (ReadVersion < 4 && MinimapSizePercent == 100)
            MinimapSizePercent = 200; // D315: the default the owner raised
        if (ReadVersion < 5)
        {
            // DV422: the level keys move from PageUp and PageDown to Space and Ctrl (Space leaves hold throw mode), pulling to H
            if (IsOnly(PlayAction.MoveUpLevel, "Pageup") && (IsOnly(PlayAction.HoldThrow, "Space") || Bindings.ActionOf("Space") == null))
                Bindings.Claim(PlayAction.MoveUpLevel, "Space");
            if (IsOnly(PlayAction.MoveDownLevel, "Pagedown") && Bindings.ActionOf("Ctrl") == null)
                Bindings.Set(PlayAction.MoveDownLevel, "Ctrl");
            if (Bindings.KeysOf(PlayAction.Pull).Count == 0 && Bindings.Has(PlayAction.StopPulling, "H"))
                Bindings.Claim(PlayAction.Pull, "H");
        }
        ReadVersion = FileVersion;
    }

    /// <summary>The file's text.</summary>
    public string ToText()
    {
        var c = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append("# Anomaly Station client options (docs/ai/specs/apps.md section 2); edit here or in the options panel (F10 by default)\n");
        sb.Append("options_version=").Append(FileVersion.ToString(c)).Append('\n');
        sb.Append("head_bob=").Append(HeadBob ? "true" : "false").Append('\n');
        sb.Append("player_names=").Append(PlayerNames ? "true" : "false").Append('\n');
        sb.Append("show_stats=").Append(ShowStats ? "true" : "false").Append('\n');
        sb.Append("minimap=").Append(Minimap ? "true" : "false").Append('\n');
        sb.Append("minimap_zoom=").Append(MinimapZoomPercent.ToString(c)).Append('\n');
        sb.Append("minimap_size=").Append(MinimapSizePercent.ToString(c)).Append('\n');
        sb.Append("minimap_text=").Append(MinimapTextPercent.ToString(c)).Append('\n');
        sb.Append("highlight_tile=").Append(HighlightTile ? "true" : "false").Append('\n');
        sb.Append("highlight_thing=").Append(HighlightThing ? "true" : "false").Append('\n');
        sb.Append("port_status=").Append(PortStatus ? "true" : "false").Append('\n');
        sb.Append("third_person_fov=").Append(ThirdPersonFov ? "true" : "false").Append('\n');
        sb.Append("aim_facing=").Append(AimFacingSeconds.ToString("0.##", c)).Append('\n');
        sb.Append("camera_shake=").Append(CameraShake ? "true" : "false").Append('\n');
        sb.Append("zero_g_bob=").Append(ZeroGLevelId(ZeroGBob)).Append('\n');
        sb.Append("zero_g_roll=").Append(ZeroGLevelId(ZeroGRoll)).Append('\n');
        sb.Append("colorblind=").Append(ColorblindId(Colorblind)).Append('\n');
        sb.Append("fullscreen_strength=").Append(FullscreenStrengthPercent.ToString(c)).Append('\n');
        sb.Append("zone_labels=").Append(ZoneLabels ? "true" : "false").Append('\n');
        sb.Append("hatch_cues=").Append(HatchCues ? "true" : "false").Append('\n');
        sb.Append("block_windows=").Append(BlockWindows ? "true" : "false").Append('\n');
        sb.Append("ui_scale=").Append(UiScalePercent.ToString(c)).Append('\n');
        sb.Append("window_scale=").Append(WindowScalePercent.ToString(c)).Append('\n');
        sb.Append("chat_scale=").Append(ChatScalePercent.ToString(c)).Append('\n');
        sb.Append("chat_lines_scale=").Append(ChatLinesScalePercent.ToString(c)).Append('\n');
        sb.Append("window_opacity=").Append(WindowOpacityPercent.ToString(c)).Append('\n');
        sb.Append("hud_text_opacity=").Append(HudTextOpacityPercent.ToString(c)).Append('\n');
        sb.Append("radial_opacity=").Append(RadialOpacityPercent.ToString(c)).Append('\n');
        sb.Append("master_volume=").Append(MasterVolumePercent.ToString(c)).Append('\n');
        sb.Append("effects_volume=").Append(EffectsVolumePercent.ToString(c)).Append('\n');
        sb.Append("music_volume=").Append(MusicVolumePercent.ToString(c)).Append('\n');
        sb.Append("interface_volume=").Append(InterfaceVolumePercent.ToString(c)).Append('\n');
        sb.Append("ambience_volume=").Append(AmbienceVolumePercent.ToString(c)).Append('\n');
        sb.Append("ship_hum_volume=").Append(ShipHumVolumePercent.ToString(c)).Append('\n');
        sb.Append("ooc_ping_volume=").Append(OocPingVolumePercent.ToString(c)).Append('\n');
        sb.Append("reverb=").Append(Reverb ? "true" : "false").Append('\n');
        sb.Append("text_to_speech=").Append(TextToSpeech ? "true" : "false").Append('\n');
        sb.Append("speech_volume=").Append(SpeechVolumePercent.ToString(c)).Append('\n');
        sb.Append("speech_to_text=").Append(SpeechToText ? "true" : "false").Append('\n');
        sb.Append("speech_languages=").Append(string.Join(',', SpeechLanguageCodes)).Append('\n');
        sb.Append("speech_to_text_language=").Append(SpeechToTextLanguage).Append('\n');
        sb.Append("chat_translation=").Append(ChatTranslation ? "true" : "false").Append('\n');
        sb.Append("chat_translation_language=").Append(ChatTranslationLanguage).Append('\n');
        sb.Append("radio_speech=").Append(RadioSpeech.ToString().ToLowerInvariant()).Append('\n');
        sb.Append("radio_speech_volume=").Append(RadioSpeechVolumePercent.ToString(c)).Append('\n');
        sb.Append("hear_own_radio=").Append(HearOwnRadio ? "true" : "false").Append('\n');
        sb.Append("tonemap=").Append(Tonemap.ToString(c)).Append('\n');
        sb.Append("saturation=").Append(SaturationPercent.ToString(c)).Append('\n');
        sb.Append("contrast=").Append(ContrastPercent.ToString(c)).Append('\n');
        sb.Append("split_tone=").Append(SplitTonePercent.ToString(c)).Append('\n');
        sb.Append("fog=").Append(FogTiles.ToString(c)).Append('\n');
        sb.Append("grain=").Append(GrainPercent.ToString(c)).Append('\n');
        sb.Append("vignette=").Append(VignettePercent.ToString(c)).Append('\n');
        sb.Append("bloom=").Append(BloomPercent.ToString(c)).Append('\n');
        sb.Append("bloom_threshold=").Append(BloomThresholdPercent.ToString(c)).Append('\n');
        sb.Append("outline=").Append(OutlineWidth.ToString(c)).Append('\n');
        sb.Append("outline_strength=").Append(OutlinePercent.ToString(c)).Append('\n');
        sb.Append("dev_ui_scale=").Append(DevUiScalePercent.ToString(c)).Append('\n');
        sb.Append("crosshair_scale=").Append(CrosshairScalePercent.ToString(c)).Append('\n');
        sb.Append("fov=").Append(FovDegrees.ToString("0.#", c)).Append('\n');
        sb.Append("fullscreen_resolution=").Append(FullscreenResolution.Width > 0 ? FullscreenResolution.Width.ToString(c) + "," + FullscreenResolution.Height.ToString(c) : "native").Append('\n');
        sb.Append("quality=").Append(Quality).Append('\n');
        if (Quality == RenderPreset.CustomName)
            foreach (GraphicsSetting setting in GraphicsSetting.All)
                sb.Append("gfx.").Append(setting.Id).Append('=').Append(setting.Get(CustomGraphics).ToString(c)).Append('\n');
        sb.Append("eye_height=").Append(EyeHeight.ToString("0.##", c)).Append('\n');
        sb.Append("item_scale=").Append(ItemScale.ToString("0.##", c)).Append('\n');
        sb.Append("mouse_sensitivity=").Append(MouseSensitivity.ToString("0.#####", c)).Append('\n');
        Dev.AppendTo(sb);
        if (WindowScreen.Width > 0 && WindowScreen.Height > 0)
            sb.Append("window_screen=").Append(WindowScreen.Width.ToString(c)).Append(',').Append(WindowScreen.Height.ToString(c)).Append('\n');
        foreach (KeyValuePair<string, (int X, int Y)> window in WindowPositions.OrderBy(w => w.Key, StringComparer.Ordinal))
            sb.Append("window.").Append(window.Key).Append('=').Append(window.Value.X.ToString(c)).Append(',').Append(window.Value.Y.ToString(c)).Append('\n');
        foreach (KeyValuePair<string, (int Width, int Height)> window in WindowSizes.OrderBy(w => w.Key, StringComparer.Ordinal))
            sb.Append("window_size.").Append(window.Key).Append('=').Append(window.Value.Width.ToString(c)).Append(',').Append(window.Value.Height.ToString(c)).Append('\n');
        foreach (KeyValuePair<string, int> window in WindowScales.OrderBy(w => w.Key, StringComparer.Ordinal))
            sb.Append("window_scale.").Append(window.Key).Append('=').Append(window.Value.ToString(c)).Append('\n');
        foreach (PlayAction action in Enum.GetValues<PlayAction>())
            sb.Append("bind.").Append(KeyBindings.IdOf(action)).Append('=').Append(string.Join(',', Bindings.KeysOf(action))).Append('\n');
        return sb.ToString();
    }

    /// <summary>Reads a file's text over the defaults; a line that is not understood is added to <paramref name="errors"/> (when given) and skipped.</summary>
    public static PlayOptions Parse(string text, List<string> errors = null)
    {
        var options = new PlayOptions();
        if (string.IsNullOrEmpty(text))
            return options;
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;
            int equals = line.IndexOf('=');
            if (equals <= 0)
            {
                errors?.Add($"'{line}': expected name=value");
                continue;
            }
            string key = line.Substring(0, equals).Trim();
            string value = line.Substring(equals + 1).Trim();
            if (!options.TrySet(key, value, out string problem))
                errors?.Add($"'{line}': {problem}");
        }
        options.Upgrade();
        return options;
    }

    /// <summary>Sets one option by its file name (also the <c>--bind=ID=KEY</c> and <c>--option=NAME=VALUE</c> forms); false with the problem when the name or value is wrong.</summary>
    public bool TrySet(string name, string value, out string problem)
    {
        problem = null;
        var c = CultureInfo.InvariantCulture;
        switch (name)
        {
            case "options_version":
                if (int.TryParse(value, NumberStyles.Integer, c, out int version) && version >= 1)
                {
                    ReadVersion = version;
                    _versionSeen = true;
                    return true;
                }
                problem = "expected a version number";
                return false;
            case "head_bob":
                return TryBool(value, v => HeadBob = v, out problem);
            case "player_names":
                return TryBool(value, v => PlayerNames = v, out problem);
            case "show_stats":
                return TryBool(value, v => ShowStats = v, out problem);
            case "minimap":
                return TryBool(value, v => Minimap = v, out problem);
            case "minimap_zoom":
                if (int.TryParse(value, NumberStyles.None, c, out int minimapZoom) && minimapZoom >= 100 && minimapZoom <= 800)
                {
                    MinimapZoomPercent = minimapZoom;
                    return true;
                }
                problem = "expected 100 to 800";
                return false;
            case "minimap_size":
                if (int.TryParse(value, NumberStyles.None, c, out int minimapSize) && minimapSize >= 50 && minimapSize <= 300)
                {
                    MinimapSizePercent = minimapSize;
                    return true;
                }
                problem = "expected 50 to 300";
                return false;
            case "minimap_text":
                if (int.TryParse(value, NumberStyles.None, c, out int minimapText) && minimapText >= 50 && minimapText <= 400)
                {
                    MinimapTextPercent = minimapText;
                    return true;
                }
                problem = "expected 50 to 400";
                return false;
            case "highlight_tile":
                return TryBool(value, v => HighlightTile = v, out problem);
            case "highlight_thing":
                return TryBool(value, v => HighlightThing = v, out problem);
            case "port_status":
                return TryBool(value, v => PortStatus = v, out problem);
            case "third_person_fov":
                return TryBool(value, v => ThirdPersonFov = v, out problem);
            case "aim_facing":
                if (float.TryParse(value, NumberStyles.Float, c, out float aimFacing) && aimFacing >= 0f && aimFacing <= MaxAimFacingSeconds)
                {
                    AimFacingSeconds = aimFacing;
                    return true;
                }
                problem = "expected seconds from 0 to 5";
                return false;
            case "camera_shake":
                return TryBool(value, v => CameraShake = v, out problem);
            case "zero_g_bob":
                return TryZeroGLevel(value, v => ZeroGBob = v, out problem);
            case "zero_g_roll":
                return TryZeroGLevel(value, v => ZeroGRoll = v, out problem);
            case "zone_labels":
                return TryBool(value, v => ZoneLabels = v, out problem);
            case "hatch_cues":
                return TryBool(value, v => HatchCues = v, out problem);
            case "block_windows":
                return TryBool(value, v => BlockWindows = v, out problem);
            case "colorblind":
                if (TryParseColorblind(value, out ColorblindMode mode))
                {
                    Colorblind = mode;
                    return true;
                }
                problem = "expected off, red_green, blue_yellow or mono";
                return false;
            case "fullscreen_strength":
                if (int.TryParse(value, NumberStyles.None, c, out int strength) && strength <= 100)
                {
                    FullscreenStrengthPercent = strength;
                    return true;
                }
                problem = "expected 0 to 100";
                return false;
            case "ui_scale":
                if (int.TryParse(value, NumberStyles.None, c, out int scale) && scale >= 50 && scale <= 300)
                {
                    UiScalePercent = scale;
                    return true;
                }
                problem = "expected 50 to 300";
                return false;
            case "window_scale":
                if (int.TryParse(value, NumberStyles.None, c, out int windowScale) && windowScale >= MinWindowScale && windowScale <= MaxWindowScale)
                {
                    WindowScalePercent = windowScale;
                    return true;
                }
                problem = $"expected {MinWindowScale} to {MaxWindowScale}";
                return false;
            case "chat_scale":
                if (int.TryParse(value, NumberStyles.None, c, out int chatScale) && chatScale >= MinWindowScale && chatScale <= MaxWindowScale)
                {
                    ChatScalePercent = chatScale;
                    return true;
                }
                problem = $"expected {MinWindowScale} to {MaxWindowScale}";
                return false;
            case "chat_lines_scale":
                if (int.TryParse(value, NumberStyles.None, c, out int chatLinesScale) && chatLinesScale >= MinChatLinesScale && chatLinesScale <= MaxChatLinesScale)
                {
                    ChatLinesScalePercent = chatLinesScale;
                    return true;
                }
                problem = $"expected {MinChatLinesScale} to {MaxChatLinesScale}";
                return false;
            case "hud_text_opacity":
                if (int.TryParse(value, NumberStyles.None, c, out int textOpacity) && textOpacity >= 10 && textOpacity <= 100)
                {
                    HudTextOpacityPercent = textOpacity;
                    return true;
                }
                problem = "expected 10 to 100";
                return false;
            case "window_opacity":
                if (int.TryParse(value, NumberStyles.None, c, out int opacity) && opacity >= 10 && opacity <= 100)
                {
                    WindowOpacityPercent = opacity;
                    return true;
                }
                problem = "expected 10 to 100";
                return false;
            case "master_volume":
                return TryPercent(value, v => MasterVolumePercent = v, out problem);
            case "effects_volume":
                return TryPercent(value, v => EffectsVolumePercent = v, out problem);
            case "music_volume":
                return TryPercent(value, v => MusicVolumePercent = v, out problem);
            case "interface_volume":
                return TryPercent(value, v => InterfaceVolumePercent = v, out problem);
            case "ambience_volume":
                return TryPercent(value, v => AmbienceVolumePercent = v, out problem);
            case "ship_hum_volume":
                return TryPercent(value, v => ShipHumVolumePercent = v, out problem);
            case "ooc_ping_volume":
                return TryPercent(value, v => OocPingVolumePercent = v, out problem);
            case "reverb":
                return TryBool(value, v => Reverb = v, out problem);
            case "text_to_speech":
                return TryBool(value, v => TextToSpeech = v, out problem);
            case "speech_volume":
                return TryPercent(value, v => SpeechVolumePercent = v, out problem);
            case "speech_to_text":
                return TryBool(value, v => SpeechToText = v, out problem);
            case "radio_speech":
                if (Enum.TryParse(value, true, out RadioSpeech radio) && Enum.IsDefined(radio))
                {
                    RadioSpeech = radio;
                    problem = null;
                    return true;
                }
                problem = "expected all, departmental or off";
                return false;
            case "radio_speech_volume":
                return TryPercent(value, v => RadioSpeechVolumePercent = v, out problem);
            case "hear_own_radio":
                return TryBool(value, v => HearOwnRadio = v, out problem);
            case "speech_voice":
            case "speech_pitch":
                problem = null;
                return true; // D309's voice in Settings, moved to Character Setup (D311): an older file's lines are ignored
            case "chat_translation":
                return TryBool(value, v => ChatTranslation = v, out problem);
            case "chat_translation_language":
                if (Speech.ChatTranslation.IsTarget(value))
                {
                    ChatTranslationLanguage = value;
                    problem = null;
                    return true;
                }
                problem = "expected a translation language code";
                return false;
            case "speech_to_text_language":
                if (value.Length == 0 || Speech.SpeechLanguages.IsKnown(value))
                {
                    SpeechToTextLanguage = value;
                    problem = null;
                    return true;
                }
                problem = "expected a language code or nothing";
                return false;
            case "speech_languages":
            {
                string[] codes = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (codes.All(Speech.SpeechLanguages.IsKnown))
                {
                    SpeechLanguageCodes = codes.Distinct().ToList();
                    problem = null;
                    return true;
                }
                problem = "expected language codes separated by commas";
                return false;
            }
            case "retro_lines":
            case "retro_dither":
                problem = null;
                return true; // D297's retro look, removed (the owner, 2026-10-31): an older file's lines are ignored
            case "tonemap":
                return TryChoice(value, PostLook.Tonemaps.Select(x => x.Value), v => Tonemap = v, out problem);
            case "saturation":
                return TryRange(value, 0, 200, v => SaturationPercent = v, out problem);
            case "contrast":
                return TryRange(value, 50, 150, v => ContrastPercent = v, out problem);
            case "split_tone":
                return TryPercent(value, v => SplitTonePercent = v, out problem);
            case "fog":
                return TryChoice(value, PostLook.Fogs.Select(x => x.Tiles), v => FogTiles = v, out problem);
            case "grain":
                return TryPercent(value, v => GrainPercent = v, out problem);
            case "vignette":
                return TryPercent(value, v => VignettePercent = v, out problem);
            case "bloom":
                return TryRange(value, 0, 300, v => BloomPercent = v, out problem);
            case "bloom_threshold":
                return TryChoice(value, PostLook.BloomThresholds.Select(x => x.Percent), v => BloomThresholdPercent = v, out problem);
            case "outline":
                return TryChoice(value, PostLook.Outlines.Select(x => x.Width), v => OutlineWidth = v, out problem);
            case "outline_strength":
                return TryPercent(value, v => OutlinePercent = v, out problem);
            case "radial_opacity":
                if (int.TryParse(value, NumberStyles.None, c, out int radialOpacity) && radialOpacity >= 10 && radialOpacity <= 100)
                {
                    RadialOpacityPercent = radialOpacity;
                    return true;
                }
                problem = "expected 10 to 100";
                return false;
            case "window_screen":
                string[] screen = value.Split(',');
                if (screen.Length == 2 && int.TryParse(screen[0].Trim(), NumberStyles.None, c, out int screenW) && int.TryParse(screen[1].Trim(), NumberStyles.None, c, out int screenH)
                    && screenW is >= 100 and <= 20000 && screenH is >= 100 and <= 20000)
                {
                    WindowScreen = (screenW, screenH);
                    return true;
                }
                problem = "expected w,h in pixels";
                return false;
            case "crosshair_scale":
                if (int.TryParse(value, NumberStyles.None, c, out int crosshairScale) && crosshairScale >= 50 && crosshairScale <= 400)
                {
                    CrosshairScalePercent = crosshairScale;
                    return true;
                }
                problem = "expected 50 to 400";
                return false;
            case "dev_ui_scale":
                if (int.TryParse(value, NumberStyles.None, c, out int devScale) && devScale >= 50 && devScale <= 400)
                {
                    DevUiScalePercent = devScale;
                    return true;
                }
                problem = "expected 50 to 400";
                return false;
            case "fov":
                if (float.TryParse(value, NumberStyles.Float, c, out float fov) && fov >= MinFov && fov <= MaxFov)
                {
                    FovDegrees = fov;
                    return true;
                }
                problem = $"expected {MinFov} to {MaxFov}";
                return false;
            case "fullscreen_resolution":
                if (string.Equals(value, "native", StringComparison.OrdinalIgnoreCase))
                {
                    FullscreenResolution = (0, 0);
                    return true;
                }
                string[] resolution = value.Split(',');
                if (resolution.Length == 2 && int.TryParse(resolution[0].Trim(), NumberStyles.None, c, out int resolutionW) && int.TryParse(resolution[1].Trim(), NumberStyles.None, c, out int resolutionH)
                    && resolutionW is >= 320 and <= 20000 && resolutionH is >= 200 and <= 20000)
                {
                    FullscreenResolution = (resolutionW, resolutionH);
                    return true;
                }
                problem = "expected native or w,h in pixels";
                return false;
            case "quality":
                if (RenderPreset.TryParse(value, out RenderPreset preset))
                {
                    Quality = preset.Name;
                    return true;
                }
                if (string.Equals(value, RenderPreset.CustomName, StringComparison.OrdinalIgnoreCase))
                {
                    Quality = RenderPreset.CustomName;
                    return true;
                }
                problem = "expected low, medium, high, max or custom";
                return false;
            case "shadowed_lights":
                // D168's override, before D288's settings: "preset" kept the preset's number, a number is the lamps-with-shadows setting now
                if (string.Equals(value, "preset", StringComparison.OrdinalIgnoreCase))
                    return true;
                return TrySet("gfx.shadowed_lights", value, out problem);
            case "wall_height":
                return true; // the owner (2026-11-20, D119 revised): the wall height is the server's (MapGrid, protocol 91); an old file's line is ignored
            case "eye_height":
                if (WorldScale.TryParseEyeHeight(value, out float eyeHeight))
                {
                    EyeHeight = eyeHeight;
                    return true;
                }
                problem = $"expected metres from {WorldScale.MinEyeHeight} to {WorldScale.MaxEyeHeight}";
                return false;
            case "item_scale":
                if (WorldScale.TryParseItemScale(value, out float itemScale))
                {
                    ItemScale = itemScale;
                    return true;
                }
                problem = $"expected a scale from {WorldScale.MinItemScale} to {WorldScale.MaxItemScale}";
                return false;
            case "mouse_sensitivity":
                if (float.TryParse(value, NumberStyles.Float, c, out float sensitivity) && sensitivity > 0.0002f && sensitivity < 0.05f)
                {
                    MouseSensitivity = sensitivity;
                    return true;
                }
                problem = "expected a number from 0.0002 to 0.05";
                return false;
        }
        if (name.StartsWith("bind.", StringComparison.Ordinal))
            return TrySetBinding(name.Substring(5), value, out problem);
        if (name.StartsWith("dev.", StringComparison.Ordinal))
            return Dev.TrySet(name.Substring(4), value, out problem);
        if (name.StartsWith("window.", StringComparison.Ordinal))
            return TrySetWindow(name.Substring(7), value, out problem);
        if (name.StartsWith("window_size.", StringComparison.Ordinal))
            return TrySetWindowSize(name.Substring(12), value, out problem);
        if (name.StartsWith("window_scale.", StringComparison.Ordinal))
            return TrySetWindowScale(name.Substring(13), value, out problem);
        if (name.StartsWith("gfx.", StringComparison.Ordinal) && GraphicsSetting.Find(name.Substring(4)) is GraphicsSetting setting)
        {
            if (int.TryParse(value, NumberStyles.None, c, out int choice) && setting.Offers(choice))
            {
                SetGraphics(setting, choice);
                return true;
            }
            problem = "expected one of " + string.Join(", ", setting.Choices.Select(ch => ch.Value.ToString(c)));
            return false;
        }
        problem = "unknown option";
        return false;
    }

    /// <summary>Sets the keys of an action from its id and a comma-separated list of key names.</summary>
    /// <summary>M5 slice 3: <c>window.NAME=x,y</c>, a tgui window kind's position (pixels, -10000 to 10000 each).</summary>
    public bool TrySetWindow(string name, string value, out string problem)
    {
        problem = null;
        if (!Anomaly.Client.Shared.Tgui.TguiWindows.IsValidName(name))
        {
            problem = "expected a window name of letters, digits and _";
            return false;
        }
        string[] parts = value.Split(',');
        var c = CultureInfo.InvariantCulture;
        if (parts.Length == 2 && int.TryParse(parts[0].Trim(), NumberStyles.Integer, c, out int x) && int.TryParse(parts[1].Trim(), NumberStyles.Integer, c, out int y)
            && Math.Abs(x) <= 10000 && Math.Abs(y) <= 10000)
        {
            WindowPositions[name] = (x, y);
            return true;
        }
        problem = "expected x,y in pixels";
        return false;
    }

    /// <summary><c>window_size.NAME=w,h</c>, a tgui window kind's size (pixels at 100 %, 100 to 10000 each).</summary>
    public bool TrySetWindowSize(string name, string value, out string problem)
    {
        problem = null;
        if (!Anomaly.Client.Shared.Tgui.TguiWindows.IsValidName(name))
        {
            problem = "expected a window name of letters, digits and _";
            return false;
        }
        string[] parts = value.Split(',');
        var c = CultureInfo.InvariantCulture;
        if (parts.Length == 2 && int.TryParse(parts[0].Trim(), NumberStyles.Integer, c, out int w) && int.TryParse(parts[1].Trim(), NumberStyles.Integer, c, out int h)
            && w is >= 100 and <= 10000 && h is >= 100 and <= 10000)
        {
            WindowSizes[name] = (w, h);
            return true;
        }
        problem = "expected w,h in pixels";
        return false;
    }

    /// <summary>D282: <c>window_scale.NAME=p</c>, a cell window's scale (percent, the windows' scale range).</summary>
    public bool TrySetWindowScale(string name, string value, out string problem)
    {
        problem = null;
        if (!Anomaly.Client.Shared.Tgui.TguiWindows.IsValidName(name))
        {
            problem = "expected a window name of letters, digits and _";
            return false;
        }
        if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int percent) && percent is >= MinWindowScale and <= MaxWindowScale)
        {
            WindowScales[name] = percent;
            return true;
        }
        problem = $"expected a percent from {MinWindowScale} to {MaxWindowScale}";
        return false;
    }

    public bool TrySetBinding(string id, string keys, out string problem)
    {
        problem = null;
        PlayAction? action = KeyBindings.ActionOfId(id);
        if (action == null && KeyBindings.RetiredIds.Contains(id))
            return true; // an action the client no longer has: its keys stay free for today's actions
        if (action == null)
        {
            problem = "unknown action '" + id + "'";
            return false;
        }
        var list = new List<string>();
        foreach (string part in keys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!KeyBindings.IsValidKeyName(part))
            {
                problem = "'" + part + "' is not a key name";
                return false;
            }
            list.Add(part);
        }
        Bindings.Claim(action.Value, list.ToArray());
        return true;
    }

    /// <summary>The words of a palette in the file.</summary>
    public static string ColorblindId(ColorblindMode mode) => mode switch
    {
        ColorblindMode.RedGreen => "red_green",
        ColorblindMode.BlueYellow => "blue_yellow",
        ColorblindMode.Mono => "mono",
        _ => "off",
    };

    public static bool TryParseColorblind(string text, out ColorblindMode mode)
    {
        switch (text)
        {
            case "off": mode = ColorblindMode.Off; return true;
            case "red_green": mode = ColorblindMode.RedGreen; return true;
            case "blue_yellow": mode = ColorblindMode.BlueYellow; return true;
            case "mono": mode = ColorblindMode.Mono; return true;
        }
        mode = ColorblindMode.Off;
        return false;
    }

    /// <summary>A zero-gravity level's name in the file: off, reduced or on.</summary>
    public static string ZeroGLevelId(ZeroGLevel level) => level switch
    {
        ZeroGLevel.Off => "off",
        ZeroGLevel.Reduced => "reduced",
        _ => "on",
    };

    private static bool TryZeroGLevel(string value, Action<ZeroGLevel> set, out string problem)
    {
        problem = null;
        switch (value.ToLowerInvariant())
        {
            case "off":
            case "false":
                set(ZeroGLevel.Off);
                return true;
            case "reduced":
            case "half":
                set(ZeroGLevel.Reduced);
                return true;
            case "on":
            case "true":
                set(ZeroGLevel.On);
                return true;
            default:
                problem = "expected off, reduced or on";
                return false;
        }
    }

    private static bool TryPercent(string value, Action<int> set, out string problem) => TryRange(value, 0, 100, set, out problem);

    private static bool TryRange(string value, int min, int max, Action<int> set, out string problem)
    {
        problem = null;
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int n) && n >= min && n <= max)
        {
            set(n);
            return true;
        }
        problem = string.Create(CultureInfo.InvariantCulture, $"expected {min} to {max}");
        return false;
    }

    private static bool TryChoice(string value, IEnumerable<int> choices, Action<int> set, out string problem)
    {
        problem = null;
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int n) && choices.Contains(n))
        {
            set(n);
            return true;
        }
        problem = "expected " + string.Join(", ", choices);
        return false;
    }

    private static bool TryBool(string value, Action<bool> set, out string problem)
    {
        problem = null;
        switch (value)
        {
            case "true" or "on" or "1" or "yes":
                set(true);
                return true;
            case "false" or "off" or "0" or "no":
                set(false);
                return true;
        }
        problem = "expected true or false";
        return false;
    }
}
