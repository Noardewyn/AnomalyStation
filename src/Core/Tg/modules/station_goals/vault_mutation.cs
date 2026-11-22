// Translated from tgstation (snapshot 1d99bd7f): code/modules/station_goals/vault_mutation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): the DNA vault's locked mutations (breathless, quick, tough, dextrous, fire immunity, quick
// recovery, plasmocile), so SSatoms' setupGenetics makes every mutation type as tg does. The DNA vault that grants them is the
// station goals' (a later row). The var blocks are generated (D111).

// tg: code/modules/station_goals/vault_mutation.dm:2
public partial class datum_mutation_breathless : datum_mutation
{
    // tg: code/modules/station_goals/vault_mutation.dm:9
    public override async DmTask<object> on_acquiring(mob_living_carbon_human acquirer = null)
    {
        object dot = await base.on_acquiring(acquirer);
        ADD_TRAIT(acquirer, TRAIT_NOBREATH, GENETIC_MUTATION);
        return dot;
    }

    // tg: code/modules/station_goals/vault_mutation.dm:13
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null) //this shouldnt happen under normal condition but just to be sure
    {
        object dot = await base.on_losing(owner);
        REMOVE_TRAIT(owner, TRAIT_NOBREATH, GENETIC_MUTATION);
        return dot;
    }
}

// tg: code/modules/station_goals/vault_mutation.dm:17
public partial class datum_mutation_quick : datum_mutation
{
    // tg: code/modules/station_goals/vault_mutation.dm:24
    public override async DmTask<object> on_acquiring(mob_living_carbon_human acquirer = null)
    {
        object dot = await base.on_acquiring(acquirer);
        await acquirer.add_movespeed_modifier(typeof(datum_movespeed_modifier_dna_vault_speedup));
        return dot;
    }

    // tg: code/modules/station_goals/vault_mutation.dm:28
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_dna_vault_speedup));
        return dot;
    }
}

// tg: code/modules/station_goals/vault_mutation.dm:32
public partial class datum_mutation_tough : datum_mutation
{
    // tg: code/modules/station_goals/vault_mutation.dm:39
    public override async DmTask<object> on_acquiring(mob_living_carbon_human acquirer = null)
    {
        object dot = await base.on_acquiring(acquirer);
        MODIFY_PHYSIOLOGY(acquirer, BRUTE, 0.7f);
        ADD_TRAIT(acquirer, TRAIT_PIERCEIMMUNE, GENETIC_MUTATION);
        return dot;
    }

    // tg: code/modules/station_goals/vault_mutation.dm:44
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        MODIFY_PHYSIOLOGY(owner, BRUTE, 1 / 0.7f);
        REMOVE_TRAIT(owner, TRAIT_PIERCEIMMUNE, GENETIC_MUTATION);
        return dot;
    }
}

// tg: code/modules/station_goals/vault_mutation.dm:49
public partial class datum_mutation_dextrous : datum_mutation
{
    // tg: code/modules/station_goals/vault_mutation.dm:56
    public override async DmTask<object> on_acquiring(mob_living_carbon_human acquirer = null)
    {
        object dot = await base.on_acquiring(acquirer);
        acquirer.next_move_modifier *= 0.5f;
        return dot;
    }

    // tg: code/modules/station_goals/vault_mutation.dm:60
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        owner.next_move_modifier /= 0.5f;
        return dot;
    }
}

// tg: code/modules/station_goals/vault_mutation.dm:64
public partial class datum_mutation_fire_immunity : datum_mutation
{
    // tg: code/modules/station_goals/vault_mutation.dm:71
    public override async DmTask<object> on_acquiring(mob_living_carbon_human acquirer = null)
    {
        object dot = await base.on_acquiring(acquirer);
        MODIFY_PHYSIOLOGY(acquirer, BURN, 0.5f);
        acquirer.add_traits(Dm.list(TRAIT_RESISTHEAT, TRAIT_NOFIRE), GENETIC_MUTATION);
        return dot;
    }

