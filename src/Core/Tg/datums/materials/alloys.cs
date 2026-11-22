// Translated from tgstation (snapshot 1d99bd7f): code/datums/materials/alloys.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 2 (materials and stacks): the whole file. The var blocks are generated (D111). The alien alloy's regenerating objects
// (/datum/element/obj_regen) came with gaps SL1 (D333).

/// Materials made from other materials.
// tg: code/datums/materials/alloys.dm:2
public partial class datum_material_alloy : datum_material
{
    // tg: code/datums/materials/alloys.dm:9
    public override async DmTask<object> return_composition(float? amount = null, object flags = null)
    {
        amount ??= 1;
        if (Dm.truthy(Dm.bitand(flags, MATCONTAINER_ACCEPT_ALLOYS)))
            return await base.return_composition(amount, flags);

        DmList dot = Dm.list();

        DmList cached_comp = composition;
        foreach (object comp_mat in Dm.each(cached_comp))
        {
            datum_material component_material = await SSmaterials.get_material(comp_mat) as datum_material;
            DmList component_composition = await component_material.return_composition(Dm.num(cached_comp[comp_mat]), flags) as DmList;
            foreach (object comp_comp_mat in Dm.each(component_composition))
                Dm.append_at(dot, comp_comp_mat, Dm.num(component_composition[comp_comp_mat]) * Dm.num(amount));
        }
        return dot;
    }
}

/**
 * Plasteel
 * An alloy of iron and plasma.
 * Applies a significant slowdown effect to any and all items that contain it.
 */
// tg: code/datums/materials/alloys.dm:27
public partial class datum_material_alloy_plasteel : datum_material_alloy
{
    // tg: code/datums/materials/alloys.dm:47
    public override async DmTask on_applied(atom source = null, object mat_amount = null, object multiplier = null, object from_slot = null)
    {
        await base.on_applied(source, mat_amount, multiplier, from_slot);
        if (istype(source, typeof(obj_item_fishing_rod)))
            ADD_TRAIT(source, TRAIT_ROD_LAVA_USABLE, REF(this));
    }

    // tg: code/datums/materials/alloys.dm:52
    public override void on_removed(atom source = null, object mat_amount = null, object material_flags = null, object from_slot = null)
    {
        base.on_removed(source, mat_amount, material_flags, from_slot);
        if (istype(source, typeof(obj_item_fishing_rod)))
            REMOVE_TRAIT(source, TRAIT_ROD_LAVA_USABLE, REF(this));
    }
}

/**
 * Plastitanium
 * An alloy of titanium and plasma.
 */
// tg: code/datums/materials/alloys.dm:61
public partial class datum_material_alloy_plastitanium : datum_material_alloy
{
    // tg: code/datums/materials/alloys.dm:81
    public override async DmTask on_applied(atom source = null, object mat_amount = null, object multiplier = null, object from_slot = null)
    {
        await base.on_applied(source, mat_amount, multiplier, from_slot);
        if (istype(source, typeof(obj_item_fishing_rod)))
            ADD_TRAIT(source, TRAIT_ROD_LAVA_USABLE, REF(this));
    }

    // tg: code/datums/materials/alloys.dm:86
    public override void on_removed(atom source = null, object mat_amount = null, object material_flags = null, object from_slot = null)
    {
        base.on_removed(source, mat_amount, material_flags, from_slot);
        if (istype(source, typeof(obj_item_fishing_rod)))
            REMOVE_TRAIT(source, TRAIT_ROD_LAVA_USABLE, REF(this));
    }
}

/**
 * Plasmaglass
 * An alloy of silicate and plasma.
 */
// tg: code/datums/materials/alloys.dm:95
public partial class datum_material_alloy_plasmaglass : datum_material_alloy
{
}

/**
 * Titaniumglass
 *
 * An alloy of glass and titanium.
 */
// tg: code/datums/materials/alloys.dm:122
public partial class datum_material_alloy_titaniumglass : datum_material_alloy
{
}

/**
 * Plastitanium Glass
 *
 * An alloy of plastitanium and glass.
 */
// tg: code/datums/materials/alloys.dm:149
public partial class datum_material_alloy_plastitaniumglass : datum_material_alloy
{
}

/**
 * Alien Alloy
 *
 * Densified plasteel.
 * Applies a significant slowdown effect to anything that contains it.
 * Anything constructed from it can slowly regenerate.
 */
// tg: code/datums/materials/alloys.dm:178
public partial class datum_material_alloy_alien : datum_material_alloy
{
    // tg: code/datums/materials/alloys.dm:197
    public override async DmTask on_applied(atom source = null, object mat_amount = null, object multiplier = null, object from_slot = null)
    {
        await base.on_applied(source, mat_amount, multiplier, from_slot);
        if (isobj(source))
            await source.AddElement(typeof(datum_element_obj_regen), Dm.named("_rate", 0.02f)); // 2% regen per tick.
        if (istype(source, typeof(obj_item_fishing_rod)))
            ADD_TRAIT(source, TRAIT_ROD_LAVA_USABLE, REF(this));
    }

    // tg: code/datums/materials/alloys.dm:204
    public override void on_removed(atom source = null, object mat_amount = null, object material_flags = null, object from_slot = null)
    {
        base.on_removed(source, mat_amount, material_flags, from_slot);
        if (isobj(source))
            source.RemoveElement(typeof(datum_element_obj_regen), Dm.named("_rate", 0.02f));
        if (istype(source, typeof(obj_item_fishing_rod)))
            REMOVE_TRAIT(source, TRAIT_ROD_LAVA_USABLE, REF(this));
    }
}
