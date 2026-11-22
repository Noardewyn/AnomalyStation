// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/recipes/drugs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P5): the drug reactions' own procs (the methamphetamine heat and explosions, the meth crystals, moon rocks,
// blastoff ampoules and Saturn-X globs); the var-only ones are live under a live parent. The admin messages and game logs are
// comments (logs, M10+).

// tg: code/modules/reagents/chemistry/recipes/drugs.dm:15
public partial class datum_chemical_reaction_methamphetamine : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/drugs.dm:34
    public override async DmTask<object> reaction_step(datum_reagents holder = null, datum_equilibrium reaction = null, object delta_t = null, object delta_ph = null, object step_reaction_vol = null)
    {
        datum_reagent meth = holder.has_reagent(typeof(datum_reagent_drug_methamphetamine)) as datum_reagent;
        if (meth == null) //First step
        {
            reaction.thermic_mod = (1 - Dm.num(delta_ph)) * 5;
            return null;
        }
        reaction.thermic_mod = (1 - meth.purity) * 5;
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/modules/reagents/chemistry/recipes/drugs.dm:41
    public override async DmTask<object> overheated(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null, params object[] __extra)
    {
        object dot = await base.overheated(holder, equilibrium, step_volume_added, __extra);
        await temp_meth_explosion(holder, equilibrium.reacted_vol);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/recipes/drugs.dm:45
    public override async DmTask<object> overly_impure(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null)
    {
        object dot = await base.overly_impure(holder, equilibrium, step_volume_added);
        await temp_meth_explosion(holder, equilibrium.reacted_vol);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/recipes/drugs.dm:49
    public override async DmTask<object> reaction_finish(datum_reagents holder = null, datum_equilibrium reaction = null, object react_vol = null)
    {
        datum_reagent meth = holder.has_reagent(typeof(datum_reagent_drug_methamphetamine)) as datum_reagent;
        if (meth == null) //Other procs before this can already blow us up
            return await base.reaction_finish(holder, reaction, react_vol);
        if (meth.purity < Dm.num(purity_min))
        {
            await temp_meth_explosion(holder, react_vol);
            return null;
        }
        return await base.reaction_finish(holder, reaction, react_vol);
    }

    // tg: code/modules/reagents/chemistry/recipes/drugs.dm:59
    public virtual async DmTask temp_meth_explosion(datum_reagents holder = null, object explode_vol = null)
    {
        float power = 5 + round(Dm.num(explode_vol) / 12, 1); //meth strengthdiv is 12
        if (power <= 0)
            return;
        turf T = get_turf(holder.my_atom);
        if (ismob(holder.my_atom))
        {
            mob M = (mob)holder.my_atom;
            string inside_msg = Dm.text(" inside []", ADMIN_LOOKUPFLW(M));
            return;
        }
        object lastkey = holder.my_atom.fingerprintslast;
        string touch_msg = "N/A";
        if (Dm.truthy(lastkey))
        {
            mob toucher = get_mob_by_key(lastkey) as mob;
            touch_msg = Dm.text("[]", ADMIN_LOOKUPFLW(toucher));
        }
        if (!istype(holder.my_atom, typeof(obj_machinery_plumbing))) //excludes standard plumbing equipment from spamming admins with this shit
            message_admins(Dm.text("Reagent explosion reaction occurred at []. Last Fingerprint: [].", ADMIN_VERBOSEJMP(T), touch_msg));
        log_game(Dm.text("Reagent explosion reaction occurred at []. Last Fingerprint: [].", AREACOORD(T), Dm.truthy(lastkey) ? lastkey : "N/A"));
        datum_effect_system_reagents_explosion expl = Dm.@new<datum_effect_system_reagents_explosion>(T, power);
        await expl.start(holder.my_atom);
        await holder.clear_reagents();
    }
}

// tg: code/modules/reagents/chemistry/recipes/drugs.dm:84
public partial class datum_chemical_reaction_meth_crystal : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/drugs.dm:82  #define METH_REQUIRED (10) (file-local)
    private const float METH_REQUIRED = 10;

    // tg: code/modules/reagents/chemistry/recipes/drugs.dm:91
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf location = get_turf(holder.my_atom);

        datum_reagent_consumable_ethanol dummy_reagent = holder.has_reagent(typeof(datum_reagent_consumable_ethanol)) as datum_reagent_consumable_ethanol;
        for (float i = 1, i_end = round(Dm.num(created_volume), CHEMICAL_VOLUME_ROUNDING); i <= i_end; i++)
        {
            obj_item_food_drug_meth_crystal new_crystal = Dm.@new<obj_item_food_drug_meth_crystal>(location);
            new_crystal.pixel_x = rand(-6, 6);
            new_crystal.pixel_y = rand(-6, 6);
            new_crystal.color = gradient("#FAFAFA", "#78C8FA", dummy_reagent.creation_purity);

            datum_reagents crystal_reagents = new_crystal.reagents as datum_reagents;
            await crystal_reagents.clear_reagents();
            await crystal_reagents.add_reagent(typeof(datum_reagent_drug_methamphetamine), METH_REQUIRED);
            float imp_amt = METH_REQUIRED * (1 - dummy_reagent.creation_purity) * 0.25f;
            if (imp_amt > 0)
                await crystal_reagents.add_reagent(typeof(datum_reagent_consumable_failed_reaction), imp_amt);
        }

        dummy_reagent.volume = 0f;
        await holder.update_total();
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/drugs.dm:168
public partial class datum_chemical_reaction_moon_rock : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/drugs.dm:174
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf location = get_turf(holder.my_atom);
        for (float i = 1; i <= Dm.num(created_volume); i++)
        {
            obj_item_food_drug_moon_rock new_rock = Dm.@new<obj_item_food_drug_moon_rock>(location);
            new_rock.pixel_x = rand(-6, 6);
            new_rock.pixel_y = rand(-6, 6);
        }
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/drugs.dm:184
public partial class datum_chemical_reaction_blastoff_ampoule : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/drugs.dm:190
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf location = get_turf(holder.my_atom);
        for (float iteration = 1; iteration <= Dm.num(created_volume); iteration++)
        {
            obj_item_reagent_containers_cup_blastoff_ampoule new_ampoule = Dm.@new<obj_item_reagent_containers_cup_blastoff_ampoule>(location);
            new_ampoule.pixel_x = rand(-6, 6);
            new_ampoule.pixel_y = rand(-6, 6);
        }
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/drugs.dm:197
public partial class datum_chemical_reaction_saturnx_glob : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/drugs.dm:203
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf location = get_turf(holder.my_atom);
        for (float iteration = 1; iteration <= Dm.num(created_volume); iteration++)
        {
            obj_item_food_drug_saturnx new_glob = Dm.@new<obj_item_food_drug_saturnx>(location);
            new_glob.pixel_x = rand(-6, 6);
            new_glob.pixel_y = rand(-6, 6);
        }
        await DmTask.CompletedTask;
        return null;
    }
}
