// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/air.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: holds the [DmPath] of /turf/open and /turf/open/space, which air.dm declares first in .dme order
// (/turf/open/proc/resolve_active_graph, :683; /turf/open/space/resolve_active_graph, :707; rules 2.3), so that the types
// of tg's world defaults (/world/turf is /turf/open/space/basic, code/world.dm) exist in wave 3 (W3.0 contracts), and
// (W4.0 contracts) SSair as an inert stub subsystem (rules 1.4; wave-3 form): tg's [DmPath], name and dependencies, never
// initialized or fired, with the members the turfs read (planetary, add_to_active). Owned by the lead: no wave-4 package
// edits it (report a needed member); air.dm is translated with the atmosphere (M4).
// STUB(M4): translated in M4 (atmospherics).
// M2 P2 (docs/ai/specs/m2-ownership.md: air.cs parse_gas_string, the stub keeps its flags): the vars atmos_gen and
// strings_to_mix and the procs generate_atmos, parse_gas_string and preprocess_gas_string translated (with the planetary
// /datum/atmosphere types, datums/atmosphere), so that a turf's
// initial_gas_mix gives real air (scope.md 2e "atmosphere").
// M3 I1b (D123: the pipes' pictures): SSair initializes in tg's place among the subsystems (its dependencies), and its
// Initialize runs setup_atmos_machinery (every atmos machine's atmos_init: the node search and the pictures);
// start_processing_machine keeps tg's machine list.
// M4b slice 1 (D134, the air that moves): tg's flags (SSair fires), fire() with every part, the active turfs, the excited
// groups, the space wind (high pressure deltas), the superconduction, the atom exposure, setup_allturfs and the adjacency
// rebuild. Deferred leaves: the gas reactions (fire and plasma, later in M4b), the excited-turf debug visuals and the admin
// panel (M10), the handbooks (tgui, M10).
// M4b slice 2 (the pipenets): setup_pipenets, setup_template_machinery, process_rebuilds and expand_pipeline.

