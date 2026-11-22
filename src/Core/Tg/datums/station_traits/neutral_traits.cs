// Translated from tgstation (snapshot 1d99bd7f): code/datums/station_traits/neutral_traits.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R2 (D327): the whole file: the neutral station traits, the birthday invitation and the party hats. Deferred leaves
// (not-translated.md section 183): Ian's extra lives (the multiple lives component is not ported) and the pet day's pets (the pet
// customization): SSstation keeps the traits whose whole effect waits out of the pool (DV432). The scryers' MODlink scryer came with
// gaps MO1, the closet anomaly's links (the eigenstate controller) with gaps RG3. The var blocks are generated (D111).

///This station traits gives 5 bananium sheets to the clown (and every dead clown out there in deep space or lavaland).
// tg: code/datums/station_traits/neutral_traits.dm:2
public partial class datum_station_trait_bananium_shipment : datum_station_trait
{
    // tg: code/datums/station_traits/neutral_traits.dm:10
    public override object get_pulsar_message()
    {
        object advisory_string = I18n.T("Advisory Level: <b>Clown Planet</b></center><BR>");
        advisory_string = Dm.add(advisory_string, I18n.T("Your sector's advisory level is Clown Planet! Our bike horns have picked up on a large bananium stash. Clowns show a large influx of clowns on your station. We highly advise you to slip any threats to keep Honkotrasen assets within the Banana Sector. The Department of Intelligence advises defending chemistry from any clowns that are trying to make baldium or space lube."));
        return advisory_string;
    }
}

// tg: code/datums/station_traits/neutral_traits.dm:46
public partial class datum_station_trait_unique_ai : datum_station_trait
{
    // tg: code/datums/station_traits/neutral_traits.dm:56
    public override void on_round_start()
    {
        base.on_round_start();
        foreach (mob_living_silicon_ai ai in Dm.each_cast<mob_living_silicon_ai>(GLOB.ai_list))
            Waiting.NoSleep(ai.show_laws());
    }
}

// tg: code/datums/station_traits/neutral_traits.dm:61
public partial class datum_station_trait_ian_adventure : datum_station_trait
{
    // tg: code/datums/station_traits/neutral_traits.dm:69
    public override void on_round_start()
    {
        foreach (mob_living_basic_pet_dog_corgi dog in Dm.each<mob_living_basic_pet_dog_corgi>(GLOB.mob_list))
        {
            if (!(istype(dog, typeof(mob_living_basic_pet_dog_corgi_ian)) || istype(dog, typeof(mob_living_basic_pet_dog_corgi_puppy_ian))))
                continue;

            // Makes this station trait more interesting. Ian probably won't go anywhere without a little external help.
            // Also gives him a couple extra lives to survive eventual tiders.
            Waiting.NoSleep(dog.deadchat_plays(DEMOCRACY_MODE | MUTE_DEMOCRACY_MESSAGES, 3 * SECONDS));
            // dog.AddComponent(/datum/component/multiple_lives, 2) and RegisterSignal(dog, COMSIG_ON_MULTIPLE_LIVES_RESPAWN,
            // PROC_REF(do_corgi_respawn)): the multiple lives component is not ported (inert)
            Dm.Deferred("code/datums/station_traits/neutral_traits.dm:77", "ian_adventure on_round_start: Ian's two extra lives (the multiple lives component is not ported)");

            // The extended safety checks at time of writing are about chasms and lava
            // if there are any chasms and lava on stations in the future, woah
            var current_turf = get_turf(dog);
            var adventure_turf = find_safe_turf(extended_safety_checks: TRUE, dense_atoms: FALSE) as turf;

            // Poof!
            Waiting.NoSleep(do_smoke(location: current_turf));
            Waiting.NoSleep(dog.forceMove(adventure_turf));
            Waiting.NoSleep(do_smoke(location: adventure_turf));
        }
    }

