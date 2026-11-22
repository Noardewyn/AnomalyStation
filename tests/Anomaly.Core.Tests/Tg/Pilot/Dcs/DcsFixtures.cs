using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Dcs;

// Fixtures for the behaviour tests of the dcs package (wave 2 pilot), written from the DM source of
// code/datums/components/_component.dm, code/datums/elements/_element.dm and code/controllers/subsystem/dcs.dm.
// The port has no content components or elements yet, so these stand in for them: DM-shaped types under
// /datum/component/test_dcs_*, /datum/element/test_dcs_* and /datum/test_dcs_* (no tg path starts so), with the slot
// signatures of docs/ai/specs/generated/slots.tsv, that record what tg's dcs procs do to them in one shared trace (the
// recording harness of translation-rules.md 15). Traces are host-side and [NotHashed].

/// <summary>
/// A world for one test, bootstrapped as the unit test harness does it (TgUnitTest.RunOnce): GLOB with the InitGlobal
/// procs translated so far (GLOB.sortInstance, which GetIdFromArguments' sortTim uses), then SSgarbage and SSdcs created
/// the way the MC would create them (NEW_SS_GLOBAL).
/// </summary>
internal sealed class DcsTestWorld
{
    public readonly World World;
    public readonly List<Exception> Runtimes = new();
    public readonly List<string> Trace = new();

    public DcsTestWorld(ulong seed = 7)
    {
        World = new World(seed) { RuntimeErrorSink = Runtimes.Add };
        using (World.Enter())
        {
            // GLOB without its New (controllers/globals.dm:11), which queues a timer (QDEL_IN) and needs SStimer; its
            // Initialize (:48) runs every translated InitGlobal proc (GLOB.sortInstance, ...). The wave-3 bootstrap
            // (TgWorld.Boot) replaces this stand-in once every wave-3 package is merged.
            GLOB__var = new datum_controller_global_vars();
            Dm.@new<datum_controller_subsystem_materials>(); // M5 slice 2: GLOB's make_datum_reference_lists reads SSmaterials' recipes (global_lists.dm:93)
            Dm.@new<datum_controller_subsystem_accessories>(); // M10x slice D2: tg makes it at PreInit, before GLOB (sprite_accessories.dm:47); bodies read its lists
            Dm.@new<datum_controller_subsystem_garbage>(); // M5 slice 8: before GLOB, as the Master makes it (master.cs:186-198): init_crafting_recipes qdels the recipes out of scope
            Waiting.NoSleep(GLOB.Initialize());
            Dm.@new<datum_controller_subsystem_processing_dcs>();
        }
    }

    /// <summary>The runtime messages reported so far, "Type: message".</summary>
    public List<string> Messages => Runtimes.Select(e => e.GetType().Name + ": " + e.Message).ToList();

    /// <summary>A host that records into <see cref="Trace"/>. Call inside <c>World.Enter()</c>.</summary>
    public test_dcs_host Host(string label)
    {
        test_dcs_host host = Dm.@new<test_dcs_host>();
        host.label = label;
        host.trace = Trace;
        return host;
    }

    /// <summary>A watcher of the dcs signals of <paramref name="targets"/> that records into <see cref="Trace"/>.</summary>
    public test_dcs_watcher Watch(params datum[] targets)
    {
        test_dcs_watcher watcher = Dm.@new<test_dcs_watcher>();
        watcher.trace = Trace;
        foreach (datum target in targets)
        {
            watcher.RegisterSignal(target, COMSIG_COMPONENT_ADDED, nameof(test_dcs_watcher.on_component_added));
            watcher.RegisterSignal(target, COMSIG_COMPONENT_REMOVING, nameof(test_dcs_watcher.on_component_removing));
            watcher.RegisterSignal(target, COMSIG_ELEMENT_ATTACH, nameof(test_dcs_watcher.on_element_attach));
            watcher.RegisterSignal(target, COMSIG_ELEMENT_DETACH, nameof(test_dcs_watcher.on_element_detach));
        }
        return watcher;
    }
}

