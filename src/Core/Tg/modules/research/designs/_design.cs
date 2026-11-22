// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/designs/_design.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the design datum's procs, which the biogenerator's printing needs (its designs are var-only subtypes). Silicons SI3
// (D326): SSresearch makes every design, and Destroy takes it out of its table. Gaps RD1 (D333): builds_inert_item, the fabricators'
// shared DV384 check.

// tg: code/modules/research/designs/_design.dm:21
public partial class datum_design : datum
{
    // tg: code/modules/research/designs/_design.dm:90
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        float base_path_len = length("/datum/design/");
        asset_id = replacetext(splicetext(Dm.text("[]", type), 1, base_path_len + 1), "/", "_");

        DmList temp_list = Dm.list();
        foreach (object mat_type in Dm.each(materials))
        {
            object amount = materials[mat_type];
            if (ispath(mat_type, typeof(datum_material_requirement)) || ispath(mat_type, typeof(datum_material_slot)))
            {
                temp_list[mat_type] = amount;
                continue;
            }

            datum_material mat = await SSmaterials.get_material(mat_type) as datum_material;
            temp_list[mat] = amount;
        }

        materials = temp_list;

        foreach (object @object in Dm.each(transfered_materials))
        {
            DmList mat_list = transfered_materials[@object] as DmList;
            temp_list = Dm.list();
            foreach (object mat_type in Dm.each(mat_list))
            {
                datum_material mat = await SSmaterials.get_material(mat_type) as datum_material;
                temp_list[mat] = mat_list[mat_type];
            }
            transfered_materials[@object] = temp_list;
        }
    }

    // tg: code/modules/research/designs/_design.dm:116
    public override object Destroy(float? force = null)
    {
        if (SSresearch != null) // a host-built room without the Master has no SSresearch (rules 10.6)
            Dm.remove(ref SSresearch.techweb_designs, type);
        return base.Destroy(force);
    }

    // tg: code/modules/research/designs/_design.dm:121
    public virtual object get_description()
    {
        Type object_build_item_path = build_path;

        return desc == null ? Dm.initial(object_build_item_path, (atom a) => a.desc) : desc;
    }

    // tg: code/modules/research/designs/_design.dm:127
    public virtual async DmTask<object> create_result(atom drop_loc = null, DmList custom_materials = null, object amount = null)
    {
        if (!ispath(build_path, typeof(obj_item_stack)) && Dm.num(amount) > 1)
            throw Dm.CRASH(Dm.text("[] create_result was passed an amount higher than 1, despite not being a stack design!", this), "code/modules/research/designs/_design.dm", 129);

        if (!ispath(build_path, typeof(obj_item_stack)))
            return Dm.@new(build_path, drop_loc);

        if (amount == null)
            amount = 1;
        return Dm.@new(build_path, drop_loc, amount);
    }

    // tg: code/modules/research/designs/_design.dm:139
    public virtual async DmTask transfer_materials(DmList custom_materials = null, object multiplier = null, atom target_object = null)
    {
        if (!islist(custom_materials))
            throw Dm.CRASH("assertion failed: islist(custom_materials), message: design/transfer_materials() called with invalid 'custom_materials' arg value", "code/modules/research/designs/_design.dm", 142);
        if (!Dm.truthy(multiplier))
            throw Dm.CRASH("assertion failed: multiplier, message: design/transfer_materials() called with invalid 'multiplier' arg value", "code/modules/research/designs/_design.dm", 143);
        if (!isatom(target_object))
            throw Dm.CRASH("assertion failed: isatom(target_object), message: design/transfer_materials() called with invalid 'target_object' arg value", "code/modules/research/designs/_design.dm", 144);

        if (!Dm.truthy(length(transfered_materials))) //most common case where the object is just one thing and 'transferred_materials' is null
        {
            await simple_transfer_materials(custom_materials, multiplier, target_object);
            return;
        }

        DmList recursive_contents = target_object.get_all_contents_type(typeof(obj_item)) as DmList;

        foreach (obj_item @object in Dm.each_cast<obj_item>(recursive_contents))
        {
            if (Dm.truthy(Dm.bitand(@object.item_flags, ABSTRACT))) //skip abstract entities
                continue;
            if (!Dm.@in(@object.type, transfered_materials))
            {
                stack_trace(Dm.text("[] missing from the 'transfered_materials' list of the design. Edit the 'transfered_materials' var of [], or give it the ABSTRACT item flag if appropriate.", @object.type, type), "code/modules/research/designs/_design.dm", 156);
                continue;
            }
            await simple_transfer_materials(transfered_materials[@object.type] as DmList, multiplier, @object);
        }
    }

    ///Called by [proc/transfer_materials] in two places and it's basically the meat and bone of the function. Having it as a separate proc reduces copypaste a little.
    // tg: code/modules/research/designs/_design.dm:161
    public virtual async DmTask simple_transfer_materials(DmList custom_materials = null, object multiplier = null, atom target_object = null)
    {
        if (isstack(target_object))
        {
            obj_item_stack stack = target_object as obj_item_stack;
            stack.mats_per_unit = await SSmaterials.get_material_set_cache(custom_materials, Dm.num(multiplier) / Dm.num(stack.amount)) as DmList;
            await stack.update_custom_materials();
        }
        else
        {
            await target_object.set_custom_materials(custom_materials, Dm.num(multiplier));
        }
    }

    // Not tg's (rules 2.5): the fabricators' shared check for DV384 (D326). The exosuit fabricator, the protolathes, circuit imprinters
    // and techfabs and the autolathe do not offer a design whose built item is inert: its type waits for procs of its own (a prop),
    // and printing it would hand the player a thing that does nothing. tg offers every design its techweb has researched.
    /// DEVIATION(DV384): whether this design builds an inert item (a design with no build path, a reagent's, builds none).
    public bool builds_inert_item()
    {
        return build_path != null && Attribute.IsDefined(build_path, typeof(DmInertAttribute), inherit: false);
    }
}
