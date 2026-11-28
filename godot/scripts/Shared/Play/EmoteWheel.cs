namespace Anomaly.Client.Shared;

// M9 slice B (D256, docs/ai/specs/chat.md 5). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// The emote wheel on its key (Y): held and released over a segment it sends that emote (tg's emote keybinding, protocol version 39);
/// tapped it stays open until a click picks a segment or Esc closes it. Eight of tg's emotes and "More...", which opens tg's emote
/// panel. The aiming and timing are the radial's (<see cref="RadialModel"/>).
/// </summary>
public sealed class EmoteWheel
{
    /// <summary>The default eight (chat.md 5): tg emote keys a human can run.</summary>
    public static readonly IReadOnlyList<string> DefaultKeys = new[] { "wave", "nod", "laugh", "shrug", "clap", "salute", "sigh", "scream" };

    /// <summary>The label of the ninth segment, the emote panel.</summary>
    public const string MoreLabel = "More...";

    /// <summary>The radial's state: the aim, the highlight, the phase.</summary>
    public RadialModel Model { get; } = new();

    /// <summary>Open after a tap: it waits for a click or Esc.</summary>
    public bool Sticky { get; private set; }

    /// <summary>Drawn now.</summary>
    public bool IsOpen => Model.Phase == RadialPhase.Open;

    /// <summary>The segments: the eight emotes (tg's panel writes a name with its first letter capitalized) and "More...".</summary>
    public static List<RadialEntry> Entries()
    {
        var entries = new List<RadialEntry>(DefaultKeys.Count + 1);
        foreach (string key in DefaultKeys)
            entries.Add(new RadialEntry(RadialKind.Emote, char.ToUpperInvariant(key[0]) + key.Substring(1)));
        entries.Add(new RadialEntry(RadialKind.EmotePanel, MoreLabel));
        return entries;
    }

    /// <summary>The emote key of an emote segment, null for any other.</summary>
    public static string KeyOf(in RadialEntry entry) => entry.Kind == RadialKind.Emote ? entry.Label.ToLowerInvariant() : null;

    /// <summary>The key went down (ignored while the wheel waits after a tap).</summary>
    public void Press(double now)
    {
        if (!Sticky)
            Model.Press(now);
    }

    /// <summary>Every frame: the wheel opens once the key is held long enough.</summary>
    public void Update(double now)
    {
        if (Model.Update(now))
            Model.SetEntries(Entries());
    }

    /// <summary>The key went up: the segment aimed at (null over the centre); a tap opens the wheel until a click or Esc.</summary>
    public RadialEntry? Release(double now)
    {
        if (Sticky)
            return null;
        switch (Model.Release(now, out RadialEntry choice))
        {
            case RadialOutcome.Tap:
                Model.OpenSticky(Entries());
                Sticky = true;
                return null;
            case RadialOutcome.Choice:
                return choice;
            default:
                return null;
        }
    }

    /// <summary>A click while the wheel waits: the segment aimed at (null over the centre); the wheel closes.</summary>
    public RadialEntry? Click()
    {
        if (!Sticky)
            return null;
        Sticky = false;
        return Model.Confirm(out RadialEntry choice) == RadialOutcome.Choice ? choice : null;
    }

    /// <summary>Closes it without a pick (Esc, another mouse button, the window losing the mouse).</summary>
    public void Close()
    {
        Sticky = false;
        Model.Reset();
    }
}