/// <summary>How the trace names things: a host and a component by their labels, an element by its type's last path segment.</summary>
internal static class DcsDescribe
{
    public static string Of(object value) => value switch
    {
        null => "null",
        test_dcs_host h => h.label,
        dcs_recording_component c => Dm.str(c.label),
        datum_element e => DmTypes.PathOf(e.type).Split('/')[^1],
        datum d => "#" + d.dm_id,
        _ => Dm.str(value),
    };

    /// <summary>The arguments a proc got in <c>__extra</c>, without the trailing nulls the binder pads with.</summary>
    public static string Args(object[] extra)
    {
        extra ??= Array.Empty<object>();
        int count = extra.Length;
        while (count > 0 && extra[count - 1] == null)
            count--;
        return count == 0 ? "" : " args=" + string.Join(",", extra.Take(count).Select(Of));
    }
}

/// <summary>The datum components and elements are added to: carries the trace and a label.</summary>
[DmPath("/datum/test_dcs_host")]
public class test_dcs_host : datum
{
    [NotHashed]
    public List<string> trace;

    public string label = "host";
}

/// <summary>A listener of the dcs signals sent on a host (signals_datum.dm:7, :9, :38, :40).</summary>
[DmPath("/datum/test_dcs_watcher")]
public class test_dcs_watcher : datum
{
    [NotHashed]
    public List<string> trace;

    [SignalHandler]
    public virtual void on_component_added(datum source = null, datum_component added = null)
        => trace.Add(DcsDescribe.Of(source) + " COMSIG_COMPONENT_ADDED " + DcsDescribe.Of(added) + " parent=" + DcsDescribe.Of(added?.parent));

    [SignalHandler]
    public virtual void on_component_removing(datum source = null, datum_component removing = null)
        => trace.Add(DcsDescribe.Of(source) + " COMSIG_COMPONENT_REMOVING " + DcsDescribe.Of(removing) + " parent=" + DcsDescribe.Of(removing?.parent));

    [SignalHandler]
    public virtual void on_element_attach(datum source = null, datum_element element = null)
        => trace.Add(DcsDescribe.Of(source) + " COMSIG_ELEMENT_ATTACH " + DcsDescribe.Of(element));

    [SignalHandler]
    public virtual void on_element_detach(datum source = null, datum_element element = null)
        => trace.Add(DcsDescribe.Of(source) + " COMSIG_ELEMENT_DETACH " + DcsDescribe.Of(element));
}

/// <summary>
/// The recording overrides shared by the fixture components. A C# convenience only: it has no [DmPath], so the type
/// registry skips it and each fixture's DM parent is its nearest registered base (/datum/component for the root
/// fixtures, which are "root level + 1" like most of tg's components, _component.dm:241).
/// Initialize takes (label, initialize_result): the label names the component in the trace and Initialize returns
/// initialize_result (COMPONENT_INCOMPATIBLE, COMPONENT_REDUNDANT, or null). The procs that tg calls with arglist of
/// the AddComponent arguments (InheritComponent for COMPONENT_DUPE_UNIQUE_PASSARGS, CheckDupeComponent, on_source_add)
/// declare the same names, as a tg component that uses those modes must.
/// </summary>
public class dcs_recording_component : datum_component
{
    [NotHashed]
    public List<string> trace;

    /// The name in the trace (Initialize's first argument)
    public object label;

    public void Log(string line) => trace?.Add(DcsDescribe.Of(this) + " " + line);

    [DmParams("label", "initialize_result")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object label = Dm.arg(__extra, 0);
        object initialize_result = Dm.arg(__extra, 1);
        this.label = label;
        trace = (parent as test_dcs_host)?.trace;
        Log("Initialize parent=" + DcsDescribe.Of(parent) + (initialize_result == null ? "" : " returns " + Dm.str(initialize_result)));
        return initialize_result;
    }

