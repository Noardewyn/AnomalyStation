// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/robot/robot_defines.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI9 (D326): the model cyborgs' Initialize and the syndicate cyborgs' procs (the file's other content is var blocks,
// generated, D111).

// tg: code/modules/mob/living/silicon/robot/robot_defines.dm:143
public partial class mob_living_silicon_robot_model : mob_living_silicon_robot
{
    // tg: code/modules/mob/living/silicon/robot/robot_defines.dm:146
    [DmParams("mapload", "innate_laws", "master_ai", "aisync", "lawsync")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.Detach(model.transform_to(set_model, TRUE)); // INVOKE_ASYNC
        return dot;
    }
}

// tg: code/modules/mob/living/silicon/robot/robot_defines.dm:182
public partial class mob_living_silicon_robot_model_syndicate : mob_living_silicon_robot_model
{
    // tg: code/modules/mob/living/silicon/robot/robot_defines.dm:198
    [DmParams("mapload", "innate_laws", "master_ai", "aisync", "lawsync")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object[] args = new object[Math.Max(4, __extra?.Length ?? 0)];
        __extra?.CopyTo(args, 0);
        args[2] = FALSE; // aisync = FALSE
        object dot = base.Initialize(mapload, args);
        _addtimer(Dm.@new<datum_callback>(this, nameof(show_playstyle)), 0.5f * SECONDS, file: "code/modules/mob/living/silicon/robot/robot_defines.dm", line: 201);
        return dot;
    }

    // tg: code/modules/mob/living/silicon/robot/robot_defines.dm:203
    public override void make_laws()
    {
        laws = Dm.@new<datum_ai_laws_syndicate_override>();
    }

    // tg: code/modules/mob/living/silicon/robot/robot_defines.dm:206
    public override void create_modularInterface()
    {
        if (modularInterface == null)
        {
            modularInterface = Dm.@new<obj_item_modular_computer_pda_silicon_cyborg_syndicate>(this);
            modularInterface.imprint_id(job_name: "Cyborg");
        }
        base.create_modularInterface();
    }

    // tg: code/modules/mob/living/silicon/robot/robot_defines.dm:212
    public virtual void show_playstyle()
    {
        if (Dm.truthy(playstyle_string))
            to_chat(this, playstyle_string);
    }

    // tg: code/modules/mob/living/silicon/robot/robot_defines.dm:216
    public override async DmTask<object> ResetModel()
    {
        await DmTask.CompletedTask;
        return null;
    }
}
