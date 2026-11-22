namespace Anomaly.Tools.Bots;

/// <summary>Another replicated body as the shove planner sees it: a capsule mob with a continuous position (1/256 tile).</summary>
/// <param name="Id">Its entity id.</param>
/// <param name="PosX">The centre's x in 1/256 tile.</param>
/// <param name="PosY">The centre's y in 1/256 tile.</param>
/// <param name="Lying">True when its posture byte says lying (a shove of a lying mob does nothing: tg's <c>can_disarm</c> wants a standing target, and lying capsules do not collide).</param>
public readonly record struct OtherBody(long Id, int PosX, int PosY, bool Lying);

/// <summary>What the shove planner wants this tick.</summary>
/// <param name="HasTarget">False when no standing body is in sight: the bot wanders.</param>
/// <param name="TargetId">The nearest standing body.</param>
/// <param name="Wish">Walk toward it, facing it.</param>
/// <param name="Click">True when the bot should send ONE right click on <see cref="TargetId"/> now.</param>
/// <param name="DistanceTiles">The distance to the target in tiles.</param>
public readonly record struct ShoveStep(bool HasTarget, long TargetId, BotWish Wish, bool Click, double DistanceTiles);

/// <summary>
/// The shove behaviour as a function of plain values (M2 scope 2f): the nearest other body that is not lying, walk to it
/// with the wish vector and the yaw facing it, and within <see cref="ReachTiles"/> send one right click on it, then wait
/// <see cref="CooldownTicks"/> before the next (tg's melee click cooldown is 16 ticks; the server drops what comes sooner).
/// </summary>
public sealed class ShovePlanner
{
    /// <summary>The distance within which the bot clicks: 1.5 tiles (the server checks tg's own <c>Adjacent</c> on tiles).</summary>
    public const double ReachTiles = 1.5;

    /// <summary>The fewest ticks between two clicks (tg's <c>CLICK_CD_MELEE</c> is 16 ticks).</summary>
    public const int CooldownTicks = 20;

    private const long ReachSubunitsSquared = (long)(ReachTiles * 256) * (long)(ReachTiles * 256);

    private long _lastClick = long.MinValue / 2;

    /// <summary>Clicks asked for so far.</summary>
    public int Clicks { get; private set; }

    /// <summary>Forgets the cooldown (a new connection).</summary>
    public void Reset()
    {
        _lastClick = long.MinValue / 2;
    }

    /// <summary>
    /// One tick at <paramref name="now"/> (the bot's clock, in ticks) from (<paramref name="ownX"/>, <paramref name="ownY"/>)
    /// in 1/256 tile. The nearest body that is not lying wins; a tie goes to the lower id. Bodies whose <see cref="OtherBody.Id"/>
    /// is <paramref name="ownId"/> are skipped.
    /// </summary>
    public ShoveStep Update(long now, int ownX, int ownY, long ownId, ReadOnlySpan<OtherBody> others)
    {
        bool found = false;
        OtherBody best = default;
        long bestDistance = 0;
        for (int i = 0; i < others.Length; i++)
        {
            OtherBody other = others[i];
            if (other.Lying || other.Id == ownId) continue;
            long dx = other.PosX - ownX;
            long dy = other.PosY - ownY;
            long distance = dx * dx + dy * dy;
            if (!found || distance < bestDistance || (distance == bestDistance && other.Id < best.Id))
            {
                found = true;
                best = other;
                bestDistance = distance;
            }
        }
        if (!found) return default;

        double tx = (best.PosX - ownX) / 256.0;
        double ty = (best.PosY - ownY) / 256.0;
        BotWish wish = bestDistance == 0
            ? default
            : BotWish.Toward(BotWish.BearingOf(tx, ty), Anomaly.Core.Motion.MotionInput.MaxMove);

        bool click = bestDistance <= ReachSubunitsSquared && now - _lastClick >= CooldownTicks;
        if (click)
        {
            _lastClick = now;
            Clicks++;
        }
        return new ShoveStep(true, best.Id, wish, click, Math.Sqrt(bestDistance) / 256.0);
    }
}
