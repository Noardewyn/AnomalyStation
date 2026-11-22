// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/typecache.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only GLOB.typecache_mob (M9 slice B: the emotes' allowed mobs), GLOB.typecache_living (M7x slice E: the flash's targets in a
// container) and GLOB.typecache_elevated_structures (gaps VI1: the germs of food on the floor); the file's other typecaches come with
// their users.

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/_globalvars/lists/typecache.dm:6  GLOBAL_LIST_INIT(typecache_mob, typecacheof(/mob))
    public DmList typecache_mob;

    // tg: code/_globalvars/lists/typecache.dm:6  (GLOBAL_MANAGED(typecache_mob, typecacheof(/mob)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaltypecache_mob()
    {
        typecache_mob = typecacheof(typeof(mob)) as DmList;
        Dm.append(ref gvars_datum_init_order, "typecache_mob");
    }

    // tg: code/_globalvars/lists/typecache.dm:8  GLOBAL_LIST_INIT(typecache_living, typecacheof(/mob/living))
    public DmList typecache_living;

    // tg: code/_globalvars/lists/typecache.dm:8  (GLOBAL_MANAGED(typecache_living, typecacheof(/mob/living)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaltypecache_living()
    {
        typecache_living = typecacheof(typeof(mob_living)) as DmList;
        Dm.append(ref gvars_datum_init_order, "typecache_living");
    }

    // tg: code/_globalvars/lists/typecache.dm:10  GLOBAL_LIST_INIT(typecache_stack, typecacheof(/obj/item/stack))
    public DmList typecache_stack;

    // tg: code/_globalvars/lists/typecache.dm:10  (GLOBAL_MANAGED(typecache_stack, typecacheof(/obj/item/stack)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaltypecache_stack()
    {
        typecache_stack = typecacheof(typeof(obj_item_stack)) as DmList;
        Dm.append(ref gvars_datum_init_order, "typecache_stack");
    }

    /// A typecache listing structures that are considered to have surfaces that you can place items on that are higher than the floor. This, of course, should be restricted to /atom/movables. This is primarily used for food decomposition code.
    // tg: code/_globalvars/lists/typecache.dm:18  GLOBAL_LIST_INIT(typecache_elevated_structures, typecacheof(list(...))) (gaps VI1: the germs)
    public DmList typecache_elevated_structures;

    // tg: code/_globalvars/lists/typecache.dm:18
    public virtual void InitGlobaltypecache_elevated_structures()
    {
        typecache_elevated_structures = typecacheof(Dm.list(
            typeof(obj_machinery_conveyor),
            typeof(obj_structure_closet),
            typeof(obj_structure_rack),
            typeof(obj_structure_table),
            //Kitchen
            typeof(obj_machinery_smartfridge),
            typeof(obj_machinery_smartfridge_drying), // Redundant, given above, but this is for the sake of explicitness.
            typeof(obj_structure_bonfire),
            typeof(obj_machinery_grill),
            typeof(obj_machinery_griddle),
            typeof(obj_machinery_deepfryer),
            typeof(obj_machinery_processor),
            typeof(obj_machinery_microwave),
            typeof(obj_machinery_oven),
            typeof(obj_machinery_stove),
            typeof(obj_machinery_gibber),
            typeof(obj_machinery_icecream_vat),
            //Botany
            typeof(obj_machinery_biogenerator),
            typeof(obj_machinery_hydroponics), // So that harvest doesn't catch germs or decompose (includes dirt piles)
            //Medbay
            typeof(obj_machinery_stasis),
            typeof(obj_machinery_chem_master), // Pills may catch germs
            typeof(obj_machinery_plumbing_pill_press)
        )) as DmList;
        Dm.append(ref gvars_datum_init_order, "typecache_elevated_structures");
    }
}
