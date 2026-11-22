// Translated from tgstation (snapshot 1d99bd7f): code/game/gamemodes/objective_items.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate package A1, D316): GLOB.steal_item_handler and its handler, /datum/objective_item and its steal targets, and the
// add_stealing_item_objective of every live target item (the advanced magboots' is in clothing/job_gear_j5.cs). Inert with rows
// (not-translated.md section 130): the targets whose own procs wait for their items or systems (the nuclear disk: N1; the plutonium
// core and the supermatter sliver with their syndicate kits: N1, G3; a functional AI: the silicons; the research server's hard drive
// came with gaps RD2), and the add_stealing_item_objective of the inert target items (they never initialize, so their targets are never on the
// map and drop out at New, as in tg with the item missing). The var blocks are generated.

public partial class datum_controller_global_vars
{
    // tg: code/game/gamemodes/objective_items.dm:1  GLOBAL_DATUM_INIT(steal_item_handler, /datum/objective_item_handler, new())
    public datum_objective_item_handler steal_item_handler;

    // tg: code/game/gamemodes/objective_items.dm:1  (GLOBAL_MANAGED(steal_item_handler, new()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsteal_item_handler()
    {
        steal_item_handler = Dm.@new<datum_objective_item_handler>();
        Dm.append(ref gvars_datum_init_order, "steal_item_handler");
    }
}

public static partial class GlobalProcs
{
    // tg: code/game/gamemodes/objective_items.dm:3
    public static object add_item_to_steal(object source = null, object type = null)
    {
        object items = GLOB.steal_item_handler.objectives_by_path[type];
        Dm.append(ref items, source);
        GLOB.steal_item_handler.objectives_by_path[type] = items;
        return type;
    }
}

/// Holds references to information about all of the items you might need to steal for objectives
// tg: code/game/gamemodes/objective_items.dm:8
public partial class datum_objective_item_handler : datum
{
    // tg: code/game/gamemodes/objective_items.dm:12
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        objectives_by_path = Dm.list();
        foreach (object item in Dm.each(subtypesof(typeof(datum_objective_item))))
            objectives_by_path[Dm.initial(item as Type, (datum_objective_item p) => p.targetitem)] = Dm.list();
        RegisterSignal(SSatoms, COMSIG_SUBSYSTEM_POST_INITIALIZE, nameof(save_items));
        RegisterSignal(SSdcs, COMSIG_GLOB_NEW_ITEM, nameof(new_item_created));
    }

    // tg: code/game/gamemodes/objective_items.dm:20
    [SignalHandler]
    public virtual void new_item_created(datum source = null, obj_item item = null)
    {
        if (HAS_TRAIT(item, TRAIT_ITEM_OBJECTIVE_BLOCKED))
            return;
        if (!Dm.truthy(generated_items))
        {
            item.add_stealing_item_objective();
            return;
        }
        object typepath = item.add_stealing_item_objective();
        if (typepath != null)
            register_item(item, typepath);
    }

    /// Registers all items that are potentially stealable and removes ones that aren't.
    /// We still need to do things this way because on mapload, items may not be on the station until everything has finished loading.
    // tg: code/game/gamemodes/objective_items.dm:33
    [SignalHandler]
    public virtual void save_items()
    {
        foreach (object typepath in Dm.each(objectives_by_path))
        {
            DmList obj_by_path_cache = ((DmList)objectives_by_path[typepath]).Copy();
            foreach (object @object in Dm.each(obj_by_path_cache))
                register_item(@object as atom, typepath);
        }
        generated_items = TRUE;
    }

    // tg: code/game/gamemodes/objective_items.dm:41
    public virtual void register_item(atom @object = null, object typepath = null)
    {
        turf place = get_turf(@object);
        if (place == null || !Dm.truthy(is_station_level(place.z)))
        {
            Dm.remove((DmList)objectives_by_path[typepath], @object);
            return;
        }
        RegisterSignal(@object, COMSIG_QDELETING, nameof(remove_item));
    }

    // tg: code/game/gamemodes/objective_items.dm:48
    [SignalHandler]
    public virtual void remove_item(atom source = null)
    {
        foreach (object typepath in Dm.each(objectives_by_path))
            Dm.remove((DmList)objectives_by_path[typepath], source);
    }
}

