using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: M4a P7 (client interaction). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// docs/ai/specs/interaction.md 6.3, D125: a right TAP is tg's right click, a right HOLD opens the radial around the crosshair.

/// <summary>What a radial entry does when it is chosen.</summary>
public enum RadialKind : byte
{
    /// <summary>One of tg's context entries (<see cref="ContextHint"/>): sends its click (button and modifiers) on the target.</summary>
    Hint = 0,

    /// <summary>Examine: a Shift-left click.</summary>
    Examine = 1,

    /// <summary>Throw at: throw mode on, then a left click on the target.</summary>
    ThrowAt = 2,

    /// <summary>Swap hand: the swap-hands keybinding.</summary>
    SwapHand = 3,

    /// <summary>Things here: opens the list of the cell's items (a pile) to pick one of them as the target.</summary>
    ThingsHere = 4,

    /// <summary>Cancel: nothing happens.</summary>
    Cancel = 5,

    /// <summary>One item of the pile (<see cref="RadialEntry.ItemId"/>): a left click on it.</summary>
    Item = 6,

    /// <summary>Protocol version 15 (DV40): a slice of tg's radial menu the server showed (<see cref="RadialEntry.Element"/>): picking it sends a RadialPick of it.</summary>
    ServerChoice = 7,

    /// <summary>Version 36 (D230): the server's Buckle (<see cref="ContextHintAction.DropSelf"/>): a Drop of the own mob onto the target.</summary>
    DropSelf = 8,

    /// <summary>Version 36 (D230): the server's Stop pulling (<see cref="ContextHintAction.StopPulling"/>): the stop-pulling keybinding.</summary>
    StopPulling = 9,

    /// <summary>Version 37 (D238): the server's Strip (<see cref="ContextHintAction.DropOntoSelf"/>): a Drop of the target onto the own mob.</summary>
    DropOntoSelf = 10,

    /// <summary>Version 37 (D240, D244): the hidden verb Sleep (<see cref="KeybindingCode.VerbSleep"/>), offered over a turf.</summary>
    Sleep = 11,

    /// <summary>Version 37 (D240, D244): the hidden verb Succumb (<see cref="KeybindingCode.VerbSuccumb"/>), offered over a turf.</summary>
    Succumb = 12,

    /// <summary>Version 39 (D256): an emote of the emote wheel (<see cref="EmoteWheel.KeyOf"/>): tg's emote keybinding.</summary>
    Emote = 13,

    /// <summary>Version 39 (D256): the emote wheel's "More...": the hidden verb that opens tg's emote panel (<see cref="KeybindingCode.VerbEmotePanel"/>).</summary>
    EmotePanel = 14,

    /// <summary>Version 86 (DV503): the server's entry that is no click (<see cref="ContextHintAction.Extra"/>, the table's Flip): a ContextPick of its label.</summary>
    Extra = 15,
}