// tg: code/controllers/subsystem/air.dm:1 SUBSYSTEM_DEF(air)
[DmPath("/datum/controller/subsystem/air")]
public partial class datum_controller_subsystem_air : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/air.dm:2
    public datum_controller_subsystem_air()
    {
        name = "Atmospherics";
        priority = FIRE_PRIORITY_AIR;
        wait = 0.5f * SECONDS;
        ss_flags = SS_BACKGROUND;
        runlevels = Dm.bitor(RUNLEVEL_GAME, RUNLEVEL_POSTGAME);
    }

    // tg: code/controllers/subsystem/air.dm:3, :41 (object-creating defaults, rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        dependencies = Dm.list(
            typeof(datum_controller_subsystem_mapping),
            typeof(datum_controller_subsystem_atoms));
        excited_groups = Dm.list(); // tg: code/controllers/subsystem/air.dm:25
        active_turfs = Dm.list(); // tg: code/controllers/subsystem/air.dm:26
        hotspots = Dm.list(); // tg: code/controllers/subsystem/air.dm:27
        networks = Dm.list(); // tg: code/controllers/subsystem/air.dm:28
        rebuild_queue = Dm.list(); // tg: code/controllers/subsystem/air.dm:29
        expansion_queue = Dm.list(); // tg: code/controllers/subsystem/air.dm:31
        adjacent_rebuild = Dm.list(); // tg: code/controllers/subsystem/air.dm:33
        atmos_machinery = Dm.list(); // tg: code/controllers/subsystem/air.dm:35
        pipe_init_dirs_cache = Dm.list(); // tg: code/controllers/subsystem/air.dm:37
        gas_reactions = Dm.list(); // tg: code/controllers/subsystem/air.dm:39
        planetary = Dm.list();
        strings_to_mix = Dm.list();
        active_super_conductivity = Dm.list(); // tg: code/controllers/subsystem/air.dm:47
        high_pressure_delta = Dm.list(); // tg: code/controllers/subsystem/air.dm:48
        atom_process = Dm.list(); // tg: code/controllers/subsystem/air.dm:49
        currentrun = Dm.list(); // tg: code/controllers/subsystem/air.dm:54
    }

    // The New() that SUBSYSTEM_DEF(air) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSair) (__DEFINES/MC.dm:29)
    // expanded inline (it reads src).
    // tg: code/controllers/subsystem/air.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSair != this)
        {
            if (SSair is datum_controller_subsystem_air)
            {
                Recover();
                qdel(SSair);
            }
            SSair = this;
        }
        PreInit();
    }

    //atmos singletons
    // tg: code/controllers/subsystem/air.dm:40
    public DmList atmos_gen;
    // tg: code/controllers/subsystem/air.dm:41
    public DmList planetary; //Lets cache static planetary mixes
    /// List of gas string -> canonical gas mixture
    // tg: code/controllers/subsystem/air.dm:65
    public DmList strings_to_mix;

    // tg: code/controllers/subsystem/air.dm:65
    public override object stat_entry(object msg = null)
    {
        Dm.NotTranslated("code/controllers/subsystem/air.dm:65", "stat_entry: stat panels are not translated (translation-rules.md 13)");
        return null;
    }

    // tg: code/controllers/subsystem/air.dm:94
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        map_loading = FALSE;
        gas_reactions = init_gas_reactions() as DmList;
        hotspot_reactions = init_hotspot_reactions() as DmList;

        await setup_allturfs();
        setup_atmos_machinery();
        setup_pipenets();
        setup_turf_visuals();
        process_adjacent_rebuild();
        atmos_handbooks_init();
        return SS_INIT_SUCCESS;
    }

    // tg: code/controllers/subsystem/air.dm:108
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        resumed ??= FALSE;
        float timer = TICK_USAGE_REAL();

        //Rebuilds can happen at any time, so this needs to be done outside of the normal system
        cost_rebuilds = 0;
        cost_adjacent = 0;

        // We need to have a solid setup for turfs before fire, otherwise we'll get massive runtimes and strange behavior
        if (Dm.truthy(length(adjacent_rebuild)))
        {
            timer = TICK_USAGE_REAL();
            process_adjacent_rebuild();
            //This does mean that the apperent rebuild costs fluctuate very quickly, this is just the cost of having them always process, no matter what
            cost_adjacent = TICK_USAGE_REAL() - timer;
            if (!Dm.eq(state, SS_RUNNING))
                return;
        }

        // Every time we fire, we want to make sure pipenets are rebuilt. The game state could have changed between each fire() proc call
        // and anything missing a pipenet can lead to unintended behaviour at worse and various runtimes at best.
        if (Dm.truthy(length(rebuild_queue)) || Dm.truthy(length(expansion_queue)))
        {
            timer = TICK_USAGE_REAL();
            process_rebuilds();
            //This does mean that the apperent rebuild costs fluctuate very quickly, this is just the cost of having them always process, no matter what
            cost_rebuilds = TICK_USAGE_REAL() - timer;
            if (!Dm.eq(state, SS_RUNNING))
                return;
        }

        if (currentpart == SSAIR_PIPENETS || !Dm.truthy(resumed))
        {
            timer = TICK_USAGE_REAL();
            if (!Dm.truthy(resumed))
                cached_cost = 0;
            process_pipenets(resumed);
            cached_cost += TICK_USAGE_REAL() - timer;
            if (!Dm.eq(state, SS_RUNNING))
                return;
            cost_pipenets = MC_AVERAGE(cost_pipenets, TICK_DELTA_TO_MS(cached_cost));
            resumed = FALSE;
            currentpart = SSAIR_ATMOSMACHINERY;
        }

        if (currentpart == SSAIR_ATMOSMACHINERY)
        {
            timer = TICK_USAGE_REAL();
            if (!Dm.truthy(resumed))
                cached_cost = 0;
            process_atmos_machinery(resumed);
            cached_cost += TICK_USAGE_REAL() - timer;
            if (!Dm.eq(state, SS_RUNNING))
                return;
            cost_atmos_machinery = MC_AVERAGE(cost_atmos_machinery, TICK_DELTA_TO_MS(cached_cost));
            resumed = FALSE;
            currentpart = SSAIR_ACTIVETURFS;
        }

        if (currentpart == SSAIR_ACTIVETURFS)
        {
            timer = TICK_USAGE_REAL();
            if (!Dm.truthy(resumed))
                cached_cost = 0;
            await process_active_turfs(resumed);
            cached_cost += TICK_USAGE_REAL() - timer;
            if (!Dm.eq(state, SS_RUNNING))
                return;
            cost_turfs = MC_AVERAGE(cost_turfs, TICK_DELTA_TO_MS(cached_cost));
            resumed = FALSE;
            currentpart = SSAIR_HOTSPOTS;
        }

        if (currentpart == SSAIR_HOTSPOTS) //We do this before excited groups to allow breakdowns to be independent of adding turfs while still *mostly preventing mass fires
        {
            timer = TICK_USAGE_REAL();
            if (!Dm.truthy(resumed))
                cached_cost = 0;
            process_hotspots(resumed);
            cached_cost += TICK_USAGE_REAL() - timer;
            if (!Dm.eq(state, SS_RUNNING))
                return;
            cost_hotspots = MC_AVERAGE(cost_hotspots, TICK_DELTA_TO_MS(cached_cost));
            resumed = FALSE;
            currentpart = SSAIR_EXCITEDGROUPS;
        }

        if (currentpart == SSAIR_EXCITEDGROUPS)
        {
            timer = TICK_USAGE_REAL();
            if (!Dm.truthy(resumed))
                cached_cost = 0;
            process_excited_groups(resumed);
            cached_cost += TICK_USAGE_REAL() - timer;
            if (!Dm.eq(state, SS_RUNNING))
                return;
            cost_groups = MC_AVERAGE(cost_groups, TICK_DELTA_TO_MS(cached_cost));
            resumed = FALSE;
            currentpart = SSAIR_HIGHPRESSURE;
        }

        if (currentpart == SSAIR_HIGHPRESSURE)
        {
            timer = TICK_USAGE_REAL();
            if (!Dm.truthy(resumed))
                cached_cost = 0;
            process_high_pressure_delta(resumed);
            cached_cost += TICK_USAGE_REAL() - timer;
            if (!Dm.eq(state, SS_RUNNING))
                return;
            cost_highpressure = MC_AVERAGE(cost_highpressure, TICK_DELTA_TO_MS(cached_cost));
            resumed = FALSE;
            currentpart = SSAIR_SUPERCONDUCTIVITY;
        }

        if (currentpart == SSAIR_SUPERCONDUCTIVITY)
        {
            timer = TICK_USAGE_REAL();
            if (!Dm.truthy(resumed))
                cached_cost = 0;
            await process_super_conductivity(resumed);
            cached_cost += TICK_USAGE_REAL() - timer;
            if (!Dm.eq(state, SS_RUNNING))
                return;
            cost_superconductivity = MC_AVERAGE(cost_superconductivity, TICK_DELTA_TO_MS(cached_cost));
            resumed = FALSE;
            currentpart = SSAIR_PROCESS_ATOMS;
        }

        if (currentpart == SSAIR_PROCESS_ATOMS)
        {
            timer = TICK_USAGE_REAL();
            if (!Dm.truthy(resumed))
                cached_cost = 0;
            await process_atoms(resumed);
            cached_cost += TICK_USAGE_REAL() - timer;
            if (!Dm.eq(state, SS_RUNNING))
                return;
            cost_atoms = MC_AVERAGE(cost_atoms, TICK_DELTA_TO_MS(cached_cost));
            resumed = FALSE;
        }

        currentpart = SSAIR_PIPENETS;
        // SStgui.update_uis(SSair): the admin atmos control panel (tgui, M10)
        Dm.Deferred("code/controllers/subsystem/air.dm:217", "fire: SStgui.update_uis(SSair): the admin atmos control panel (tgui, M10+)");
    }

    // tg: code/controllers/subsystem/air.dm:233
    public override void Recover()
    {
        excited_groups = SSair.excited_groups;
        active_turfs = SSair.active_turfs;
        hotspots = SSair.hotspots;
        networks = SSair.networks;
        rebuild_queue = SSair.rebuild_queue;
        expansion_queue = SSair.expansion_queue;
        adjacent_rebuild = SSair.adjacent_rebuild;
        atmos_machinery = SSair.atmos_machinery;
        pipe_init_dirs_cache = SSair.pipe_init_dirs_cache;
        gas_reactions = SSair.gas_reactions;
        atmos_gen = SSair.atmos_gen;
        planetary = SSair.planetary;
        active_super_conductivity = SSair.active_super_conductivity;
        high_pressure_delta = SSair.high_pressure_delta;
        atom_process = SSair.atom_process;
        currentrun = SSair.currentrun;
        queued_for_activation = SSair.queued_for_activation;
    }

    // tg: code/controllers/subsystem/air.dm:252
    public virtual void process_adjacent_rebuild(float? init = null)
    {
        init ??= FALSE;
        DmList queue = adjacent_rebuild;

        while (Dm.truthy(length(queue)))
        {
            turf currT = queue[1] as turf;
            object goal = queue[currT];
            queue.Cut(1, 2);

            currT.immediate_calculate_adjacent_turfs();
            if (Dm.eq(goal, MAKE_ACTIVE))
                add_to_active(currT);
            else if (Dm.eq(goal, KILL_EXCITED))
                add_to_active(currT, TRUE);

            if (Dm.truthy(init))
                CHECK_TICK();
            else
            {
                ChargeTick(AtmosTickCost.RebuildStep); // DEVIATION(DV33)
                if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                    break;
            }
        }
    }

    // tg: code/controllers/subsystem/air.dm:272
    public virtual void process_pipenets(float? resumed = null)
    {
        resumed ??= FALSE;
        if (!Dm.truthy(resumed))
            this.currentrun = networks.Copy();
        //cache for sanic speed (lists are references anyways)
        DmList currentrun = this.currentrun;
        while (Dm.truthy(currentrun.len))
        {
            datum thing = currentrun[currentrun.len] as datum;
            currentrun.len--;
            if (thing != null)
                Waiting.NoSleep(Dm.Call(thing, "process"));
            else
                networks.Remove(thing);
            ChargeTick(AtmosTickCost.Pipenet); // DEVIATION(DV33)
            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                return;
        }
    }

    // tg: code/controllers/subsystem/air.dm:287
    public virtual void add_to_rebuild_queue(obj_machinery_atmospherics atmos_machine = null)
    {
        if (atmos_machine is obj_machinery_atmospherics && !Dm.truthy(atmos_machine.rebuilding))
        {
            Dm.append(ref rebuild_queue, atmos_machine);
            atmos_machine.rebuilding = TRUE;
        }
    }

    // tg: code/controllers/subsystem/air.dm:292
    public virtual void add_to_expansion(datum_pipeline line = null, object starting_point = null)
    {
        DmList new_packet = Dm.list_n(SSAIR_REBUILD_QUEUE);
        new_packet[SSAIR_REBUILD_PIPELINE] = line;
        new_packet[SSAIR_REBUILD_QUEUE] = Dm.list(starting_point);
        Dm.append(ref expansion_queue, Dm.list(new_packet));
    }

    // tg: code/controllers/subsystem/air.dm:298
    public virtual void remove_from_expansion(datum_pipeline line = null)
    {
        foreach (DmList packet in Dm.each<DmList>(expansion_queue))
        {
            if (packet[SSAIR_REBUILD_PIPELINE] == line)
            {
                Dm.remove(ref expansion_queue, packet);
                return;
            }
        }
    }

    // tg: code/controllers/subsystem/air.dm:304
    public virtual async DmTask process_atoms(float? resumed = null)
    {
        resumed ??= FALSE;
        if (!Dm.truthy(resumed))
            this.currentrun = atom_process.Copy();
        //cache for sanic speed (lists are references anyways)
        DmList currentrun = this.currentrun;
        while (Dm.truthy(currentrun.len))
        {
            atom talk_to = currentrun[currentrun.len] as atom;
            currentrun.len--;
            if (talk_to == null)
                return;
            await talk_to.process_exposure();
            ChargeTick(AtmosTickCost.Atom); // DEVIATION(DV33)
            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                return;
        }
    }

    // tg: code/controllers/subsystem/air.dm:318
    public virtual void process_atmos_machinery(float? resumed = null)
    {
        resumed ??= FALSE;
        if (!Dm.truthy(resumed))
            this.currentrun = atmos_machinery.Copy();
        //cache for sanic speed (lists are references anyways)
        DmList currentrun = this.currentrun;
        while (Dm.truthy(currentrun.len))
        {
            datum M = currentrun[currentrun.len] as datum;
            currentrun.len--;
            if (M == null)
                Dm.remove(ref atmos_machinery, M);
            // a machine, or a gas leaker component (start_processing_machine): its process_atmos by name
            object processed = M is obj_machinery machine ? machine.process_atmos(wait * 0.1f) : Waiting.NoSleep(Dm.Call(M, "process_atmos", wait * 0.1f));
            if (Dm.eq(processed, PROCESS_KILL))
                stop_processing_machine(M);
            ChargeTick(AtmosTickCost.Machine); // DEVIATION(DV33)
            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                return;
        }
    }

    // tg: code/controllers/subsystem/air.dm:334
    public virtual async DmTask process_super_conductivity(float? resumed = null)
    {
        resumed ??= FALSE;
        if (!Dm.truthy(resumed))
            this.currentrun = active_super_conductivity.Copy();
        //cache for sanic speed (lists are references anyways)
        DmList currentrun = this.currentrun;
        while (Dm.truthy(currentrun.len))
        {
            turf T = currentrun[currentrun.len] as turf;
            currentrun.len--;
            await T.super_conduct();
            ChargeTick(AtmosTickCost.SuperConductingTurf); // DEVIATION(DV33)
            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                return;
        }
    }

    // tg: code/controllers/subsystem/air.dm:346
    public virtual void process_hotspots(float? resumed = null)
    {
        resumed ??= FALSE;
        if (!Dm.truthy(resumed))
            this.currentrun = hotspots.Copy();
        //cache for sanic speed (lists are references anyways)
        DmList currentrun = this.currentrun;
        while (Dm.truthy(currentrun.len))
        {
            obj_effect_hotspot H = currentrun[currentrun.len] as obj_effect_hotspot;
            currentrun.len--;
            if (H != null)
                Waiting.NoSleep(Dm.Call(H, "process"));
            else
                Dm.remove(ref hotspots, H);
            ChargeTick(AtmosTickCost.Hotspot); // DEVIATION(DV33)
            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                return;
        }
    }

    // tg: code/controllers/subsystem/air.dm:361
    public virtual void process_high_pressure_delta(float? resumed = null)
    {
        resumed ??= FALSE;
        while (Dm.truthy(high_pressure_delta.len))
        {
            turf_open T = high_pressure_delta[high_pressure_delta.len] as turf_open;
            high_pressure_delta.len--;
            AirflowSeam.Note(T); // DEVIATION(DV34): the flow the clients draw as streams, read before it is zeroed
            if (!CeilingSystem.Of(World.Current).IsExteriorOutlet(T))
            {
                LocomotionSeam.NoteWind(T); // DEVIATION(DV357): the wind that carries a walking capsule
                T.high_pressure_movements();
            }
            T.pressure_difference = 0;
            ChargeTick(AtmosTickCost.HighPressureTurf); // DEVIATION(DV33)
            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                return;
        }
        AirflowSeam.CycleEnd(); // DEVIATION(DV34)
        LocomotionSeam.WindCycleEnd(); // DEVIATION(DV357)
    }

    // tg: code/controllers/subsystem/air.dm:370
    public virtual async DmTask process_active_turfs(float? resumed = null)
    {
        resumed ??= FALSE;
        //cache for sanic speed
        float fire_count = times_fired;
        if (!Dm.truthy(resumed))
        {
            CeilingSystem.Of(World.Current).ExchangeExterior();
            this.currentrun = active_turfs.Copy();
        }
        //cache for sanic speed (lists are references anyways)
        DmList currentrun = this.currentrun;
        while (Dm.truthy(currentrun.len))
        {
            turf_open T = currentrun[currentrun.len] as turf_open;
            currentrun.len--;
            if (T != null)
                await T.process_cell(fire_count);
            ChargeTick(AtmosTickCost.ActiveTurf); // DEVIATION(DV33)
            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                return;
        }
    }

    // tg: code/controllers/subsystem/air.dm:385
    public virtual void process_excited_groups(float? resumed = null)
    {
        resumed ??= FALSE;
        if (!Dm.truthy(resumed))
            this.currentrun = excited_groups.Copy();
        //cache for sanic speed (lists are references anyways)
        DmList currentrun = this.currentrun;
        while (Dm.truthy(currentrun.len))
        {
            datum_excited_group EG = currentrun[currentrun.len] as datum_excited_group;
            currentrun.len--;
            float group_turfs = EG.turf_list.len; // DV33: a breakdown or a dismantle walks the group's turfs
            float volatile_reaction = Dm.bitand(EG.turf_reactions, VOLATILE_REACTION);
            EG.breakdown_cooldown++;
            if (!Dm.truthy(volatile_reaction))
                EG.dismantle_cooldown++;
            float group_cost = AtmosTickCost.ExcitedGroup; // DEVIATION(DV33)
            if (EG.breakdown_cooldown >= EXCITED_GROUP_BREAKDOWN_CYCLES && !Dm.truthy(volatile_reaction))
            {
                EG.self_breakdown(poke_turfs: TRUE);
                group_cost += group_turfs * AtmosTickCost.ExcitedGroupTurf;
            }
            else if (EG.dismantle_cooldown >= EXCITED_GROUP_DISMANTLE_CYCLES && !Dm.truthy(Dm.bitand(EG.turf_reactions, Dm.bitor(REACTING, STOP_REACTIONS))))
            {
                EG.dismantle();
                group_cost += group_turfs * AtmosTickCost.ExcitedGroupTurf;
            }
            EG.turf_reactions = NONE;
            ChargeTick(group_cost); // DEVIATION(DV33)
            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                return;
        }
    }

    // tg: code/controllers/subsystem/air.dm:405
    public virtual void process_rebuilds()
    {
        //Yes this does mean rebuilding pipenets can freeze up the subsystem forever, but if we're in that situation something else is very wrong
        DmList currentrun = rebuild_queue;
        while (Dm.truthy(currentrun.len) || Dm.truthy(Dm.length(expansion_queue)))
        {
            while (Dm.truthy(currentrun.len) && !Dm.truthy(Dm.length(expansion_queue))) //If we found anything, process that first
            {
                obj_machinery_atmospherics remake = currentrun[currentrun.len] as obj_machinery_atmospherics;
                currentrun.len--;
                if (remake == null)
                    continue;
                remake.rebuild_pipes();
                ChargeTick(AtmosTickCost.RebuildStep); // DEVIATION(DV33)
                if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                    return;
            }

            DmList queue = expansion_queue;
            while (Dm.truthy(queue.len))
            {
                DmList pack = queue[queue.len] as DmList;
                //We operate directly with the pipeline like this because we can trust any rebuilds to remake it properly
                datum_pipeline linepipe = pack[SSAIR_REBUILD_PIPELINE] as datum_pipeline;
                DmList border = pack[SSAIR_REBUILD_QUEUE] as DmList;
                expand_pipeline(linepipe, border);
                if (Dm.truthy(Dm.length(border))) //expand_pipeline failed a tick check before finishing, resume it next fire
                    return;

                linepipe.building = FALSE;
                queue.len--;
                // machinery was claimed by another pipeline before we could expand somehow, check if it has no pipes or machines left we can just delete it
                if (!Dm.truthy(Dm.length(linepipe.members)) && !Dm.truthy(Dm.length(linepipe.other_atmos_machines)))
                    qdel(linepipe);
                ChargeTick(AtmosTickCost.RebuildStep); // DEVIATION(DV33)
                if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                    return;
            }
        }
    }

    ///Rebuilds a pipeline by expanding outwards, while yielding when sane
    // tg: code/controllers/subsystem/air.dm:437
    public virtual void expand_pipeline(datum_pipeline net = null, DmList border = null)
    {
        while (Dm.truthy(border.len))
        {
            obj_machinery_atmospherics borderline = border[border.len] as obj_machinery_atmospherics;
            border.len--;

            DmList result = borderline.pipeline_expansion(net) as DmList;
            if (!Dm.truthy(Dm.length(result)))
                continue;
            foreach (obj_machinery_atmospherics considered_device in Dm.each<obj_machinery_atmospherics>(result))
            {
                if (!istype(considered_device, typeof(obj_machinery_atmospherics_pipe)))
                {
                    considered_device.set_pipenet(net, borderline);
                    net.add_machinery_member(considered_device as obj_machinery_atmospherics_components);
                    continue;
                }
                obj_machinery_atmospherics_pipe item = considered_device as obj_machinery_atmospherics_pipe;
                if (Dm.truthy(net.members.Find(item)))
                    continue;
                if (item.parent != null)
                {
                    // var/static/pipenetwarnings = 10 and log_mapping("build_pipeline(): [item.type] added to a pipenet while
                    // still having one. ... around [AREACOORD(item)].") (:456-462): the mapping log of the pipes stacked in
                    // one turf, which reads nothing else (AREACOORD, the admin coordinates, M10)
                    Dm.Deferred("code/controllers/subsystem/air.dm:456", "expand_pipeline: the mapping log of pipes stacked into one pipenet (AREACOORD, M10+)");
                }

                Dm.append(ref net.members, item);
                Dm.append(ref border, item);

                net.air.volume += Dm.num(item.volume);
                item.replace_pipenet(item.parent, net);

                if (item.air_temporary != null)
                {
                    net.air.merge(item.air_temporary);
                    item.air_temporary = null;
                }
            }
            ChargeTick(AtmosTickCost.RebuildStep); // DEVIATION(DV33)
            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                return;
        }
    }

    ///Removes a turf from processing, and causes its excited group to clean up so things properly adapt to the change
    // tg: code/controllers/subsystem/air.dm:475
    public virtual void remove_from_active(turf T = null)
    {
        Dm.remove(ref active_turfs, T);
        if (currentpart == SSAIR_ACTIVETURFS)
            Dm.remove(ref currentrun, T);
        // :453-455  #ifdef VISUALIZE_ACTIVE_TURFS #endif: inactive in production (rules 1.7)
        if (T is turf_open open)
        {
            open.excited = FALSE;
            if (open.excited_group != null)
            {
                //If this fires during active turfs it'll cause a slight removal of active turfs, as they breakdown if they have no excited group
                //The group also expands by a tile per rebuild on each edge, suffering
                open.excited_group.garbage_collect(); //Kill the excited group, it'll reform on its own later
            }
        }
    }

    ///Puts an active turf to sleep so it doesn't process. Do this without cleaning up its excited group.
    // tg: code/controllers/subsystem/air.dm:490
    public virtual void sleep_active_turf(turf T = null)
    {
        Dm.remove(ref active_turfs, T);
        if (currentpart == SSAIR_ACTIVETURFS)
            Dm.remove(ref currentrun, T);
        // :468-470  #ifdef VISUALIZE_ACTIVE_TURFS #endif: inactive in production (rules 1.7)
        if (T is turf_open open)
            open.excited = FALSE;
    }

    ///Adds a turf to active processing, handles duplicates. Call this with blockchanges == TRUE if you want to nuke the assoc excited group
    // tg: code/controllers/subsystem/air.dm:501
    public virtual void add_to_active(turf activate = null, float? blockchanges = null)
    {
        blockchanges ??= FALSE;
        if (activate is turf_open open && Dm.truthy(open.air))
        {
            open.significant_share_ticker = 0;
            if (Dm.truthy(blockchanges) && open.excited_group != null) //This is used almost exclusivly for shuttles, so the excited group doesn't stay behind
                open.excited_group.garbage_collect(); //Nuke it
            if (Dm.truthy(open.excited)) //Don't keep doing it if there's no point
                return;
            // :483-485  #ifdef VISUALIZE_ACTIVE_TURFS #endif: inactive in production (rules 1.7)
            open.excited = TRUE;
            Dm.append(ref active_turfs, open);
        }
        else if (Dm.truthy(Dm.bitand(activate.flags_1, INITIALIZED_1)))
        {
            foreach (turf neighbor in Dm.each<turf>(activate.atmos_adjacent_turfs))
                add_to_active(neighbor, TRUE);
        }
        else if (Dm.truthy(map_loading))
        {
            if (Dm.truthy(queued_for_activation))
                queued_for_activation[activate] = activate;
        }
        else
            activate.requires_activation = TRUE;
    }

    // tg: code/controllers/subsystem/air.dm:522
    public override async DmTask StartLoadingMap()
    {
        LAZYINITLIST(ref queued_for_activation);
        map_loading = TRUE;
    }

    // tg: code/controllers/subsystem/air.dm:526
    public override void StopLoadingMap(params object[] __extra)
    {
        map_loading = FALSE;
        foreach (object T in Dm.each(queued_for_activation))
            add_to_active(T as turf, TRUE);
        queued_for_activation.Cut();
    }

    // tg: code/controllers/subsystem/air.dm:532
    public virtual async DmTask setup_allturfs()
    {
        DmList active_turfs = this.active_turfs;
        times_fired++;

        // Clear active turfs - faster than removing every single turf in the world
        // one-by-one, and Initalize_Atmos only ever adds `src` back in.
        // :515-519  #ifdef VISUALIZE_ACTIVE_TURFS #endif: inactive in production (rules 1.7)
        active_turfs.Cut();
        // We compare this against turf.current cycle using <= to ensure O(n)
        // It defaults to 0, so we start at -1
        float time = -1;

        DmList difference_check = Dm.list();
        foreach (turf setup in Dm.each<turf>(ALL_TURFS()))
        {
            if (!Dm.truthy(setup.init_air))
                continue;
            // We pass the tick as the current step so if we sleep the step changes
            // This way we can make setting up adjacent turfs O(n) rather then O(n^2)
            setup.Initalize_Atmos(time);
            // We assert that we'll only get open turfs here
            Dm.append(ref difference_check, setup);
            // if(CHECK_TICK) time--: the value of CHECK_TICK is the stoplag it ran (__DEFINES/_tick.dm:21); the port's CHECK_TICK
            // never reaches stoplag (the virtual tick usage is 0, DV3), so its value is always 0 and time stays
            CHECK_TICK();
        }

        // Now we're gonna compare for differences
        // Taking advantage of current cycle being set to negative before this run to do A->B B->A prevention
        foreach (turf_open potential_diff in Dm.each<turf_open>(difference_check))
        {
            // I can't use 0 here, so we're gonna do this instead. If it ever breaks I'll eat my shoe
            potential_diff.current_cycle = -INFINITY;
            foreach (turf_open enemy_tile in Dm.each<turf_open>(potential_diff.atmos_adjacent_turfs))
            {
                // If it's already been processed, then it's already talked to us
                if (enemy_tile.current_cycle == -INFINITY)
                    continue;
                // .air instead of .return_air() because we can guarantee that the proc won't do anything
                if (Dm.truthy(potential_diff.air.compare(enemy_tile.air, FALSE)))
                {
                    if (!Dm.truthy(potential_diff.excited))
                    {
                        potential_diff.excited = TRUE;
                        Dm.append(ref SSair.active_turfs, potential_diff);
                    }
                    if (!Dm.truthy(enemy_tile.excited))
                    {
                        enemy_tile.excited = TRUE;
                        Dm.append(ref SSair.active_turfs, enemy_tile);
                    }
                    // No sense continuing to iterate
                    break;
                }
            }
            CHECK_TICK();
        }

        if (Dm.truthy(active_turfs.len))
        {
            float starting_ats = active_turfs.len;
            await Dm.sleep(world.tick_lag);
            float timer = world.timeofday;

            log_mapping(Dm.text("There are [] active turfs at roundstart caused by a difference of the air between the adjacent turfs. To locate these active turfs, go into the \"Debug\" tab of your stat-panel. Then hit the verb that says \"Mapping Verbs - Enable\". Now, you can see all of the associated coordinates using \"Mapping -> Show roundstart AT list\" verb.", starting_ats));

            foreach (turf T in Dm.each<turf>(active_turfs))
                Dm.append(ref GLOB.active_turfs_startlist, T);

            //now lets clear out these active turfs
            DmList turfs_to_check = active_turfs.Copy();
            do
            {
                DmList new_turfs_to_check = Dm.list();
                foreach (turf_open T in Dm.each<turf_open>(turfs_to_check))
                    Dm.append(ref new_turfs_to_check, T.resolve_active_graph());
                CHECK_TICK();

                Dm.append(ref active_turfs, new_turfs_to_check);
                turfs_to_check = new_turfs_to_check;
            }
            while (Dm.truthy(turfs_to_check.len));

            float ending_ats = active_turfs.len;
            foreach (object thing in Dm.each(excited_groups))
            {
                datum_excited_group EG = thing as datum_excited_group;
                EG.self_breakdown(roundstart: TRUE);
                EG.dismantle();
                CHECK_TICK();
            }

            log_active_turfs(); // invoke this here so we can count the time it takes to run this proc as "wasted time", quite simple honestly.

            string msg = Dm.text("HEY! LISTEN! [] were wasted processing [] turf(s) (connected to [] other turfs) with atmos differences at round start.", DisplayTimeText(world.timeofday - timer, 0.00001f), starting_ats, ending_ats - starting_ats);
            to_chat(world, span_boldannounce(msg));
            warning(msg);
        }
    }

    /// Logs all active turfs at roundstart to the mapping log so it can be readily accessed.
    // tg: code/controllers/subsystem/air.dm:619
    public virtual void log_active_turfs()
    {
        // The mapping log of the roundstart active turfs per z-level and z-trait, and their SSblackbox feedback, are mapping
        // diagnostics (logging and SSblackbox, M10); the count already went to log_mapping above.
        Dm.Deferred("code/controllers/subsystem/air.dm:609", "log_active_turfs: the per-level mapping log and SSblackbox feedback of roundstart active turfs (M10+)");
    }

    // tg: code/controllers/subsystem/air.dm:710
    public virtual void setup_atmos_machinery()
    {
        foreach (obj_machinery_atmospherics AM in Dm.each<obj_machinery_atmospherics>(atmos_machinery))
        {
            AM.atmos_init();
            CHECK_TICK();
        }
    }

    //this can't be done with setup_atmos_machinery() because
    // all atmos machinery has to initialize before the first
    // pipenet can be built.
    // tg: code/controllers/subsystem/air.dm:718
    public virtual void setup_pipenets()
    {
        foreach (obj_machinery_atmospherics AM in Dm.each<obj_machinery_atmospherics>(atmos_machinery))
        {
            datum_pipeline build_target = AM.get_rebuild_target() as datum_pipeline;
            while (build_target != null)
            {
                build_target.build_pipeline_blocking(AM);
                build_target = AM.get_rebuild_target() as datum_pipeline;
            }
            CHECK_TICK();
        }
    }

    // tg: code/controllers/subsystem/air.dm:741
    public virtual void setup_template_machinery(DmList atmos_machines = null)
    {
        obj_machinery_atmospherics AM;
        for (float A = 1, A__end = atmos_machines.len; A <= A__end; A++)
        {
            AM = atmos_machines[A] as obj_machinery_atmospherics;
            AM.atmos_init();
            CHECK_TICK();
        }

        for (float A = 1, A__end = atmos_machines.len; A <= A__end; A++)
        {
            AM = atmos_machines[A] as obj_machinery_atmospherics;
            datum_pipeline build_target = AM.get_rebuild_target() as datum_pipeline;
            while (build_target != null)
            {
                build_target.build_pipeline_blocking(AM);
                build_target = AM.get_rebuild_target() as datum_pipeline;
            }
            CHECK_TICK();
        }
    }

    // tg: code/controllers/subsystem/air.dm:728
    public virtual void setup_turf_visuals()
    {
        // GLOB.colored_turfs and GLOB.colored_images: the colored overlays of the excited groups' debug display (the admin
        // atmos control panel, M10); nothing shows them (display_turfs is a deferred leaf).
        Dm.Deferred("code/controllers/subsystem/air.dm:728", "setup_turf_visuals: the excited groups' debug overlays (the admin atmos control panel, M10+)");
    }

    /**
     * Adds a given machine to the processing system for SSAIR_ATMOSMACHINERY processing.
     *
     * Arguments:
     * * machine - The machine to start processing. Can be any /obj/machinery.
     */
    // tg: code/controllers/subsystem/air.dm:824
    // (tg declares an /obj/machinery but also passes /datum/component/gas_leaker, which has the same atmos_processing var and process_atmos proc:
    // the var is reached by name, as DM does)
    public virtual void start_processing_machine(datum machine = null)
    {
        if (Dm.truthy(Dm.get_var(machine, "atmos_processing")))
            return;
        if (QDELETED(machine))
        {
            stack_trace("We tried to add a garbage collecting machine to SSair. Don't", "code/controllers/subsystem/air.dm", 828);
            return;
        }
        Dm.set_var(machine, "atmos_processing", TRUE);
        Dm.append(ref atmos_machinery, machine);
    }

    /**
     * Removes a given machine to the processing system for SSAIR_ATMOSMACHINERY processing.
     *
     * Arguments:
     * * machine - The machine to stop processing.
     */
    // tg: code/controllers/subsystem/air.dm:839
    public virtual void stop_processing_machine(datum machine = null)
    {
        if (!Dm.truthy(Dm.get_var(machine, "atmos_processing")))
            return;
        Dm.set_var(machine, "atmos_processing", FALSE);
        Dm.remove(ref atmos_machinery, machine);

        // If we're currently processing atmos machines, there's a chance this machine is in
        // the currentrun list, which is a cache of atmos_machinery. Remove it from that list
        // as well to prevent processing qdeleted objects in the cache.
        if (currentpart == SSAIR_ATMOSMACHINERY)
            Dm.remove(ref currentrun, machine);
    }

    // tg: code/controllers/subsystem/air.dm:772
    public virtual void generate_atmos()
    {
        atmos_gen = Dm.list();
        foreach (object T in Dm.each(subtypesof(typeof(datum_atmosphere))))
        {
            Type atmostype = T as Type;
            atmos_gen[Dm.initial(atmostype, (datum_atmosphere p) => p.id)] = Dm.@new(atmostype);
        }
    }

    /// Takes a gas string, returns the matching mutable gas_mixture
    // tg: code/controllers/subsystem/air.dm:779
    public virtual object parse_gas_string(object gas_string = null, Type gastype = null)
    {
        gastype ??= typeof(datum_gas_mixture);
        datum_gas_mixture cached = strings_to_mix[Dm.text("[]-[]", gas_string, gastype)] as datum_gas_mixture;

        if (cached != null)
        {
            if (istype(cached, typeof(datum_gas_mixture_immutable)))
                return cached;
            return cached.copy();
        }

        datum_gas_mixture canonical_mix = (datum_gas_mixture)Dm.@new(gastype);
        // We set here so any future key changes don't fuck us
        strings_to_mix[Dm.text("[]-[]", gas_string, gastype)] = canonical_mix;
        gas_string = preprocess_gas_string(gas_string);

        DmList gas = params2list(gas_string);
        if (Dm.truthy(gas["TEMP"]))
        {
            canonical_mix.temperature = Dm.num(text2num(gas["TEMP"]));
            canonical_mix.temperature_archived = canonical_mix.temperature;
            Dm.remove(ref gas, "TEMP");
        }
        else // if we do not have a temp in the new gas mix lets assume room temp.
            canonical_mix.temperature = T20C;
        DmList cached_moles = canonical_mix.moles;
        foreach (object id in Dm.each(gas))
        {
            object path = id;
            if (!ispath(path))
                path = gas_id2path(path); //a lot of these strings can't have embedded expressions (especially for mappers), so support for IDs needs to stick around
            cached_moles[path] = text2num(gas[id]);
        }

        if (istype(canonical_mix, typeof(datum_gas_mixture_immutable)))
            return canonical_mix;
        return canonical_mix.copy();
    }

    // tg: code/controllers/subsystem/air.dm:810
    public virtual object preprocess_gas_string(object gas_string = null)
    {
        if (!Dm.truthy(atmos_gen))
            generate_atmos();
        if (!Dm.truthy(atmos_gen[gas_string]))
            return gas_string;
        datum_atmosphere mix = atmos_gen[gas_string] as datum_atmosphere;
        return mix.gas_string;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/air.dm:1  SUBSYSTEM_DEF(air): GLOBAL_REAL(SSair, /datum/controller/subsystem/air)
    internal datum_controller_subsystem_air SSair;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_air SSair => ref TgState.Current.SSair;
}

