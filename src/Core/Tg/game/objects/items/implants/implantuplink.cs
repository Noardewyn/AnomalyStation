// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/implants/implantuplink.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3). The var blocks are generated (D111).

// tg: code/game/objects/items/implants/implantuplink.dm:1
public partial class obj_item_implant_uplink : obj_item_implant
{
    // tg: code/game/objects/items/implants/implantuplink.dm:14
    [DmParams("mapload", "uplink_handler")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object uplink_handler = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        // tg: :16-17  if(!uplink_flag) uplink_flag = src.uplink_flag: the same var on both sides (no uplink_flag argument), a no-op
        this.uplink_handler = uplink_handler;
        RegisterSignal(this, COMSIG_COMPONENT_REMOVING, nameof(on_component_removing));
        return dot;
    }

    // tg: code/game/objects/items/implants/implantuplink.dm:21
    [DmParams("target", "user", "silent", "force")]
    public override async DmTask<object> implant(mob_living target = null, mob user = null, float? silent = null, float? force = null, params object[] __extra)
    {
        object dot = await base.implant(target, user, silent, force, __extra);
        datum_component_uplink new_uplink = await AddComponent(
            typeof(datum_component_uplink),
            Dm.named("owner", target?.key),
            Dm.named("lockable", TRUE),
            Dm.named("enabled", FALSE),
            Dm.named("uplink_handler_override", uplink_handler),
            Dm.named("starting_tc", starting_tc)) as datum_component_uplink;
        new_uplink.unlock_text = I18n.T("Your Syndicate Uplink has been cunningly implanted in you, for a small TC fee. Simply trigger the uplink to access it.");
        if (!Dm.truthy(uplink_handler))
        {
            new_uplink.uplink_handler.owner = target.mind;
            new_uplink.uplink_handler.assigned_role = target.mind.assigned_role.title;
            new_uplink.uplink_handler.assigned_species = ((datum_species)((mob_living_carbon)target).dna.species).id;
        }
        return dot;
    }

    /**
     * Proc called when component is removed; ie. uplink component
     *
     * Callback catching if the underlying uplink component has been removed,
     * generally by admin verbs or var editing. Implant does nothing without
     * the component, so delete itself.
     */
    // tg: code/game/objects/items/implants/implantuplink.dm:39
    [SignalHandler]
    public virtual void on_component_removing(datum source = null, datum_component component = null)
    {
        if (QDELING(this))
            return;

        if (component is datum_component_uplink)
            qdel(this);
    }
}

// tg: code/game/objects/items/implants/implantuplink.dm:47
public partial class obj_item_implanter_uplink : obj_item_implanter
{
    // tg: code/game/objects/items/implants/implantuplink.dm:51
    [DmParams("mapload", "uplink_handler")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object uplink_handler = Dm.arg(__extra, 0);
        imp = Dm.@new(imp_type, this, uplink_handler) as obj_item_implant;
        return base.Initialize(mapload, __extra);
    }
}