    /// Moves the new dog somewhere safe, equips it with the old one's inventory and makes it deadchat_playable.
    // tg: code/datums/station_traits/neutral_traits.dm:91  SIGNAL_HANDLER
    public virtual void do_corgi_respawn(mob_living_basic_pet_dog_corgi old_dog = null, mob_living_basic_pet_dog_corgi new_dog = null, object gibbed = null, object lives_left = null)
    {
        var current_turf = get_turf(new_dog);
        var adventure_turf = find_safe_turf(extended_safety_checks: TRUE, dense_atoms: FALSE) as turf;

        Waiting.NoSleep(do_smoke(location: current_turf));
        Waiting.NoSleep(new_dog.forceMove(adventure_turf));
        Waiting.NoSleep(do_smoke(location: adventure_turf));

        if (old_dog.inventory_back != null)
        {
            obj_item old_dog_back = old_dog.inventory_back;
            old_dog.inventory_back = null;
            Waiting.NoSleep(old_dog_back.forceMove(new_dog));
            new_dog.inventory_back = old_dog_back;
        }

        if (old_dog.inventory_head != null)
        {
            obj_item old_dog_hat = old_dog.inventory_head;
            old_dog.inventory_head = null;
            Waiting.NoSleep(new_dog.place_on_head(old_dog_hat));
        }

        new_dog.update_corgi_fluff();
        Waiting.NoSleep(new_dog.regenerate_icons());
        Waiting.NoSleep(new_dog.deadchat_plays(DEMOCRACY_MODE | MUTE_DEMOCRACY_MESSAGES, 3 * SECONDS));
        if (Dm.truthy(lives_left))
            RegisterSignal(new_dog, COMSIG_ON_MULTIPLE_LIVES_RESPAWN, nameof(do_corgi_respawn));

        if (!Dm.truthy(gibbed)) //The old dog will now disappear so we won't have more than one Ian at a time.
            qdel(old_dog);
    }
}

// tg: code/datums/station_traits/neutral_traits.dm:130
public partial class datum_station_trait_announcement_intern : datum_station_trait
{
    // tg: code/datums/station_traits/neutral_traits.dm:138
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        SSstation.announcer = typeof(datum_centcom_announcer_intern);
    }

    // tg: code/datums/station_traits/neutral_traits.dm:142
    public override object get_pulsar_message()
    {
        object advisory_string = I18n.T("Advisory Level: <b>(TITLE HERE)</b></center><BR>");
        advisory_string = Dm.add(advisory_string, I18n.T("(Copy/Paste the summary provided by the Threat Intelligence Office in this field. You shouldn't have any trouble with this just make sure to replace this message before hitting the send button. Also, make sure there's coffee ready for the meeting at 06:00 when you're done.)"));
        return advisory_string;
    }
}

// tg: code/datums/station_traits/neutral_traits.dm:147
public partial class datum_station_trait_announcement_medbot : datum_station_trait
{
    // tg: code/datums/station_traits/neutral_traits.dm:155
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        SSstation.announcer = typeof(datum_centcom_announcer_medbot);
    }
}

// tg: code/datums/station_traits/neutral_traits.dm:159
public partial class datum_station_trait_colored_assistants : datum_station_trait
{
    // tg: code/datums/station_traits/neutral_traits.dm:168
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);

        var new_colored_assistant_type = (Type)pick(Dm.sub(subtypesof(typeof(datum_colored_assistant)), get_configured_colored_assistant_type()) as DmList);
        GLOB.colored_assistant = Dm.@new(new_colored_assistant_type) as datum_colored_assistant;
    }
}

