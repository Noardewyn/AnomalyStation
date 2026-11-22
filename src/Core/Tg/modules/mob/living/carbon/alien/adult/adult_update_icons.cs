// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/adult/adult_update_icons.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file, the adult castes' icon states (alienX, _dead, _husked, _unconscious, _pounce, _sleep, _leap, the
// spit overlay, the queen sheet's inhands). The emissive blockers are not made (DV12, as the carbons').

// tg: code/modules/mob/living/carbon/alien/adult/adult_update_icons.dm:2
public partial class mob_living_carbon_alien_adult : mob_living_carbon_alien
{
    // tg: code/modules/mob/living/carbon/alien/adult/adult_update_icons.dm:2
    public override async DmTask update_icons()
    {
        cut_overlays();
        foreach (object I in Dm.each(overlays_standing))
            add_overlay(I);
        foreach (object I in Dm.each(managed_overlays))
            add_overlay(I);

        bool are_we_drooling = click_intercept is datum_action_cooldown_alien_acid;

        if (stat == DEAD)
        {
            //If we mostly took damage from fire
            if (Dm.num(get_fire_loss()) > 125)
                icon_state = Dm.text("alien[]_husked", caste);
            else
                icon_state = Dm.text("alien[]_dead", caste);
        }

        else if (IS_UNCONSCIOUS_OR_CRIT(this) || Dm.truthy(IsParalyzed()))
            icon_state = Dm.text("alien[]_unconscious", caste);
        else if (Dm.truthy(leap_on_click))
            icon_state = Dm.text("alien[]_pounce", caste);

        else if (body_position == LYING_DOWN)
            icon_state = Dm.text("alien[]_sleep", caste);
        else if (mob_size == MOB_SIZE_LARGE)
        {
            icon_state = Dm.text("alien[]", caste);
            if (are_we_drooling)
                add_overlay(Dm.text("alienspit_[]", caste));
        }
        else
        {
            icon_state = Dm.text("alien[]", caste);
            if (are_we_drooling)
                add_overlay("alienspit");
        }

        if (Dm.truthy(leaping))
        {
            if (Dm.eq(alt_icon, Dm.initial(this, (mob_living_carbon_alien_adult p) => p.alt_icon)))
            {
                object old_icon = icon;
                icon = alt_icon;
                alt_icon = old_icon;
            }
            icon_state = Dm.text("alien[]_leap", caste);
        }
        else
        {
            if (!Dm.eq(alt_icon, Dm.initial(this, (mob_living_carbon_alien_adult p) => p.alt_icon)))
            {
                object old_icon = icon;
                icon = alt_icon;
                alt_icon = old_icon;
            }
        }
        await update_held_items();
        update_worn_handcuffs();
    }

    // tg: code/modules/mob/living/carbon/alien/adult/adult_update_icons.dm:48
    public override async DmTask<object> regenerate_icons()
    {
        if (!Dm.truthy(await base.regenerate_icons()))
        // update_icons() //Handled in update_transform(), leaving this here as a reminder
            await update_transform();
        return null;
    }

    // tg: code/modules/mob/living/carbon/alien/adult/adult_update_icons.dm:53
    public override async DmTask<object> update_transform(float? resize = null) //The old method of updating lying/standing was update_icons(). Aliens still expect that.
    {
        object dot = await base.update_transform(resize);
        await update_icons();
        return dot;
    }

    // tg: code/modules/mob/living/carbon/alien/adult/adult_update_icons.dm:57
    public override void update_worn_handcuffs()
    {
        remove_overlay(HANDCUFF_LAYER);
        string cuff_icon = "aliencuff";
        object dmi_file = Dm.resource("icons/mob/nonhuman-player/alien.dmi");

        if (mob_size == MOB_SIZE_LARGE)
        {
            cuff_icon = Dm.text("aliencuff_[]", caste);
            dmi_file = Dm.resource("icons/mob/nonhuman-player/alienqueen.dmi");
        }

        if (Dm.truthy(handcuffed))
        {
            mutable_appearance handcuff_overlay = mutable_appearance(dmi_file, cuff_icon, -HANDCUFF_LAYER) as mutable_appearance;
            if (handcuffed.blocks_emissive != EMISSIVE_BLOCK_NONE)
            {
                // DEVIATION(DV12): emissive blockers are not created (lighting planes are not ported)
                Dm.Deferred("code/modules/mob/living/carbon/alien/adult/adult_update_icons.dm:68", "update_worn_handcuffs: the emissive blocker of the alien's handcuff overlay (DV12)");
            }

            overlays_standing[HANDCUFF_LAYER] = handcuff_overlay;
            apply_overlay(HANDCUFF_LAYER);
        }
    }
    // AND HERE MOTHERFUCKER AHHHHHH
}

//Royals have bigger sprites, so inhand things must be handled differently.
// tg: code/modules/mob/living/carbon/alien/adult/adult_update_icons.dm:76
public partial class mob_living_carbon_alien_adult_royal : mob_living_carbon_alien_adult
{
    // tg: code/modules/mob/living/carbon/alien/adult/adult_update_icons.dm:76
    public override async DmTask update_held_items()
    {
        await base.update_held_items();
        remove_overlay(HANDS_LAYER);
        DmList hands = Dm.list();
        obj_item l_hand = get_item_for_held_index(1) as obj_item;
        if (l_hand != null)
        {
            object itm_state = l_hand.inhand_icon_state;
            if (!Dm.truthy(itm_state))
                itm_state = l_hand.icon_state;
            mutable_appearance l_hand_item = mutable_appearance(alt_inhands_file, Dm.text("[][]_l", itm_state, caste), -HANDS_LAYER) as mutable_appearance;
            if (l_hand.blocks_emissive != EMISSIVE_BLOCK_NONE)
                Dm.Deferred("code/modules/mob/living/carbon/alien/adult/adult_update_icons.dm:87", "update_held_items: the emissive blocker of the royal's left inhand (DV12)");
            Dm.append(ref hands, l_hand_item);
        }

        obj_item r_hand = get_item_for_held_index(2) as obj_item;
        if (r_hand != null)
        {
            object itm_state = r_hand.inhand_icon_state;
            if (!Dm.truthy(itm_state))
                itm_state = r_hand.icon_state;
            mutable_appearance r_hand_item = mutable_appearance(alt_inhands_file, Dm.text("[][]_r", itm_state, caste), -HANDS_LAYER) as mutable_appearance;
            if (r_hand.blocks_emissive != EMISSIVE_BLOCK_NONE)
                Dm.Deferred("code/modules/mob/living/carbon/alien/adult/adult_update_icons.dm:97", "update_held_items: the emissive blocker of the royal's right inhand (DV12)");
            Dm.append(ref hands, r_hand_item);
        }

        overlays_standing[HANDS_LAYER] = hands;
        apply_overlay(HANDS_LAYER);
    }
}