    public override async DmTask RegisterWithParent()
    {
        Log("RegisterWithParent parent=" + DcsDescribe.Of(parent));
    }

    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        Log("UnregisterFromParent parent=" + DcsDescribe.Of(parent));
    }

    public override object Destroy(float? force = null)
    {
        Log("Destroy force=" + DcsDescribe.Of(force) + " parent=" + DcsDescribe.Of(parent));
        return base.Destroy(force);
    }

    [DmParams("C", "i_am_original", "label", "initialize_result")]
    public override async DmTask InheritComponent(datum_component C = null, object i_am_original = null, params object[] __extra)
    {
        Log("InheritComponent C=" + DcsDescribe.Of(C) + " i_am_original=" + DcsDescribe.Of(i_am_original) + DcsDescribe.Args(__extra));
    }

    [DmParams("C", "label", "initialize_result")]
    public override async DmTask<object> CheckDupeComponent(datum_component C = null, params object[] __extra)
    {
        Log("CheckDupeComponent C=" + DcsDescribe.Of(C) + DcsDescribe.Args(__extra));
        return null;
    }
}

/// <summary>COMPONENT_DUPE_HIGHLANDER (the default dupe_mode, _component.dm:18).</summary>
[DmPath("/datum/component/test_dcs_highlander")]
public class test_dcs_highlander : dcs_recording_component
{
}

/// <summary>A subtype: registered under its own type and /datum/component/test_dcs_highlander (_GetInverseTypeList).</summary>
[DmPath("/datum/component/test_dcs_highlander/sub")]
public class test_dcs_highlander_sub : test_dcs_highlander
{
}

/// <summary>A component whose Initialize qdels its parent (what tg's New guards against, _component.dm:58-59).</summary>
[DmPath("/datum/component/test_dcs_parent_killer")]
public class test_dcs_parent_killer : dcs_recording_component
{
    [DmParams("label", "initialize_result")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object dot = await base.Initialize(__extra);
        qdel(parent);
        return dot;
    }
}

/// <summary>COMPONENT_DUPE_ALLOWED.</summary>
[DmPath("/datum/component/test_dcs_allowed")]
public class test_dcs_allowed : dcs_recording_component
{
    public test_dcs_allowed()
    {
        dupe_mode = COMPONENT_DUPE_ALLOWED;
    }
}

[DmPath("/datum/component/test_dcs_allowed/sub")]
public class test_dcs_allowed_sub : test_dcs_allowed
{
}

[DmPath("/datum/component/test_dcs_allowed/sub/deeper")]
public class test_dcs_allowed_sub_deeper : test_dcs_allowed_sub
{
}

/// <summary>COMPONENT_DUPE_UNIQUE.</summary>
[DmPath("/datum/component/test_dcs_unique")]
public class test_dcs_unique : dcs_recording_component
{
    public test_dcs_unique()
    {
        dupe_mode = COMPONENT_DUPE_UNIQUE;
    }
}

/// <summary>COMPONENT_DUPE_UNIQUE_PASSARGS.</summary>
[DmPath("/datum/component/test_dcs_unique_passargs")]
public class test_dcs_unique_passargs : dcs_recording_component
{
    public test_dcs_unique_passargs()
    {
        dupe_mode = COMPONENT_DUPE_UNIQUE_PASSARGS;
    }
}

/// <summary>COMPONENT_DUPE_SELECTIVE: absorbs a new component whose label equals its own (CheckDupeComponent returns TRUE).</summary>
[DmPath("/datum/component/test_dcs_selective")]
public class test_dcs_selective : dcs_recording_component
{
    public test_dcs_selective()
    {
        dupe_mode = COMPONENT_DUPE_SELECTIVE;
    }

