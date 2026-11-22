// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/pets/dog/corgi.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the petrify and Nar'Sie's own branch (rows) (gaps package C2; the deadchat control since gaps E1a).

//CORGIS!

// tg: code/modules/mob/living/basic/pets/dog/corgi.dm:4
public partial class mob_living_basic_pet_dog_corgi : mob_living_basic_pet_dog
{
    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:33
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        Waiting.NoSleep(AddElement(typeof(datum_element_strippable), Dm.truthy(length(strippable_inventory_slots)) ? create_strippable_list(strippable_inventory_slots) : GLOB.strippable_corgi_items));
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_CORGI, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 5f));
        RegisterSignal(this, COMSIG_MOB_RETRIEVE_ACCESS, nameof(retrieve_access));
        RegisterSignals(this, Dm.list(COMSIG_BASICMOB_LOOK_ALIVE, COMSIG_BASICMOB_LOOK_DEAD), nameof(on_appearance_change));
        if (Dm.truthy(can_breed))
            add_breeding_component();
        return dot;
    }

    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:43
    public override object Destroy(float? force = null)
    {
        qdel(inventory_head);
        inventory_head = null;
        qdel(inventory_back);
        inventory_back = null;
        qdel(access_card);
        access_card = null;
        UnregisterSignal(this, Dm.list(COMSIG_BASICMOB_LOOK_ALIVE, COMSIG_BASICMOB_LOOK_DEAD));
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:50
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        bool dropped_something = false;
        if (gone == inventory_head)
        {
            dropped_something = true;
            inventory_head = null;
        }
        if (gone == inventory_back)
        {
            dropped_something = true;
            inventory_back = null;
        }
        if (dropped_something)
        {
            update_corgi_fluff();
            update_appearance(UPDATE_OVERLAYS);
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:63
    public override async DmTask gib(float? drop_bitflags = null)
    {
        await undress_dog();
        if (access_card != null)
        {
            await access_card.forceMove(drop_location() as atom);
            access_card = null;
        }
        await base.gib(drop_bitflags);
    }

    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:70
    public virtual void add_breeding_component()
    {
        DmList baby_paths = Dm.list(Dm.kv(typeof(mob_living_basic_pet_dog_corgi_puppy), 95), Dm.kv(typeof(mob_living_basic_pet_dog_corgi_puppy_void), 5));
        Waiting.NoSleep(AddComponent(typeof(datum_component_breed), typecacheof(Dm.list(typeof(mob_living_basic_pet_dog_corgi))), null, baby_paths));
    }

    /// Removes the hat and shirt (but not ID) of this corgi
    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:83
    public virtual async DmTask undress_dog()
    {
        if (inventory_head != null)
            await inventory_head.forceMove(drop_location() as atom);
        if (inventory_back != null)
            await inventory_back.forceMove(drop_location() as atom);
    }

    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:87
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (access_card != null)
            Dm.append(ref dot, Dm.text("There appears to be \\a [] pinned to [].", access_card, p_them()));
        return dot;
    }

    /**
     * Corgis get full protection from their equipped fashion items if attacked in a way that passes def_zone,
     * which usually means any direct attack like melee or gunshot. Anything abstract like a bomb or acid or something
     * will instead give half the armor value.
     */
    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:97
    public override object get_worn_armor_value(object def_zone = null, object damage_type = null)
    {
        float armorval = 0;

        if (Dm.truthy(def_zone))
        {
            if (Dm.eq(def_zone, BODY_ZONE_HEAD))
            {
                if (inventory_head != null)
                    armorval = Dm.num(inventory_head.get_armor_rating(damage_type));
            }
            else
            {
                if (inventory_back != null)
                    armorval = Dm.num(inventory_back.get_armor_rating(damage_type));
            }
            return armorval;
        }
        if (inventory_head != null)
            armorval += Dm.num(inventory_head.get_armor_rating(damage_type));
        if (inventory_back != null)
            armorval += Dm.num(inventory_back.get_armor_rating(damage_type));
        return armorval * 0.5f;
    }

    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:115
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_razor)
            return await base.item_interaction(user, tool, modifiers, __extra);

        if (Dm.truthy(shaved))
        {
            to_chat(user, span_warning(Dm.text("You can't shave this corgi, [] [] already been shaved!", p_they(), p_have())));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(can_be_shaved))
        {
            to_chat(user, span_warning(Dm.text("You can't shave this corgi, [] []n't have a fur coat!", p_they(), p_do())));
            return ITEM_INTERACT_BLOCKING;
        }

        user.visible_message(
            span_notice(Dm.text("[] starts to shave [] using \\the [].", user, this, tool)),
            span_notice(Dm.text("You start to shave [] using \\the []...", this, tool)),
            span_hear("You hear electric buzzing."));

        if (!Dm.truthy(await do_after(user, 5 * SECONDS, target: this)))
            return ITEM_INTERACT_BLOCKING;

        user.visible_message(span_notice(Dm.text("[] shaves []'s hair using \\the [].", user, this, tool)));
        playsound(get_turf(this), Dm.resource("sound/items/hair-clippers.ogg"), 20, TRUE);
        shaved = TRUE;
        icon_living = Dm.text("[]_shaved", icon_living);
        icon_dead = Dm.text("[]_shaved_dead", icon_living);
        if (!IS_UNCONSCIOUS_OR_CRIT(this))
            icon_state = icon_living;
        else
            icon_state = icon_dead;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:147
    public override void update_dog_speech(DmList speech_data = null)
    {
        base.update_dog_speech(speech_data);
        if (inventory_head?.dog_fashion != null)
        {
            datum_dog_fashion equipped_head_fashion_item = Dm.@new(inventory_head.dog_fashion, this) as datum_dog_fashion;
            equipped_head_fashion_item.apply_to_speech(speech_data);
        }

        if (inventory_back?.dog_fashion != null)
        {
            datum_dog_fashion equipped_back_fashion_item = Dm.@new(inventory_back.dog_fashion, this) as datum_dog_fashion;
            equipped_back_fashion_item.apply_to_speech(speech_data);
        }
    }

    /// Applies corgi fashion to the BT blackboard speech data by routing through the planning stub.
    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:158
    public override void update_dog_speak_blackboard(datum_ai_controller controller = null)
    {
        DmList speech_data = Dm.list();
        update_dog_speech(speech_data);
        if (!Dm.truthy(speech_data[BB_SPEAK_CHANCE]))
            speech_data[BB_SPEAK_CHANCE] = 1;
        controller.override_blackboard_key(BB_BASIC_MOB_SPEAK_LINES, speech_data);
    }

    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:165
    public override async DmTask<object> deadchat_plays(float? mode = null, float? cooldown = null)
    {
        mode ??= ANARCHY_MODE;
        cooldown ??= 12 * SECONDS;
        object dot = await AddComponent(typeof(datum_component_deadchat_control_cardinal_movement), mode, Dm.list(
            Dm.kv("speak", Dm.@new<datum_callback>(this, nameof(bork))),
            Dm.kv("wear_hat", Dm.@new<datum_callback>(this, nameof(find_new_hat))),
            Dm.kv("drop_hat", Dm.@new<datum_callback>(this, nameof(drop_hat))),
            Dm.kv("spin", Dm.@new<datum_callback>(this, nameof(emote), "spin"))), cooldown, Dm.@new<datum_callback>(this, nameof(stop_deadchat_plays)));

        if (Dm.eq(dot, COMPONENT_INCOMPATIBLE))
            return dot;

        // Stop all automated behavior.
        QDEL_NULL(ref ai_controller);
        return dot;
    }

    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:178
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (inventory_head != null)
        {
            image head_icon;
            datum_dog_fashion equipped_head_fashion_item = Dm.@new(inventory_head.dog_fashion, this) as datum_dog_fashion;

            if (!Dm.truthy(equipped_head_fashion_item.obj_icon_state))
                equipped_head_fashion_item.obj_icon_state = inventory_head.icon_state;
            if (!Dm.truthy(equipped_head_fashion_item.obj_alpha))
                equipped_head_fashion_item.obj_alpha = inventory_head.alpha;
            if (!Dm.truthy(equipped_head_fashion_item.obj_color))
                equipped_head_fashion_item.obj_color = inventory_head.color;

            if (IS_DEAD_OR_FAKING(this))
            {
                head_icon = equipped_head_fashion_item.get_overlay(dir: EAST);
                head_icon.pixel_z = -8;
                head_icon.transform = ((matrix)head_icon.transform).Turn(180);
            }
            else
                head_icon = equipped_head_fashion_item.get_overlay();

            Dm.append(ref dot, head_icon);
        }

        if (inventory_back != null)
        {
            image back_icon;
            datum_dog_fashion equipped_back_fashion_item = Dm.@new(inventory_back.dog_fashion, this) as datum_dog_fashion;

            if (!Dm.truthy(equipped_back_fashion_item.obj_icon_state))
                equipped_back_fashion_item.obj_icon_state = inventory_back.icon_state;
            if (!Dm.truthy(equipped_back_fashion_item.obj_alpha))
                equipped_back_fashion_item.obj_alpha = inventory_back.alpha;
            if (!Dm.truthy(equipped_back_fashion_item.obj_color))
                equipped_back_fashion_item.obj_color = inventory_back.color;

            if (IS_DEAD_OR_FAKING(this))
            {
                back_icon = equipped_back_fashion_item.get_overlay(dir: EAST);
                back_icon.pixel_z = -11;
                back_icon.transform = ((matrix)back_icon.transform).Turn(180);
            }
            else
                back_icon = equipped_back_fashion_item.get_overlay();

            Dm.append(ref dot, back_icon);
        }
        return dot;
    }

    //Corgis are supposed to be simpler, so only a select few objects can actually be put
    //to be compatible with them. The objects are below.
    //Many  hats added, Some will probably be removed, just want to see which ones are popular.
    // > some will probably be removed

    /**
     * Places an item on the corgi's head, handling updating the corgi's appearance and the item's dog fashion modifiers
     * to the name, description, speech etc. Doesn't need the user to complete, and is also used in station traits/events/persistence reading.
    */
    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:229
    public virtual async DmTask<object> place_on_head(obj_item item_to_add = null, mob_living user = null)
    {
        if (inventory_head != null)
        {
            if (user != null)
                balloon_alert(user, "already wearing a hat!");
            return FALSE;
        }

        if (item_to_add == null)
        {
            if (user != null)
            {
                user.visible_message(span_notice(Dm.text("[] pets [].", user, this)), span_notice(Dm.text("You rest your hand on []'s head for a moment.", this)));
                if (Dm.truthy(Dm.bitand(flags_1, HOLOGRAM_1)))
                    return null;
                await user.add_mood_event(REF(this), typeof(datum_mood_event_pet_animal), this);
            }
            return FALSE;
        }

        if (user != null && !Dm.truthy(await user.temporarilyRemoveItemFromInventory(item_to_add)))
        {
            to_chat(user, span_warning(Dm.text("\\The [] is stuck to your hand, you cannot put it on []'s head!", item_to_add, this)));
            return FALSE;
        }

        //Various hats and items (worn on his head) change Ian's behaviour. His attributes are reset when a hat is removed.
        if (!ispath(item_to_add.dog_fashion, typeof(datum_dog_fashion_head)))
        {
            to_chat(user, span_warning(Dm.text("You set [] on []'s head, but it falls off!", item_to_add, this)));
            await item_to_add.forceMove(drop_location() as atom);
            if (prob(25))
                await step_rand(item_to_add);
            Waiting.Detach(dance_rotate(this, set_original_dir: TRUE));
            return FALSE;
        }

        if (user != null)
        {
            if (IS_DEAD_OR_FAKING(this))
                to_chat(user, span_notice(Dm.text("There is merely a dull, lifeless look in []'s eyes as you put \\the [] on [].", real_name, item_to_add, p_them())));
            else
                user.visible_message(
                    span_notice(Dm.text("[] puts [] on []'s head. [] looks at [] and barks once.", user, item_to_add, real_name, this, user)),
                    span_notice(Dm.text("You put [] on []'s head. [] gives you a peculiar look, then wags [] tail once and barks.", item_to_add, real_name, this, p_their())),
                    span_hear("You hear a friendly-sounding bark."));
        }

        await item_to_add.forceMove(this);
        inventory_head = item_to_add;
        update_corgi_fluff();
        update_appearance(UPDATE_OVERLAYS);
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:272
    public virtual void update_corgi_fluff()
    {
        // First, change back to defaults
        name = real_name as string;
        desc = initial(this, "desc") as string;
        // BYOND/DM doesn't support the use of initial on lists.
        speak_emote = Dm.list("barks", "woofs");
        desc = initial(this, "desc") as string;
        Waiting.NoSleep(set_light(0));

        if (inventory_head?.dog_fashion != null)
        {
            datum_dog_fashion equipped_head_fashion_item = Dm.@new(inventory_head.dog_fashion, this) as datum_dog_fashion;
            equipped_head_fashion_item.apply(this);
        }

        if (inventory_back?.dog_fashion != null)
        {
            datum_dog_fashion equipped_back_fashion_item = Dm.@new(inventory_back.dog_fashion, this) as datum_dog_fashion;
            equipped_back_fashion_item.apply(this);
        }
    }

    ///Handler for COMSIG_MOB_RETRIEVE_ACCESS
    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:290
    [SignalHandler]
    public virtual void retrieve_access(mob accessor = null, DmList player_access = null)
    {
        if (access_card != null)
            Dm.append(ref player_access, access_card.GetAccess());
    }

    ///Handles updating any existing overlays for the corgi (such as fashion items) when it changes how it appears, as in, dead or alive.
    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:296
    [SignalHandler]
    public virtual void on_appearance_change()
    {
        update_appearance(UPDATE_OVERLAYS);
    }

    ///Deadchat plays bark.
    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:301
    public virtual void bork()
    {
        object emote = pick("barks!", "woofs!", "yaps.", "pants.");
        manual_emote(emote);
    }

    ///Deadchat plays command that picks a new hat for Ian.
    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:306
    public virtual async DmTask find_new_hat()
    {
        if (!isturf(loc))
            return;
        DmList possible_headwear = Dm.list();
        foreach (obj_item item in Dm.each<obj_item>(loc))
        {
            if (ispath(item.dog_fashion, typeof(datum_dog_fashion_head)))
                Dm.append(ref possible_headwear, item);
        }
        if (!Dm.truthy(length(possible_headwear)))
        {
            foreach (obj_item item in Dm.each<obj_item>(orange(1, this)))
            {
                if (ispath(item.dog_fashion, typeof(datum_dog_fashion_head)) && Dm.truthy(await item.IsReachableBy(this)))
                    Dm.append(ref possible_headwear, item);
            }
        }
        if (!Dm.truthy(length(possible_headwear)))
            return;
        if (inventory_head != null)
        {
            await inventory_head.forceMove(drop_location() as atom);
            inventory_head = null;
        }
        await place_on_head(pick(possible_headwear) as obj_item);
        visible_message(span_notice(Dm.text("[] puts [] on [] own head, somehow.", this, inventory_head, p_their())));
    }

    ///Deadchat plays command that drops the current hat off Ian.
    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:326
    public virtual async DmTask drop_hat()
    {
        if (inventory_head == null)
            return;
        visible_message(span_notice(Dm.text("[] vigorously shakes [] head, dropping [] to the ground.", this, p_their(), inventory_head)));
        await inventory_head.forceMove(drop_location() as atom);
        inventory_head = null;
        update_corgi_fluff();
        update_appearance(UPDATE_OVERLAYS);
    }

    ///Turn AI back on.
    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:336
    public virtual void stop_deadchat_plays()
    {
        Type controller_type = initial(this, "ai_controller") as Type;
        ai_controller = Dm.@new(controller_type, this);
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_DOG_IS_SLOW, is_slow);
    }
}

