// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/eightball.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4a, D316). The ghosts vote in the haunted ball's EightBallVote window.
// The var block is generated (D111).

// tg: code/game/objects/items/eightball.dm:1
public partial class obj_item_toy_eightball : obj_item_toy
{
    // tg: code/game/objects/items/eightball.dm:17  var/static/list/possible_answers
    public static DmList possible_answers => TgState.Current.obj_item_toy_eightball__possible_answers ??= Dm.list(
        Dm.kv("Yes", Dm.list(
            "It is certain",
            "It is decidedly so",
            "Without a doubt",
            "Yes definitely",
            "You may rely on it",
            "As I see it, yes",
            "Most likely",
            "Outlook good",
            "Yes",
            "Signs point to yes")),
        Dm.kv("Maybe", Dm.list(
            "Reply hazy try again",
            "Ask again later",
            "Better not tell you now",
            "Cannot predict now",
            "Concentrate and ask again")),
        Dm.kv("No", Dm.list(
            "Don't count on it",
            "My reply is no",
            "My sources say no",
            "Outlook not so good",
            "Very doubtful")));

    // tg: code/game/objects/items/eightball.dm:46
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(MakeHaunted()))
            return INITIALIZE_HINT_QDEL;
        return dot;
    }

    // tg: code/game/objects/items/eightball.dm:51
    public virtual object MakeHaunted()
    {
        if (prob(1))
        {
            Dm.@new<obj_item_toy_eightball_haunted>(loc);
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/objects/items/eightball.dm:57
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(await base.attack_self(user, modifiers, __extra)))
            return null;

        object dot = TRUE;
        if (Dm.truthy(shaking))
            return dot;

        if (Dm.truthy(on_cooldown))
        {
            to_chat(user, span_warning(I18n.T("[] was shaken recently, it needs time to settle.", this)));
            return dot;
        }

        user.visible_message(span_notice(I18n.T("[] starts shaking [].", user, this)), span_notice(I18n.T("You start shaking [].", this)), span_hear(I18n.T("You hear shaking and sloshing.")));

        shaking = TRUE;

        if (!Dm.truthy(await start_shaking(user)))
            return dot;

        if (Dm.truthy(await do_after(user, shake_time)))
        {
            await say(get_answer());

            on_cooldown = TRUE;
            _addtimer(Dm.@new<datum_callback>(this, nameof(reset_on_cooldown)), Dm.num(cooldown_time), file: "code/game/objects/items/eightball.dm", line: 80);
        }

        shaking = FALSE;
        return dot;
    }

    // tg: eightball.dm:80  VARSET_CALLBACK(src, on_cooldown, FALSE)
    public virtual void reset_on_cooldown() => on_cooldown = FALSE;

    // tg: code/game/objects/items/eightball.dm:84
    public virtual async DmTask<object> start_shaking(mob user = null)
    {
        return TRUE;
    }

    /// Different from get_answer().
    // tg: code/game/objects/items/eightball.dm:88
    public virtual object pick_from_answer_list()
    {
        //! This is for grabbing an answer from the answer matrix.
        object key = pick(possible_answers);
        return pick(possible_answers[key] as DmList);
    }

    // tg: code/game/objects/items/eightball.dm:93
    public virtual object get_answer()
    {
        return pick_from_answer_list();
    }
}

// A broken magic eightball, it only says "YOU SUCK" over and over again.

// tg: code/game/objects/items/eightball.dm:98
public partial class obj_item_toy_eightball_broken : obj_item_toy_eightball
{
    // tg: code/game/objects/items/eightball.dm:103
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        fixed_answer = pick_from_answer_list();
        return dot;
    }

    // tg: code/game/objects/items/eightball.dm:107
    public override object get_answer()
    {
        return fixed_answer;
    }
}

// Haunted eightball is identical in description and function to toy,
// except it actually ASKS THE DEAD (wooooo)

