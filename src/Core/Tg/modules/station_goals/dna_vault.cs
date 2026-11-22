// Translated from tgstation (snapshot 1d99bd7f): code/modules/station_goals/dna_vault.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R1 (D327): the whole file: the DNA vault goal (how many animal, plant and human samples it asks for), the vault with its
// fillers and its DnaVault window, the vault's lottery of two of its mutations (vault_mutation.dm) for each crewman, the gene therapy
// that grants the chosen one once the vault is full, and the ark shuttle it unlocks. The VAULT_* defines (:6-12) are #undef'd unused.
// The var blocks are generated (D111).

//Crew has to create dna vault
// Cargo can order DNA samplers + DNA vault boards
// DNA vault requires x animals, y plants, z human dna
// DNA vaults require high tier stock parts
// After completion each crewmember can receive single upgrade chosen out of 2 for the mob.
// tg: code/modules/station_goals/dna_vault.dm:14
public partial class datum_station_goal_dna_vault : datum_station_goal
{
    // tg: code/modules/station_goals/dna_vault.dm:20
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        animal_count = rand(16, 20); //might be too few given ~15 roundstart stationside ones
        human_count = rand(round(0.75f * SSticker.totalPlayersReady), SSticker.totalPlayersReady); // 75%+ roundstart population.
        object non_standard_plants = non_standard_plants_count();
        plant_count = rand(round(0.15f * Dm.num(non_standard_plants)), round(0.3f * Dm.num(non_standard_plants)));
    }

    // tg: code/modules/station_goals/dna_vault.dm:27
    public virtual object non_standard_plants_count()
    {
        float dot = 0;
        foreach (object T in Dm.each(subtypesof(typeof(obj_item_seeds)))) //put a cache if it's used anywhere else
        {
            Type S = T as Type;
            if (Dm.initial(S, (obj_item_seeds p) => p.rarity) > 0)
                dot++;
        }
        return dot;
    }

    // tg: code/modules/station_goals/dna_vault.dm:34
    public override object get_report()
    {
        return Dm.list(
            I18n.T("Our long term prediction systems indicate a 99% chance of system-wide cataclysm in the near future."),
            I18n.T("We need you to construct a DNA Vault aboard your station."),
            "",
            I18n.T("The DNA Vault needs to contain samples of:"),
            I18n.T("* [] unique animal data", animal_count),
            I18n.T("* [] unique non-standard plant data", plant_count),
            I18n.T("* [] unique sapient humanoid DNA data", human_count),
            "",
            I18n.T("Base vault parts are available for shipping via cargo.")
        ).Join("\n");
    }

    // tg: code/modules/station_goals/dna_vault.dm:48
    public override void on_report()
    {
        var P = SSshuttle.supply_packs[typeof(datum_supply_pack_engineering_dna_vault)] as datum_supply_pack;
        P.order_flags = Dm.num(Dm.bitor(P.order_flags, ORDER_SPECIAL_ENABLED));

        P = SSshuttle.supply_packs[typeof(datum_supply_pack_engineering_dna_probes)] as datum_supply_pack;
        P.order_flags = Dm.num(Dm.bitor(P.order_flags, ORDER_SPECIAL_ENABLED));
    }

    // tg: code/modules/station_goals/dna_vault.dm:55
    public override object check_completion()
    {
        if (Dm.truthy(base.check_completion()))
            return TRUE;
        foreach (obj_machinery_dna_vault V in Dm.each_cast<obj_machinery_dna_vault>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_dna_vault))))
        {
            if (V.animal_dna.len >= Dm.num(animal_count) && V.plant_dna.len >= Dm.num(plant_count) && V.human_dna.len >= Dm.num(human_count))
                return TRUE;
        }
        return FALSE;
    }
}

// tg: code/modules/station_goals/dna_vault.dm:63
public partial class obj_machinery_dna_vault : obj_machinery
{
    // tg: code/modules/station_goals/dna_vault.dm:91
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        //TODO: Replace this,bsa and gravgen with some big machinery datum
        DmList occupied = Dm.list();
        foreach (object direct in Dm.each(Dm.list(EAST, WEST, SOUTHEAST, SOUTHWEST)))
            Dm.append(ref occupied, get_step(this, Dm.num(direct)));
        Dm.append(ref occupied, locate(x + 1, y - 2, z));
        Dm.append(ref occupied, locate(x - 1, y - 2, z));

        foreach (object T in Dm.each(occupied))
        {
            var F = Dm.@new<obj_structure_filler>(T);
            F.parent = this;
            Dm.append(ref fillers, F);
        }