//SUBTYPES!

// tg: code/modules/mob/living/basic/pets/dog/corgi.dm:344
public partial class mob_living_basic_pet_dog_corgi_exoticcorgi : mob_living_basic_pet_dog_corgi
{
    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:352
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        object newcolor = Dm.rgb(rand(0, 255), rand(0, 255), rand(0, 255));
        Waiting.NoSleep(add_atom_colour(newcolor, FIXED_COLOUR_PRIORITY));
        return dot;
    }
}

//IAN! SQUEEEEEEEEE~
// tg: code/modules/mob/living/basic/pets/dog/corgi.dm:359
public partial class mob_living_basic_pet_dog_corgi_ian : mob_living_basic_pet_dog_corgi
{
    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:382
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // REGISTER_REQUIRED_MAP_ITEM(1, 1): the unit tests' map item check (UNIT_TESTS)

        //parent call must happen first to ensure IAN
        //is not in nullspace when child puppies spawn
        Read_Memory();
        if (Dm.num(age) == 0)
        {
            object target = get_turf(loc);
            if (Dm.truthy(target))
            {
                Dm.@new<mob_living_basic_pet_dog_corgi_puppy_ian>(target);
                Write_Memory(FALSE);
                return INITIALIZE_HINT_QDEL;
            }
        }
        else if (Dm.num(age) == Dm.num(record_age))
        {
            icon_state = "old_corgi";
            icon_living = "old_corgi";
            held_state = "old_corgi";
            icon_dead = "old_corgi_dead";
            desc = Dm.text("At a ripe old age of [], Ian's not as spry as he used to be, but he'll always be the HoP's beloved corgi.", record_age); //RIP
            (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_DOG_IS_SLOW, TRUE);
            is_slow = TRUE;
            speed = 2;
        }

