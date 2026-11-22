// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/verbs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI4 (D326): the whole file but serialize_verb, the tgui panel's entry of a verb (procpath's name and category; the port's
// stat panel is the replicated command verb list, Infrastructure/ClientSeam.Verbs.cs). The var block is generated.

// tg: code/controllers/subsystem/verbs.dm:1 SUBSYSTEM_DEF(verbs)
public partial class datum_controller_subsystem_verbs : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(verbs) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSverbs) (__DEFINES/MC.dm:29) expanded inline
    // (it reads src).
    // tg: code/controllers/subsystem/verbs.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSverbs != this)
        {
            if (SSverbs is datum_controller_subsystem_verbs)
            {
                Recover();
                qdel(SSverbs);
            }
            SSverbs = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/verbs.dm:8
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!Dm.truthy(Dm.length(verbs_by_type)))
            initialize_verb_types();
        initialized = TRUE;
        return SS_INIT_SUCCESS;
    }

    // tg: code/controllers/subsystem/verbs.dm:14
    public virtual void initialize_verb_types()
    {
        foreach (Type verb_type in Dm.each_cast<Type>(subtypesof(typeof(datum_verb_metadata))))
        {
            var meta = Dm.@new(verb_type) as datum_verb_metadata;
            var pending = GLOB.____pending_verb_args[meta.body_path] as DmList;
            if (Dm.truthy(pending))
                meta.arguments = pending;
            verbs_by_type[verb_type] = meta;
            if (Dm.truthy(meta.verb_path))
                verbs_by_verb_path[meta.verb_path] = meta;
        }
    }

    // tg: code/controllers/subsystem/verbs.dm:24  (INVOKE_GAME_VERB passes the arguments as one alist, the body's structured_args)
    public virtual async DmTask invoke(object target = null, object verb_type = null, object invoker = null, params object[] __extra)
    {
        if (!Dm.truthy(initialized))
            initialize_verb_types();

        var meta = verbs_by_type[verb_type] as datum_verb_metadata;
        if (meta == null)
            throw Dm.CRASH($"Attempted to invoke unknown verb '{Dm.str(verb_type)}'.", "code/controllers/subsystem/verbs.dm", 30);
        object[] invoke_args = __extra ?? Array.Empty<object>(); // args.Copy(4)
        if (Dm.truthy(invoker))
            usr = invoker as mob;
        await Dm.Call(target, (string)meta.body_path, invoke_args);
    }

    // tg: code/controllers/subsystem/verbs.dm:36
    public virtual async DmTask invoke_verb(object target = null, object verb_path = null, DmList positional_args = null, object invoker = null)
    {
        if (!Dm.truthy(initialized))
            initialize_verb_types();

        var meta = verbs_by_verb_path[verb_path] as datum_verb_metadata;
        if (meta == null)
            throw Dm.CRASH($"invoke_verb called for '{Dm.str(verb_path)}' with no metadata registered", "code/controllers/subsystem/verbs.dm", 42);
        object resolved_caller = Dm.truthy(invoker) ? invoker : target;
        client user_client = null;
        if (resolved_caller is client caller_client)
            user_client = caller_client;
        else if (ismob(resolved_caller))
        {
            var mob_target = (mob)resolved_caller;
            user_client = mob_target.client;
        }
        if (user_client == null)
            return;

        DmList structured_args = await ____collect_verb_args(user_client, meta.name, meta.arguments, positional_args);
        if (structured_args == null)
            return;

        if (Dm.truthy(invoker))
            usr = invoker as mob;
        await Dm.Call(target, (string)meta.body_path, structured_args);
    }

    // tg: code/controllers/subsystem/verbs.dm:61
    public virtual void assign_verb(object target = null, Type verb_type = null)
    {
        // When launching via dreamseeker and not dreamdaemon, client is created first before any of the subsystems init
        // This can only happen in dev environments so its not a big deal
        if (!Dm.truthy(initialized))
            initialize_verb_types();
        var meta = verbs_by_type[verb_type] as datum_verb_metadata;
        if (meta == null)
            throw Dm.CRASH($"Attempted to assign unknown verb '{Dm.str(verb_type)}'.", "code/controllers/subsystem/verbs.dm", 68);
        meta.assign_to(target);
    }

    // tg: code/controllers/subsystem/verbs.dm:71
    public virtual void unassign_verb(object target = null, Type verb_type = null)
    {
        var meta = verbs_by_type[verb_type] as datum_verb_metadata;
        if (meta == null)
            throw Dm.CRASH($"Attempted to unassign unknown verb '{Dm.str(verb_type)}'.", "code/controllers/subsystem/verbs.dm", 74);
        meta.unassign_from(target);
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/verbs.dm:1  SUBSYSTEM_DEF(verbs): GLOBAL_REAL(SSverbs, /datum/controller/subsystem/verbs)
    internal datum_controller_subsystem_verbs SSverbs;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_verbs SSverbs => ref TgState.Current.SSverbs;
}
