// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/ballistic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice C: the ballistic gun base (magazines, bolts, chambering, racking, casing ejection and catching, suppressors, sawing off,
// misfires). Leaves are rows of docs/ai/specs/not-translated.md section 48; the weapon description element's combat information link is
// attached (D291). The suppressor's attachment from the clear_suppressor of gun.dm is translated here. The var blocks are generated (D111).

// tg: code/modules/projectiles/guns/ballistic.dm:3
public partial class obj_item_gun_ballistic : obj_item_gun
{
    // tg: code/modules/projectiles/guns/ballistic.dm:154  #define CASING_CATCH_* (file-local)
    private const int CASING_CATCH_NO_ATTEMPT = 0; // Didn't attempt to catch the casing.
    private const int CASING_CATCH_FAILED_SPICY = 1; // Tried to catch, failed because casing was hot and hands were unprotected.
    private const int CASING_CATCH_FAILED_CLUMSY = 2; // Tried to catch, failed because clumsy.
    private const int CASING_CATCH_FAILED_PLACEMENT = 3; // Tried to catch, failed because hands full.
    private const int CASING_CATCH_SUCCESSFUL = 4; // Tried to catch, succeeded. Hands protected or casing was cold (not recently fired).
    private const int CASING_CATCH_SUCCESSFUL_OUCH = 5; // Tried to catch, succeeded. Casing was hot, hands were unprotected, hands burned.
    // Offset added to an ejected casing's fire timestamp; if world.time is past the casing's fired timestamp plus this offset, casing is considered cold, and won't burn hands.
    private const float CASING_HOT_DELAY = 5 * SECONDS;
    // tg: code/modules/projectiles/guns/ballistic.dm:ff  #define BRAINS_BLOWN_THROW_RANGE 3, BRAINS_BLOWN_THROW_SPEED 1
    private const int BRAINS_BLOWN_THROW_RANGE = 3;
    private const int BRAINS_BLOWN_THROW_SPEED = 1;