/// <summary>One entry of the radial.</summary>
/// <param name="Kind">What it does.</param>
/// <param name="Label">The text (tg's label for a hint, the fixed word otherwise).</param>
/// <param name="Button">A hint's mouse button.</param>
/// <param name="Modifiers">A hint's modifier keys.</param>
/// <param name="ItemId">A pile item's entity id.</param>
/// <param name="Element">Version 15: a server slice's element number (1 based, the page's order).</param>
/// <param name="Icon">Version 15: a server slice's picture (the choice's image), none for a slice without one.</param>
/// <param name="Background">Version 15: the slice's own picture (tg's <c>radial_slice</c>) and its hovered one.</param>
/// <param name="BackgroundFocus">Version 15: the hovered slice.</param>
/// <param name="ClickOnHover">Version 15: tg's <c>click_on_hover</c>: highlighting it picks it.</param>
/// <param name="NextPage">Version 15: tg's next-page slice.</param>
public readonly record struct RadialEntry(RadialKind Kind, string Label, ClickButton Button = ClickButton.None, ClickModifiers Modifiers = ClickModifiers.None, long ItemId = 0,
    int Element = 0, TableRef Icon = default, TableRef Background = default, TableRef BackgroundFocus = default, bool ClickOnHover = false, bool NextPage = false)
{
    /// <summary>The chord of a hint as tg writes it ("LMB", "Shift-RMB"), empty for the fixed entries.</summary>
    public string Chord
    {
        get
        {
            if (Kind != RadialKind.Hint)
                return string.Empty;
            string button = Button switch { ClickButton.Right => "RMB", ClickButton.Middle => "MMB", _ => "LMB" };
            string prefix = string.Empty;
            if ((Modifiers & ClickModifiers.Ctrl) != 0)
                prefix += "Ctrl-";
            if ((Modifiers & ClickModifiers.Alt) != 0)
                prefix += "Alt-";
            if ((Modifiers & ClickModifiers.Shift) != 0)
                prefix += "Shift-";
            return prefix + button;
        }
    }

    /// <summary>
    /// The owner (2026-10-30): the Font Awesome icon drawn on the client's own entry (a hint's guessed from its tg label's verb, the hand
    /// otherwise); null for a pile item, which shows its own sprite, and for a server slice, which has tg's picture.
    /// </summary>
    public string IconName => Kind switch
    {
        RadialKind.Hint => HintIcon(Label),
        RadialKind.Examine => "eye",
        RadialKind.ThrowAt => "bullseye",
        RadialKind.SwapHand => "exchange-alt",
        RadialKind.ThingsHere => "boxes",
        RadialKind.Cancel => "times",
        RadialKind.DropSelf => "chair",
        RadialKind.StopPulling => "unlink",
        RadialKind.DropOntoSelf => "tshirt",
        RadialKind.Sleep => "bed",
        RadialKind.Succumb => "skull",
        RadialKind.Emote => "smile",
        RadialKind.EmotePanel => "ellipsis-h",
        RadialKind.Extra => "undo",
        _ => null,
    };

    private static readonly (string Word, string Icon)[] HintIcons =
    {
        ("pick", "hand-holding"), ("take", "hand-holding"), ("remove", "hand-holding"), ("open", "door-open"), ("close", "door-open"),
        ("attack", "fist-raised"), ("hit", "fist-raised"), ("harm", "fist-raised"), ("pull", "hand-rock"), ("grab", "hand-rock"),
        ("insert", "box"), ("put", "box"), ("place", "box"), ("load", "box"), ("toggle", "power-off"), ("turn", "power-off"),
        ("activate", "power-off"), ("examine", "eye"), ("look", "eye"), ("throw", "paper-plane"), ("fix", "wrench"), ("repair", "wrench"),
    };

    private static string HintIcon(string label)
    {
        foreach ((string word, string icon) in HintIcons)
            if (label.Contains(word, StringComparison.OrdinalIgnoreCase))
                return icon;
        return "hand-pointer";
    }
}

/// <summary>Where the right button is.</summary>
public enum RadialPhase : byte
{
    Closed = 0,

    /// <summary>Pressed and not yet held long enough: a tap if released now.</summary>
    Pressed = 1,

    /// <summary>Held: the radial is open.</summary>
    Open = 2,
}

/// <summary>How a release of the right button ended.</summary>
public enum RadialOutcome : byte
{
    /// <summary>The button was not down.</summary>
    None = 0,

    /// <summary>Released within the tap time: tg's right click.</summary>
    Tap = 1,

    /// <summary>Released over an entry: <c>choice</c> says which.</summary>
    Choice = 2,

    /// <summary>Released in the centre (or the radial was never aimed): nothing is sent.</summary>
    Cancel = 3,
}

/// <summary>
/// The timing and the aiming of the radial (D125): a press that is released within <see cref="HoldSeconds"/> is a tap; held longer it opens
/// around the crosshair, the mouse's movement since it opened picks the entry by direction (the mouse is captured, so the movement is
/// relative), and the release over an entry chooses it, in the centre cancels. A pure state machine: the scene feeds it the times and the motion.
/// </summary>
public sealed class RadialModel
{
    /// <summary>The longest a right press may last and still be a tap (interaction.md 4.5).</summary>
    public const double HoldSeconds = 0.25;