        var dna_vault_goal = SSstation.get_station_goal(typeof(datum_station_goal_dna_vault)) as datum_station_goal_dna_vault;
        if (dna_vault_goal != null)
        {
            animals_max = Dm.num(dna_vault_goal.animal_count);
            plants_max = Dm.num(dna_vault_goal.plant_count);
            dna_max = Dm.num(dna_vault_goal.human_count);
        }

        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/station_goals/dna_vault.dm:112
    public override object Destroy(float? force = null)
    {
        foreach (obj_structure_filler filler in Dm.each_cast<obj_structure_filler>(fillers))
        {
            filler.parent = null;
            qdel(filler);
        }
        return base.Destroy(force);
    }

    // tg: code/modules/station_goals/dna_vault.dm:118
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            roll_powers(user);
            ui = Dm.@new<datum_tgui>(user, this, "DnaVault", name);
            await ui.open();
        }
        return null;
    }

    //Generate a unique set of mutation for each person
    // tg: code/modules/station_goals/dna_vault.dm:126
    public virtual void roll_powers(mob user = null)
    {
        object user_weakref = WEAKREF(user);
        if (Dm.@in(user_weakref, power_lottery) || isdead(user))
            return;
        possible_powers = Dm.list(
            typeof(datum_mutation_breathless),
            typeof(datum_mutation_dextrous),
            typeof(datum_mutation_quick),
            typeof(datum_mutation_fire_immunity),
            typeof(datum_mutation_plasmocile),
            typeof(datum_mutation_quick_recovery),
            typeof(datum_mutation_tough)
        );
        DmList gained_mutation = Dm.list();
        Dm.append(ref gained_mutation, pick_n_take(possible_powers));
        Dm.append(ref gained_mutation, pick_n_take(possible_powers));

        power_lottery[user_weakref] = gained_mutation;
    }

    // tg: code/modules/station_goals/dna_vault.dm:145
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["plants"] = plant_dna.len;
        data["plants_max"] = plants_max;
        data["animals"] = animal_dna.len;
        data["animals_max"] = animals_max;
        data["dna"] = human_dna.len;
        data["dna_max"] = dna_max;
        data["completed"] = completed;
        data["used"] = TRUE;
        data["choiceA"] = "";
        data["choiceB"] = "";
        if (user != null && Dm.truthy(completed))
        {
            DmList mutation_options = power_lottery[WEAKREF(user)] as DmList;
            if (Dm.truthy(Dm.length(mutation_options)))
            {
                Type mutation1 = mutation_options[1] as Type;
                Type mutation2 = mutation_options[2] as Type;
                data["used"] = FALSE;
                data["choiceA"] = Dm.initial(mutation1, (datum_mutation p) => p.name);
                data["choiceB"] = Dm.initial(mutation2, (datum_mutation p) => p.name);
            }
        }
        return data;
    }

    // tg: code/modules/station_goals/dna_vault.dm:167
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action as string)
        {
            case "gene":
                await upgrade(usr as mob_living_carbon_human, @params["choice"]);
                dot = TRUE;
                break;
        }
        return dot;
    }

    // tg: code/modules/station_goals/dna_vault.dm:177
    public virtual void check_goal()
    {
        if (plant_dna.len >= plants_max && animal_dna.len >= animals_max && human_dna.len >= dna_max)
        {
            completed = TRUE;
            SSshuttle.shuttle_purchase_requirements_met[SHUTTLE_UNLOCK_ARK] = TRUE;
        }
    }

    // tg: code/modules/station_goals/dna_vault.dm:184  var/static/list/associated_mutation (a proc-level static, rules 10.6)
    private static DmList upgrade__associated_mutation => TgState.Current.obj_machinery_dna_vault__upgrade__associated_mutation;

    // tg: code/modules/station_goals/dna_vault.dm:182
    public virtual async DmTask upgrade(mob_living_carbon_human target = null, object upgrade_type = null)
    {
        object human_weakref = WEAKREF(target);
        if (!Dm.@in(upgrade__associated_mutation[upgrade_type], power_lottery[human_weakref]) || (HAS_TRAIT(target, TRAIT_USED_DNA_VAULT)))
            return;
        await target.dna.add_mutation(upgrade__associated_mutation[upgrade_type], MUTATION_SOURCE_DNA_VAULT);
        ADD_TRAIT(target, TRAIT_USED_DNA_VAULT, DNA_VAULT_TRAIT);
        power_lottery[human_weakref] = Dm.list();
        await use_energy(active_power_usage);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/station_goals/dna_vault.dm:184  var/static/list/associated_mutation
    internal DmList obj_machinery_dna_vault__upgrade__associated_mutation = Dm.list(
        Dm.kv("Breathless", typeof(datum_mutation_breathless)),
        Dm.kv("Dextrous", typeof(datum_mutation_dextrous)),
        Dm.kv("Quick", typeof(datum_mutation_quick)),
        Dm.kv("Fire Immunity", typeof(datum_mutation_fire_immunity)),
        Dm.kv("Plasmocile", typeof(datum_mutation_plasmocile)),
        Dm.kv("Quick Recovery", typeof(datum_mutation_quick_recovery)),
        Dm.kv("Tough", typeof(datum_mutation_tough)));
}
