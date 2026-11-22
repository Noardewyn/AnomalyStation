// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/arcade.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4a, D316: the toy spawner of the mail's goodies draws from it; the arcade machines are a later
// milestone).

public partial class datum_controller_global_vars
{
    ///List of all things that can be dispensed by an arcade cabinet and the weight of them being picked.
    // tg: code/_globalvars/arcade.dm:2  GLOBAL_LIST_INIT(arcade_prize_pool, list(...))
    public DmList arcade_prize_pool;

    // tg: code/_globalvars/arcade.dm:2  (GLOBAL_MANAGED(arcade_prize_pool, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalarcade_prize_pool()
    {
        arcade_prize_pool = Dm.list(
            Dm.kv(typeof(obj_item_storage_box_snappops), 2),
            Dm.kv(typeof(obj_item_toy_talking_ai), 2),
            Dm.kv(typeof(obj_item_toy_talking_codex_gigas), 2),
            Dm.kv(typeof(obj_item_clothing_under_syndicate_tacticool), 2),
            Dm.kv(typeof(obj_item_toy_sword), 2),
            Dm.kv(typeof(obj_item_toy_gun), 2),
            Dm.kv(typeof(obj_item_gun_ballistic_shotgun_toy_crossbow), 2),
            Dm.kv(typeof(obj_item_storage_box_fakesyndiesuit), 2),
            Dm.kv(typeof(obj_item_storage_crayons), 2),
            Dm.kv(typeof(obj_item_toy_spinningtoy), 2),
            Dm.kv(typeof(obj_item_toy_spinningtoy_dark_matter), 1),
            Dm.kv(typeof(obj_item_toy_balloon_arrest), 2),
            Dm.kv(typeof(obj_item_toy_mecha_ripley), 1),
            Dm.kv(typeof(obj_item_toy_mecha_ripleymkii), 1),
            Dm.kv(typeof(obj_item_toy_mecha_hauler), 1),
            Dm.kv(typeof(obj_item_toy_mecha_clarke), 1),
            Dm.kv(typeof(obj_item_toy_mecha_odysseus), 1),
            Dm.kv(typeof(obj_item_toy_mecha_gygax), 1),
            Dm.kv(typeof(obj_item_toy_mecha_durand), 1),
            Dm.kv(typeof(obj_item_toy_mecha_savannahivanov), 1),
            Dm.kv(typeof(obj_item_toy_mecha_phazon), 1),
            Dm.kv(typeof(obj_item_toy_mecha_honk), 1),
            Dm.kv(typeof(obj_item_toy_mecha_darkgygax), 1),
            Dm.kv(typeof(obj_item_toy_mecha_mauler), 1),
            Dm.kv(typeof(obj_item_toy_mecha_darkhonk), 1),
            Dm.kv(typeof(obj_item_toy_mecha_deathripley), 1),
            Dm.kv(typeof(obj_item_toy_mecha_reticence), 1),
            Dm.kv(typeof(obj_item_toy_mecha_marauder), 1),
            Dm.kv(typeof(obj_item_toy_mecha_seraph), 1),
            Dm.kv(typeof(obj_item_toy_mecha_firefighter), 1),
            Dm.kv(typeof(obj_item_toy_cards_deck), 2),
            Dm.kv(typeof(obj_item_toy_nuke), 2),
            Dm.kv(typeof(obj_item_toy_minimeteor), 2),
            Dm.kv(typeof(obj_item_toy_redbutton), 2),
            Dm.kv(typeof(obj_item_toy_talking_owl), 2),
            Dm.kv(typeof(obj_item_toy_talking_griffin), 2),
            Dm.kv(typeof(obj_item_coin_antagtoken), 2),
            Dm.kv(typeof(obj_item_stack_tile_fakepit_loaded), 2),
            Dm.kv(typeof(obj_item_stack_tile_eighties_loaded), 2),
            Dm.kv(typeof(obj_item_toy_toy_xeno), 2),
            Dm.kv(typeof(obj_item_storage_box_actionfigure), 1),
            Dm.kv(typeof(obj_item_restraints_handcuffs_fake), 2),
            Dm.kv(typeof(obj_item_grenade_chem_grenade_glitter_pink), 1),
            Dm.kv(typeof(obj_item_grenade_chem_grenade_glitter_blue), 1),
            Dm.kv(typeof(obj_item_grenade_chem_grenade_glitter), 1),
            Dm.kv(typeof(obj_item_toy_eightball), 2),
            Dm.kv(typeof(obj_item_toy_windup_toolbox), 2),
            Dm.kv(typeof(obj_item_toy_clockwork_watch), 2),
            Dm.kv(typeof(obj_item_toy_toy_dagger), 2),
            Dm.kv(typeof(obj_item_extendohand_acme), 1),
            Dm.kv(typeof(obj_item_hot_potato_harmless_toy), 1),
            Dm.kv(typeof(obj_item_card_emagfake), 1),
            Dm.kv(typeof(obj_item_clothing_shoes_wheelys), 2),
            Dm.kv(typeof(obj_item_clothing_shoes_kindle_kicks), 2),
            Dm.kv(typeof(obj_item_toy_plush_moth), 2),
            Dm.kv(typeof(obj_item_toy_plush_pkplush), 2),
            Dm.kv(typeof(obj_item_toy_plush_rouny), 2),
            Dm.kv(typeof(obj_item_toy_plush_abductor), 2),
            Dm.kv(typeof(obj_item_toy_plush_abductor_agent), 2),
            Dm.kv(typeof(obj_item_toy_plush_shark), 2),
            Dm.kv(typeof(obj_item_toy_plush_monkey), 2),
            Dm.kv(typeof(obj_item_storage_belt_military_snack_full), 2),
            Dm.kv(typeof(obj_item_toy_brokenradio), 2),
            Dm.kv(typeof(obj_item_toy_braintoy), 2),
            Dm.kv(typeof(obj_item_toy_eldritch_book), 2),
            Dm.kv(typeof(obj_item_storage_box_heretic_box), 1),
            Dm.kv(typeof(obj_item_toy_foamfinger), 2),
            Dm.kv(typeof(obj_item_clothing_glasses_trickblindfold), 2),
            Dm.kv(typeof(obj_item_clothing_mask_party_horn), 2),
            Dm.kv(typeof(obj_item_storage_box_party_poppers), 2));
        Dm.append(ref gvars_datum_init_order, "arcade_prize_pool");
    }
}
