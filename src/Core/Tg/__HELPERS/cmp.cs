// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/cmp.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public sealed partial class TgState
{
    // tg: code/__HELPERS/cmp.dm:210  (var/static/list/fluids_priority in /proc/cmp_fish_fluid)
    internal DmList GlobalProcs__cmp_fish_fluid__fluids_priority = Dm.list(
        AQUARIUM_FLUID_FRESHWATER,
        AQUARIUM_FLUID_SALTWATER,
        AQUARIUM_FLUID_ANADROMOUS,
        AQUARIUM_FLUID_SULPHWATEVER,
        AQUARIUM_FLUID_ANY_WATER,
        AQUARIUM_FLUID_AIR);
}

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/cmp.dm:1
    public static object cmp_numeric_dsc(object a = null, object b = null)
    {
        return Dm.sub(b, a);
    }

    // tg: code/__HELPERS/cmp.dm:4
    public static object cmp_numeric_asc(object a = null, object b = null)
    {
        return Dm.sub(a, b);
    }

    // tg: code/__HELPERS/cmp.dm:7
    public static object cmp_text_asc(object a = null, object b = null)
    {
        // Rules 3.5 (v1.2): a builtin's text argument takes the object as is; tg sorts mobs and typepaths with this
        // comparator (sort_list's default, __HELPERS/mobs.dm:415, atoms.dm:299), and sorttext treats a value that is not
        // text as OpenDream does.
        return sorttext(b, a);
    }

    // tg: code/__HELPERS/cmp.dm:10
    public static object cmp_text_dsc(object a = null, object b = null)
    {
        return sorttext(a, b);
    }

    // tg: code/__HELPERS/cmp.dm:13
    public static object cmp_embed_text_asc(object a = null, object b = null)
    {
        if (isdatum(a))
            a = REF(a);
        if (isdatum(b))
            b = REF(b);
        return sorttext(Dm.text("[]", b), Dm.text("[]", a));
    }

    // tg: code/__HELPERS/cmp.dm:20
    public static object cmp_embed_text_dsc(object a = null, object b = null)
    {
        if (isdatum(a))
            a = REF(a);
        if (isdatum(b))
            b = REF(b);
        return sorttext(Dm.text("[]", a), Dm.text("[]", b));
    }

    // tg: code/__HELPERS/cmp.dm:27
    public static object cmp_list_len_asc(DmList a = null, DmList b = null)
    {
        return length(a) - length(b);
    }

    // tg: code/__HELPERS/cmp.dm:30
    public static object cmp_list_len_dsc(DmList a = null, DmList b = null)
    {
        return length(b) - length(a);
    }

    // tg: code/__HELPERS/cmp.dm:33
    public static object cmp_name_asc(object a = null, object b = null)
    {
        // tg sorts any datum with a name with it, not only atoms (the surgery radial's choices, gaps P1): the parameters are object
        // (Appendix A) and the names are read as DM reads them
        return sorttext(Dm.get_var(b, "name"), Dm.get_var(a, "name"));
    }

    // tg: code/__HELPERS/cmp.dm:36
    public static object cmp_name_dsc(atom a = null, atom b = null)
    {
        return sorttext(a.name, b.name);
    }

    // PORT: a and b are atoms or typepaths (the slot's note: initial reads an instance or a type): its one tg caller, SSfishing's
    // start, sorts the fish types by their initial names, and tg's atom/a does not check it; a typed parameter would read a type as null.
    // tg: code/__HELPERS/cmp.dm:39
    public static object cmp_init_name_asc(object a = null, object b = null)
    {
        return sorttext(Dm.initial(b, (atom p) => p.name), Dm.initial(a, (atom p) => p.name));
    }

    // tg: code/__HELPERS/cmp.dm:42
    public static object cmp_records_asc(datum_record a = null, datum_record b = null)
    {
        return sorttext(b.name, a.name);
    }

    // tg: code/__HELPERS/cmp.dm:45
    public static object cmp_records_dsc(datum_record a = null, datum_record b = null)
    {
        return sorttext(a.name, b.name);
    }

    // Datum cmp with vars is always slower than a specialist cmp proc, use your judgement.
    // tg: code/__HELPERS/cmp.dm:49
    public static object cmp_datum_numeric_asc(datum a = null, datum b = null, object variable = null)
    {
        Dm.NotTranslated("code/__HELPERS/cmp.dm:49", "cmp_datum_numeric_asc: reads a.vars[variable]; the vars reflection is not translated (rules 10.7)");
        return default;
    }

    // tg: code/__HELPERS/cmp.dm:52
    public static object cmp_datum_numeric_dsc(datum a = null, datum b = null, object variable = null)
    {
        Dm.NotTranslated("code/__HELPERS/cmp.dm:52", "cmp_datum_numeric_dsc: reads a.vars[variable]; the vars reflection is not translated (rules 10.7)");
        return default;
    }

    // tg: code/__HELPERS/cmp.dm:55
    public static object cmp_datum_text_asc(datum a = null, datum b = null, object variable = null)
    {
        Dm.NotTranslated("code/__HELPERS/cmp.dm:55", "cmp_datum_text_asc: reads a.vars[variable]; the vars reflection is not translated (rules 10.7)");
        return default;
    }

    // tg: code/__HELPERS/cmp.dm:58
    public static object cmp_datum_text_dsc(datum a = null, datum b = null, object variable = null)
    {
        Dm.NotTranslated("code/__HELPERS/cmp.dm:58", "cmp_datum_text_dsc: reads a.vars[variable]; the vars reflection is not translated (rules 10.7)");
        return default;
    }

    // tg: code/__HELPERS/cmp.dm:61
    public static object cmp_ckey_asc(client a = null, client b = null)
    {
        return sorttext(b.ckey, a.ckey);
    }

    // tg: code/__HELPERS/cmp.dm:64
    public static object cmp_ckey_dsc(client a = null, client b = null)
    {
        return sorttext(a.ckey, b.ckey);
    }

    // tg: code/__HELPERS/cmp.dm:67
    public static object cmp_playtime_asc(client a = null, client b = null)
    {
        return cmp_numeric_asc(a.get_exp_living(TRUE), b.get_exp_living(TRUE));
    }

    // tg: code/__HELPERS/cmp.dm:70  (tg: the same body as cmp_playtime_asc)
    public static object cmp_playtime_dsc(client a = null, client b = null)
    {
        return cmp_numeric_asc(a.get_exp_living(TRUE), b.get_exp_living(TRUE));
    }

    // tg: code/__HELPERS/cmp.dm:73
    public static object cmp_subsystem_init(datum_controller_subsystem a = null, datum_controller_subsystem b = null)
    {
        return a.init_order - b.init_order;
    }

    // tg: code/__HELPERS/cmp.dm:76  (a and b decided object, rules 3.3 P1 and 3.2 "wider values", Appendix A: slots.tsv lists
    // them undecided; both tg callers sort lists of typepaths, subtypesof(/datum/controller/subsystem) at
    // controllers/master.dm:111 and failsafe.dm:155, which the declared class would turn into null, and initial() reads a
    // typepath's default as well as an instance's)
    public static object cmp_subsystem_init_stage(object a = null, object b = null)
    {
        return Dm.initial(a, (datum_controller_subsystem p) => p.init_stage) - Dm.initial(b, (datum_controller_subsystem p) => p.init_stage);
    }

    // tg: code/__HELPERS/cmp.dm:79
    public static object cmp_subsystem_display(datum_controller_subsystem a = null, datum_controller_subsystem b = null)
    {
        return sorttext(b.name, a.name);
    }

    // tg: code/__HELPERS/cmp.dm:82
    public static object cmp_subsystem_priority(datum_controller_subsystem a = null, datum_controller_subsystem b = null)
    {
        return a.priority - b.priority;
    }

    // tg: code/__HELPERS/cmp.dm:85
    public static object cmp_filter_data_priority(DmList A = null, DmList B = null)
    {
        return Dm.sub(A["priority"], B["priority"]);
    }

    // tg: code/__HELPERS/cmp.dm:88
    public static object cmp_timer(datum_timedevent a = null, datum_timedevent b = null)
    {
        return Dm.sub(a.timeToRun, b.timeToRun);
    }

    // tg: code/__HELPERS/cmp.dm:91
    // (tg's preloadRuinTemplates sorts the ruin types, not instances: A and B are type paths, which initial reads)
    public static object cmp_ruincost_priority(object A = null, object B = null)
    {
        return Dm.initial(A, (datum_map_template_ruin p) => p.cost) - Dm.initial(B, (datum_map_template_ruin p) => p.cost);
    }

    // tg: code/__HELPERS/cmp.dm:94
    public static object cmp_qdel_item_time(datum_qdel_item A = null, datum_qdel_item B = null)
    {
        object dot = null;
        dot = B.hard_delete_time - A.hard_delete_time;
        if (!Dm.truthy(dot))
            dot = B.destroy_time - A.destroy_time;
        if (!Dm.truthy(dot))
            dot = B.failures - A.failures;
        if (!Dm.truthy(dot))
            dot = B.qdels - A.qdels;
        return dot;
    }

    // tg: code/__HELPERS/cmp.dm:103
    public static object cmp_generic_stat_item_time(DmList A = null, DmList B = null)
    {
        object dot = null;
        dot = Dm.sub(B[STAT_ENTRY_TIME], A[STAT_ENTRY_TIME]);
        if (!Dm.truthy(dot))
            dot = Dm.sub(B[STAT_ENTRY_COUNT], A[STAT_ENTRY_COUNT]);
        return dot;
    }

    // tg: code/__HELPERS/cmp.dm:108
    public static object cmp_profile_avg_time_dsc(DmList A = null, DmList B = null)
    {
        return Dm.sub(Dm.div(B[PROFILE_ITEM_TIME], (B[PROFILE_ITEM_COUNT] is var or__1 && Dm.truthy(or__1) ? or__1 : (object)1)), Dm.div(A[PROFILE_ITEM_TIME], (A[PROFILE_ITEM_COUNT] is var or__2 && Dm.truthy(or__2) ? or__2 : (object)1)));
    }

    // tg: code/__HELPERS/cmp.dm:111
    public static object cmp_profile_time_dsc(DmList A = null, DmList B = null)
    {
        return Dm.sub(B[PROFILE_ITEM_TIME], A[PROFILE_ITEM_TIME]);
    }

    // tg: code/__HELPERS/cmp.dm:114
    public static object cmp_profile_count_dsc(DmList A = null, DmList B = null)
    {
        return Dm.sub(B[PROFILE_ITEM_COUNT], A[PROFILE_ITEM_COUNT]);
    }

    // tg: code/__HELPERS/cmp.dm:117
    public static object cmp_atom_layer_asc(atom A = null, atom B = null)
    {
        if (A.plane != B.plane)
            return A.plane - B.plane;
        else
            return A.layer - B.layer;
    }

    // tg: code/__HELPERS/cmp.dm:123
    public static object cmp_advdisease_resistance_asc(datum_disease_advance A = null, datum_disease_advance B = null)
    {
        return Dm.sub(A.totalResistance(), B.totalResistance());
    }

    // tg: code/__HELPERS/cmp.dm:126
    public static object cmp_quirk_asc(datum_quirk A = null, datum_quirk B = null)
    {
        float a_sign = sign(Dm.num(Dm.initial(A, (datum_quirk p) => p.value)) * -1);
        float b_sign = sign(Dm.num(Dm.initial(B, (datum_quirk p) => p.value)) * -1);

        // Neutral traits go last.
        if (a_sign == 0)
            a_sign = 2;
        if (b_sign == 0)
            b_sign = 2;

        string a_name = Dm.initial(A, (datum_quirk p) => p.name);
        string b_name = Dm.initial(B, (datum_quirk p) => p.name);

        if (a_sign != b_sign)
            return a_sign - b_sign;
        else
            return sorttext(b_name, a_name);
    }

    // tg: code/__HELPERS/cmp.dm:144
    public static object cmp_job_display_with_departments_asc(datum_job A = null, datum_job B = null)
    {
        return Dm.sub(A.display_order_with_department(), B.display_order_with_department());
    }

    // tg: code/__HELPERS/cmp.dm:147
    public static object cmp_department_display_asc(datum_job_department A = null, datum_job_department B = null)
    {
        return A.display_order - B.display_order;
    }

    // tg: code/__HELPERS/cmp.dm:150
    // a and b are object (Appendix A, gaps P5): the chem dispenser sorts its lists of reagent typepaths, whose initial name DM reads
    public static object cmp_reagents_asc(object a = null, object b = null)
    {
        static object name_of(object reagent) => reagent is Type path ? Dm.initial(path, (datum_reagent p) => p.name) : Dm.initial(reagent as datum_reagent, (datum_reagent p) => p.name);
        return sorttext(name_of(b), name_of(a));
    }

    // tg: code/__HELPERS/cmp.dm:153
    public static object cmp_typepaths_asc(object A = null, object B = null)
    {
        return sorttext(Dm.text("[]", B), Dm.text("[]", A));
    }

    // tg: code/__HELPERS/cmp.dm:156
    public static object cmp_num_string_asc(object A = null, object B = null)
    {
        return Dm.num(text2num(A)) - Dm.num(text2num(B));
    }

    // tg: code/__HELPERS/cmp.dm:159
    public static object cmp_mob_realname_dsc(mob A = null, mob B = null)
    {
        return sorttext(A.real_name, B.real_name);
    }

    /// Orders bodyparts by their body_part value, ascending.
    // tg: code/__HELPERS/cmp.dm:163  (limb_one and limb_two decided object, M2 P2, as cmp_subsystem_init_stage above: slots.tsv
    // lists them undecided; tg's caller sorts the species' bodypart_overrides by their typepath values,
    // modules/mob/living/carbon/human/_species.dm:191, which the declared class would turn into null, and initial() reads a
    // typepath's default as well as an instance's)
    public static object cmp_bodypart_by_body_part_asc(object limb_one = null, object limb_two = null)
    {
        return Dm.sub(Dm.initial(limb_one, (obj_item_bodypart p) => p.body_part), Dm.initial(limb_two, (obj_item_bodypart p) => p.body_part));
    }

    /// Orders by integrated circuit weight
    // tg: code/__HELPERS/cmp.dm:167
    public static object cmp_port_order_asc(datum_port compare1 = null, datum_port compare2 = null)
    {
        return Dm.num(compare1.order) - Dm.num(compare2.order);
    }

    /// Orders by uplink category weight
    // tg: code/__HELPERS/cmp.dm:171
    // (syndicate A2: the parameters are object, Appendix A: the uplink asset sorts the categories' typepaths, assets/uplink.dm:12)
    public static object cmp_uplink_category_desc(object compare1 = null, object compare2 = null)
    {
        return Dm.initial(compare2, (datum_uplink_category p) => p.weight) - Dm.initial(compare1, (datum_uplink_category p) => p.weight);
    }

    /**
     * Sorts crafting recipe requirements before the crafting recipe is inserted into GLOB.crafting_recipes
     *
     * Prioritises [/datum/reagent] to ensure reagent requirements are always processed first when crafting.
     * This prevents any reagent_containers from being consumed before the reagents they contain, which can
     * lead to runtimes and item duplication when it happens.
     */
    // tg: code/__HELPERS/cmp.dm:181
    public static object cmp_crafting_req_priority(object A = null, object B = null)
    {
        float lhs = 0;
        float rhs = 0;

        lhs = (ispath(A, typeof(datum_reagent)) ? 0 : 1);
        rhs = (ispath(B, typeof(datum_reagent)) ? 0 : 1);

        return lhs - rhs;
    }

    /// Orders heretic knowledge by priority
    // tg: code/__HELPERS/cmp.dm:191
    public static object cmp_heretic_knowledge(datum_heretic_knowledge knowledge_a = null, datum_heretic_knowledge knowledge_b = null)
    {
        return Dm.initial(knowledge_b, (datum_heretic_knowledge p) => p.priority) - Dm.initial(knowledge_a, (datum_heretic_knowledge p) => p.priority);
    }

    /// Passed a list of assoc lists, sorts them by the list's "name" keys.
    // tg: code/__HELPERS/cmp.dm:195
    public static object cmp_assoc_list_name(DmList A = null, DmList B = null)
    {
        return sorttext(B["name"], A["name"]);
    }

    /// Orders mobs by health
    // tg: code/__HELPERS/cmp.dm:199
    public static object cmp_mob_health(mob_living mob_a = null, mob_living mob_b = null)
    {
        return mob_b.health - mob_a.health;
    }

    // tg: code/__HELPERS/cmp.dm:202
    public static object cmp_deathmatch_mods(datum_deathmatch_modifier a = null, datum_deathmatch_modifier b = null)
    {
        return sorttext(b.name, a.name);
    }

    // tg: code/__HELPERS/cmp.dm:210  (var/static, rules 10.6)
    private static ref DmList cmp_fish_fluid__fluids_priority => ref TgState.Current.GlobalProcs__cmp_fish_fluid__fluids_priority;

    /**
     * Orders fish types following this order (freshwater -> saltwater -> anadromous -> sulphuric water -> any water -> air)
     * If both share the same required fluid type, they'll be ordered by name instead.
     */
    // tg: code/__HELPERS/cmp.dm:209
    public static object cmp_fish_fluid(obj_item_fish a = null, obj_item_fish b = null)
    {
        float position_a = cmp_fish_fluid__fluids_priority.Find(Dm.initial(a, (obj_item_fish p) => p.required_fluid_type));
        float position_b = cmp_fish_fluid__fluids_priority.Find(Dm.initial(b, (obj_item_fish p) => p.required_fluid_type));
        return (cmp_numeric_asc(position_a, position_b) is var or__1 && Dm.truthy(or__1) ? or__1 : cmp_text_asc(Dm.initial(b, (obj_item_fish p) => p.name), Dm.initial(a, (obj_item_fish p) => p.name)));
    }

    /// Orders vending products by their price
    // tg: code/__HELPERS/cmp.dm:223
    public static object cmp_vending_prices(datum_data_vending_product a = null, datum_data_vending_product b = null)
    {
        return Dm.sub(b.price, a.price);
    }

    // tg: code/__HELPERS/cmp.dm:226
    public static object cmp_item_vending_prices(obj_item a = null, obj_item b = null)
    {
        return Dm.sub(b.custom_price, a.custom_price);
    }

    ///Sorts stock parts based on tier
    // tg: code/__HELPERS/cmp.dm:230
    public static object cmp_rped_sort(obj_item first_item = null, obj_item second_item = null)
    {
        ///even though stacks aren't stock parts, get_part_rating() is defined on the item level (see /obj/item/proc/get_part_rating()) and defaults to returning 0.
        return Dm.sub(second_item.get_part_rating(), first_item.get_part_rating());
    }

    /// Orders cameras by their `c_tag` ascending
    // tg: code/__HELPERS/cmp.dm:235
    public static object cmp_camera_ctag_asc(obj_machinery_camera a = null, obj_machinery_camera b = null)
    {
        return sorttext(b.c_tag, a.c_tag);
    }

    /// Sorts client colors based on their priority
    // tg: code/__HELPERS/cmp.dm:239
    public static object cmp_client_colours(datum_client_colour first_color = null, datum_client_colour second_color = null)
    {
        return second_color.priority - first_color.priority;
    }
}
