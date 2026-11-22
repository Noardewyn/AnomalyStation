// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/girder_wall_recipes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 4: the whole file. The recipe lists name every wall, false wall and tram wall type of tg; their vars are read
// from the types' initial values (tg's `wall_type::sheet_type`), so a type that is not translated (an inert mineral wall, a
// tram wall) gives its recipe as in tg and builds that inert thing (D148, D132).

public partial class datum_controller_global_vars
{
    // tg: code/_globalvars/lists/girder_wall_recipes.dm:1  GLOBAL_LIST_INIT(main_girder_wall_recipes, init_main_girder_wall_recipes())
    public DmList main_girder_wall_recipes;

    // tg: code/_globalvars/lists/girder_wall_recipes.dm:1  (GLOBAL_MANAGED, __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmain_girder_wall_recipes()
    {
        main_girder_wall_recipes = init_main_girder_wall_recipes() as DmList;
        Dm.append(ref gvars_datum_init_order, "main_girder_wall_recipes");
    }

    // tg: code/_globalvars/lists/girder_wall_recipes.dm:2  GLOBAL_LIST_INIT(material_girder_wall_recipes, init_material_girder_wall_recipes())
    public DmList material_girder_wall_recipes;

    // tg: code/_globalvars/lists/girder_wall_recipes.dm:2  (GLOBAL_MANAGED, __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmaterial_girder_wall_recipes()
    {
        material_girder_wall_recipes = init_material_girder_wall_recipes() as DmList;
        Dm.append(ref gvars_datum_init_order, "material_girder_wall_recipes");
    }
}

public static partial class GlobalProcs
{
    /// Initializes the main, false and tram wall types using three lists of wall types.
    // tg: code/_globalvars/lists/girder_wall_recipes.dm:5
    public static object init_main_girder_wall_recipes()
    {
        return create_girder_wall_recipes(
            wall_types: Dm.list(
                typeof(turf_closed_wall),
                typeof(turf_closed_wall_metal_foam_base),
                typeof(turf_closed_wall_r_wall),
                typeof(turf_closed_wall_r_wall_plastitanium),
                typeof(turf_closed_wall_mineral_gold),
                typeof(turf_closed_wall_mineral_silver),
                typeof(turf_closed_wall_mineral_diamond),
                typeof(turf_closed_wall_mineral_bananium),
                typeof(turf_closed_wall_mineral_sandstone),
                typeof(turf_closed_wall_mineral_uranium),
                typeof(turf_closed_wall_mineral_plasma),
                typeof(turf_closed_wall_mineral_wood),
                typeof(turf_closed_wall_mineral_wood_nonmetal),
                typeof(turf_closed_wall_mineral_bamboo),
                typeof(turf_closed_wall_mineral_iron),
                typeof(turf_closed_wall_mineral_snow),
                typeof(turf_closed_wall_mineral_abductor),
                typeof(turf_closed_wall_mineral_titanium),
                typeof(turf_closed_wall_mineral_plastitanium),
                typeof(turf_closed_wall_mineral_plastitanium_wall_fill),
                typeof(turf_closed_wall_mineral_plastitanium_darkpod),
                typeof(turf_closed_wall_mineral_plastitanium_redpod),
                typeof(turf_closed_wall_mineral_plastitanium_survival),
                typeof(turf_closed_wall_mineral_cult),
                typeof(turf_closed_wall_mineral_bronze),
                typeof(turf_closed_wall_material)
            ),
            falsewall_types: Dm.list(
                typeof(obj_structure_falsewall),
                typeof(obj_structure_falsewall_reinforced),
                typeof(obj_structure_falsewall_gold),
                typeof(obj_structure_falsewall_silver),
                typeof(obj_structure_falsewall_diamond),
                typeof(obj_structure_falsewall_bananium),
                typeof(obj_structure_falsewall_sandstone),
                typeof(obj_structure_falsewall_uranium),
                typeof(obj_structure_falsewall_plasma),
                typeof(obj_structure_falsewall_wood),
                typeof(obj_structure_falsewall_bamboo),
                typeof(obj_structure_falsewall_iron),
                typeof(obj_structure_falsewall_abductor),
                typeof(obj_structure_falsewall_titanium),
                typeof(obj_structure_falsewall_plastitanium),
                typeof(obj_structure_falsewall_plastitanium),
                typeof(obj_structure_falsewall_plastitanium_wall_fill),
                typeof(obj_structure_falsewall_plastitanium_darkpod),
                typeof(obj_structure_falsewall_plastitanium_redpod),
                typeof(obj_structure_falsewall_plastitanium_survival),
                typeof(obj_structure_falsewall_material)
            ),
            tram_types: Dm.list(
                typeof(obj_structure_tram),
                typeof(obj_structure_tram_alt_gold),
                typeof(obj_structure_tram_alt_silver),
                typeof(obj_structure_tram_alt_diamond),
                typeof(obj_structure_tram_alt_bananium),
                typeof(obj_structure_tram_alt_sandstone),
                typeof(obj_structure_tram_alt_uranium),
                typeof(obj_structure_tram_alt_plasma),
                typeof(obj_structure_tram_alt_wood),
                typeof(obj_structure_tram_alt_bamboo),
                typeof(obj_structure_tram_alt_iron),
                typeof(obj_structure_tram_alt_abductor),
                typeof(obj_structure_tram_alt_titanium),
                typeof(obj_structure_tram_alt_plastitanium)
            )
        );
    }

