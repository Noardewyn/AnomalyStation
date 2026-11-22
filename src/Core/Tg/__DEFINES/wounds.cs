// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/wounds.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file's global lists and procs (M6 slice 2, the wound engine; GLOB.bio_state_anatomy since M2 P2); the constants
// are generated into Defines.

public partial class datum_controller_global_vars
{
    /// A "chronological" list of wound severities, starting at the least severe.
    // tg: code/__DEFINES/wounds.dm:35  GLOBAL_LIST_INIT(wound_severities_chronological, list(...))
    public DmList wound_severities_chronological;

    // tg: code/__DEFINES/wounds.dm:35  (GLOBAL_MANAGED(wound_severities_chronological, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalwound_severities_chronological()
    {
        wound_severities_chronological = Dm.list(
            Dm.text("[]", WOUND_SEVERITY_TRIVIAL),
            Dm.text("[]", WOUND_SEVERITY_MODERATE),
            Dm.text("[]", WOUND_SEVERITY_SEVERE),
            Dm.text("[]", WOUND_SEVERITY_CRITICAL));
        Dm.append(ref gvars_datum_init_order, "wound_severities_chronological");
    }

    /// A assoc list of BIO_ define to EXTERIOR/INTERIOR defines.
    /// This is where the interior/exterior state of a given biostate is set.
    /// Note that not all biostates are guaranteed to be one of these - and in fact, many are not
    /// IMPORTANT NOTE: All keys are stored as text and must be converted via text2num
    // tg: code/__DEFINES/wounds.dm:88  GLOBAL_LIST_INIT(bio_state_anatomy, list(...))
    public DmList bio_state_anatomy;

    // tg: code/__DEFINES/wounds.dm:88  (GLOBAL_MANAGED(bio_state_anatomy, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalbio_state_anatomy()
    {
        bio_state_anatomy = Dm.list(
            Dm.kv(Dm.text("[]", BIO_WIRED), ANATOMY_EXTERIOR),
            Dm.kv(Dm.text("[]", BIO_METAL), ANATOMY_INTERIOR),
            Dm.kv(Dm.text("[]", BIO_FLESH), ANATOMY_EXTERIOR),
            Dm.kv(Dm.text("[]", BIO_BONE), ANATOMY_INTERIOR),
            Dm.kv(Dm.text("[]", BIO_CHITIN), ANATOMY_EXTERIOR));
        Dm.append(ref gvars_datum_init_order, "bio_state_anatomy");
    }

    /// A assoc list of (wound typepath -> wound_pregen_data instance). Every wound should have a pregen data.
    // tg: code/__DEFINES/wounds.dm:116  GLOBAL_LIST_INIT_TYPED(all_wound_pregen_data, /datum/wound_pregen_data, generate_wound_static_data())
    public DmList all_wound_pregen_data;

    // tg: code/__DEFINES/wounds.dm:116  (GLOBAL_MANAGED(all_wound_pregen_data, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalall_wound_pregen_data()
    {
        all_wound_pregen_data = generate_wound_static_data();
        Dm.append(ref gvars_datum_init_order, "all_wound_pregen_data");
    }

    /// A branching assoc list of (series -> list(severity -> list(typepath -> weight))). Allows you to say "I want a generic slash wound",
    /// then "Of severity 2", and get a wound of that description - via get_corresponding_wound_type()
    /// Series: A generic wound_series, such as WOUND_SERIES_BONE_BLUNT_BASIC
    /// Severity: Any wounds held within this will be of this severity.
    /// Typepath, Weight: Merely a pairing of a given typepath to its weight, held for convenience in pickweight.
    // tg: code/__DEFINES/wounds.dm:147  GLOBAL_LIST_INIT(wound_series_collections, generate_wound_series_collection())
    public DmList wound_series_collections;

    // tg: code/__DEFINES/wounds.dm:147  (GLOBAL_MANAGED(wound_series_collections, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalwound_series_collections()
    {
        wound_series_collections = generate_wound_series_collection();
        Dm.append(ref gvars_datum_init_order, "wound_series_collections");
    }

    /// A branching assoc list of (wounding_type -> list(wound_series)).
    /// Allows for determining of which wound series are caused by what.
    // tg: code/__DEFINES/wounds.dm:184  GLOBAL_LIST_INIT(wounding_types_to_series, list(...))
    public DmList wounding_types_to_series;