// tg: code/datums/station_traits/neutral_traits.dm:174
public partial class datum_station_trait_birthday : datum_station_trait
{
    // tg: code/datums/station_traits/neutral_traits.dm:189
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        RegisterSignals(SSdcs, Dm.list(COMSIG_GLOB_JOB_AFTER_SPAWN), nameof(on_job_after_spawn));
    }

    // tg: code/datums/station_traits/neutral_traits.dm:193
    public override void revert()
    {
        foreach (obj_effect_landmark_start_hangover party_spot in Dm.each<obj_effect_landmark_start_hangover>(GLOB.start_landmarks_list))
            QDEL_LIST(party_spot.party_debris);
        base.revert();
    }

    // tg: code/datums/station_traits/neutral_traits.dm:198
    public override void on_round_start()
    {
        base.on_round_start();
        if (Dm.truthy(birthday_override_ckey))
        {
            if (!Dm.truthy(check_valid_override()))
                message_admins(Dm.text("Attempted to make [] the birthday person but they are not a valid station role. A random birthday person has be selected instead.", birthday_override_ckey));
        }

        if (birthday_person == null)
        {
            DmList birthday_options = Dm.list();
            foreach (mob_living_carbon_human human in Dm.each<mob_living_carbon_human>(GLOB.human_list))
            {
                if (Dm.truthy(Dm.bitand(human.mind?.assigned_role?.job_flags, JOB_CREW_MEMBER)))
                    Dm.append(ref birthday_options, human);
            }
            if (Dm.truthy(length(birthday_options)))
            {
                birthday_person = pick(birthday_options) as mob_living_carbon_human;
                birthday_person_name = Dm.str(birthday_person.real_name);
                ADD_TRAIT(birthday_person, TRAIT_BIRTHDAY_BOY, REF(this));
            }
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(announce_birthday)), 10 * SECONDS, file: "code/datums/station_traits/neutral_traits.dm", line: 213);
    }

    // tg: code/datums/station_traits/neutral_traits.dm:215
    public virtual object check_valid_override()
    {
        var birthday_override_mob = get_mob_by_ckey(birthday_override_ckey) as mob_living_carbon_human;

        if (birthday_override_mob == null)
            return FALSE;

        if (Dm.truthy(Dm.bitand(birthday_override_mob.mind?.assigned_role?.job_flags, JOB_CREW_MEMBER)))
        {
            birthday_person = birthday_override_mob;
            birthday_person_name = Dm.str(birthday_person.real_name);
            return TRUE;
        }
        else
            return FALSE;
    }

    // tg: code/datums/station_traits/neutral_traits.dm:230
    public virtual async DmTask announce_birthday()
    {
        report_message = I18n.T("We here at Nanotrasen would all like to wish [] a very happy birthday.", birthday_person != null ? birthday_person_name : I18n.T("Employee Name"));
        priority_announce(I18n.T("Happy birthday to []! Nanotrasen wishes you a very happy [] birthday.",
            birthday_person != null ? birthday_person_name : I18n.T("Employee Name"),
            birthday_person != null ? thtotext(birthday_person.age + 1) : I18n.T("255th")));
        if (birthday_person != null)
        {
            playsound(birthday_person, Dm.resource("sound/items/party_horn.ogg"), 50);
            await birthday_person.add_mood_event("birthday", typeof(datum_mood_event_birthday));
            birthday_person = null;
        }
    }

    // tg: code/datums/station_traits/neutral_traits.dm:238  SIGNAL_HANDLER
    public virtual void on_job_after_spawn(datum source = null, datum_job job = null, mob_living spawned_mob = null)
    {
        var hat_type = (Type)pick_weight(Dm.list(
            Dm.kv(typeof(obj_item_clothing_head_costume_party_festive), 12),
            Dm.kv(typeof(obj_item_clothing_head_costume_party), 12),
            Dm.kv(typeof(obj_item_clothing_head_costume_festive), 2),
            Dm.kv(typeof(obj_item_clothing_head_utility_hardhat_cakehat), 1)));
        var hat = Dm.@new(hat_type, spawned_mob) as obj_item;
        if (!Dm.truthy(Waiting.NoSleep(spawned_mob.equip_to_slot_if_possible(hat, ITEM_SLOT_HEAD, disable_warning: TRUE))))
            spawned_mob.equip_to_storage(hat, ITEM_SLOT_BACK, indirect_action: TRUE);
        var toy_type = (Type)pick_weight(Dm.list(
            Dm.kv(typeof(obj_item_reagent_containers_spray_chemsprayer_party), 4),
            Dm.kv(typeof(obj_item_toy_balloon), 2),
            Dm.kv(typeof(obj_item_sparkler), 2),
            Dm.kv(typeof(obj_item_clothing_mask_party_horn), 2),
            Dm.kv(typeof(obj_item_storage_box_tail_pin), 1)));
        var toy = Dm.@new(toy_type, spawned_mob) as obj_item;
        if (istype(toy, typeof(obj_item_toy_balloon)))
            Waiting.NoSleep(spawned_mob.equip_to_slot_or_del(toy, ITEM_SLOT_HANDS)); //Balloons do not fit inside of backpacks.
        else
            spawned_mob.equip_to_storage(toy, ITEM_SLOT_BACK, indirect_action: TRUE);
        if (Dm.truthy(birthday_person_name)) //Anyone who joins after the annoucement gets one of these.
        {
            var birthday_invite = Dm.@new<obj_item_birthday_invite>(spawned_mob);
            birthday_invite.setup_card(birthday_person_name);
            if (!Dm.truthy(Waiting.NoSleep(spawned_mob.equip_to_slot_if_possible(birthday_invite, ITEM_SLOT_HANDS, disable_warning: TRUE))))
                spawned_mob.equip_to_storage(birthday_invite, ITEM_SLOT_BACK, indirect_action: TRUE); //Just incase someone spawns with both hands full.
        }
    }
}

