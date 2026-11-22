// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/votingbox.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST5 (D333): the file whole. The voting box: an ID card swiped on it registers its operator; its window is tg's browse() page
// (the client's Browser window, its links tg's Topic): the operator toggles voting ("votebox_active", "votebox_maint"), sets the
// description (tg's text input), requires one vote per ID, resets the voters, draws a random ballot, shreds the ballots and prints
// the tally (a paper "Voting Results", once a minute, sorted by votes, skipping the slips over 255 characters and the tallies); a
// paper posted into it while voting is a ballot; a crowbar empties it in maintenance mode; a wrench (un)anchors it in four seconds.
// The var block is generated (D111).

// tg: code/game/objects/structures/votingbox.dm:1-2 (file-local, #undef at :227-228)
public partial class obj_structure_votebox
{
    internal const float VOTE_TEXT_LIMIT = 255;
    internal const float MAX_VOTES = 255;
}

// tg: code/game/objects/structures/votingbox.dm:4
public partial class obj_structure_votebox : obj_structure
{
    // tg: code/game/objects/structures/votingbox.dm:22
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_card_id)
        {
            if (owner != null)
                return ITEM_INTERACT_BLOCKING;

            await register_owner((obj_item_card_id)tool, user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_paper)
        {
            if (!Dm.truthy(voting_active))
            {
                to_chat(user, span_warning(I18n.T("[] is in maintenance mode. Voting is not possible at the moment.", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            await apply_vote((obj_item_paper)tool, user);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/objects/structures/votingbox.dm:39
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        await base.interact(user, __extra);
        await ui_interact(user);
        return null;
    }

    // tg: code/game/objects/structures/votingbox.dm:43
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);

        DmList dat = Dm.list();
        if (owner == null)
            Dm.append(ref dat, I18n.T("<h1> Unregistered. Swipe ID card to register as voting box operator </h1>"));
        Dm.append(ref dat, Dm.text("<h1>[]</h1>", vote_description));
        if (Dm.truthy(is_operator(user as mob_living)))
        {
            Dm.append(ref dat, I18n.T("Voting: <a href='byond://?src=[];act=toggle_vote'>[]</a><br>", REF(this), Dm.truthy(voting_active) ? I18n.T("Active") : I18n.T("Maintenance Mode")));
            Dm.append(ref dat, I18n.T("Set Description: <a href='byond://?src=[];act=set_desc'>Set Description</a><br>", REF(this)));
            Dm.append(ref dat, I18n.T("One vote per ID: <a href='byond://?src=[];act=toggle_auth'>[]</a><br>", REF(this), Dm.truthy(id_auth) ? I18n.T("Yes") : I18n.T("No")));
            Dm.append(ref dat, I18n.T("Reset voted ID's: <a href='byond://?src=[];act=reset_voted'>Reset</a><br>", REF(this)));
            Dm.append(ref dat, I18n.T("Draw random vote: <a href='byond://?src=[];act=raffle'>Raffle</a><br>", REF(this)));
            Dm.append(ref dat, I18n.T("Shred votes: <a href='byond://?src=[];act=shred'>Shred</a><br>", REF(this)));
            Dm.append(ref dat, I18n.T("Tally votes: <a href='byond://?src=[];act=tally'>Tally</a><br>", REF(this)));
        }

        datum_browser popup = Dm.@new<datum_browser>(user, "votebox", I18n.T("Voting Box"), 300, 300);
        popup.set_content(dat.Join(""));
        popup.open();
        return dot;
    }

    // tg: code/game/objects/structures/votingbox.dm:63
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        if (Dm.truthy(await base.Topic(href, href_list)))
            return null;

        mob user = usr;
        if (!Dm.truthy(await can_interact(user)))
            return null;
        if (!Dm.truthy(is_operator(user as mob_living)))
        {
            to_chat(user, span_warning(I18n.T("Voting box operator authorization required!")));
            return null;
        }

        if (Dm.truthy(href_list["act"]))
        {
            object sw__1 = href_list["act"];
            if (Dm.eq(sw__1, "toggle_vote"))
            {
                voting_active = Dm.not(voting_active);
                update_appearance();
            }
            else if (Dm.eq(sw__1, "toggle_auth"))
                id_auth = Dm.not(id_auth);
            else if (Dm.eq(sw__1, "reset_voted"))
            {
                if (voted != null)
                    voted.Cut();
                to_chat(user, span_notice(I18n.T("You reset the voter buffer. Everyone can vote again.")));
            }
            else if (Dm.eq(sw__1, "raffle"))
                await raffle(user);
            else if (Dm.eq(sw__1, "shred"))
                shred(user);
            else if (Dm.eq(sw__1, "tally"))
                await print_tally(user);
            else if (Dm.eq(sw__1, "set_desc"))
                await set_description(user);
            await interact(user);
        }
        return null;
    }

    // tg: code/game/objects/structures/votingbox.dm:95
    public virtual async DmTask register_owner(obj_item_card_id I = null, mob_living user = null)
    {
        owner = I;
        to_chat(user, span_notice(I18n.T("You register [] to your ID card.", this)));
        await ui_interact(user);
    }

    // tg: code/game/objects/structures/votingbox.dm:100
    public virtual async DmTask set_description(mob user = null)
    {
        object new_description = await tgui_input_text(user, I18n.T("Enter a new description"), I18n.T("Vote Description"), vote_description, multiline: TRUE, max_length: MAX_DESC_LEN);
        if (Dm.truthy(new_description))
            vote_description = Dm.str(new_description);
    }

    // tg: code/game/objects/structures/votingbox.dm:105
    public virtual object is_operator(mob_living user = null)
    {
        return Dm.num(user != null && Dm.eq(user?.get_idcard(), owner));
    }

    // tg: code/game/objects/structures/votingbox.dm:108
    public virtual async DmTask apply_vote(obj_item_paper I = null, mob_living user = null)
    {
        obj_item_card_id voter_card = user.get_idcard() as obj_item_card_id;
        if (Dm.truthy(id_auth))
        {
            if (voter_card == null)
            {
                to_chat(user, span_warning(I18n.T("[] requires a valid ID card to vote!", this)));
                return;
            }
            if (voted != null && Dm.@in(voter_card, voted))
            {
                to_chat(user, span_warning(I18n.T("[] allows only one vote per person.", this)));
                return;
            }
        }
        if (Dm.truthy(await user.transferItemToLoc(I, this)))
        {
            if (voted == null)
                voted = Dm.list();
            Dm.append(ref voted, voter_card);
            to_chat(user, span_notice(I18n.T("You cast your vote.")));
        }
    }

    // tg: code/game/objects/structures/votingbox.dm:123
    public virtual object valid_vote(obj_item_paper voting_slip = null)
    {
        if (Dm.num(voting_slip.get_total_length()) > VOTE_TEXT_LIMIT)
            return FALSE;

        foreach (datum_paper_input text in Dm.each_cast<datum_paper_input>(voting_slip.raw_text_inputs))
        {
            if (Dm.truthy(findtext(text.raw_text, "<h1>Voting Results:</h1><hr><ol>")))
                return FALSE;
        }
        return TRUE;
    }

    // tg: code/game/objects/structures/votingbox.dm:132
    public virtual void shred(mob user = null)
    {
        foreach (obj_item_paper P in Dm.each<obj_item_paper>(contents))
            qdel(P);
        to_chat(user, span_notice(I18n.T("You shred the current votes.")));
    }

    // tg: code/game/objects/structures/votingbox.dm:137
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool, time: 4 * SECONDS);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/votingbox.dm:142
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        await base.crowbar_act(user, tool);
        if (Dm.truthy(voting_active))
        {
            to_chat(user, span_warning(I18n.T("You can only retrieve votes if maintenance mode is active!")));
            return FALSE;
        }
        await dump_contents();
        to_chat(user, span_notice(I18n.T("You open vote retrieval hatch and dump all the votes.")));
        return TRUE;
    }

    // tg: code/game/objects/structures/votingbox.dm:151
    public override async DmTask dump_contents(params object[] __extra)
    {
        atom droppoint = drop_location() as atom;
        foreach (atom_movable AM in Dm.each<atom_movable>(contents.Copy()))
            await AM.forceMove(droppoint);
    }

    // tg: code/game/objects/structures/votingbox.dm:156
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        await dump_contents();
    }

    // tg: code/game/objects/structures/votingbox.dm:159
    public virtual async DmTask raffle(mob user = null)
    {
        DmList options = Dm.list();
        foreach (obj_item_paper P in Dm.each<obj_item_paper>(contents))
            Dm.append(ref options, P);
        if (!Dm.truthy(length(options)))
            to_chat(user, span_warning(I18n.T("[] is empty!", this)));
        else
        {
            obj_item_paper P = pick(options) as obj_item_paper;
            await user.put_in_hands(P);
            to_chat(user, span_notice(I18n.T("[] pops out random vote.", this)));
        }
    }

    // tg: code/game/objects/structures/votingbox.dm:170
    public virtual async DmTask print_tally(mob user = null)
    {
        DmList results = Dm.list();
        float i = 0;
        foreach (obj_item_paper paper_content in Dm.each<obj_item_paper>(contents))
        {
            if (i++ > MAX_VOTES)
                break;
            if (!Dm.truthy(valid_vote(paper_content)))
                continue;

            string full_vote_text = "";
            foreach (datum_paper_input text in Dm.each_cast<datum_paper_input>(paper_content.raw_text_inputs))
                full_vote_text += Dm.text("[]", text.raw_text);

            if (!Dm.truthy(results[full_vote_text]))
                results[full_vote_text] = 1;
            else
                results[full_vote_text] = Dm.num(results[full_vote_text]) + 1;
        }

        sortTim(results, "/proc/" + nameof(GlobalProcs.cmp_numeric_dsc), associative: TRUE);
        if (!(vote_print_cooldown <= world.time)) // COOLDOWN_FINISHED(src, vote_print_cooldown), inline (rules 11.1 (a))
            return;
        vote_print_cooldown = world.time + 60 * SECONDS; // COOLDOWN_START(src, vote_print_cooldown, 60 SECONDS)
        obj_item_paper vote_tally_paper = Dm.@new<obj_item_paper>(drop_location());
        DmList tally = Dm.list();
        Dm.append(ref tally, "\n\t\t<style>\n\t\t\t.vote_box_content{\n\t\t\t\tmax-width:250px;\n\t\t\t\tdisplay:inline-block;\n\t\t\t\toverflow:hidden;\n\t\t\t\ttext-overflow:ellipsis;\n\t\t\t\twhite-space:nowrap;\n\t\t\t\tvertical-align:bottom\n\t\t\t}\n\t\t\t.vote_box_content br {\n\t\t\t\tdisplay: none;\n\t\t\t}\n\t\t\t.vote_box_content hr {\n\t\t\t\tdisplay: none;\n\t\t\t}\n\t\t</style>");

        Dm.append(ref tally, I18n.T("<h1>Voting Results:</h1><br/><h2>[]</h2><hr><ol>", vote_description));
        foreach (object option in Dm.each(results))
            Dm.append(ref tally, I18n.T("<li>\"<span class='vote_box_content'>[]</span>\" - [] Vote[].</li>", option, results[option], Dm.num(results[option]) > 1 ? "s" : ""));
        Dm.append(ref tally, "</ol>");

        vote_tally_paper.add_raw_text(tally.Join(""));
        vote_tally_paper.name = I18n.T("Voting Results");
        vote_tally_paper.update_appearance();
        await user.put_in_hands(vote_tally_paper);
        to_chat(user, span_notice(I18n.T("[] prints out the voting tally.", this)));
    }

    // tg: code/game/objects/structures/votingbox.dm:223
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("votebox_[]", Dm.truthy(voting_active) ? "active" : "maint");
        return await base.update_icon_state(__extra);
    }
}
