// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/proficient_miner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Component given to mobs that can mine when moving
// tg: code/datums/components/proficient_miner.dm:2
public partial class datum_component_proficient_miner : datum_component
{
    // tg: code/datums/components/proficient_miner.dm:10
    [DmParams("mining_speed", "pass_driver")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object mining_speed = Dm.arg(__extra, 0) ?? 0f;
        object pass_driver = Dm.arg(__extra, 1) ?? FALSE;
        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;
        this.mining_speed = Dm.num(mining_speed);
        this.pass_driver = Dm.num(pass_driver);
        return null;
    }

    // tg: code/datums/components/proficient_miner.dm:16
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_MOVABLE_BUMP, nameof(on_bump));
        RegisterSignal(parent, COMSIG_LIVING_UNARMED_ATTACK, nameof(on_unarmed_attack));
    }

    // tg: code/datums/components/proficient_miner.dm:20
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_MOVABLE_BUMP, COMSIG_LIVING_UNARMED_ATTACK));
    }

    // tg: code/datums/components/proficient_miner.dm:23
    [SignalHandler]
    public virtual void on_unarmed_attack(mob_living source = null, atom target = null, object proximity = null, object modifiers = null)
    {
        if (!Dm.truthy(proximity))
            return;
        try_mine(source, target);
    }

    // tg: code/datums/components/proficient_miner.dm:30
    [SignalHandler]
    public virtual void on_bump(atom_movable source = null, atom target = null)
    {
        if (last_bumpmine_tick == world.time)
            return;
        try_mine(source, target);
    }

    // tg: code/datums/components/proficient_miner.dm:37
    public virtual void try_mine(atom_movable source = null, atom target = null)
    {
        if (!ismineralturf(target))
            return;

        mob_living user = null;
        if (isliving(parent))
        {
            user = parent as mob_living;
            if (IS_UNCONSCIOUS_OR_CRIT(user))
                return;
        }

        turf_closed_mineral mineral_wall = target as turf_closed_mineral;
        if (mineral_wall is turf_closed_mineral_gibtonite gibtonite_wall)
        {
            if (!Dm.eq(gibtonite_wall.stage, GIBTONITE_UNSTRUCK))
                return;
        }

        if (user != null && mining_speed > 0)
        {
            Waiting.Detach(slow_mine(user, mineral_wall)); // INVOKE_ASYNC
            return;
        }

        mob_living driver = null;
        if (Dm.truthy(pass_driver) && Dm.length(user.buckled_mobs) > 0)
            driver = user.buckled_mobs[1f] as mob_living;

        last_bumpmine_tick = world.time;
        parent.SEND_SIGNAL(COMSIG_PROFICIENT_MINER_MINED, Dm.list(parent, mineral_wall, (object)driver ?? user));
        Waiting.NoSleep(mineral_wall.gets_drilled(source as mob));
    }

    // tg: code/datums/components/proficient_miner.dm:65
    public virtual async DmTask slow_mine(mob_living user = null, turf_closed_mineral mineral_wall = null)
    {
        if (Dm.truthy(TIMER_COOLDOWN_RUNNING(mineral_wall, REF(user)))) //prevents mining turfs in progress
            return;

        float mining_delay = mineral_wall.tool_mine_speed * mining_speed;
        // TIMER_COOLDOWN_START(mineral_wall, REF(user), mining_delay) (inline, rules 11.1 (f))
        LAZYSET(ref mineral_wall.cooldowns, REF(user), _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), mineral_wall, REF(user)), mining_delay, file: "code/datums/components/proficient_miner.dm", line: 71));
        DmList mine_sounds = TgState.Current.datum_component_proficient_miner__slow_mine__mine_sounds ??= Dm.list(Dm.resource("sound/effects/pickaxe/picaxe1.ogg"), Dm.resource("sound/effects/pickaxe/picaxe2.ogg"), Dm.resource("sound/effects/pickaxe/picaxe3.ogg"));
        playsound(user, pick(mine_sounds), 50);

        mob_living driver = null;
        if (Dm.truthy(pass_driver) && Dm.length(user.buckled_mobs) > 0)
            driver = user.buckled_mobs[1f] as mob_living;

        if (!Dm.truthy(await do_after(user, mining_delay, mineral_wall, bar_override: driver)))
        {
            TIMER_COOLDOWN_END(mineral_wall, REF(user)); //if we fail we can start again immediately
            return;
        }

        if (mining_delay > MIN_TOOL_SOUND_DELAY)
            playsound(user, pick(mine_sounds), 50);

        // istype(mineral_wall): the wall is still a mineral turf (a drilled one is no longer)
        if (mineral_wall is turf_closed_mineral && !QDELETED(mineral_wall))
        {
            parent.SEND_SIGNAL(COMSIG_PROFICIENT_MINER_MINED, Dm.list(parent, mineral_wall, (object)driver ?? user));
            await mineral_wall.gets_drilled((mob)driver ?? user);
        }
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/proficient_miner.dm:73 (var/static/list/mine_sounds)
    internal DmList datum_component_proficient_miner__slow_mine__mine_sounds;
}