//Contains the target item datums for Steal objectives.
// tg: code/game/gamemodes/objective_items.dm:54
public partial class datum_objective_item : datum
{
    /// For objectives with special checks (does that intellicard have an ai in it? etcetc)
    // tg: code/game/gamemodes/objective_items.dm:92
    public virtual object check_special_completion(obj_item thing = null)
    {
        return TRUE;
    }

    /// Takes a list of minds and returns true if this is a valid objective to give to a team of these minds
    // tg: code/game/gamemodes/objective_items.dm:96
    public virtual object valid_objective_for(DmList potential_thieves = null, float? require_owner = null)
    {
        require_owner ??= FALSE;
        if (!Dm.truthy(target_exists()) || (Dm.truthy(require_owner) && !Dm.truthy(owner_exists())))
            return FALSE;
        foreach (object t in Dm.each(potential_thieves))
        {
            datum_mind possible_thief = (datum_mind)t;
            datum_job role = possible_thief.assigned_role;
            if (Dm.@in(role.title, excludefromjob))
                return FALSE;
        }
        return TRUE;
    }

    /// Returns true if the target item exists
    // tg: code/game/gamemodes/objective_items.dm:106
    public virtual object target_exists()
    {
        return Dm.truthy(exists_on_map) ? length(GLOB.steal_item_handler.objectives_by_path[targetitem]) : TRUE;
    }

    /// Returns true if one of the item's owners exists somewhere
    // tg: code/game/gamemodes/objective_items.dm:110
    public virtual object owner_exists()
    {
        if (!Dm.truthy(length(item_owner)))
            return TRUE;
        foreach (object p in Dm.each(GLOB.player_list))
        {
            mob_living player = (mob_living)p;
            if (Dm.@in(player.mind?.assigned_role.title, item_owner) && player.stat != DEAD && !Dm.truthy(is_centcom_level(player.z)))
                return TRUE;
        }
        return FALSE;
    }
}

// tg: code/game/gamemodes/objective_items.dm:118
public partial class datum_objective_item_steal : datum_objective_item
{
    // tg: code/game/gamemodes/objective_items.dm:118
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (Dm.truthy(target_exists()))
            Dm.append(GLOB.possible_items, this);
        else
            qdel(this);
    }

    // tg: code/game/gamemodes/objective_items.dm:125
    public override object Destroy(float? force = null)
    {
        Dm.remove(GLOB.possible_items, this);
        return base.Destroy(force);
    }
}

// tg: code/game/gamemodes/objective_items.dm:281
public partial class datum_objective_item_steal_traitor_telebaton : datum_objective_item_steal_traitor
{
    // tg: code/game/gamemodes/objective_items.dm:297
    public override object check_special_completion(obj_item thing = null)
    {
        return Dm.num(!istype(thing, typeof(obj_item_melee_baton_telescopic_contractor_baton)));
    }
}

// tg: code/game/gamemodes/objective_items.dm:582
public partial class datum_objective_item_steal_blueprints : datum_objective_item_steal
{
    // tg: code/game/gamemodes/objective_items.dm:595
    public override object check_special_completion(obj_item I = null)
    {
        if (istype(I, typeof(obj_item_blueprints)))
            return TRUE;
        if (istype(I, typeof(obj_item_photo)))
        {
            obj_item_photo P = (obj_item_photo)I;
            if (Dm.truthy(P.picture.has_blueprints)) //if the blueprints are in frame
                return TRUE;
        }
        return FALSE;
    }
}

// tg: code/game/gamemodes/objective_items.dm:757
public partial class datum_objective_item_steal_spy_energy_gun : datum_objective_item_steal_spy
{
    // tg: code/game/gamemodes/objective_items.dm:776
    public override object check_special_completion(obj_item thing = null)
    {
        return Dm.num(Dm.eq(thing.type, typeof(obj_item_gun_energy_e_gun)));
    }
}

