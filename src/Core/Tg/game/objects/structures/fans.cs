// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/fans.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST5 (D333): the file whole. The environmental regulation fans (the survival pod's, the tiny ones, the invisible air flow
// blockers and the shuttle bay shields): they block the air of their tile (can_atmos_pass), are wrenched apart into iron sheets
// (a shield is not). The var blocks are generated (D111).

//Fans
// tg: code/game/objects/structures/fans.dm:2
public partial class obj_structure_fans : obj_structure
{
    // tg: code/game/objects/structures/fans.dm:13
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        if (buildstacktype != null)
            Dm.@new(buildstacktype, loc, buildstackamount);
        await DmTask.CompletedTask;
    }

    // tg: code/game/objects/structures/fans.dm:17
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item I = null)
    {
        user.visible_message(span_warning(I18n.T("[] disassembles [].", user, this)),
            span_notice(I18n.T("You start to disassemble []...", this)), span_hear(I18n.T("You hear clanking and banging noises.")));
        if (Dm.truthy(await I.use_tool(this, user, 20, volume: 50)))
            await deconstruct(TRUE);
        return TRUE;
    }

    // tg: code/game/objects/structures/fans.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        air_update_turf(TRUE, TRUE);
        return dot;
    }

    // tg: code/game/objects/structures/fans.dm:36
    public override object Destroy(float? force = null)
    {
        air_update_turf(TRUE, FALSE);
        return base.Destroy(force);
    }
}

// tg: code/game/objects/structures/fans.dm:46
public partial class obj_structure_fans_tiny_shield : obj_structure_fans_tiny
{
    // tg: code/game/objects/structures/fans.dm:54
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item I = null)
    {
        await DmTask.CompletedTask;
        return ITEM_INTERACT_SKIP_TO_ATTACK; //how you gonna wrench disassemble a shield?????????
    }
}