// tg: code/datums/station_traits/neutral_traits.dm:268
public partial class obj_item_birthday_invite : obj_item
{
    // tg: code/datums/station_traits/neutral_traits.dm:274
    public virtual void setup_card(object birthday_name = null)
    {
        desc = I18n.T("A card stating that its []'s birthday today.", birthday_name);
        icon_state = "paperslip_words";
        icon = Dm.resource("icons/obj/service/bureaucracy.dmi");
    }
}

// tg: code/datums/station_traits/neutral_traits.dm:279
public partial class obj_item_clothing_head_costume_party : obj_item_clothing_head_costume
{
    // tg: code/datums/station_traits/neutral_traits.dm:287  var/static/list/hat_colors
    public static ref DmList hat_colors => ref TgState.Current.obj_item_clothing_head_costume_party__hat_colors;

    // tg: code/datums/station_traits/neutral_traits.dm:296
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Waiting.NoSleep(set_greyscale(colors: Dm.list(pick(hat_colors))));
        return base.Initialize(mapload, __extra);
    }
}

public sealed partial class TgState
{
    // tg: code/datums/station_traits/neutral_traits.dm:287
    internal DmList obj_item_clothing_head_costume_party__hat_colors = Dm.list(
        COLOR_PRIDE_RED,
        COLOR_PRIDE_ORANGE,
        COLOR_PRIDE_YELLOW,
        COLOR_PRIDE_GREEN,
        COLOR_PRIDE_BLUE,
        COLOR_PRIDE_PURPLE);
}

// tg: code/datums/station_traits/neutral_traits.dm:306
public partial class datum_station_trait_scryers : datum_station_trait
{
    // tg: code/datums/station_traits/neutral_traits.dm:314
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        RegisterSignal(SSdcs, COMSIG_GLOB_JOB_AFTER_SPAWN, nameof(on_job_after_spawn));
    }

    // tg: code/datums/station_traits/neutral_traits.dm:318  SIGNAL_HANDLER
    public virtual void on_job_after_spawn(datum source = null, datum_job job = null, mob_living spawned = null, client player_client = null)
    {
        if (!ishuman(spawned))
            return;
        var humanspawned = (mob_living_carbon_human)spawned;
        // Put their silly little scarf or necktie somewhere else
        obj_item silly_little_scarf = humanspawned.wear_neck as obj_item;
        if (silly_little_scarf != null)
        {
            DmList backup_slots = Dm.list(LOCATION_LPOCKET, LOCATION_RPOCKET, LOCATION_BACKPACK);
            Waiting.NoSleep(humanspawned.temporarilyRemoveItemFromInventory(silly_little_scarf));
            Waiting.NoSleep(silly_little_scarf.forceMove(get_turf(humanspawned)));
            Waiting.NoSleep(humanspawned.equip_in_one_of_slots(silly_little_scarf, backup_slots, qdel_on_fail: FALSE));
        }

        var new_scryer = Dm.@new<obj_item_clothing_neck_link_scryer_loaded>(spawned);
        new_scryer.label = spawned.name;
        new_scryer.update_name();

        Waiting.NoSleep(spawned.equip_to_slot_or_del(new_scryer, ITEM_SLOT_NECK, initial: FALSE));
    }
}