// tg: code/game/gamemodes/objective_items.dm:783
public partial class datum_objective_item_steal_spy_laser_gun : datum_objective_item_steal_spy
{
    // tg: code/game/gamemodes/objective_items.dm:802
    public override object check_special_completion(obj_item thing = null)
    {
        return Dm.num(Dm.eq(thing.type, typeof(obj_item_gun_energy_laser)));
    }
}

// tg: code/game/gamemodes/objective_items.dm:936
public partial class datum_objective_item_steal_spy_stun_baton : datum_objective_item_steal_spy
{
    // tg: code/game/gamemodes/objective_items.dm:950
    public override object check_special_completion(obj_item thing = null)
    {
        return Dm.num(!istype(thing, typeof(obj_item_melee_baton_security_cattleprod)));
    }
}

// tg: code/game/gamemodes/objective_items.dm:953
public partial class datum_objective_item_steal_spy_det_baton : datum_objective_item_steal_spy
{
    // tg: code/game/gamemodes/objective_items.dm:968
    public override object check_special_completion(obj_item thing = null)
    {
        return Dm.num(Dm.eq(thing.type, typeof(obj_item_melee_baton)));
    }
}

// tg: code/game/gamemodes/objective_items.dm:683 (gaps OD1: the Space Law book is live with its suicide)
public partial class obj_item_book_manual_wiki_security_space_law : obj_item_book_manual_wiki
{
    // tg: code/game/gamemodes/objective_items.dm:683
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_book_manual_wiki_security_space_law));
    }
}

// tg: code/game/gamemodes/objective_items.dm:313 (gaps P4a: the cargo budget card is live with the economy)
public partial class obj_item_card_id_departmental_budget_car : obj_item_card_id_departmental_budget
{
    // tg: code/game/gamemodes/objective_items.dm:313
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_card_id_departmental_budget_car));
    }
}

// tg: code/game/gamemodes/objective_items.dm:346
public partial class obj_item_card_id_advanced_gold_captains_spare : obj_item_card_id_advanced_gold
{
    // tg: code/game/gamemodes/objective_items.dm:346
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_card_id_advanced_gold_captains_spare));
    }
}

// tg: code/game/gamemodes/objective_items.dm:411 (the owner's play test, 2026-10-15: the Captain's jetpack was inert without it)
public partial class obj_item_tank_jetpack_captain : obj_item_tank_jetpack
{
    // tg: code/game/gamemodes/objective_items.dm:411
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_tank_jetpack_captain));
    }
}

// tg: code/game/gamemodes/objective_items.dm:165 (M5 slice 5: the fire axe turns live)
public partial class obj_item_fireaxe : obj_item
{
    // tg: code/game/gamemodes/objective_items.dm:165
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_fireaxe));
    }
}

// tg: code/game/gamemodes/objective_items.dm:143
public partial class obj_item_gun_ballistic_shotgun_doublebarrel : obj_item_gun_ballistic_shotgun
{
    // tg: code/game/gamemodes/objective_items.dm:143
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_gun_ballistic_shotgun_doublebarrel));
    }
}

// tg: code/game/gamemodes/objective_items.dm:194
public partial class obj_item_nullrod : obj_item
{
    // tg: code/game/gamemodes/objective_items.dm:194
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_nullrod));
    }
}

// tg: code/game/gamemodes/objective_items.dm:206
public partial class obj_item_clothing_shoes_clown_shoes : obj_item_clothing_shoes
{
    // tg: code/game/gamemodes/objective_items.dm:206
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_clothing_shoes_clown_shoes));
    }
}

// tg: code/game/gamemodes/objective_items.dm:218
public partial class obj_item_clothing_mask_gas_mime : obj_item_clothing_mask_gas
{
    // tg: code/game/gamemodes/objective_items.dm:218
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_clothing_mask_gas_mime));
    }
}

// tg: code/game/gamemodes/objective_items.dm:231
public partial class obj_item_gun_energy_recharge_kinetic_accelerator : obj_item_gun_energy_recharge
{
    // tg: code/game/gamemodes/objective_items.dm:231
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_gun_energy_recharge_kinetic_accelerator));
    }
}

