// Translated from tgstation (snapshot 1d99bd7f): code/datums/storage/subtypes/medkits.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice C: the whole file (what the medkits hold). The vars of the storage types (slots, sizes, sounds) are the
// generated var blocks.

///Regular medkit
// tg: code/datums/storage/subtypes/medkits.dm:2
public partial class datum_storage_medkit : datum_storage
{
    ///List of everything a medkit can hold
    // tg: code/datums/storage/subtypes/medkits.dm:9
    public static DmList list_of_everything_medkits_can_hold => TgState.Current.datum_storage_medkit__list_of_everything_medkits_can_hold ??= Dm.list(
        typeof(obj_item_surgical_drapes),
        typeof(obj_item_scalpel),
        typeof(obj_item_circular_saw),
        typeof(obj_item_bonesetter),
        typeof(obj_item_surgicaldrill),
        typeof(obj_item_retractor),
        typeof(obj_item_cautery),
        typeof(obj_item_hemostat),
        typeof(obj_item_blood_filter),
        typeof(obj_item_blood_scanner),
        typeof(obj_item_dnainjector),
        typeof(obj_item_healthanalyzer),
        typeof(obj_item_implant),
        typeof(obj_item_implantcase),
        typeof(obj_item_implanter),
        typeof(obj_item_lazarus_injector),
        typeof(obj_item_reflexhammer),
        typeof(obj_item_tourniquet),
        typeof(obj_item_stack_medical),
        typeof(obj_item_stack_medical_wrap_sticky_tape),
        typeof(obj_item_reagent_containers_applicator),
        typeof(obj_item_reagent_containers_dropper),
        typeof(obj_item_reagent_containers_cup_beaker),
        typeof(obj_item_reagent_containers_cup_bottle),
        typeof(obj_item_reagent_containers_cup_tube),
        typeof(obj_item_reagent_containers_syringe),
        typeof(obj_item_reagent_containers_medigel),
        typeof(obj_item_reagent_containers_spray),
        typeof(obj_item_reagent_containers_hypospray),
        typeof(obj_item_reagent_containers_blood),
        typeof(obj_item_storage_box_bandages),
        typeof(obj_item_storage_fancy_cigarettes),
        typeof(obj_item_storage_pill_bottle),
        typeof(obj_item_storage_box_evilmeds),
        typeof(obj_item_clothing_mask_breath),
        typeof(obj_item_clothing_mask_surgical),
        typeof(obj_item_clothing_neck_stethoscope),
        typeof(obj_item_clothing_gloves),
        typeof(obj_item_clothing_mask_muzzle),
        typeof(obj_item_clothing_glasses),
        typeof(obj_item_mod_module_thread_ripper),
        typeof(obj_item_mod_module_surgical_processor_preloaded),
        typeof(obj_item_mod_module_defibrillator_combat),
        typeof(obj_item_mod_module_health_analyzer),
        typeof(obj_item_lighter),
        typeof(obj_item_sensor_device),
        typeof(obj_item_radio),
        typeof(obj_item_bikehorn_rubberducky),
        typeof(obj_item_shears),
        typeof(obj_item_geiger_counter),
        typeof(obj_item_flashlight_pen),
        typeof(obj_item_extinguisher_mini),
        typeof(obj_item_stamp),
        typeof(obj_item_wrench_medical),
        typeof(obj_item_tank_internals_emergency_oxygen),
        typeof(obj_item_gun_syringe_syndicate),
        typeof(obj_item_pinpointer_crew),
        typeof(obj_item_holosign_creator_medical),
        typeof(obj_item_autosurgeon_syndicate_emaggedsurgerytoolset)
    );

    // tg: code/datums/storage/subtypes/medkits.dm:86
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound", "holdables")]
    public override async DmTask New(params object[] __extra)
    {
        object holdables = Dm.arg(__extra, 6);
        await base.New(__extra);
        if (holdables != null)
            set_holdable(can_hold_list: holdables);
    }
}

///Surgery medkit
// tg: code/datums/storage/subtypes/medkits.dm:92
public partial class datum_storage_medkit_surgery : datum_storage_medkit
{
    // tg: code/datums/storage/subtypes/medkits.dm:97
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound", "holdables")]
    public override async DmTask New(params object[] __extra)
    {
        object holdables = list_of_everything_medkits_can_hold;
        await base.New(Dm.forward(__extra, Dm.arg(__extra, 0), Dm.arg(__extra, 1), Dm.arg(__extra, 2), Dm.arg(__extra, 3), Dm.arg(__extra, 4), Dm.arg(__extra, 5), holdables));
    }
}

///Tactical medkit
// tg: code/datums/storage/subtypes/medkits.dm:102
public partial class datum_storage_medkit_tactical : datum_storage_medkit
{
    // tg: code/datums/storage/subtypes/medkits.dm:107
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound", "holdables")]
    public override async DmTask New(params object[] __extra)
    {
        object holdables = list_of_everything_medkits_can_hold;
        await base.New(Dm.forward(__extra, Dm.arg(__extra, 0), Dm.arg(__extra, 1), Dm.arg(__extra, 2), Dm.arg(__extra, 3), Dm.arg(__extra, 4), Dm.arg(__extra, 5), holdables));
    }
}

///Coroner medkit
// tg: code/datums/storage/subtypes/medkits.dm:117
public partial class datum_storage_medkit_coroner : datum_storage_medkit
{
    // tg: code/datums/storage/subtypes/medkits.dm:122
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage", "rustle_sound", "remove_rustle_sound", "holdables")]
    public override async DmTask New(params object[] __extra)
    {
        object holdables = Dm.list(
            typeof(obj_item_reagent_containers),
            typeof(obj_item_bodybag),
            typeof(obj_item_toy_crayon),
            typeof(obj_item_pen),
            typeof(obj_item_paper),
            typeof(obj_item_surgical_drapes),
            typeof(obj_item_scalpel),
            typeof(obj_item_retractor),
            typeof(obj_item_hemostat),
            typeof(obj_item_cautery),
            typeof(obj_item_autopsy_scanner)
        );
        await base.New(Dm.forward(__extra, Dm.arg(__extra, 0), Dm.arg(__extra, 1), Dm.arg(__extra, 2), Dm.arg(__extra, 3), Dm.arg(__extra, 4), Dm.arg(__extra, 5), holdables));
    }
}

public sealed partial class TgState
{
    // tg: code/datums/storage/subtypes/medkits.dm:9  the static list of what a medkit can hold
    internal DmList datum_storage_medkit__list_of_everything_medkits_can_hold;
}
