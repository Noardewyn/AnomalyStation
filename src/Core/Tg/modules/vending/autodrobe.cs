// Translated from tgstation (snapshot 1d99bd7f): code/modules/vending/autodrobe.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package S2: the whole file, the AutoDrobe and its costume lists. The var blocks are generated.

public partial class datum_controller_global_vars
{
    // tg: code/modules/vending/autodrobe.dm:2  GLOBAL_LIST_INIT(autodrobe_costumes_items, list(...))
    public DmList autodrobe_costumes_items;

    // tg: code/modules/vending/autodrobe.dm:2  (GLOBAL_MANAGED(autodrobe_costumes_items, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalautodrobe_costumes_items()
    {
        autodrobe_costumes_items = Dm.list(
            Dm.kv(typeof(obj_item_clothing_under_costume_gladiator), 3),
            Dm.kv(typeof(obj_item_clothing_head_helmet_gladiator), 3),
            Dm.kv(typeof(obj_item_clothing_suit_toggle_labcoat_mad), 3),
            Dm.kv(typeof(obj_item_clothing_suit_bio_suit_plaguedoctorsuit), 3),
            Dm.kv(typeof(obj_item_clothing_head_bio_hood_plague), 3),
            Dm.kv(typeof(obj_item_clothing_mask_gas_plaguedoctor), 3),
            Dm.kv(typeof(obj_item_clothing_under_rank_medical_doctor_nurse), 3),
            Dm.kv(typeof(obj_item_clothing_suit_toggle_owlwings), 1),
            Dm.kv(typeof(obj_item_clothing_under_costume_owl), 1),
            Dm.kv(typeof(obj_item_clothing_mask_gas_owl_mask), 1),
            Dm.kv(typeof(obj_item_clothing_suit_toggle_owlwings_griffinwings), 1),
            Dm.kv(typeof(obj_item_clothing_under_costume_griffin), 1),
            Dm.kv(typeof(obj_item_clothing_shoes_griffin), 1),
            Dm.kv(typeof(obj_item_clothing_head_costume_griffin), 1),
            Dm.kv(typeof(obj_item_clothing_under_costume_pirate), 3),
            Dm.kv(typeof(obj_item_clothing_suit_costume_pirate), 3),
            Dm.kv(typeof(obj_item_clothing_head_costume_pirate), 3),
            Dm.kv(typeof(obj_item_clothing_head_costume_pirate_bandana), 3),
            Dm.kv(typeof(obj_item_clothing_shoes_pirate), 3),
            Dm.kv(typeof(obj_item_clothing_under_costume_soviet), 3),
            Dm.kv(typeof(obj_item_clothing_head_costume_ushanka), 3),
            Dm.kv(typeof(obj_item_clothing_accessory_vest_sheriff), 1),
            Dm.kv(typeof(obj_item_clothing_head_cowboy_brown), 3),
            Dm.kv(typeof(obj_item_clothing_head_cowboy_red), 3),
            Dm.kv(typeof(obj_item_clothing_head_cowboy_black), 3),
            Dm.kv(typeof(obj_item_clothing_head_costume_sombrero_green), 3),
            Dm.kv(typeof(obj_item_clothing_suit_costume_poncho), 3),
            Dm.kv(typeof(obj_item_clothing_suit_costume_poncho_green), 3),
            Dm.kv(typeof(obj_item_clothing_suit_costume_poncho_red), 3),
            Dm.kv(typeof(obj_item_clothing_suit_costume_snowman), 3),
            Dm.kv(typeof(obj_item_clothing_head_costume_snowman), 3),
            Dm.kv(typeof(obj_item_clothing_under_costume_referee), 1),
            Dm.kv(typeof(obj_item_clothing_mask_whistle), 1),
            Dm.kv(typeof(obj_item_storage_backpack_henchmen), 5),
            Dm.kv(typeof(obj_item_clothing_under_costume_henchmen), 5),
            Dm.kv(typeof(obj_item_clothing_head_costume_jackbros), 5),
            Dm.kv(typeof(obj_item_clothing_under_costume_jackbros), 5),
            Dm.kv(typeof(obj_item_clothing_under_costume_deckers), 5),
            Dm.kv(typeof(obj_item_clothing_under_costume_sailor), 3),
            Dm.kv(typeof(obj_item_clothing_head_costume_delinquent), 1),
            Dm.kv(typeof(obj_item_clothing_suit_costume_dracula), 3),
            Dm.kv(typeof(obj_item_clothing_under_costume_draculass), 3),
            Dm.kv(typeof(obj_item_clothing_suit_costume_gothcoat), 3),
            Dm.kv(typeof(obj_item_clothing_glasses_eyepatch), 3),
            Dm.kv(typeof(obj_item_clothing_glasses_eyepatch_medical), 3),
            Dm.kv(typeof(obj_item_clothing_under_costume_gi), 4),
            Dm.kv(typeof(obj_item_clothing_head_soft_propeller_hat), 3),
            Dm.kv(typeof(obj_item_clothing_neck_bowtie_rainbow), 3)
        );
        Dm.append(ref gvars_datum_init_order, "autodrobe_costumes_items");
    }

