// Translated from tgstation (snapshot 1d99bd7f): code/datums/outfit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the vars, pre_equip, post_equip, equip and apply_fingerprints translated (M2 F1: a joining player's
// outfit; apply_fingerprints' body in M6 slice 1). Of equip's slots only the uniform is equipped in M2; the other slots, the ID trim, the underwear, the
// accessory, the hands, the pockets, the backpack and belt contents, internals, implants and skillchips are deferred leaves
// or not translated (inventory and storage M4a, ID cards M4, internals M4b). The chameleon info, the
// preloading list, copying an outfit from a mob and the JSON save and load (admin tooling) are translated with them.

/**
 * # Outfit datums
 *
 * This is a clean system of applying outfits to mobs, if you need to equip someone in a uniform
 * this is the way to do it cleanly and properly.
 *
 * You can also specify an outfit datum on a job to have it auto equipped to the mob on join
 *
 * /mob/living/carbon/human/proc/equipOutfit(outfit) is the mob level proc to equip an outfit
 * and you pass it the relevant datum outfit
 *
 * outfits can also be saved as json blobs downloadable by a client and then can be uploaded
 * by that user to recreate the outfit, this is used by admins to allow for custom event outfits
 * that can be restored at a later date
 */
// tg: code/datums/outfit.dm:16
[DmPath("/datum/outfit")]
public partial class datum_outfit : datum
{
    ///Name of the outfit (shows up in the equip admin verb)
    // tg: code/datums/outfit.dm:18
    public string name = "Naked";

    /// Type path of item to go in the idcard slot
    // tg: code/datums/outfit.dm:21
    public Type id = null;

    /// Type path of ID card trim associated with this outfit.
    // tg: code/datums/outfit.dm:24
    public Type id_trim = null;

    /// Type path of item to go in uniform slot
    // tg: code/datums/outfit.dm:27
    public Type uniform = null;

    /// Type path of item to go in suit slot
    // tg: code/datums/outfit.dm:30
    public Type suit = null;

    /**
      * Type path of item to go in suit storage slot
      *
      * (make sure it's valid for that suit)
      */
    // tg: code/datums/outfit.dm:37
    public Type suit_store = null;

    /// Type path of item to go in back slot
    // tg: code/datums/outfit.dm:40
    public Type back = null;

    /**
      * list of items that should go in the backpack of the user
      *
      * Format of this list should be: list(path=count,otherpath=count)
      */
    // tg: code/datums/outfit.dm:47
    public DmList backpack_contents = null;

    /// Type path of item to go in belt slot
    // tg: code/datums/outfit.dm:50
    public Type belt = null;

    /**
      * list of items that should go in the belt of the user
      *
      * Format of this list should be: list(path=count,otherpath=count)
      */
    // tg: code/datums/outfit.dm:57
    public DmList belt_contents = null;

    /// Type path of item to go in ears slot
    // tg: code/datums/outfit.dm:60
    public Type ears = null;

    /// Type path of item to go in the glasses slot
    // tg: code/datums/outfit.dm:63
    public Type glasses = null;

    /// Type path of item to go in gloves slot
    // tg: code/datums/outfit.dm:66
    public Type gloves = null;

    /// Type path of item to go in head slot
    // tg: code/datums/outfit.dm:69
    public Type head = null;

    /// Type path of item to go in mask slot
    // tg: code/datums/outfit.dm:72
    public Type mask = null;

    /// Type path of item to go in neck slot
    // tg: code/datums/outfit.dm:75
    public Type neck = null;

    /// Type path of item to go in shoes slot
    // tg: code/datums/outfit.dm:78
    public Type shoes = null;

    /// Type path of item for left pocket slot
    // tg: code/datums/outfit.dm:81
    public Type l_pocket = null;

    /// Type path of item for right pocket slot
    // tg: code/datums/outfit.dm:84
    public Type r_pocket = null;

    ///Type path of item to go in the right hand
    // tg: code/datums/outfit.dm:87
    public Type l_hand = null;

    //Type path of item to go in left hand
    // tg: code/datums/outfit.dm:90
    public Type r_hand = null;

    /// Any clothing accessory item
    // tg: code/datums/outfit.dm:93
    public Type accessory = null;

    /// Internals box. Will be inserted at the start of backpack_contents
    // tg: code/datums/outfit.dm:96
    public Type box;

