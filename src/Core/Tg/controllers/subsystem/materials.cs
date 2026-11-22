// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/materials.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 2 (materials and stacks): the whole file. The var block is generated (D111) but for the type's defaults and the
// list initializers of base_stack_recipes and rigid_stack_recipes, written here (the recipes' named arguments).

/*! How material datums work
Materials are now instanced datums, with an associative list of them being kept in SSmaterials. We only instance the materials once and then re-use these instances for everything.

These materials call on_applied() on whatever item they are applied to, common effects are adding components, changing color and changing description. This allows us to differentiate items based on the material they are made out of.area

*/
// tg: code/controllers/subsystem/materials.dm:7 SUBSYSTEM_DEF(materials)
public partial class datum_controller_subsystem_materials : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(materials) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSmaterials)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/materials.dm:7
    public override async DmTask New(params object[] __extra)
    {
        if (SSmaterials != this)
        {
            if (SSmaterials is datum_controller_subsystem_materials)
            {
                Recover();
                qdel(SSmaterials);
            }
            SSmaterials = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/materials.dm:8 (the type's own var defaults: a hand-written var block, types-converter.md 3.2)
    public datum_controller_subsystem_materials()
    {
        name = "Materials";
        ss_flags = SS_NO_FIRE | SS_NO_INIT;
    }

    // tg: code/controllers/subsystem/materials.dm:19  var/list/base_stack_recipes = list(...) and :29 rigid_stack_recipes
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        /// List of stackcrafting recipes for materials using base recipes
        base_stack_recipes = Dm.list(
            Dm.@new<datum_stack_recipe>("Chair", typeof(obj_structure_chair_greyscale), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND | CRAFT_SKIP_MATERIALS_PARITY), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("Toilet", typeof(obj_structure_toilet_greyscale), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND | CRAFT_SKIP_MATERIALS_PARITY), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("Sink Frame", typeof(obj_item_wallframe_sinkframe), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND | CRAFT_SKIP_MATERIALS_PARITY), Dm.named("category", CAT_FURNITURE)),
            Dm.@new<datum_stack_recipe>("Material floor tile", typeof(obj_item_stack_tile_material), 1, 4, 20, Dm.named("crafting_flags", CRAFT_SKIP_MATERIALS_PARITY), Dm.named("category", CAT_TILES)),
            Dm.@new<datum_stack_recipe>("Material airlock assembly", typeof(obj_structure_door_assembly_door_assembly_material), 4, Dm.named("time", 5 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND | CRAFT_SKIP_MATERIALS_PARITY), Dm.named("category", CAT_DOORS)),
            Dm.@new<datum_stack_recipe>("Material platform", typeof(obj_structure_platform_material), 2, Dm.named("time", 3 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND | CRAFT_SKIP_MATERIALS_PARITY), Dm.named("trait_booster", TRAIT_QUICK_BUILD), Dm.named("trait_modifier", 0.75f), Dm.named("category", CAT_STRUCTURE)),
            Dm.@new<datum_stack_recipe>("Carving block", typeof(obj_structure_carving_block), 5, Dm.named("time", 3 * SECONDS), Dm.named("crafting_flags", CRAFT_CHECK_DENSITY | CRAFT_ONE_PER_TURF | CRAFT_ON_SOLID_GROUND | CRAFT_SKIP_MATERIALS_PARITY), Dm.named("category", CAT_STRUCTURE))
        );
        /// List of stackcrafting recipes for materials using rigid recipes (none yet)
        rigid_stack_recipes = Dm.list();
    }

    ///Ran on initialize, populated the materials and material dictionaries with their appropriate vars (See these variables for more info)
    // tg: code/controllers/subsystem/materials.dm:43
    public virtual async DmTask initialize_materials()
    {
        materials = Dm.list();
        flat_materials = Dm.list();
        materials_by_type = Dm.list();
        material_combos = Dm.list();

        properties = Dm.list();
        // GAP(M3 P1 -> M5 slice 2): generated-only types skipped until translated (DV25; types-converter.md 8).
        foreach (Type property_type in Dm.each<Type>(typesof_written(valid_subtypesof(typeof(datum_material_property)))))
            properties[Dm.initial(property_type, (datum_material_property p) => p.id)] = Dm.@new(property_type);

        requirements = Dm.list();
        // GAP(M3 P1 -> M5 slice 2): generated-only types skipped until translated (DV25; types-converter.md 8).
        foreach (Type requirement_type in Dm.each<Type>(typesof_written(valid_subtypesof(typeof(datum_material_requirement)))))
            requirements[requirement_type] = Dm.@new(requirement_type);

        material_slots = Dm.list();
        // GAP(M3 P1 -> M5 slice 2): generated-only types skipped until translated (DV25; types-converter.md 8): the
        // weapon slots of generic.dm and the spear and mace slots come with their weapons.
        foreach (Type slot_type in Dm.each<Type>(typesof_written(valid_subtypesof(typeof(datum_material_slot)))))
            material_slots[slot_type] = Dm.@new(slot_type);

        // GAP(M3 P1 -> M5 slice 2): generated-only types skipped until translated (DV25; types-converter.md 8): the meat,
        // pizza and hauntium materials (meat.dm, pizza.dm, hauntium.dm: food and the hauntium sheet, M8 and later).
        foreach (Type mat_type in Dm.each<Type>(typesof_written(valid_subtypesof(typeof(datum_material)))))
        {
            if (Dm.truthy(Dm.bitand(Dm.initial(mat_type, (datum_material m) => m.init_flags), MATERIAL_INIT_MAPLOAD)))
                await initialize_material(mat_type);
        }

        dimensional_themes = await init_subtypes_w_path_keys(typeof(datum_dimension_theme)) as DmList;
    }

    /** Creates and caches a material datum.
     *	The first argument is the type of material to initialize, the rest are passed to the material's init
     */
    // tg: code/controllers/subsystem/materials.dm:70
    public virtual async DmTask<object> initialize_material(Type mat_type = null, params object[] __extra)
    {
        DmList args = Dm.args(mat_type, __extra);
        object mat_id = mat_type;
        if (Dm.truthy(Dm.bitand(Dm.initial(mat_type, (datum_material m) => m.init_flags), MATERIAL_INIT_BESPOKE)))
            mat_id = Waiting.NoSleep(Dm.Call(this, nameof(get_id_from_args), Dm.arglist(Dm.add(Dm.list(mat_type), args.Copy(2)))));

        datum_material mat_ref = Dm.@new(mat_type) as datum_material;
        if (!Dm.truthy(await Dm.Call(mat_ref, nameof(datum_material.Initialize), Dm.arglist(Dm.add(Dm.list(mat_id), args.Copy(2))))))
            return null;

        mat_id = mat_ref.id;
        materials[mat_id] = mat_ref;
        Dm.append(ref flat_materials, mat_ref);
        object t__1 = materials_by_type[mat_type];
        Dm.append(ref t__1, Dm.list(mat_ref));
        materials_by_type[mat_type] = t__1;
        if (Dm.truthy(this._listen_lookup?[COMSIG_MATERIALS_INIT_MAT]))
            this._SendSignal(COMSIG_MATERIALS_INIT_MAT, Dm.list(this, mat_ref));
        return mat_ref;
    }

    /**
     * Fetches a cached material singleton when passed sufficient arguments.
     * Arguments:
     * - The first argument is a material datum, text string, or material type.
     * - [Material datums][/datum/material] are assumed to be references to the cached datum and are returned
     * - Text is assumed to be the text ID of a material and the corresponding material is fetched from the cache
     * - A material type is checked for bespokeness:
     *   - If the material type is not bespoke the type is assumed to be the id for a material and the corresponding material is loaded from the cache.
     *   - If the material type is bespoke a text ID is generated from the arguments list and used to load a material datum from the cache.
     * - The following elements are used to generate bespoke IDs
     */
    // tg: code/controllers/subsystem/materials.dm:97
    public virtual async DmTask<object> get_material(object material_type = null, params object[] __extra)
    {
        object dot = null;

        if (!Dm.truthy(materials))
            await initialize_materials();

        if (material_type is datum_material)
            return material_type; // We are assuming here that the only thing allowed to create material datums is [/datum/controller/subsystem/materials/proc/initialize_material]

        if (istext(material_type)) // Handle text id
        {
            dot = materials[material_type];
            if (!Dm.truthy(dot))
                // WARNING(MSG) (__DEFINES/logging.dm), expanded inline: warning("[MSG] in [__FILE__] at line [__LINE__] src: [src] usr: [usr].")
                warning(Dm.text("[] in [] at line [] src: [] usr: [].", Dm.text("Attempted to fetch material ref with invalid text id '[]'", material_type), "code/controllers/subsystem/materials.dm", 109, this, usr));
            return dot;
        }

        if (!ispath(material_type, typeof(datum_material)))
            throw CRASH(Dm.text("Attempted to fetch material ref with invalid key []", material_type), "code/controllers/subsystem/materials.dm", 113);

        if (!Dm.truthy(Dm.bitand(Dm.initial((Type)material_type, (datum_material m) => m.init_flags), MATERIAL_INIT_BESPOKE)))
        {
            dot = materials[material_type];
            if (!Dm.truthy(dot))
                // WARNING(MSG) (__DEFINES/logging.dm), expanded inline
                warning(Dm.text("[] in [] at line [] src: [] usr: [].", Dm.text("Attempted to fetch reference to an abstract material with key []", material_type), "code/controllers/subsystem/materials.dm", 118, this, usr));
            return dot;
        }

        DmList args = Dm.args(material_type, __extra);
        object material_id = Waiting.NoSleep(Dm.Call(this, nameof(get_id_from_args), Dm.arglist(Dm.add(Dm.list(material_type), args.Copy(2)))));
        object or__1 = materials[material_id];
        return Dm.truthy(or__1) ? or__1 : await Dm.Call(this, nameof(initialize_material), Dm.arglist(Dm.add(Dm.list(material_type), args.Copy(2))));
    }

    /// Fetches all materials that match a flag, or that don't have a flag if the passed flag is negative
    // tg: code/controllers/subsystem/materials.dm:125
    public virtual object get_materials_by_flag(object mat_flag = null)
    {
        if (mat_flag == null || Dm.eq(mat_flag, MATERIAL_CLASS_ANY)) // Wildcard for "any" material
            return flat_materials;

        // Lazy cache for access speed on repeated calls
        if (!Dm.truthy(get_materials_by_flag__materials_by_flag))
            get_materials_by_flag__materials_by_flag = Dm.list();

        DmList result = get_materials_by_flag__materials_by_flag[Dm.text("[]", mat_flag)] as DmList;
        if (Dm.truthy(result))
            return result;

        result = Dm.list();
        foreach (datum_material material in Dm.each<datum_material>(flat_materials))
        {
            if (Dm.num(mat_flag) > 0)
            {
                if (Dm.truthy(Dm.bitand(material.mat_flags, mat_flag)))
                    Dm.append(ref result, material);
            }
            else if (!Dm.truthy(Dm.bitand(material.mat_flags, (-Dm.num(mat_flag)))))
                Dm.append(ref result, material);
        }

        get_materials_by_flag__materials_by_flag[Dm.text("[]", mat_flag)] = result;
        return result;
    }

    // tg: code/controllers/subsystem/materials.dm:130  var/static/list/materials_by_flag (a proc-level static, rules 10.6)
    private static ref DmList get_materials_by_flag__materials_by_flag => ref TgState.Current.datum_controller_subsystem_materials__get_materials_by_flag__materials_by_flag;

    /**
     * I'm not going to lie, this was swiped from [SSdcs][/datum/controller/subsystem/processing/dcs].
     * Credit does to ninjanomnom
     *
     * Generates an id for bespoke ~~elements~~ materials when given the argument list
     * Generating the id here is a bit complex because we need to support named arguments
     * Named arguments can appear in any order and we need them to appear after ordered arguments
     * We assume that no one will pass in a named argument with a value of null
     **/
    // tg: code/controllers/subsystem/materials.dm:158
    // M5 slice 2 (Appendix A): mat_type is object, not the declared /datum/material: its callers pass the material's type
    // (initialize_material :73, get_material :121), and initial(mat_type.id) reads the type's default (rules 10.3).
    public virtual object get_id_from_args(object mat_type = null, params object[] __extra)
    {
        DmList args = Dm.args(mat_type, __extra);
        object or__1 = Dm.initial(mat_type, (datum_material m) => m.id);
        DmList fullid = Dm.list(Dm.text("[]", Dm.truthy(or__1) ? or__1 : mat_type));
        DmList named_arguments = Dm.list();
        for (float i = 2, i__end = length(args); i <= i__end; i++)
        {
            object key = args[i];
            object value = null;
            if (istext(key))
                value = args[key];
            if (!(istext(key) || isnum(key)))
                key = REF(key);
            key = Dm.text("[]", key); // Key is stringified so numbers don't break things
            if (value != null)
            {
                if (!(istext(value) || isnum(value)))
                    value = REF(value);
                named_arguments[Dm.text("[]", key)] = value;
            }
            else
                Dm.append(ref fullid, Dm.text("[]", key));
        }

        if (Dm.truthy(length(named_arguments)))
        {
            named_arguments = sort_list(named_arguments) as DmList;
            Dm.append(ref fullid, named_arguments);
        }
        return list2params(fullid);
    }


    /// Returns a list to be used as an object's custom_materials. Lists will be cached and re-used based on the parameters.
    // tg: code/controllers/subsystem/materials.dm:183
    public virtual async DmTask<object> get_material_set_cache(DmList materials_declaration = null, float? multiplier = null)
    {
        multiplier ??= 1;
        if (!Dm.truthy(LAZYLEN(materials_declaration)))
            return null; // If we get a null we pass it right back, we don't want to generate stack traces just because something is clearing out its materials list.

        if (!Dm.truthy(material_combos))
            await initialize_materials();
        DmList combo_params = Dm.list();
        foreach (object mat in Dm.each(materials_declaration))
            Dm.append(ref combo_params, Dm.text("[]=[]", mat is datum_material m__1 ? m__1.id : mat, OPTIMAL_COST(Dm.num(materials_declaration[mat]) * Dm.num(multiplier))));
        sortTim(combo_params, "/proc/" + nameof(GlobalProcs.cmp_text_asc)); // We have to sort now in case the declaration was not in order
        string combo_index = combo_params.Join("-");
        DmList combo = material_combos[combo_index] as DmList;
        if (!Dm.truthy(combo))
        {
            combo = Dm.list();
            foreach (object mat in Dm.each(materials_declaration))
                combo[await get_material(mat)] = OPTIMAL_COST(Dm.num(materials_declaration[mat]) * Dm.num(multiplier));
            material_combos[combo_index] = combo;
        }
        return combo;
    }

    /// Returns all materials that fit a requirement datum
    // tg: code/controllers/subsystem/materials.dm:203
    public virtual object get_materials_by_req(object requirement = null)
    {
        DmList dot = null;
        if (ispath(requirement))
        {
            if (!Dm.truthy(requirements[requirement]))
                throw CRASH(Dm.text("Invalid material requirement passed into get_materials_by_req: []", requirement), "code/controllers/subsystem/materials.dm", 206);
            requirement = requirements[requirement];
        }

        dot = Dm.list();
        foreach (datum_material material in Dm.each<datum_material>(get_materials_by_flag(((datum_material_requirement)requirement).required_flags))) // If none are set, this returns all materials
        {
            if (Dm.truthy(((datum_material_requirement)requirement).valid_material(material)))
                Dm.append(ref dot, material);
        }
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/materials.dm:7  SUBSYSTEM_DEF(materials): GLOBAL_REAL(SSmaterials, /datum/controller/subsystem/materials)
    internal datum_controller_subsystem_materials SSmaterials;

    // tg: code/controllers/subsystem/materials.dm:130  var/static/list/materials_by_flag
    internal DmList datum_controller_subsystem_materials__get_materials_by_flag__materials_by_flag;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_materials SSmaterials => ref TgState.Current.SSmaterials;
}
