// Translated from tgstation (snapshot 1d99bd7f): code/datums/brain_damage/split_personality.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps B1 (D327): the whole file, the split personality (a ghost polled for the stranger, the two backseat mobs inside the body
// that swap the player, the personalities' commune), the brainwashing's split mind with its codeword, and the alcohol blackout.
// The var blocks are generated (D111).

// tg: code/datums/brain_damage/split_personality.dm:4
public partial class datum_brain_trauma_severe_split_personality : datum_brain_trauma_severe
{
    // tg: code/datums/brain_damage/split_personality.dm:1  #define OWNER 0 (#undef at :349)
    protected const float OWNER = 0;
    // tg: code/datums/brain_damage/split_personality.dm:2  #define STRANGER 1 (#undef at :350)
    protected const float STRANGER = 1;

    // tg: code/datums/brain_damage/split_personality.dm:22
    public override async DmTask<object> on_gain()
    {
        mob_living brain_owner = owner;
        if (brain_owner.stat == DEAD || !Dm.truthy(GET_CLIENT(brain_owner)) || istype(get_area(brain_owner), typeof(area_deathmatch)) || HAS_TRAIT(brain_owner, TRAIT_NO_SPLIT_PERSONALITY)) //No use assigning people to a corpse or braindead
            return FALSE;
        object dot = await base.on_gain();
        await make_backseats();

        // tg: #ifdef UNIT_TESTS ... #endif not translated (inactive in production)

        await get_ghost();
        return dot;
    }

    // tg: code/datums/brain_damage/split_personality.dm:35
    public virtual async DmTask make_backseats()
    {
        stranger_backseat = Dm.@new<mob_living_split_personality>(owner, this);
        datum_action_personality_commune stranger_spell = Dm.@new<datum_action_personality_commune>(this);
        await stranger_spell.Grant(stranger_backseat);

        owner_backseat = Dm.@new<mob_living_split_personality>(owner, this);
        datum_action_personality_commune owner_spell = Dm.@new<datum_action_personality_commune>(this);
        await owner_spell.Grant(owner_backseat);
    }

    /// Attempts to get a ghost to play the personality
    // tg: code/datums/brain_damage/split_personality.dm:45
    public virtual async DmTask get_ghost()
    {
        mob chosen_one = await SSpolling.poll_ghosts_for_target(
            question: I18n.T("Do you want to play as [] []?", span_danger(Dm.text("[]'s", owner.real_name)), span_notice(poll_role)),
            check_jobban: ROLE_PAI,
            poll_time: poll_time,
            checked_target: owner,
            ignore_category: POLL_IGNORE_SPLITPERSONALITY,
            alert_pic: owner,
            role_name_text: poll_role) as mob;
        schism(chosen_one as mob_dead_observer);
    }

    /// Ghost poll has concluded
    // tg: code/datums/brain_damage/split_personality.dm:58
    public virtual void schism(mob_dead_observer ghost = null)
    {
        if (ghost == null)
        {
            qdel(this);
            return;
        }

        stranger_backseat.PossessByPlayer(ghost.ckey);
        stranger_backseat.log_message(Dm.text("became []'s split personality.", key_name(owner)), LOG_GAME);
        message_admins(Dm.text("[] became []'s split personality.", ADMIN_LOOKUPFLW(stranger_backseat), ADMIN_LOOKUPFLW(owner)));
    }

