// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/anomaly_neutralizer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DV2 (D333): the file whole. The anomaly neutralizer: one use on an anomaly (or a drained heretic influence) neutralizes it
// through the effect remover component, sparks and burns the neutralizer out. The var block is generated (D111).

// tg: code/game/objects/items/devices/anomaly_neutralizer.dm:1
public partial class obj_item_anomaly_neutralizer : obj_item
{
    // tg: code/game/objects/items/devices/anomaly_neutralizer.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        // Primarily used to delete and neutralize anomalies.
        Waiting.NoSleep(AddComponent(typeof(datum_component_effect_remover),
            Dm.named("success_feedback", "You neutralize %THEEFFECT with %THEWEAPON, frying its circuitry in the process."),
            Dm.named("tip_text", "Neutralize anomaly"),
            Dm.named("on_clear_callback", Dm.@new<datum_callback>(this, nameof(on_anomaly_neutralized))),
            Dm.named("effects_we_clear", Dm.list(typeof(obj_effect_anomaly)))));

        // Can also be used to delete drained heretic influences, to stop fools from losing arms.
        Waiting.NoSleep(AddComponent(typeof(datum_component_effect_remover),
            Dm.named("success_feedback", "You close %THEEFFECT with %THEWEAPON, frying its circuitry in the process."),
            Dm.named("tip_text", "Close rift"),
            Dm.named("on_clear_callback", Dm.@new<datum_callback>(this, nameof(on_use))),
            Dm.named("effects_we_clear", Dm.list(typeof(obj_effect_visible_heretic_influence)))));
        return dot;
    }

    /**
     * Callback for the effect remover component to handle neutralizing anomalies.
     */
    // tg: code/game/objects/items/devices/anomaly_neutralizer.dm:35
    public virtual async DmTask on_anomaly_neutralized(obj_effect_anomaly target = null, mob_living user = null)
    {
        await target.anomalyNeutralize();
        on_use(target, user);
    }

    /**
     * Use up the anomaly neutralizer. Cause some sparks and delete it.
     */
    // tg: code/game/objects/items/devices/anomaly_neutralizer.dm:42
    public virtual void on_use(obj_effect target = null, mob_living user = null)
    {
        do_sparks(3, FALSE, user);
        qdel(this);
    }
}