    /// <summary>The distance the pointer must move from the centre to aim at an entry (pixels of accumulated mouse movement).</summary>
    public const double DeadZone = 28.0;

    /// <summary>The farthest the aiming vector goes (pixels).</summary>
    public const double MaxReach = 140.0;

    private List<RadialEntry> _entries = new();
    private double _pressedAt;
    private double _x, _y;

    public RadialPhase Phase { get; private set; }

    /// <summary>The entries now (empty until the scene sets them when the radial opens).</summary>
    public IReadOnlyList<RadialEntry> Entries => _entries;

    /// <summary>The entry the aiming vector points at; -1 in the centre.</summary>
    public int Highlight { get; private set; } = -1;

    /// <summary>The aiming vector since the radial opened (pixels, y down).</summary>
    public (double X, double Y) Aim => (_x, _y);

    /// <summary>True while it is the pile list ("Things here") that is shown.</summary>
    public bool ShowingPile { get; private set; }

    /// <summary>Version 15: the server's radial menu shown (<see cref="RadialMenuMessage.MenuId"/>), 0 for the client's own radial.</summary>
    public long ServerMenu { get; private set; }

    /// <summary>Version 15: the server menu's centre button (a close) and its pictures, when it has one.</summary>
    public bool ServerCloseButton { get; private set; }

    public TableRef ServerCenter { get; private set; }

    public TableRef ServerCenterFocus { get; private set; }

    /// <summary>Version 15: the server menu's page, "1/2" style, empty for a single page.</summary>
    public string ServerPage { get; private set; } = string.Empty;

    /// <summary>The right button went down at <paramref name="now"/>.</summary>
    public void Press(double now)
    {
        ServerMenu = 0;
        Phase = RadialPhase.Pressed;
        _pressedAt = now;
        _x = _y = 0;
        Highlight = -1;
        ShowingPile = false;
        _entries = new List<RadialEntry>();
    }

    /// <summary>Call every frame while the button is down: true once, when the radial has just opened (the scene then sets its entries).</summary>
    public bool Update(double now)
    {
        if (Phase == RadialPhase.Pressed && now - _pressedAt >= HoldSeconds)
        {
            Phase = RadialPhase.Open;
            return true;
        }
        return false;
    }

    /// <summary>Sets the entries (and clears the aim).</summary>
    public void SetEntries(IEnumerable<RadialEntry> entries, bool pile = false)
    {
        _entries = new List<RadialEntry>(entries);
        ShowingPile = pile;
        _x = _y = 0;
        Highlight = -1;
    }

    /// <summary>Mouse movement while the radial is open (relative pixels).</summary>
    public void Motion(double dx, double dy)
    {
        if (Phase != RadialPhase.Open)
            return;
        _x += dx;
        _y += dy;
        double length = Math.Sqrt(_x * _x + _y * _y);
        if (length > MaxReach)
        {
            _x *= MaxReach / length;
            _y *= MaxReach / length;
        }
        Highlight = IndexAt(_x, _y, _entries.Count, DeadZone);
    }

    /// <summary>Version 15: aims at a point (pixels from the centre, y down), the pointer of cursor mode; the same clamp and pick as <see cref="Motion"/>.</summary>
    public void AimAt(double x, double y)
    {
        if (Phase != RadialPhase.Open)
            return;
        _x = 0;
        _y = 0;
        Motion(x, y);
    }

