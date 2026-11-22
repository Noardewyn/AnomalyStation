// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/transform_procs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps package C5): monkeyize and humanize; silicons SI2 (D326): Robotize, replace_banned_cyborg and transform_into_mob.
// Silicons SI4: AIize (the mob's and the carbon's). Gaps P3: gorillize. The alien, slime and other transformations are rows.

// tg: code/modules/mob/transform_procs.dm:7
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/transform_procs.dm:1
    private const float TRANSFORMATION_DURATION = 22;
    /// Will be removed once the transformation is complete.
    // tg: code/modules/mob/transform_procs.dm:3
    private const string TEMPORARY_TRANSFORMATION_TRAIT = "temporary_transformation";

    // tg: code/modules/mob/transform_procs.dm:7
    public virtual void monkeyize(float? instant = null)
    {
        instant ??= FALSE;
        if (transformation_timer != null || HAS_TRAIT(this, TRAIT_NO_TRANSFORM))
            return;

        if (ismonkey(this))
            return;

        if (Dm.truthy(instant))
        {
            Waiting.NoSleep(finish_monkeyize());
            return;
        }

        //Make mob invisible and spawn animation
        ADD_TRAIT(this, TRAIT_NO_TRANSFORM, TEMPORARY_TRANSFORMATION_TRAIT);
        Waiting.NoSleep(Stun(TRANSFORMATION_DURATION, ignore_canstun: TRUE));
        icon = null;
        cut_overlays();

        obj effect = Dm.@new<obj_effect_temp_visual_monkeyify>(loc);
        effect.SetInvisibility(invisibility);
        SetInvisibility(INVISIBILITY_MAXIMUM, id: type);

        transformation_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(finish_monkeyize)), TRANSFORMATION_DURATION, TIMER_UNIQUE, file: "code/modules/mob/transform_procs.dm", line: 28);
    }

    // tg: code/modules/mob/transform_procs.dm:30
    public virtual async DmTask<object> finish_monkeyize()
    {
        transformation_timer = null;
        REMOVE_TRAIT(this, TRAIT_NO_TRANSFORM, TEMPORARY_TRANSFORMATION_TRAIT);
        icon = Dm.initial(type, "icon");
        RemoveInvisibility(type);
        set_species(typeof(datum_species_monkey));
        to_chat(this, span_boldnotice(I18n.T("You are now \\a [].", ((datum_species)dna.species).name)));
        name = LOWER_TEXT(((datum_species)dna.species).name as string);
        await regenerate_icons();
        set_name();
        // SEND_SIGNAL(src, COMSIG_HUMAN_MONKEYIZE) (rules 11.1 (e), a statement)
        if (Dm.truthy(this._listen_lookup?[COMSIG_HUMAN_MONKEYIZE]))
            this._SendSignal(COMSIG_HUMAN_MONKEYIZE, Dm.list(this));
        await uncuff();
        return this;
    }

    //////////////////////////           Humanize               //////////////////////////////
    //Could probably be merged with monkeyize but other transformations got their own procs, too

    // tg: code/modules/mob/transform_procs.dm:47
    public virtual void humanize(object species = null, float? instant = null)
    {
        species ??= typeof(datum_species_human);
        instant ??= FALSE;
        if (transformation_timer != null || HAS_TRAIT(this, TRAIT_NO_TRANSFORM))
            return;

        if (!ismonkey(this))
            return;

        if (Dm.truthy(instant))
        {
            Waiting.NoSleep(finish_humanize(species));
            return;
        }

        //Make mob invisible and spawn animation
        ADD_TRAIT(this, TRAIT_NO_TRANSFORM, TEMPORARY_TRANSFORMATION_TRAIT);
        Waiting.NoSleep(Stun(TRANSFORMATION_DURATION, ignore_canstun: TRUE));
        icon = null;
        cut_overlays();

        obj effect = Dm.@new<obj_effect_temp_visual_monkeyify_humanify>(loc);
        effect.SetInvisibility(invisibility);
        SetInvisibility(INVISIBILITY_MAXIMUM, id: type);

        transformation_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(finish_humanize), species), TRANSFORMATION_DURATION, TIMER_UNIQUE, file: "code/modules/mob/transform_procs.dm", line: 68);
    }

    // tg: code/modules/mob/transform_procs.dm:71
    public virtual async DmTask<object> finish_humanize(object species = null)
    {
        species ??= typeof(datum_species_human);
        transformation_timer = null;
        REMOVE_TRAIT(this, TRAIT_NO_TRANSFORM, TEMPORARY_TRANSFORMATION_TRAIT);
        icon = Dm.initial(type, "icon");
        RemoveInvisibility(type);
        set_species(species);
        to_chat(this, span_boldnotice(I18n.T("You are now \\a [].", ((datum_species)dna.species).name)));
        // SEND_SIGNAL(src, COMSIG_MONKEY_HUMANIZE) (rules 11.1 (e), a statement)
        if (Dm.truthy(this._listen_lookup?[COMSIG_MONKEY_HUMANIZE]))
            this._SendSignal(COMSIG_MONKEY_HUMANIZE, Dm.list(this));
        return this;
    }
}

