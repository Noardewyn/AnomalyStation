// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/tank_dispenser.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M4c slice 1: Meta's EVA room and engineering dispense tanks by hand: a click for a plasma tank, a right
// click for an oxygen tank, a tank put back); M5 slice 7: atom_deconstruct (its tanks and 2 iron). The var block is generated (D111).

// tg: code/game/objects/structures/tank_dispenser.dm:3
public partial class obj_structure_tank_dispenser : obj_structure
{
    // tg: code/game/objects/structures/tank_dispenser.dm:1  #define TANK_DISPENSER_CAPACITY 10 (file-local, #undef at :106)
    private const float TANK_DISPENSER_CAPACITY = 10;

    // tg: code/game/objects/structures/tank_dispenser.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_contextual_screentip_bare_hands), Dm.named("lmb_text", I18n.T("Take Plasma Tank")), Dm.named("rmb_text", I18n.T("Take Oxygen Tank"))));
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/structures/tank_dispenser.dm:25
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        if (oxygentanks >= 1 && oxygentanks <= 3)
            Dm.append(ref dot, Dm.text("oxygen-[]", oxygentanks));
        else if (oxygentanks >= 4 && oxygentanks <= TANK_DISPENSER_CAPACITY)
            Dm.append(ref dot, "oxygen-4");
        if (plasmatanks >= 1 && plasmatanks <= 4)
            Dm.append(ref dot, Dm.text("plasma-[]", plasmatanks));
        else if (plasmatanks >= 5 && plasmatanks <= TANK_DISPENSER_CAPACITY)
            Dm.append(ref dot, "plasma-5");
        return dot;
    }

    // tg: code/game/objects/structures/tank_dispenser.dm:38
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.attack_hand(user, modifiers, __extra);
        if (!Dm.truthy(plasmatanks))
        {
            balloon_alert(user, I18n.T("no plasma tanks!"));
            return dot;
        }
        await dispense(typeof(obj_item_tank_internals_plasma), user);
        plasmatanks--;
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/structures/tank_dispenser.dm:47
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        await base.attack_hand_secondary(user, modifiers);
        if (!Dm.truthy(oxygentanks))
        {
            balloon_alert(user, I18n.T("no oxygen tanks!"));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        await dispense(typeof(obj_item_tank_internals_oxygen), user);
        oxygentanks--;
        update_appearance();
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/objects/structures/tank_dispenser.dm:57
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/tank_dispenser.dm:62
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_tank_internals_plasma)
        {
            if (plasmatanks == TANK_DISPENSER_CAPACITY)
            {
                balloon_alert(user, I18n.T("it is full!"));
                return ITEM_INTERACT_BLOCKING;
            }
            plasmatanks++;
        }
        else if (tool is obj_item_tank_internals_oxygen)
        {
            if (oxygentanks == TANK_DISPENSER_CAPACITY)
            {
                balloon_alert(user, I18n.T("it is full!"));
                return ITEM_INTERACT_BLOCKING;
            }
            oxygentanks++;
        }
        else
        {
            if (!Dm.truthy(user.combat_mode) || Dm.truthy(Dm.bitand(tool.item_flags, NOBLUDGEON)))
            {
                balloon_alert(user, I18n.T("can't insert!"));
                return ITEM_INTERACT_BLOCKING;
            }
            return NONE;
        }

        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;

        balloon_alert(user, I18n.T("tank inserted"));
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/tank_dispenser.dm:86  (M5 slice 7)
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        foreach (object X in Dm.each(contents.Copy()))
        {
            obj_item I = X as obj_item;
            await I.forceMove(loc);
        }
        Dm.@new<obj_item_stack_sheet_iron>(loc, 2);
    }

    // tg: code/game/objects/structures/tank_dispenser.dm:92
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        if (Dm.truthy(plasmatanks) && Dm.truthy(oxygentanks))
            Dm.append(ref dot, span_notice(I18n.T("It has <b>[]</b> plasma tank\\s and <b>[]</b> oxygen tank\\s left.", plasmatanks, oxygentanks)));
        else if (Dm.truthy(plasmatanks) || Dm.truthy(oxygentanks))
            Dm.append(ref dot, span_notice(I18n.T("It has <b>[] tank\\s left.", Dm.truthy(plasmatanks) ? Dm.text("[]</b> plasma", plasmatanks) : Dm.text("[]</b> oxygen", oxygentanks))));
        return dot;
    }

    // tg: code/game/objects/structures/tank_dispenser.dm:99
    public virtual async DmTask dispense(object tank_type = null, mob receiver = null)
    {
        object existing_tank = Dm.locate_type(tank_type as Type, contents);
        if (existing_tank == null)
            existing_tank = Dm.@new(tank_type as Type);
        await receiver.put_in_hands(existing_tank as obj_item);
        balloon_alert(receiver, I18n.T("tank received"));
    }
}