    // tg: code/modules/vending/autodrobe.dm:53  GLOBAL_LIST_INIT(autodrobe_supernatural_items, list(...))
    public DmList autodrobe_supernatural_items;

    // tg: code/modules/vending/autodrobe.dm:53  (GLOBAL_MANAGED(autodrobe_supernatural_items, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalautodrobe_supernatural_items()
    {
        autodrobe_supernatural_items = Dm.list(
            Dm.kv(typeof(obj_item_clothing_suit_costume_imperium_monk), 3),
            Dm.kv(typeof(obj_item_clothing_suit_chaplainsuit_holidaypriest), 3),
            Dm.kv(typeof(obj_item_clothing_suit_chaplainsuit_habit), 3),
            Dm.kv(typeof(obj_item_clothing_head_chaplain_habit_veil), 3),
            Dm.kv(typeof(obj_item_clothing_suit_chaplainsuit_whiterobe), 3),
            Dm.kv(typeof(obj_item_clothing_head_wizard_marisa_fake), 3),
            Dm.kv(typeof(obj_item_clothing_suit_wizrobe_marisa_fake), 3),
            Dm.kv(typeof(obj_item_clothing_head_costume_witchwig), 3),
            Dm.kv(typeof(obj_item_staff_broom), 3),
            Dm.kv(typeof(obj_item_clothing_suit_wizrobe_fake), 3),
            Dm.kv(typeof(obj_item_clothing_head_wizard_fake), 3),
            Dm.kv(typeof(obj_item_staff), 3),
            Dm.kv(typeof(obj_item_clothing_head_costume_shrine_wig), 1),
            Dm.kv(typeof(obj_item_clothing_suit_costume_shrine_maiden), 1),
            Dm.kv(typeof(obj_item_gohei), 3)
        );
        Dm.append(ref gvars_datum_init_order, "autodrobe_supernatural_items");
    }

    // tg: code/modules/vending/autodrobe.dm:71  GLOBAL_LIST_INIT(autodrobe_entretainers_items, list(...))
    public DmList autodrobe_entretainers_items;

