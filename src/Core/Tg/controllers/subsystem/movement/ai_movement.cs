// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/movement/ai_movement.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1, the AI). The var block is generated.

/// The subsystem used to tick [/datum/ai_movement] instances. Handling the movement of individual AI instances
// tg: code/controllers/subsystem/movement/ai_movement.dm:2 MOVEMENT_SUBSYSTEM_DEF(ai_movement)
public partial class datum_controller_subsystem_movement_ai_movement : datum_controller_subsystem_movement
{
    // The New() that MOVEMENT_SUBSYSTEM_DEF(ai_movement) defines (__DEFINES/MC.dm:107-111), with NEW_SS_GLOBAL (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/movement/ai_movement.dm:2
    public override async DmTask New(params object[] __extra)
    {
        if (SSai_movement != this)
        {
            if (SSai_movement is datum_controller_subsystem_movement_ai_movement)
            {
                Recover();
                qdel(SSai_movement);
            }
            SSai_movement = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/movement/ai_movement.dm:2 (the macro's fire(), ..() for the profiler)
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra);
    }

    // tg: code/controllers/subsystem/movement/ai_movement.dm:11
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        SetupAIMovementInstances();
        return SS_INIT_SUCCESS;
    }

    // tg: code/controllers/subsystem/movement/ai_movement.dm:15
    public virtual void SetupAIMovementInstances()
    {
        movement_types = Dm.list();
        foreach (Type key in Dm.each_cast<Type>(subtypesof(typeof(datum_ai_movement))))
        {
            datum_ai_movement ai_movement = Dm.@new(key) as datum_ai_movement;
            movement_types[key] = ai_movement;
        }
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/movement/ai_movement.dm:2
    internal datum_controller_subsystem_movement_ai_movement SSai_movement;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_movement_ai_movement SSai_movement => ref TgState.Current.SSai_movement;
}