    /**
      * extra types for chameleon outfit changes, mostly guns
      *
      * Valid values are a single typepath or list of typepaths
      *
      * These are all added and returns in the list for get_chamelon_diguise_info proc
      */
    // tg: code/datums/outfit.dm:105
    public DmList chameleon_extras;

    /**
      * Any implants the mob should start implanted with
      *
      * Format of this list is (typepath, typepath, typepath)
      */
    // tg: code/datums/outfit.dm:112
    public DmList implants = null;

    ///ID of the slot containing a gas tank
    // tg: code/datums/outfit.dm:115
    public object internals_slot = null;

    /**
      * Any skillchips the mob should have in their brain.
      *
      * Format of this list is (typepath, typepath, typepath)
      */
    // tg: code/datums/outfit.dm:122
    public DmList skillchips = null;

    ///Should we preload some of this job's items?
    // tg: code/datums/outfit.dm:125
    public float preload = FALSE;

    /// Any undershirt. While on humans it is a string, here we use paths to stay consistent with the rest of the equips.
    // tg: code/datums/outfit.dm:128
    public Type undershirt = null;
    // tg: code/datums/outfit.dm:129
    public Type underwear = null;
    // tg: code/datums/outfit.dm:130
    public Type socks = null;

    /**
     * Called at the start of the equip proc
     *
     * Override to change the value of the slots depending on client prefs, species and
     * other such sources of change
     *
     * Extra Arguments
     * * visuals_only true if this is only for display (in the character setup screen)
     *
     * If visuals_only is true, you can omit any work that doesn't visually appear on the character sprite
     */
    // tg: code/datums/outfit.dm:143
    public virtual async DmTask pre_equip(mob_living_carbon_human user = null, float? visuals_only = null)
    {
        visuals_only ??= FALSE;
        //to be overridden for customization depending on client prefs,species etc
        return;
    }

    /**
     * Called after the equip proc has finished
     *
     * All items are on the mob at this point, use this proc to toggle internals
     * fiddle with id bindings and accesses etc
     *
     * Extra Arguments
     * * visuals_only true if this is only for display (in the character setup screen)
     *
     * If visuals_only is true, you can omit any work that doesn't visually appear on the character sprite
     */
    // tg: code/datums/outfit.dm:158
    public virtual async DmTask post_equip(mob_living_carbon_human user = null, float? visuals_only = null)
    {
        visuals_only ??= FALSE;
        //to be overridden for toggling internals, id binding, access etc
        return;
    }

    // EQUIP_OUTFIT_ITEM(item_path, slot_name) (:162, file-local, #undef at :311) is expanded inline at each use (rules 11.1
    // (d) and 11.4: it reads the caller's user and visuals_only).