    // tg: code/modules/vending/autodrobe.dm:71  (GLOBAL_MANAGED(autodrobe_entretainers_items, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalautodrobe_entretainers_items()
    {
        autodrobe_entretainers_items = Dm.list(
            Dm.kv(typeof(obj_item_clothing_under_rank_civilian_clown_blue), 3),
            Dm.kv(typeof(obj_item_clothing_under_rank_civilian_clown_green), 3),
            Dm.kv(typeof(obj_item_clothing_under_rank_civilian_clown_yellow), 3),
            Dm.kv(typeof(obj_item_clothing_under_rank_civilian_clown_orange), 3),
            Dm.kv(typeof(obj_item_clothing_under_rank_civilian_clown_purple), 3),
            Dm.kv(typeof(obj_item_clothing_mask_gas_sexyclown), 3),
            Dm.kv(typeof(obj_item_clothing_under_rank_civilian_clown_sexy), 3),
            Dm.kv(typeof(obj_item_clothing_head_beret), 6),
            Dm.kv(typeof(obj_item_clothing_mask_gas_sexymime), 3),
            Dm.kv(typeof(obj_item_clothing_under_rank_civilian_mime_sexy), 3),
            Dm.kv(typeof(obj_item_clothing_under_rank_civilian_mime_skirt), 3),
            Dm.kv(typeof(obj_item_clothing_under_rank_civilian_clown_jester), 3),
            Dm.kv(typeof(obj_item_clothing_head_costume_jester), 3),
            Dm.kv(typeof(obj_item_clothing_shoes_jester_shoes), 3),
            Dm.kv(typeof(obj_item_clothing_under_rank_civilian_clown_jesteralt), 3),
            Dm.kv(typeof(obj_item_clothing_head_costume_jesteralt), 3),
            Dm.kv(typeof(obj_item_clothing_shoes_clown_shoes_jester), 1),
            Dm.kv(typeof(obj_item_clothing_under_costume_villain), 3),
            Dm.kv(typeof(obj_item_clothing_suit_costume_joker), 3),
            Dm.kv(typeof(obj_item_clothing_under_costume_joker), 3),
            Dm.kv(typeof(obj_item_clothing_shoes_singery), 1),
            Dm.kv(typeof(obj_item_clothing_under_costume_singer_yellow), 1),
            Dm.kv(typeof(obj_item_clothing_shoes_singerb), 1),
            Dm.kv(typeof(obj_item_clothing_under_costume_singer_blue), 1),
            Dm.kv(typeof(obj_item_clothing_shoes_singerr), 1),
            Dm.kv(typeof(obj_item_clothing_under_costume_singer_red), 1),
            Dm.kv(typeof(obj_item_clothing_head_costume_cueball), 1)
        );
        Dm.append(ref gvars_datum_init_order, "autodrobe_entretainers_items");
    }

    // tg: code/modules/vending/autodrobe.dm:101  GLOBAL_LIST_INIT(autodrobe_fancy_items, list(...))
    public DmList autodrobe_fancy_items;

    // tg: code/modules/vending/autodrobe.dm:101  (GLOBAL_MANAGED(autodrobe_fancy_items, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalautodrobe_fancy_items()
    {
        autodrobe_fancy_items = Dm.list(
            Dm.kv(typeof(obj_item_clothing_under_costume_captain), 1),
            Dm.kv(typeof(obj_item_clothing_under_costume_captain_skirt), 1),
            Dm.kv(typeof(obj_item_clothing_under_costume_seifuku), 3),
            Dm.kv(typeof(obj_item_clothing_under_costume_seifuku_red), 3),
            Dm.kv(typeof(obj_item_clothing_under_costume_seifuku_teal), 3),
            Dm.kv(typeof(obj_item_clothing_under_costume_seifuku_tan), 3),
            Dm.kv(typeof(obj_item_clothing_under_dress_skirt), 3),
            Dm.kv(typeof(obj_item_clothing_neck_tie), 3),
            Dm.kv(typeof(obj_item_clothing_head_hats_tophat), 3),
            Dm.kv(typeof(obj_item_clothing_under_costume_kilt), 3),
            Dm.kv(typeof(obj_item_clothing_glasses_monocle), 3),
            Dm.kv(typeof(obj_item_clothing_head_hats_bowler), 3),
            Dm.kv(typeof(obj_item_cane), 3),
            Dm.kv(typeof(obj_item_clothing_under_dress_sundress), 3),
            Dm.kv(typeof(obj_item_clothing_suit_costume_whitedress), 3),
            Dm.kv(typeof(obj_item_clothing_suit_costume_changshan_red), 3),
            Dm.kv(typeof(obj_item_clothing_suit_costume_changshan_blue), 3),
            Dm.kv(typeof(obj_item_clothing_suit_costume_cheongsam_red), 3),
            Dm.kv(typeof(obj_item_clothing_suit_costume_cheongsam_blue), 3)
        );
        Dm.append(ref gvars_datum_init_order, "autodrobe_fancy_items");
    }

