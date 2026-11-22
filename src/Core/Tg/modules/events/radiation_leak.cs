// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/radiation_leak.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2c (D327): the whole file: a powered machine near an event spawn starts to leak radiation and a green gas of polonium and
// mutagen; one to three tools named in its examine stop the leak after a long repair. The var blocks are generated (D111).

// tg: code/modules/events/radiation_leak.dm:13
public partial class datum_round_event_radiation_leak : datum_round_event
{
    // tg: code/modules/events/radiation_leak.dm:22
    public override async DmTask<object> setup()
    {
        // Pick a generic event spawn somewhere in the world.
        // We will try to find a machine within a few turfs of it to start spewing rads from.
        DmList possible_locs = GLOB.generic_event_spawns.Copy();
        while (Dm.length(possible_locs) > 0)
        {
            turf chosen_loc = get_turf(pick_n_take(possible_locs) as atom);
            foreach (obj_machinery sick_device in Dm.each<obj_machinery>(range(3, chosen_loc)))
            {
                // Excludes machines that don't use power, as these are usually non-machine machinery
                if (Dm.eq(sick_device.use_power, NO_POWER_USE))
                    continue;
                // Look for dense machinery. Basically stops stuff like wall mounts and pipes, silly ones.
                // But keep in vents and scrubbers. I think it's funny if they start spitting out radiation
                if (!Dm.truthy(sick_device.density) && sick_device is not obj_machinery_atmospherics_components_unary)
                    continue;
                // Skip invisible stuff
                if (Dm.truthy(sick_device.invisibility) || !Dm.truthy(sick_device.alpha) || !Dm.truthy(sick_device.mouse_opacity))
                    continue;
                // Skip undertiles
                if (Dm.truthy(sick_device.IsObscured()))
                    continue;
                // Very basic check for atmos passability here
                // We don't want to put our smoke inside something that can't spread it out, like airlocks
                if (!Dm.eq(sick_device.can_atmos_pass__proc(), ATMOS_PASS_YES))
                    continue;
                // We found something, we can just return now
                picked_machine_ref = (datum_weakref)WEAKREF(sick_device);
                return null;
            }
        }
        return null;
    }

    // tg: code/modules/events/radiation_leak.dm:52
    public override async DmTask announce__proc(object fake = null)
    {
        var the_source_of_our_problems = picked_machine_ref?.resolve() as obj_machinery;
        area location_descriptor = null;
        if (Dm.truthy(fake))
            location_descriptor = GLOB.areas_by_type[pick(GLOB.the_station_areas)] as area;
        else if (the_source_of_our_problems != null)
            location_descriptor = (area)get_area(the_source_of_our_problems);

        priority_announce(Dm.text("A radiation leak has been detected in []. All crew are to evacuate the affected area. Our [] report that a machine within is causing it - repair it quickly to stop the leak.",
            (object)location_descriptor ?? "an unknown area", pick("mechanics", "engineers", "scientists", "interns", "sensors", "readings")),
            Dm.text("[] Engineering Division", command_name()));
    }

    // tg: code/modules/events/radiation_leak.dm:64
    public override async DmTask<object> start()
    {
        var the_source_of_our_problems = picked_machine_ref?.resolve() as obj_machinery;
        if (the_source_of_our_problems == null)
            return null;

        // We'll add some tool acts to the thing that allow people to "repair the machine"
        // The key of this assoc list is the "method" of how they're fixing the thing (just flavor for examine),
        // and the value is what tool they actually need to use on the thing to fix it
        var how_do_we_fix_it = Dm.list(
            Dm.kv(I18n.T("wrenching a few valves"), TOOL_WRENCH),
            Dm.kv(I18n.T("tightening its bolts"), TOOL_WRENCH),
            Dm.kv(I18n.T("crowbaring its panel []", pick(I18n.T("down"), I18n.T("up"))), TOOL_CROWBAR),
            Dm.kv(I18n.T("tightening some screws"), TOOL_SCREWDRIVER),
            Dm.kv(I18n.T("checking its []", pick(I18n.T("wires"), I18n.T("circuits"))), TOOL_MULTITOOL),
            Dm.kv(I18n.T("welding its panel []", pick(I18n.T("open"), I18n.T("shut"))), TOOL_WELDER),
            Dm.kv(I18n.T("analyzing its readings"), TOOL_ANALYZER),
            Dm.kv(I18n.T("cutting some excess wires"), TOOL_WIRECUTTER)
        );

        var fix_it_keys = (DmList)assoc_to_keys(how_do_we_fix_it); // Returns a copy that we can pick and take from, fortunately
        // Select a few methods of how to fix it
        var methods_to_fix = Dm.list();
        float count = rand(1, 3);
        for (int i = 1; i <= count; i++)
            Dm.append(methods_to_fix, pick_n_take(fix_it_keys));

        // Construct the signals
        signals_to_add = Dm.list();
        foreach (object tool_method in Dm.each(methods_to_fix))
            Dm.append(signals_to_add, COMSIG_ATOM_TOOL_ACT(how_do_we_fix_it[tool_method]));

        the_source_of_our_problems.visible_message(span_danger(I18n.T("[] starts to emanate a horrible green gas!", the_source_of_our_problems)));
        // Add the component that makes the thing radioactive
        await the_source_of_our_problems.AddComponent(typeof(datum_component_radioactive_emitter),
            Dm.named("cooldown_time", 2 * SECONDS),
            Dm.named("range", 5),
            Dm.named("threshold", RAD_MEDIUM_INSULATION),
            Dm.named("examine_text", span_green(I18n.T("<i>It's emanating a green gas... You could probably stop it by [].</i>", english_list(methods_to_fix, and_text: I18n.T(" or "))))));

        // Register signals to make it fixable
        if (Dm.length(signals_to_add) > 0)
            RegisterSignals(the_source_of_our_problems, signals_to_add, nameof(on_machine_tooled));

        // And yknow puffs some nasty reagents into the air, just to seal the deal
        await puff_some_smoke(the_source_of_our_problems);

        // Let ghosts know
        announce_to_ghosts(the_source_of_our_problems);
        return null;
    }

