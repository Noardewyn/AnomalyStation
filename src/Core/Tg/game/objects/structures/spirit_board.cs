// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/spirit_board.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST4 (D333): the file whole. The spirit board (one in a hundred is a luigi board): a ghost or a living player picks the
// planchette's letter from tg's list in a dark spot (light at most 0.2) with two attentive players around it, three to five seconds
// apart (a second more for the last user); the first pick tells the ghosts. The var block is generated (D111).

// tg: code/game/objects/structures/spirit_board.dm:1
public partial class obj_structure_spirit_board : obj_structure
{
    // tg: code/game/objects/structures/spirit_board.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(1))
            name = "luigi board";
        planchette = ghosty_options[1];
        return dot;
    }

    // tg: code/game/objects/structures/spirit_board.dm:32
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList ?? Dm.list();
        if (Dm.truthy(planchette))
            Dm.append(dot, span_notice(I18n.T("The planchette is currently at the letter \"[]\".", planchette)));
        else
            Dm.append(dot, span_notice(I18n.T("The planchette is in the middle of the board on no particular letter.")));
        return dot;
    }

    // tg: code/game/objects/structures/spirit_board.dm:39
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        await spirit_board_pick_letter(user);
        return TRUE;
    }

    // tg: code/game/objects/structures/spirit_board.dm:46
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        object dot = await base.attack_ghost(user);
        if (Dm.truthy(dot))
            return dot;
        await spirit_board_pick_letter(user);
        return TRUE;
    }

    // tg: code/game/objects/structures/spirit_board.dm:53
    public virtual async DmTask spirit_board_pick_letter(mob ghost = null)
    {
        if (!Dm.truthy(spirit_board_checks(ghost)))
            return;

        if (Dm.truthy(virgin))
        {
            virgin = FALSE;
            notify_ghosts(
                I18n.T("Someone has begun playing with \\a [] in []!", this, get_area(this)),
                source: this,
                header: "Spirit board");
        }

        object new_planchette = await tgui_input_list(ghost, I18n.T("Choose the letter."), I18n.T("Seance!"), ghosty_options);
        if (new_planchette == null)
            return;
        if (!Dm.truthy(await Adjacent(ghost)) || !(next_use <= world.time)) // COOLDOWN_FINISHED(src, next_use), inline (rules 11.1 (a))
            return;
        planchette = new_planchette;
        ghost.log_message(Dm.text("picked a letter on [], which was \"[]\".", this, planchette), LOG_GAME);
        next_use = world.time + Dm.num(rand(3 * SECONDS, 5 * SECONDS)); // COOLDOWN_START(src, next_use, ...), inline
        lastuser = ghost.ckey;
        visible_message(
            message: span_notice(I18n.T("The planchette slowly moves... and stops at the letter \"[]\".", planchette)),
            blind_message: span_hear(I18n.T("You hear a scraping sound...")),
            vision_distance: 2);
    }

    // tg: code/game/objects/structures/spirit_board.dm:80
    public virtual object spirit_board_checks(mob ghost = null)
    {
        float cd_penalty = Dm.eq(ghost.ckey, lastuser) ? 1 * SECONDS : 0 * SECONDS; //Give some other people a chance, hog.

        if (next_use - cd_penalty > world.time)
            return FALSE; //No feedback here, hiding the cooldown a little makes it harder to tell who's really picking letters.

        turf play_turf = get_turf(this);
        if (Dm.truthy(play_turf?.check_lumcount_above(0.2f)))
        {
            to_chat(ghost, span_warning(I18n.T("It's too bright here to use []!", this)));
            return FALSE;
        }

        if (required_user_count > 0)
        {
            float users_in_range = 0;
            foreach (mob_living player in Dm.each<mob_living>(orange(1, this)))
            {
                if (player.ckey == null || player.client == null)
                    continue;

                if (Dm.truthy(player.client?.is_afk()) || HAS_TRAIT(player, TRAIT_HANDS_BLOCKED)) //no playing with braindeads or corpses or handcuffed dudes.
                {
                    to_chat(ghost, span_warning(I18n.T("[] doesn't seem to be paying attention...", player)));
                    continue;
                }

                users_in_range++;
            }

            if (users_in_range < required_user_count)
            {
                to_chat(ghost, span_warning(I18n.T("There aren't enough people around to use []!", this)));
                return FALSE;
            }
        }

        return TRUE;
    }
}
