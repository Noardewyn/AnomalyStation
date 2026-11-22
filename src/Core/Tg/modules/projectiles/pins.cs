// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/pins.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Whole (the abductor pin is abductor_items.dm's). M7x slice B: the base pin, the
// test-range pin and the clown pins; syndicate N2a (D316): the implant pins (the operatives' syndicate pin and the mindshield pin);
// gaps CL1 (D333): the laser tag pins (their vest check is the wearer's lasertag component); gaps GU1 (D333): the DNA-keyed pins, the
// paywall pin with its bank-account license, the explorers' outback pin and the monkeylock pin. The var blocks are generated (D111).

// tg: code/modules/projectiles/pins.dm:1
public partial class obj_item_firing_pin : obj_item
{
    // tg: code/modules/projectiles/pins.dm:25
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(gun))
            gun_remove();
        return base.Destroy(force);
    }

    // tg: code/modules/projectiles/pins.dm:30
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isgun(interacting_with))
            return NONE;

        obj_item_gun targeted_gun = interacting_with as obj_item_gun;
        obj_item_firing_pin old_pin = targeted_gun.pin as obj_item_firing_pin;
        if (Dm.truthy(old_pin?.pin_removable) && (Dm.truthy(force_replace) || Dm.truthy(old_pin.pin_hot_swappable)))
        {
            if (Dm.truthy(await Adjacent(user)))
                await user.put_in_hands(old_pin);
            else
                await old_pin.forceMove(targeted_gun.drop_location() as atom);
            old_pin.gun_remove(user);
        }

        if (targeted_gun.pin == null)
        {
            if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(this)))
                return null;
            if (Dm.truthy(await gun_insert(user, targeted_gun)))
            {
                if (old_pin != null)
                    balloon_alert(user, I18n.T("swapped firing pin"));
                else
                    balloon_alert(user, I18n.T("inserted firing pin"));
            }
        }
        else
        {
            to_chat(user, span_notice(I18n.T("This firearm already has a firing pin installed.")));
        }

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/projectiles/pins.dm:56
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        balloon_alert(user, I18n.T("authentication checks overridden"));
        return TRUE;
    }

    // tg: code/modules/projectiles/pins.dm:63
    public virtual async DmTask<object> gun_insert(mob_living user = null, obj_item_gun new_gun = null, float? starting = null)
    {
        starting ??= FALSE;
        gun = new_gun;
        await forceMove(gun);
        gun.pin = this;
        if (Dm.truthy(gun._listen_lookup?[COMSIG_GUN_PIN_INSERTED]))
            gun._SendSignal(COMSIG_GUN_PIN_INSERTED, Dm.list(gun, this, user, starting));
        return TRUE;
    }

    // tg: code/modules/projectiles/pins.dm:70
    public virtual void gun_remove(mob_living user = null)
    {
        gun.pin = null;
        if (Dm.truthy(gun._listen_lookup?[COMSIG_GUN_PIN_REMOVED]))
            gun._SendSignal(COMSIG_GUN_PIN_REMOVED, Dm.list(gun, this, user));
        gun = null;
        return;
    }

    // tg: code/modules/projectiles/pins.dm:76
    public virtual async DmTask<object> pin_auth(mob_living user = null)
    {
        object result = !Dm.truthy(user._listen_lookup?[COMSIG_LIVING_FIRING_PIN_CHECK]) ? NONE : user._SendSignal(COMSIG_LIVING_FIRING_PIN_CHECK, Dm.list(user, this));
        if (Dm.truthy(Dm.bitand(result, ALLOW_FIRE)))
            return TRUE;
        if (Dm.truthy(Dm.bitand(result, BLOCK_FIRE)))
            return FALSE;
        return default_pin_auth;
    }

    // tg: code/modules/projectiles/pins.dm:84
    public virtual async DmTask auth_fail(mob_living user = null)
    {
        if (user != null)
            balloon_alert(user, I18n.T(fail_message));
        if (Dm.truthy(selfdestruct))
        {
            if (user != null)
            {
                user.show_message(Dm.text("[]<br>", span_danger(I18n.T("SELF-DESTRUCTING..."))), MSG_VISUAL);
                to_chat(user, span_userdanger(I18n.T("[] explodes!", gun)));
            }
            await explosion(this, devastation_range: -1, light_impact_range: 2, flash_range: 3);
            if (gun != null)
                qdel(gun);
        }
    }
}

