// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/arcade/amputation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: Mediborg's Amputation Adventure and its festive kind. The var blocks are generated (D111).

// tg: code/game/machinery/computer/arcade/amputation.dm:1
public partial class obj_machinery_computer_arcade_amputation : obj_machinery_computer_arcade
{
    // tg: code/game/machinery/computer/arcade/amputation.dm:9
    public override DmTask<object> attack_tk(mob user = null)
    {
        return DmTask<object>.FromResult(null); //that's a pretty damn big guillotine
    }

    // tg: code/game/machinery/computer/arcade/amputation.dm:12
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (!iscarbon(user))
            return dot;
        var player = (mob_living)user;
        to_chat(player, span_warning(I18n.T("You move your hand towards the machine, and begin to hesitate as a bloodied guillotine emerges from inside of it...")));
        await player.played_game();
        var chopchop = player.get_active_hand() as obj_item_bodypart;
        if (Dm.truthy(await do_after(player, 5 * SECONDS, target: this, extra_checks: Dm.@new<datum_callback>(this, nameof(do_they_still_have_that_hand), player, chopchop))))
        {
            playsound(this, Dm.resource("sound/items/weapons/slice.ogg"), 25, TRUE, -1);
            to_chat(player, span_userdanger(I18n.T("The guillotine drops on your arm, and the machine sucks it in!")));
            await chopchop.dismember();
            qdel(chopchop);
            player.mind?.adjust_experience(typeof(datum_skill_gaming), 100);
            player.won_game();
            victory_tickets(rand(6, 10));
            return dot;
        }
        if (!Dm.truthy(do_they_still_have_that_hand(player, chopchop)))
        {
            to_chat(player, span_warning(I18n.T("The guillotine drops, but your hand seems to be gone already!")));
            playsound(this, Dm.resource("sound/items/weapons/slice.ogg"), 25, TRUE, -1);
        }
        else
            to_chat(player, span_notice(I18n.T("You (wisely) decide against putting your hand in the machine.")));
        player.lost_game();
        return dot;
    }

    ///Makes sure the user still has their starting hand, preventing the user from pulling the arm out and still getting prizes.
    // tg: code/game/machinery/computer/arcade/amputation.dm:37
    public virtual object do_they_still_have_that_hand(mob user = null, obj_item_bodypart chopchop = null)
    {
        if (QDELETED(chopchop) || chopchop.owner != user)
            return FALSE;
        return TRUE;
    }
}
