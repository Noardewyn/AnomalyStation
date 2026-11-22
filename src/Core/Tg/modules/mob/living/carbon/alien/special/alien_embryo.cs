// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/special/alien_embryo.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file, the embryo a facehugger leaves: its six stages, the host's symptoms, the ghosts' poll for the
// larva and the burst (the host's burst_lie overlay, the larva hidden for the 0.6 s of the animation). The infection images go to
// the aliens' client.images (the client draws them over the host). The var block is generated (D111).

// This is to replace the previous datum/disease/alien_embryo for slightly improved handling and maintainability
// It functions almost identically (see code/datums/diseases/alien_embryo.dm)
// tg: code/modules/mob/living/carbon/alien/special/alien_embryo.dm:3
public partial class obj_item_organ_body_egg_alien_embryo : obj_item_organ_body_egg
{
    // tg: code/modules/mob/living/carbon/alien/special/alien_embryo.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        advance_embryo_stage();
        return dot;
    }

    // tg: code/modules/mob/living/carbon/alien/special/alien_embryo.dm:19
    public override async DmTask on_find(mob_living finder = null)
    {
        await base.on_find(finder);
        if (stage < 5)
            to_chat(finder, span_notice(I18n.T("It's small and weak, barely the size of a foetus.")));
        else
        {
            to_chat(finder, span_notice(I18n.T("It's grown quite large, and writhes slightly as you look at it.")));
            if (prob(10))
                Waiting.Detach(attempt_grow(gib_on_success: FALSE));
        }
    }

    // tg: code/modules/mob/living/carbon/alien/special/alien_embryo.dm:28
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);
        if (QDELETED(this) || QDELETED(owner))
            return;

        switch (stage)
        {
            case 3 or 4:
                if (SPT_PROB(1, seconds_per_tick))
                    await owner.emote("sneeze");
                if (SPT_PROB(1, seconds_per_tick))
                    await owner.emote("cough");
                if (SPT_PROB(1, seconds_per_tick))
                    to_chat(owner, span_danger(I18n.T("Your throat feels sore.")));
                if (SPT_PROB(1, seconds_per_tick))
                    to_chat(owner, span_danger(I18n.T("Mucous runs down the back of your throat.")));
                break;
            case 5:
                if (SPT_PROB(1, seconds_per_tick))
                    await owner.emote("sneeze");
                if (SPT_PROB(1, seconds_per_tick))
                    await owner.emote("cough");
                if (SPT_PROB(2, seconds_per_tick))
                {
                    to_chat(owner, span_danger(I18n.T("Your muscles ache.")));
                    if (prob(20))
                        await owner.take_bodypart_damage(1);
                }
                if (SPT_PROB(2, seconds_per_tick))
                {
                    to_chat(owner, span_danger(I18n.T("Your stomach hurts.")));
                    if (prob(20))
                        await owner.adjust_tox_loss(1);
                }
                break;
            case 6:
                to_chat(owner, span_danger(I18n.T("You feel something tearing its way out of your chest...")));
                await owner.adjust_tox_loss(5 * Dm.num(seconds_per_tick)); // Why is this [TOX]?
                break;
        }
    }

    // tg: code/modules/mob/living/carbon/alien/special/alien_embryo.dm:60
    public override object get_status_appendix(object scanpower = null, object add_tooltips = null)
    {
        if (Dm.num(scanpower) >= SCANPOWER_SUPER)
            return I18n.T("Stage: []/6", stage);
        return null;
    }

    /// Controls Xenomorph Embryo growth. If embryo is fully grown (or overgrown), stop the proc. If not, increase the stage by one and if it's not fully grown (stage 6), add a timer to do this proc again after however long the growth time variable is.
    // tg: code/modules/mob/living/carbon/alien/special/alien_embryo.dm:65
    public virtual void advance_embryo_stage()
    {
        if (stage >= 6)
            return;
        stage++;
        if (stage < 6)
        {
            Waiting.DetachSync(() => RefreshInfectionImage());
            float slowdown = 1;
            if (owner != null) // it gestates out of bodies.
            {
                if (HAS_TRAIT(owner, TRAIT_VIRUS_RESISTANCE))
                    slowdown *= 2; // spaceacillin doubles the time it takes to grow
                if (Dm.truthy(owner.has_status_effect(typeof(datum_status_effect_nest_sustenance))))
                    slowdown *= 0.80f; //egg gestates 20% faster if you're trapped in a nest
                if (HAS_TRAIT(owner, TRAIT_IMMUNODEFICIENCY) && !HAS_TRAIT(owner, TRAIT_VIRUS_RESISTANCE))
                    slowdown *= 0.5f; //terrible immune system = doubled parasite growth
            }

            _addtimer(Dm.@new<datum_callback>(this, nameof(advance_embryo_stage)), Dm.num(growth_time) * slowdown, file: "code/modules/mob/living/carbon/alien/special/alien_embryo.dm", line: 80);
        }
    }

    // tg: code/modules/mob/living/carbon/alien/special/alien_embryo.dm:82
    public override async DmTask egg_process(object seconds_per_tick = null)
    {
        if (stage == 6 && prob(50))
            // If we are mid surgery we won't gib the mob, isn't that neat?
            Waiting.Detach(attempt_grow(gib_on_success: Dm.num(!LIMB_HAS_SURGERY_STATE(bodypart_owner, Dm.bitor(SURGERY_SKIN_OPEN, SURGERY_BONE_SAWED)))));
    }

    /// Attempt to burst an alien outside of the host, getting a ghost to play as the xeno.
    // tg: code/modules/mob/living/carbon/alien/special/alien_embryo.dm:88
    public virtual async DmTask attempt_grow(float? gib_on_success = null)
    {
        gib_on_success ??= TRUE;
        if (QDELETED(owner) || Dm.truthy(bursting))
            return;

        bursting = TRUE;
        mob chosen_one = await SSpolling.poll_ghosts_for_target(
            question: I18n.T("An [] is bursting out of []!", span_notice(I18n.T("alien")), span_danger(Dm.str(owner.real_name))),
            role: ROLE_ALIEN,
            check_jobban: ROLE_ALIEN,
            poll_time: 20 * SECONDS,
            checked_target: this,
            ignore_category: POLL_IGNORE_ALIEN_LARVA,
            alert_pic: owner,
            role_name_text: "alien larva",
            chat_text_border_icon: typeof(mob_living_carbon_alien_larva)) as mob;
        await on_poll_concluded(gib_on_success, chosen_one as mob_dead_observer);
    }

    /// Poll has concluded with a suitor
    // tg: code/modules/mob/living/carbon/alien/special/alien_embryo.dm:107
    public virtual async DmTask on_poll_concluded(object gib_on_success = null, mob_dead_observer ghost = null)
    {
        if (QDELETED(owner))
            return;

        if (ghost == null)
        {
            bursting = FALSE;
            stage = 5; // If no ghosts sign up for the Larva, let's regress our growth by one minute, we will try again!
            _addtimer(Dm.@new<datum_callback>(this, nameof(advance_embryo_stage)), Dm.num(growth_time), file: "code/modules/mob/living/carbon/alien/special/alien_embryo.dm", line: 114);
            return;
        }

        mutable_appearance overlay = mutable_appearance(Dm.resource("icons/mob/nonhuman-player/alien.dmi"), "burst_lie") as mutable_appearance;
        owner.add_overlay(overlay);

        atom xeno_loc = get_turf(owner);
        mob_living_carbon_alien_larva new_xeno = Dm.@new<mob_living_carbon_alien_larva>(xeno_loc);
        new_xeno.PossessByPlayer(ghost.key);
        Dm.output_to(new_xeno, sound(Dm.resource("sound/mobs/non-humanoids/hiss/hiss5.ogg"), 0, 0, 0, 100)); //To get the player's attention (SEND_SOUND)
        new_xeno.add_traits(Dm.list(TRAIT_HANDS_BLOCKED, TRAIT_IMMOBILIZED, TRAIT_NO_TRANSFORM), type); //so we don't move during the bursting animation
        new_xeno.SetInvisibility(INVISIBILITY_MAXIMUM, id: type);

        await sleep(0.6f * SECONDS);

        if (QDELETED(this) || QDELETED(owner))
        {
            qdel(new_xeno);
            throw Dm.CRASH("AttemptGrow failed due to the early qdeletion of source or owner.", "code/modules/mob/living/carbon/alien/special/alien_embryo.dm", 131);
        }

        if (new_xeno != null)
        {
            new_xeno.remove_traits(Dm.list(TRAIT_HANDS_BLOCKED, TRAIT_IMMOBILIZED, TRAIT_NO_TRANSFORM), type);
            new_xeno.RemoveInvisibility(type);
        }

        if (Dm.truthy(gib_on_success))
        {
            new_xeno.visible_message(span_danger(I18n.T("[] bursts out of [] in a shower of gore!", new_xeno, owner)), span_userdanger(I18n.T("You exit [], your previous host.", owner)), span_hear(I18n.T("You hear organic matter ripping and tearing!")));
            owner.investigate_log("has been gibbed by an alien larva.", INVESTIGATE_DEATHS);
            await owner.gib(Dm.bitor(DROP_ORGANS, DROP_BODYPARTS));
        }
        else
        {
            new_xeno.visible_message(span_danger(I18n.T("[] wriggles out of []!", new_xeno, owner)), span_userdanger(I18n.T("You exit [], your previous host.", owner)));
            owner.log_message("had an alien larva within them escape (without being gibbed).", LOG_ATTACK, log_globally: FALSE);
            await owner.adjust_brute_loss(40);
            owner.cut_overlay(overlay);
        }
        qdel(this);
    }

    /*----------------------------------------
    Proc: AddInfectionImages(C)
    Des: Adds the infection image to all aliens for this embryo
    ----------------------------------------*/
    // tg: code/modules/mob/living/carbon/alien/special/alien_embryo.dm:153
    public override void AddInfectionImages()
    {
        foreach (mob_living_carbon_alien alien in Dm.each<mob_living_carbon_alien>(GLOB.player_list))
        {
            image I = Dm.image(Dm.resource("icons/mob/nonhuman-player/alien.dmi"), loc: owner, icon_state: Dm.text("infected[]", stage));
            if (alien.client != null)
                Dm.append(alien.client.images, I);
        }
    }

    /*----------------------------------------
    Proc: RemoveInfectionImage(C)
    Des: Removes all images from the mob infected by this embryo
    ----------------------------------------*/
    // tg: code/modules/mob/living/carbon/alien/special/alien_embryo.dm:162
    public override void RemoveInfectionImages()
    {
        foreach (mob_living_carbon_alien alien in Dm.each<mob_living_carbon_alien>(GLOB.player_list))
        {
            DmList to_remove = Dm.list();
            foreach (image client_image in Dm.each<image>(alien.client?.images))
            {
                string searchfor = "infected";
                if (client_image.loc == owner && Dm.truthy(findtext(client_image.icon_state, searchfor, 1, Dm.length(searchfor) + 1)))
                    Dm.append(to_remove, client_image);
            }
            if (alien.client != null)
                Dm.remove(alien.client.images, to_remove);
        }
    }
}
