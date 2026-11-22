// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/server/obj_generation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, what the server makes in a domain: the cache and curiosities, the avatar with its outfit,
// bodycam and gear (the disks and the gear signal, the active skillchips cloned), and the modular mob segments.

// tg: code/modules/bitrunning/server/obj_generation.dm:1
public partial class obj_machinery_quantum_server : obj_machinery
{
    /// Attempts to spawn a crate twice based on the list of available locations
    // tg: code/modules/bitrunning/server/obj_generation.dm:2
    public virtual async DmTask<object> attempt_spawn_cache(DmList possible_turfs = null)
    {
        if (!Dm.truthy(length(possible_turfs)))
            return TRUE;

        shuffle_inplace(possible_turfs);
        turf chosen_turf = await validate_turf(pick(possible_turfs) as turf) as turf;

        if (chosen_turf == null)
        {
            possible_turfs.Remove(chosen_turf);
            chosen_turf = await validate_turf(pick(possible_turfs) as turf) as turf;
            if (chosen_turf == null)
                throw Dm.CRASH("vdom: after two attempts, could not find a valid turf for cache", "code/modules/bitrunning/server/obj_generation.dm", 14);
        }

        await Dm.new_async<obj_structure_closet_crate_secure_bitrunning_encrypted>(chosen_turf);
        return TRUE;
    }

    /// Attempts to spawn a lootbox
    // tg: code/modules/bitrunning/server/obj_generation.dm:20
    public virtual async DmTask<object> attempt_spawn_curiosity(DmList possible_turfs = null)
    {
        if (!Dm.truthy(length(possible_turfs))) // Out of turfs to place a curiosity
            return FALSE;

        if (Dm.num(generated_domain.secondary_loot_generated) >= Dm.num(counterlist_sum(generated_domain.secondary_loot))) // Out of curiosities to place
            return FALSE;

        shuffle_inplace(possible_turfs);
        turf chosen_turf = await validate_turf(pick(possible_turfs) as turf) as turf;

        if (chosen_turf == null)
        {
            possible_turfs.Remove(chosen_turf);
            chosen_turf = await validate_turf(pick(possible_turfs) as turf) as turf;
            if (chosen_turf == null)
                throw Dm.CRASH("vdom: after two attempts, could not find a valid turf for curiosity", "code/modules/bitrunning/server/obj_generation.dm", 35);
        }

        await Dm.new_async<obj_item_storage_lockbox_bitrunning_encrypted>(chosen_turf);
        return chosen_turf;
    }

    /// Generates a new avatar for the bitrunner.
    // tg: code/modules/bitrunning/server/obj_generation.dm:41
    public virtual async DmTask<object> generate_avatar(turf destination = null, Type netsuit = null)
    {
        mob_living_carbon_human avatar = await Dm.new_async<mob_living_carbon_human>(destination);

        Type outfit_path = generated_domain.forced_outfit ?? netsuit;
        datum_outfit to_wear = await Dm.new_async(outfit_path) as datum_outfit;

        to_wear.belt = typeof(obj_item_bitrunning_host_monitor);
        to_wear.ears = null;
        to_wear.glasses = null;
        to_wear.gloves = null;
        to_wear.l_pocket = null;
        to_wear.r_pocket = null;
        to_wear.suit = null;
        to_wear.suit_store = null;

        await avatar.equipOutfit(to_wear, visuals_only: TRUE);

        obj_item_clothing_under jumpsuit = avatar.w_uniform as obj_item_clothing_under;
        if (jumpsuit != null)
            jumpsuit.set_armor(typeof(datum_armor_clothing_under));

        obj_item_clothing_head hat = Dm.locate<obj_item_clothing_head>(avatar.get_equipped_items());
        if (hat != null)
            hat.set_armor(typeof(datum_armor_none));

        if (generated_domain.forced_outfit == null)
        {
            foreach (obj thing in Dm.each<obj>(avatar.held_items))
                qdel(thing);
        }

        obj_item_storage_backpack bag = avatar.back as obj_item_storage_backpack;
        if (bag != null)
        {
            QDEL_LIST(bag.contents);

            Dm.append(bag.contents, Dm.list(
                await Dm.new_async<obj_item_storage_box_survival>(),
                await Dm.new_async<obj_item_storage_medkit_regular>(),
                await Dm.new_async<obj_item_flashlight>()));
        }

        obj_item_card_id outfit_id = avatar.wear_id as obj_item_card_id;
        if (outfit_id != null)
        {
            outfit_id.set_account(await Dm.new_async<datum_bank_account>());
            outfit_id.registered_account.replaceable = FALSE;

            await SSid_access.apply_trim_to_card(outfit_id, typeof(datum_id_trim_bit_avatar));
        }

        await avatar.AddComponent(
            typeof(datum_component_simple_bodycam),
            Dm.named("camera_name", "bitrunner bodycam"),
            Dm.named("c_tag", Dm.text("Avatar []", avatar.real_name)),
            Dm.named("network", BITRUNNER_CAMERA_NET),
            Dm.named("emp_proof", TRUE));

        return avatar;
    }

