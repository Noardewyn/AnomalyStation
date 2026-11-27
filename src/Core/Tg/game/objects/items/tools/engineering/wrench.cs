// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/tools/engineering/wrench.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the base wrench's Initialize (M4a P4a), the medical wrench's examine and the combat wrench (syndicate G4d, D316: the
// sentry gun's); the suicide acts are live-partial stubs (the suicide verb, M10+); the power and bolter wrenches come after the
// core version.

// tg: code/game/objects/items/tools/engineering/wrench.dm:1
public partial class obj_item_wrench : obj_item
{
    // tg: code/game/objects/items/tools/engineering/wrench.dm:33
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/game/objects/items/tools/engineering/wrench.dm:35", "Initialize: AddElement(/datum/element/falling_hazard, ...), the wrench falling on a head (falls, M5)");
        return dot;
    }

    // tg: code/game/objects/items/tools/engineering/wrench.dm:37  suicide_act: live-partial (M10).
}

// tg: code/game/objects/items/tools/engineering/wrench.dm:53
public partial class obj_item_wrench_medical : obj_item_wrench
{
    // tg: code/game/objects/items/tools/engineering/wrench.dm:65
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(suicider))
            Dm.append(ref dot, span_notice(I18n.T("For some reason, it reminds you of [].", suicider)));
        return dot;
    }

    // tg: code/game/objects/items/tools/engineering/wrench.dm:70  suicide_act: live-partial (the suicide verb, M10+).
}

// tg: code/game/objects/items/tools/engineering/wrench.dm:97
public partial class obj_item_wrench_combat : obj_item_wrench
{
    // tg: code/game/objects/items/tools/engineering/wrench.dm:108
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_transforming),
            Dm.named("force_on", 6),
            Dm.named("throwforce_on", 8),
            Dm.named("hitsound_on", hitsound),
            Dm.named("w_class_on", WEIGHT_CLASS_NORMAL),
            Dm.named("clumsy_check", FALSE)));
        RegisterSignal(this, COMSIG_TRANSFORMING_ON_TRANSFORM, nameof(on_transform));
        return dot;
    }

    /*
     * Signal proc for [COMSIG_TRANSFORMING_ON_TRANSFORM].
     *
     * Gives it wrench behaviors when active.
     */
    // tg: code/game/objects/items/tools/engineering/wrench.dm:125
    [SignalHandler]
    public virtual object on_transform(obj_item source = null, mob user = null, object active = null)
    {
        tool_behaviour = Dm.truthy(active) ? TOOL_WRENCH : Dm.initial(this, (obj_item p) => p.tool_behaviour);
        if (user != null)
            balloon_alert(user, I18n.T("[] []", name, Dm.truthy(active) ? "active, woe!" : "restrained"));
        playsound(this, Dm.truthy(active) ? Dm.resource("sound/items/weapons/saberon.ogg") : Dm.resource("sound/items/weapons/saberoff.ogg"), 5, TRUE);
        return COMPONENT_NO_DEFAULT_MESSAGE;
    }
}
