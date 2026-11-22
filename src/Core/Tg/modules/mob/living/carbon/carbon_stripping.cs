// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/carbon_stripping.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8z slice B (D234-D238): the whole file. The var blocks are generated.

// tg: code/modules/mob/living/carbon/carbon_stripping.dm:5
public partial class datum_strippable_item_mob_item_slot_back : datum_strippable_item_mob_item_slot
{
    // tg: code/modules/mob/living/carbon/carbon_stripping.dm:9
    public override object get_alternate_actions(atom source = null, mob user = null, obj_item item = null)
    {
        DmList dot = base.get_alternate_actions(source, user, item) as DmList;
        Dm.append(ref dot, get_strippable_alternate_action_internals(item, source));
        return dot;
    }

    // tg: code/modules/mob/living/carbon/carbon_stripping.dm:13
    public override async DmTask<object> perform_alternate_action(atom source = null, mob user = null, object action_key = null, obj_item item = null)
    {
        if (!Dm.truthy(await base.perform_alternate_action(source, user, action_key, item)))
            return null;
        if (Dm.@in(action_key, get_strippable_alternate_action_internals(item, source)))
            await strippable_alternate_action_internals(item, source, user);
        return null;
    }
}

// tg: code/modules/mob/living/carbon/carbon_stripping.dm:27
public partial class datum_strippable_item_mob_item_slot_handcuffs : datum_strippable_item_mob_item_slot
{
    // tg: code/modules/mob/living/carbon/carbon_stripping.dm:31
    public override object should_show(atom source = null, mob user = null)
    {
        if (!iscarbon(source))
            return FALSE;

        mob_living_carbon carbon_source = source as mob_living_carbon;
        return carbon_source.handcuffed != null ? TRUE : FALSE;
    }

    // You shouldn't be able to equip things to handcuff slots.
    // tg: code/modules/mob/living/carbon/carbon_stripping.dm:39
    public override async DmTask<object> try_equip(atom source = null, obj_item equipping = null, mob user = null)
    {
        return FALSE;
    }
}

// tg: code/modules/mob/living/carbon/carbon_stripping.dm:42
public partial class datum_strippable_item_mob_item_slot_legcuffs : datum_strippable_item_mob_item_slot
{
    // tg: code/modules/mob/living/carbon/carbon_stripping.dm:46
    public override object should_show(atom source = null, mob user = null)
    {
        if (!iscarbon(source))
            return FALSE;

        mob_living_carbon carbon_source = source as mob_living_carbon;
        return carbon_source.legcuffed != null ? TRUE : FALSE;
    }

    // You shouldn't be able to equip things to legcuff slots.
    // tg: code/modules/mob/living/carbon/carbon_stripping.dm:54
    public override async DmTask<object> try_equip(atom source = null, obj_item equipping = null, mob user = null)
    {
        return FALSE;
    }
}

/// A strippable item for a hand
// tg: code/modules/mob/living/carbon/carbon_stripping.dm:57
public partial class datum_strippable_item_hand : datum_strippable_item
{
    // tg: code/modules/mob/living/carbon/carbon_stripping.dm:65
    public override object get_item(atom source = null)
    {
        if (!ismob(source))
            return null;

        mob mob_source = source as mob;
        return mob_source.get_item_for_held_index(hand_index);
    }

    // tg: code/modules/mob/living/carbon/carbon_stripping.dm:72
    public override async DmTask<object> try_equip(atom source = null, obj_item equipping = null, mob user = null)
    {
        object dot = await base.try_equip(source, equipping, user);
        if (!Dm.truthy(dot))
            return FALSE;

        if (!ismob(source))
            return FALSE;

        mob mob_source = source as mob;

        if (!Dm.truthy(mob_source.can_put_in_hand(equipping, hand_index)))
        {
            to_chat(this, span_warning(I18n.T("\\The [] doesn't fit in that place!", equipping))); // tg writes src (this datum) here, not the user
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/carbon_stripping.dm:88
    public override async DmTask<object> start_equip(atom source = null, obj_item equipping = null, mob user = null)
    {
        object dot = await base.start_equip(source, equipping, user);
        if (!Dm.truthy(dot))
            return dot;

        if (!ismob(source))
            return FALSE;

        mob mob_source = source as mob;

        if (!Dm.truthy(await do_after(user, equipping.equip_delay_other, source)))
            return FALSE;

        if (!Dm.truthy(mob_source.can_put_in_hand(equipping, hand_index)))
            return FALSE;

        if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(equipping)))
            return FALSE;

        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/carbon_stripping.dm:109
    public override object finish_equip(atom source = null, obj_item equipping = null, mob user = null)
    {
        mob mob_source = source as mob;
        Waiting.NoSleep(mob_source.put_in_hand(equipping, hand_index));
        return null;
    }

    // tg: code/modules/mob/living/carbon/carbon_stripping.dm:113
    public override async DmTask<object> start_unequip(atom source = null, mob user = null)
    {
        object dot = await base.start_unequip(source, user);
        if (!Dm.truthy(dot))
            return dot;

        return await start_unequip_mob(get_item(source) as obj_item, source as mob, user);
    }

    // tg: code/modules/mob/living/carbon/carbon_stripping.dm:120
    public override async DmTask<object> finish_unequip(atom source = null, mob user = null)
    {
        obj_item item = get_item(source) as obj_item;
        if (item == null)
            return FALSE;

        if (!ismob(source))
            return FALSE;

        return await finish_unequip_mob(item, source as mob, user);
    }
}