// tg: code/game/gamemodes/objective_items.dm:243
public partial class obj_item_clothing_mask_fakemoustache_italian : obj_item_clothing_mask_fakemoustache
{
    // tg: code/game/gamemodes/objective_items.dm:243
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_clothing_mask_fakemoustache_italian));
    }
}

// tg: code/game/gamemodes/objective_items.dm:255
public partial class obj_item_gun_ballistic_revolver_c38_detective : obj_item_gun_ballistic_revolver_c38
{
    // tg: code/game/gamemodes/objective_items.dm:255
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_gun_ballistic_revolver_c38_detective));
    }
}

// tg: code/game/gamemodes/objective_items.dm:267
public partial class obj_item_clothing_accessory_lawyers_badge : obj_item_clothing_accessory
{
    // tg: code/game/gamemodes/objective_items.dm:267
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_clothing_accessory_lawyers_badge));
    }
}

// tg: code/game/gamemodes/objective_items.dm:278
public partial class obj_item_storage_belt_utility_chief : obj_item_storage_belt_utility
{
    // tg: code/game/gamemodes/objective_items.dm:278
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_storage_belt_utility_chief));
    }
}

// tg: code/game/gamemodes/objective_items.dm:300
public partial class obj_item_melee_baton_telescopic : obj_item_melee_baton
{
    // tg: code/game/gamemodes/objective_items.dm:300
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_melee_baton_telescopic));
    }
}

// tg: code/game/gamemodes/objective_items.dm:361  (gaps GU1: the antique laser turned live)
public partial class obj_item_gun_energy_laser_captain : obj_item_gun_energy_laser
{
    // tg: code/game/gamemodes/objective_items.dm:361
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_gun_energy_laser_captain));
    }
}

// tg: code/game/gamemodes/objective_items.dm:374
public partial class obj_item_gun_energy_e_gun_hos : obj_item_gun_energy_e_gun
{
    // tg: code/game/gamemodes/objective_items.dm:374
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_gun_energy_e_gun_hos));
    }
}

// tg: code/game/gamemodes/objective_items.dm:386
public partial class obj_item_gun_ballistic_shotgun_automatic_combat_compact : obj_item_gun_ballistic_shotgun_automatic_combat
{
    // tg: code/game/gamemodes/objective_items.dm:386
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_gun_ballistic_shotgun_automatic_combat_compact));
    }
}

// tg: code/game/gamemodes/objective_items.dm:437
public partial class obj_item_clothing_accessory_medal_gold_captain : obj_item_clothing_accessory_medal_gold
{
    // tg: code/game/gamemodes/objective_items.dm:437
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_clothing_accessory_medal_gold_captain));
    }
}

// tg: code/game/gamemodes/objective_items.dm:476
public partial class obj_item_clothing_suit_hooded_ablative : obj_item_clothing_suit_hooded
{
    // tg: code/game/gamemodes/objective_items.dm:476
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_clothing_suit_hooded_ablative));
    }
}

// tg: code/game/gamemodes/objective_items.dm:503
public partial class obj_item_documents : obj_item
{
    // tg: code/game/gamemodes/objective_items.dm:503
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_documents));
    }
}

// tg: code/game/gamemodes/objective_items.dm:592
public partial class obj_item_blueprints : obj_item
{
    // tg: code/game/gamemodes/objective_items.dm:592
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_blueprints));
    }
}

// tg: code/game/gamemodes/objective_items.dm:628
public partial class obj_item_clothing_gloves_color_yellow : obj_item_clothing_gloves_color
{
    // tg: code/game/gamemodes/objective_items.dm:628
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_clothing_gloves_color_yellow));
    }
}

// tg: code/game/gamemodes/objective_items.dm:660
public partial class obj_item_stamp_denied : obj_item_stamp
{
    // tg: code/game/gamemodes/objective_items.dm:660
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_stamp_denied));
    }
}

// tg: code/game/gamemodes/objective_items.dm:671
public partial class obj_item_stamp_granted : obj_item_stamp
{
    // tg: code/game/gamemodes/objective_items.dm:671
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_stamp_granted));
    }
}