    // tg: code/modules/vending/autodrobe.dm:123  GLOBAL_LIST_INIT(autodrobe_animal_items, list(...))
    public DmList autodrobe_animal_items;

    // tg: code/modules/vending/autodrobe.dm:123  (GLOBAL_MANAGED(autodrobe_animal_items, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalautodrobe_animal_items()
    {
        autodrobe_animal_items = Dm.list(
            Dm.kv(typeof(obj_item_clothing_head_costume_kitty), 3),
            Dm.kv(typeof(obj_item_clothing_head_costume_rabbitears), 3),
            Dm.kv(typeof(obj_item_clothing_suit_costume_chickensuit), 3),
            Dm.kv(typeof(obj_item_clothing_head_costume_chicken), 3),
            Dm.kv(typeof(obj_item_clothing_suit_hooded_carp_costume), 3),
            Dm.kv(typeof(obj_item_clothing_suit_hooded_ian_costume), 3),
            Dm.kv(typeof(obj_item_clothing_suit_hooded_bee_costume), 3),
            Dm.kv(typeof(obj_item_clothing_mask_animal_small_bat), 3),
            Dm.kv(typeof(obj_item_clothing_mask_animal_small_bee), 3),
            Dm.kv(typeof(obj_item_clothing_mask_animal_small_bear), 3),
            Dm.kv(typeof(obj_item_clothing_mask_animal_small_raven), 3),
            Dm.kv(typeof(obj_item_clothing_mask_animal_small_jackal), 3),
            Dm.kv(typeof(obj_item_clothing_mask_animal_small_fox), 3),
            Dm.kv(typeof(obj_item_clothing_mask_animal_frog), 3),
            Dm.kv(typeof(obj_item_clothing_mask_animal_small_rat), 3),
            Dm.kv(typeof(obj_item_clothing_mask_animal_pig), 3),
            Dm.kv(typeof(obj_item_clothing_mask_animal_cowmask), 3),
            Dm.kv(typeof(obj_item_clothing_mask_animal_horsehead), 3)
        );
        Dm.append(ref gvars_datum_init_order, "autodrobe_animal_items");
    }

    // tg: code/modules/vending/autodrobe.dm:144  GLOBAL_LIST_INIT(autodrobe_service_items, list(...))
    public DmList autodrobe_service_items;

    // tg: code/modules/vending/autodrobe.dm:144  (GLOBAL_MANAGED(autodrobe_service_items, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalautodrobe_service_items()
    {
        autodrobe_service_items = Dm.list(
            Dm.kv(typeof(obj_item_clothing_under_costume_buttondown_slacks_service), 3),
            Dm.kv(typeof(obj_item_clothing_under_costume_buttondown_skirt_service), 3),
            Dm.kv(typeof(obj_item_clothing_neck_bowtie), 6),
            Dm.kv(typeof(obj_item_clothing_accessory_waistcoat), 4),
            Dm.kv(typeof(obj_item_clothing_under_suit_waiter), 4),
            Dm.kv(typeof(obj_item_clothing_suit_apron), 3),
            Dm.kv(typeof(obj_item_clothing_suit_apron_overalls), 3),
            Dm.kv(typeof(obj_item_clothing_head_costume_maid_headband), 3),
            Dm.kv(typeof(obj_item_clothing_under_costume_maid), 3),
            Dm.kv(typeof(obj_item_clothing_under_rank_civilian_janitor_maid), 3),
            Dm.kv(typeof(obj_item_clothing_accessory_maidapron), 3)
        );
        Dm.append(ref gvars_datum_init_order, "autodrobe_service_items");
    }

    // tg: code/modules/vending/autodrobe.dm:158  GLOBAL_LIST_INIT(autodrobe_other_items, list(...))
    public DmList autodrobe_other_items;

