// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/recipes/medicine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P5): the medicine reactions' own procs (the oculine flash, the inacusiate bang, the albuterol smoke, the
// ephedrine blast, the mannitol and neurine attacks, the antihol smoke, the sutures, meshes and poultices); the var-only medicine
// reactions are live under a live parent. The container's chat icon is a comment (the chat icons, section 76).

// tg: code/modules/reagents/chemistry/recipes/medicine.dm:29
public partial class datum_chemical_reaction_medicine_oculine : datum_chemical_reaction_medicine
{
    // tg: code/modules/reagents/chemistry/recipes/medicine.dm:48
    public override async DmTask<object> overheated(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null, params object[] __extra)
    {
        object dot = await base.overheated(holder, equilibrium, step_volume_added, __extra);
        await explode_flash(holder, equilibrium, round(equilibrium.reacted_vol / 10), 10);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/recipes/medicine.dm:52
    public override async DmTask<object> overly_impure(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null)
    {
        object dot = await base.overly_impure(holder, equilibrium, step_volume_added);
        await explode_flash(holder, equilibrium, 3, 30);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/recipes/medicine.dm:57
public partial class datum_chemical_reaction_medicine_inacusiate : datum_chemical_reaction_medicine
{
    // tg: code/modules/reagents/chemistry/recipes/medicine.dm:77
    public override async DmTask<object> overheated(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null, params object[] __extra)
    {
        // [icon2html(holder.my_atom, viewers(DEFAULT_MESSAGE_RANGE, src))]: the container's picture in the chat line (the chat icons)
        holder.my_atom.audible_message(span_notice(Dm.text("The [] suddenly gives out a loud bang!", holder.my_atom)));
        await explode_deafen(holder, equilibrium, 0.5f, 10, 3);
        return null;
    }

    // tg: code/modules/reagents/chemistry/recipes/medicine.dm:81
    public override async DmTask<object> overly_impure(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null)
    {
        float power = equilibrium.reacted_vol / 10;
        holder.my_atom.audible_message(span_notice(Dm.text("The [] suddenly gives out an ear-crushingly loud bang!", holder.my_atom)));
        await explode_deafen(holder, equilibrium, power / 2, power * 2, Dm.max(power / 2, 3));
        await clear_products(holder);
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/medicine.dm:191
public partial class datum_chemical_reaction_medicine_albuterol_to_convermol : datum_chemical_reaction_medicine
{
    // tg: code/modules/reagents/chemistry/recipes/medicine.dm:202
    [DmParams("holder", "equilibrium", "step_volume_added", "impure")]
    public override async DmTask<object> overheated(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null, params object[] __extra)
    {
        object impure = Dm.arg(__extra, 0) ?? FALSE;
        float bonus = Dm.truthy(impure) ? 2 : 1;
        await explode_smoke(holder, equilibrium, 7.5f * bonus, TRUE, TRUE);
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/medicine.dm:206
public partial class datum_chemical_reaction_medicine_ephedrine : datum_chemical_reaction_medicine
{
    // tg: code/modules/reagents/chemistry/recipes/medicine.dm:225
    public override async DmTask<object> overheated(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null, params object[] __extra)
    {
        await reagent_explode(holder, equilibrium.reacted_vol, 0, 25);
        return null;
    }

    // tg: code/modules/reagents/chemistry/recipes/medicine.dm:228
    public override async DmTask<object> overly_impure(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null)
    {
        await reagent_explode(holder, equilibrium.reacted_vol, 0, 20);
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/medicine.dm:267
public partial class datum_chemical_reaction_medicine_mannitol : datum_chemical_reaction_medicine
{
    // tg: code/modules/reagents/chemistry/recipes/medicine.dm:286
    public override async DmTask<object> overheated(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null, params object[] __extra)
    {
        if (Dm.truthy(off_cooldown(holder, equilibrium, 10, "mannitol")))
        {
            await explode_attack_chem(holder, equilibrium, typeof(datum_reagent_impurity_mannitol), 5);
            await explode_invert_smoke(holder, equilibrium);
        }
        return null;
    }

    // tg: code/modules/reagents/chemistry/recipes/medicine.dm:291
    public override async DmTask<object> overly_impure(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null)
    {
        await overheated(holder, equilibrium, step_volume_added);
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/medicine.dm:294
public partial class datum_chemical_reaction_medicine_neurine : datum_chemical_reaction_medicine
{
    // tg: code/modules/reagents/chemistry/recipes/medicine.dm:312
    public override async DmTask<object> overheated(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null, params object[] __extra)
    {
        if (Dm.truthy(off_cooldown(holder, equilibrium, 10, "neurine")))
        {
            await explode_invert_smoke(holder, equilibrium, clear_products: FALSE, clear_reactants: FALSE);
            await explode_attack_chem(holder, equilibrium, typeof(datum_reagent_inverse_neurine), 10);
            await clear_products(holder, 5);
        }
        return null;
    }

    // tg: code/modules/reagents/chemistry/recipes/medicine.dm:318
    public override async DmTask<object> overly_impure(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null)
    {
        await overheated(holder, equilibrium, step_volume_added);
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/medicine.dm:326
public partial class datum_chemical_reaction_medicine_antihol : datum_chemical_reaction_medicine
{
    // tg: code/modules/reagents/chemistry/recipes/medicine.dm:345
    public override async DmTask<object> overheated(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null, params object[] __extra)
    {
        await explode_smoke(holder, equilibrium);
        return null;
    }

    // tg: code/modules/reagents/chemistry/recipes/medicine.dm:348
    public override async DmTask<object> overly_impure(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null)
    {
        await explode_smoke(holder, equilibrium);
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/medicine.dm:402
public partial class datum_chemical_reaction_medicine_medsuture : datum_chemical_reaction_medicine
{
    // tg: code/modules/reagents/chemistry/recipes/medicine.dm:407
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_stack_medical_suture_medicated>(get_turf(holder.my_atom), round(Dm.num(created_volume) * 4));
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/medicine.dm:410
public partial class datum_chemical_reaction_medicine_medmesh : datum_chemical_reaction_medicine
{
    // tg: code/modules/reagents/chemistry/recipes/medicine.dm:415
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_stack_medical_mesh_advanced>(get_turf(holder.my_atom), round(Dm.num(created_volume) * 3));
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/medicine.dm:418
public partial class datum_chemical_reaction_medicine_poultice : datum_chemical_reaction_medicine
{
    // tg: code/modules/reagents/chemistry/recipes/medicine.dm:423
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        Dm.@new<obj_item_stack_medical_poultice>(get_turf(holder.my_atom), round(Dm.num(created_volume) * 3));
        await DmTask.CompletedTask;
        return null;
    }
}