    // tg: code/modules/projectiles/guns/ballistic.dm:155
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (spawn_magazine_type == null)
            spawn_magazine_type = accepted_magazine_type;
        if (!Dm.truthy(spawnwithmagazine))
        {
            bolt_locked = TRUE;
            update_appearance();
            return dot;
        }
        if (magazine == null)
        {
            magazine = Dm.@new(spawn_magazine_type, this) as obj_item_ammo_box_magazine;
            if (!istype(magazine, accepted_magazine_type))
                throw Dm.CRASH(Dm.text("[] spawned with a magazine type that isn't allowed by its accepted_magazine_type!", this), "code/modules/projectiles/guns/ballistic.dm", 166);
        }
        if (bolt_type == BOLT_TYPE_STANDARD || Dm.truthy(internal_magazine)) //Internal magazines shouldn't get magazine + 1.
            chamber_round();
        else
            chamber_round(replace_new_round: TRUE);
        if (Dm.truthy(spawn_sawn_off))
            do_sawoff();
        update_appearance();
        RegisterSignal(this, COMSIG_ITEM_RECHARGED, nameof(instant_reload));
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:177
    public override async DmTask on_craft_completion(DmList components = null, datum_crafting_recipe current_recipe = null, atom crafter = null)
    {
        await base.on_craft_completion(components, current_recipe, crafter);
        bool replace_chamber = true;
        bool replace_magazine = magazine == null || !Dm.truthy(Dm.bitand(magazine.item_flags, ABSTRACT)); //don't replace abstract magazines
        foreach (object entry in Dm.each(components))
        {
            if (entry is not obj_item_gun_ballistic gun)
                continue;
            if (gun.magazine != null && Dm.truthy(Dm.bitand(gun.magazine.item_flags, ABSTRACT))) //we cannot insert an internal magazine into the new gun, so we insert the individual casings instead.
            {
                for (int i = 1; i <= gun.magazine.stored_ammo.Count; i++)
                {
                    obj_item_ammo_casing round = gun.magazine.get_round();
                    if (!Dm.truthy(magazine.give_round(round)))
                        await round.forceMove(drop_location() as atom);
                }
            }
            else if (gun.magazine != null && istype(gun.magazine, accepted_magazine_type)) //insert the new magazine into the gun
            {
                obj_item_ammo_box_magazine new_magazine = gun.magazine; //hold onto the reference since magazine is set to null once ejected
                qdel(magazine);
                await new_magazine.forceMove(this);
                magazine = new_magazine;
                replace_magazine = false;
            }
            else if (gun.magazine != null) //the magazine cannot be replaced
            {
                await gun.magazine.forceMove(drop_location() as atom); //drop the magazine on the floor so it doesn't get deleted alongside the gun components.
            }
            else if (replace_magazine && istype(gun.accepted_magazine_type, accepted_magazine_type)) //the gun we used for crafting lacked a magazine so this one should as well
            {
                qdel(magazine);
            }
            if (gun.chambered == null)
                continue;
            obj_item_ammo_casing round__1 = gun.chambered; //hold onto the reference since chambered is set to null once the casing is ejected
            if (!Dm.truthy(magazine?.is_compatible_round(round__1)) || !replace_chamber)
            {
                await round__1.forceMove(drop_location() as atom);
                continue;
            }
            qdel(chambered); //nulled when moved to null
            await round__1.forceMove(this);
            chambered = round__1;
            replace_chamber = false;
        }
        update_appearance();
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:212
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == suppressor)
            clear_suppressor();
        if (gone == magazine)
        {
            if (!QDELETED(magazine))
                magazine.update_appearance();
            magazine = null;
            if (!QDELETED(this))
                update_appearance();
        }
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:223
    public override async DmTask add_weapon_description()
    {
        await AddElement(typeof(datum_element_weapon_description), Dm.named("attached_proc", nameof(add_notes_ballistic)));
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:226
    public override void fire_sounds()
    {
        float max_ammo = (magazine != null && Dm.truthy(magazine.max_ammo)) ? magazine.max_ammo : (spawn_magazine_type != null ? Dm.num(Dm.initial(spawn_magazine_type, (obj_item_ammo_box p) => p.max_ammo)) : 0);
        float current_ammo = get_ammo();
        float frequency_to_use = sin((90 / max_ammo) * current_ammo);
        float click_frequency_to_use = 1 - frequency_to_use * 0.75f;
        bool play_click = round(sqrt(max_ammo * 2)) > current_ammo;
        if (Dm.truthy(suppressed))
        {
            playsound(this, suppressed_sound, suppressed_volume, vary_fire_sound, ignore_walls: FALSE, extrarange: SILENCED_SOUND_EXTRARANGE, falloff_distance: 0);
            if (play_click && Dm.truthy(click_on_low_ammo))
                playsound(this, Dm.resource("sound/items/weapons/gun/general/ballistic_click.ogg"), suppressed_volume, vary_fire_sound, ignore_walls: FALSE, extrarange: SILENCED_SOUND_EXTRARANGE, falloff_distance: 0, frequency: click_frequency_to_use);
        }
        else
        {
            playsound(this, fire_sound, fire_sound_volume, vary_fire_sound);
            if (play_click && Dm.truthy(click_on_low_ammo))
                playsound(this, Dm.resource("sound/items/weapons/gun/general/ballistic_click.ogg"), fire_sound_volume, vary_fire_sound, frequency: click_frequency_to_use);
        }
    }

    /**
     *
     * Outputs type-specific weapon stats for ballistic weaponry based on its magazine and its caliber.
     * It contains extra breaks for the sake of presentation
     *
     **/
    // tg: code/modules/projectiles/guns/ballistic.dm:248
    public virtual object add_notes_ballistic()
    {
        if (magazine != null) // Make sure you have a magazine, to get the notes from
            return Dm.text("\n[]", magazine.add_notes_box());
        else if (chambered != null) // if you don't have a magazine, is there something chambered?
            return Dm.text("\n[]", chambered.add_notes_ammo());
        else // we have a very expensive mechanical paperweight.
            return "\nThe lack of magazine and usable cartridge in chamber makes its usefulness questionable, at best.";
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:261
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][]", Dm.truthy(base_icon_state) ? base_icon_state : Dm.initial(this, (atom p) => p.icon_state), Dm.truthy(sawn_off) ? "_sawn" : "");
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:265
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(selector_switch_icon))
        {
            if (Dm.truthy(burst_fire_selection))
                Dm.append(ref dot, Dm.text("[]_burst", Dm.initial(this, (atom p) => p.icon_state)));
            else
                Dm.append(ref dot, Dm.text("[]_semi", Dm.initial(this, (atom p) => p.icon_state)));
        }
        if (Dm.truthy(show_bolt_icon))
        {
            if (bolt_type == BOLT_TYPE_LOCKING)
                Dm.append(ref dot, Dm.text("[]_bolt[]", icon_state, Dm.truthy(bolt_locked) ? "_locked" : ""));
            if (bolt_type == BOLT_TYPE_OPEN && Dm.truthy(bolt_locked))
                Dm.append(ref dot, Dm.text("[]_bolt", icon_state));
        }
        if (Dm.truthy(suppressed) && Dm.truthy(can_unsuppress)) // if it can't be unsuppressed, we assume the suppressor is integrated into the gun itself and don't generate an overlay
        {
            mutable_appearance MA = mutable_appearance(icon, Dm.text("[]_suppressor", icon_state)) as mutable_appearance;
            if (Dm.truthy(suppressor_x_offset))
                MA.pixel_w = Dm.num(suppressor_x_offset);
            if (Dm.truthy(suppressor_y_offset))
                MA.pixel_z = Dm.num(suppressor_y_offset);
            Dm.append(ref dot, MA);
        }
        if (chambered == null && Dm.truthy(empty_indicator)) //this is duplicated in c20's update_overlayss due to a layering issue with the select fire icon.
            Dm.append(ref dot, Dm.text("[]_empty", icon_state));
        if (Dm.truthy(Dm.bitand(gun_flags, TOY_FIREARM_OVERLAY)))
            Dm.append(ref dot, Dm.text("[]_toy", icon_state));
        if (magazine == null || Dm.truthy(internal_magazine) || !Dm.truthy(mag_display))
            return dot;
        if (Dm.truthy(special_mags))
        {
            Dm.append(ref dot, Dm.text("[]_mag_[]", icon_state, Dm.initial(magazine, (obj_item_ammo_box_magazine p) => p.icon_state)));
            if (Dm.truthy(mag_display_ammo) && magazine.ammo_count() == 0)
                Dm.append(ref dot, Dm.text("[]_mag_empty", icon_state));
            return dot;
        }
        Dm.append(ref dot, Dm.text("[]_mag", icon_state));
        if (!Dm.truthy(mag_display_ammo))
            return dot;
        float capacity_number = 0;
        float fullness = get_ammo() / magazine.max_ammo;
        if (fullness >= 1) //cause we can have one in the chamber.
            capacity_number = 100;
        else if (fullness >= 0.8f)
            capacity_number = 80;
        else if (fullness >= 0.6f)
            capacity_number = 60;
        else if (fullness >= 0.4f)
            capacity_number = 40;
        else if (fullness >= 0.2f)
            capacity_number = 20;
        if (capacity_number != 0)
            Dm.append(ref dot, Dm.text("[]_mag_[]", icon_state, capacity_number));
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:323
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        if (istype(actiontype, typeof(datum_action_item_action_toggle_firemode)))
            await burst_select();
        else
            await base.ui_action_click(user, actiontype);
        return null;
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:329
    public virtual async DmTask burst_select()
    {
        mob_living_carbon_human user = usr as mob_living_carbon_human;
        burst_fire_selection = Dm.truthy(burst_fire_selection) ? FALSE : TRUE;
        if (!Dm.truthy(burst_fire_selection))
        {
            burst_size = 1;
            fire_delay = 0 * SECONDS;
            balloon_alert(user, I18n.T("switched to semi-automatic"));
        }
        else
        {
            burst_size = Dm.num(Dm.initial(this, (obj_item_gun p) => p.burst_size));
            fire_delay = Dm.num(Dm.initial(this, (obj_item_gun p) => p.fire_delay));
            balloon_alert(user, I18n.T("switched to []-round burst", burst_size));
        }
        if (burst_select_sound != null)
            playsound(user, burst_select_sound, 50, TRUE);
        else
            playsound(user, Dm.resource("sound/items/weapons/empty.ogg"), 100, TRUE);
        update_appearance();
        await update_item_action_buttons();
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:364
    public override async DmTask handle_chamber(float? empty_chamber = null, float? from_firing = null, float? chamber_next_round = null)
    {
        empty_chamber ??= TRUE;
        from_firing ??= TRUE;
        chamber_next_round ??= TRUE;
        if (!Dm.truthy(semi_auto) && Dm.truthy(from_firing))
            return;
        obj_item_ammo_casing casing = chambered; //Find chambered round
        if (casing != null) //there's a chambered round
        {
            if (QDELING(casing))
            {
                stack_trace(Dm.text("Trying to move a qdeleted casing of type []!", casing.GetType()), "code/modules/projectiles/guns/ballistic.dm", 349);
                chambered = null;
            }
            else if (Dm.truthy(casing_ejector) || !Dm.truthy(from_firing))
            {
                await casing.forceMove(drop_location() as atom); //Eject casing onto ground.
                if (!QDELETED(casing))
                {
                    if (Dm.truthy(casing._listen_lookup?[COMSIG_CASING_EJECTED]))
                        casing._SendSignal(COMSIG_CASING_EJECTED, Dm.list(casing));
                    bool hitting_ground = true;
                    if (ishuman(loc))
                    {
                        mob_living_carbon_human wielder = loc as mob_living_carbon_human;
                        int caught_casing = can_catch_casing(casing, wielder);
                        switch (caught_casing)
                        {
                            case CASING_CATCH_NO_ATTEMPT:
                                hitting_ground = true;
                                break;
                            case CASING_CATCH_FAILED_SPICY:
                                {
                                    hitting_ground = true;
                                    wielder.visible_message(
                                        span_warning(I18n.T("[] reaches out for \\the [] as it ejects from [], and catches it... before fumbling it because it's a hot casing. Uncool!", wielder, casing, this)),
                                        span_warning(I18n.T("You reach out and catch \\the [] as it ejects from []... before dropping it, because it's a hot casing! Ouch! Uncool!", casing, this)),
                                        span_notice(I18n.T("You hear someone reaching for something before a hiss of pain and the sound of something clattering.")));
                                    obj_item_bodypart affecting = wielder.get_inactive_hand() as obj_item_bodypart;
                                    await wielder.apply_damage(2, BURN, affecting, wound_bonus: CANT_WOUND);
                                    break;
                                }
                            case CASING_CATCH_FAILED_CLUMSY:
                                {
                                    hitting_ground = true;
                                    wielder.visible_message(
                                        span_warning(I18n.T("[] reaches out for \\the [] as it ejects from []... before fumbling it in an incredibly unlikely, comical manner! Uncool!", wielder, casing, this)),
                                        span_warning(I18n.T("You reach out and catch \\the [] as it ejects from []... before fumbling it in an incredibly unlikely, comical manner! Uncool!", casing, this)),
                                        span_notice(I18n.T("You hear someone reaching for something, shortly followed by an embarassingly loud, comedic clattering.")));
                                    if (!(world.time >= casing.shot_timestamp + CASING_HOT_DELAY))
                                    {
                                        obj_item_bodypart affecting = wielder.get_inactive_hand() as obj_item_bodypart;
                                        to_chat(wielder, span_warning(I18n.T("As if to add insult to injury, \\the [] lands in the perfect way... to burn your [].", casing, affecting.plaintext_zone)));
                                        await wielder.apply_damage(5, BURN, affecting, wound_bonus: CANT_WOUND);
                                    }
                                    break;
                                }
                            case CASING_CATCH_FAILED_PLACEMENT:
                                hitting_ground = true;
                                wielder.visible_message(
                                    span_warning(I18n.T("[] reaches out for \\the [] as it ejects from [] and fumbles it due to [] full hands. Uncool!", wielder, casing, this, wielder.p_their())),
                                    span_warning(I18n.T("You try and reach out for \\the [] as it ejects from [], and fumble it because your hands are full. Uncool!", casing, this)),
                                    span_notice(I18n.T("You hear someone reaching for something, before a metallic clattering.")));
                                break;
                            case CASING_CATCH_SUCCESSFUL:
                                hitting_ground = false;
                                casing.update_appearance();
                                to_chat(wielder, span_notice(I18n.T("You reach out and catch \\the [] as it ejects from []. Awesome.", casing, this)));
                                break;
                            case CASING_CATCH_SUCCESSFUL_OUCH:
                                {
                                    hitting_ground = false;
                                    casing.update_appearance();
                                    obj_item_bodypart affecting = wielder.get_inactive_hand() as obj_item_bodypart;
                                    to_chat(wielder, span_notice(I18n.T("You reach out and catch \\the [] as it ejects from []. Awesome. Your [] hurts, though.", casing, this, affecting.plaintext_zone)));
                                    await wielder.apply_damage(4, BURN, affecting, wound_bonus: CANT_WOUND);
                                    break;
                                }
                        }
                    }
                    if (hitting_ground)
                        casing.bounce_away(TRUE);
                }
            }
            else if (Dm.truthy(empty_chamber))
            {
                clear_chambered();
            }
        }
        if (Dm.truthy(chamber_next_round) && (magazine != null && magazine.max_ammo > 1))
            chamber_round();
    }

    /// Used to check if the mob `wielder` can catch an ejected casing.
    /// Returns CASING_CATCH_NO_ATTEMPT if not trying, CASING_CATCH_FAILED if failed, CASING_CATCH_SUCCESSFUL if successful.
    // tg: code/modules/projectiles/guns/ballistic.dm:429
    public virtual int can_catch_casing(obj_item_ammo_casing casing = null, mob_living_carbon_human wielder = null)
    {
        if (!Dm.truthy(wielder.throw_mode)) // if they're not in throw mode, don't bother
            return CASING_CATCH_NO_ATTEMPT;
        if (HAS_TRAIT(wielder, TRAIT_CLUMSY)) // feats of dexterity are beyond the jester
            return CASING_CATCH_FAILED_CLUMSY;
        // following adapted from lightbulbs
        bool protected_hands = false;
        if (wielder.gloves != null)
        {
            obj_item_clothing_gloves electrician_gloves = wielder.gloves as obj_item_clothing_gloves;
            if (Dm.truthy(electrician_gloves.max_heat_protection_temperature) && Dm.num(electrician_gloves.max_heat_protection_temperature) > 360)
                protected_hands = true;
        }
        // from left to right: are our hands protected from hot things via gloves? are we or our hands heat resistant? was this casing shot more than 5 seconds ago?
        if (protected_hands || HAS_TRAIT(wielder, TRAIT_RESISTHEAT) || HAS_TRAIT(wielder, TRAIT_RESISTHEATHANDS) || world.time >= casing.shot_timestamp + CASING_HOT_DELAY)
        {
            if (Dm.truthy(Waiting.NoSleep(wielder.put_in_hands(casing)))) // try placement in hand,
                return CASING_CATCH_SUCCESSFUL; // success
            return CASING_CATCH_FAILED_PLACEMENT; // or not.
        }
        if (HAS_TRAIT(wielder, TRAIT_LIGHTBULB_REMOVER))
        {
            if (Dm.truthy(Waiting.NoSleep(wielder.put_in_hands(casing)))) // try placement in hand,
                return CASING_CATCH_SUCCESSFUL_OUCH; // success
            return CASING_CATCH_FAILED_PLACEMENT; // or not.
        }
        return CASING_CATCH_FAILED_SPICY;
    }

    ///Used to chamber a new round and eject the old one
    // tg: code/modules/projectiles/guns/ballistic.dm:460
    public virtual void chamber_round(float? spin_cylinder = null, float? replace_new_round = null)
    {
        if (chambered != null || magazine == null)
            return;
        if (magazine.ammo_count() != 0)
        {
            chambered = ((bolt_type == BOLT_TYPE_OPEN && !Dm.truthy(bolt_locked)) || bolt_type == BOLT_TYPE_NO_BOLT) ? magazine.get_and_shuffle_round() : magazine.get_round();
            if (bolt_type != BOLT_TYPE_OPEN && !(Dm.truthy(internal_magazine) && bolt_type == BOLT_TYPE_NO_BOLT))
                Waiting.NoSleep(chambered.forceMove(this));
            else
                RegisterSignal(chambered, COMSIG_MOVABLE_MOVED, nameof(clear_chambered));
            if (Dm.truthy(replace_new_round))
                magazine.give_round(Dm.@new(chambered.GetType()) as obj_item_ammo_casing);
        }
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:472
    [SignalHandler]
    public virtual void clear_chambered(datum source = null)
    {
        UnregisterSignal(chambered, COMSIG_MOVABLE_MOVED);
        chambered = null;
    }

    ///updates a bunch of racking related stuff and also handles the sound effects and the like
    // tg: code/modules/projectiles/guns/ballistic.dm:478
    public virtual async DmTask rack(mob user = null)
    {
        if (bolt_type == BOLT_TYPE_NO_BOLT) //If there's no bolt, nothing to rack
            return;
        if (bolt_type == BOLT_TYPE_OPEN)
        {
            if (!Dm.truthy(bolt_locked)) //If it's an open bolt, racking again would do nothing
            {
                if (user != null)
                    balloon_alert(user, I18n.T("[] already cocked!", bolt_wording));
                return;
            }
            bolt_locked = FALSE;
        }
        if (user != null)
            balloon_alert(user, I18n.T("[] racked", bolt_wording));
        await process_chamber(chambered == null ? TRUE : FALSE, FALSE);
        if (bolt_type == BOLT_TYPE_LOCKING && chambered == null)
        {
            bolt_locked = TRUE;
            playsound(this, lock_back_sound, lock_back_sound_volume, lock_back_sound_vary);
        }
        else
        {
            playsound(this, rack_sound, rack_sound_volume, rack_sound_vary);
        }
        HeldMotionSeam.Report(user, HeldMotion.Rack); // DEVIATION(DV213): the user's first-person gun moves
        update_appearance();
    }

    ///Drops the bolt from a locked position
    // tg: code/modules/projectiles/guns/ballistic.dm:498
    public virtual async DmTask drop_bolt(mob user = null)
    {
        playsound(this, bolt_drop_sound, bolt_drop_sound_volume, FALSE);
        if (user != null)
            balloon_alert(user, I18n.T("[] dropped", bolt_wording));
        chamber_round();
        bolt_locked = FALSE;
        HeldMotionSeam.Report(user, HeldMotion.Rack); // DEVIATION(DV213): the user's first-person gun moves
        update_appearance();
    }

    ///Handles all the logic needed for magazine insertion
    // tg: code/modules/projectiles/guns/ballistic.dm:507
    public virtual async DmTask<object> insert_magazine(mob user = null, obj_item_ammo_box_magazine AM = null, float? display_message = null)
    {
        display_message ??= TRUE;
        if (!istype(AM, accepted_magazine_type))
        {
            balloon_alert(user, I18n.T("[] doesn't fit!", AM.name));
            return FALSE;
        }
        if (Dm.truthy(await user.transferItemToLoc(AM, this)))
        {
            magazine = AM;
            if (Dm.truthy(display_message))
                balloon_alert(user, I18n.T("[] loaded", magazine_wording));
            if (magazine.ammo_count() != 0)
                playsound(this, load_sound, load_sound_volume, load_sound_vary);
            else
                playsound(this, load_empty_sound, load_sound_volume, load_sound_vary);
            HeldMotionSeam.Report(user, HeldMotion.Load); // DEVIATION(DV213): the user's first-person gun moves
            if (bolt_type == BOLT_TYPE_OPEN && !Dm.truthy(bolt_locked))
                chamber_round();
            update_appearance();
            return TRUE;
        }
        else
        {
            to_chat(user, span_warning(I18n.T("You cannot seem to get [] out of your hands!", this)));
            return FALSE;
        }
    }

    ///Handles all the logic of magazine ejection, if tac_load is set that magazine will be tacloaded in the place of the old eject
    // tg: code/modules/projectiles/guns/ballistic.dm:528
    public virtual async DmTask eject_magazine(mob user = null, float? display_message = null, obj_item_ammo_box_magazine tac_load = null)
    {
        display_message ??= TRUE;
        if (bolt_type == BOLT_TYPE_OPEN)
            chambered = null;
        if (magazine.ammo_count() != 0)
            playsound(this, eject_sound, eject_sound_volume, eject_sound_vary);
        else
            playsound(this, eject_empty_sound, eject_sound_volume, eject_sound_vary);
        HeldMotionSeam.Report(user, HeldMotion.Unload); // DEVIATION(DV213): the user's first-person gun moves
        obj_item_ammo_box_magazine old_mag = magazine;
        await magazine.forceMove(drop_location() as atom);
        if (tac_load != null)
        {
            if (Dm.truthy(await insert_magazine(user, tac_load, FALSE)))
                balloon_alert(user, I18n.T("[] swapped", magazine_wording));
            else
                to_chat(user, span_warning(I18n.T("You dropped the old [], but the new one doesn't fit. How embarassing.", magazine_wording)));
        }
        await user.put_in_hands(old_mag);
        if (Dm.truthy(display_message))
            balloon_alert(user, I18n.T("[] unloaded", magazine_wording));
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:546
    public override object can_shoot()
    {
        return chambered?.loaded_projectile;
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:549
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(internal_magazine) && istype(tool, typeof(obj_item_ammo_box_magazine)))
        {
            if (magazine == null)
            {
                await insert_magazine(user, tool as obj_item_ammo_box_magazine);
                return ITEM_INTERACT_SUCCESS;
            }
            if (Dm.truthy(tac_reloads))
            {
                await eject_magazine(user, FALSE, tool as obj_item_ammo_box_magazine);
                return ITEM_INTERACT_SUCCESS;
            }
            balloon_alert(user, I18n.T("already loaded!"));
            return ITEM_INTERACT_FAILURE;
        }
        if (istype(tool, typeof(obj_item_ammo_casing)) || istype(tool, typeof(obj_item_ammo_box)))
        {
            if (Dm.truthy(must_hold_to_load) && !check_if_held(user))
                return NONE;
            if (bolt_type == BOLT_TYPE_NO_BOLT || Dm.truthy(internal_magazine))
            {
                if (Dm.truthy(await load_gun(tool, user)))
                    return ITEM_INTERACT_SUCCESS;
                return ITEM_INTERACT_FAILURE;
            }
        }
        if (istype(tool, typeof(obj_item_suppressor)))
        {
            if (!Dm.truthy(can_suppress))
            {
                balloon_alert(user, I18n.T("[] doesn't fit!", tool.name));
                return ITEM_INTERACT_FAILURE;
            }
            if (!Dm.truthy(user.is_holding(this)))
            {
                balloon_alert(user, I18n.T("not in hand!"));
                return ITEM_INTERACT_FAILURE;
            }
            if (Dm.truthy(suppressed))
            {
                balloon_alert(user, I18n.T("already has a suppressor!"));
                return ITEM_INTERACT_FAILURE;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            {
                balloon_alert(user, I18n.T("cannot attach!"));
                return ITEM_INTERACT_FAILURE;
            }
            balloon_alert(user, I18n.T("[] attached", tool.name));
            install_suppressor(tool as obj_item_suppressor);
            return ITEM_INTERACT_SUCCESS;
        }
        if (Dm.truthy(can_be_sawn_off) && Dm.truthy(await try_sawoff(user, tool)))
            return ITEM_INTERACT_SUCCESS;
        return null;
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:599
    public virtual async DmTask<object> load_gun(obj_item ammo = null, mob_living user = null)
    {
        if (chambered != null && chambered.loaded_projectile == null)
        {
            await chambered.forceMove(drop_location() as atom);
            if (magazine?.stored_ammo != null && magazine.stored_ammo.Count > 0 && chambered != magazine.stored_ammo[1f])
                magazine.stored_ammo.Remove(chambered);
            chambered = null;
        }
        float num_loaded = magazine != null ? Dm.num(await magazine.try_load(user, ammo, silent: TRUE)) : 0;
        if (num_loaded == 0)
            return FALSE;
        balloon_alert(user, I18n.T("[] []\\s loaded", num_loaded, cartridge_wording));
        playsound(this, load_sound, load_sound_volume, load_sound_vary);
        HeldMotionSeam.Report(user, HeldMotion.Load); // DEVIATION(DV213): the user's first-person gun moves
        if (chambered == null && bolt_type == BOLT_TYPE_NO_BOLT)
            chamber_round();
        ammo.update_appearance();
        update_appearance();
        return TRUE;
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:618
    public virtual bool check_if_held(mob user = null)
    {
        if (this != user.get_inactive_held_item())
            return false;
        return true;
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:623
    public override async DmTask<object> process_fire(atom target = null, mob_living user = null, float? message = null, object @params = null, string zone_override = null, float? bonus_spread = null)
    {
        message ??= TRUE;
        zone_override ??= "";
        bonus_spread ??= 0;
        bool could_it_misfire = Dm.truthy(can_misfire) || Dm.truthy(chambered?.can_misfire);
        if (target != user && chambered?.loaded_projectile != null && could_it_misfire && prob(misfire_probability) && Dm.truthy(await blow_up(user)))
        {
            to_chat(user, span_userdanger(I18n.T("[] misfires!", this)));
            return null;
        }
        if (Dm.truthy(sawn_off))
            bonus_spread += SAWN_OFF_ACC_PENALTY;
        return await base.process_fire(target, user, message, @params, zone_override, bonus_spread);
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:634
    public override object calculate_recoil(mob_living user = null, float? recoil_amount = null)
    {
        recoil_amount ??= 0;
        if (Dm.truthy(sawn_off))
            recoil_amount += sawoff_bonus_recoil;
        return base.calculate_recoil(user, recoil_amount);
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:639
    public override async DmTask<object> shoot_live_shot(mob_living user = null, float? pointblank = null, atom pbtarget = null, float? message = null)
    {
        pointblank ??= 0;
        message ??= 1;
        if (chambered == null)
            return await base.shoot_live_shot(user, pointblank, pbtarget, message);
        if (Dm.truthy(can_misfire))
        {
            misfire_probability += misfire_percentage_increment;
            misfire_probability = clamp(misfire_probability, 0, misfire_probability_cap);
        }
        if (Dm.truthy(chambered.can_misfire))
        {
            misfire_probability += chambered.misfire_increment;
            misfire_probability = clamp(misfire_probability, 0, misfire_probability_cap);
        }
        return await base.shoot_live_shot(user, pointblank, pbtarget, message);
    }

    ///Installs a new suppressor, assumes that the suppressor is already in the contents of src
    // tg: code/modules/projectiles/guns/ballistic.dm:651
    public virtual void install_suppressor(obj_item_suppressor new_suppressor = null)
    {
        suppressor = new_suppressor;
        suppressed = suppressor.suppression;
        update_weight_class(w_class + suppressor.w_class); //so pistols do not fit in pockets when suppressed
        can_muzzle_flash = FALSE;
        update_appearance();
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:658
    public override void clear_suppressor()
    {
        suppressed = SUPPRESSED_NONE;
        if (suppressor != null)
        {
            update_weight_class(w_class - suppressor.w_class);
            suppressor = null;
        }
        can_muzzle_flash = Dm.num(Dm.initial(this, (obj_item_gun p) => p.can_muzzle_flash));
        update_appearance();
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:666
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(suppressed) || !Dm.truthy(can_unsuppress))
            return CLICK_ACTION_BLOCKING;
        if (!Dm.truthy(user.is_holding(this)))
            return CLICK_ACTION_BLOCKING;
        balloon_alert(user, I18n.T("[] removed", suppressor.name));
        await user.put_in_hands(suppressor);
        clear_suppressor();
        return CLICK_ACTION_SUCCESS;
    }

    ///Prefire empty checks for the bolt drop
    // tg: code/modules/projectiles/guns/ballistic.dm:677
    public virtual void prefire_empty_checks()
    {
        if (chambered == null && get_ammo() == 0)
        {
            if (bolt_type == BOLT_TYPE_OPEN && !Dm.truthy(bolt_locked))
            {
                bolt_locked = TRUE;
                playsound(this, bolt_drop_sound, bolt_drop_sound_volume);
                update_appearance();
            }
        }
    }

    ///postfire empty checks for bolt locking and sound alarms
    // tg: code/modules/projectiles/guns/ballistic.dm:685
    public virtual void postfire_empty_checks(object last_shot_succeeded = null)
    {
        if (chambered == null && get_ammo() == 0)
        {
            if (Dm.truthy(empty_alarm) && Dm.truthy(last_shot_succeeded))
            {
                playsound(this, empty_alarm_sound, empty_alarm_volume, empty_alarm_vary);
                update_appearance();
            }
            if (Dm.truthy(last_shot_succeeded) && bolt_type == BOLT_TYPE_LOCKING && Dm.truthy(semi_auto))
            {
                bolt_locked = TRUE;
                update_appearance();
            }
        }
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:694
    public override async DmTask<object> fire_gun(atom target = null, object user = null, object flag = null, object @params = null)
    {
        prefire_empty_checks();
        object dot = await base.fire_gun(target, user, flag, @params); //The gun actually firing
        postfire_empty_checks(dot);
        return dot;
    }

    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/modules/projectiles/guns/ballistic.dm:700
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(internal_magazine) && loc == user && Dm.truthy(user.is_holding(this)) && magazine != null)
        {
            await eject_magazine(user);
            return null;
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:706
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        mob_living living_user = user as mob_living;
        if (!Dm.truthy(internal_magazine) && magazine != null)
        {
            if (magazine.ammo_count() == 0)
            {
                await eject_magazine(user);
                return null;
            }
        }
        if (bolt_type == BOLT_TYPE_NO_BOLT)
        {
            await unload_ammo(living_user);
            return null;
        }
        if (bolt_type == BOLT_TYPE_LOCKING && Dm.truthy(bolt_locked))
        {
            await drop_bolt(user);
            return null;
        }
        if (recent_rack > world.time)
            return null;
        recent_rack = world.time + rack_delay;
        await rack(user);
        return null;
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:723
    public virtual async DmTask unload_ammo(mob_living user = null, float? forced = null) // a DmTask slot since gaps GU1: the doohickey's override waits on a do_after (slots.tsv)
    {
        forced ??= FALSE;
        float num_unloaded = 0;
        turf drop_turf = get_turf(drop_location());
        foreach (obj_item_ammo_casing casing in Dm.each_cast<obj_item_ammo_casing>(get_ammo_list(FALSE)))
        {
            await casing.forceMove(drop_location() as atom);
            casing.bounce_away(FALSE, NONE);
            num_unloaded++;
            if (drop_turf != null && Dm.truthy(is_station_level(drop_turf.z)))
                SSblackbox.record_feedback("tally", "station_mess_created", 1, casing.name);
        }
        if (num_unloaded == 0)
        {
            if (!Dm.truthy(forced))
                balloon_alert(user, I18n.T("it's empty!"));
            return;
        }
        if (!Dm.truthy(forced))
            balloon_alert(user, I18n.T("[] []\\s unloaded", num_unloaded, cartridge_wording));
        playsound(user, eject_sound, eject_sound_volume, eject_sound_vary);
        HeldMotionSeam.Report(user, HeldMotion.Unload); // DEVIATION(DV213): the user's first-person gun moves
        update_appearance();
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:743
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        bool count_chambered = !(bolt_type == BOLT_TYPE_NO_BOLT || bolt_type == BOLT_TYPE_OPEN);
        Dm.append(ref dot, Dm.text("It has <b>[]</b> round\\s remaining.", get_ammo(count_chambered ? TRUE : FALSE)));
        if (chambered == null && !Dm.truthy(hidden_chambered))
            Dm.append(ref dot, "It does not seem to have a round chambered.");
        if (Dm.truthy(bolt_locked))
            Dm.append(ref dot, Dm.text("The [] is locked back and needs to be released before firing or de-fouling.", bolt_wording));
        if (suppressor != null)
            Dm.append(ref dot, Dm.text("It has a suppressor []", Dm.truthy(can_unsuppress) ? "attached that can be removed with <b>alt+click</b>." : "that is integral or can't otherwise be removed."));
        if (Dm.truthy(can_misfire))
        {
            Dm.append(ref dot, span_danger("You get the feeling this might explode if you fire it..."));
            if (misfire_probability > 0)
                Dm.append(ref dot, span_danger(Dm.text("Given the state of the gun, there is a []% chance it'll misfire.", misfire_probability)));
        }
        else if (misfire_probability > 0)
        {
            Dm.append(ref dot, span_warning("You get a feeling this might explode if you fire it with the wrong ammunitions..."));
            Dm.append(ref dot, span_warning(Dm.text("Given the state of the gun, there is a [] chance it'll misfire.", EXAMINE_HINT(Dm.text("[]%", misfire_probability)))));
        }
        return dot;
    }

    ///Gets the number of bullets in the gun
    // tg: code/modules/projectiles/guns/ballistic.dm:763
    public virtual float get_ammo(float? countchambered = null, float? countempties = null)
    {
        countchambered ??= TRUE;
        float bullets = 0; //No silly variable names on my watch.
        if (chambered != null && Dm.truthy(countchambered))
            bullets++;
        if (magazine != null)
            bullets += magazine.ammo_count();
        return bullets;
    }

    ///gets a list of every bullet in the gun
    // tg: code/modules/projectiles/guns/ballistic.dm:772
    public virtual DmList get_ammo_list(float? countchambered = null)
    {
        countchambered ??= TRUE;
        DmList rounds = Dm.list();
        if (chambered != null && Dm.truthy(countchambered))
            rounds.Add(chambered);
        if (magazine != null)
            rounds.Add(magazine.ammo_list());
        return rounds;
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:783
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        obj_item_organ_brain B = user.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
        if (B != null && chambered != null && chambered.loaded_projectile != null && Dm.truthy(await can_trigger_gun(user)) && chambered.loaded_projectile.damage > 0)
        {
            user.visible_message(span_suicide(I18n.T("[] is putting the barrel of [] in [] mouth. It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));
            await sleep(2.5f * SECONDS);
            if (Dm.truthy(user.is_holding(this)))
            {
                turf T = get_turf(user);
                await process_fire(user, user, FALSE, null, BODY_ZONE_HEAD);
                user.visible_message(span_suicide(I18n.T("[] blows [] brain[] out with []!", user, user.p_their(), user.p_s(), this)));
                object target = get_ranged_target_turf(user, REVERSE_DIR(user.dir), BRAINS_BLOWN_THROW_RANGE);
                await B.Remove(user as mob_living_carbon);
                await B.forceMove(T);
                datum_callback gibspawner = Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.spawn_atom_to_turf), typeof(obj_effect_gibspawner_generic), B, 1, FALSE, user);
                await B.throw_at(target as atom, BRAINS_BLOWN_THROW_RANGE, BRAINS_BLOWN_THROW_SPEED, callback: gibspawner);
                return BRUTELOSS;
            }
            else
            {
                user.visible_message(span_suicide(I18n.T("[] panics and starts choking to death!", user)));
                return OXYLOSS;
            }
        }
        else
        {
            user.visible_message(span_suicide(I18n.T("[] is pretending to blow [] brain[] out with []! It looks like [] trying to commit suicide!</b>", user, user.p_their(), user.p_s(), this, user.p_theyre())));
            playsound(this, dry_fire_sound, 30, TRUE);
            return OXYLOSS;
        }
    }

    ///Handles all the logic of sawing off guns,
    // tg: code/modules/projectiles/guns/ballistic.dm:816
    public virtual async DmTask<object> try_sawoff(mob user = null, obj_item saw = null)
    {
        if (!Dm.truthy(saw.get_sharpness()) || (!Dm.truthy(is_type_in_typecache(saw, GLOB.gun_saw_types)) && !Dm.eq(saw.tool_behaviour, TOOL_SAW))) //needs to be sharp. Otherwise turned off eswords can cut this.
            return null;
        if (Dm.truthy(sawn_off))
        {
            balloon_alert(user, I18n.T("it's already shortened!"));
            return null;
        }
        if (Dm.truthy(Dm.bitand(!Dm.truthy(_listen_lookup?[COMSIG_GUN_BEING_SAWNOFF]) ? (object)NONE : _SendSignal(COMSIG_GUN_BEING_SAWNOFF, Dm.list(this, user)), COMPONENT_CANCEL_SAWING_OFF)))
            return null;
        user.changeNext_move(CLICK_CD_MELEE);
        user.visible_message(span_notice(I18n.T("[] begins to shorten [].", user, this)), span_notice(I18n.T("You begin to shorten []...", this)));

        //if there's any live ammo inside the gun, makes it go off
        if (Dm.truthy(await blow_up(user)))
        {
            user.visible_message(span_danger(I18n.T("[] goes off!", this)), span_danger(I18n.T("[] goes off in your face!", this)));
            return null;
        }

        if (!Dm.truthy(await do_after(user, 3 * SECONDS, this)))
            return null;

        if (Dm.truthy(sawn_off))
            return null;
        user.visible_message(span_notice(I18n.T("[] shortens []!", user, this)), span_notice(I18n.T("You shorten [].", this)));
        object dot = do_sawoff();
        update_appearance();
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:840
    [ShouldCallParent]
    public virtual object do_sawoff()
    {
        sawn_off = TRUE;
        if (Dm.truthy(_listen_lookup?[COMSIG_GUN_SAWN_OFF]))
            _SendSignal(COMSIG_GUN_SAWN_OFF, Dm.list(this));
        name = Dm.text("sawn-off []", name);
        desc = sawn_desc as string;
        update_weight_class(WEIGHT_CLASS_NORMAL);
        //The file might not have a "gun" icon, let's prepare for this
        lefthand_file = Dm.resource("icons/mob/inhands/weapons/guns_lefthand.dmi");
        righthand_file = Dm.resource("icons/mob/inhands/weapons/guns_righthand.dmi");
        inhand_x_dimension = 32;
        inhand_y_dimension = 32;
        inhand_icon_state = "gun";
        worn_icon_state = "gun";
        slot_flags = Dm.num(Dm.bitand(slot_flags, ~ITEM_SLOT_BACK)); //you can't sling it on your back
        slot_flags = Dm.num(Dm.bitor(slot_flags, ITEM_SLOT_BELT)); //but you can wear it on your belt (poorly concealed under a trenchcoat, ideally)
        return TRUE;
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:859
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item I = null)
    {
        if (!Dm.truthy(can_modify_ammo))
            return null;
        if (!Dm.truthy(user.is_holding(this)))
        {
            balloon_alert(user, I18n.T("hold to modify!"));
            return TRUE;
        }
        if (get_ammo() != 0)
        {
            balloon_alert(user, I18n.T("can't modify while loaded!"));
            return null;
        }
        if (!Dm.truthy(bolt_locked) && bolt_type == BOLT_TYPE_LOCKING)
        {
            balloon_alert(user, I18n.T("the bolt is in the way!"));
            return null;
        }
        balloon_alert(user, I18n.T("tinkering..."));
        I.play_tool_sound(this);
        if (!Dm.truthy(await I.use_tool(this, user, 3 * SECONDS)))
            return TRUE;

        if (Dm.eq(magazine.caliber, initial_caliber))
        {
            magazine.caliber = alternative_caliber;
            if (Dm.truthy(alternative_ammo_misfires))
                can_misfire = TRUE;
            fire_sound = alternative_fire_sound;
            to_chat(user, span_notice(I18n.T("You modify []. Now it will fire [] rounds.", this, alternative_caliber)));
        }
        else
        {
            magazine.caliber = initial_caliber;
            if (Dm.truthy(alternative_ammo_misfires))
                can_misfire = FALSE;
            fire_sound = initial_fire_sound;
            to_chat(user, span_notice(I18n.T("You reset []. Now it will fire [] rounds.", this, initial_caliber)));
        }
        return null;
    }

    ///used for sawing guns, causes the gun to fire without the input of the user
    // tg: code/modules/projectiles/guns/ballistic.dm:894
    public virtual async DmTask<object> blow_up(mob user = null)
    {
        return chambered != null ? await process_fire(user, user as mob_living, FALSE) : null;
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:897
    [SignalHandler]
    public virtual void instant_reload()
    {
        if (magazine != null)
        {
            magazine.top_off();
        }
        else
        {
            if (spawn_magazine_type == null)
                return;
            magazine = Dm.@new(spawn_magazine_type, this) as obj_item_ammo_box_magazine;
        }
        chamber_round();
        update_appearance();
    }

    // tg: code/modules/projectiles/guns/ballistic.dm:908
    public override async DmTask<object> toss_gun_hard(mob_living thrower = null, mob_living target = null)
    {
        object dot = await base.toss_gun_hard(thrower, target);
        if (!Dm.truthy(dot))
            return dot;
        switch (bolt_type)
        {
            case BOLT_TYPE_NO_BOLT: //emptying the revolver cylinder
                await attack_self();
                return dot;
            case BOLT_TYPE_OPEN: //emptying the chamber of an automatic weapon, because rack() doesn't do this to it
                await handle_chamber(chamber_next_round: FALSE);
                break;
        }
        if (!Dm.truthy(internal_magazine) && magazine != null) //if a magazine is attached to the weapon, we remove it and throw it aside
        {
            await magazine.forceMove(drop_location() as atom);
            await magazine.throw_at(get_edge_target_turf(this, pick(GLOB.alldirs)) as atom, 1, 1);
            await update_icon(); //updating the sprite of weapons without a magazine
        }
        if (chambered != null) //if there is a cartridge in the chamber, we remove it
            await rack();
        return dot;
    }
}

// tg: code/modules/projectiles/guns/ballistic.dm:906  GLOBAL_LIST_INIT(gun_saw_types, typecacheof(list(...)))
public partial class datum_controller_global_vars
{
    public DmList gun_saw_types;

    // tg: code/modules/projectiles/guns/ballistic.dm:906  (GLOBAL_MANAGED(gun_saw_types, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalgun_saw_types()
    {
        gun_saw_types = typecacheof(Dm.list(
            typeof(obj_item_gun_energy_plasmacutter),
            typeof(obj_item_melee_energy),
            typeof(obj_item_dualsaber)
        )) as DmList;
        Dm.append(ref gvars_datum_init_order, "gun_saw_types");
    }
}
