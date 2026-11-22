// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/mushrooms.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/mushrooms.dm:193
public partial class obj_item_food_grown_mushroom_chanterelle : obj_item_food_grown_mushroom
{
    // tg: code/modules/hydroponics/grown/mushrooms.dm:198
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_kitchen_spoon)
            return NONE;

        obj_item_seeds our_seed = seed as obj_item_seeds;
        if (Dm.num(our_seed.potency) < 95)
            return ITEM_INTERACT_BLOCKING;

        to_chat(user, span_notice(I18n.T("You hollow out the chanterelle with [].", tool)));
        await remove_item_from_storage(user);
        if (Dm.truthy(Dm.bitand(our_seed.resistance_flags, FIRE_PROOF)))
            await user.put_in_hands(Dm.@new<obj_item_clothing_head_wizard_chanterelle_fireproof>());
        else
            await user.put_in_hands(Dm.@new<obj_item_clothing_head_wizard_chanterelle>());
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/hydroponics/grown/mushrooms.dm:268
public partial class obj_item_food_grown_mushroom_glowshroom : obj_item_food_grown_mushroom
{
    // tg: code/modules/hydroponics/grown/mushrooms.dm:275
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (isspaceturf(user.loc))
            return FALSE;
        if (!isturf(user.loc))
        {
            to_chat(user, span_warning(I18n.T("You need more space to plant [].", this)));
            return FALSE;
        }
        float count = 0;
        float maxcount = 1;
        foreach (object tempdir in Dm.each(GLOB.cardinals))
        {
            if (get_step(user.loc, Dm.num(tempdir)) is turf_closed)
                maxcount++;
        }
        foreach (obj_structure_glowshroom G in Dm.each<obj_structure_glowshroom>(user.loc))
            count++;
        if (count >= maxcount)
        {
            to_chat(user, span_warning(I18n.T("There are too many shrooms here to plant [].", this)));
            return FALSE;
        }
        Dm.@new(effect_path, user.loc, seed);
        to_chat(user, span_notice(I18n.T("You plant [].", this)));
        seed = null; // We pass our seed to our planted shroom, null it here
        qdel(this);
        return TRUE;
    }
}

// tg: code/modules/hydroponics/grown/mushrooms.dm:344
public partial class obj_item_food_grown_mushroom_glowshroom_shadowshroom : obj_item_food_grown_mushroom_glowshroom
{
    // tg: code/modules/hydroponics/grown/mushrooms.dm:352
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot))
            investigate_log(Dm.text("was planted by [] at []", key_name(user), AREACOORD(user)), INVESTIGATE_BOTANY);
        return dot;
    }
}