    /// <summary>
    /// The input script's <c>radial=LABEL</c> (slice T4c): aims at the open radial's entry labelled <paramref name="label"/> (case and outer spaces
    /// ignored) as the mouse would, at its slice's middle, so a <see cref="Release"/> chooses it; with no such entry the aim goes back to the
    /// centre (a release cancels) and it returns false.
    /// </summary>
    public bool AimAtLabel(string label)
    {
        if (Phase != RadialPhase.Open)
            return false;
        string wanted = (label ?? string.Empty).Trim();
        int index = _entries.FindIndex(e => string.Equals((e.Label ?? string.Empty).Trim(), wanted, StringComparison.OrdinalIgnoreCase));
        (double x, double y) = index < 0 ? (0.0, 0.0) : PositionOf(index, _entries.Count, MaxReach);
        AimAt(x, y);
        return index >= 0 && Highlight == index;
    }

    /// <summary>The right button went up at <paramref name="now"/>.</summary>
    public RadialOutcome Release(double now, out RadialEntry choice)
    {
        choice = default;
        RadialPhase was = Phase;
        Phase = RadialPhase.Closed;
        if (was == RadialPhase.Closed)
            return RadialOutcome.None;
        if (was == RadialPhase.Pressed && now - _pressedAt < HoldSeconds)
            return RadialOutcome.Tap;
        if (was == RadialPhase.Open || was == RadialPhase.Pressed)
        {
            if (Highlight >= 0 && Highlight < _entries.Count)
            {
                choice = _entries[Highlight];
                return RadialOutcome.Choice;
            }
            return RadialOutcome.Cancel;
        }
        return RadialOutcome.None;
    }

    /// <summary>Opens a list that stays open without the button held (the pile: "Things here"); <see cref="Confirm"/> or <see cref="Reset"/> ends it.</summary>
    public void OpenSticky(IEnumerable<RadialEntry> entries)
    {
        ServerMenu = 0;
        Phase = RadialPhase.Open;
        SetEntries(entries, pile: true);
    }

    /// <summary>
    /// Version 15 (DV40): shows tg's radial menu the server sent (a new menu or a new page), sticky like the pile list: the mouse aims,
    /// <see cref="Confirm"/> picks the highlighted slice (the centre: a close), number keys pick by position (<see cref="SelectNumber"/>).
    /// The aim restarts when the menu is another one, and is kept over a new page of the same menu.
    /// </summary>
    public void OpenServer(RadialMenuMessage menu)
    {
        ArgumentNullException.ThrowIfNull(menu);
        bool samePage = Phase == RadialPhase.Open && ServerMenu == menu.MenuId;
        (double x, double y) = (_x, _y);
        Phase = RadialPhase.Open;
        SetEntries(BuildServer(menu));
        ServerMenu = menu.MenuId;
        ServerCloseButton = (menu.Flags & RadialMenuFlags.CloseButton) != 0;
        ServerCenter = menu.Center;
        ServerCenterFocus = menu.CenterFocus;
        ServerPage = menu.Pages > 1 ? menu.Page + "/" + menu.Pages : string.Empty;
        if (samePage)
            Motion(x - _x, y - _y);
    }

    /// <summary>Version 15: the server's slices of a menu, in its order (tg's: clockwise from the top), each its element number.</summary>
    public static List<RadialEntry> BuildServer(RadialMenuMessage menu)
    {
        var entries = new List<RadialEntry>(menu.Elements.Count);
        for (int i = 0; i < menu.Elements.Count; i++)
        {
            RadialSlice slice = menu.Elements[i];
            entries.Add(new RadialEntry(RadialKind.ServerChoice, slice.Name, Element: i + 1, Icon: slice.Icon, Background: slice.Background,
                BackgroundFocus: slice.BackgroundFocus, ClickOnHover: (slice.Flags & RadialElementFlags.ClickOnHover) != 0,
                NextPage: (slice.Flags & RadialElementFlags.NextPage) != 0));
        }
        return entries;
    }

    /// <summary>Version 15: highlights the entry at position <paramref name="number"/> (1 based, the keys 1 to 9); false outside the list.</summary>
    public bool SelectNumber(int number)
    {
        if (Phase != RadialPhase.Open || number < 1 || number > _entries.Count)
            return false;
        Highlight = number - 1;
        return true;
    }

