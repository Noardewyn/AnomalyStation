// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/mulligan_kit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign G1c (D316): the whole file: the fake identity kit (after the mulligan serum: a crew record, an Assistant's
// ID card and the arrivals announcement signaller) and the signaller. The var blocks are generated (D111).

// tg: code/game/objects/items/devices/mulligan_kit.dm:1
public partial class obj_item_fake_identity_kit : obj_item
{
    // tg: code/game/objects/items/devices/mulligan_kit.dm:11
    public override async DmTask<object> examine_more(mob user = null)
    {
        DmList dot = await base.examine_more(user) as DmList;
        Dm.append(dot, span_info(I18n.T("Using this kit after exposure to Mulligan serum will create a fake identity for your new appearance.")));
        Dm.append(dot, span_info(I18n.T("This will add you to various station manifests, create an Assistant-level ID card, and announce your arrival over the radio.")));
        return dot;
    }

    // tg: code/game/objects/items/devices/mulligan_kit.dm:16
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (user is not mob_living_carbon_human human_user)
        {
            balloon_alert(user, "can't pass as employee!");
            return dot;
        }
        if (Dm.truthy(find_record(human_user.real_name)))
        {
            balloon_alert(user, "records already exist!");
            return dot;
        }

        await human_user.temporarilyRemoveItemFromInventory(this);
        human_user.playsound_local(human_user, "sound/items/cards/cardshuffle.ogg", 50, TRUE);

        obj_item_card_id_advanced original_id = human_user.get_idcard(hand_first: FALSE) as obj_item_card_id_advanced;
        if (original_id != null)
            await human_user.temporarilyRemoveItemFromInventory(original_id);

        datum_job job = SSjob.get_job(assigned_job) as datum_job;
        human_user.mind.set_assigned_role(job);

        Type job_outfit = job.outfit;
        object id_trim = Dm.initial(job_outfit, (datum_outfit_job p) => p.id_trim);
        var fake_id = Dm.@new<obj_item_card_id_advanced>();

        if (Dm.truthy(id_trim))
        {
            await SSid_access.apply_trim_to_card(fake_id, id_trim);
            shuffle_inplace(fake_id.access);
        }

        fake_id.registered_name = human_user.real_name;
        if (Dm.truthy(human_user.age))
            fake_id.registered_age = Dm.num(human_user.age);
        fake_id.update_label();
        await fake_id.update_icon();

        object placed_in = await human_user.equip_in_one_of_slots(fake_id, Dm.list(
            LOCATION_ID,
            LOCATION_LPOCKET,
            LOCATION_RPOCKET,
            LOCATION_BACKPACK,
            LOCATION_HANDS), qdel_on_fail: FALSE, indirect_action: TRUE);
        if (placed_in == null)
        {
            await fake_id.forceMove(human_user.drop_location() as atom);
            to_chat(human_user, span_warning(I18n.T("You drop your new ID card on the ground.")));
        }
        else
            to_chat(human_user, span_notice(I18n.T("You quickly put your new ID card [].", placed_in)));

        human_user.update_ID_card();

        var dummy = Dm.@new<mob_living_carbon_human_dummy_consistent>(); // For manifest rendering, unfortunately
        dummy.physique = human_user.physique;
        await human_user.dna.copy_dna(dummy.dna, Dm.bitor(COPY_DNA_SE, COPY_DNA_SPECIES));
        human_user.copy_clothing_prefs(dummy);
        await dummy.updateappearance(icon_update: TRUE, mutcolor_update: TRUE, mutations_overlay_update: TRUE);
        await dummy.dress_up_as_job(job, visual_only: TRUE, player_client: human_user.client);

        GLOB.manifest.inject(human_user, appearance_proxy: dummy);
        qdel(dummy); // QDEL_NULL

        if (original_id != null)
        {
            object returned_to = await human_user.equip_in_one_of_slots(original_id, Dm.list(
                LOCATION_BACKPACK,
                LOCATION_LPOCKET,
                LOCATION_RPOCKET,
                LOCATION_HANDS), qdel_on_fail: FALSE, indirect_action: TRUE);
            if (returned_to == null)
            {
                // tg moves the new card here (fake_id.forceMove), not the old one it says it drops: kept as tg has it
                await fake_id.forceMove(human_user.drop_location() as atom);
                to_chat(human_user, span_warning(I18n.T("You drop your old ID card on the ground.")));
            }
            else
                to_chat(human_user, span_notice(I18n.T("You stash your old ID card [].", returned_to)));
        }

        var announcer = Dm.@new<obj_item_arrival_announcer>(human_user.drop_location());
        await human_user.put_in_hands(announcer);
        to_chat(human_user, span_notice(I18n.T("You quickly eat the leftover paperwork, leaving only the signaller used to announce your arrival on the station.")));
        qdel(this);
        return dot;
    }
}

// tg: code/game/objects/items/devices/mulligan_kit.dm:87
public partial class obj_item_arrival_announcer : obj_item
{
    // tg: code/game/objects/items/devices/mulligan_kit.dm:102
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (user is not mob_living living_user)
            return dot;

        object name = living_user.real_name;
        datum_record manifest_data = find_record(name) as datum_record;
        if (manifest_data == null)
        {
            balloon_alert(user, "no records found!");
            return dot;
        }
        object job = manifest_data.rank;
        if (!Dm.eq(await tgui_alert(user, Dm.text("Announce arrival of [] as []?", name, job), "Are you ready?", Dm.list("Yes", "No"), timeout: 30 * SECONDS), "Yes"))
            return dot;
        if (QDELETED(this) || !Dm.truthy(await user.can_perform_action(this, interaction_flags_click)))
            return dot;

        await announce_arrival(living_user as mob_living_carbon_human, job, announce_to_ghosts: FALSE);
        do_sparks(1, FALSE, user);
        Dm.@new<obj_effect_decal_cleanable_ash>(user.drop_location());
        await user.temporarilyRemoveItemFromInventory(this);
        qdel(this);
        return dot;
    }
}
