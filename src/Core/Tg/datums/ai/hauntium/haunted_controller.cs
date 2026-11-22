// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/hauntium/haunted_controller.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1a (D327): the whole file, the haunted item's AI controller (its tree is tg's haunted.bt.json). The var block is generated
// (D111).

// tg: code/datums/ai/hauntium/haunted_controller.dm:1
public partial class datum_ai_controller_haunted : datum_ai_controller
{
    // tg: code/datums/ai/hauntium/haunted_controller.dm:12
    public override async DmTask<object> TryPossessPawn(atom new_pawn = null)
    {
        if (!isitem(new_pawn))
            return AI_CONTROLLER_INCOMPATIBLE;
        RegisterSignal(new_pawn, COMSIG_ITEM_EQUIPPED, nameof(on_equip));
        return await base.TryPossessPawn(new_pawn); //Run parent at end
    }

    // tg: code/datums/ai/hauntium/haunted_controller.dm:18
    public override async DmTask UnpossessPawn(object destroy = null)
    {
        UnregisterSignal(pawn, COMSIG_ITEM_EQUIPPED);
        await base.UnpossessPawn(destroy); //Run parent at end
    }

    ///Signal response for when the item is picked up; stops listening for follow up equips, just waits for a drop.
    // tg: code/datums/ai/hauntium/haunted_controller.dm:23
    [SignalHandler]
    public virtual void on_equip(datum source = null, mob equipper = null, object slot = null)
    {
        UnregisterSignal(pawn, COMSIG_ITEM_EQUIPPED);
        bool haunt_equipper = true;
        if (isliving(equipper))
        {
            var possibly_cool = (mob_living)equipper;
            if (Dm.truthy(Dm.bitand(possibly_cool.mob_biotypes, MOB_UNDEAD)) || HAS_MIND_TRAIT(possibly_cool, TRAIT_MORBID))
                haunt_equipper = false;
        }
        if (haunt_equipper)
            //You have now become one of the victims of the HAAAAUNTTIIIINNGGG OOOOOO~~~
            set_blackboard_key_assoc(BB_TO_HAUNT_LIST, equipper, HAUNTED_ITEM_AGGRO_ADDITION);
        else
            set_blackboard_key(BB_LIKES_EQUIPPER, TRUE);

        RegisterSignal(pawn, COMSIG_ITEM_DROPPED, nameof(on_dropped));
    }

    ///Flip it so we listen for equip again but not for drop.
    // tg: code/datums/ai/hauntium/haunted_controller.dm:41
    [SignalHandler]
    public virtual void on_dropped(datum source = null, mob user = null)
    {
        set_blackboard_key(BB_LIKES_EQUIPPER, FALSE);
        RegisterSignal(pawn, COMSIG_ITEM_EQUIPPED, nameof(on_equip));
        UnregisterSignal(pawn, COMSIG_ITEM_DROPPED);
    }
}
