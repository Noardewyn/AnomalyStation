// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/gloves/insulated.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the insulated gloves (yellow), the budget ones (fyellow) and the worn out ones (fyellow/old) translated (the M7
// shocks slice: electrocute_mob reads the gloves' siemens_coefficient; the gloves item of the engineering tools, D164, is a
// later slice). Their fantasy bonuses and the stealing objective are generated live-partial stubs (M10); the fishing
// difficulty element is a deferred leaf (fishing, M10+). Gaps CL1 (D333): the spray-on gloves and their can (their charges spent
// by a prevented shock or a cleaning), the cut gloves. The var blocks are generated.

// tg: code/modules/clothing/gloves/insulated.dm:7
public partial class obj_item_clothing_gloves_color_yellow : obj_item_clothing_gloves_color
{
    // tg: code/modules/clothing/gloves/insulated.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        // AddElement(/datum/element/adjust_fishing_difficulty, 10): fishing (M10+)
        Dm.Deferred("code/modules/clothing/gloves/insulated.dm:21", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, 10): fishing (M10+)");
        return dot;
    }
}

// tg: code/modules/clothing/gloves/insulated.dm:43
public partial class obj_item_toy_sprayoncan : obj_item_toy
{
    // tg: code/modules/clothing/gloves/insulated.dm:49
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!iscarbon(interacting_with))
            return NONE;
        mob_living_carbon C = (mob_living_carbon)interacting_with;
        mob_living_carbon U = user as mob_living_carbon;
        object success = await C.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_gloves_color_yellow_sprayon>(), ITEM_SLOT_GLOVES, qdel_on_fail: TRUE, disable_warning: TRUE);
        if (Dm.truthy(success))
        {
            if (C == user)
                C.visible_message(span_notice(I18n.T("[] sprays their hands with glittery rubber!", U)));
            else
                C.visible_message(span_warning(I18n.T("[] sprays glittery rubber on the hands of []!", U, C)));
        }
        else
            C.visible_message(span_warning(I18n.T("The rubber fails to stick to []'s hands!", C)));
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/clothing/gloves/insulated.dm:64
public partial class obj_item_clothing_gloves_color_yellow_sprayon : obj_item_clothing_gloves_color_yellow
{
    // tg: code/modules/clothing/gloves/insulated.dm:75
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NODROP, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/clothing/gloves/insulated.dm:79
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        RegisterSignal(user, COMSIG_LIVING_SHOCK_PREVENTED, nameof(use_charge));
        RegisterSignal(this, COMSIG_COMPONENT_CLEAN_ACT, nameof(use_charge));
    }

    // tg: code/modules/clothing/gloves/insulated.dm:84
    [SignalHandler]
    public virtual object use_charge()
    {
        object dot = NONE;

        charges_remaining--;
        if (charges_remaining <= 0)
        {
            turf location = get_turf(this) as turf;
            location.visible_message(span_warning(I18n.T("[] crumble[] away into nothing.", this, p_s()))); // just like my dreams after working with .dm
            qdel(this);
        }

        dot = Dm.bitor(dot, COMPONENT_CLEANED);
        return dot;
    }
}

// tg: code/modules/clothing/gloves/insulated.dm:97
public partial class obj_item_clothing_gloves_color_fyellow : obj_item_clothing_gloves_color //Cheap Chinese Crap
{
    // tg: code/modules/clothing/gloves/insulated.dm:111
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        siemens_coefficient = Dm.num(pick(0, 0.5f, 0.5f, 0.5f, 0.5f, 0.75f, 1.5f));
        return dot;
    }

    // tg: code/modules/clothing/gloves/insulated.dm:115
    public override object examine_tags(mob user = null)
    {
        DmList dot = null;
        dot = base.examine_tags(user) as DmList;
        // Pretend we're always insulated
        if (Dm.truthy(dot["partially insulated"]))
            Dm.remove(ref dot, "partially insulated");
        dot["insulated"] = "It is made from a robust electrical insulator and will block any electricity passing through it!";
        return dot;
    }
}

// tg: code/modules/clothing/gloves/insulated.dm:122
public partial class obj_item_clothing_gloves_color_fyellow_old : obj_item_clothing_gloves_color_fyellow
{
    // tg: code/modules/clothing/gloves/insulated.dm:126
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        siemens_coefficient = Dm.num(pick(0, 0, 0, 0.5f, 0.5f, 0.5f, 0.75f));
        return dot;
    }
}

// tg: code/modules/clothing/gloves/insulated.dm:130
public partial class obj_item_clothing_gloves_cut : obj_item_clothing_gloves
{
    // tg: code/modules/clothing/gloves/insulated.dm:138
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/gloves/insulated.dm:140", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -5): fishing (M10+)");
        return dot;
    }
}