    // tg: code/modules/vending/autodrobe.dm:158  (GLOBAL_MANAGED(autodrobe_other_items, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalautodrobe_other_items()
    {
        autodrobe_other_items = Dm.list(
            Dm.kv(typeof(obj_item_clothing_head_wig_random), 6),
            Dm.kv(typeof(obj_item_clothing_head_flatcap), 3),
            Dm.kv(typeof(obj_item_clothing_suit_jacket_miljacket), 3),
            Dm.kv(typeof(obj_item_clothing_shoes_workboots_black), 3),
            Dm.kv(typeof(obj_item_clothing_mask_fakemoustache), 3),
            Dm.kv(typeof(obj_item_clothing_glasses_cold), 3),
            Dm.kv(typeof(obj_item_clothing_glasses_heat), 3),
            Dm.kv(typeof(obj_item_clothing_mask_gas_cyborg), 3),
            Dm.kv(typeof(obj_item_clothing_mask_joy), 3),
            Dm.kv(typeof(obj_item_clothing_mask_gas_prop), 4),
            Dm.kv(typeof(obj_item_clothing_mask_gas_atmosprop), 3),
            Dm.kv(typeof(obj_item_clothing_mask_animal_small_tribal), 1)
        );
        Dm.append(ref gvars_datum_init_order, "autodrobe_other_items");
    }

    // tg: code/modules/vending/autodrobe.dm:173  GLOBAL_LIST_INIT(autodrobe_premium_items, list(...))
    public DmList autodrobe_premium_items;

    // tg: code/modules/vending/autodrobe.dm:173  (GLOBAL_MANAGED(autodrobe_premium_items, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalautodrobe_premium_items()
    {
        autodrobe_premium_items = Dm.list(
            Dm.kv(typeof(obj_item_clothing_suit_costume_pirate_captain), 2),
            Dm.kv(typeof(obj_item_clothing_head_costume_pirate_captain), 2),
            Dm.kv(typeof(obj_item_clothing_under_rank_civilian_clown_rainbow), 1),
            Dm.kv(typeof(obj_item_clothing_head_helmet_roman_fake), 3),
            Dm.kv(typeof(obj_item_clothing_head_helmet_roman_legionnaire_fake), 3),
            Dm.kv(typeof(obj_item_clothing_under_costume_roman), 3),
            Dm.kv(typeof(obj_item_clothing_shoes_roman), 3),
            Dm.kv(typeof(obj_item_shield_roman_fake), 3),
            Dm.kv(typeof(obj_item_clothing_suit_chaplainsuit_clownpriest), 1),
            Dm.kv(typeof(obj_item_clothing_head_chaplain_clownmitre), 1),
            Dm.kv(typeof(obj_item_skub), 1),
            Dm.kv(typeof(obj_item_clothing_suit_hooded_mysticrobe), 1),
            Dm.kv(typeof(obj_item_clothing_under_dress_wedding_dress), 1),
            Dm.kv(typeof(obj_item_clothing_under_suit_tuxedo), 1),
            Dm.kv(typeof(obj_item_clothing_head_costume_weddingveil), 1),
            Dm.kv(typeof(obj_item_storage_belt_fannypack_cummerbund), 1),
            Dm.kv(typeof(obj_item_clothing_suit_costume_drfreeze_coat), 1),
            Dm.kv(typeof(obj_item_clothing_under_costume_drfreeze), 1),
            Dm.kv(typeof(obj_item_clothing_head_costume_drfreezehat), 1)
        );
        Dm.append(ref gvars_datum_init_order, "autodrobe_premium_items");
    }

    // tg: code/modules/vending/autodrobe.dm:195  GLOBAL_LIST_INIT(autodrobe_contraband_items, list(...))
    public DmList autodrobe_contraband_items;

