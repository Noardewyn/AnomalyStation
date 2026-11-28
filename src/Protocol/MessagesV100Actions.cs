using System.Linq;

namespace Anomaly.Protocol;

// Protocol version 100 also (ui-overhaul.md section 15, DV698; docs/ai/specs/protocol.md section 100; the codec: Codec.V100Actions.cs).
// - Actions (server to client, State channel, after the tick's Delta, when it changed, and in a join): the own mob's action buttons on its screen
//   (tg's /atom/movable/screen/movable/action_button) with their actions' names and the state tg shows on a button that its picture does not say
//   for certain (whether the action is active, tg's is_action_active, and a cooldown action's cooldown, its end and length in server ticks); and
//   the screen objects of tg's action palette (the palette button, its scroll arrows, the landing spots), which the client does not draw because it
//   lays the buttons out itself (the actions menu and its nine slots).

/// <summary>
/// Version 100: one action button: the button's screen object (<see cref="Button"/>, the id a click names: tg's <c>action_button/Click</c>), the
/// action's name, whether tg shows it active, and its cooldown: the server tick it ends at and how many ticks it lasts (both 0 when it is not cooling
/// down).
/// </summary>
public readonly record struct ActionButtonState(long Button, string Name, bool Active, long CooldownEnd = 0, int CooldownTicks = 0)
{
    /// <summary>The share of the cooldown still to run at server tick <paramref name="tick"/>: 1 when it starts, 0 when it is over (or there is none).</summary>
    public float CooldownLeft(double tick)
        => CooldownTicks <= 0 ? 0f : (float)Math.Clamp((CooldownEnd - tick) / CooldownTicks, 0.0, 1.0);
}

/// <summary>Version 100: the own action buttons, in the order tg made them (the buttons' ids), and tg's palette's screen objects.</summary>
public sealed class ActionsMessage : IEquatable<ActionsMessage>
{
    /// <summary>No buttons: what a client starts from.</summary>
    public static readonly ActionsMessage None = new();

    /// <summary>The buttons, each once.</summary>
    public IReadOnlyList<ActionButtonState> Buttons { get; init; } = Array.Empty<ActionButtonState>();

    /// <summary>tg's palette button, scroll arrows and landing spots on the own screen.</summary>
    public IReadOnlyList<long> Palette { get; init; } = Array.Empty<long>();

    /// <summary>The button of screen object <paramref name="id"/>, or null.</summary>
    public ActionButtonState? Find(long id)
    {
        foreach (ActionButtonState b in Buttons)
        {
            if (b.Button == id)
                return b;
        }
        return null;
    }

    public bool Equals(ActionsMessage other)
        => other is not null && Buttons.SequenceEqual(other.Buttons) && Palette.SequenceEqual(other.Palette);

    public override bool Equals(object obj) => Equals(obj as ActionsMessage);

    public override int GetHashCode() => HashCode.Combine(Buttons.Count, Palette.Count, Buttons.Count > 0 ? Buttons[0] : default);

    public override string ToString()
        => $"Actions([{string.Join(", ", Buttons.Select(b => b.Button + ":" + b.Name + (b.Active ? " on" : "") + (b.CooldownTicks > 0 ? $" cd {b.CooldownEnd}/{b.CooldownTicks}" : "")))}], palette [{string.Join(", ", Palette)}])";
}
