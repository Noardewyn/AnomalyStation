// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/target_sources/oview_single_type.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1): OVIEW_TARGET_SOURCE expanded, one class per macro call.

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:18  OVIEW_TARGET_SOURCE(carbon_mob)
public partial class datum_target_source_oview_single_type_carbon_mob : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (mob_living_carbon candidate in Dm.each<mob_living_carbon>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:19  OVIEW_TARGET_SOURCE(human_mob)
public partial class datum_target_source_oview_single_type_human_mob : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (mob_living_carbon_human candidate in Dm.each<mob_living_carbon_human>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:20  OVIEW_TARGET_SOURCE(living_mob)
public partial class datum_target_source_oview_single_type_living_mob : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (mob_living candidate in Dm.each<mob_living>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:21  OVIEW_TARGET_SOURCE(disposal_unit)
public partial class datum_target_source_oview_single_type_disposal_unit : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_machinery_disposal candidate in Dm.each<obj_machinery_disposal>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:22  OVIEW_TARGET_SOURCE(paper)
public partial class datum_target_source_oview_single_type_paper : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_item_paper candidate in Dm.each<obj_item_paper>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:23  OVIEW_TARGET_SOURCE(watering_can)
public partial class datum_target_source_oview_single_type_watering_can : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_item_reagent_containers_cup_watering_can candidate in Dm.each<obj_item_reagent_containers_cup_watering_can>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:24  OVIEW_TARGET_SOURCE(ore_stand)
public partial class datum_target_source_oview_single_type_ore_stand : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_structure_ore_container_material_stand candidate in Dm.each<obj_structure_ore_container_material_stand>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:25  OVIEW_TARGET_SOURCE(flora_tree)
public partial class datum_target_source_oview_single_type_flora_tree : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_structure_flora_tree candidate in Dm.each<obj_structure_flora_tree>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:26  OVIEW_TARGET_SOURCE(vent_pump)
public partial class datum_target_source_oview_single_type_vent_pump : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_machinery_atmospherics_components_unary_vent_pump candidate in Dm.each<obj_machinery_atmospherics_components_unary_vent_pump>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:27  OVIEW_TARGET_SOURCE(tribal_chief)
public partial class datum_target_source_oview_single_type_tribal_chief : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (mob_living_basic_mining_mook_worker_tribal_chief candidate in Dm.each<mob_living_basic_mining_mook_worker_tribal_chief>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:28  OVIEW_TARGET_SOURCE(apc)
public partial class datum_target_source_oview_single_type_apc : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_machinery_power_apc candidate in Dm.each<obj_machinery_power_apc>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:29  OVIEW_TARGET_SOURCE(machine)
public partial class datum_target_source_oview_single_type_machine : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_machinery candidate in Dm.each<obj_machinery>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:30  OVIEW_TARGET_SOURCE(beehive)
public partial class datum_target_source_oview_single_type_beehive : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_structure_beebox candidate in Dm.each<obj_structure_beebox>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:31  OVIEW_TARGET_SOURCE(penguin_egg)
public partial class datum_target_source_oview_single_type_penguin_egg : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_item_food_egg_penguin_egg candidate in Dm.each<obj_item_food_egg_penguin_egg>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:32  OVIEW_TARGET_SOURCE(raptor)
public partial class datum_target_source_oview_single_type_raptor : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (mob_living_basic_raptor candidate in Dm.each<mob_living_basic_raptor>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:33  OVIEW_TARGET_SOURCE(raptor_trough)
public partial class datum_target_source_oview_single_type_raptor_trough : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_structure_ore_container_food_trough_raptor_trough candidate in Dm.each<obj_structure_ore_container_food_trough_raptor_trough>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:34  OVIEW_TARGET_SOURCE(gutlunch_trough)
public partial class datum_target_source_oview_single_type_gutlunch_trough : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_structure_ore_container_food_trough_gutlunch_trough candidate in Dm.each<obj_structure_ore_container_food_trough_gutlunch_trough>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:35  OVIEW_TARGET_SOURCE(mouse)
public partial class datum_target_source_oview_single_type_mouse : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (mob_living_basic_mouse candidate in Dm.each<mob_living_basic_mouse>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:36  OVIEW_TARGET_SOURCE(oven)
public partial class datum_target_source_oview_single_type_oven : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_machinery_oven_range candidate in Dm.each<obj_machinery_oven_range>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:37  OVIEW_TARGET_SOURCE(cable)
public partial class datum_target_source_oview_single_type_cable : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_structure_cable candidate in Dm.each<obj_structure_cable>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:38  OVIEW_TARGET_SOURCE(donut)
public partial class datum_target_source_oview_single_type_donut : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_item_food_donut candidate in Dm.each<obj_item_food_donut>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:39  OVIEW_TARGET_SOURCE(hydroponics)
public partial class datum_target_source_oview_single_type_hydroponics : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_machinery_hydroponics candidate in Dm.each<obj_machinery_hydroponics>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:40  OVIEW_TARGET_SOURCE(cheese)
public partial class datum_target_source_oview_single_type_cheese : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_item_food_cheese candidate in Dm.each<obj_item_food_cheese>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:41  OVIEW_TARGET_SOURCE(piano_synth)
public partial class datum_target_source_oview_single_type_piano_synth : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_item_instrument_piano_synth candidate in Dm.each<obj_item_instrument_piano_synth>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:42  OVIEW_TARGET_SOURCE(orbie)
public partial class datum_target_source_oview_single_type_orbie : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (mob_living_basic_orbie candidate in Dm.each<mob_living_basic_orbie>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:43  OVIEW_TARGET_SOURCE(ore_vent)
public partial class datum_target_source_oview_single_type_ore_vent : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_structure_ore_vent candidate in Dm.each<obj_structure_ore_vent>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:44  OVIEW_TARGET_SOURCE(mushroom_food)
public partial class datum_target_source_oview_single_type_mushroom_food : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_item_food_grown_mushroom candidate in Dm.each<obj_item_food_grown_mushroom>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:45  OVIEW_TARGET_SOURCE(ore)
public partial class datum_target_source_oview_single_type_ore : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_item_stack_ore candidate in Dm.each<obj_item_stack_ore>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:46  OVIEW_TARGET_SOURCE(minebot_target)
public partial class datum_target_source_oview_single_type_minebot_target : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_effect_temp_visual_minebot_target candidate in Dm.each<obj_effect_temp_visual_minebot_target>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:47  OVIEW_TARGET_SOURCE(node_drone)
public partial class datum_target_source_oview_single_type_node_drone : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (mob_living_basic_node_drone candidate in Dm.each<mob_living_basic_node_drone>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:48  OVIEW_TARGET_SOURCE(icy_rock)
public partial class datum_target_source_oview_single_type_icy_rock : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_structure_flora_rock_icy candidate in Dm.each<obj_structure_flora_rock_icy>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:49  OVIEW_TARGET_SOURCE(ice_whelp)
public partial class datum_target_source_oview_single_type_ice_whelp : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (mob_living_basic_mining_ice_whelp candidate in Dm.each<mob_living_basic_mining_ice_whelp>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:50  OVIEW_TARGET_SOURCE(hivebot)
public partial class datum_target_source_oview_single_type_hivebot : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (mob_living_basic_hivebot candidate in Dm.each<mob_living_basic_hivebot>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:51  OVIEW_TARGET_SOURCE(carrot)
public partial class datum_target_source_oview_single_type_carrot : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_item_food_grown_carrotlike_carrot candidate in Dm.each<obj_item_food_grown_carrotlike_carrot>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:52  OVIEW_TARGET_SOURCE(ants)
public partial class datum_target_source_oview_single_type_ants : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_effect_decal_cleanable_ants candidate in Dm.each<obj_effect_decal_cleanable_ants>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:53  OVIEW_TARGET_SOURCE(cat_house)
public partial class datum_target_source_oview_single_type_cat_house : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_structure_cat_house candidate in Dm.each<obj_structure_cat_house>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:54  OVIEW_TARGET_SOURCE(honeycomb)
public partial class datum_target_source_oview_single_type_honeycomb : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_item_food_honeycomb candidate in Dm.each<obj_item_food_honeycomb>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:55  OVIEW_TARGET_SOURCE(kitten)
public partial class datum_target_source_oview_single_type_kitten : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (mob_living_basic_pet_cat_kitten candidate in Dm.each<mob_living_basic_pet_cat_kitten>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/oview_single_type.dm:56  OVIEW_TARGET_SOURCE(deer_animals)
public partial class datum_target_source_oview_single_type_deer_animals : datum_target_source_oview_single_type
{
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (mob_living_basic_deer candidate in Dm.each<mob_living_basic_deer>(oview(range, pawn)))
            Dm.append(ref candidates, candidate);
        return candidates;
    }
}