    // tg: code/__DEFINES/wounds.dm:184  (GLOBAL_MANAGED(wounding_types_to_series, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalwounding_types_to_series()
    {
        wounding_types_to_series = Dm.list(
            Dm.kv(WOUND_BLUNT, Dm.list(
                WOUND_SERIES_BONE_BLUNT_BASIC)),
            Dm.kv(WOUND_SLASH, Dm.list(
                WOUND_SERIES_FLESH_SLASH_BLEED)),
            Dm.kv(WOUND_BURN, Dm.list(
                WOUND_SERIES_FLESH_BURN_BASIC)),
            Dm.kv(WOUND_PIERCE, Dm.list(
                WOUND_SERIES_FLESH_PUNCTURE_BLEED)));
        Dm.append(ref gvars_datum_init_order, "wounding_types_to_series");
    }

    /// Assoc list of biotype -> ideal scar file to be used and grab stuff from.
    // tg: code/__DEFINES/wounds.dm:262  GLOBAL_LIST_INIT(biotypes_to_scar_file, list(...))
    public DmList biotypes_to_scar_file;

    // tg: code/__DEFINES/wounds.dm:262  (GLOBAL_MANAGED(biotypes_to_scar_file, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalbiotypes_to_scar_file()
    {
        biotypes_to_scar_file = Dm.list(
            Dm.kv(Dm.text("[]", BIO_FLESH), FLESH_SCAR_FILE),
            Dm.kv(Dm.text("[]", BIO_BONE), BONE_SCAR_FILE));
        Dm.append(ref gvars_datum_init_order, "biotypes_to_scar_file");
    }
}

public static partial class GlobalProcs
{
    /// Constructs [GLOB.all_wound_pregen_data] by iterating through a typecache of pregen data, ignoring abstract types, and instantiating the rest.
    // tg: code/__DEFINES/wounds.dm:119
    public static DmList generate_wound_static_data()
    {
        DmList all_pregen_data = Dm.list();

        foreach (Type iterated_path in Dm.each_cast<Type>(typecacheof(path: typeof(datum_wound_pregen_data), ignore_root_path: TRUE)))
        {
            if (Dm.truthy(Dm.initial(iterated_path, (datum_wound_pregen_data p) => p.@abstract)))
                continue;

            Type path_to_generate = Dm.initial(iterated_path, (datum_wound_pregen_data p) => p.wound_path_to_generate);
            if (all_pregen_data[path_to_generate] != null)
            {
                stack_trace(Dm.text("pre-existing pregen data for [] when [] was being considered: []. this is definitely a bug, and is probably because one of the two pregen data have the wrong wound typepath defined. [] will not be instantiated", path_to_generate, iterated_path, all_pregen_data[path_to_generate], iterated_path), "code/__DEFINES/wounds.dm", 129);

                continue;
            }

            datum_wound_pregen_data pregen_data = (datum_wound_pregen_data)Dm.@new(iterated_path);
            all_pregen_data[pregen_data.wound_path_to_generate] = pregen_data;
        }

        return all_pregen_data;
    }

    // Series -> severity -> type -> weight
    /// Generates [wound_series_collections] by iterating through all pregen_data. Refer to the mentioned list for documentation
    // tg: code/__DEFINES/wounds.dm:151
    public static DmList generate_wound_series_collection()
    {
        DmList wound_collection = Dm.list();

        foreach (Type wound_typepath in Dm.each_cast<Type>(typecacheof(typeof(datum_wound), FALSE, TRUE)))
        {
            datum_wound_pregen_data pregen_data = GLOB.all_wound_pregen_data[wound_typepath] as datum_wound_pregen_data;
            if (pregen_data == null)
                continue;

            if (Dm.truthy(pregen_data.@abstract))
            {
                stack_trace(Dm.text("somehow, a abstract wound_pregen_data instance ([]) was instantiated and made it to generate_wound_series_collection()! i literally have no idea how! please fix this!", pregen_data.type), "code/__DEFINES/wounds.dm", 162);
                continue;
            }

            object series = pregen_data.wound_series;
            DmList series_list = wound_collection[series] as DmList;
            if (series_list == null)
            {
                wound_collection[series] = Dm.list();
                series_list = wound_collection[series] as DmList;
            }

            string severity = Dm.text("[]", Dm.initial(wound_typepath, (datum_wound p) => p.severity));
            DmList severity_list = series_list[severity] as DmList;
            if (severity_list == null)
            {
                series_list[severity] = Dm.list();
                severity_list = series_list[severity] as DmList;
            }

            severity_list[wound_typepath] = pregen_data.weight;
        }

        return wound_collection;
    }

