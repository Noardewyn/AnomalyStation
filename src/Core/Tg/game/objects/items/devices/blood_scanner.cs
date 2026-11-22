// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/blood_scanner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DV2 (D333): the file whole. The hemoanalytic scanner: two seconds of a needle in the patient's limb read their blood type and
// warn of hypoxia, low blood, toxins, eigenstasium, the Midas hexes, the penthrite revival, high blood pressure and immunodeficiency
// (the left click), or list the medicines in their blood (the right click); a patient who moves off the needle tears a pinprick
// wound a quarter of the time. The var block is generated (D111).

///Pricks people with a big needle and gives the user a bunch of info about bloodtype, tox damage, other such stuff. if you fuck up it stabs the guy a bit.
// tg: code/game/objects/items/devices/blood_scanner.dm:2
public partial class obj_item_blood_scanner : obj_item
{
    // tg: code/game/objects/items/devices/blood_scanner.dm:20
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(usable_check(person_scanning: user, scanee: interacting_with)))
            return NONE;
        mob_living_carbon poked_guy = (mob_living_carbon)interacting_with;
        obj_item_bodypart poked_bit = poked_guy.get_bodypart(check_zone(user.zone_selected) as string) as obj_item_bodypart;
        if (!Dm.truthy(poked_bit))
            return null;
        user.visible_message(span_notice(I18n.T("[] pricks [] with [].", user, poked_guy, this)), span_notice(I18n.T("You begin scanning [] with [].", poked_guy, this)), ignored_mobs: poked_guy);
        to_chat(poked_guy, span_notice(I18n.T("[] slides the needle of [] into your [] and begins pressing buttons.", user, this, poked_bit)));
        object success = await do_after(user, 2 * SECONDS, poked_guy);
        if (Dm.truthy(success))
            scan_blood(scanner: user, scanned_person: poked_guy);
        else
        {
            if (prob(25))
                await regret(stabber: user, stabbed: poked_guy, to_stab: poked_bit);
            return null;
        }

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/devices/blood_scanner.dm:39
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(usable_check(person_scanning: user, scanee: interacting_with)))
            return NONE;
        mob_living_carbon poked_guy = (mob_living_carbon)interacting_with;
        obj_item_bodypart poked_bit = poked_guy.get_bodypart(check_zone(user.zone_selected) as string) as obj_item_bodypart;
        user.visible_message(span_notice(I18n.T("[] pricks [] with [].", user, poked_guy, this)), span_notice(I18n.T("You begin scanning [] with [].", poked_guy, this)), ignored_mobs: poked_guy);
        to_chat(poked_guy, span_notice(I18n.T("[] slides the needle of [] into your [] and begins pressing buttons.", user, this, poked_bit)));
        object success = await do_after(user, 2 * SECONDS, poked_guy);
        if (Dm.truthy(success))
            chemscan(user, poked_guy, reagent_types_to_check: typeof(datum_reagent_medicine));
        else
        {
            if (prob(25))
                await regret(stabber: user, stabbed: poked_guy, to_stab: poked_bit);
            return null;
        }

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/devices/blood_scanner.dm:57
    public virtual void scan_blood(mob_living scanner = null, mob_living_carbon scanned_person = null)
    {
        object render_list = Dm.list();
        object oxy_loss = scanned_person.get_oxy_loss();
        object tox_loss = scanned_person.get_tox_loss();
        Dm.append(ref render_list, span_info(I18n.T("You read the []'s screen:\n", this)));
        Dm.append(ref render_list, I18n.T("<span class='notice ml-1'>Blood Type: []</span>\n", scanned_person?.dna?.blood_type));
        if (Dm.num(oxy_loss) > 50)//if they have knockout levels of suffocation damage
            Dm.append(ref render_list, I18n.T("<span class='danger ml-1'>Warning: Hypoxic blood oxygen levels.</span>\n"));
        if (Dm.num(scanned_person.get_blood_volume(apply_modifiers: TRUE)) <= BLOOD_VOLUME_SAFE)
            Dm.append(ref render_list, I18n.T("<span class='danger ml-1'>Warning: Dangerously low blood flow.</span>\n"));
        if (Dm.num(tox_loss) > 10)
            Dm.append(ref render_list, I18n.T("<span class='danger ml-1'>Warning: Toxic buildup detected in bloodstream.</span>\n"));
        if (Dm.truthy(scanned_person.has_status_effect(typeof(datum_status_effect_eigenstasium))))
            Dm.append(ref render_list, I18n.T("<span class='danger ml-1'>Warning: Dimensional instability detected. Administer stabilizers.</span>\n"));
        if (Dm.truthy(scanned_person.has_reagent(typeof(datum_reagent_gold_cursed))) || Dm.truthy(scanned_person.has_status_effect(typeof(datum_status_effect_midas_blight))))
            Dm.append(ref render_list, I18n.T("<span class='danger ml-1'>Warning: Hemo-aurificating hexes present.</span>\n")); //can it detect normal things? barely. But ancient greed-cursed magicks? Spot on.
        if (HAS_TRAIT_FROM(scanned_person, TRAIT_NODEATH, typeof(datum_reagent_inverse_penthrite)))
            Dm.append(ref render_list, I18n.T("<span class='danger ml-1'>Warning: Frankensteinian revivification in progress.</span>\n")); //patient is currently an immortal drug-zombie
        if (Dm.truthy(scanned_person.has_status_effect(typeof(datum_status_effect_high_blood_pressure))))
            Dm.append(ref render_list, I18n.T("<span class='danger ml-1'>Warning: Dangerously high blood pressure.</span>\n"));
        if (HAS_TRAIT(scanned_person, TRAIT_IMMUNODEFICIENCY))
            Dm.append(ref render_list, I18n.T("<span class='danger ml-1'>Warning: Low immune cell concentration.</span>\n")); //it doe
        to_chat(scanner, boxed_message(Dm.jointext(render_list, "")), type: MESSAGE_TYPE_INFO);
    }

    // tg: code/game/objects/items/devices/blood_scanner.dm:81
    public virtual async DmTask regret(mob_living stabber = null, mob_living stabbed = null, obj_item_bodypart to_stab = null)
    {
        if (to_stab != null)
            await to_stab.force_wound_upwards(typeof(datum_wound_pierce_bleed_moderate_needle_fail), wound_source: I18n.T("idiot moved with a needle in them"));
        stabber.visible_message(span_warning(I18n.T("[]'s needle is ripped out, tearing a hole in []'s []!", this, stabbed, to_stab)), span_warning(I18n.T("Damnit! The needle is torn out, making a tiny hole in [] [].", stabbed.p_their(), to_stab)), ignored_mobs: stabbed);
        to_chat(stabbed, span_userdanger(I18n.T("<b>OWWW!</b> The needle of [] is ripped out, tearing a small hole in your []!", this, to_stab)));
    }

    // tg: code/game/objects/items/devices/blood_scanner.dm:86
    public virtual object usable_check(mob_living person_scanning = null, atom scanee = null)
    {
        if (!isliving(scanee))
            return FALSE;
        if (scanee is not mob_living_carbon)
        {
            to_chat(person_scanning, span_warning(I18n.T("[] is incompatible.", this)));
            return FALSE;
        }
        if (!Dm.truthy(person_scanning.can_read(this)) || Dm.truthy(person_scanning.is_blind()))
        {
            to_chat(person_scanning, span_warning(I18n.T("You are unable to read []'s screen!", this)));
            return FALSE;
        }
        return TRUE;
    }
}