    /**
     * Equips all defined types and paths to the mob passed in
     *
     * Extra Arguments
     * * visuals_only true if this is only for display (in the character setup screen)
     *
     * If visuals_only is true, you can omit any work that doesn't visually appear on the character sprite
     */
    // tg: code/datums/outfit.dm:178
    public virtual async DmTask<object> equip(mob_living_carbon_human user = null, float? visuals_only = null)
    {
        visuals_only ??= FALSE;
        await pre_equip(user, visuals_only);

        //Start with uniform,suit,backpack for additional slots
        if (Dm.truthy(uniform))
            await EQUIP_OUTFIT_ITEM(user, visuals_only, uniform, ITEM_SLOT_ICLOTHING);
        if (Dm.truthy(suit))
            await EQUIP_OUTFIT_ITEM(user, visuals_only, suit, ITEM_SLOT_OCLOTHING);
        if (Dm.truthy(belt))
            await EQUIP_OUTFIT_ITEM(user, visuals_only, belt, ITEM_SLOT_BELT);
        if (Dm.truthy(gloves))
            await EQUIP_OUTFIT_ITEM(user, visuals_only, gloves, ITEM_SLOT_GLOVES);
        if (Dm.truthy(shoes))
            await EQUIP_OUTFIT_ITEM(user, visuals_only, shoes, ITEM_SLOT_FEET);
        if (Dm.truthy(head))
            await EQUIP_OUTFIT_ITEM(user, visuals_only, head, ITEM_SLOT_HEAD);
        if (Dm.truthy(mask))
            await EQUIP_OUTFIT_ITEM(user, visuals_only, mask, ITEM_SLOT_MASK);
        if (Dm.truthy(neck))
            await EQUIP_OUTFIT_ITEM(user, visuals_only, neck, ITEM_SLOT_NECK);
        if (Dm.truthy(ears))
            await EQUIP_OUTFIT_ITEM(user, visuals_only, ears, ITEM_SLOT_EARS);
        if (Dm.truthy(glasses))
            await EQUIP_OUTFIT_ITEM(user, visuals_only, glasses, ITEM_SLOT_EYES);
        if (Dm.truthy(back))
            await EQUIP_OUTFIT_ITEM(user, visuals_only, back, ITEM_SLOT_BACK);
        if (Dm.truthy(id))
            await EQUIP_OUTFIT_ITEM(user, visuals_only, id, ITEM_SLOT_ID);
        if (!Dm.truthy(visuals_only) && Dm.truthy(id_trim) && Dm.truthy(user.wear_id))
        {
            obj_item_card_id id_card = user.wear_id as obj_item_card_id;
            if (id_card == null) //If an ID wasn't found in their ID slot, it's probably something holding their ID like a wallet or PDA
                id_card = Dm.locate<obj_item_card_id>(user.wear_id);

            if (id_card != null) //Make sure that we actually found an ID to modify, otherwise this runtimes and cancels equipping the outfit
            {
                id_card.registered_age = user.age;
                if (Dm.truthy(id_trim))
                {
                    if (!Dm.truthy(await SSid_access.apply_trim_to_card(id_card, id_trim)))
                        warning(Dm.text("[] in [] at line [] src: [] usr: [].", Dm.text("Unable to apply trim [] to [] in outfit [].", id_trim, id_card, name), "code/datums/outfit.dm", 214, this, usr)); // WARNING(MSG), inline
                    user.update_ID_card();
                }
            }
        }

        if (Dm.truthy(suit_store))
            await EQUIP_OUTFIT_ITEM(user, visuals_only, suit_store, ITEM_SLOT_SUITSTORE);

        if (Dm.truthy(undershirt) || Dm.truthy(underwear) || Dm.truthy(socks))
        {
            // user.undershirt = initial(undershirt.name) and the same for underwear and socks (:221-228): the clothing
            // sprite accessories are not declared (sprite accessories, M9)
            Dm.NotTranslated("code/datums/outfit.dm:221", "equip: the outfit's undershirt, underwear and socks (sprite accessories, M10+)");
        }

        if (Dm.truthy(accessory))
        {
            obj_item_clothing_under U = user.w_uniform as obj_item_clothing_under;
            if (U != null)
                await U.attach_accessory(await SSwardrobe.provide_type(accessory, user) as obj_item_clothing_accessory);
            else if (!Dm.truthy(visuals_only))
                warning(Dm.text("[] in [] at line [] src: [] usr: [].", Dm.text("Unable to equip accessory [] in outfit []. No uniform present!", accessory, name), "code/datums/outfit.dm", 234, this, usr)); // WARNING(MSG), inline
        }

        if (Dm.truthy(l_hand))
            await user.put_in_l_hand(await SSwardrobe.provide_type(l_hand, user) as obj_item, visuals_only: visuals_only);
        if (Dm.truthy(r_hand))
            await user.put_in_r_hand(await SSwardrobe.provide_type(r_hand, user) as obj_item, visuals_only: visuals_only);

        if (!Dm.truthy(visuals_only)) // Items in pockets or backpack don't show up on mob's icon.
        {
            if (Dm.truthy(l_pocket))
                await EQUIP_OUTFIT_ITEM(user, visuals_only, l_pocket, ITEM_SLOT_LPOCKET);
            if (Dm.truthy(r_pocket))
                await EQUIP_OUTFIT_ITEM(user, visuals_only, r_pocket, ITEM_SLOT_RPOCKET);

            if (Dm.truthy(box))
            {
                if (!Dm.truthy(backpack_contents))
                    backpack_contents = Dm.list();
                backpack_contents.Insert(1, box);
                backpack_contents[box] = 1;
            }

            if (Dm.truthy(backpack_contents))
            {
                foreach (object path in Dm.each(backpack_contents))
                {
                    object number = backpack_contents[path];
                    if (!isnum(number)) //Default to 1
                        number = 1;
                    for (float i = 1, i__end = Dm.num(number); i <= i__end; i++)
                        user.equip_to_storage(await SSwardrobe.provide_type(path as Type, user) as obj_item, ITEM_SLOT_BACK, indirect_action: TRUE, del_on_fail: TRUE);
                }
            }

            if (Dm.truthy(belt_contents))
            {
                foreach (object path in Dm.each(belt_contents))
                {
                    object number = belt_contents[path];
                    if (!isnum(number)) //Default to 1
                        number = 1;
                    for (float i = 1, i__end = Dm.num(number); i <= i__end; i++)
                        user.equip_to_storage(await SSwardrobe.provide_type(path as Type, user) as obj_item, ITEM_SLOT_BELT, indirect_action: TRUE, del_on_fail: TRUE);
                }
            }
        }

        await post_equip(user, visuals_only);
        datum_species t__1 = user.dna?.species as datum_species;
        if (t__1 != null)
            await t__1.post_equip_species_outfit(user, visuals_only);

        if (!Dm.truthy(visuals_only))
        {
            await apply_fingerprints(user);
            if (Dm.truthy(internals_slot))
            {
                if (Dm.truthy(Dm.bitand(internals_slot, ITEM_SLOT_HANDS)))
                {
                    obj_item_tank_internals internals = user.is_holding_item_of_type(typeof(obj_item_tank_internals)) as obj_item_tank_internals;
                    if (internals != null)
                        await user.open_internals(internals);
                }
                else
                    await user.open_internals(user.get_item_by_slot(internals_slot) as obj_item_tank);
            }
            if (Dm.truthy(implants))
            {
                // tg: code/datums/outfit.dm:284  var/obj/item/implant/implanter = SSwardrobe.provide_type(implant_type, user); implanter.implant(user, null, TRUE)
                foreach (Type implant_type in Dm.each_cast<Type>(implants))
                {
                    obj_item_implant implanter = await SSwardrobe.provide_type(implant_type, user) as obj_item_implant;
                    await implanter.implant(user, null, TRUE);
                }
            }

            // Insert the skillchips associated with this outfit into the target.
            if (Dm.truthy(skillchips))
            {
                foreach (Type skillchip_path in Dm.each_cast<Type>(skillchips))
                {
                    obj_item_skillchip skillchip_instance = await SSwardrobe.provide_type(skillchip_path) as obj_item_skillchip;
                    object implant_msg = await user.implant_skillchip(skillchip_instance);
                    if (Dm.truthy(implant_msg))
                    {
                        stack_trace(Dm.text("Failed to implant [] with [], on job []. Failure message: []", user, skillchip_instance, this, implant_msg), "code/datums/outfit.dm", 298);
                        qdel(skillchip_instance);
                        return null;
                    }

                    object activate_msg = await skillchip_instance.try_activate_skillchip(TRUE, TRUE);
                    if (Dm.truthy(activate_msg))
                        throw Dm.CRASH(Dm.text("Failed to activate []'s [], on job []. Failure message: []", user, skillchip_instance, this, activate_msg), "code/datums/outfit.dm", 303);
                }
            }
        }

        await user.update_body();
        return TRUE;
    }