        //setup roundend check for Ian's whereabouts
        i_will_survive = Dm.@new<datum_callback>(this, nameof(check_ian_survival));
        SSticker.OnRoundend(i_will_survive);
        return dot;
    }

    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:410
    public override object Destroy(float? force = null)
    {
        LAZYREMOVE(ref SSticker.round_end_events, i_will_survive); //cleanup the survival callback
        i_will_survive = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:415
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        if (!Dm.truthy(memory_saved))
            Write_Memory(TRUE);
        return await base.death(gibbed, __extra);
    }

    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:420
    public override async DmTask<object> revive(float? full_heal_flags = null, float? excess_healing = null, float? force_grab_ghost = null)
    {
        object dot = await base.revive(full_heal_flags, excess_healing, force_grab_ghost);
        if (!Dm.truthy(dot))
            return dot;
        if (inventory_head is not obj_item_clothing_glasses_eyepatch)
        {
            if (inventory_head != null)
                await inventory_head.forceMove(drop_location() as atom);
        }
        await place_on_head(Dm.@new<obj_item_clothing_glasses_eyepatch_medical>());
        return dot;
    }

    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:428
    public override async DmTask<object> narsie_act(params object[] __extra)
    {
        playsound(this, Dm.resource("sound/effects/magic/demon_dies.ogg"), 75, TRUE);
        mob_living_basic_pet_dog_corgi_narsie narsIan = Dm.@new<mob_living_basic_pet_dog_corgi_narsie>(loc);
        await narsIan.setDir(dir);
        await gib();
        return null;
    }

    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:435
    public override object Write_Memory(object dead = null, object gibbed = null)
    {
        object dot = base.Write_Memory(dead, gibbed);
        if (!Dm.truthy(dot))
            return dot;
        memory_saved = TRUE;
        DmFileRef json_file = Dm.file("data/npc_saves/Ian.json");
        DmList file_data = Dm.list();
        if (!Dm.truthy(dead))
        {
            file_data["age"] = Dm.num(age) + 1;
            if ((Dm.num(age) + 1) > Dm.num(record_age))
                file_data["record_age"] = Dm.num(record_age) + 1;
            else
                file_data["record_age"] = record_age;
            if (inventory_head != null)
                file_data["saved_head"] = inventory_head.type;
            else
                file_data["saved_head"] = null;
        }
        else
        {
            file_data["age"] = 0;
            file_data["record_age"] = record_age;
            file_data["saved_head"] = null;
        }
        Dm.fdel(json_file);
        Dm.output_to(json_file, Dm.json_encode(file_data));
        return dot;
    }

    ///Reads the database's persistence json file and applies age, record_age and saved_head to Ian.
    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:460
    public virtual void Read_Memory()
    {
        // the legacy savefile branch (data/npc_saves/Ian.sav, tg's old format) has no port: no such file exists here
        DmFileRef json_file = Dm.file("data/npc_saves/Ian.json");
        if (!Dm.truthy(Dm.fexists(json_file)))
            return;
        DmList json = Dm.json_decode(Dm.file2text(json_file)) as DmList;
        age = json["age"] == null ? null : Dm.num(json["age"]);
        record_age = json["record_age"] == null ? null : Dm.num(json["record_age"]);
        saved_head = json["saved_head"] is string saved_path ? Dm.text2path(saved_path) : json["saved_head"];
        age ??= 0;
        record_age ??= 1;
        if (Dm.truthy(saved_head))
            Waiting.NoSleep(place_on_head(Dm.@new((Type)saved_head) as obj_item));
    }

    ///Checks whether Ian has survived the round or not
    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:483
    public virtual void check_ian_survival()
    {
        if (!IS_UNCONSCIOUS_OR_CRIT(this) && !Dm.truthy(memory_saved))
            Write_Memory(FALSE);
    }
}