    [DmParams("C", "label", "initialize_result")]
    public override async DmTask<object> CheckDupeComponent(datum_component C = null, params object[] __extra)
    {
        object label = Dm.arg(__extra, 0);
        await base.CheckDupeComponent(C, __extra);
        return Dm.num(Dm.eq(label, this.label));
    }
}

/// <summary>
/// COMPONENT_DUPE_SOURCES. on_source_add takes the AddComponentFrom arguments (source, label, initialize_result,
/// source_result) and returns source_result when it is set (after the parent call, as a tg override that rejects a
/// source would); <see cref="allow_update"/> is what allow_source_update returns.
/// </summary>
[DmPath("/datum/component/test_dcs_sources")]
public class test_dcs_sources : dcs_recording_component
{
    public float allow_update = FALSE;

    public test_dcs_sources()
    {
        dupe_mode = COMPONENT_DUPE_SOURCES;
    }

    [DmParams("source", "label", "initialize_result", "source_result")]
    public override async DmTask<object> on_source_add(object source = null, params object[] __extra)
    {
        object source_result = Dm.arg(__extra, 2);
        object dot = await base.on_source_add(source, __extra);
        Log("on_source_add " + DcsDescribe.Of(source) + DcsDescribe.Args(__extra) + " sources=" + Sources());
        return source_result != null ? source_result : dot;
    }

    public override void on_source_remove(object source = null)
    {
        Log("on_source_remove " + DcsDescribe.Of(source));
        base.on_source_remove(source);
    }

    public override object allow_source_update(object source = null)
    {
        return allow_update;
    }

    public string Sources() => sources == null ? "null" : string.Join(",", Dm.each(sources).Select(DcsDescribe.Of));
}

/// <summary>
/// A transferable component (can_transfer = TRUE) that records PreTransfer and PostTransfer; PostTransfer returns
/// <see cref="post_transfer_result"/> (null: accept, COMPONENT_NOTRANSFER, COMPONENT_INCOMPATIBLE).
/// </summary>
[DmPath("/datum/component/test_dcs_transferable")]
public class test_dcs_transferable : dcs_recording_component
{
    public object post_transfer_result;

    public test_dcs_transferable()
    {
        can_transfer = TRUE;
    }

    public override async DmTask PreTransfer(datum new_parent = null)
    {
        Log("PreTransfer new_parent=" + DcsDescribe.Of(new_parent) + " parent=" + DcsDescribe.Of(parent));
    }

    public override async DmTask<object> PostTransfer(datum new_parent = null)
    {
        Log("PostTransfer new_parent=" + DcsDescribe.Of(new_parent) + " parent=" + DcsDescribe.Of(parent));
        return post_transfer_result;
    }
}

/// <summary>A transferable COMPONENT_DUPE_UNIQUE component: a transfer onto a parent that has one already is absorbed.</summary>
[DmPath("/datum/component/test_dcs_transferable_unique")]
public class test_dcs_transferable_unique : test_dcs_transferable
{
    public test_dcs_transferable_unique()
    {
        dupe_mode = COMPONENT_DUPE_UNIQUE;
    }
}

/// <summary>A transferable COMPONENT_DUPE_UNIQUE_PASSARGS component.</summary>
[DmPath("/datum/component/test_dcs_transferable_passargs")]
public class test_dcs_transferable_passargs : test_dcs_transferable
{
    public test_dcs_transferable_passargs()
    {
        dupe_mode = COMPONENT_DUPE_UNIQUE_PASSARGS;
    }
}

/// <summary>A transferable COMPONENT_DUPE_SELECTIVE component that would absorb any new one.</summary>
[DmPath("/datum/component/test_dcs_transferable_selective")]
public class test_dcs_transferable_selective : test_dcs_transferable
{
    public test_dcs_transferable_selective()
    {
        dupe_mode = COMPONENT_DUPE_SELECTIVE;
    }

    [DmParams("C", "label", "initialize_result")]
    public override async DmTask<object> CheckDupeComponent(datum_component C = null, params object[] __extra)
    {
        await base.CheckDupeComponent(C, __extra);
        return TRUE;
    }
}