    // EQUIP_OUTFIT_ITEM(item_path, slot_name) (:162-168), a file-local macro (#undef at the end of the file) that reads
    // equip's user and visuals_only: a private member of the class with them as parameters (rules 11.4, 11.1).
    // tg: code/datums/outfit.dm:162
    private async DmTask EQUIP_OUTFIT_ITEM(mob_living_carbon_human user, float? visuals_only, Type item_path, float slot_name)
    {
        if (Dm.truthy(item_path))
        {
            await user.equip_to_slot_or_del(await SSwardrobe.provide_type(item_path, user) as obj_item, slot_name, TRUE, indirect_action: TRUE);
            obj_item outfit_item = user.get_item_by_slot(slot_name) as obj_item;
            if (Dm.truthy(outfit_item) && outfit_item.type == item_path)
                await outfit_item.on_outfit_equip(user, visuals_only, slot_name);
        }
    }

    /**
     * Apply a fingerprint from the passed in human to all items in the outfit
     *
     * Used for forensics setup when the mob is first equipped at roundstart
     * essentially calls add_fingerprint to every defined item on the human
     *
     */
    // tg: code/datums/outfit.dm:314
    public virtual async DmTask<object> apply_fingerprints(mob_living_carbon_human user = null)
    {
        if (!(user is mob_living_carbon_human))
            return null;
        // tg: code/datums/outfit.dm:317-347
        if (user.back != null)
        {
            ((atom)user.back).add_fingerprint(user, ignoregloves: TRUE);
            foreach (obj_item item in Dm.each<obj_item>(((atom)user.back).contents))
                item.add_fingerprint(user, ignoregloves: TRUE);
        }
        if (user.wear_id != null)
            ((atom)user.wear_id).add_fingerprint(user, ignoregloves: TRUE);
        if (user.w_uniform != null)
            ((atom)user.w_uniform).add_fingerprint(user, ignoregloves: TRUE);
        if (user.wear_suit != null)
            ((atom)user.wear_suit).add_fingerprint(user, ignoregloves: TRUE);
        if (user.wear_mask != null)
            ((atom)user.wear_mask).add_fingerprint(user, ignoregloves: TRUE);
        if (user.wear_neck != null)
            ((atom)user.wear_neck).add_fingerprint(user, ignoregloves: TRUE);
        if (user.head != null)
            ((atom)user.head).add_fingerprint(user, ignoregloves: TRUE);
        if (user.shoes != null)
            ((atom)user.shoes).add_fingerprint(user, ignoregloves: TRUE);
        if (user.gloves != null)
            ((atom)user.gloves).add_fingerprint(user, ignoregloves: TRUE);
        if (user.ears != null)
            ((atom)user.ears).add_fingerprint(user, ignoregloves: TRUE);
        if (user.glasses != null)
            ((atom)user.glasses).add_fingerprint(user, ignoregloves: TRUE);
        if (user.belt != null)
        {
            ((atom)user.belt).add_fingerprint(user, ignoregloves: TRUE);
            foreach (obj_item item in Dm.each<obj_item>(((atom)user.belt).contents))
                item.add_fingerprint(user, ignoregloves: TRUE);
        }
        if (user.s_store != null)
            ((atom)user.s_store).add_fingerprint(user, ignoregloves: TRUE);
        if (user.l_store != null)
            ((atom)user.l_store).add_fingerprint(user, ignoregloves: TRUE);
        if (user.r_store != null)
            ((atom)user.r_store).add_fingerprint(user, ignoregloves: TRUE);
        return null;
    }
}

