using Anomaly.Protocol;

namespace Anomaly.Tools.Bots;

/// <summary>The bot's own body as the driver sees it.</summary>
/// <param name="Have">False while the own entity is not known yet.</param>
/// <param name="Id">The own entity id.</param>
/// <param name="PosX">The centre's x in 1/256 tile (the predicted one for a predicting bot).</param>
/// <param name="PosY">The centre's y in 1/256 tile.</param>
/// <param name="Z">The level.</param>
/// <param name="Lying">The replicated posture says lying; null when it is not known.</param>
public readonly record struct OwnBody(bool Have, long Id, int PosX, int PosY, int Z, bool? Lying);

/// <summary>A discrete action the driver wants sent this tick.</summary>
public readonly record struct PlannedAction(ActionKind Kind, long Target, ClickButton Button);

/// <summary>
/// The decisions of a walking bot for one tick, from plain values (M2 scope 2l): the wish vector and yaw, and the discrete
/// actions. <see cref="Bot"/> builds the views from its replica and sends what comes out; every rule is tested without a
/// replica or a codec. The concrete behaviours are <see cref="BotBehaviour.Wander"/> (and Churn, which wanders),
/// <see cref="BotBehaviour.Rest"/> and <see cref="BotBehaviour.Shove"/>; with <c>toggles</c> the bot also presses walk/run and
/// combat mode now and then.
/// </summary>
public sealed class BotDriver
{
    private readonly BotBehaviour _behaviour;
    private readonly WanderPlanner _wander;
    private readonly ShovePlanner _shove = new();
    private readonly TourPlanner _tour;
    private readonly RestSchedule _rest;
    private readonly ToggleSchedule _intent;
    private readonly ToggleSchedule _combat;
    private ushort _yaw;
    private bool _wasStopped;

    /// <param name="behaviour">A concrete behaviour (never <see cref="BotBehaviour.Mixed"/>; see <see cref="BotBehaviourNames.Resolve"/>).</param>
    /// <param name="toggles">Press walk/run and combat mode now and then.</param>
    public BotDriver(BotBehaviour behaviour, bool toggles, BotRandom random, WanderSettings wander = null, RestSettings rest = null, TourCoverage coverage = null, TourSettings tour = null)
    {
        if (behaviour == BotBehaviour.Mixed) throw new ArgumentException("Mixed is resolved to a concrete behaviour first", nameof(behaviour));
        _behaviour = behaviour;
        _wander = new WanderPlanner(random, wander);
        _rest = new RestSchedule(random, rest);
        _tour = new TourPlanner(coverage ?? new TourCoverage(), tour);
        if (toggles)
        {
            _intent = new ToggleSchedule(random, minGapSeconds: 20, maxGapSeconds: 60, minHoldSeconds: 3, maxHoldSeconds: 8);
            _combat = new ToggleSchedule(random, minGapSeconds: 30, maxGapSeconds: 90, minHoldSeconds: 5, maxHoldSeconds: 10);
        }
    }

    public BotBehaviour Behaviour => _behaviour;

    public WanderPlanner Wander => _wander;

    public ShovePlanner Shove => _shove;

    /// <summary>M3 P5. The tour planner (used by <see cref="BotBehaviour.Tour"/>).</summary>
    public TourPlanner Tour => _tour;

    public RestSchedule Rest => _rest;

    /// <summary>The walk/run toggle's schedule, null when the bot does not toggle.</summary>
    public ToggleSchedule Intent => _intent;

    /// <summary>The combat-mode toggle's schedule, null when the bot does not toggle.</summary>
    public ToggleSchedule Combat => _combat;

    /// <summary>Forgets the per-connection state (the random stream goes on).</summary>
    public void Reset()
    {
        _wander.Reset();
        _shove.Reset();
        _tour.Reset();
        _rest.Reset();
        _intent?.Reset();
        _combat?.Reset();
        _yaw = 0;
        _wasStopped = false;
    }

    /// <summary>How far a shoving bot goes after a body, in tiles: farther than that, or behind a wall, it wanders on (it has no path finding).</summary>
    public const double ChaseTiles = 10.0;

    // The target is worth walking to when it is near enough and the straight way to it is free of walls (the bot walks the
    // straight line; a wall in between would hold it against the wall for good).
    private bool CanSee(in OwnBody own, in ShoveStep step, ISolidity solidity)
    {
        if (step.DistanceTiles > ChaseTiles) return false;
        if (solidity == null || step.Wish.IsStill) return true;
        double turns = step.Wish.Yaw / 65536.0;
        double need = Math.Max(0, step.DistanceTiles - 0.6);
        return _wander.FreeDistance(own.PosX / 256.0, own.PosY / 256.0, own.Z, turns, need, solidity) >= need;
    }

    /// <summary>
    /// One tick at <paramref name="now"/> (the bot's clock). Appends the actions to <paramref name="actions"/> and returns the
    /// wish. Without an own body the wish is zero and nothing is sent.
    /// </summary>
    public BotWish Decide(long now, in OwnBody own, ReadOnlySpan<OtherBody> others, ISolidity solidity, List<PlannedAction> actions)
    {
        if (!own.Have) return new BotWish(0, 0, _yaw);

        bool mayMove = true;
        if (_behaviour == BotBehaviour.Rest)
        {
            RestStep rest = _rest.Update(now, own.Lying);
            if (rest.SendRest) actions.Add(new PlannedAction(ActionKind.Rest, 0, ClickButton.None));
            mayMove = rest.MayMove;
        }

        if (_intent != null && _intent.Update(now)) actions.Add(new PlannedAction(ActionKind.ToggleMoveIntent, 0, ClickButton.None));
        if (_combat != null && _combat.Update(now)) actions.Add(new PlannedAction(ActionKind.ToggleCombatMode, 0, ClickButton.None));

        if (!mayMove)
        {
            // Lying down: it cannot move, so it asks for nothing and the blocked rule must not count these ticks.
            _wasStopped = true;
            return new BotWish(0, 0, _yaw);
        }
        if (_wasStopped)
        {
            _wasStopped = false;
            _wander.ResumeAfterPause();
        }

        BotWish wish;
        if (_behaviour == BotBehaviour.Shove)
        {
            ShoveStep step = _shove.Update(now, own.PosX, own.PosY, own.Id, others);
            if (step.HasTarget && CanSee(own, step, solidity))
            {
                _wasStopped = true; // the wander planner sat out these ticks: its blocked rule starts again afterwards
                if (step.Click) actions.Add(new PlannedAction(ActionKind.Click, step.TargetId, ClickButton.Right));
                // Nose to nose (the same spot) there is no direction to face: keep the last one.
                wish = step.Wish.IsStill && step.DistanceTiles == 0 ? new BotWish(0, 0, _yaw) : step.Wish;
                _yaw = wish.Yaw;
                return wish;
            }
        }

        if (_behaviour == BotBehaviour.Tour && solidity is IRouteMap routes)
        {
            wish = _tour.Update(now, true, own.PosX, own.PosY, own.Z, routes);
            if (!wish.IsStill) _yaw = wish.Yaw;
            else wish = new BotWish(0, 0, _yaw);
            return wish;
        }

        wish = _wander.Update(true, own.PosX, own.PosY, own.Z, solidity);
        _yaw = wish.Yaw;
        return wish;
    }
}
