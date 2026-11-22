// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/granters/_granters.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * Books that teach things.
 *
 * (Intrinsic actions like bar flinging, spells like fireball or smoke, or martial arts)
 */
// tg: code/game/objects/items/granters/_granters.dm:6
public partial class obj_item_book_granter : obj_item_book
{
    // tg: code/game/objects/items/granters/_granters.dm:26
    public override async DmTask<object> attack_self(mob user_mob = null, object modifiers = null, params object[] __extra)
    {
        mob_living user = user_mob as mob_living;
        if (Dm.truthy(reading))
        {
            to_chat(user_mob, span_warning(I18n.T("You're already reading this!")));
            return FALSE;
        }
        if (Dm.truthy(user_mob.is_blind()))
        {
            to_chat(user_mob, span_warning(I18n.T("You are blind and can't read anything!")));
            return FALSE;
        }
        if (user == null || !Dm.truthy(user.can_read(this)))
            return FALSE;
        if (!Dm.truthy(can_learn(user)))
            return FALSE;

        if (uses <= 0)
        {
            await recoil(user);
            return FALSE;
        }

        if (!Dm.truthy(await on_reading_start(user)))
            return null;
        reading = TRUE;
        for (int i = 1; i <= pages_to_mastery; i++)
        {
            if (!Dm.truthy(await turn_page(user)))
            {
                await on_reading_stopped();
                reading = FALSE;
                return null;
            }
        }
        if (Dm.truthy(await do_after(user, reading_time, this)))
        {
            uses--;
            await on_reading_finished(user);
        }
        reading = FALSE;

        return TRUE;
    }

    /// Called when the user starts to read the granter.
    // tg: code/game/objects/items/granters/_granters.dm:58
    public virtual async DmTask<object> on_reading_start(mob_living user = null)
    {
        to_chat(user, span_notice(I18n.T("You start reading []...", name)));
        return TRUE;
    }

    /// Called when the reading is interrupted without finishing.
    // tg: code/game/objects/items/granters/_granters.dm:63
    public virtual async DmTask on_reading_stopped(mob_living user = null)
    {
        to_chat(user, span_notice(I18n.T("You stop reading...")));
    }

    /// Called when the reading is completely finished. This is where the actual granting should happen.
    // tg: code/game/objects/items/granters/_granters.dm:67
    public virtual async DmTask on_reading_finished(mob_living user = null)
    {
        to_chat(user, span_notice(I18n.T("You finish reading []!", name)));
    }

    /// The actual "turning over of the page" flavor bit that happens while someone is reading the granter.
    // tg: code/game/objects/items/granters/_granters.dm:71
    public virtual async DmTask<object> turn_page(mob_living user = null)
    {
        playsound(user, pick(book_sounds), 30, TRUE);

        if (!Dm.truthy(await do_after(user, reading_time, this)))
            return FALSE;

        to_chat(user, span_notice(Dm.text("[]", Dm.truthy(length(remarks)) ? pick(remarks) : I18n.T("You keep reading..."))));
        return TRUE;
    }

    /// Effects that occur whenever the book is read when it has no uses left.
    // tg: code/game/objects/items/granters/_granters.dm:81
    public virtual async DmTask recoil(mob_living user = null)
    {
        return;
    }

    /// Checks if the user can learn whatever this granter... grants
    // tg: code/game/objects/items/granters/_granters.dm:84
    public virtual object can_learn(mob_living user = null)
    {
        return TRUE;
    }
}

// Generic action giver
// tg: code/game/objects/items/granters/_granters.dm:88
public partial class obj_item_book_granter_action : obj_item_book_granter
{
    // tg: code/game/objects/items/granters/_granters.dm:94
    public override object can_learn(mob_living user = null)
    {
        if (granted_action == null)
            throw Dm.CRASH(Dm.text("Someone attempted to learn [], which did not have an action set.", type), "code/game/objects/items/granters/_granters.dm", 96);
        if (Dm.locate_type(granted_action, user.actions) != null)
        {
            to_chat(user, span_warning(I18n.T("You already know all about []!", action_name)));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/game/objects/items/granters/_granters.dm:102
    public override async DmTask<object> on_reading_start(mob_living user = null)
    {
        to_chat(user, span_notice(I18n.T("You start reading about []...", action_name)));
        return TRUE;
    }

    // tg: code/game/objects/items/granters/_granters.dm:106
    public override async DmTask on_reading_finished(mob_living user = null)
    {
        to_chat(user, span_notice(I18n.T("You feel like you've got a good handle on []!", action_name)));
        // Action goes on the mind as the user actually learns the thing in your brain
        datum_action new_action = await Dm.new_async(granted_action, (object)user.mind ?? user) as datum_action;
        await new_action.Grant(user);
    }
}
