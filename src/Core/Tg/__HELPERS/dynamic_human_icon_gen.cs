// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/dynamic_human_icon_gen.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M8 slice G, D187, DV113): only get_dynamic_human_appearance translated (the delusions and the mother of the hallucinations dress a dummy
// and take its look). The mob spawners are not ported (M10), so a corpse spawner type given as mob_spawn_path stands for its `outfit` (what its
// spawner would equip, minus the spawner's own special() and the hair), equipped on the dummy the same way. apply_dynamic_human_appearance and
// set_dynamic_human_appearance from gaps package C6 (the paper wizard, the cat surgeon).

public partial class datum_controller_global_vars
{
    // tg: code/__HELPERS/dynamic_human_icon_gen.dm:2  GLOBAL_LIST_EMPTY(dynamic_human_appearances)
    public DmList dynamic_human_appearances;

    // tg: code/__HELPERS/dynamic_human_icon_gen.dm:2  (GLOBAL_MANAGED(dynamic_human_appearances, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldynamic_human_appearances()
    {
        dynamic_human_appearances = Dm.list();
        Dm.append(ref gvars_datum_init_order, "dynamic_human_appearances");
    }
}

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/dynamic_human_icon_gen.dm:54
    public static void apply_dynamic_human_appearance(atom target = null, Type outfit_path = null, Type species_path = null, Type mob_spawn_path = null, object r_hand = null, object l_hand = null, float? bloody_slots = null)
    {
        Waiting.Detach(set_dynamic_human_appearance(target, outfit_path, species_path, mob_spawn_path, r_hand, l_hand, bloody_slots)); // INVOKE_ASYNC
    }

    ///This proc gets an argument of a target and runs
    // tg: code/__HELPERS/dynamic_human_icon_gen.dm:58
    public static async DmTask set_dynamic_human_appearance(atom target = null, Type outfit_path = null, Type species_path = null, Type mob_spawn_path = null, object r_hand = null, object l_hand = null, float? bloody_slots = null)
    {
        atom dynamic_appearance = await get_dynamic_human_appearance(outfit_path, species_path, mob_spawn_path, r_hand, l_hand, bloody_slots) as atom;
        target.icon = Dm.resource("icons/mob/human/human.dmi");
        target.icon_state = "";
        target.appearance_flags = Dm.bitor(target.appearance_flags, KEEP_TOGETHER);
        target.copy_overlays(dynamic_appearance, TRUE);
    }

    // tg: code/__HELPERS/dynamic_human_icon_gen.dm:5
    public static async DmTask<object> get_dynamic_human_appearance(Type outfit_path = null, Type species_path = null, Type mob_spawn_path = null, object r_hand = null, object l_hand = null, float? bloody_slots = null, float? animated = null, float? combat_mode = null)
    {
        species_path ??= typeof(datum_species_human);
        bloody_slots ??= NONE;
        animated ??= TRUE;
        combat_mode ??= TRUE;
        if (species_path == null)
            return FALSE;
        string arg_string = Dm.text("[]_[]_[]_[]_[]_[]_[]", outfit_path, species_path, mob_spawn_path, l_hand, r_hand, bloody_slots, combat_mode);
        if (Dm.truthy(GLOB.dynamic_human_appearances[arg_string])) //if already exists in our cache, just return that
            return GLOB.dynamic_human_appearances[arg_string];
        mob_living_carbon_human_dummy_consistent dummy = Dm.@new<mob_living_carbon_human_dummy_consistent>();
        // DV113: a dummy is born human, and the species change of a human to a human (on_species_loss) is not translated: it is left out then
        if (!Dm.eq((dummy.dna?.species as datum_species)?.type, species_path))
            dummy.set_species(species_path);
        dummy.stat = DEAD; //this is to avoid side effects of mob spawners
        dummy.underwear = "Nude";
        dummy.undershirt = "Nude";
        dummy.socks = "Nude";
        dummy.set_combat_mode(combat_mode);
        // DV113: a mob spawner's outfit stands for the spawner
        outfit_path ??= mob_spawn_path != null ? Dm.initial(mob_spawn_path, (obj_effect_mob_spawn p) => p.outfit) : null;
        if (outfit_path != null)
        {
            datum_outfit outfit = (datum_outfit)Dm.@new(outfit_path);
            if (!Dm.eq(r_hand, NO_REPLACE)) //we can still override to be null, no replace means just use outfit's
                outfit.r_hand = r_hand as Type;
            if (!Dm.eq(l_hand, NO_REPLACE))
                outfit.l_hand = l_hand as Type;
            await dummy.equipOutfit(outfit, visuals_only: TRUE);
        }
        foreach (obj_item carried_item in Dm.each<obj_item>(dummy.contents))
        {
            if (Dm.truthy(dummy.is_holding(carried_item)))
            {
                datum_component_two_handed twohanded = carried_item.GetComponent(typeof(datum_component_two_handed)) as datum_component_two_handed;
                if (twohanded != null)
                    await twohanded.wield(dummy);
                datum_component_transforming transforming = carried_item.GetComponent(typeof(datum_component_transforming)) as datum_component_transforming;
                if (transforming != null)
                    transforming.set_active(carried_item);
            }
            if (Dm.truthy(Dm.bitand(bloody_slots, carried_item.slot_flags)))
                await carried_item.add_mob_blood(dummy);
        }
        await dummy.update_held_items();
        mutable_appearance output = (mutable_appearance)mutable_appearance();
        output.appearance = dummy.appearance;
        GLOB.dynamic_human_appearances[arg_string] = output;
        qdel(dummy);
        return output;
    }
}
