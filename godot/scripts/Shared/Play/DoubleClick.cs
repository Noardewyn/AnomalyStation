using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

/// <summary>
/// Protocol version 51 (M10x slice C): BYOND's double click. Dream Seeker calls <c>DblClick</c> when a second press of the same button
/// lands on the same thing within the system's double-click time, after that press's own <c>Click</c>; a third press starts over.
/// The client's clicks go out on the release of a press (a press that becomes a drag is no click), so the pair is told from the clicks
/// sent: <see cref="Clicked"/> is told every click and says whether it is the second of a double click.
/// </summary>
// DEVIATION(DV202): the pair is two clicks sent on one thing within 0.5 s, not BYOND's second press under the pointer.
public sealed class DoubleClick
{
    /// <summary>The double-click time in seconds: BYOND takes the system's, 500 ms by default on Windows and macOS.</summary>
    public const double Window = 0.5;

    private ClickTarget _last;
    private ClickButton _lastButton;
    private double _lastAt = double.NegativeInfinity;

    /// <summary>A click went out on <paramref name="target"/> at <paramref name="now"/> seconds; true when it makes a double click.</summary>
    public bool Clicked(in ClickTarget target, ClickButton button, double now)
    {
        bool same = _last.Kind == target.Kind && _last.Id == target.Id && _last.X == target.X && _last.Y == target.Y && _last.Z == target.Z;
        if (target.IsSet && same && button == _lastButton && now - _lastAt <= Window)
        {
            _last = ClickTarget.None;
            _lastAt = double.NegativeInfinity;
            return true;
        }
        _last = target;
        _lastButton = button;
        _lastAt = now;
        return false;
    }
}