// tg: code/game/gamemodes/objective_items.dm:703
public partial class obj_item_pipe_dispenser : obj_item
{
    // tg: code/game/gamemodes/objective_items.dm:703
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_pipe_dispenser));
    }
}

// tg: code/game/gamemodes/objective_items.dm:726 (gaps H2, D333: the donut box is live)
public partial class obj_item_storage_fancy_donut_box : obj_item_storage_fancy
{
    // tg: code/game/gamemodes/objective_items.dm:726
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_storage_fancy_donut_box));
    }
}

// tg: code/game/gamemodes/objective_items.dm:740 (gaps X5: Lamarr is live with the xenomorphs)
public partial class obj_item_clothing_mask_facehugger_lamarr : obj_item_clothing_mask_facehugger
{
    // tg: code/game/gamemodes/objective_items.dm:740
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_clothing_mask_facehugger_lamarr));
    }
}

// tg: code/game/gamemodes/objective_items.dm:779
public partial class obj_item_gun_energy_e_gun : obj_item_gun_energy
{
    // tg: code/game/gamemodes/objective_items.dm:779
    public override object add_stealing_item_objective()
    {
        if (Dm.eq(type, typeof(obj_item_gun_energy_e_gun)))
            return add_item_to_steal(this, typeof(obj_item_gun_energy_e_gun));
        return null;
    }
}

// tg: code/game/gamemodes/objective_items.dm:805
public partial class obj_item_gun_energy_laser : obj_item_gun_energy
{
    // tg: code/game/gamemodes/objective_items.dm:805
    public override object add_stealing_item_objective()
    {
        if (Dm.eq(type, typeof(obj_item_gun_energy_laser)))
            return add_item_to_steal(this, typeof(obj_item_gun_energy_laser));
        return null;
    }
}

// tg: code/game/gamemodes/objective_items.dm:823
public partial class obj_item_gun_ballistic_shotgun_riot : obj_item_gun_ballistic_shotgun
{
    // tg: code/game/gamemodes/objective_items.dm:823
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_gun_ballistic_shotgun_riot));
    }
}

// tg: code/game/gamemodes/objective_items.dm:840
public partial class obj_item_gun_energy_temperature_security : obj_item_gun_energy_temperature
{
    // tg: code/game/gamemodes/objective_items.dm:840
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_gun_energy_temperature_security));
    }
}

// tg: code/game/gamemodes/objective_items.dm:639 (syndicate G3c: the plushes are live)
public partial class obj_item_toy_plush_moth : obj_item_toy_plush
{
    // tg: code/game/gamemodes/objective_items.dm:639
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_toy_plush_moth));
    }
}

// tg: code/game/gamemodes/objective_items.dm:649
public partial class obj_item_toy_plush_lizard_plushie : obj_item_toy_plush
{
    // tg: code/game/gamemodes/objective_items.dm:649
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_toy_plush_lizard_plushie));
    }
}

// tg: code/game/gamemodes/objective_items.dm:859
public partial class obj_item_stamp_head : obj_item_stamp
{
    // tg: code/game/gamemodes/objective_items.dm:859
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_stamp_head));
    }
}

// tg: code/game/gamemodes/objective_items.dm:922
public partial class obj_item_defibrillator_compact_loaded_cmo : obj_item_defibrillator_compact_loaded
{
    // tg: code/game/gamemodes/objective_items.dm:922
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_defibrillator_compact_loaded_cmo));
    }
}

// tg: code/game/gamemodes/objective_items.dm:971
public partial class obj_item_melee_baton : obj_item_melee
{
    // tg: code/game/gamemodes/objective_items.dm:971
    public override object add_stealing_item_objective()
    {
        if (Dm.eq(type, typeof(obj_item_melee_baton)))
            return add_item_to_steal(this, typeof(obj_item_melee_baton));
        return null;
    }
}

// The nuclear disk and the self-destruct's plutonium core (syndicate N1a, D316).

// tg: code/game/gamemodes/objective_items.dm:453
public partial class datum_objective_item_steal_nukedisc : datum_objective_item_steal
{
    // tg: code/game/gamemodes/objective_items.dm:464
    public override object check_special_completion(obj_item N = null)
    {
        return Dm.num(!Dm.truthy(((obj_item_disk_nuclear)N).fake));
    }
}