/// <summary>
/// A transferable component that listens to its parent the way tg's components do: it registers for a signal in
/// RegisterWithParent and unregisters in UnregisterFromParent (_component.dm:26), and records what it hears.
/// </summary>
[DmPath("/datum/component/test_dcs_listening")]
public class test_dcs_listening : test_dcs_transferable
{
    public const string COMSIG_TEST_DCS_PING = "test_dcs_ping";

    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_TEST_DCS_PING, nameof(on_ping));
    }

    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        UnregisterSignal(parent, COMSIG_TEST_DCS_PING);
    }

    [SignalHandler]
    public virtual void on_ping(datum source = null)
    {
        Log("heard ping from " + DcsDescribe.Of(source));
    }
}

/// <summary>
/// The recording overrides shared by the fixture elements (a C# convenience without [DmPath], like
/// <see cref="dcs_recording_component"/>). An element is a singleton, so it records into the trace of the host it is
/// attached to or detached from.
/// </summary>
public class dcs_recording_element : datum_element
{
    private static void Log(datum host, string line) => (host as test_dcs_host)?.trace?.Add(DcsDescribe.Of(host) + " " + line);

    [DmParams("target", "first", "second", "a", "b")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (Dm.eq(dot, ELEMENT_INCOMPATIBLE))
            return dot;
        Log(target, "Attach " + DcsDescribe.Of(this) + DcsDescribe.Args(__extra));
        return dot;
    }

    [DmParams("source", "first", "second", "a", "b")]
    public override object Detach(datum source = null, params object[] __extra)
    {
        Log(source, "Detach " + DcsDescribe.Of(this) + DcsDescribe.Args(__extra));
        return base.Detach(source, __extra);
    }
}

/// <summary>An element without flags: one instance for every target.</summary>
[DmPath("/datum/element/test_dcs_simple")]
public class test_dcs_simple : dcs_recording_element
{
}

/// <summary>ELEMENT_DETACH_ON_HOST_DESTROY.</summary>
[DmPath("/datum/element/test_dcs_host_bound")]
public class test_dcs_host_bound : dcs_recording_element
{
    public test_dcs_host_bound()
    {
        element_flags = ELEMENT_DETACH_ON_HOST_DESTROY;
    }
}

/// <summary>ELEMENT_BESPOKE from the second argument on.</summary>
[DmPath("/datum/element/test_dcs_bespoke")]
public class test_dcs_bespoke : dcs_recording_element
{
    public test_dcs_bespoke()
    {
        element_flags = ELEMENT_BESPOKE;
        argument_hash_start_idx = 2;
    }
}

/// <summary>ELEMENT_BESPOKE over the second and third arguments only (argument_hash_end_idx).</summary>
[DmPath("/datum/element/test_dcs_bespoke_window")]
public class test_dcs_bespoke_window : dcs_recording_element
{
    public test_dcs_bespoke_window()
    {
        element_flags = ELEMENT_BESPOKE;
        argument_hash_start_idx = 2;
        argument_hash_end_idx = 3;
    }
}

/// <summary>ELEMENT_BESPOKE | ELEMENT_COMPLEX_DETACH: RemoveElement hands Detach its arguments.</summary>
[DmPath("/datum/element/test_dcs_complex_detach")]
public class test_dcs_complex_detach : dcs_recording_element
{
    public test_dcs_complex_detach()
    {
        element_flags = Dm.bitor(ELEMENT_BESPOKE, ELEMENT_COMPLEX_DETACH);
        argument_hash_start_idx = 2;
    }
}

/// <summary>An element whose Attach refuses every target.</summary>
[DmPath("/datum/element/test_dcs_incompatible")]
public class test_dcs_incompatible : dcs_recording_element
{
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        await base.Attach(target, __extra);
        return ELEMENT_INCOMPATIBLE;
    }
}