// tg: code/modules/mob/transform_procs.dm:81 (interleaved types, rules 1.9)
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/transform_procs.dm:81
    public override async DmTask<object> finish_humanize(object species = null)
    {
        species ??= typeof(datum_species_human);
        underwear = "Nude";
        undershirt = "Nude";
        socks = "Nude";
        return await base.finish_humanize(species);
    }
}

// tg: code/modules/mob/transform_procs.dm:135 (interleaved types, rules 1.9)
public partial class mob : atom_movable
{
    // tg: code/modules/mob/transform_procs.dm:135
    public virtual async DmTask<object> Robotize(float? delete_items = null)
    {
        delete_items ??= FALSE;
        mob_living_silicon_robot new_borg = await transform_into_mob(typeof(mob_living_silicon_robot), delete_items) as mob_living_silicon_robot;

        if (new_borg == null)
            return null;

        if (new_borg.client != null)
        {
            new_borg.set_gender(new_borg.client);
            if (!Dm.eq(new_borg.client.prefs.read_preference(typeof(datum_preference_name_cyborg)), DEFAULT_CYBORG_NAME))
                await new_borg.apply_pref_name(typeof(datum_preference_name_cyborg), new_borg.client);
        }

        new_borg.mmi?.set_name(real_name);

        new_borg.job = JOB_CYBORG;
        new_borg.notify_ai(AI_NOTIFICATION_NEW_BORG);

        if (Dm.truthy(new_borg.ckey) && Dm.truthy(is_banned_from(new_borg.ckey, JOB_CYBORG)))
            Waiting.Detach(new_borg.replace_banned_cyborg()); // INVOKE_ASYNC
        return new_borg;
    }

    // tg: code/modules/mob/transform_procs.dm:210  (gaps P3: genetic damage turns a monkey into a gorilla)
    public virtual async DmTask<object> gorillize(float? genetics_gorilla = null)
    {
        genetics_gorilla ??= FALSE;
        mob_living_basic_gorilla ideal_body = await transform_into_mob(Dm.truthy(genetics_gorilla) ? typeof(mob_living_basic_gorilla_genetics) : typeof(mob_living_basic_gorilla)) as mob_living_basic_gorilla;
        if (ideal_body == null)
            return null;
        SSblackbox.record_feedback("amount", "gorillas_created", 1);
        to_chat(ideal_body, span_boldnotice(I18n.T("You are now a gorilla. Ooga ooga!")));
        ideal_body.set_combat_mode(TRUE);
        return ideal_body;
    }

    ///Creates a new mob, deletes the old one, transfers old mob into new mob
    // tg: code/modules/mob/transform_procs.dm:232
    public virtual async DmTask<object> transform_into_mob(object mob_path = null, float? delete_items = null)
    {
        delete_items ??= FALSE;
        if (HAS_TRAIT(this, TRAIT_NO_TRANSFORM))
            return null;

        if (!Dm.truthy(delete_items))
        {
            foreach (obj_item detritus in Dm.each<obj_item>(get_equipped_items(Dm.bitor(INCLUDE_POCKETS, INCLUDE_HELD))))
                await dropItemToGround(detritus, TRUE);
        }

        mob new_mob = Dm.@new((Type)mob_path, get_turf(this)) as mob;
        if (mind != null)
            await mind.transfer_to(new_mob);
        else
            new_mob.PossessByPlayer(key);

        qdel(this);
        return new_mob;
    }
}

