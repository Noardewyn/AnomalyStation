// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/haunted.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1a (D327): the whole file, the haunted element (a floating item with a ghostly outline and the haunted AI) and the item's
// make_haunted and remove_haunted (the ectoplasmic anomaly's haunted items).

///Attaching this element to something will make it float, get a special ai controller, and gives it a spooky outline.
// tg: code/datums/elements/haunted.dm:2
public partial class datum_element_haunted : datum_element
{
    // tg: code/datums/elements/haunted.dm:4
    [DmParams("target", "haunt_color")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object haunt_color = Dm.arg(__extra, 0) ?? "#f8f8ff";
        object dot = await base.Attach(target, __extra);
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;

        var master = (obj_item)target;
        if (master.ai_controller is datum_ai_controller_haunted)
            return ELEMENT_INCOMPATIBLE;

        //Make em look spooky
        master.add_filter("haunt_glow", 2, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", haunt_color), Dm.kv("size", 1)));
        master.ai_controller = Dm.@new<datum_ai_controller_haunted>(master);
        await master.AddElement(typeof(datum_element_movetype_handler));
        ADD_TRAIT(master, TRAIT_MOVE_FLYING, ELEMENT_TRAIT(type));
        return dot;
    }

    // tg: code/datums/elements/haunted.dm:19
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        var master = (atom_movable)source;
        master.remove_filter("haunt_glow");
        QDEL_NULL(ref master.ai_controller);
        REMOVE_TRAIT(master, TRAIT_MOVE_FLYING, ELEMENT_TRAIT(type));
        master.RemoveElement(typeof(datum_element_movetype_handler));
        return dot;
    }
}

public partial class obj_item : obj
{
    // tg: code/datums/elements/haunted.dm:27
    public virtual void make_haunted(object source = null, object color = null) //if not haunted, make haunted
    {
        if (!HAS_TRAIT(this, TRAIT_HAUNTED))
            Waiting.NoSleep(AddElement(typeof(datum_element_haunted), color));
        ADD_TRAIT(this, TRAIT_HAUNTED, source);
    }

    // tg: code/datums/elements/haunted.dm:32
    public virtual void remove_haunted(object source = null) //if haunted, make not haunted
    {
        REMOVE_TRAIT(this, TRAIT_HAUNTED, source);
        if (!HAS_TRAIT(this, TRAIT_HAUNTED))
            RemoveElement(typeof(datum_element_haunted));
    }
}