// tg: code/controllers/subsystem/air.dm:683
[DmPath("/turf/open")]
public partial class turf_open : turf
{
    // tg: code/controllers/subsystem/air.dm:683
    public virtual DmList resolve_active_graph()
    {
        DmList dot = Dm.list();
        datum_excited_group EG = excited_group;
        if (Dm.truthy(blocks_air) || !Dm.truthy(air))
            return dot;
        if (EG == null)
        {
            EG = Dm.@new<datum_excited_group>();
            EG.add_turf(this);
        }

        foreach (turf_open ET in Dm.each<turf_open>(atmos_adjacent_turfs))
        {
            if (Dm.truthy(ET.blocks_air) || !Dm.truthy(ET.air))
                continue;

            datum_excited_group ET_EG = ET.excited_group;
            if (ET_EG != null)
            {
                if (ET_EG != EG)
                {
                    EG.merge_groups(ET_EG);
                    EG = excited_group; //merge_groups() may decide to replace our current EG
                }
            }
            else
                EG.add_turf(ET);
            if (!Dm.truthy(ET.excited))
            {
                ET.excited = TRUE;
                Dm.append(ref dot, ET);
            }
        }
        return dot;
    }
}

// tg: code/controllers/subsystem/air.dm:707
[DmPath("/turf/open/space")]
public partial class turf_open_space : turf_open
{
    // tg: code/controllers/subsystem/air.dm:707
    public override DmList resolve_active_graph()
    {
        return Dm.list();
    }
}