// Test pin, works only near firing range.
// tg: code/modules/projectiles/pins.dm:102
public partial class obj_item_firing_pin_test_range : obj_item_firing_pin
{
    // tg: code/modules/projectiles/pins.dm:108
    public override async DmTask<object> pin_auth(mob_living user = null)
    {
        if (user == null)
            return FALSE;
        if (istype(get_area(user), typeof(area_station_security_range)))
            return TRUE;
        return FALSE;
    }
}

// Implant pin, checks for implant
// tg: code/modules/projectiles/pins.dm:117
public partial class obj_item_firing_pin_implant : obj_item_firing_pin
{
    // tg: code/modules/projectiles/pins.dm:123
    public override async DmTask<object> pin_auth(mob_living user = null)
    {
        if (user != null)
        {
            foreach (obj_item_implant I in Dm.each<obj_item_implant>(user.implants))
            {
                if (req_implant != null && I.type == req_implant)
                    return TRUE;
            }
        }
        return FALSE;
    }
}

// Honk pin, clown's joke item.
// Can replace other pins. Replace a pin in cap's laser for extra fun!
// tg: code/modules/projectiles/pins.dm:146
public partial class obj_item_firing_pin_clown : obj_item_firing_pin
{
    // tg: code/modules/projectiles/pins.dm:154
    public override async DmTask<object> pin_auth(mob_living user = null)
    {
        playsound(this, Dm.resource("sound/items/bikehorn.ogg"), 50, TRUE);
        return FALSE;
    }
}

// Ultra-honk pin, clown's deadly joke item.
// A gun with ultra-honk pin is useful for clown and useless for everyone else.
// tg: code/modules/projectiles/pins.dm:160
public partial class obj_item_firing_pin_clown_ultra : obj_item_firing_pin_clown
{
    // tg: code/modules/projectiles/pins.dm:163
    public override async DmTask<object> pin_auth(mob_living user = null)
    {
        playsound(loc as atom, Dm.resource("sound/items/bikehorn.ogg"), 50, TRUE);
        if (QDELETED(user))  //how the hell...?
        {
            stack_trace(Dm.text("/obj/item/firing_pin/clown/ultra/pin_auth called with a [] user.", user == null ? "null" : "invalid"), "code/modules/projectiles/pins.dm", 166);
            return TRUE;
        }
        if (HAS_MIND_TRAIT(user, TRAIT_CLUMSY)) // Clumsy, which clowns are usually (but not always)
            return TRUE;
        if (HAS_MIND_TRAIT(user, TRAIT_NAIVE)) // Naive, which clowns are always
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/projectiles/pins.dm:174
    public override async DmTask<object> gun_insert(mob_living user = null, obj_item_gun new_gun = null, float? starting = null)
    {
        object dot = await base.gun_insert(user, new_gun, starting);
        new_gun.clumsy_check = FALSE;
        return dot;
    }

    // tg: code/modules/projectiles/pins.dm:178
    public override void gun_remove(mob_living user = null)
    {
        gun.clumsy_check = Dm.initial(gun, (obj_item_gun p) => p.clumsy_check);
        base.gun_remove(user);
    }
}

// DNA-keyed pin.
// When you want to keep your toys for yourself.
// tg: code/modules/projectiles/pins.dm:191
public partial class obj_item_firing_pin_dna : obj_item_firing_pin
{
    // tg: code/modules/projectiles/pins.dm:198
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (iscarbon(interacting_with))
        {
            mob_living_carbon M = interacting_with as mob_living_carbon;
            if (M.dna != null && Dm.truthy(M.dna.unique_enzymes))
            {
                unique_enzymes = M.dna.unique_enzymes;
                balloon_alert(user, I18n.T("dna lock set"));
                return ITEM_INTERACT_SUCCESS;
            }
            return ITEM_INTERACT_BLOCKING;
        }
        return await base.interact_with_atom(interacting_with, user, modifiers);
    }