//NARS-IAN! SQ-Q-QooEglor-r'EEn-nl-luEEEf-f-fth-h
// tg: code/modules/mob/living/basic/pets/dog/corgi.dm:489
public partial class mob_living_basic_pet_dog_corgi_narsie : mob_living_basic_pet_dog_corgi
{
    /// Mobs we will consume in the name of Nar'Sie
    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:502
    private static DmList edible_types => TgState.Current.mob_living_basic_pet_dog_corgi_narsie__edible_types ??= Dm.list(typeof(mob_living_basic_pet));

    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:506
    private static DmList connections => TgState.Current.mob_living_basic_pet_dog_corgi_narsie__connections ??= Dm.list(Dm.kv(COMSIG_ATOM_ENTERED, nameof(on_prey_approached)));

    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:504
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_connect_range), this, connections, 1f, FALSE));
        return dot;
    }

    /// Attempt to eat a pet we get near
    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:510
    [SignalHandler]
    public virtual void on_prey_approached(atom_movable dog = null, atom_movable prey = null)
    {
        if (!Dm.truthy(is_type_in_list(prey, edible_types)) || istype(prey, type))
            return;
        visible_message(span_warning(Dm.text("Dark magic resonating from [] devours []!", this, prey)), "<span class='cult big bold'>DELICIOUS SOULS</span>");
        playsound(this, Dm.resource("sound/effects/magic/demon_attack1.ogg"), 75, TRUE);
        Dm.@new<obj_effect_temp_visual_cult_sac>(get_turf(prey));
        Waiting.Detach(narsie_act());
        if (isliving(prey))
        {
            mob_living living_sacrifice = prey as mob_living;
            Waiting.Detach(living_sacrifice.gib(DROP_ALL_REMAINS));
        }
        else
            qdel(prey);
    }

    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:526
    public override void update_corgi_fluff()
    {
        base.update_corgi_fluff();
        speak_emote = Dm.list("growls", "barks ominously");
    }

    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:530
    public override void update_dog_speech(DmList speech_data = null)
    {
        speech_data[BB_EMOTE_SAY] = string_list(Dm.list("Tari'karat-pasnar!", "IA! IA!", "BRRUUURGHGHRHR"));
        speech_data[BB_EMOTE_HEAR] = string_list(Dm.list("barks echoingly!", "woofs hauntingly!", "yaps in an eldritch manner.", "mutters something unspeakable."));
        speech_data[BB_EMOTE_SEE] = string_list(Dm.list("communes with the unnameable.", "ponders devouring some souls.", "shakes."));
    }

    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:535
    public override async DmTask<object> narsie_act(params object[] __extra)
    {
        if (stat == DEAD) //Nar'Sie loves her doggy
        {
            visible_message(span_warning(Dm.text("[] arises again, revived by the dark magicks!", this)), span_cult_large("RISE"));
            await revive(ADMIN_HEAL_ALL); //also means that a dead Nars-Ian can consume a pet and revive
        }
        await adjust_brute_loss(-Dm.num(maxHealth));
        return null;
    }
}

// tg: code/modules/mob/living/basic/pets/dog/corgi.dm:586
public partial class mob_living_basic_pet_dog_corgi_puppy_void : mob_living_basic_pet_dog_corgi_puppy
{
    // tg: code/modules/mob/living/basic/pets/dog/corgi.dm:599
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        //void puppies can spacewalk and can harass people from within a container because they're void puppies
        add_traits(Dm.list(TRAIT_AI_BAGATTACK, TRAIT_SPACEWALK), INNATE_TRAIT);
        return dot;
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_pet_dog_corgi_narsie__edible_types;
    internal DmList mob_living_basic_pet_dog_corgi_narsie__connections;
}
