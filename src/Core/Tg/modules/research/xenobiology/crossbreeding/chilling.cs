// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/crossbreeding/chilling.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the chilling extracts, filled with 10u of plasma and squeezed in hand for a defensive effect of their colour. The
// golems are not ported (not-translated.md section 243): the black extract crystallizes over its user and leaves the body as it is.
// The var blocks are generated (D111).

/*
Chilling extracts:
	Have a unique, primarily defensive effect when
	filled with 10u plasma and activated in-hand.
*/
// tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:7
public partial class obj_item_slimecross_chilling : obj_item_slimecross
{
    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:12
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(10, Dm.bitor(INJECTABLE, DRAWABLE));
        return dot;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:16
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        datum_reagents holder = (datum_reagents)reagents;
        if (!Dm.truthy(holder.has_reagent(typeof(datum_reagent_toxin_plasma), 10)))
        {
            to_chat(user, span_warning(I18n.T("This extract needs to be full of plasma to activate!")));
            return null;
        }
        await holder.remove_reagent(typeof(datum_reagent_toxin_plasma), 10);
        to_chat(user, span_notice(I18n.T("You squeeze the extract, and it absorbs the plasma!")));
        playsound(this, "sound/effects/bubbles/bubbles.ogg", 50, TRUE);
        playsound(this, "sound/effects/glass/glassbr1.ogg", 50, TRUE);
        await do_effect(user);
        return null;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:26
    public virtual async DmTask do_effect(mob user = null) //If, for whatever reason, you don't want to delete the extract, don't do ..()
    {
        await DmTask.CompletedTask;
        qdel(this);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:31
public partial class obj_item_slimecross_chilling_grey : obj_item_slimecross_chilling
{
    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:34
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_notice(I18n.T("[] produces a few small, grey cubes", this)));
        for (int i = 1; i <= 3; i++)
            Dm.@new<obj_item_barriercube>(get_turf(user));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:41
public partial class obj_item_slimecross_chilling_orange : obj_item_slimecross_chilling
{
    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:44
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_danger(I18n.T("[] shatters, and lets out a jet of heat!", this)));
        foreach (turf T in Dm.each<turf>(orange(get_turf(user), 2)))
        {
            if (get_dist(get_turf(user), T) > 1)
                Dm.@new<obj_effect_hotspot>(T);
        }
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:52
public partial class obj_item_slimecross_chilling_purple : obj_item_slimecross_chilling
{
    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:55
    public override async DmTask do_effect(mob user = null)
    {
        area user_area = get_area(user) as area;
        if (Dm.truthy(user_area.outdoors))
        {
            to_chat(user, span_warning(I18n.T("[] can't affect such a large area.", this)));
            return;
        }
        user.visible_message(span_notice(I18n.T("[] shatters, and a healing aura fills the room briefly.", this)));
        foreach (DmList zlevel_turfs in Dm.each_cast<DmList>(user_area.get_zlevel_turf_lists()))
        {
            foreach (turf area_turf in Dm.each_cast<turf>(zlevel_turfs))
            {
                foreach (mob_living_carbon nearby in Dm.each<mob_living_carbon>(area_turf))
                {
                    if (nearby.reagents is datum_reagents holder)
                        await holder.add_reagent(typeof(datum_reagent_medicine_regen_jelly), 10);
                }
            }
        }
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:68
public partial class obj_item_slimecross_chilling_blue : obj_item_slimecross_chilling
{
    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:71
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_notice(I18n.T("[] cracks, and spills out a liquid goo, which reforms into a mask!", this)));
        Dm.@new<obj_item_clothing_mask_nobreath>(get_turf(user));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:77
public partial class obj_item_slimecross_chilling_metal : obj_item_slimecross_chilling
{
    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:80
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_danger(I18n.T("[] melts like quicksilver, and surrounds [] in a wall!", this, user)));
        foreach (turf T in Dm.each<turf>(orange(get_turf(user), 1)))
        {
            if (get_dist(get_turf(user), T) > 0)
                Dm.@new<obj_effect_forcefield_slimewall>(T);
        }
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:88
public partial class obj_item_slimecross_chilling_yellow : obj_item_slimecross_chilling
{
    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:91
    public override async DmTask do_effect(mob user = null)
    {
        area user_area = get_area(user) as area;
        if (user_area.apc?.cell == null)
        {
            user.visible_message(span_notice(I18n.T("[] shatters, yet the air around you feels normal.", this)));
            return;
        }

        obj_machinery_power_apc area_apc = user_area.apc;
        area_apc.cell.charge = Math.Min(Dm.num(area_apc.cell.charge) + Dm.num(area_apc.cell.maxcharge) / 2, Dm.num(area_apc.cell.maxcharge));
        user.visible_message(span_notice(I18n.T("[] shatters, and the air suddenly feels charged for a moment.", this)));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:103
public partial class obj_item_slimecross_chilling_darkpurple : obj_item_slimecross_chilling
{
    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:106
    public override async DmTask do_effect(mob user = null)
    {
        area A = get_area(get_turf(user)) as area;
        if (Dm.truthy(A.outdoors))
        {
            to_chat(user, span_warning(I18n.T("[] can't affect such a large area.", this)));
            return;
        }
        bool filtered = false;
        foreach (turf_open T in Dm.each<turf_open>(A.get_turfs_from_all_zlevels()))
        {
            datum_gas_mixture G = T.air as datum_gas_mixture;
            if (G is datum_gas_mixture)
            {
                G.assert_gas(typeof(datum_gas_plasma));
                G.moles[typeof(datum_gas_plasma)] = 0;
                filtered = true;
                G.garbage_collect();
                T.air_update_turf(FALSE, FALSE);
            }
        }
        if (filtered)
            user.visible_message(span_notice(I18n.T("Cracks spread throughout [], and some air is sucked in!", this)));
        else
            user.visible_message(span_notice(I18n.T("[] cracks, but nothing happens.", this)));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:127
public partial class obj_item_slimecross_chilling_darkblue : obj_item_slimecross_chilling
{
    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:130
    public override async DmTask do_effect(mob user = null)
    {
        if (isliving(user))
        {
            user.visible_message(span_notice(I18n.T("[] freezes over []'s entire body!", this, user)));
            mob_living M = (mob_living)user;
            await M.apply_status_effect(typeof(datum_status_effect_frozenstasis));
        }
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:138
public partial class obj_item_slimecross_chilling_silver : obj_item_slimecross_chilling
{
    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:141
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_notice(I18n.T("[] crumbles into icy powder, leaving behind several emergency food supplies!", this)));
        float amount = rand(5, 10);
        for (int i = 1; i <= amount; i++)
            Dm.@new<obj_item_food_rationpack>(get_turf(user));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:149
public partial class obj_item_slimecross_chilling_bluespace : obj_item_slimecross_chilling
{
    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:154
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isliving(interacting_with) || Dm.truthy(active))
            return NONE;
        await user.do_attack_animation(interacting_with);
        if (HAS_TRAIT(interacting_with, TRAIT_NO_TELEPORT))
        {
            to_chat(user, span_warning(I18n.T("[] resists being linked with []!", interacting_with, this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.@in(interacting_with, slimepals))
        {
            Dm.remove(ref slimepals, interacting_with);
            to_chat(user, span_notice(I18n.T("You unlink [] with [].", this, interacting_with)));
        }
        else
        {
            Dm.append(ref slimepals, interacting_with);
            to_chat(user, span_notice(I18n.T("You link [] with [].", this, interacting_with)));
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:169
    public override async DmTask do_effect(mob user = null)
    {
        if (slimepals.len <= 0)
        {
            to_chat(user, span_warning(I18n.T("[] is not linked to anyone!", this)));
            return;
        }
        to_chat(user, span_notice(I18n.T("You feel [] pulse as it begins charging bluespace energies...", this)));
        active = TRUE;
        foreach (mob_living M in Dm.each<mob_living>(slimepals))
        {
            datum_status_effect_slimerecall S = await M.apply_status_effect(typeof(datum_status_effect_slimerecall)) as datum_status_effect_slimerecall;
            S.target = user;
        }
        if (Dm.truthy(await do_after(user, 10 * SECONDS, target: this)))
        {
            to_chat(user, span_notice(I18n.T("[] shatters as it tears a hole in reality, snatching the linked individuals from the void!", this)));
            foreach (mob_living M in Dm.each<mob_living>(slimepals))
            {
                datum_status_effect_slimerecall S = M.has_status_effect(typeof(datum_status_effect_slimerecall)) as datum_status_effect_slimerecall;
                M.remove_status_effect(S);
            }
        }
        else
        {
            to_chat(user, span_warning(I18n.T("[] falls dark, dissolving into nothing as the energies fade away.", this)));
            foreach (mob_living M in Dm.each<mob_living>(slimepals))
            {
                datum_status_effect_slimerecall S = M.has_status_effect(typeof(datum_status_effect_slimerecall)) as datum_status_effect_slimerecall;
                if (S is datum_status_effect_slimerecall)
                {
                    S.interrupted = TRUE;
                    M.remove_status_effect(S);
                }
            }
        }
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:193
public partial class obj_item_slimecross_chilling_sepia : obj_item_slimecross_chilling
{
    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:197
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isliving(interacting_with))
            return NONE;
        await user.do_attack_animation(interacting_with);
        if (Dm.@in(interacting_with, slimepals))
        {
            Dm.remove(ref slimepals, interacting_with);
            to_chat(user, span_notice(I18n.T("You unlink [] with [].", this, interacting_with)));
        }
        else
        {
            Dm.append(ref slimepals, interacting_with);
            to_chat(user, span_notice(I18n.T("You link [] with [].", this, interacting_with)));
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:209
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_warning(I18n.T("[] shatters, freezing time itself!", this)));
        Dm.remove(ref slimepals, user); //support class
        Dm.@new<obj_effect_timestop>(get_turf(user), 2, 300, slimepals);
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:216
public partial class obj_item_slimecross_chilling_cerulean : obj_item_slimecross_chilling
{
    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:219
    public override async DmTask do_effect(mob user = null)
    {
        if (isliving(user))
        {
            user.visible_message(span_warning(I18n.T("[] creaks and shifts into a clone of []!", this, user)));
            mob_living M = (mob_living)user;
            await M.apply_status_effect(typeof(datum_status_effect_slime_clone));
        }
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:227
public partial class obj_item_slimecross_chilling_pyrite : obj_item_slimecross_chilling
{
    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:230
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_notice(I18n.T("[] crystallizes into a pair of spectacles!", this)));
        Dm.@new<obj_item_clothing_glasses_prism_glasses>(get_turf(user));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:236
public partial class obj_item_slimecross_chilling_red : obj_item_slimecross_chilling
{
    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:239
    public override async DmTask do_effect(mob user = null)
    {
        bool slimesfound = false;
        foreach (mob_living_basic_slime slime_in_view in Dm.each<mob_living_basic_slime>(view(get_turf(user), 7)))
        {
            slimesfound = true;
            slime_in_view.set_pacified_behaviour();
        }
        if (slimesfound)
            user.visible_message(span_notice(I18n.T("[] lets out a peaceful ring as it shatters, and nearby slimes seem calm.", this)));
        else
            user.visible_message(span_notice(I18n.T("[] lets out a peaceful ring as it shatters, but nothing happens...", this)));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:251
public partial class obj_item_slimecross_chilling_green : obj_item_slimecross_chilling
{
    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:254
    public override async DmTask do_effect(mob user = null)
    {
        mob_living L = user as mob_living;
        if (!(user is mob_living))
            return;
        obj_item held = L.get_active_held_item() as obj_item; //This should be itself, but just in case...
        await L.dropItemToGround(held);
        obj_item_gun_magic_bloodchill gun = Dm.@new<obj_item_gun_magic_bloodchill>(user);
        if (!Dm.truthy(await L.put_in_hands(gun)))
        {
            qdel(gun);
            user.visible_message(span_warning(I18n.T("[] flash-freezes []'s arm, cracking the flesh horribly!", this, user)));
        }
        else
            user.visible_message(span_danger(I18n.T("[] chills and snaps off the front of the bone on []'s arm, leaving behind a strange, gun-like structure!", this, user)));
        await user.emote("scream");
        await L.apply_damage(30, BURN, L.get_active_hand());
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:271
public partial class obj_item_slimecross_chilling_pink : obj_item_slimecross_chilling
{
    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:274
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_notice(I18n.T("[] cracks like an egg, and an adorable puppy comes tumbling out!", this)));
        Dm.@new<mob_living_basic_pet_dog_corgi_puppy_slime>(get_turf(user));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:280
public partial class obj_item_slimecross_chilling_gold : obj_item_slimecross_chilling
{
    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:283
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_notice(I18n.T("[] lets off golden light as it melts and reforms into an egg-like device!", this)));
        Dm.@new<obj_item_capturedevice>(get_turf(user));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:289
public partial class obj_item_slimecross_chilling_oil : obj_item_slimecross_chilling
{
    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:292
    public override async DmTask do_effect(mob user = null)
    {
        await DmTask.CompletedTask;
        user.visible_message(span_danger(I18n.T("[] begins to shake with muted intensity!", this)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(boom)), 5 * SECONDS, file: "code/modules/research/xenobiology/crossbreeding/chilling.dm", line: 294);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:296
    public virtual async DmTask boom()
    {
        await explosion(this, devastation_range: -1, heavy_impact_range: -1, light_impact_range: 10, explosion_cause: this); //Large radius, but mostly light damage, and no flash.
        qdel(this);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:301
public partial class obj_item_slimecross_chilling_black : obj_item_slimecross_chilling
{
    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:304
    public override async DmTask do_effect(mob user = null)
    {
        if (ishuman(user))
        {
            user.visible_message(span_notice(I18n.T("[] crystallizes along []'s skin, turning into solid stone!", this, user)));
            mob_living_carbon_human H = (mob_living_carbon_human)user;
            // GAP(SP3): the golems are inert (not-translated.md section 243): tg turns the user into one, the port leaves the body as it
            // is until the golems are ported.
            if (!InertSeam.IsInertType(typeof(datum_species_golem)))
                H.set_species(typeof(datum_species_golem));
        }
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:312
public partial class obj_item_slimecross_chilling_lightpink : obj_item_slimecross_chilling
{
    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:315
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_notice(I18n.T("[] blooms into a beautiful flower!", this)));
        Dm.@new<obj_item_clothing_head_peaceflower>(get_turf(user));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:321
public partial class obj_item_slimecross_chilling_adamantine : obj_item_slimecross_chilling
{
    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:324
    public override async DmTask do_effect(mob user = null)
    {
        user.visible_message(span_notice(I18n.T("[] creaks and breaks as it shifts into a heavy set of armor!", this)));
        Dm.@new<obj_item_clothing_suit_armor_heavy_adamantine>(get_turf(user));
        await base.do_effect(user);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:330
public partial class obj_item_slimecross_chilling_rainbow : obj_item_slimecross_chilling
{
    // tg: code/modules/research/xenobiology/crossbreeding/chilling.dm:333
    public override async DmTask do_effect(mob user = null)
    {
        area area = get_area(user) as area;
        if (Dm.truthy(area.outdoors))
        {
            to_chat(user, span_warning(I18n.T("[] can't affect such a large area.", this)));
            return;
        }
        user.visible_message(span_warning(I18n.T("[] reflects an array of dazzling colors and light, energy rushing to nearby doors!", this)));
        foreach (DmList zlevel_turfs in Dm.each_cast<DmList>(area.get_zlevel_turf_lists()))
        {
            foreach (turf area_turf in Dm.each_cast<turf>(zlevel_turfs))
            {
                foreach (obj_machinery_door_airlock door in Dm.each<obj_machinery_door_airlock>(area_turf))
                    Dm.@new<obj_effect_forcefield_slimewall_rainbow>(door.loc);
            }
        }
        await base.do_effect(user);
    }
}