    // tg: code/modules/projectiles/pins.dm:208
    public override async DmTask<object> pin_auth(mob_living user = null)
    {
        mob_living_carbon carbon_user = user as mob_living_carbon; // tg types the parameter mob/living/carbon (rules 3.3)
        if (carbon_user != null && carbon_user.dna != null && Dm.truthy(carbon_user.dna.unique_enzymes))
        {
            if (Dm.eq(carbon_user.dna.unique_enzymes, unique_enzymes))
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/projectiles/pins.dm:214
    public override async DmTask auth_fail(mob_living user = null)
    {
        mob_living_carbon carbon_user = user as mob_living_carbon; // tg types the parameter mob/living/carbon (rules 3.3)
        if (!Dm.truthy(unique_enzymes))
        {
            if (carbon_user != null && carbon_user.dna != null && Dm.truthy(carbon_user.dna.unique_enzymes))
            {
                unique_enzymes = carbon_user.dna.unique_enzymes;
                balloon_alert(user, I18n.T("dna lock set"));
            }
        }
        else
        {
            await base.auth_fail(user);
        }
    }
}

// Paywall pin, brought to you by ARMA 3 DLC.
// Checks if the user has a valid bank account on an ID and if so attempts to extract a one-time payment to authorize use of the gun. Otherwise fails to shoot.
// tg: code/modules/projectiles/pins.dm:228
public partial class obj_item_firing_pin_paywall : obj_item_firing_pin
{
    // tg: code/modules/projectiles/pins.dm:244
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        multi_payment = Dm.not(multi_payment);
        to_chat(user, span_notice(I18n.T("You set the pin to [].", Dm.truthy(multi_payment) ? I18n.T("process payment for every shot") : I18n.T("one-time license payment"))));
        return null;
    }

    // tg: code/modules/projectiles/pins.dm:248
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (pin_owner != null)
            Dm.append(ref dot, span_notice(I18n.T("This firing pin is currently authorized to pay into the account of [].", pin_owner.account_holder)));
        return dot;
    }

    // tg: code/modules/projectiles/pins.dm:253
    public override async DmTask<object> gun_insert(mob_living user = null, obj_item_gun new_gun = null, float? starting = null)
    {
        starting ??= FALSE;
        object dot = null;
        if (pin_owner != null || Dm.truthy(starting))
        {
            dot = await base.gun_insert(user, new_gun, starting);
            // MONEY_NAME_AUTOPURAL(payment_amount) (__DEFINES/economy.dm:99), inline (rules 11.1)
            gun.desc += span_notice(I18n.T("This [] has a [] cost of [] [].", gun.name, Dm.truthy(multi_payment) ? I18n.T("per-shot") : I18n.T("license permit"), payment_amount, Dm.text("credit[]", Dm.eq(payment_amount, 1) ? "" : "s")));
            return dot;
        }

        if (user == null)
            await forceMove(new_gun.drop_location() as atom);
        else
        {
            to_chat(user, span_warning(I18n.T("ERROR: Please swipe valid identification card before installing firing pin!")));
            await user.put_in_hands(this);
        }
        return FALSE;
    }

    // tg: code/modules/projectiles/pins.dm:266
    public override void gun_remove(mob_living user = null)
    {
        gun.desc = Dm.initial(gun, (obj_item_gun p) => p.desc);
        base.gun_remove(user);
    }

