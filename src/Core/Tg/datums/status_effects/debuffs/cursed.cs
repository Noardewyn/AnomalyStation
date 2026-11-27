// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/cursed.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the greed slot machine's curse (its brand, its steam, its toll and its end) and its alert. The var blocks are
// generated.

/// Status effect that gives the target miscellanous debuffs while throwing a status alert and causing them to smoke from the damage they're incurring.
/// Purposebuilt for cursed slot machines.
// tg: code/datums/status_effects/debuffs/cursed.dm:5
public partial class datum_status_effect_slot_machine_curse : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/cursed.dm:1  #define DEFAULT_MAX_CURSE_COUNT 5
    private const float DEFAULT_MAX_CURSE_COUNT = 5;

    // tg: code/datums/status_effects/debuffs/cursed.dm:23
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        RegisterSignal(owner, COMSIG_MOB_STATCHANGE, nameof(on_stat_changed));
        RegisterSignal(owner, COMSIG_LIVING_DEATH, nameof(on_death));
        RegisterSignal(owner, COMSIG_CURSED_SLOT_MACHINE_USE, nameof(check_curses));
        RegisterSignal(owner, COMSIG_CURSED_SLOT_MACHINE_LOST, nameof(update_curse_count));
        RegisterSignal(SSdcs, COMSIG_GLOB_CURSED_SLOT_MACHINE_WON, nameof(clear_curses));
        return await base.on_apply(__extra);
    }

    // tg: code/datums/status_effects/debuffs/cursed.dm:31
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(SSdcs, COMSIG_GLOB_CURSED_SLOT_MACHINE_WON);
        branded_hand = null;
        if (Dm.truthy(smoke_path))
            owner.remove_shared_particles(smoke_path);
        return base.Destroy(force);
    }

    /// Checks the number of curses we have and returns information back to the slot machine. `max_curse_amount` is set by the slot machine itself.
    // tg: code/datums/status_effects/debuffs/cursed.dm:39
    [SignalHandler]
    public virtual object check_curses(mob user = null, object max_curse_amount = null)
    {
        if (curse_count >= Dm.num(max_curse_amount))
            return SLOT_MACHINE_USE_CANCEL;

        if (Dm.truthy(monologuing))
        {
            to_chat(owner, span_warning(I18n.T("Your arm is resisting your attempts to pull the lever!"))); // listening to kitschy monologues to postpone your powergaming is the true curse here.
            return SLOT_MACHINE_USE_POSTPONE;
        }
        return null;
    }

    /// Handles the debuffs of this status effect and incrementing the number of curses we have.
    // tg: code/datums/status_effects/debuffs/cursed.dm:49
    [SignalHandler]
    public virtual void update_curse_count()
    {
        curse_count++;

        linked_alert?.update_appearance(); // we may have not initialized it yet

        // addtimer(CALLBACK(src, PROC_REF(handle_after_effects), 1 SECONDS)): tg passes the second to the callback, not to the timer, so
        // the toll comes on the next tick; kept
        _addtimer(Dm.@new<datum_callback>(this, nameof(handle_after_effects), 1 * SECONDS), 0, file: "code/datums/status_effects/debuffs/cursed.dm", line: 55); // give it a second to let the failure sink in before we exact our toll
    }

    /// Makes a nice lorey message about the curse level we're at. I think it's nice
    // tg: code/datums/status_effects/debuffs/cursed.dm:58
    public virtual async DmTask handle_after_effects(object unused = null)
    {
        if (QDELETED(this))
            return;

        monologuing = TRUE;
        DmList messages = Dm.list();
        if (curse_count == 1) // basically your first is a "freebie" that will still require urgent medical attention and will leave you smoking forever but could be worse tbh
        {
            if (owner is mob_living_carbon_human human_owner)
            {
                playsound(human_owner, SFX_SEAR, 50, TRUE);
                var affecting = human_owner.get_active_hand() as obj_item_bodypart;
                branded_hand = affecting;
                await affecting.force_wound_upwards(typeof(datum_wound_burn_flesh_severe_cursed_brand), wound_source: "curse of the slot machine");
            }

            Dm.append(ref messages, span_boldwarning(I18n.T("Your hand burns, and you quickly let go of the lever! You feel a little sick as the nerves deaden in your hand...")));
            Dm.append(ref messages, span_boldwarning(I18n.T("Some smoke appears to be coming out of your hand now, but it's not too bad...")));
            Dm.append(ref messages, span_boldwarning(I18n.T("Fucking stupid machine.")));
        }
        else if (curse_count == 2)
        {
            Dm.append(ref messages, span_boldwarning(I18n.T("The machine didn't burn you this time, it must be some arcane work of the brand recognizing a source...")));
            Dm.append(ref messages, span_boldwarning(I18n.T("Blisters and boils start to appear over your skin. Each one hissing searing hot steam out of its own pocket...")));
            Dm.append(ref messages, span_boldwarning(I18n.T("You understand that the machine tortures you with its simplistic allure. It can kill you at any moment, but it derives a sick satisfaction at forcing you to keep going.")));
            Dm.append(ref messages, span_boldwarning(I18n.T("If you could get away from here, you might be able to live with some medical supplies. Is it too late to stop now?")));
            Dm.append(ref messages, span_boldwarning(I18n.T("As you shut your eyes to dwell on this conundrum, the brand surges in pain. You shudder to think what might happen if you go unconscious.")));
        }
        else if (curse_count == 3)
        {
            await owner.emote("cough");
            Dm.append(ref messages, span_boldwarning(I18n.T("Your throat becomes to feel like it's slowly caking up with sand and dust. You eject the contents of the back of your throat onto your sleeve.")));
            Dm.append(ref messages, span_boldwarning(I18n.T("It is sand. Crimson red. You've never felt so thirsty in your life, yet you don't trust your own hand to carry the glass to your lips.")));
            Dm.append(ref messages, span_boldwarning(I18n.T("You get the sneaking feeling that if someone else were to win, that it might clear your curse too. Saving your life is a noble cause.")));
            Dm.append(ref messages, span_boldwarning(I18n.T("Of course, you might have to not speak on the nature of this machine, in case they scamper off to leave you to die.")));
            Dm.append(ref messages, span_boldwarning(I18n.T("Is it truly worth it to condemn someone to this fate to cure the manifestation of your own hedonistic urges? You'll have to decide quickly.")));
        }
        else if (curse_count == 4)
        {
            Dm.append(ref messages, span_boldwarning(I18n.T("A migraine swells over your head as your thoughts become hazy. Your hand desperately inches closer towards the slot machine for one final pull...")));
            Dm.append(ref messages, span_boldwarning(I18n.T("The ultimate test of mind over matter. You can jerk your own muscle back in order to prevent a terrible fate, but your life already is worth so little now.")));
            Dm.append(ref messages, span_boldwarning(I18n.T("This is what they want, is it not? To witness your failure against itself? The compulsion carries you forward as a sinking feeling of dread fills your stomach.")));
            Dm.append(ref messages, span_boldwarning(I18n.T("Paradoxically, where there is hopelessness, there is elation. Elation at the fact that there's still enough power in you for one more pull.")));
            Dm.append(ref messages, span_boldwarning(I18n.T("Your legs desperate wish to jolt away on the thought of running away from this wretched machination, but your own arm remains complacent in the thought of seeing spinning wheels.")));
            Dm.append(ref messages, span_userdanger(I18n.T("The toll has already been exacted. There is no longer death on 'your' terms. Is your dignity worth more than your life?")));
        }
        else if (curse_count >= 5)
        {
            if (max_curse_count != DEFAULT_MAX_CURSE_COUNT) // this probably will only happen through admin schenanigans letting people stack up infinite curses or something
            {
                to_chat(owner, span_boldwarning(I18n.T("Do you <i>still</i> think you're in control?")));
                return;
            }

            to_chat(owner, span_userdanger(I18n.T("Why couldn't I get one more try?!")));
            owner.investigate_log(Dm.text("has been gibbed by the cursed status effect after accumulating [] curses.", curse_count), INVESTIGATE_DEATHS);
            await owner.gib(DROP_ALL_REMAINS);
            qdel(this);
            return;
        }

        foreach (object message in Dm.each(messages))
        {
            to_chat(owner, message);
            await Dm.sleep(1.5f * SECONDS); // yes yes a bit fast but it can be a lot of text and i want the whole thing to send before the cooldown on the slot machine might expire
        }
        monologuing = FALSE;
    }

    /// Cleans ourselves up and removes our curses. Meant to be done in a "positive" way, when the curse is broken. Directly use qdel otherwise.
    // tg: code/datums/status_effects/debuffs/cursed.dm:117
    [SignalHandler]
    public virtual void clear_curses()
    {
        if (branded_hand != null)
        {
            var brand = branded_hand.get_wound_type(typeof(datum_wound_burn_flesh_severe_cursed_brand)) as datum_wound;
            if (brand != null)
                Waiting.NoSleep(brand.remove_wound());
        }

        owner.visible_message(
            span_notice(I18n.T("The smoke slowly clears from []...", owner.name)),
            span_notice(I18n.T("Your skin finally settles down and your throat no longer feels as dry... The brand disappearing confirms that the curse has been lifted.")));
        qdel(this);
    }

    /// If our owner's stat changes, rapidly surge the damage chance.
    // tg: code/datums/status_effects/debuffs/cursed.dm:131
    [SignalHandler]
    public virtual void on_stat_changed()
    {
        if (!IS_UNCONSCIOUS_OR_CRIT(owner) || owner.stat == DEAD) // reset on these two states
        {
            damage_chance = Dm.initial(this, (datum_status_effect_slot_machine_curse c) => c.damage_chance);
            return;
        }

        to_chat(owner, span_userdanger(I18n.T("As your body crumbles, you feel the curse of the slot machine surge through your body!")));
        damage_chance += 75; //ruh roh raggy
    }

    /// If our owner dies without getting gibbed (as in of other causes), stop smoking because we've "expended all the life energy".
    // tg: code/datums/status_effects/debuffs/cursed.dm:141
    [SignalHandler]
    public virtual void on_death(mob_living source = null, object gibbed = null)
    {
        if (!Dm.truthy(gibbed) && Dm.truthy(smoke_path))
            owner.remove_shared_particles(smoke_path);
    }

    // tg: code/datums/status_effects/debuffs/cursed.dm:147
    public override async DmTask update_particles()
    {
        Type particle_path = typeof(particles_smoke_steam_mild);
        if (curse_count >= 2 && curse_count <= 3)
            particle_path = typeof(particles_smoke_steam);
        else if (curse_count >= 4)
            particle_path = typeof(particles_smoke_steam_bad);

        if (Dm.eq(smoke_path, particle_path))
            return;

        if (Dm.truthy(smoke_path))
            owner.remove_shared_particles(smoke_path);
        owner.add_shared_particles(particle_path);
        smoke_path = particle_path;
        await DmTask.CompletedTask;
    }

    // tg: code/datums/status_effects/debuffs/cursed.dm:163
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (curse_count <= 1)
            return null; // you get one "freebie" (single damage) to nudge you into thinking this is a bad idea before the house begins to win.

        // the house won.
        float ticked_coefficient = Dm.rand(15, 40) / 100f;
        float effective_percentile_chance = (curse_count == 2 ? 1 : curse_count) * damage_chance * ticked_coefficient;

        if (SPT_PROB(effective_percentile_chance, seconds_between_ticks))
        {
            await owner.apply_damages(
                brute: curse_count * ticked_coefficient,
                burn: curse_count * ticked_coefficient,
                oxy: curse_count * ticked_coefficient);
        }
        return null;
    }
}

// tg: code/datums/status_effects/debuffs/cursed.dm:179
public partial class atom_movable_screen_alert_status_effect_cursed : atom_movable_screen_alert_status_effect
{
    // tg: code/datums/status_effects/debuffs/cursed.dm:185
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        var linked_effect = attached_effect as datum_status_effect_slot_machine_curse;
        float? curses = linked_effect?.curse_count;
        if (curses == 2)
            desc = "Your greed is catching up to you...";
        else if (curses == 3)
            desc = "You really don't feel good right now... But why stop now?";
        else if (curses >= 4)
            desc = "Real winners quit before they reach the ultimate prize.";
        return dot;
    }
}