// tg: code/datums/station_traits/neutral_traits.dm:337
public partial class datum_station_trait_wallets : datum_station_trait
{
    // tg: code/datums/station_traits/neutral_traits.dm:345
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        RegisterSignal(SSdcs, COMSIG_GLOB_JOB_AFTER_SPAWN, nameof(on_job_after_spawn));
    }

    // tg: code/datums/station_traits/neutral_traits.dm:349  SIGNAL_HANDLER
    public virtual void on_job_after_spawn(datum source = null, datum_job job = null, mob_living living_mob = null, mob M = null, object joined_late = null)
    {
        var id_card = living_mob.get_item_by_slot(ITEM_SLOT_ID) as obj_item_card_id_advanced;
        if (!istype(id_card, typeof(obj_item_card_id_advanced)))
            return;

        Waiting.NoSleep(living_mob.temporarilyRemoveItemFromInventory(id_card, force: TRUE));

        // "Doc, what's wrong with me?"
        // new(src): src is the trait, a datum, so BYOND makes the wallet nowhere (a null loc)
        var wallet = Dm.@new<obj_item_storage_wallet>((atom)null);
        // "You've got a wallet embedded in your chest."
        wallet.add_fingerprint(living_mob, ignoregloves: TRUE);

        Waiting.NoSleep(living_mob.equip_to_slot_if_possible(wallet, ITEM_SLOT_ID, initial: TRUE));

        Waiting.NoSleep(id_card.forceMove(wallet));

        object holochip_amount = id_card.registered_account.account_balance;
        Dm.@new<obj_item_holochip>(wallet, holochip_amount);
        id_card.registered_account.adjust_money(Dm.neg(holochip_amount), "System: Withdrawal");

        Dm.@new<obj_effect_spawner_random_entertainment_wallet_storage>(wallet);

        // Put our filthy fingerprints all over the contents
        foreach (obj_item item in Dm.each<obj_item>(wallet))
            item.add_fingerprint(living_mob, ignoregloves: TRUE);
    }
}

// tg: code/datums/station_traits/neutral_traits.dm:387
public partial class datum_station_trait_linked_closets : datum_station_trait
{
    // tg: code/datums/station_traits/neutral_traits.dm:395
    public override void on_round_start()
    {
        base.on_round_start();
        DmList roundstart_closets = GLOB.roundstart_station_closets.Copy();

        /**
         * The number of links to perform. the chance of a closet being linked are about 1 in 10
         * There are more than 220 roundstart closets on meta, so, about 22 closets will be affected on average.
         */
        float number_of_links = round(length(roundstart_closets) * (rand(400, 430) * 0.0001f), 1);
        for (float repetition = 1; repetition <= number_of_links; repetition++)
        {
            DmList targets = Dm.list();
            float how_many_count = rand(2, 3);
            for (float how_many = 1; how_many <= how_many_count; how_many++)
                Dm.append(ref targets, pick_n_take(roundstart_closets));
            GLOB.closet_teleport_controller.create_new_link(targets);
        }
    }
}

/// A trait that lets players choose whether they want pro-skub or anti-skub (or neither), and receive the appropriate equipment.
// tg: code/datums/station_traits/neutral_traits.dm:417
public partial class datum_station_trait_skub : datum_station_trait
{
    // tg: code/datums/station_traits/neutral_traits.dm:411
    private const string PRO_SKUB = "pro-skub";
    // tg: code/datums/station_traits/neutral_traits.dm:412
    private const string ANTI_SKUB = "anti-skub";
    // tg: code/datums/station_traits/neutral_traits.dm:413
    private const string SKUB_IDFC = "i don't frikkin' care";
    // tg: code/datums/station_traits/neutral_traits.dm:414  RANDOM_SKUB null: if you forgot to opt in/against/out, there's a 50/50 chance to be pro or anti
    private const string RANDOM_SKUB = null;

