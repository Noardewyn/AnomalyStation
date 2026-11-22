// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/objects/vending_machines/vending_machine_controller.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1b (D327): the whole file, the rogue vending machine's controller (the brand intelligence's uprising: a machine that waddles
// after the crew and tilts onto them; its tree is tg's vending_machine.bt.json). The var blocks are generated (D111).

///AI controller for vending machine gone rogue, Don't try using this on anything else, it wont work.
// tg: code/datums/ai/objects/vending_machines/vending_machine_controller.dm:2
public partial class datum_ai_controller_vending_machine : datum_ai_controller
{
    // tg: code/datums/ai/objects/vending_machines/vending_machine_controller.dm:15
    public override async DmTask<object> TryPossessPawn(atom new_pawn = null)
    {
        if (!(new_pawn is obj_machinery_vending))
            return AI_CONTROLLER_INCOMPATIBLE;
        var vendor_pawn = (obj_machinery_vending)new_pawn;
        vendor_pawn.tiltable = FALSE; //Not manually tiltable by hitting it anymore. We are now aggressively doing it ourselves.
        await vendor_pawn.AddElementTrait(TRAIT_WADDLING, REF(this), typeof(datum_element_waddling));
        await vendor_pawn.AddElement(typeof(datum_element_footstep), FOOTSTEP_OBJ_MACHINE, 1, -6, Dm.named("sound_vary", TRUE));
        vendor_pawn.squish_damage = 15;
        RegisterSignal(vendor_pawn, COMSIG_VENDING_UI_INTERACT, nameof(deny_vending_interact));
        return await base.TryPossessPawn(new_pawn); //Run parent at end
    }

    // tg: code/datums/ai/objects/vending_machines/vending_machine_controller.dm:26
    public override async DmTask UnpossessPawn(object destroy = null)
    {
        var vendor_pawn = (obj_machinery_vending)pawn;
        vendor_pawn.tiltable = TRUE;
        REMOVE_TRAIT(vendor_pawn, TRAIT_WADDLING, REF(this));
        vendor_pawn.squish_damage = Dm.initial(vendor_pawn, (obj_machinery_vending p) => p.squish_damage);
        // tg removes the footstep element from the controller (src), not from the pawn: a no-op it keeps
        RemoveElement(typeof(datum_element_footstep), FOOTSTEP_OBJ_MACHINE, 1, -6, Dm.named("sound_vary", TRUE));
        UnregisterSignal(vendor_pawn, COMSIG_VENDING_UI_INTERACT);
        await base.UnpossessPawn(destroy); //Run parent at end
    }

    // tg: code/datums/ai/objects/vending_machines/vending_machine_controller.dm:35
    [SignalHandler]
    public virtual object deny_vending_interact(obj_machinery_vending vending_machine = null, mob user = null, datum_tgui ui = null)
    {
        if (!Dm.truthy(block_usage))
            return NONE;
        Waiting.NoSleep(vending_machine.speak(pick(
            I18n.T("Once in a life time offer, and you []!", pick(I18n.T("blew it"), I18n.T("missed it"), I18n.T("screwed it up"))),
            I18n.T("The deals are off!"),
            I18n.T("We don't accept card, only accept flesh and blood!"),
            I18n.T("You had your chance!"))));
        return VENDING_DENIED;
    }
}