    // tg: code/datums/brain_damage/split_personality.dm:68
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (owner.stat == DEAD)
        {
            if (current_controller != OWNER)
                switch_personalities(TRUE);
            qdel(this);
        }
        else if (SPT_PROB(1.5f, seconds_per_tick))
            switch_personalities();
        await base.on_life(seconds_per_tick);
    }

    // tg: code/datums/brain_damage/split_personality.dm:77
    public override async DmTask on_lose(object silent = null)
    {
        if (current_controller != OWNER) //it would be funny to cure a guy only to be left with the other personality, but it seems too cruel
            switch_personalities(TRUE);
        QDEL_NULL(ref stranger_backseat);
        QDEL_NULL(ref owner_backseat);
        await base.on_lose(silent);
    }

    // tg: code/datums/brain_damage/split_personality.dm:85
    public virtual void switch_personalities(float? reset_to_owner = null)
    {
        reset_to_owner ??= FALSE;
        if (QDELETED(owner) || QDELETED(stranger_backseat) || QDELETED(owner_backseat))
            return;

        mob_living_split_personality current_backseat;
        mob_living_split_personality new_backseat;
        if (current_controller == STRANGER || Dm.truthy(reset_to_owner))
        {
            current_backseat = owner_backseat;
            new_backseat = stranger_backseat;
        }
        else
        {
            current_backseat = stranger_backseat;
            new_backseat = owner_backseat;
        }

        if (current_backseat.client == null) //Make sure we never switch to a logged off mob.
            return;

        current_backseat.log_message(Dm.text("assumed control of [] due to []. (Original owner: [])", key_name(owner), this, current_controller == OWNER ? owner.key : current_backseat.key), LOG_GAME);
        to_chat(owner, span_userdanger(I18n.T("You feel your control being taken away... your other personality is in charge now!")));
        to_chat(current_backseat, span_userdanger(I18n.T("You manage to take control of your body!")));

        //Body to backseat

        object h2b_id = owner.computer_id;
        object h2b_ip = owner.lastKnownIP;
        owner.computer_id = null;
        owner.lastKnownIP = null;

        new_backseat.ckey = owner.ckey;

        new_backseat.name = owner.name;

        if (owner.mind != null)
            new_backseat.mind = owner.mind;

        if (!Dm.truthy(new_backseat.computer_id))
            new_backseat.computer_id = h2b_id;

        if (!Dm.truthy(new_backseat.lastKnownIP))
            new_backseat.lastKnownIP = h2b_ip;

        if (Dm.truthy(reset_to_owner) && new_backseat.mind != null)
            new_backseat.ghostize(FALSE);

        //Backseat to body

        object s2h_id = current_backseat.computer_id;
        object s2h_ip = current_backseat.lastKnownIP;
        current_backseat.computer_id = null;
        current_backseat.lastKnownIP = null;

        owner.ckey = current_backseat.ckey;
        owner.mind = current_backseat.mind;

        if (!Dm.truthy(owner.computer_id))
            owner.computer_id = s2h_id;

        if (!Dm.truthy(owner.lastKnownIP))
            owner.lastKnownIP = s2h_ip;

        current_controller = Dm.num(!Dm.truthy(current_controller));
    }
}

// tg: code/datums/brain_damage/split_personality.dm:147
public partial class mob_living_split_personality : mob_living
{
    // tg: code/datums/brain_damage/split_personality.dm:153
    [DmParams("mapload", "_trauma")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object _trauma = Dm.arg(__extra, 0);
        if (iscarbon(loc))
        {
            body = (mob_living_carbon)loc;
            name = Dm.str(body.real_name);
            real_name = body.real_name;
            trauma = _trauma as datum_brain_trauma_severe_split_personality;
        }
        return base.Initialize(mapload, __extra);
    }

    // tg: code/datums/brain_damage/split_personality.dm:161
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        seconds_per_tick ??= SSMOBS_DT();
        if (QDELETED(body))
            qdel(this); //in case trauma deletion doesn't already do it

        if ((body.stat == DEAD && trauma.owner_backseat == this))
        {
            trauma.switch_personalities();
            qdel(trauma);
        }

        //if one of the two ghosts, the other one stays permanently
        if (body.client == null && Dm.truthy(trauma.initialized))
        {
            trauma.switch_personalities();
            qdel(trauma);
        }

        return base.Life(seconds_per_tick, __extra);
    }

    // tg: code/datums/brain_damage/split_personality.dm:176
    public override async DmTask<object> Login()
    {
        object dot = await base.Login();
        if (!Dm.truthy(dot) || client == null)
            return FALSE;
        to_chat(this, span_notice(I18n.T("As a split personality, you cannot do anything but observe. However, you will eventually gain control of your body, switching places with the current personality.")));
        to_chat(this, span_warning(I18n.T("<b>Do not commit suicide or put the body in a deadly position. Behave like you care about it as much as the owner.</b>")));
        return dot;
    }

    // tg: code/datums/brain_damage/split_personality.dm:183
    public override async DmTask<object> try_speak(object message = null, float? ignore_spam = null, object forced = null, float? filterproof = null)
    {
        // SHOULD_CALL_PARENT(FALSE)
        to_chat(this, span_warning(I18n.T("You cannot speak, your other self is controlling your body!")));
        await DmTask.CompletedTask;
        return FALSE;
    }

    // tg: code/datums/brain_damage/split_personality.dm:188
    public override async DmTask<object> emote(object act = null, float? type_override = null, object message = null, float? intentional = null, float? force_silence = null, float? forced = null)
    {
        await DmTask.CompletedTask;
        return FALSE;
    }
}

///////////////BRAINWASHING////////////////////