    // tg: code/datums/station_traits/neutral_traits.dm:426
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        RegisterSignal(SSdcs, COMSIG_GLOB_JOB_AFTER_SPAWN, nameof(on_job_after_spawn));
    }

    // tg: code/datums/station_traits/neutral_traits.dm:430
    public override object get_lobby_description()
    {
        return I18n.T("Are you pro-skub or anti-skub? Click to cycle through pro-skub, anti-skub, random and neutral.");
    }

    /// Let late-joiners jump on this gimmick too.
    // tg: code/datums/station_traits/neutral_traits.dm:434
    public override object can_display_lobby_button(client player = null)
    {
        return sign_up_button;
    }

    /// We don't destroy buttons on round start for those who are still in the lobby.
    // tg: code/datums/station_traits/neutral_traits.dm:438
    public override void on_round_start()
    {
    }

    // tg: code/datums/station_traits/neutral_traits.dm:441
    public override object get_lobby_icon_state(mob_dead_new_player player = null)
    {
        object stance = skubbers[player.ckey];
        if (Dm.eq(stance, PRO_SKUB))
            return "signup_on";
        if (Dm.eq(stance, ANTI_SKUB))
            return "signup";
        return "signup_neutral";
    }

    // tg: code/datums/station_traits/neutral_traits.dm:449
    public override object get_lobby_overlay_states(mob_dead_new_player player = null)
    {
        object stance = skubbers[player.ckey];
        if (Dm.eq(stance, PRO_SKUB))
            return Dm.list("pro_skub");
        if (Dm.eq(stance, ANTI_SKUB))
            return Dm.list("anti_skub");
        if (Dm.eq(stance, SKUB_IDFC))
            return Dm.list("neutral_skub");
        if (Dm.eq(stance, RANDOM_SKUB))
            return Dm.list("random_skub");
        return Dm.list();
    }

    // tg: code/datums/station_traits/neutral_traits.dm:461
    public override async DmTask<object> on_lobby_button_click(mob_dead_new_player player = null)
    {
        await DmTask.CompletedTask;
        object skub_stance = skubbers[player.ckey];
        if (Dm.eq(skub_stance, PRO_SKUB))
        {
            skubbers[player.ckey] = ANTI_SKUB;
            return I18n.T("anti-skub");
        }
        if (Dm.eq(skub_stance, ANTI_SKUB))
        {
            skubbers[player.ckey] = SKUB_IDFC;
            return I18n.T("don't care");
        }
        if (Dm.eq(skub_stance, SKUB_IDFC))
        {
            skubbers[player.ckey] = RANDOM_SKUB;
            return I18n.T("on the best side");
        }
        if (Dm.eq(skub_stance, RANDOM_SKUB))
        {
            skubbers[player.ckey] = PRO_SKUB;
            return I18n.T("pro-skub");
        }
        return null;
    }

    // tg: code/datums/station_traits/neutral_traits.dm:477  SIGNAL_HANDLER
    public virtual void on_job_after_spawn(datum source = null, datum_job job = null, mob_living spawned = null, client player_client = null)
    {
        object skub_stance = skubbers[player_client.ckey];
        if (Dm.eq(skub_stance, SKUB_IDFC))
            return;

        if ((Dm.eq(skub_stance, RANDOM_SKUB) && prob(50)) || Dm.eq(skub_stance, PRO_SKUB))
        {
            var boxie_pro = Dm.@new<obj_item_storage_box_stickers_skub>(spawned.loc);
            spawned.equip_to_storage(boxie_pro, ITEM_SLOT_BACK, indirect_action: TRUE);
            if (ishuman(spawned))
            {
                var shirt_pro = Dm.@new<obj_item_clothing_suit_costume_wellworn_shirt_skub>(spawned.loc);
                if (!Dm.truthy(Waiting.NoSleep(spawned.equip_to_slot_if_possible(shirt_pro, ITEM_SLOT_OCLOTHING, indirect_action: TRUE))))
                    Waiting.NoSleep(shirt_pro.forceMove(boxie_pro));
            }
            return;
        }

        var boxie = Dm.@new<obj_item_storage_box_stickers_anti_skub>(spawned.loc);
        spawned.equip_to_storage(boxie, ITEM_SLOT_BACK, indirect_action: TRUE);
        if (!ishuman(spawned))
            return;
        var shirt = Dm.@new<obj_item_clothing_suit_costume_wellworn_shirt_skub_anti>(spawned.loc);
        if (!Dm.truthy(Waiting.NoSleep(spawned.equip_to_slot_if_possible(shirt, ITEM_SLOT_OCLOTHING, indirect_action: TRUE))))
            Waiting.NoSleep(shirt.forceMove(boxie));
    }
}

