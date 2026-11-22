// Translated from tgstation (snapshot 1d99bd7f): code/modules/mafia/roles/roles.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): a Mafia role: its abilities and the day vote, its body and the panel action, its messages, the
// game's chat and dead chat, the player put in the body, the kill, the greeting and the reveal with the role's outfit. The var block
// is generated (D111). tg's callers pass FALSE as the attacker of a kill (the clown's prank), so the attacker is an object (rules 3.3).

// tg: code/modules/mafia/roles/roles.dm:1
public partial class datum_mafia_role : datum
{
    // tg: code/modules/mafia/roles/roles.dm:55
    [DmParams("new_game")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        datum_mafia_controller new_game = Dm.arg(__extra, 0) as datum_mafia_controller;
        mafia_game_controller = new_game;
        mafia_panel = Dm.@new<datum_action_innate_mafia_panel>(null, new_game);
        foreach (Type abilities in Dm.each_cast<Type>(Dm.add(role_unique_actions, typeof(datum_mafia_ability_voting))))
        {
            Dm.append(ref role_unique_actions, Dm.@new(abilities, this));
            Dm.remove(ref role_unique_actions, abilities);
        }
    }

    // tg: code/modules/mafia/roles/roles.dm:63
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(body, Dm.list(COMSIG_MOB_SAY, COMSIG_MOB_DEADSAY));
        QDEL_NULL(ref mafia_alert);
        QDEL_NULL(ref mafia_panel);
        QDEL_LIST(role_unique_actions);
        //we null these instead of qdel because Mafia controller's mapdeleter deletes it all.
        assigned_landmark = null;
        body = null;
        role_messages.Cut();
        return base.Destroy(force);
    }

    // tg: code/modules/mafia/roles/roles.dm:74
    public virtual async DmTask register_body(mob_living_carbon_human new_body = null)
    {
        if (body != null)
        {
            UnregisterSignal(new_body, Dm.list(COMSIG_MOB_SAY, COMSIG_MOB_DEADSAY));
            await mafia_panel.Remove(body);
        }
        body = new_body;
        RegisterSignal(new_body, COMSIG_MOB_SAY, nameof(handle_speech));
        RegisterSignal(new_body, COMSIG_MOB_DEADSAY, nameof(handle_speech_dead));
        await mafia_panel.Grant(new_body);
    }

    /**
     * send_message_to_player
     *
     * Sends a message to a player, checking if they are playing through a PDA or not.
     */
    // tg: code/modules/mafia/roles/roles.dm:91
    public virtual void send_message_to_player(object message = null, float? balloon_alert = null)
    {
        balloon_alert ??= FALSE;
        if (player_pda != null)
        {
            Dm.append(ref role_messages, message);
            return;
        }
        if (Dm.truthy(balloon_alert))
        {
            body.balloon_alert(body, message);
            return;
        }
        to_chat(body, message);
    }

    /**
     * handle_speech
     *
     * Handles Mafia roles talking in chat.
     * First we'll go through their abilities for Ability-specific speech,
     * if none affects it, we will go to day chat (if it is indeed day).
     */
    // tg: code/modules/mafia/roles/roles.dm:107
    [SignalHandler]
    public virtual void handle_speech(datum source = null, DmList speech_args = null)
    {
        foreach (datum_mafia_ability abilities in Dm.each_cast<datum_mafia_ability>(role_unique_actions))
        {
            if (Dm.truthy(abilities.handle_speech(source, speech_args)))
                return;
        }
        if (mafia_game_controller.phase == MAFIA_PHASE_NIGHT)
            return;
        string message = I18n.T("[]: []", source, html_decode(speech_args[SPEECH_MESSAGE]));
        mafia_game_controller.send_message(message, log_only: TRUE);
    }

    ///Same as handle_speech, but for dead players.
    // tg: code/modules/mafia/roles/roles.dm:118
    [SignalHandler]
    public virtual object handle_speech_dead(datum source = null, object message = null)
    {
        string message_sent = span_changeling(I18n.T("<b>\\[DEAD CHAT\\] []</b>: []", source, message));
        mafia_game_controller.send_message(message_sent, team: MAFIA_TEAM_DEAD);
        return MOB_DEADSAY_SIGNAL_INTERCEPT;
    }

    /**
     * Puts the player in their body and keeps track of their previous one to put them back in later.
     * Adds the playing_mafia trait so people examining them will know why they're currently lacking a soul.
     */
    // tg: code/modules/mafia/roles/roles.dm:128
    public virtual async DmTask put_player_in_body(client player = null)
    {
        if (player.mob.mind != null)
        {
            await body.AddComponent(
                typeof(datum_component_temporary_body),
                Dm.named("old_mind", player.mob.mind));
        }
        body.PossessByPlayer(player.key);
        ADD_TRAIT(body, TRAIT_CORPSELOCKED, MAFIA_TRAIT);
    }

    /**
     * Tests kill immunities, if nothing prevents the kill, kills this role.
     *
     * Does not count as visiting, see visit proc.
     */
    // tg: code/modules/mafia/roles/roles.dm:142
    public virtual async DmTask<object> kill(datum_mafia_controller game = null, object attacker = null, float? lynch = null)
    {
        lynch ??= FALSE;
        if (game_status == MAFIA_DEAD)
            return FALSE;
        if (Dm.truthy(attacker) && Dm.truthy(Dm.bitand((attacker as datum_mafia_role).role_flags, ROLE_ROLEBLOCKED)))
            return FALSE;
        if (Dm.truthy(Dm.bitand(SEND_SIGNAL(COMSIG_MAFIA_ON_KILL, Dm.list(this, game, attacker, lynch)), MAFIA_PREVENT_KILL)))
            return FALSE;
        game_status = MAFIA_DEAD;
        //can now hear dead chat speaking.
        team = Dm.num(Dm.bitor(team, MAFIA_TEAM_DEAD));
        await body.death();
        if (Dm.truthy(lynch))
            await reveal_role(game, verbose: TRUE);
        Dm.remove(ref game.living_roles, this);
        return TRUE;
    }

    // tg: code/modules/mafia/roles/roles.dm:158
    public virtual void greet()
    {
        mafia_alert = Dm.@new<atom_movable_screen_mafia_popup>(body, this);
        Dm.output_to(body, sound(Dm.resource("sound/ambience/misc/ambifailure.ogg"))); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
        to_chat(body, span_danger(I18n.T("You are the [].", name)));
        to_chat(body, span_danger(Dm.text("[]", desc)));
        switch (Dm.num(team))
        {
            case MAFIA_TEAM_MAFIA:
                to_chat(body, span_danger(I18n.T("You and your co-conspirators win if you outnumber crewmembers.")));
                break;
            case MAFIA_TEAM_TOWN:
                to_chat(body, span_danger(I18n.T("You are a crewmember. Find out and lynch the changelings!")));
                break;
            case MAFIA_TEAM_SOLO:
                to_chat(body, span_danger(I18n.T("You are not aligned to town or mafia. Accomplish your own objectives!")));
                break;
        }
        to_chat(body, I18n.T("<span class='warningplain'><b>Be sure to read <a href=\"https://tgstation13.org/wiki/Mafia\">the wiki page</a> to learn more, if you have no idea what's going on.</b></span>"));
    }

    // tg: code/modules/mafia/roles/roles.dm:172
    public virtual async DmTask reveal_role(datum_mafia_controller game = null, float? verbose = null)
    {
        verbose ??= FALSE;
        if (Dm.truthy(Dm.bitand(role_flags, ROLE_REVEALED)))
            return;
        if (Dm.truthy(verbose))
            game.send_message(I18n.T("<span class='big bold notice'>It is revealed that the true role of [] [] []!</span>", body, game_status == MAFIA_ALIVE ? I18n.T("is") : I18n.T("was"), name));
        DmList oldoutfit = body.get_equipped_items() as DmList;
        foreach (object thing in Dm.each(oldoutfit))
            qdel(thing);
        special_reveal_equip(game);
        await body.equipOutfit(revealed_outfit);
        role_flags = Dm.num(Dm.bitor(role_flags, ROLE_REVEALED));
    }

    // tg: code/modules/mafia/roles/roles.dm:184
    public virtual void special_reveal_equip(datum_mafia_controller game = null)
    {
        return;
    }

    // tg: code/modules/mafia/roles/roles.dm:187
    public virtual void show_help(object clueless = null)
    {
        DmList result = Dm.list();
        string team_desc = "";
        string team_span = "";
        bool the = true;
        switch (Dm.num(team))
        {
            case MAFIA_TEAM_TOWN:
                team_desc = I18n.T("Town");
                team_span = "nicegreen";
                break;
            case MAFIA_TEAM_MAFIA:
                team_desc = I18n.T("Mafia");
                team_span = "red";
                break;
            case MAFIA_TEAM_SOLO:
                team_desc = I18n.T("Nobody");
                team_span = "comradio";
                the = false;
                break;
        }
        Dm.append(ref result, span_notice(I18n.T("The [] is aligned with []<span class='[]'>[]</span>", span_bold(Dm.text("[]", name)), the ? I18n.T("the ") : "", team_span, team_desc)));
        Dm.append(ref result, Dm.text("<span class='bold notice'>\"[]\"</span>", Dm.initial(this, (datum_mafia_role r) => r.desc)));
        Dm.append(ref result, span_notice(I18n.T("[] wins when they []", name, win_condition)));
        to_chat(clueless, result.Join("</br>"));
    }
}