// tg: code/datums/brain_damage/split_personality.dm:193
public partial class datum_brain_trauma_severe_split_personality_brainwashing : datum_brain_trauma_severe_split_personality
{
    // tg: code/datums/brain_damage/split_personality.dm:204
    [DmParams("B", "_permanent", "_codeword", "_objective")]
    public override async DmTask New(params object[] __extra)
    {
        object _codeword = Dm.arg(__extra, 2);
        await base.New(__extra);
        if (Dm.truthy(_codeword))
            codeword = _codeword;
        else
        {
            codeword = pick(Dm.bitor(Dm.bitor(Dm.bitor(Dm.bitor(Dm.bitor(
                strings("ion_laws.json", "ionabstract"),
                strings("ion_laws.json", "ionobjects")),
                strings("ion_laws.json", "ionadjectives")),
                strings("ion_laws.json", "ionthreats")),
                strings("ion_laws.json", "ionfood")),
                strings("ion_laws.json", "iondrinks")) as DmList);
        }
    }

    // tg: code/datums/brain_damage/split_personality.dm:216
    public override async DmTask<object> on_gain()
    {
        object dot = await base.on_gain();
        mob_living_split_personality_traitor traitor_backseat = stranger_backseat as mob_living_split_personality_traitor;
        traitor_backseat.codeword = codeword;
        traitor_backseat.objective = objective;
        return dot;
    }

    // tg: code/datums/brain_damage/split_personality.dm:222
    public override async DmTask make_backseats()
    {
        stranger_backseat = Dm.@new<mob_living_split_personality_traitor>(owner, this, codeword, objective);
        owner_backseat = Dm.@new<mob_living_split_personality>(owner, this);
        await DmTask.CompletedTask;
    }

    // tg: code/datums/brain_damage/split_personality.dm:226  (set waitfor = FALSE: the wrapper of waiting-model.md 5.4)
    public override async DmTask get_ghost()
    {
        Waiting.WaitforFalse(get_ghost__body());
    }

    // The body of the set waitfor = FALSE wrapper above (waiting-model.md 5.4).
    private async DmTask get_ghost__body()
    {
        mob chosen_one = await SSpolling.poll_ghosts_for_target(I18n.T("Do you want to play as [] brainwashed mind?", span_danger(Dm.text("[]'s", owner.real_name))), poll_time: 7.5f * SECONDS, checked_target: stranger_backseat, alert_pic: owner, role_name_text: "brainwashed mind") as mob;
        if (chosen_one != null)
            stranger_backseat.PossessByPlayer(chosen_one.ckey);
        else
            qdel(this);
    }

    // tg: code/datums/brain_damage/split_personality.dm:234
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await DmTask.CompletedTask;
        return; //no random switching
    }

    // tg: code/datums/brain_damage/split_personality.dm:237
    public override void handle_hearing(datum source = null, DmList hearing_args = null)
    {
        if (HAS_TRAIT(owner, TRAIT_DEAF) || owner == hearing_args[HEARING_SPEAKER] || !Dm.truthy(owner.has_language(hearing_args[HEARING_LANGUAGE])))
            return;

        object message = hearing_args[HEARING_RAW_MESSAGE];
        if (Dm.truthy(findtext(message, codeword)))
        {
            hearing_args[HEARING_RAW_MESSAGE] = replacetext(message, codeword, span_warning(Dm.text("[]", codeword)));
            _addtimer(Dm.@new<datum_callback>(this, nameof(datum_brain_trauma_severe_split_personality.switch_personalities)), 1 * SECONDS, file: "code/datums/brain_damage/split_personality.dm", line: 244);
        }
    }

    // tg: code/datums/brain_damage/split_personality.dm:246
    public override void handle_speech(datum source = null, DmList speech_args = null)
    {
        if (Dm.truthy(findtext(speech_args[SPEECH_MESSAGE], codeword)))
            speech_args[SPEECH_MESSAGE] = ""; //oh hey did you want to tell people about the secret word to bring you back?
    }
}

// tg: code/datums/brain_damage/split_personality.dm:250
public partial class mob_living_split_personality_traitor : mob_living_split_personality
{
    // tg: code/datums/brain_damage/split_personality.dm:256
    public override async DmTask<object> Login()
    {
        object dot = await base.Login();
        if (!Dm.truthy(dot) || client == null)
            return FALSE;
        to_chat(this, span_notice(I18n.T("As a brainwashed personality, you cannot do anything yet but observe. However, you may gain control of your body if you hear the special codeword, switching places with the current personality.")));
        to_chat(this, span_notice(I18n.T("Your activation codeword is: <b>[]</b>", codeword)));
        if (Dm.truthy(objective))
            to_chat(this, span_notice(I18n.T("Your master left you an objective: <b>[]</b>. Follow it at all costs when in control.", objective)));
        return dot;
    }
}