    // tg: code/modules/station_goals/vault_mutation.dm:76
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        MODIFY_PHYSIOLOGY(owner, BURN, 2);
        owner.remove_traits(Dm.list(TRAIT_RESISTHEAT, TRAIT_NOFIRE), GENETIC_MUTATION);
        return dot;
    }
}

// tg: code/modules/station_goals/vault_mutation.dm:81
public partial class datum_mutation_quick_recovery : datum_mutation
{
    // tg: code/modules/station_goals/vault_mutation.dm:88
    public override async DmTask<object> on_acquiring(mob_living_carbon_human acquirer = null)
    {
        object dot = await base.on_acquiring(acquirer);
        MODIFY_PHYSIOLOGY(acquirer, PHYS_COEFF_STUN, 0.5f);
        return dot;
    }

    // tg: code/modules/station_goals/vault_mutation.dm:92
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_STUN, 2);
        return dot;
    }
}

// tg: code/modules/station_goals/vault_mutation.dm:96
public partial class datum_mutation_plasmocile : datum_mutation
{
    // tg: code/modules/station_goals/vault_mutation.dm:103
    public override async DmTask<object> on_acquiring(mob_living_carbon_human acquirer = null)
    {
        object dot = await base.on_acquiring(acquirer);
        obj_item_organ_lungs improved_lungs = acquirer.get_organ_slot(ORGAN_SLOT_LUNGS) as obj_item_organ_lungs;
        ADD_TRAIT(owner, TRAIT_VIRUSIMMUNE, GENETIC_MUTATION);
        if (improved_lungs != null)
            apply_buff(improved_lungs);
        RegisterSignal(acquirer, COMSIG_CARBON_LOSE_ORGAN, nameof(remove_modification));
        RegisterSignal(acquirer, COMSIG_CARBON_GAIN_ORGAN, nameof(reapply_modification));
        return dot;
    }

    // tg: code/modules/station_goals/vault_mutation.dm:113
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        obj_item_organ_lungs improved_lungs = owner.get_organ_slot(ORGAN_SLOT_LUNGS) as obj_item_organ_lungs;
        REMOVE_TRAIT(owner, TRAIT_VIRUSIMMUNE, GENETIC_MUTATION);
        UnregisterSignal(owner, COMSIG_CARBON_LOSE_ORGAN);
        UnregisterSignal(owner, COMSIG_CARBON_GAIN_ORGAN);
        if (improved_lungs != null)
            remove_buff(improved_lungs);
        return dot;
    }

    // tg: code/modules/station_goals/vault_mutation.dm:122
    [SignalHandler]
    public virtual void remove_modification(mob source = null, obj_item_organ old_organ = null)
    {
        if (old_organ is obj_item_organ_lungs lungs)
            remove_buff(lungs);
    }

    // tg: code/modules/station_goals/vault_mutation.dm:128
    [SignalHandler]
    public virtual void reapply_modification(mob source = null, obj_item_organ new_organ = null)
    {
        if (new_organ is obj_item_organ_lungs lungs)
            apply_buff(lungs);
    }

    // tg: code/modules/station_goals/vault_mutation.dm:134
    public virtual void apply_buff(obj_item_organ_lungs our_lungs = null)
    {
        our_lungs.plas_breath_dam_min *= 0;
        our_lungs.plas_breath_dam_max *= 0;
    }

    // tg: code/modules/station_goals/vault_mutation.dm:138
    public virtual void remove_buff(obj_item_organ_lungs our_lungs = null)
    {
        our_lungs.plas_breath_dam_min = Dm.initial(our_lungs, (obj_item_organ_lungs l) => l.plas_breath_dam_min);
        our_lungs.plas_breath_dam_max = Dm.initial(our_lungs, (obj_item_organ_lungs l) => l.plas_breath_dam_max);
    }
}