    /// Initializes the normal and false girder material wall recipes using two lists of material wall types.
    // tg: code/_globalvars/lists/girder_wall_recipes.dm:86
    public static object init_material_girder_wall_recipes()
    {
        return create_girder_wall_recipes(
            wall_types: Dm.list(typeof(turf_closed_wall_material)),
            falsewall_types: Dm.list(typeof(obj_structure_falsewall_material))
        );
    }

    /// Returns a list of newly created [/datum/girder_wall_recipe] instances from the given wall type lists.
    // tg: code/_globalvars/lists/girder_wall_recipes.dm:93
    public static object create_girder_wall_recipes(DmList wall_types = null, DmList falsewall_types = null, DmList tram_types = null)
    {
        DmList recipes = Dm.list();

        foreach (Type wall_type in Dm.each<Type>(wall_types))
        {
            add_girder_wall_recipe(
                recipes: recipes,
                wall_type: wall_type,
                stack_type: Dm.initial(wall_type, "sheet_type"),
                stack_amount: Dm.initial(wall_type, "sheet_amount"),
                girder_type: Dm.initial(wall_type, "girder_type"),
                girder_state: Dm.initial(wall_type, "girder_state"),
                make_delay: Dm.initial(wall_type, "make_delay"),
                start_alert: I18n.T("adding plating...")
            );
        }

        foreach (Type falsewall_type in Dm.each<Type>(falsewall_types))
        {
            add_girder_wall_recipe(
                recipes: recipes,
                wall_type: falsewall_type,
                stack_type: Dm.initial(falsewall_type, "mineral"),
                stack_amount: Dm.initial(falsewall_type, "mineral_amount"),
                girder_type: Dm.initial(falsewall_type, "girder_type"),
                girder_state: GIRDER_DISPLACED,
                make_delay: 2 * SECONDS,
                start_alert: I18n.T("concealing entrance...")
            );
        }

        foreach (Type tram_type in Dm.each<Type>(tram_types))
        {
            add_girder_wall_recipe(
                recipes: recipes,
                wall_type: tram_type,
                stack_type: Dm.initial(tram_type, "mineral"),
                stack_amount: Dm.initial(tram_type, "mineral_amount"),
                girder_type: Dm.initial(tram_type, "girder_type"),
                girder_state: GIRDER_TRAM,
                make_delay: 4 * SECONDS,
                start_alert: I18n.T("adding plating...")
            );
        }

        return recipes;
    }

    /// Adds a new [/datum/girder_wall_recipe] instance to the given recipe list from the given recipe arguments.
    // tg: code/_globalvars/lists/girder_wall_recipes.dm:126
    public static void add_girder_wall_recipe(DmList recipes = null, object wall_type = null, object stack_type = null, object stack_amount = null, object girder_type = null, object girder_state = null, object make_delay = null, object start_alert = null)
    {
        if (!ispath(stack_type, typeof(obj_item_stack)))
            throw Dm.CRASH(Dm.text("Attempted to create a girder wall recipe for wall type ([]) with an invalid stack type ([])", wall_type, stack_type), "code/_globalvars/lists/girder_wall_recipes.dm", 128);

        Dm.append(ref recipes, Dm.@new<datum_girder_wall_recipe>(wall_type, stack_type, stack_amount, girder_type, girder_state, make_delay, start_alert));
    }
}
