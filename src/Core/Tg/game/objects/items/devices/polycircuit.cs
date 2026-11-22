// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/polycircuit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DV2 (D333): the file whole. The polycircuit aggregate: held in the other hand, a circuit picked from tg's list (airlock,
// firelock, fire alarm, air alarm, APC) is pulled out of it in three seconds, or the hand is cut on its edges if the pull is broken
// off. The var block is generated (D111).

// tg: code/game/objects/items/devices/polycircuit.dm:1
public partial class obj_item_stack_circuit_stack : obj_item_stack
{
    // tg: code/game/objects/items/devices/polycircuit.dm:13  Prevents the crafting menu, and tells you how to use it.
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        to_chat(user, span_warning(I18n.T("You can't use [] by itself, you'll have to try and remove one of these circuits by hand... carefully.", this)));
        return null;
    }

    // tg: code/game/objects/items/devices/polycircuit.dm:16
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        mob_living_carbon_human H = user as mob_living_carbon_human;
        if (user.get_inactive_held_item() != this)
            return await base.attack_hand(user, modifiers, __extra);
        else
        {
            if (Dm.truthy(is_zero_amount(delete_if_zero: TRUE)))
                return null;
            chosen_circuit = await tgui_input_list(user, I18n.T("Circuit to remove"), I18n.T("Circuit Removal"), Dm.list("airlock", "firelock", "fire alarm", "air alarm", "APC"), chosen_circuit) as string;
            if (chosen_circuit == null)
            {
                to_chat(user, span_notice(I18n.T("You wisely avoid putting your hands anywhere near [].", this)));
                return null;
            }
            if (Dm.truthy(is_zero_amount(delete_if_zero: TRUE)))
                return null;
            if (loc != user)
                return null;
            object sw__1 = chosen_circuit;
            if (Dm.eq(sw__1, "airlock"))
                circuit_type = typeof(obj_item_electronics_airlock);
            else if (Dm.eq(sw__1, "firelock"))
                circuit_type = typeof(obj_item_electronics_firelock);
            else if (Dm.eq(sw__1, "fire alarm"))
                circuit_type = typeof(obj_item_electronics_firealarm);
            else if (Dm.eq(sw__1, "air alarm"))
                circuit_type = typeof(obj_item_electronics_airalarm);
            else if (Dm.eq(sw__1, "APC"))
                circuit_type = typeof(obj_item_electronics_apc);
            to_chat(user, span_notice(I18n.T("You spot your circuit, and carefully attempt to remove it from [], hold still!", this)));
            if (Dm.truthy(await do_after(user, 3 * SECONDS, target: user)))
            {
                if (!Dm.truthy(this) || QDELETED(this))//Sanity Check.
                    return null;
                obj_item returned_circuit = Dm.@new(circuit_type, this) as obj_item;
                await user.put_in_hands(returned_circuit);
                await use(1);
                if (!Dm.truthy(amount))
                    to_chat(user, span_notice(I18n.T("You navigate the sharp edges of circuitry and remove the last board.")));
                else
                    to_chat(user, span_notice(I18n.T("You navigate the sharp edges of circuitry and remove a single board from []", this)));
            }
            else
            {
                await H.apply_damage(15, BRUTE, pick(GLOB.arm_zones));
                to_chat(user, span_warning(I18n.T("You give yourself a wicked cut on []'s many sharp corners and edges!", this)));
            }
        }
        return null;
    }
}