    /**
     * Searches through all wounds for any of proper type, series, and biostate, and then returns a single one via pickweight.
     * Is able to discern between, say, a flesh slash wound, and a metallic slash wound, and will return the respective one for the provided limb.
     *
     * The severity_max and severity_pick_mode args mostly exist in case you want a wound in a series that may not have your ideal severity wound, as it lets you
     * essentially set a "fallback", where if your ideal wound doesnt exist, it'll still return something, trying to get closest to your ideal severity.
     *
     * Generally speaking, if you want a critical/severe/moderate wound, you should set severity_min to WOUND_SEVERITY_MODERATE, severity_max to your ideal wound,
     * and severity_pick_mode to WOUND_PICK_HIGHEST_SEVERITY - UNLESS you for some reason want the LOWEST severity, in which case you should set
     * severity_max to the highest wound you're willing to tolerate, and severity_pick_mode to WOUND_PICK_LOWEST_SEVERITY.
     *
     * Args:
     * * wounding_type: Wounding type wounds of which we should be searching for
     * * obj/item/bodypart/part: The limb we are considering. Extremely important for biostates.
     * * severity_min: The minimum wound severity we will search for.
     * * severity_max = severity_min: The maximum wound severity we will search for.
     * * severity_pick_mode = WOUND_PICK_HIGHEST_SEVERITY: The "pick mode" we will use when considering multiple wounds of acceptable severity. See the above defines.
     * * random_roll = TRUE: If this is considered a "random" consideration. If true, only wounds that can be randomly generated will be considered.
     * * duplicates_allowed = FALSE: If exact duplicates of a given wound on part are tolerated. Useful for simply getting a path and not instantiating.
     * * care_about_existing_wounds = TRUE: If we iterate over wounds to see if any are above or at a given wounds severity, and disregard it if any are. Useful for simply getting a path and not instantiating.
     *
     * Returns:
     * A randomly picked wound typepath meeting all the above criteria and being applicable to the part's biotype - or null if there were none.
     */
    // tg: code/__DEFINES/wounds.dm:228
    public static Type get_corresponding_wound_type(object wounding_type = null, obj_item_bodypart part = null, object severity_min = null, object severity_max = null, object severity_pick_mode = null, object random_roll = null, object duplicates_allowed = null, object care_about_existing_wounds = null)
    {
        severity_max ??= severity_min;
        severity_pick_mode ??= WOUND_PICK_HIGHEST_SEVERITY;
        random_roll ??= TRUE;
        duplicates_allowed ??= FALSE;
        care_about_existing_wounds ??= TRUE;

        DmList wounding_type_list = GLOB.wounding_types_to_series[wounding_type] as DmList;
        if (length(wounding_type_list) == 0)
            return null;

        DmList paths_to_pick_from = Dm.list();
        foreach (object series in Dm.each(shuffle(wounding_type_list) as DmList))
        {
            DmList severity_list = GLOB.wound_series_collections[series] as DmList;
            if (length(severity_list) == 0)
                continue;

            object picked_severity = null;
            foreach (object severity_text in Dm.each(shuffle(GLOB.wound_severities_chronological) as DmList))
            {
                float severity = Dm.num(text2num(severity_text));
                // ISINRANGE(severity, severity_min, severity_max) (__DEFINES/maths.dm:89), expanded inline (rules 11.1 (e))
                if (!(Dm.num(severity_min) <= severity && severity <= Dm.num(severity_max)))
                    continue;

                if (picked_severity == null || ((Dm.eq(severity_pick_mode, WOUND_PICK_HIGHEST_SEVERITY) && severity > Dm.num(picked_severity)) || (Dm.eq(severity_pick_mode, WOUND_PICK_LOWEST_SEVERITY) && severity < Dm.num(picked_severity))))
                    picked_severity = severity;
            }

            DmList wound_typepaths = severity_list[Dm.text("[]", picked_severity)] as DmList;
            if (length(wound_typepaths) == 0)
                continue;

            foreach (Type iterated_path in Dm.each_cast<Type>(wound_typepaths))
            {
                datum_wound_pregen_data pregen_data = (datum_wound_pregen_data)GLOB.all_wound_pregen_data[iterated_path];
                if (Dm.truthy(pregen_data.can_be_applied_to(part, wounding_type, random_roll: Dm.num_or_null(random_roll), duplicates_allowed: duplicates_allowed, care_about_existing_wounds: Dm.num_or_null(care_about_existing_wounds))))
                    paths_to_pick_from[iterated_path] = wound_typepaths[iterated_path];
            }
        }

        return pick_weight(paths_to_pick_from) as Type; // we found our winners!
    }
}