// tg: code/modules/mob/transform_procs.dm:156 (interleaved types, rules 1.9)
public partial class mob_living_silicon_robot : mob_living_silicon
{
    // tg: code/modules/mob/transform_procs.dm:156
    public virtual async DmTask replace_banned_cyborg()
    {
        to_chat(this, I18n.T("<b>You are job banned from cyborg! Appeal your job ban if you want to avoid this in the future!</b>"));
        ghostize(FALSE);

        mob chosen_one = await SSpolling.poll_ghosts_for_target(I18n.T("Do you want to play as []?", span_notice(name)), check_jobban: JOB_CYBORG, poll_time: 5 * SECONDS, checked_target: this, alert_pic: this, role_name_text: "cyborg") as mob;
        if (chosen_one != null)
        {
            message_admins(Dm.text("[] has taken control of ([]) to replace a jobbanned player.", key_name_admin(chosen_one), key_name_admin(this)));
            key = chosen_one.key;
        }
    }
}

// tg: code/modules/mob/transform_procs.dm:87 (interleaved types, rules 1.9)
public partial class mob : atom_movable
{
    // tg: code/modules/mob/transform_procs.dm:87
    public virtual async DmTask<object> AIize(client preference_source = null, float? move = null)
    {
        move ??= TRUE;
        DmList landmark_loc = Dm.list();

        if (!Dm.truthy(move))
            Dm.append(ref landmark_loc, loc);
        else
        {
            foreach (obj_effect_landmark_start_ai sloc in Dm.each<obj_effect_landmark_start_ai>(GLOB.landmarks_list))
            {
                if (Dm.locate<mob_living_silicon_ai>(sloc.loc) != null)
                    continue;
                if (Dm.truthy(sloc.primary_ai))
                {
                    landmark_loc?.Cut(); // LAZYCLEARLIST
                    Dm.append(ref landmark_loc, sloc.loc);
                    break;
                }
                Dm.append(ref landmark_loc, sloc.loc);
            }
            if (!Dm.truthy(length(landmark_loc)))
            {
                to_chat(this, I18n.T("Oh god sorry we can't find an unoccupied AI spawn location, so we're spawning you on top of someone."));
                foreach (obj_effect_landmark_start_ai sloc in Dm.each<obj_effect_landmark_start_ai>(GLOB.landmarks_list))
                    Dm.append(ref landmark_loc, sloc.loc);
            }
        }

        if (!Dm.truthy(length(landmark_loc)))
        {
            message_admins(Dm.text("Could not find ai landmark for []. Yell at a mapper! We are spawning them at their current location.", this));
            Dm.append(ref landmark_loc, loc);
        }

        if (client != null)
            stop_sound_channel(CHANNEL_LOBBYMUSIC);

        mob_living_silicon_ai our_AI = Dm.@new<mob_living_silicon_ai>(pick(landmark_loc), this);
        object dot = our_AI;

        if (preference_source != null)
        {
            await apply_pref_name(typeof(datum_preference_name_ai), preference_source);
            our_AI.apply_pref_hologram_display(preference_source);
            await our_AI.set_core_display_icon(null, preference_source);
        }

        qdel(this);
        return dot;
    }
}

// tg: code/modules/mob/transform_procs.dm:123 (interleaved types, rules 1.9)
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/transform_procs.dm:123
    /// Will not be removed when the transformation is complete.
    // tg: code/modules/mob/transform_procs.dm:5
    private const string PERMANENT_TRANSFORMATION_TRAIT = "permanent_transformation";

    public override async DmTask<object> AIize(client preference_source = null, float? move = null)
    {
        float? transfer_after = move ?? TRUE; // tg names the carbon's second argument transfer_after; it is the mob's move
        if (HAS_TRAIT(this, TRAIT_NO_TRANSFORM))
            return null;
        ADD_TRAIT(this, TRAIT_NO_TRANSFORM, PERMANENT_TRANSFORMATION_TRAIT);
        await Paralyze(1, ignore_canstun: TRUE);
        foreach (obj_item W in Dm.each<obj_item>(contents.Copy()))
            await dropItemToGround(W);
        await regenerate_icons();
        icon = null;
        SetInvisibility(INVISIBILITY_MAXIMUM);
        return await base.AIize(preference_source, transfer_after);
    }
}