    /// Loads in any mob segments of the map
    // tg: code/modules/bitrunning/server/obj_generation.dm:99
    public virtual async DmTask<object> load_mob_segments()
    {
        if (!Dm.truthy(length(generated_domain.mob_modules)))
            return TRUE;

        float current_index = 1;
        shuffle_inplace(generated_domain.mob_modules);

        foreach (obj_effect_landmark_bitrunning_mob_segment landmark in Dm.each<obj_effect_landmark_bitrunning_mob_segment>(GLOB.landmarks_list))
        {
            if (current_index > length(generated_domain.mob_modules))
            {
                stack_trace("vdom: mobs segments are set to unique, but there are more landmarks than available segments", "code/modules/bitrunning/server/obj_generation.dm", 109);
                return FALSE;
            }

            object path;
            if (Dm.truthy(generated_domain.modular_unique_mobs))
            {
                path = generated_domain.mob_modules[current_index];
                current_index += 1;
            }
            else
                path = pick(generated_domain.mob_modules);

            datum_modular_mob_segment segment = await Dm.new_async((Type)path) as datum_modular_mob_segment;

            DmList mob_spawns = await landmark.spawn_mobs(get_turf(landmark), segment) as DmList;
            if (Dm.truthy(length(mob_spawns)))
                Dm.append(ref mutation_candidate_refs, mob_spawns);

            qdel(landmark);
            qdel(segment);
        }

        return TRUE;
    }

    /// Scans over neo's contents for bitrunning tech disks. Loads the items or abilities onto the avatar.
    // tg: code/modules/bitrunning/server/obj_generation.dm:131
    public virtual async DmTask stock_gear(mob_living_carbon_human avatar = null, mob_living_carbon_human neo = null, datum_lazy_template_virtual_domain generated_domain = null)
    {
        float domain_forbids_flags = generated_domain.domain_flags;

        DmList import_ban = Dm.list();
        DmList disk_ban = Dm.list();
        if (Dm.truthy(Dm.bitand(domain_forbids_flags, DOMAIN_FORBIDS_ITEMS)))
        {
            Dm.append(ref import_ban, "smuggled digital equipment");
            Dm.append(ref disk_ban, "items");
        }
        if (Dm.truthy(Dm.bitand(domain_forbids_flags, DOMAIN_FORBIDS_ABILITIES)))
        {
            Dm.append(ref import_ban, "imported_abilities");
            Dm.append(ref disk_ban, "powers");
        }

        if (Dm.truthy(length(import_ban)))
            to_chat(neo, span_warning(I18n.T("This domain forbids the use of [], your externally loaded [] will not be granted!", english_list(import_ban), english_list(disk_ban))));

        object return_flags = NONE;
        return_flags = neo.SEND_SIGNAL(COMSIG_BITRUNNER_STOCKING_GEAR, Dm.list(neo, avatar, domain_forbids_flags));

        if (Dm.truthy(Dm.bitand(return_flags, BITRUNNER_GEAR_LOAD_FAILED)))
            to_chat(neo, span_warning(I18n.T("At least one of your external data sources has encountered a failure in its loading process. Check for overlapping or inactive disks.")));
        if (Dm.truthy(Dm.bitand(return_flags, BITRUNNER_GEAR_LOAD_BLOCKED)))
            to_chat(neo, span_warning(I18n.T("At least one of your external data sources has been blocked from fully loading. Check domain restrictions.")));

        obj_item_organ_brain neo_brain = neo.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
        foreach (obj_item_skillchip skill_chip in Dm.each_cast<obj_item_skillchip>(neo_brain?.skillchips))
        {
            if (!Dm.truthy(skill_chip.active))
                continue;
            obj_item_skillchip clone_chip = await Dm.new_async(skill_chip.type) as obj_item_skillchip;
            await avatar.implant_skillchip(clone_chip, force: TRUE);
            await clone_chip.try_activate_skillchip(silent: TRUE, force: TRUE);
        }
    }
}
