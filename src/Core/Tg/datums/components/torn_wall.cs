// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/torn_wall.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

/// Component applied to a wall to progressively destroy it; destroyed on the third application; a welder fixes it.
// tg: code/datums/components/torn_wall.dm:13
public partial class datum_component_torn_wall : datum_component
{
    // tg: code/datums/components/torn_wall.dm:1
    private const float TORN_WALL_RUINED = 2;
    // tg: code/datums/components/torn_wall.dm:3
    private const float TORN_WALL_INITIAL = 0;

    // tg: code/datums/components/torn_wall.dm:17
    [DmParams("current_stage")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        await base.Initialize(__extra);
        if (!isclosedturf(parent) || isindestructiblewall(parent))
            return COMPONENT_INCOMPATIBLE;
        object stage = Dm.arg(__extra, 0);
        this.current_stage = Dm.truthy(stage) ? Dm.num(stage) : this.current_stage;
        return null;
    }

    // tg: code/datums/components/torn_wall.dm:23
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examined));
        RegisterSignal(parent, COMSIG_ATOM_TOOL_ACT(TOOL_WELDER), nameof(on_welded));
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(on_update_overlays));
        RegisterSignal(parent, COMSIG_TURF_CHANGE, nameof(on_turf_changed));
        apply_visuals();
    }

    // tg: code/datums/components/torn_wall.dm:30
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        atom atom_parent = parent as atom;
        UnregisterSignal(parent, Dm.list(COMSIG_ATOM_EXAMINE, COMSIG_ATOM_TOOL_ACT(TOOL_WELDER), COMSIG_ATOM_UPDATE_OVERLAYS, COMSIG_TURF_CHANGE));
        atom_parent.update_appearance(UPDATE_ICON);
    }

    // tg: code/datums/components/torn_wall.dm:39
    public override async DmTask InheritComponent(datum_component new_comp = null, object i_am_original = null, params object[] __extra)
    {
        await increase_stage();
    }

    /// Play a fun animation and make our wall look damaged
    // tg: code/datums/components/torn_wall.dm:43
    public virtual void apply_visuals()
    {
        atom atom_parent = parent as atom;
        playsound(atom_parent, Dm.resource("sound/effects/bang.ogg"), 50, vary: TRUE);
        atom_parent.update_appearance(UPDATE_ICON);
        atom_parent.Shake(shake_interval: 0.1f * SECONDS, duration: 0.5f * SECONDS);
    }

    /// Make the effect more dramatic
    // tg: code/datums/components/torn_wall.dm:50
    public virtual async DmTask increase_stage()
    {
        current_stage++;
        if (current_stage != TORN_WALL_RUINED)
        {
            apply_visuals();
            return;
        }
        playsound(parent as atom, Dm.resource("sound/effects/meteorimpact.ogg"), 100, vary: TRUE);

        if (parent is turf_closed_mineral mineral_turf)
        {
            await mineral_turf.gets_drilled();
            return;
        }

        await (parent as turf_closed_wall).dismantle_wall(devastated: TRUE);
    }

    /// Fix it up on weld
    // tg: code/datums/components/torn_wall.dm:65
    [SignalHandler]
    public virtual object on_welded(atom source = null, mob user = null, obj_item tool = null)
    {
        Waiting.Detach(try_repair(source, user, tool)); // INVOKE_ASYNC
        return ITEM_INTERACT_BLOCKING;
    }

    /// Fix us up
    // tg: code/datums/components/torn_wall.dm:71
    public virtual async DmTask try_repair(atom source = null, mob user = null, obj_item tool = null)
    {
        source.balloon_alert(user, "repairing...");
        if (!Dm.truthy(await tool.use_tool(source, user as mob_living, 5 * SECONDS, amount: 2, volume: 50)))
        {
            source.balloon_alert(user, "interrupted!");
            return;
        }
        current_stage--;
        if (current_stage < TORN_WALL_INITIAL)
        {
            qdel(this);
            return;
        }
        source.update_appearance(UPDATE_ICON);
        await try_repair(source, user, tool); // Keep going
    }

    /// Give them a hint
    // tg: code/datums/components/torn_wall.dm:84
    [SignalHandler]
    public virtual void on_examined(atom source = null, mob user = null, DmList examine_list = null)
    {
        string intensity = current_stage == TORN_WALL_INITIAL ? "slightly" : "badly";
        Dm.append(ref examine_list, span_notice(Dm.text("It looks [] damaged.", intensity)));
        Dm.append(ref examine_list, span_info("You may be able to repair it using a welding tool."));
    }

    /// Show a little crack on here
    // tg: code/datums/components/torn_wall.dm:91
    [SignalHandler]
    public virtual void on_update_overlays(turf source = null, DmList overlays = null)
    {
        mutable_appearance crack = mutable_appearance(Dm.resource("icons/turf/overlays.dmi"), "explodable", Dm.num(source.layer) + 0.1f) as mutable_appearance;
        if (current_stage == TORN_WALL_INITIAL)
            crack.alpha = Dm.num(crack.alpha) * 0.5f;
        Dm.append(ref overlays, crack);
    }

    /// If the wall becomes any other turf, delete us. Transforming into a different works fine as a fix.
    // tg: code/datums/components/torn_wall.dm:99
    [SignalHandler]
    public virtual void on_turf_changed()
    {
        qdel(this);
    }
}