/// Crew don't ever spawn as enemies of the station. Obsesseds, blob infection, space changelings etc can still happen though
// tg: code/datums/station_traits/neutral_traits.dm:507
public partial class datum_station_trait_background_checks : datum_station_trait
{
    // tg: code/datums/station_traits/neutral_traits.dm:518
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        RegisterSignal(SSdynamic, COMSIG_DYNAMIC_PRE_ROUNDSTART, nameof(modify_config));
    }

    // tg: code/datums/station_traits/neutral_traits.dm:522  SIGNAL_HANDLER
    public virtual void modify_config(datum source = null, DmList dynamic_config = null)
    {
        foreach (Type ruleset in Dm.each_cast<Type>(subtypesof(typeof(datum_dynamic_ruleset))))
        {
            if (Dm.truthy(Dm.bitand(Dm.initial(ruleset, (datum_dynamic_ruleset p) => p.ruleset_flags), RULESET_INVADER)))
                continue;
            object config_tag = Dm.initial(ruleset, (datum_dynamic_ruleset p) => p.config_tag);
            if (!Dm.truthy(dynamic_config[config_tag]))
                dynamic_config[config_tag] = Dm.list();
            (dynamic_config[config_tag] as DmList)["weight"] = 0; // NAMEOF(ruleset, weight)
        }
    }
}

// tg: code/datums/station_traits/neutral_traits.dm:531
public partial class datum_station_trait_pet_day : datum_station_trait
{
    // tg: code/datums/station_traits/neutral_traits.dm:538
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        RegisterSignal(SSdcs, COMSIG_GLOB_JOB_AFTER_SPAWN, nameof(on_job_after_spawn));
    }

    // tg: code/datums/station_traits/neutral_traits.dm:542
    public override object get_lobby_description()
    {
        return I18n.T("Want to bring your innocent pet to a giant metal deathtrap? Click here to customize it!");
    }

    // tg: code/datums/station_traits/neutral_traits.dm:545
    public override object get_lobby_overlay_states(mob_dead_new_player player = null)
    {
        return Dm.list("select_pet");
    }

    // tg: code/datums/station_traits/neutral_traits.dm:548
    public override object can_display_lobby_button(client player = null)
    {
        return sign_up_button;
    }

    // tg: code/datums/station_traits/neutral_traits.dm:551
    public override void on_round_start()
    {
    }

    // tg: code/datums/station_traits/neutral_traits.dm:554
    public override async DmTask<object> on_lobby_button_click(mob_dead_new_player player = null)
    {
        await DmTask.CompletedTask;
        client player_client = player.client;
        if (player_client == null)
            return null;
        // GLOB.customized_pets[REF(player_client)] || new /datum/pet_customization(player_client), INVOKE_ASYNC(customization,
        // TYPE_PROC_REF(/datum, ui_interact), player): the pet customization and its window are not ported (inert)
        Dm.Deferred("code/datums/station_traits/neutral_traits.dm:558", "pet_day on_lobby_button_click: the pet customization window (not ported)");
        return null;
    }

    // tg: code/datums/station_traits/neutral_traits.dm:563  SIGNAL_HANDLER
    public virtual void on_job_after_spawn(datum source = null, datum_job job = null, mob_living spawned = null, client player_client = null)
    {
        // GLOB.customized_pets[REF(player_client)] and INVOKE_ASYNC(customization, create_pet, spawned, player_client): the pet
        // customization is not ported (inert), so nobody has customized a pet
        Dm.Deferred("code/datums/station_traits/neutral_traits.dm:566", "pet_day on_job_after_spawn: the customized pet (the pet customization is not ported)");
    }
}