    /// <summary>Chooses the highlighted entry of a sticky list (or cancels in the centre) and closes it.</summary>
    public RadialOutcome Confirm(out RadialEntry choice)
    {
        choice = default;
        if (Phase != RadialPhase.Open)
            return RadialOutcome.None;
        Phase = RadialPhase.Closed;
        if (Highlight >= 0 && Highlight < _entries.Count)
        {
            choice = _entries[Highlight];
            return RadialOutcome.Choice;
        }
        return RadialOutcome.Cancel;
    }

    /// <summary>Closes without an outcome (the window lost focus, the mouse was released by Esc).</summary>
    public void Reset()
    {
        ServerMenu = 0;
        Phase = RadialPhase.Closed;
        Highlight = -1;
        _entries = new List<RadialEntry>();
    }

    /// <summary>The entry a vector points at: <paramref name="count"/> entries are spread evenly from the top, clockwise; the centre disc is none.</summary>
    public static int IndexAt(double x, double y, int count, double deadZone)
    {
        if (count <= 0 || x * x + y * y < deadZone * deadZone)
            return -1;
        double degrees = Math.Atan2(y, x) * 180.0 / Math.PI + 90.0; // 0 is up, clockwise
        degrees = ((degrees % 360.0) + 360.0) % 360.0;
        double sector = 360.0 / count;
        return (int)Math.Floor((degrees + sector / 2.0) / sector) % count;
    }

    /// <summary>The centre of entry <paramref name="index"/> of <paramref name="count"/> on a circle of <paramref name="radius"/> (y down), the first one at the top.</summary>
    public static (double X, double Y) PositionOf(int index, int count, double radius)
    {
        double angle = (-90.0 + 360.0 * index / Math.Max(1, count)) * Math.PI / 180.0;
        return (Math.Cos(angle) * radius, Math.Sin(angle) * radius);
    }

    /// <summary>
    /// The entries for a target (interaction.md 6.3): tg's context entries first, as the server sent them, then the fixed ones: Examine, Throw at,
    /// Swap hand, Sleep and Succumb (over a turf, D240), Things here (when a pile has more than one thing) and Cancel.
    /// </summary>
    public static List<RadialEntry> Build(ContextHintsMessage hints, int pileCount, bool targetIsEntity)
    {
        var entries = new List<RadialEntry>();
        if (hints != null)
        {
            foreach (ContextHint hint in hints.Hints)
            {
                RadialKind kind = hint.Action switch
                {
                    ContextHintAction.DropSelf => RadialKind.DropSelf,
                    ContextHintAction.StopPulling => RadialKind.StopPulling,
                    ContextHintAction.DropOntoSelf => RadialKind.DropOntoSelf,
                    ContextHintAction.Extra => RadialKind.Extra,
                    _ => RadialKind.Hint,
                };
                entries.Add(new RadialEntry(kind, hint.Label, hint.Button, hint.Modifiers));
            }
        }
        if (targetIsEntity)
            entries.Add(new RadialEntry(RadialKind.Examine, "Examine", ClickButton.Left, ClickModifiers.Shift));
        entries.Add(new RadialEntry(RadialKind.ThrowAt, "Throw at"));
        entries.Add(new RadialEntry(RadialKind.SwapHand, "Swap hand"));
        if (!targetIsEntity)
        {
            // D240: the verbs a tg player runs from the verb panel (M9's), offered when the radial opens over no thing; the server's
            // verbs check whether they apply (tg's "You are already sleeping!", "You are unable to succumb to death!")
            entries.Add(new RadialEntry(RadialKind.Sleep, "Sleep"));
            entries.Add(new RadialEntry(RadialKind.Succumb, "Succumb"));
        }
        if (pileCount > 1)
            entries.Add(new RadialEntry(RadialKind.ThingsHere, "Things here"));
        entries.Add(new RadialEntry(RadialKind.Cancel, "Cancel"));
        return entries;
    }
}
