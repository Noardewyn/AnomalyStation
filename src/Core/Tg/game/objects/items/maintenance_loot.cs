// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/maintenance_loot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps OD2 (D333): the file whole. The lead pipe grinds into lead; the lead-acid battery starts 40-60 % charged, keeps its one
// picture (no charge lights), is fishing bait, grinds into lead, sulfuric acid and water, and loads the shock touch mutation onto its
// carrier's bitrunning avatar (the loads_avatar_gear component, modules/bitrunning/components/avatar_gear.cs, translated with it;
// the virtual domains that send its signal are not ported). The var blocks are generated (D111).

//The items defined in this file are intended be scarce maintenance loot items some of these items are used as a non-renewable resource in crafting or ghetto chem.
//Exercise good judgement and don't add these to a lathe willy nilly.

//Saw-tier bulky & blunt weapon. A decent bone breaker. Source of lead reagent.
//Add lead material to this once implemented.
// tg: code/game/objects/items/maintenance_loot.dm:6
public partial class obj_item_lead_pipe : obj_item
{
    // tg: code/game/objects/items/maintenance_loot.dm:29
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_lead), 20));
    }
}

//A good battery early in the shift. Source of lead & sulfuric acid reagents.
//Add lead material to this once implemented.
// tg: code/game/objects/items/maintenance_loot.dm:34
public partial class obj_item_stock_parts_power_store_cell_lead : obj_item_stock_parts_power_store_cell
{
    // tg: code/game/objects/items/maintenance_loot.dm:48
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_lead), 15), Dm.kv(typeof(datum_reagent_toxin_acid), 15), Dm.kv(typeof(datum_reagent_water), 20));
    }

    //starts partially discharged
    // tg: code/game/objects/items/maintenance_loot.dm:52
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_blocker)));
        object dot = base.Initialize(mapload, __extra);
        float initial_percent = rand(40, 60) / 100; // 250kJ to 350kJ
        charge = initial_percent * maxcharge;
        ADD_TRAIT(this, TRAIT_FISHING_BAIT, INNATE_TRAIT);
        Waiting.NoSleep(AddComponent(typeof(datum_component_loads_avatar_gear),
            Dm.named("load_callback", Dm.@new<datum_callback>(this, nameof(shockingly_improve_avatar)))
        ));
        return dot;
    }

    // Give our owner shock touch when entering the digital realm
    // tg: code/game/objects/items/maintenance_loot.dm:63
    public virtual async DmTask<object> shockingly_improve_avatar(mob_living_carbon_human neo = null, mob_living_carbon_human avatar = null, object domain_flags = null)
    {
        if (Dm.truthy(Dm.bitand(domain_flags, DOMAIN_FORBIDS_ABILITIES)))
            return BITRUNNER_GEAR_LOAD_BLOCKED;

        if (!Dm.truthy(avatar.can_mutate()))
            return BITRUNNER_GEAR_LOAD_FAILED;

        if (Dm.truthy(avatar.dna.mutation_in_sequence(typeof(datum_mutation_shock))))
            await avatar.dna.activate_mutation(typeof(datum_mutation_shock));
        else
            await avatar.dna.add_mutation(typeof(datum_mutation_shock), MUTATION_SOURCE_MUTATOR);
        return null;
    }
}
