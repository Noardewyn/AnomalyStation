// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/sheets/sheet_types.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only iron (metal_recipes and the iron sheet), plasteel (plasteel_recipes and the sheet) and plastic
// (plastic_recipes and the sheet) translated (M5 slice 2: the stacks of the station's construction; recipes whose results
// are not ported build inert things, D132, D148), and wood (wood_recipes and the plank; the M5 leftovers, 2026-10-16: the
// planks of can_barricade); the cloth's [DmPath] is held here for M2 F1 (the clothing's repair stack); the paper frames
// (paperframe_recipes and the sheet: gaps ST5, the paper frame separator); gaps SK1 (D333): the bamboo, cloth, durathread,
// cardboard (with the clown's and the chameleon stamp's boxes), bronze, bone, pizza and hauntium sheets and their recipe lists. The
// runed metal (the cult's, runed_metal.dm) waits for the cult (D333).

/* Diffrent misc types of sheets
 * Contains:
 * Iron
 * Plasteel
 * Wood
 * Cloth
 * Plastic
 * Cardboard
 * Paper Frames
 * Runed Metal (cult)
 * Bronze (bake brass)
 */

/*
 * Iron
 */
public partial class datum_controller_global_vars
{
    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:17  GLOBAL_LIST_INIT(metal_recipes, list(...))
    public DmList metal_recipes;

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:17  (GLOBAL_MANAGED(metal_recipes, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmetal_recipes()
    {
        metal_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("stool", typeof(obj_structure_chair_stool), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("bar stool", typeof(obj_structure_chair_stool_bar), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("bed", typeof(obj_structure_bed), 2, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("double bed", typeof(obj_structure_bed_double), 4, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
            null,
            Dm.@new<datum_stack_recipe_list>("office chairs", Dm.list(
                Dm.@new<datum_stack_recipe>("dark office chair", typeof(obj_structure_chair_office), 5, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("light office chair", typeof(obj_structure_chair_office_light), 5, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE))
            )),
            Dm.@new<datum_stack_recipe_list>("comfy chairs", Dm.list(
                Dm.@new<datum_stack_recipe>("beige comfy chair", typeof(obj_structure_chair_comfy_beige), 2, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("black comfy chair", typeof(obj_structure_chair_comfy_black), 2, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("brown comfy chair", typeof(obj_structure_chair_comfy_brown), 2, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("lime comfy chair", typeof(obj_structure_chair_comfy_lime), 2, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("teal comfy chair", typeof(obj_structure_chair_comfy_teal), 2, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE))
            )),
            Dm.@new<datum_stack_recipe_list>("sofas", Dm.list(
                Dm.@new<datum_stack_recipe>("corporate sofa (middle)", typeof(obj_structure_chair_sofa_corp), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("corporate sofa (left)", typeof(obj_structure_chair_sofa_corp_left), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("corporate sofa (right)", typeof(obj_structure_chair_sofa_corp_right), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("corporate sofa (corner)", typeof(obj_structure_chair_sofa_corp_corner), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("sofa (middle)", typeof(obj_structure_chair_sofa_middle), 1, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("sofa (left)", typeof(obj_structure_chair_sofa_left), 1, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("sofa (right)", typeof(obj_structure_chair_sofa_right), 1, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("sofa (corner)", typeof(obj_structure_chair_sofa_corner), 1, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("retro sofa (middle)", typeof(obj_structure_chair_sofa_middle_brown), 1, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("retro sofa (left)", typeof(obj_structure_chair_sofa_left_brown), 1, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("retro sofa (right)", typeof(obj_structure_chair_sofa_right_brown), 1, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("retro sofa (corner)", typeof(obj_structure_chair_sofa_corner_brown), 1, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE))
            )),
            Dm.@new<datum_stack_recipe_list>("benches", Dm.list(
                Dm.@new<datum_stack_recipe>("bench (middle)", typeof(obj_structure_chair_sofa_bench), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("bench (left)", typeof(obj_structure_chair_sofa_bench_left), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("bench (right)", typeof(obj_structure_chair_sofa_bench_right), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("bench (corner)", typeof(obj_structure_chair_sofa_bench_corner), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("tram bench (solo)", typeof(obj_structure_chair_sofa_bench_tram_solo), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("tram bench (middle)", typeof(obj_structure_chair_sofa_bench_tram), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("tram bench (left)", typeof(obj_structure_chair_sofa_bench_tram_left), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("tram bench (right)", typeof(obj_structure_chair_sofa_bench_tram_right), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("tram bench (corner)", typeof(obj_structure_chair_sofa_bench_tram_corner), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE))
            )),
            Dm.@new<datum_stack_recipe_list>("chess pieces", Dm.list(
                Dm.@new<datum_stack_recipe>("White Pawn", typeof(obj_structure_chess_whitepawn), 2, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_ENTERTAINMENT)),
                Dm.@new<datum_stack_recipe>("White Rook", typeof(obj_structure_chess_whiterook), 2, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_ENTERTAINMENT)),
                Dm.@new<datum_stack_recipe>("White Knight", typeof(obj_structure_chess_whiteknight), 2, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_ENTERTAINMENT)),
                Dm.@new<datum_stack_recipe>("White Bishop", typeof(obj_structure_chess_whitebishop), 2, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_ENTERTAINMENT)),
                Dm.@new<datum_stack_recipe>("White Queen", typeof(obj_structure_chess_whitequeen), 2, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_ENTERTAINMENT)),
                Dm.@new<datum_stack_recipe>("White King", typeof(obj_structure_chess_whiteking), 2, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_ENTERTAINMENT)),
                Dm.@new<datum_stack_recipe>("Black Pawn", typeof(obj_structure_chess_blackpawn), 2, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_ENTERTAINMENT)),
                Dm.@new<datum_stack_recipe>("Black Rook", typeof(obj_structure_chess_blackrook), 2, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_ENTERTAINMENT)),
                Dm.@new<datum_stack_recipe>("Black Knight", typeof(obj_structure_chess_blackknight), 2, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_ENTERTAINMENT)),
                Dm.@new<datum_stack_recipe>("Black Bishop", typeof(obj_structure_chess_blackbishop), 2, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_ENTERTAINMENT)),
                Dm.@new<datum_stack_recipe>("Black Queen", typeof(obj_structure_chess_blackqueen), 2, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_ENTERTAINMENT)),
                Dm.@new<datum_stack_recipe>("Black King", typeof(obj_structure_chess_blackking), 2, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_ENTERTAINMENT))
            )),
            Dm.@new<datum_stack_recipe_list>("checkers pieces", Dm.list(
                Dm.@new<datum_stack_recipe>("White Checker Man", typeof(obj_structure_chess_checker_whiteman), 2, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_ENTERTAINMENT)),
                Dm.@new<datum_stack_recipe>("White Checker King", typeof(obj_structure_chess_checker_whiteking), 2, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_ENTERTAINMENT)),
                Dm.@new<datum_stack_recipe>("Black Checker Man", typeof(obj_structure_chess_checker_blackman), 2, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_ENTERTAINMENT)),
                Dm.@new<datum_stack_recipe>("Black Checker King", typeof(obj_structure_chess_checker_blackking), 2, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_ENTERTAINMENT))
            )),
            null,
            Dm.@new<datum_stack_recipe>("rack parts", typeof(obj_item_rack_parts), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("closet", typeof(obj_structure_closet), 2, Dm.named("time", 1.5f * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
            null,
            Dm.@new<datum_stack_recipe>("atmos canister", typeof(obj_machinery_portable_atmospherics_canister), 10, Dm.named("time", 3 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_ATMOSPHERIC)),
            Dm.@new<datum_stack_recipe>("pipe", typeof(obj_item_pipe_quaternary_pipe_crafted), 1, Dm.named("time", 4 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_ATMOSPHERIC)),
            null,
            Dm.@new<datum_stack_recipe>("floor tile", typeof(obj_item_stack_tile_iron_base), 1, 4, 20, Dm.named("category", CAT_TILES)),
            Dm.@new<datum_stack_recipe>("iron rod", typeof(obj_item_stack_rods), 1, 2, 60, Dm.named("category", CAT_MISC)),
            null,
            // Independent ceilings: a full-cell column reuses wall integrity and dismantling.
            Dm.@new<datum_stack_recipe>("ceiling support column", typeof(turf_closed_wall_ceiling_column), 4, Dm.named("time", 4 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("wall girders (anchored)", typeof(obj_structure_girder), 2, Dm.named("time", 4 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("placement_checks", STACK_CHECK_TRAM_FORBIDDEN), Dm.named("trait_booster", TRAIT_QUICK_BUILD), Dm.named("trait_modifier", 0.75f), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe_list>("platforms (anchored)", Dm.list(
                Dm.@new<datum_stack_recipe>("rounded platform", typeof(obj_structure_platform), 2, Dm.named("time", 3 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("trait_booster", TRAIT_QUICK_BUILD), Dm.named("trait_modifier", 0.75f), Dm.named("category", CAT_STRUCTURE)),
                Dm.@new<datum_stack_recipe>("rough platform", typeof(obj_structure_platform_iron), 2, Dm.named("time", 3 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("trait_booster", TRAIT_QUICK_BUILD), Dm.named("trait_modifier", 0.75f), Dm.named("category", CAT_STRUCTURE)),
                Dm.@new<datum_stack_recipe>("platform steps", typeof(obj_structure_steps), 2, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("trait_booster", TRAIT_QUICK_BUILD), Dm.named("trait_modifier", 0.75f), Dm.named("category", CAT_STRUCTURE))
            )),
            null,
            Dm.@new<datum_stack_recipe>("computer frame", typeof(obj_structure_frame_computer), 5, Dm.named("time", 2.5f * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("modular console", typeof(obj_machinery_modular_computer), 10, Dm.named("time", 2.5f * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("machine frame", typeof(obj_structure_frame_machine), 5, Dm.named("time", 2.5f * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_STRUCTURE)),
            null,
            Dm.@new<datum_stack_recipe_list>("airlock assemblies", Dm.list(
                Dm.@new<datum_stack_recipe>("standard airlock assembly", typeof(obj_structure_door_assembly), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
                Dm.@new<datum_stack_recipe>("public airlock assembly", typeof(obj_structure_door_assembly_door_assembly_public), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
                Dm.@new<datum_stack_recipe>("command airlock assembly", typeof(obj_structure_door_assembly_door_assembly_com), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
                Dm.@new<datum_stack_recipe>("security airlock assembly", typeof(obj_structure_door_assembly_door_assembly_sec), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
                Dm.@new<datum_stack_recipe>("engineering airlock assembly", typeof(obj_structure_door_assembly_door_assembly_eng), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
                Dm.@new<datum_stack_recipe>("mining airlock assembly", typeof(obj_structure_door_assembly_door_assembly_min), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
                Dm.@new<datum_stack_recipe>("atmospherics airlock assembly", typeof(obj_structure_door_assembly_door_assembly_atmo), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
                Dm.@new<datum_stack_recipe>("research airlock assembly", typeof(obj_structure_door_assembly_door_assembly_research), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
                Dm.@new<datum_stack_recipe>("freezer airlock assembly", typeof(obj_structure_door_assembly_door_assembly_fre), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
                Dm.@new<datum_stack_recipe>("science airlock assembly", typeof(obj_structure_door_assembly_door_assembly_science), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
                Dm.@new<datum_stack_recipe>("medical airlock assembly", typeof(obj_structure_door_assembly_door_assembly_med), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
                Dm.@new<datum_stack_recipe>("hydroponics airlock assembly", typeof(obj_structure_door_assembly_door_assembly_hydro), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
                Dm.@new<datum_stack_recipe>("virology airlock assembly", typeof(obj_structure_door_assembly_door_assembly_viro), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
                Dm.@new<datum_stack_recipe>("maintenance airlock assembly", typeof(obj_structure_door_assembly_door_assembly_mai), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
                Dm.@new<datum_stack_recipe>("external airlock assembly", typeof(obj_structure_door_assembly_door_assembly_ext), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
                Dm.@new<datum_stack_recipe>("external maintenance airlock assembly", typeof(obj_structure_door_assembly_door_assembly_extmai), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
                Dm.@new<datum_stack_recipe>("airtight hatch assembly", typeof(obj_structure_door_assembly_door_assembly_hatch), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
                Dm.@new<datum_stack_recipe>("maintenance hatch assembly", typeof(obj_structure_door_assembly_door_assembly_mhatch), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS))
            )),
            null,
            Dm.@new<datum_stack_recipe>("firelock frame", typeof(obj_structure_firelock_frame), 3, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
            Dm.@new<datum_stack_recipe>("directional firelock frame", typeof(obj_structure_firelock_frame_border_only), 2, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ON_SOLID_GROUND | CRAFT_CHECK_DIRECTION), Dm.named("category", CAT_DOORS)),
            Dm.@new<datum_stack_recipe>("turret frame", typeof(obj_machinery_porta_turret_construct), 5, Dm.named("time", 2.5f * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("meatspike frame", typeof(obj_structure_kitchenspike_frame), 5, Dm.named("time", 2.5f * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("reflector frame", typeof(obj_structure_reflector), 5, Dm.named("time", 2.5f * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_STRUCTURE)),
            null,
            Dm.@new<datum_stack_recipe>("grenade casing", typeof(obj_item_grenade_chem_grenade), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CHEMISTRY)),
            Dm.@new<datum_stack_recipe>("light fixture frame", typeof(obj_item_wallframe_light_fixture), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("small light fixture frame", typeof(obj_item_wallframe_light_fixture_small), 1, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_STRUCTURE)),
            null,
            Dm.@new<datum_stack_recipe>("apc frame", typeof(obj_item_wallframe_apc), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("air alarm frame", typeof(obj_item_wallframe_airalarm), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("fire alarm frame", typeof(obj_item_wallframe_firealarm), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("extinguisher cabinet frame", typeof(obj_item_wallframe_extinguisher_cabinet), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("button frame", typeof(obj_item_wallframe_button), 1, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("light switch frame", typeof(obj_item_wallframe_light_switch), 1, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("sparker frame", typeof(obj_item_wallframe_sparker), 1, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("turret control frame", typeof(obj_item_wallframe_turret_control), 6, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("camera assembly", typeof(obj_item_wallframe_camera), 1, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("newscaster frame", typeof(obj_item_wallframe_newscaster), 7, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("status display frame", typeof(obj_item_wallframe_status_display), 7, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("intercom frame", typeof(obj_item_wallframe_intercom), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("requests console frame", typeof(obj_item_wallframe_requests_console), 7, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("telescreen turbine frame", typeof(obj_item_wallframe_telescreen_turbine), 7, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("telescreen engine frame", typeof(obj_item_wallframe_telescreen_engine), 7, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("telescreen auxbase frame", typeof(obj_item_wallframe_telescreen_auxbase), 7, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("tram controller frame", typeof(obj_item_wallframe_tram), 20, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("tram display frame", typeof(obj_item_wallframe_indicator_display), 7, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_STRUCTURE)),
            null,
            Dm.@new<datum_stack_recipe>("iron door", typeof(obj_structure_mineral_door_iron), 20, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
            Dm.@new<datum_stack_recipe>("filing cabinet", typeof(obj_structure_filingcabinet), 2, Dm.named("time", 10 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("desk bell", typeof(obj_structure_desk_bell), 2, Dm.named("time", 3 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("floodlight frame", typeof(obj_structure_floodlight_frame), 5, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("voting box", typeof(obj_structure_votebox), 15, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_ENTERTAINMENT)),
            Dm.@new<datum_stack_recipe>("pestle", typeof(obj_item_pestle), 1, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CHEMISTRY)),
            Dm.@new<datum_stack_recipe>("hygienebot assembly", typeof(obj_item_bot_assembly_hygienebot), 2, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_ROBOT)),
            Dm.@new<datum_stack_recipe>("shower frame", typeof(obj_structure_showerframe), 2, Dm.named("time", 2 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("urinal", typeof(obj_item_wallframe_urinal), 2, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_FURNITURE))
        );
        Dm.append(ref gvars_datum_init_order, "metal_recipes");
    }
}

// tg: code/game/objects/items/stacks/sheets/sheet_types.dm:161
public partial class obj_item_stack_sheet_iron : obj_item_stack_sheet
{
    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:182
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // var/static/list/tool_behaviors = list(TOOL_WELDER = list(LMB = "Craft iron rods", RMB = "Craft floor tiles")) and
        // AddElement(/datum/element/contextual_screentip_tools, tool_behaviors) (:184-190): screentips (M9), as elsewhere.
        Dm.Deferred("code/game/objects/items/stacks/sheets/sheet_types.dm:190", "Initialize: AddElement(/datum/element/contextual_screentip_tools, tool_behaviors): screentips (M10+)");
        return dot;
    }

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:192
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("Right click on floor to build:")));
        Dm.append(ref dot, span_notice(I18n.T("- Unanchored wall girder")));
        Dm.append(ref dot, span_notice(I18n.T("- Computer or Machine frame (with circuitboard)")));
        return dot;
    }

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:210
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.metal_recipes);
        return dot;
    }

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:214
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] begins whacking []self over the head with \the []! It looks like [] trying to commit suicide!", user, user.p_them(), this, user.p_theyre())));
        return BRUTELOSS;
    }

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:218
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(await tool.use_tool(this, user, delay: 0, volume: 40)))
        {
            obj_item_stack_rods_two new_item = Dm.@new<obj_item_stack_rods_two>(user.loc);
            user.visible_message(
                span_notice(I18n.T("[] shaped [] into floor rods with [].", user.name, this, tool)),
                blind_message: span_hear(I18n.T("You hear welding.")),
                vision_distance: COMBAT_MESSAGE_RANGE,
                ignored_mobs: user
            );
            await use(1);
            await user.put_in_inactive_hand(new_item);
            return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:231
    public override async DmTask<object> welder_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(await tool.use_tool(this, user, delay: 0, volume: 40)))
        {
            obj_item_stack_tile_iron_four new_item = Dm.@new<obj_item_stack_tile_iron_four>(user.loc);
            user.visible_message(
                span_notice(I18n.T("[] shaped [] into floor tiles with [].", user.name, this, tool)),
                blind_message: span_hear(I18n.T("You hear welding.")),
                vision_distance: COMBAT_MESSAGE_RANGE,
                ignored_mobs: user
            );
            await use(1);
            await user.put_in_inactive_hand(new_item);
            return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:244
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isopenturf(interacting_with))
            return NONE;
        turf_open build_on = interacting_with as turf_open;
        if (!Dm.truthy(await user.Adjacent(build_on)))
            return ITEM_INTERACT_BLOCKING;
        if (isgroundlessturf(build_on))
        {
            user.balloon_alert(user, I18n.T("can't place it here!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(await build_on.is_blocked_turf()))
        {
            user.balloon_alert(user, I18n.T("something is blocking the tile!"));
            return ITEM_INTERACT_BLOCKING;
        }

        Type frame_path = null;
        float cost = 2; // Default girder cost
        float time = 4 * SECONDS; //Default girder build time
        obj_item_circuitboard held_board = locate_type(typeof(obj_item_circuitboard), user.held_items) as obj_item_circuitboard;
        if (held_board != null)
        {
            if (istype(held_board, typeof(obj_item_circuitboard_machine)))
                frame_path = typeof(obj_structure_frame_machine);
            else
                frame_path = typeof(obj_structure_frame_computer);
            foreach (datum_stack_recipe recipe in Dm.each<datum_stack_recipe>(GLOB.metal_recipes))
            {
                if (recipe.result_type == frame_path)
                {
                    time = Dm.num(recipe.time);
                    cost = recipe.req_amount;
                    break;
                }
            }
        }
        if (Dm.num(get_amount()) < cost)
        {
            user.balloon_alert(user, I18n.T("need [] metal sheets!", cost));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await do_after(user, time, build_on)))
            return ITEM_INTERACT_BLOCKING;
        if (Dm.truthy(await build_on.is_blocked_turf()))
        {
            user.balloon_alert(user, I18n.T("something is blocking the tile!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await use(cost)))
        {
            user.balloon_alert(user, I18n.T("not enough material!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (frame_path != null)
        {
            obj_structure_frame constructed_frame = Dm.@new(frame_path, build_on) as obj_structure_frame;
            await constructed_frame.setDir(REVERSE_DIR(user.dir)); //to align computer frame with player direction
            user.balloon_alert(user, I18n.T("frame created"));
        }
        else
        {
            Dm.@new<obj_structure_girder_displaced>(build_on);
            user.balloon_alert(user, I18n.T("girder created"));
        }
        return ITEM_INTERACT_SUCCESS;
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:294  GLOBAL_LIST_INIT(plasteel_recipes, list(...))
    public DmList plasteel_recipes;

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:294  (GLOBAL_MANAGED(plasteel_recipes, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalplasteel_recipes()
    {
        plasteel_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("AI core", typeof(obj_structure_ai_core), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF), Dm.named("category", CAT_ROBOT)),
            Dm.@new<datum_stack_recipe>("bomb assembly", typeof(obj_machinery_syndicatebomb_empty), 10, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CHEMISTRY)),
            Dm.@new<datum_stack_recipe>("Large Gas Tank", typeof(obj_structure_tank_frame), 4, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF), Dm.named("category", CAT_ATMOSPHERIC)),
            Dm.@new<datum_stack_recipe>("shutter assembly", typeof(obj_machinery_door_poddoor_shutters_preopen_deconstructed), 5, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_ONE_PER_TURF), Dm.named("category", CAT_DOORS)),
            Dm.@new<datum_stack_recipe>("large metal box", typeof(obj_structure_closet_cardboard_metal), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_CONTAINERS)),
            null,
            Dm.@new<datum_stack_recipe_list>("airlock assemblies", Dm.list(
                Dm.@new<datum_stack_recipe>("high security airlock assembly", typeof(obj_structure_door_assembly_door_assembly_highsecurity), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
                Dm.@new<datum_stack_recipe>("vault door assembly", typeof(obj_structure_door_assembly_door_assembly_vault), 6, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS))
            )),
            Dm.@new<datum_stack_recipe>("Radar Tower", typeof(obj_machinery_power_weather_tower_constructed), 12, Dm.named("time", 10 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_STRUCTURE))
        );
        Dm.append(ref gvars_datum_init_order, "plasteel_recipes");
    }
}

/*
 * Plasteel
 */
// tg: code/game/objects/items/stacks/sheets/sheet_types.dm:308
public partial class obj_item_stack_sheet_plasteel : obj_item_stack_sheet
{
    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:331
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.plasteel_recipes);
        return dot;
    }
}

/*
 * Wood
 */
public partial class datum_controller_global_vars
{
    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:344  GLOBAL_LIST_INIT(wood_recipes, list(...))
    public DmList wood_recipes;

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:344  (GLOBAL_MANAGED(wood_recipes, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalwood_recipes()
    {
        wood_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("wooden sandals", typeof(obj_item_clothing_shoes_sandal), 1, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("wood floor tile", typeof(obj_item_stack_tile_wood), 1, 4, 20, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_TILES)),
            Dm.@new<datum_stack_recipe>("wood table frame", typeof(obj_structure_table_frame_wood), 2, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe_list>("platforms (anchored)", Dm.list(
                Dm.@new<datum_stack_recipe>("wooden platform", typeof(obj_structure_platform_wood), 2, Dm.named("time", 3 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("trait_booster", TRAIT_QUICK_BUILD), Dm.named("trait_modifier", 0.75f), Dm.named("category", CAT_STRUCTURE)),
                Dm.@new<datum_stack_recipe>("wooden stage", typeof(obj_structure_platform_wood_stage), 2, Dm.named("time", 3 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("trait_booster", TRAIT_QUICK_BUILD), Dm.named("trait_modifier", 0.75f), Dm.named("category", CAT_STRUCTURE))
            )),
            Dm.@new<datum_stack_recipe>("rolling pin", typeof(obj_item_kitchen_rollingpin), 2, Dm.named("time", 3 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_TOOLS)),
            Dm.@new<datum_stack_recipe>("wooden chair", typeof(obj_structure_chair_wood), 3, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("winged wooden chair", typeof(obj_structure_chair_wood_wings), 3, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("wooden barricade", typeof(obj_structure_barricade_wooden), 5, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("wooden door", typeof(obj_structure_mineral_door_wood), 10, Dm.named("time", 2 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
            Dm.@new<datum_stack_recipe>("wooden stairs frame", typeof(obj_structure_stairs_frame_wood), 10, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("wooden fence", typeof(obj_structure_railing_wooden_fence), 2, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("raptor trough", typeof(obj_structure_ore_container_food_trough_raptor_trough), 5, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("cat house", typeof(obj_structure_cat_house), 5, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("coffin", typeof(obj_structure_closet_crate_coffin), 5, Dm.named("time", 1.5f * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("book case", typeof(obj_structure_bookcase), 4, Dm.named("time", 1.5f * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("drying rack", typeof(obj_machinery_smartfridge_drying_rack), 10, Dm.named("time", 1.5f * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_TOOLS)),
            Dm.@new<datum_stack_recipe>("wooden barrel", typeof(obj_structure_fermenting_barrel), 8, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_CONTAINERS)),
            Dm.@new<datum_stack_recipe>("dog bed", typeof(obj_structure_bed_dogbed), 10, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("gavel hammer", typeof(obj_item_gavelhammer), 2, Dm.named("time", 3 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_ENTERTAINMENT)),
            Dm.@new<datum_stack_recipe>("gavel block", typeof(obj_item_gavelblock), 1, Dm.named("time", 2 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_ENTERTAINMENT)),
            Dm.@new<datum_stack_recipe>("dresser", typeof(obj_structure_dresser), 10, Dm.named("time", 1.5f * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("cabinet", typeof(obj_structure_closet_cabinet), 2, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_CONTAINERS)),
            Dm.@new<datum_stack_recipe>("picture frame", typeof(obj_item_wallframe_picture), 1, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_ENTERTAINMENT)),
            Dm.@new<datum_stack_recipe>("painting frame", typeof(obj_item_wallframe_painting), 1, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_ENTERTAINMENT)),
            Dm.@new<datum_stack_recipe>("display case chassis", typeof(obj_structure_displaycase_chassis), 5, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("wooden buckler", typeof(obj_item_shield_buckler), 20, Dm.named("time", 4 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_EQUIPMENT)),
            Dm.@new<datum_stack_recipe>("apiary", typeof(obj_structure_beebox), 40, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_TOOLS)),
            Dm.@new<datum_stack_recipe>("mannequin", typeof(obj_structure_mannequin_wood), 25, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_ONE_PER_TURF), Dm.named("category", CAT_ENTERTAINMENT)),
            Dm.@new<datum_stack_recipe>("tiki mask", typeof(obj_item_clothing_mask_gas_tiki_mask), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("smoking pipe", typeof(obj_item_cigarette_pipe), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("honey frame", typeof(obj_item_honey_frame), 5, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_TOOLS)),
            Dm.@new<datum_stack_recipe>("wooden bucket", typeof(obj_item_reagent_containers_cup_bucket_wooden), 3, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
            Dm.@new<datum_stack_recipe>("rake", typeof(obj_item_cultivator_rake), 5, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_TOOLS)),
            Dm.@new<datum_stack_recipe>("ore box", typeof(obj_structure_ore_box), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_CONTAINERS)),
            Dm.@new<datum_stack_recipe>("wooden crate", typeof(obj_structure_closet_crate_wooden), 6, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("baseball bat", typeof(obj_item_melee_baseball_bat), 5, Dm.named("time", 1.5f * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_WEAPON_MELEE), Dm.named("removed_mats", Dm.list(Dm.kv(typeof(datum_material_wood), SHEET_MATERIAL_AMOUNT * 1.5f)))),
            Dm.@new<datum_stack_recipe>("wooden crutch", typeof(obj_item_cane_crutch_wood), 5, Dm.named("time", 1.5f * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_WEAPON_MELEE)),
            Dm.@new<datum_stack_recipe>("loom", typeof(obj_structure_loom), 10, Dm.named("time", 1.5f * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_TOOLS)),
            Dm.@new<datum_stack_recipe>("mortar", typeof(obj_item_reagent_containers_cup_mortar), 3, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CHEMISTRY)),
            Dm.@new<datum_stack_recipe>("firebrand", typeof(obj_item_match_firebrand), 2, Dm.named("time", 10 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_TOOLS)),
            Dm.@new<datum_stack_recipe>("bonfire", typeof(obj_structure_bonfire), 10, Dm.named("time", 6 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_TOOLS)),
            Dm.@new<datum_stack_recipe>("easel", typeof(obj_structure_easel), 5, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_ENTERTAINMENT)),
            Dm.@new<datum_stack_recipe>("noticeboard", typeof(obj_item_wallframe_noticeboard), 1, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("fish mount", typeof(obj_item_wallframe_fish), 2, Dm.named("time", 3 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("test tube rack", typeof(obj_item_storage_test_tube_rack), 1, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CHEMISTRY)),
            null,
            Dm.@new<datum_stack_recipe_list>("pews", Dm.list(
                Dm.@new<datum_stack_recipe>("pew (middle)", typeof(obj_structure_chair_pew), 3, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("pew (left)", typeof(obj_structure_chair_pew_left), 3, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("pew (right)", typeof(obj_structure_chair_pew_right), 3, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE))
            )),
            Dm.@new<datum_stack_recipe_list>("peg limbs", Dm.list(
                Dm.@new<datum_stack_recipe>("peg arm (left)", typeof(obj_item_bodypart_arm_left_ghetto), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_MISC)),
                Dm.@new<datum_stack_recipe>("peg arm (right)", typeof(obj_item_bodypart_arm_right_ghetto), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_MISC)),
                Dm.@new<datum_stack_recipe>("peg leg (left)", typeof(obj_item_bodypart_leg_left_ghetto), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_MISC)),
                Dm.@new<datum_stack_recipe>("peg leg (right)", typeof(obj_item_bodypart_leg_right_ghetto), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_MISC))
            )),
            null
        );
        Dm.append(ref gvars_datum_init_order, "wood_recipes");
    }
}

// tg: code/game/objects/items/stacks/sheets/sheet_types.dm:408
public partial class obj_item_stack_sheet_mineral_wood : obj_item_stack_sheet_mineral
{
    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:430
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.wood_recipes);
        return dot;
    }

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:437
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (interacting_with is not mob_living_carbon_human target)
            return NONE;

        var affecting = target.get_bodypart(check_zone(user.zone_selected) as string) as obj_item_bodypart;
        if (affecting != null && Dm.truthy(Dm.bitand(affecting.bodytype, BODYTYPE_PEG))) // IS_PEG_LIMB(affecting)
        {
            if (ReferenceEquals(user, target))
            {
                user.visible_message(span_notice(I18n.T("[] starts to fix their [].", user, affecting.name)), span_notice(I18n.T("You start fixing [] [].", ReferenceEquals(target, user) ? "your" : Dm.str(target) + "'s", affecting.name)));
                if (!Dm.truthy(await do_after(user, 5 * SECONDS, target)))
                    return ITEM_INTERACT_FAILURE;
            }
            if (Dm.truthy(await target.item_heal(user, brute_heal: 15, burn_heal: 15, heal_message_brute: "splintering", heal_message_burn: "charring", required_bodytype: BODYTYPE_PEG)))
                await use(1);
            return ITEM_INTERACT_SUCCESS;
        }
        else
            return NONE;
    }
}

// tg: code/game/objects/items/stacks/sheets/sheet_types.dm:544
[DmPath("/obj/item/stack/sheet/cloth")]
public partial class obj_item_stack_sheet_cloth : obj_item_stack_sheet
{
    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:559
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_cellulose), 20));
    }

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:562
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.cloth_recipes);
        return dot;
    }
}

// gaps ST4: the raw cotton, durathread and wool bundles spun on the loom (datums/elements/loomable.dm).
// tg: code/game/objects/items/stacks/sheets/sheet_types.dm:609
public partial class obj_item_stack_sheet_cotton : obj_item_stack_sheet
{
    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:624
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_loomable), Dm.named("resulting_atom", loom_result), Dm.named("loom_time", loom_time)));
        return dot;
    }

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:628
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_cellulose), 20));
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:889  GLOBAL_LIST_INIT(plastic_recipes, list(...))
    public DmList plastic_recipes;

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:889  (GLOBAL_MANAGED(plastic_recipes, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalplastic_recipes()
    {
        plastic_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("plastic floor tile", typeof(obj_item_stack_tile_plastic), 1, 4, 20, Dm.named("time", 2 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_TILES)),
            Dm.@new<datum_stack_recipe>("light tram tile", typeof(obj_item_stack_thermoplastic_light), 1, 4, 20, Dm.named("time", 2 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_TILES)),
            Dm.@new<datum_stack_recipe>("dark tram tile", typeof(obj_item_stack_thermoplastic), 1, 4, 20, Dm.named("time", 2 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_TILES)),
            Dm.@new<datum_stack_recipe>("folding plastic chair", typeof(obj_structure_chair_plastic), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("plastic flaps", typeof(obj_structure_plasticflaps), 5, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("time", 4 * SECONDS), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("water bottle", typeof(obj_item_reagent_containers_cup_glass_waterbottle_empty), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
            Dm.@new<datum_stack_recipe>("large water bottle", typeof(obj_item_reagent_containers_cup_glass_waterbottle_large_empty), 3, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
            Dm.@new<datum_stack_recipe>("colo cups", typeof(obj_item_reagent_containers_cup_glass_colocup), 1, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS), Dm.named("removed_mats", Dm.list(Dm.kv(typeof(datum_material_plastic), HALF_SHEET_MATERIAL_AMOUNT)))),
            Dm.@new<datum_stack_recipe>("mannequin", typeof(obj_structure_mannequin_plastic), 25, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_ONE_PER_TURF), Dm.named("category", CAT_ENTERTAINMENT)),
            Dm.@new<datum_stack_recipe>("wet floor sign", typeof(obj_item_clothing_suit_caution), 2, Dm.named("crafting_flags", CRAFT_SKIP_MATERIALS_PARITY), Dm.named("category", CAT_EQUIPMENT)),
            Dm.@new<datum_stack_recipe>("warning cone", typeof(obj_item_clothing_head_cone), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_EQUIPMENT)),
            Dm.@new<datum_stack_recipe>("blank wall sign", typeof(obj_item_sign), 1, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("liquid cooler jug", typeof(obj_item_reagent_containers_cooler_jug), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
            Dm.@new<datum_stack_recipe>("liquid cooler", typeof(obj_structure_reagent_dispensers_water_cooler_jugless), 25, Dm.named("time", 10 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("rebellion mask", typeof(obj_item_clothing_mask_rebellion), 1, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING))
        );
        Dm.append(ref gvars_datum_init_order, "plastic_recipes");
    }
}

/*
 * Plastic
 */
// tg: code/game/objects/items/stacks/sheets/sheet_types.dm:906
public partial class obj_item_stack_sheet_plastic : obj_item_stack_sheet
{
    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:926
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.plastic_recipes);
        return dot;
    }
}

/*
 * Paper Frames
 */
public partial class datum_controller_global_vars
{
    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:933  GLOBAL_LIST_INIT(paperframe_recipes, list(...))  (gaps ST5)
    public DmList paperframe_recipes;

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:933  (GLOBAL_MANAGED(paperframe_recipes, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalpaperframe_recipes()
    {
        paperframe_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("paper frame separator", typeof(obj_structure_window_paperframe), 2, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND | CRAFT_IS_FULLTILE), Dm.named("time", 1 * SECONDS)),
            Dm.@new<datum_stack_recipe>("paper frame door", typeof(obj_structure_mineral_door_paperframe), 3, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("time", 1 * SECONDS)),
            Dm.@new<datum_stack_recipe>("paper frame platform", typeof(obj_structure_platform_paper), 2, Dm.named("time", 3 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("trait_booster", TRAIT_QUICK_BUILD), Dm.named("trait_modifier", 0.75f), Dm.named("category", CAT_STRUCTURE))
        );
        Dm.append(ref gvars_datum_init_order, "paperframe_recipes");
    }
}

// tg: code/game/objects/items/stacks/sheets/sheet_types.dm:939
public partial class obj_item_stack_sheet_paperframes : obj_item_stack_sheet
{
    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:953
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.paperframe_recipes);
        return dot;
    }
}

// Gaps SK1 (D333): the recipe lists of the other sheets.
public partial class datum_controller_global_vars
{
    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:456  GLOBAL_LIST_INIT(bamboo_recipes, list(...))
    public DmList bamboo_recipes;

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:456
    public virtual void InitGlobalbamboo_recipes()
    {
        bamboo_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("punji sticks trap", typeof(obj_structure_punji_sticks), 5, Dm.named("time", 3 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_EQUIPMENT)),
            Dm.@new<datum_stack_recipe>("bamboo spear", typeof(obj_item_spear_bamboospear), 25, Dm.named("time", 9 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_WEAPON_MELEE)),
            Dm.@new<datum_stack_recipe>("blow gun", typeof(obj_item_gun_syringe_blowgun), 10, Dm.named("time", 7 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_WEAPON_RANGED)),
            Dm.@new<datum_stack_recipe>("crude syringe", typeof(obj_item_reagent_containers_syringe_crude), 5, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CHEMISTRY)),
            Dm.@new<datum_stack_recipe>("rice hat", typeof(obj_item_clothing_head_costume_rice_hat), 10, Dm.named("time", 7 * SECONDS), Dm.named("crafting_flags", CRAFT_SKIP_MATERIALS_PARITY), Dm.named("category", CAT_CLOTHING)),
            null,
            Dm.@new<datum_stack_recipe>("bamboo stool", typeof(obj_structure_chair_stool_bamboo), 2, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("bamboo mat piece", typeof(obj_item_stack_tile_bamboo), 1, 4, 20, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_TILES)),
            null,
            Dm.@new<datum_stack_recipe>("bamboo platform", typeof(obj_structure_platform_bamboo), 2, Dm.named("time", 3 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("trait_booster", TRAIT_QUICK_BUILD), Dm.named("trait_modifier", 0.75f), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe_list>("bamboo benches", Dm.list(
                Dm.@new<datum_stack_recipe>("bamboo bench (middle)", typeof(obj_structure_chair_sofa_bamboo), 3, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("bamboo bench (left)", typeof(obj_structure_chair_sofa_bamboo_left), 3, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)),
                Dm.@new<datum_stack_recipe>("bamboo bench (right)", typeof(obj_structure_chair_sofa_bamboo_right), 3, Dm.named("time", 1 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)))));
        Dm.append(ref gvars_datum_init_order, "bamboo_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:505  GLOBAL_LIST_INIT(cloth_recipes, list(...))
    public DmList cloth_recipes;

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:505
    public virtual void InitGlobalcloth_recipes()
    {
        cloth_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("white jumpskirt", typeof(obj_item_clothing_under_color_jumpskirt_white), 3, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("white jumpsuit", typeof(obj_item_clothing_under_color_white), 3, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("white shoes", typeof(obj_item_clothing_shoes_sneakers_white), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("white scarf", typeof(obj_item_clothing_neck_scarf), 1, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("white bandana", typeof(obj_item_clothing_mask_bandana_white), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            null,
            Dm.@new<datum_stack_recipe>("backpack", typeof(obj_item_storage_backpack), 4, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
            Dm.@new<datum_stack_recipe>("satchel", typeof(obj_item_storage_backpack_satchel), 4, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
            Dm.@new<datum_stack_recipe>("messenger bag", typeof(obj_item_storage_backpack_messenger), 4, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
            Dm.@new<datum_stack_recipe>("duffel bag", typeof(obj_item_storage_backpack_duffelbag), 6, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
            null,
            Dm.@new<datum_stack_recipe>("plant bag", typeof(obj_item_storage_bag_plants), 4, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
            Dm.@new<datum_stack_recipe>("book bag", typeof(obj_item_storage_bag_books), 4, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
            Dm.@new<datum_stack_recipe>("mining satchel", typeof(obj_item_storage_bag_ore), 4, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
            Dm.@new<datum_stack_recipe>("chemistry bag", typeof(obj_item_storage_bag_chemistry), 4, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
            Dm.@new<datum_stack_recipe>("bio bag", typeof(obj_item_storage_bag_bio), 4, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
            Dm.@new<datum_stack_recipe>("science bag", typeof(obj_item_storage_bag_xeno), 4, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
            Dm.@new<datum_stack_recipe>("construction bag", typeof(obj_item_storage_bag_construction), 4, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
            null,
            Dm.@new<datum_stack_recipe>("improvised gauze", typeof(obj_item_stack_medical_wrap_gauze_improvised), 1, 2, 6, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_TOOLS)),
            Dm.@new<datum_stack_recipe>("rag", typeof(obj_item_rag), 1, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CHEMISTRY)),
            Dm.@new<datum_stack_recipe>("bedsheet", typeof(obj_item_bedsheet), 3, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("double bedsheet", typeof(obj_item_bedsheet_double), 6, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("empty sandbag", typeof(obj_item_emptysandbag), 4, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
            null,
            Dm.@new<datum_stack_recipe>("fingerless gloves", typeof(obj_item_clothing_gloves_fingerless), 1, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("white gloves", typeof(obj_item_clothing_gloves_color_white), 3, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("white softcap", typeof(obj_item_clothing_head_soft_mime), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("white beanie", typeof(obj_item_clothing_head_beanie), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            null,
            Dm.@new<datum_stack_recipe>("blindfold", typeof(obj_item_clothing_glasses_blindfold), 2, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_ENTERTAINMENT)),
            null,
            Dm.@new<datum_stack_recipe>("19x19 canvas", typeof(obj_item_canvas_nineteen_nineteen), 3, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_ENTERTAINMENT)),
            Dm.@new<datum_stack_recipe>("23x19 canvas", typeof(obj_item_canvas_twentythree_nineteen), 4, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_ENTERTAINMENT)),
            Dm.@new<datum_stack_recipe>("23x23 canvas", typeof(obj_item_canvas_twentythree_twentythree), 5, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_ENTERTAINMENT)),
            Dm.@new<datum_stack_recipe>("pillow", typeof(obj_item_pillow), 3, Dm.named("category", CAT_FURNITURE)));
        Dm.append(ref gvars_datum_init_order, "cloth_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:572  GLOBAL_LIST_INIT(durathread_recipes, list(...))
    public DmList durathread_recipes;

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:572
    public virtual void InitGlobaldurathread_recipes()
    {
        durathread_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("durathread jumpsuit", typeof(obj_item_clothing_under_misc_durathread), 4, Dm.named("time", 4 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("durathread beret", typeof(obj_item_clothing_head_beret_durathread), 2, Dm.named("time", 4 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("durathread beanie", typeof(obj_item_clothing_head_beanie_durathread), 2, Dm.named("time", 4 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("durathread bandana", typeof(obj_item_clothing_mask_bandana_durathread), 1, Dm.named("time", 2.5f * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)));
        Dm.append(ref gvars_datum_init_order, "durathread_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:650  GLOBAL_LIST_INIT(cardboard_recipes, list(...))
    public DmList cardboard_recipes;

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:650
    public virtual void InitGlobalcardboard_recipes()
    {
        cardboard_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("box", typeof(obj_item_storage_box), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
            Dm.@new<datum_stack_recipe>("cardborg suit", typeof(obj_item_clothing_suit_costume_cardborg), 3, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("cardborg helmet", typeof(obj_item_clothing_head_costume_cardborg), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("large box", typeof(obj_structure_closet_cardboard), 4, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_CONTAINERS)),
            Dm.@new<datum_stack_recipe>("medium box", typeof(obj_structure_closet_crate_cardboard), 4, Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_CONTAINERS)),
            Dm.@new<datum_stack_recipe>("cardboard cutout", typeof(obj_item_cardboard_cutout), 5, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_ENTERTAINMENT)),
            null,
            Dm.@new<datum_stack_recipe>("pizza box", typeof(obj_item_pizzabox), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
            Dm.@new<datum_stack_recipe>("folder", typeof(obj_item_folder), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
            null,
            Dm.@new<datum_stack_recipe_list>("fancy boxes", Dm.list(
                Dm.@new<datum_stack_recipe>("donut box", typeof(obj_item_storage_fancy_donut_box), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("egg box", typeof(obj_item_storage_fancy_egg_box), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("donk-pockets box", typeof(obj_item_storage_box_donkpockets), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("donk-pockets spicy box", typeof(obj_item_storage_box_donkpockets_donkpocketspicy), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("donk-pockets teriyaki box", typeof(obj_item_storage_box_donkpockets_donkpocketteriyaki), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("donk-pockets pizza box", typeof(obj_item_storage_box_donkpockets_donkpocketpizza), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("donk-pockets berry box", typeof(obj_item_storage_box_donkpockets_donkpocketberry), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("donk-pockets honk box", typeof(obj_item_storage_box_donkpockets_donkpockethonk), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("monkey cube box", typeof(obj_item_storage_box_monkeycubes), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("nugget box", typeof(obj_item_storage_fancy_nugget_box), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("red nugget box", typeof(obj_item_storage_fancy_nugget_box_red), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("red wing box", typeof(obj_item_storage_fancy_wing_box), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("red fry box", typeof(obj_item_storage_fancy_fry_box), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("drinking glasses box", typeof(obj_item_storage_box_drinkingglasses), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("paper cups box", typeof(obj_item_storage_box_cups), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("cigar case", typeof(obj_item_storage_fancy_cigarettes_cigars), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                null,
                Dm.@new<datum_stack_recipe>("lethal ammo box", typeof(obj_item_storage_box_lethalshot), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("rubber shot ammo box", typeof(obj_item_storage_box_rubbershot), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("bean bag ammo box", typeof(obj_item_storage_box_beanbag), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("flashbang box", typeof(obj_item_storage_box_flashbangs), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("flashes box", typeof(obj_item_storage_box_flashes), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("handcuffs box", typeof(obj_item_storage_box_handcuffs), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("ID card box", typeof(obj_item_storage_box_ids), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("PDA box", typeof(obj_item_storage_box_pdas), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                null,
                Dm.@new<datum_stack_recipe>("pillbottle box", typeof(obj_item_storage_box_pillbottles), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("medical gels box", typeof(obj_item_storage_box_medigels), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("beaker box", typeof(obj_item_storage_box_beakers), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("syringe box", typeof(obj_item_storage_box_syringes), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("latex gloves box", typeof(obj_item_storage_box_gloves), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("sterile masks box", typeof(obj_item_storage_box_masks), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("body bag box", typeof(obj_item_storage_box_bodybags), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("prescription glasses box", typeof(obj_item_storage_box_rxglasses), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("medipen box", typeof(obj_item_storage_box_medipens), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("DNA injectors box", typeof(obj_item_storage_box_injectors), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("oxygen tank box", typeof(obj_item_storage_box_emergencytank), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("extended oxygen tank box", typeof(obj_item_storage_box_engitank), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                null,
                Dm.@new<datum_stack_recipe>("survival box", typeof(obj_item_storage_box_survival_crafted), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("extended tank survival box", typeof(obj_item_storage_box_survival_engineer_crafted), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("disk box", typeof(obj_item_storage_box_disks), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("light tubes box", typeof(obj_item_storage_box_lights_tubes), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("light bulbs box", typeof(obj_item_storage_box_lights_bulbs), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("mixed lights box", typeof(obj_item_storage_box_lights_mixed), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("mousetraps box", typeof(obj_item_storage_box_mousetraps), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("candle box", typeof(obj_item_storage_fancy_candle_box), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)),
                Dm.@new<datum_stack_recipe>("bandage box", typeof(obj_item_storage_box_bandages), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CONTAINERS)))),
            null);
        Dm.append(ref gvars_datum_init_order, "cardboard_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:775  GLOBAL_LIST_INIT(bronze_recipes, list(...))
    public DmList bronze_recipes;

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:775
    public virtual void InitGlobalbronze_recipes()
    {
        bronze_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("wall gear", typeof(obj_structure_girder_bronze), 2, Dm.named("time", 2 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("clockwork platform", typeof(obj_structure_platform_bronze), 2, Dm.named("time", 3 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("trait_booster", TRAIT_QUICK_BUILD), Dm.named("trait_modifier", 0.75f), Dm.named("category", CAT_STRUCTURE)),
            null,
            Dm.@new<datum_stack_recipe>("directional bronze window", typeof(obj_structure_window_bronze_unanchored), Dm.named("time", 0), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ON_SOLID_GROUND | CRAFT_CHECK_DIRECTION), Dm.named("category", CAT_WINDOWS)),
            Dm.@new<datum_stack_recipe>("fulltile bronze window", typeof(obj_structure_window_bronze_fulltile_unanchored), 2, Dm.named("time", 0), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ON_SOLID_GROUND | CRAFT_IS_FULLTILE), Dm.named("category", CAT_WINDOWS)),
            Dm.@new<datum_stack_recipe>("pinion airlock assembly", typeof(obj_structure_door_assembly_door_assembly_bronze), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
            Dm.@new<datum_stack_recipe>("bronze pinion airlock assembly", typeof(obj_structure_door_assembly_door_assembly_bronze_seethru), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_DOORS)),
            Dm.@new<datum_stack_recipe>("bronze floor tile", typeof(obj_item_stack_tile_bronze), 1, 4, 20, Dm.named("crafting_flags", NONE), Dm.named("category", CAT_TILES)),
            Dm.@new<datum_stack_recipe>("bronze hat", typeof(obj_item_clothing_head_costume_bronze), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("bronze suit", typeof(obj_item_clothing_suit_costume_bronze), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            Dm.@new<datum_stack_recipe>("bronze boots", typeof(obj_item_clothing_shoes_bronze), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_CLOTHING)),
            null,
            Dm.@new<datum_stack_recipe>("bronze chair", typeof(obj_structure_chair_bronze), 1, Dm.named("time", 0), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("category", CAT_FURNITURE)));
        Dm.append(ref gvars_datum_init_order, "bronze_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:986  GLOBAL_LIST_INIT(pizza_sheet_recipes, list(...))
    public DmList pizza_sheet_recipes;

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:986
    public virtual void InitGlobalpizza_sheet_recipes()
    {
        pizza_sheet_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("huge pizza", typeof(obj_structure_platform_pizza), 2, Dm.named("time", 3 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND), Dm.named("trait_booster", TRAIT_QUICK_BUILD), Dm.named("trait_modifier", 0.75f), Dm.named("category", CAT_STRUCTURE)));
        Dm.append(ref gvars_datum_init_order, "pizza_sheet_recipes");
    }

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:1019  GLOBAL_LIST_INIT(hauntium_recipes, list(...))
    public DmList hauntium_recipes;

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:1019
    public virtual void InitGlobalhauntium_recipes()
    {
        hauntium_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("hauntium tile", typeof(obj_item_stack_tile_hauntium), 1, 4, 20, Dm.named("time", 2 * SECONDS), Dm.named("crafting_flags", NONE), Dm.named("category", CAT_TILES)));
        Dm.append(ref gvars_datum_init_order, "hauntium_recipes");
    }
}

/*
 * Bamboo
 */
// tg: code/game/objects/items/stacks/sheets/sheet_types.dm:474
public partial class obj_item_stack_sheet_mineral_bamboo : obj_item_stack_sheet_mineral
{
    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:495
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.bamboo_recipes);
        return dot;
    }
}

// tg: code/game/objects/items/stacks/sheets/sheet_types.dm:578
public partial class obj_item_stack_sheet_durathread : obj_item_stack_sheet
{
    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:592
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(
            typeof(datum_element_slapcrafting),
            Dm.named("slapcraft_recipes", TgState.Current.obj_item_stack_sheet_durathread__Initialize__slapcraft_recipe_list)));
        return dot;
    }

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:601
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.durathread_recipes);
        return dot;
    }

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:605
    public override async DmTask used_in_craft(atom result = null, datum_crafting_recipe current_recipe = null)
    {
        await base.used_in_craft(result, current_recipe);
        result.set_armor_rating(CONSUME, max(50, result.get_armor_rating(CONSUME)));
    }
}

/*
 * Cardboard
 */
// tg: code/game/objects/items/stacks/sheets/sheet_types.dm:720
public partial class obj_item_stack_sheet_cardboard : obj_item_stack_sheet
{
    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:736
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(
            typeof(datum_element_slapcrafting),
            Dm.named("slapcraft_recipes", TgState.Current.obj_item_stack_sheet_cardboard__Initialize__slapcraft_recipe_list)));
        return dot;
    }

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:745
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.cardboard_recipes);
        return dot;
    }

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:752
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_stamp_clown && loc is not obj_item_storage)
        {
            object droploc = drop_location();
            if (!Dm.truthy(await use(1)))
                return ITEM_INTERACT_BLOCKING;
            playsound(tool, Dm.resource("sound/items/bikehorn.ogg"), 50, TRUE, -1);
            to_chat(user, span_notice(I18n.T("You stamp the cardboard! It's a clown box! Honk!")));
            Dm.@new<obj_item_storage_box_clown>(droploc); //bugfix
            return ITEM_INTERACT_SUCCESS;
        }
        if (tool is obj_item_stamp_chameleon && loc is not obj_item_storage)
        {
            object droploc = drop_location();
            if (!Dm.truthy(await use(1)))
                return ITEM_INTERACT_BLOCKING;
            to_chat(user, span_notice(I18n.T("You stamp the cardboard in a sinister way.")));
            Dm.@new<obj_item_storage_box_syndie_kit>(droploc);
            return ITEM_INTERACT_SUCCESS;
        }
        return await base.item_interaction(user, tool, modifiers, __extra);
    }
}

/*
 * Bronze
 */
// tg: code/game/objects/items/stacks/sheets/sheet_types.dm:791
public partial class obj_item_stack_sheet_bronze : obj_item_stack_sheet
{
    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:816
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.bronze_recipes);
        return dot;
    }
}

/*
 * Bones
 */
// tg: code/game/objects/items/stacks/sheets/sheet_types.dm:850
public partial class obj_item_stack_sheet_bone : obj_item_stack_sheet
{
    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:867
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // As bone and sinew have just a little too many recipes for this, we'll just split them up.
        // Sinew slapcrafting will mostly-sinew recipes, and bones will have mostly-bones recipes.
        Waiting.NoSleep(AddElement(
            typeof(datum_element_slapcrafting),
            Dm.named("slapcraft_recipes", TgState.Current.obj_item_stack_sheet_bone__Initialize__slapcraft_recipe_list)));
        return dot;
    }
}

/*
 * Pizza
 */
// tg: code/game/objects/items/stacks/sheets/sheet_types.dm:993
public partial class obj_item_stack_sheet_pizza : obj_item_stack_sheet
{
    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:1005
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.pizza_sheet_recipes);
        return dot;
    }
}

/*
 * Hauntium
 */
// tg: code/game/objects/items/stacks/sheets/sheet_types.dm:1027
public partial class obj_item_stack_sheet_hauntium : obj_item_stack_sheet
{
    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:1023
    public override object get_main_recipes()
    {
        DmList dot = base.get_main_recipes() as DmList;
        Dm.append(ref dot, GLOB.hauntium_recipes);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:594  var/static/list/slapcraft_recipe_list = list(/datum/crafting_recipe/durathread_helmet, /datum/crafting_recipe/durathread_vest)
    internal DmList obj_item_stack_sheet_durathread__Initialize__slapcraft_recipe_list = Dm.list(typeof(datum_crafting_recipe_durathread_helmet), typeof(datum_crafting_recipe_durathread_vest));

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:738  var/static/list/slapcraft_recipe_list = list(/datum/crafting_recipe/cardboard_id)
    internal DmList obj_item_stack_sheet_cardboard__Initialize__slapcraft_recipe_list = Dm.list(typeof(datum_crafting_recipe_cardboard_id));

    // tg: code/game/objects/items/stacks/sheets/sheet_types.dm:871  var/static/list/slapcraft_recipe_list = list(...)
    internal DmList obj_item_stack_sheet_bone__Initialize__slapcraft_recipe_list = Dm.list(
        typeof(datum_crafting_recipe_bonearmor),
        typeof(datum_crafting_recipe_boneaxe),
        typeof(datum_crafting_recipe_bonedagger),
        typeof(datum_crafting_recipe_bonespear),
        typeof(datum_crafting_recipe_bracers),
        typeof(datum_crafting_recipe_skullhelm));
}