    // tg: code/modules/projectiles/pins.dm:270
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!isidcard(tool))
            return NONE;
        obj_item_card_id id = tool as obj_item_card_id;
        if (id.registered_account == null)
        {
            to_chat(user, span_warning(I18n.T("ERROR: Identification card lacks registered bank account!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (pin_owner != null && id.registered_account != pin_owner)
        {
            to_chat(user, span_warning(I18n.T("ERROR: This firing pin has already been authorized!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (id.registered_account == pin_owner)
        {
            to_chat(user, span_notice(I18n.T("You unlink the card from the firing pin.")));
            Dm.remove(ref gun_owners, user.get_bank_account());
            pin_owner = null;
            return ITEM_INTERACT_SUCCESS;
        }
        object transaction_amount = await tgui_input_number(user, I18n.T("Insert valid deposit amount for gun purchase"), I18n.T("Money Deposit"));
        if (!Dm.truthy(transaction_amount) || QDELETED(user) || QDELETED(this) || !Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return ITEM_INTERACT_BLOCKING;
        pin_owner = id.registered_account;
        payment_amount = transaction_amount;
        Dm.append(ref gun_owners, user.get_bank_account());
        to_chat(user, span_notice(I18n.T("You link the card to the firing pin.")));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/projectiles/pins.dm:294
    public override async DmTask<object> pin_auth(mob_living user = null)
    {
        if (!(user is mob_living)) //nice try commie
            return FALSE;
        datum_bank_account credit_card_details = user.get_bank_account() as datum_bank_account;
        if (Dm.@in(credit_card_details, gun_owners))
        {
            if (Dm.truthy(multi_payment) && credit_card_details != null)
            {
                if (!Dm.truthy(gun.can_shoot()))
                    return TRUE; //So you don't get charged for attempting to fire an empty gun.
                if (Dm.truthy(credit_card_details.adjust_money(-Dm.num(payment_amount), I18n.T("Firing Pin: Gun Rent"))))
                {
                    if (pin_owner != null)
                        pin_owner.adjust_money(payment_amount, I18n.T("Firing Pin: Payout For Gun Rent"));
                    return TRUE;
                }
                to_chat(user, span_warning(I18n.T("ERROR: User balance insufficent for successful transaction!")));
                return FALSE;
            }
            return TRUE;
        }
        if (credit_card_details == null)
        {
            to_chat(user, span_warning(I18n.T("ERROR: User has no valid bank account to subtract neccesary funds from!")));
            return FALSE;
        }
        if (active_prompt_user == user)
            return FALSE;
        active_prompt_user = user;
        // MONEY_NAME_AUTOPURAL(payment_amount) (__DEFINES/economy.dm:99), inline (rules 11.1)
        object license_request = await tgui_alert(user, I18n.T("Do you wish to pay [] [] for []?", payment_amount, Dm.text("credit[]", Dm.eq(payment_amount, 1) ? "" : "s"), Dm.truthy(multi_payment) ? I18n.T("each shot of []", gun.name) : I18n.T("usage license of []", gun.name)), I18n.T("Weapon Purchase"), Dm.list("Yes", "No"), 15 * SECONDS);
        if (!Dm.truthy(await user.can_perform_action(this)))
        {
            active_prompt_user = null;
            return FALSE;
        }
        object sw__1 = license_request;
        if (Dm.eq(sw__1, "Yes"))
        {
            if (Dm.truthy(multi_payment))
            {
                Dm.append(ref gun_owners, credit_card_details);
                to_chat(user, span_notice(I18n.T("Gun rental terms agreed to, have a secure day!")));
            }
            else if (Dm.truthy(credit_card_details.adjust_money(-Dm.num(payment_amount), I18n.T("Firing Pin: Gun License"))))
            {
                if (pin_owner != null)
                    pin_owner.adjust_money(payment_amount, I18n.T("Firing Pin: Gun License Bought"));
                Dm.append(ref gun_owners, credit_card_details);
                to_chat(user, span_notice(I18n.T("Gun license purchased, have a secure day!")));
            }
            else
            {
                to_chat(user, span_warning(I18n.T("ERROR: User balance insufficent for successful transaction!")));
            }
        }
        else if (Dm.eq(sw__1, "No") || sw__1 == null)
        {
            to_chat(user, span_warning(I18n.T("ERROR: User has declined to purchase gun license!")));
        }

        active_prompt_user = null;
        return FALSE; //we return false here so you don't click initially to fire, get the prompt, accept the prompt, and THEN the gun
    }
}

// Explorer Firing Pin- Prevents use on station Z-Level, so it's justifiable to give Explorers guns that don't suck.
// tg: code/modules/projectiles/pins.dm:340
public partial class obj_item_firing_pin_explorer : obj_item_firing_pin
{
    // This checks that the user isn't on the station Z-level.
    // tg: code/modules/projectiles/pins.dm:348
    public override async DmTask<object> pin_auth(mob_living user = null)
    {
        turf station_check = get_turf(user);
        if (station_check == null || Dm.truthy(is_station_level(station_check.z)))
            return FALSE;
        return TRUE;
    }
}

// Laser tag pins
// tg: code/modules/projectiles/pins.dm:355
public partial class obj_item_firing_pin_tag : obj_item_firing_pin
{
    // tg: code/modules/projectiles/pins.dm:362
    public override async DmTask auth_fail(mob_living user = null)
    {
        await base.auth_fail(user);
        to_chat(user, span_warning(I18n.T("You need to be wearing [] laser tag armor!", tagcolor)));
    }
}

// tg: code/modules/projectiles/pins.dm:376
public partial class obj_item_firing_pin_monkey : obj_item_firing_pin
{
    // tg: code/modules/projectiles/pins.dm:381
    public override async DmTask<object> pin_auth(mob_living user = null)
    {
        if (!HAS_TRAIT(user, TRAIT_SIMIAN))
        {
            playsound(this, SFX_SCREECH, 75, TRUE);
            return FALSE;
        }
        return TRUE;
    }
}