    // tg: code/modules/events/radiation_leak.dm:113
    public override async DmTask tick()
    {
        // Puff some smoke into the air around our machine roughly 3 times before we stop
        if (activeFor % (end_when / 3) != 0)
            return;

        var impromptu_smoke_machine = picked_machine_ref?.resolve() as obj_machinery;
        if (impromptu_smoke_machine == null)
            return;

        await puff_some_smoke(impromptu_smoke_machine);
    }

    // tg: code/modules/events/radiation_leak.dm:124
    public override async DmTask end()
    {
        var the_end_of_our_problems = picked_machine_ref?.resolve() as obj_machinery;
        if (the_end_of_our_problems == null)
            return;

        the_end_of_our_problems.visible_message(span_notice(I18n.T("The gas emanating from [] dissipates.", the_end_of_our_problems)));
        qdel(the_end_of_our_problems.GetComponent(typeof(datum_component_radioactive_emitter)));
        if (Dm.length(signals_to_add) > 0)
            UnregisterSignal(the_end_of_our_problems, signals_to_add);

        picked_machine_ref = null;
        signals_to_add = null;
    }

    /// Helper to shoot some smoke into the air around the passed atom
    // tg: code/modules/events/radiation_leak.dm:138
    public virtual async DmTask puff_some_smoke(atom where = null)
    {
        // Causes radioation and mutations
        turf below_where = get_turf(where);
        await do_chem_smoke(2, where, below_where, Dm.list(Dm.kv(typeof(datum_reagent_toxin_polonium), 10), Dm.kv(typeof(datum_reagent_toxin_mutagen), 10)));
        playsound(below_where, "sound/effects/smoke.ogg", 50, vary: TRUE);
    }

    /**
     * Signal proc for [COMSIG_ATOM_TOOL_ACT], from a variety of signals, registered on the machine spitting radiation
     *
     * We allow for someone to stop the event early by using the proper tools, hinted at in examine, on the machine
     */
    // tg: code/modules/events/radiation_leak.dm:150
    public virtual object on_machine_tooled(obj_machinery source = null, mob_living user = null, obj_item tool = null)
    {
        // SIGNAL_HANDLER
        Waiting.Detach(try_remove_radiation(source, user, tool)); // INVOKE_ASYNC
        return ITEM_INTERACT_BLOCKING;
    }

    /// Attempts a do_after, and if successful, stops the event
    // tg: code/modules/events/radiation_leak.dm:156
    public virtual async DmTask try_remove_radiation(obj_machinery source = null, mob_living user = null, obj_item tool = null)
    {
        source.balloon_alert(user, I18n.T("fixing leak..."));

        // Fairly long do after. It shouldn't be SUPER easy to just run in and stop it.
        // A tider can fix it if they want to soak a bunch of rads and inhale noxious fumes,
        // but only an equipped engineer should be able to handle it painlessly.
        if (!Dm.truthy(await tool.use_tool(source, user, 30 * SECONDS, amount: Dm.eq(tool.tool_behaviour, TOOL_WELDER) ? 2 : 0, volume: 50)))
        {
            source.balloon_alert(user, I18n.T("interrupted!"));
            return;
        }

        source.balloon_alert(user, I18n.T("leak repaired"));
        // Force end the event
        processing = FALSE;
        await end();
        kill();
    }
}