// M4a contracts: the roots of this file's procs that no part declared yet, with the signatures of
// docs/ai/specs/generated/slots.tsv (DmTask exactly where suspends = yes), Dm.NotTranslated until M4a P5 translates
// them or leaves them for their milestone (docs/ai/specs/m4a-ownership.md). Overrides are not declared, so the inherited
// code keeps running until then.

// tg: code/datums/outfit.dm:356
public partial class datum_outfit : datum
{
    // tg: code/datums/outfit.dm:356  (the syndicate campaign G1b, D316: the chameleon outfit action reads it)
    public virtual object get_chameleon_disguise_info()
    {
        DmList types = Dm.list(uniform, suit, back, belt, gloves, shoes, head, mask, neck, ears, glasses, id, l_pocket, r_pocket, suit_store, r_hand, l_hand);
        Dm.append(types, chameleon_extras);
        Dm.append(types, skillchips);
        list_clear_nulls(types);
        return types;
    }

    // tg: code/datums/outfit.dm:365
    public virtual object get_types_to_preload()
    {
        Dm.NotTranslated("code/datums/outfit.dm:365", "get_types_to_preload: M4a P5 (skeleton)");
        return default;
    }

    // tg: code/datums/outfit.dm:408
    public virtual object get_json_data()
    {
        Dm.NotTranslated("code/datums/outfit.dm:408", "get_json_data: M4a P5 (skeleton)");
        return default;
    }

    // tg: code/datums/outfit.dm:438
    public virtual void copy_from(datum_outfit target = null)
    {
        Dm.NotTranslated("code/datums/outfit.dm:438", "copy_from: M4a P5 (skeleton)");
    }

    // tg: code/datums/outfit.dm:466
    public virtual void save_to_file(mob admin = null)
    {
        Dm.NotTranslated("code/datums/outfit.dm:466", "save_to_file: M4a P5 (skeleton)");
    }

    // tg: code/datums/outfit.dm:476
    public virtual object load_from(DmList outfit_data = null)
    {
        Dm.NotTranslated("code/datums/outfit.dm:476", "load_from: M4a P5 (skeleton)");
        return default;
    }
}