// tg: code/datums/brain_damage/split_personality.dm:265
public partial class datum_brain_trauma_severe_split_personality_blackout : datum_brain_trauma_severe_split_personality
{
    // tg: code/datums/brain_damage/split_personality.dm:280
    public override async DmTask<object> on_gain()
    {
        object dot = await base.on_gain();

        if (QDELETED(this))
            return dot;

        RegisterSignal(owner, COMSIG_ATOM_SPLASHED, nameof(on_splashed));
        notify_ghosts(
            I18n.T("[] is blacking out!", owner.real_name),
            source: owner,
            header: I18n.T("Bro I'm not even drunk right now"),
            notify_flags: NOTIFY_CATEGORY_NOFLASH);
        datum_status_effect_inebriated inebriation = owner.has_status_effect(typeof(datum_status_effect_inebriated)) as datum_status_effect_inebriated;
        if (inebriation != null)
            inebriation.iron_liver = TRUE;
        return dot;
    }

    // tg: code/datums/brain_damage/split_personality.dm:296
    public override async DmTask on_lose(object silent = null)
    {
        await base.on_lose(silent);
        await owner.add_mood_event("hang_over", typeof(datum_mood_event_hang_over));
        UnregisterSignal(owner, COMSIG_ATOM_SPLASHED);
        datum_status_effect_inebriated inebriation = owner.has_status_effect(typeof(datum_status_effect_inebriated)) as datum_status_effect_inebriated;
        if (inebriation != null)
            inebriation.iron_liver = FALSE;
    }

    // tg: code/datums/brain_damage/split_personality.dm:303
    [SignalHandler]
    public virtual void on_splashed()
    {
        if (prob(20))//we don't want every single splash to wake them up now do we
            qdel(this);
    }

    // tg: code/datums/brain_damage/split_personality.dm:308
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (current_controller == OWNER && stranger_backseat != null)//we should only start transitioning after the other personality has entered
        {
            owner.overlay_fullscreen("fade_to_black", typeof(atom_movable_screen_fullscreen_blind));
            owner.clear_fullscreen("fade_to_black", animated: 4 * SECONDS);
            switch_personalities();
        }
        if (owner.stat == DEAD)
        {
            if (current_controller != OWNER)
                switch_personalities(TRUE);
            qdel(this);
            return;
        }
        if (duration_in_seconds <= 0)
        {
            qdel(this);
            return;
        }
        else if (duration_in_seconds <= 60 && !Dm.truthy(Dm.mod(duration_in_seconds, 20)))
            to_chat(owner, span_warning(I18n.T("You have [] seconds left before sobering up!", duration_in_seconds)));
        if (prob(10) && !HAS_TRAIT(owner, TRAIT_DISCOORDINATED_TOOL_USER))
        {
            ADD_TRAIT(owner, TRAIT_DISCOORDINATED_TOOL_USER, TRAUMA_TRAIT);
            owner.balloon_alert(owner, I18n.T("dexterity reduced temporarily!"));
            //We then send a callback to automatically re-add the trait
            _addtimer(TRAIT_CALLBACK_REMOVE(owner, TRAIT_DISCOORDINATED_TOOL_USER, TRAUMA_TRAIT), 10 * SECONDS, file: "code/datums/brain_damage/split_personality.dm", line: 327);
            _addtimer(Dm.@new<datum_callback>(owner, nameof(atom.balloon_alert), owner, I18n.T("dexterity regained!")), 10 * SECONDS, file: "code/datums/brain_damage/split_personality.dm", line: 328);
        }
        if (prob(15))
        {
            playsound(owner, Dm.resource("sound/mobs/humanoids/human/hiccup/sf_hiccup_male_01.ogg"), 50);
            await owner.emote("hiccup");
        }
        //too drunk to feel anything
        //if they're to this point, they're likely dying of liver damage
        //and not accounting for that, the split personality is temporary
        await owner.adjust_stamina_loss(-25);
        duration_in_seconds -= Dm.num(seconds_per_tick);
    }
}

// tg: code/datums/brain_damage/split_personality.dm:338
public partial class mob_living_split_personality_blackout : mob_living_split_personality
{
    // tg: code/datums/brain_damage/split_personality.dm:342
    public override async DmTask<object> Login()
    {
        object dot = await base.Login();
        if (!Dm.truthy(dot) || client == null)
            return FALSE;
        to_chat(this, span_notice(I18n.T("You're the incredibly inebriated leftovers of your host's consciousness! Make sure to act the part and leave a trail of confusion and chaos in your wake.")));
        to_chat(this, span_boldwarning(I18n.T("While you're drunk, you're not suicidal. Do not commit suicide or put the body in danger. You have a minor license to grief just like a clown, but do not kill anyone or create a situation leading to the body being put in danger or at risk of being harmed.")));
        return dot;
    }
}