// tg: code/game/gamemodes/objective_items.dm:461
public partial class obj_item_disk_nuclear : obj_item_disk
{
    // tg: code/game/gamemodes/objective_items.dm:461
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_disk_nuclear));
    }
}

// tg: code/game/gamemodes/objective_items.dm:506
public partial class datum_objective_item_steal_nuke_core : datum_objective_item_steal
{
    // tg: code/game/gamemodes/objective_items.dm:517
    public override async DmTask New(params object[] __extra)
    {
        Dm.append(ref special_equipment, typeof(obj_item_storage_box_syndie_kit_nuke));
        await base.New(__extra);
    }
}

// tg: code/game/gamemodes/objective_items.dm:514
public partial class obj_item_nuke_core : obj_item
{
    // tg: code/game/gamemodes/objective_items.dm:514
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_nuke_core));
    }
}

// gaps RD2 (D333): the master R&D server's hard drive, a steal target now that the server is live.
// tg: code/game/gamemodes/objective_items.dm:520
public partial class datum_objective_item_steal_hdd_extraction : datum_objective_item_steal
{
    // tg: code/game/gamemodes/objective_items.dm:533
    public override async DmTask New(params object[] __extra)
    {
        Dm.append(ref special_equipment, typeof(obj_item_paper_guides_antag_hdd_extraction));
        await base.New(__extra);
    }
}

// tg: code/game/objects/items/tools/theft_tools.dm:133
public partial class obj_item_disk_computer_hdd_theft : obj_item_disk_computer
{
    // tg: code/game/gamemodes/objective_items.dm:530
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_disk_computer_hdd_theft));
    }
}

// tg: code/game/gamemodes/objective_items.dm:399 (gaps TP1: the hand teleporter is live)
public partial class obj_item_hand_tele : obj_item
{
    // tg: code/game/gamemodes/objective_items.dm:399
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_hand_tele));
    }
}

// tg: code/game/gamemodes/objective_items.dm:983 (gaps SR1: the sabre's sheath is live)
public partial class obj_item_storage_belt_sheath_sabre : obj_item_storage_belt_sheath
{
    // tg: code/game/gamemodes/objective_items.dm:983
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_storage_belt_sheath_sabre));
    }
}

// tg: code/game/gamemodes/objective_items.dm:325 (gaps MO1: the MODsuits are live)
public partial class obj_item_mod_control_pre_equipped_magnate : obj_item_mod_control_pre_equipped
{
    // tg: code/game/gamemodes/objective_items.dm:325
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_mod_control_pre_equipped_magnate));
    }
}

// tg: code/game/gamemodes/objective_items.dm:889 (gaps MO1: the MODsuits are live)
public partial class obj_item_mod_control_pre_equipped_advanced : obj_item_mod_control_pre_equipped
{
    // tg: code/game/gamemodes/objective_items.dm:889
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_mod_control_pre_equipped_advanced));
    }
}

// tg: code/game/gamemodes/objective_items.dm:900 (gaps MO1: the MODsuits are live)
public partial class obj_item_mod_control_pre_equipped_research : obj_item_mod_control_pre_equipped
{
    // tg: code/game/gamemodes/objective_items.dm:900
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_mod_control_pre_equipped_research));
    }
}

// tg: code/game/gamemodes/objective_items.dm:911 (gaps MO1: the MODsuits are live)
public partial class obj_item_mod_control_pre_equipped_rescue : obj_item_mod_control_pre_equipped
{
    // tg: code/game/gamemodes/objective_items.dm:911
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_mod_control_pre_equipped_rescue));
    }
}

// tg: code/game/gamemodes/objective_items.dm:933 (gaps MO1: the MODsuits are live)
public partial class obj_item_mod_control_pre_equipped_safeguard : obj_item_mod_control_pre_equipped
{
    // tg: code/game/gamemodes/objective_items.dm:933
    public override object add_stealing_item_objective()
    {
        return add_item_to_steal(this, typeof(obj_item_mod_control_pre_equipped_safeguard));
    }
}