// tg: code/game/objects/items/eightball.dm:113
public partial class obj_item_toy_eightball_haunted : obj_item_toy_eightball
{
    // tg: code/game/objects/items/eightball.dm:121
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        foreach (object answer in Dm.each(possible_answers))
            votes[answer] = 0;
        Waiting.NoSleep(SSpoints_of_interest.make_point_of_interest(this));
        return dot;
    }

    // tg: code/game/objects/items/eightball.dm:127
    public override object MakeHaunted()
    {
        return FALSE;
    }

    //ATTACK GHOST IGNORING PARENT RETURN VALUE
    // tg: code/game/objects/items/eightball.dm:131
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        if (!Dm.truthy(shaking))
        {
            to_chat(user, span_warning(I18n.T("[] is not currently being shaken.", this)));
            return null;
        }
        await interact(user);
        return await base.attack_ghost(user);
    }

    // tg: code/game/objects/items/eightball.dm:138
    public override async DmTask<object> start_shaking(mob user = null)
    {
        // notify ghosts that someone's shaking a haunted eightball
        // and inform them of the message, (hopefully a yes/no question)
        object question = await tgui_input_text(user, I18n.T("What is your question?"), I18n.T("Eightball"), max_length: CHAT_MESSAGE_MAX_LENGTH);
        selected_message = Dm.truthy(question) ? Dm.str(question) : Dm.initial(this, (obj_item_toy_eightball_haunted p) => p.selected_message);
        if (!Dm.@in(this, user.held_items))
            return FALSE;
        notify_ghosts(
            I18n.T("[] is shaking [], hoping to get an answer to \"[]\"", user.real_name, this, selected_message),
            source: this,
            header: "Magic eightball",
            click_interact: TRUE
        );
        return TRUE;
    }

    // tg: code/game/objects/items/eightball.dm:152
    public override object get_answer()
    {
        float top_amount = 0;
        object top_vote = null;

        foreach (object vote in Dm.each(votes))
        {
            float amount_of_votes = Dm.num(votes[vote]);
            if (amount_of_votes > top_amount)
            {
                top_vote = vote;
                top_amount = amount_of_votes;
            }
            //If one option actually has votes and there's a tie, pick between them 50/50
            else if (Dm.truthy(top_amount) && amount_of_votes == top_amount && prob(50))
            {
                top_vote = vote;
                top_amount = amount_of_votes;
            }
        }

        if (top_vote == null)
            top_vote = pick(votes);

        foreach (object vote in Dm.each(votes))
            votes[vote] = 0;

        voted.Cut();

        DmList top_options = possible_answers[top_vote] as DmList;
        return pick(top_options);
    }

    // Only ghosts can interact because only ghosts can open the ui
    // tg: code/game/objects/items/eightball.dm:178
    public override async DmTask<object> can_interact(mob user = null, float? require_adjacent_turf = null)
    {
        return isobserver(user);
    }

    // tg: code/game/objects/items/eightball.dm:181
    public override object ui_state(mob user = null)
    {
        return GLOB.observer_state;
    }

    // tg: code/game/objects/items/eightball.dm:184
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (!isobserver(user))
            return null;
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "EightBallVote", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/eightball.dm:192
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["shaking"] = shaking;
        data["question"] = selected_message;

        DmList answers = Dm.list();
        data["answers"] = answers;
        foreach (object vote in Dm.each(possible_answers))
        {
            DmList answer_data = Dm.list();
            answer_data["answer"] = vote;
            answer_data["amount"] = votes[vote];
            answer_data["selected"] = Dm.eq(voted[user.ckey], vote) ? TRUE : FALSE;

            answers.Add(Dm.list(answer_data));
        }
        return data;
    }

    // tg: code/game/objects/items/eightball.dm:207
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        mob user = usr;

        switch (action)
        {
            case "vote":
            {
                object selected_answer = @params["answer"];
                if (!Dm.@in(selected_answer, possible_answers))
                    return dot;
                object oldvote = voted[user.ckey];
                if (Dm.truthy(oldvote))
                    // detract their old vote
                    votes[oldvote] = Dm.num(votes[oldvote]) - 1;

                votes[selected_answer] = Dm.num(votes[selected_answer]) + 1;
                voted[user.ckey] = selected_answer;
                return TRUE;
            }
        }
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/eightball.dm:17  var/static/list/possible_answers
    internal DmList obj_item_toy_eightball__possible_answers;
}