    // tg: code/modules/vending/autodrobe.dm:195  (GLOBAL_MANAGED(autodrobe_contraband_items, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalautodrobe_contraband_items()
    {
        autodrobe_contraband_items = Dm.list(
            Dm.kv(typeof(obj_item_clothing_glasses_blindfold), 1),
            Dm.kv(typeof(obj_item_clothing_glasses_sunglasses_gar), 2),
            Dm.kv(typeof(obj_item_clothing_head_costume_powdered_wig), 1),
            Dm.kv(typeof(obj_item_clothing_head_costume_tv_head), 1),
            Dm.kv(typeof(obj_item_clothing_mask_muzzle), 2),
            Dm.kv(typeof(obj_item_clothing_shoes_clown_shoes_meown_shoes), 1),
            Dm.kv(typeof(obj_item_clothing_shoes_clown_shoes_moffers), 1),
            Dm.kv(typeof(obj_item_clothing_shoes_ducky_shoes), 1),
            Dm.kv(typeof(obj_item_clothing_suit_costume_judgerobe), 1),
            Dm.kv(typeof(obj_item_clothing_head_costume_lobsterhat), 1),
            Dm.kv(typeof(obj_item_clothing_under_costume_lobster), 1),
            Dm.kv(typeof(obj_item_gun_magic_wand_nothing), 2),
            Dm.kv(typeof(obj_item_skillchip_musical), 3),
            Dm.kv(typeof(obj_item_storage_box_tape_wizard), 1)
        );
        Dm.append(ref gvars_datum_init_order, "autodrobe_contraband_items");
    }

    // tg: code/modules/vending/autodrobe.dm:212  GLOBAL_VAR_INIT(all_autodrobe_items, (autodrobe_costumes_items + ... + autodrobe_contraband_items))
    public object all_autodrobe_items;

    // tg: code/modules/vending/autodrobe.dm:212  (GLOBAL_MANAGED(all_autodrobe_items, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalall_autodrobe_items()
    {
        all_autodrobe_items = Dm.add(Dm.add(Dm.add(Dm.add(Dm.add(Dm.add(Dm.add(Dm.add(Dm.add(autodrobe_costumes_items,
            autodrobe_costumes_items),
            autodrobe_supernatural_items),
            autodrobe_entretainers_items),
            autodrobe_fancy_items),
            autodrobe_animal_items),
            autodrobe_service_items),
            autodrobe_other_items),
            autodrobe_premium_items),
            autodrobe_contraband_items);
        Dm.append(ref gvars_datum_init_order, "all_autodrobe_items");
    }
}

// tg: code/modules/vending/autodrobe.dm:225
public partial class obj_machinery_vending_autodrobe : obj_machinery_vending
{
    // tg: code/modules/vending/autodrobe.dm:243
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        product_categories = Dm.list(
            Dm.list(Dm.kv("name", "Costumes"), Dm.kv("icon", "mask"), Dm.kv("products", GLOB.autodrobe_costumes_items)),
            Dm.list(Dm.kv("name", "Supernatural"), Dm.kv("icon", "hand-sparkles"), Dm.kv("products", GLOB.autodrobe_supernatural_items)),
            Dm.list(Dm.kv("name", "Entertainers"), Dm.kv("icon", "masks-theater"), Dm.kv("products", GLOB.autodrobe_entretainers_items)),
            Dm.list(Dm.kv("name", "Fancy"), Dm.kv("icon", "user-tie"), Dm.kv("products", GLOB.autodrobe_fancy_items)),
            Dm.list(Dm.kv("name", "Animals"), Dm.kv("icon", "paw"), Dm.kv("products", GLOB.autodrobe_animal_items)),
            Dm.list(Dm.kv("name", "Service"), Dm.kv("icon", "kitchen-set"), Dm.kv("products", GLOB.autodrobe_service_items)),
            Dm.list(Dm.kv("name", "Other"), Dm.kv("icon", "star"), Dm.kv("products", GLOB.autodrobe_other_items))
        );
        premium = GLOB.autodrobe_premium_items;
        contraband = GLOB.autodrobe_contraband_items;

        return base.Initialize(mapload, __extra);
    }
}
