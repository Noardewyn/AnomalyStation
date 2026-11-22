// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/granters/magic/_spell_granter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps J2, D298): the spell granter base; the random and true random granters stay with the wizard.

// tg: code/game/objects/items/granters/magic/_spell_granter.dm:1
public partial class obj_item_book_granter_action_spell : obj_item_book_granter_action
{
    // tg: code/game/objects/items/granters/magic/_spell_granter.dm:3
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_ITEM_MAGICALLY_CHARGED, nameof(on_magic_charge));
        return dot;
    }

    /**
     * Signal proc for [COMSIG_ITEM_MAGICALLY_CHARGED]
     *
     * Refreshes uses on our spell granter, or make it quicker to read if it's already infinite use
     */
    // tg: code/game/objects/items/granters/magic/_spell_granter.dm:12
    [SignalHandler]
    public virtual object on_magic_charge(datum source = null, datum_action_cooldown_spell spell = null, mob_living caster = null)
    {
        // What're the odds someone uses 2000 uses of an infinite use book?
        if (uses >= INFINITY - 2000)
        {
            to_chat(caster, span_notice(I18n.T("This book is infinite use and can't be recharged, yet the magic has improved it somehow...")));
            pages_to_mastery = Dm.max(pages_to_mastery - 1, 1);
            return Dm.bitor(COMPONENT_ITEM_CHARGED, COMPONENT_ITEM_BURNT_OUT);
        }

        if (prob(80))
        {
            Waiting.NoSleep(caster.dropItemToGround(this, TRUE));
            visible_message(span_warning(I18n.T("[] catches fire and burns to ash!", this)));
            Dm.@new<obj_effect_decal_cleanable_ash>(drop_location());
            qdel(this);
            return COMPONENT_ITEM_BURNT_OUT;
        }

        uses++;
        return COMPONENT_ITEM_CHARGED;
    }

    // tg: code/game/objects/items/granters/magic/_spell_granter.dm:32
    public override object can_learn(mob_living user = null)
    {
        if (granted_action == null)
            throw Dm.CRASH(Dm.text("Someone attempted to learn [], which did not have a spell set.", type), "code/game/objects/items/granters/magic/_spell_granter.dm", 34);
        if (Dm.locate_type(granted_action, user.actions) != null)
        {
            if (HAS_MIND_TRAIT(user, TRAIT_MAGICALLY_GIFTED))
                to_chat(user, span_warning(I18n.T("You're already far more versed in the spell [] than this flimsy how-to book can provide!", action_name)));
            else
                to_chat(user, span_warning(I18n.T("You've already know the spell []!", action_name)));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/game/objects/items/granters/magic/_spell_granter.dm:44
    public override async DmTask<object> on_reading_start(mob_living user = null)
    {
        to_chat(user, span_notice(I18n.T("You start reading about casting []...", action_name)));
        return TRUE;
    }

    // tg: code/game/objects/items/granters/magic/_spell_granter.dm:48
    public override async DmTask on_reading_finished(mob_living user = null)
    {
        to_chat(user, span_notice(I18n.T("You feel like you've experienced enough to cast []!", action_name)));
        datum_action_cooldown_spell new_spell = await Dm.new_async(granted_action, (object)user.mind ?? user) as datum_action_cooldown_spell;
        await new_spell.Grant(user);
        user.log_message(Dm.text("learned the spell [] ([])", action_name, new_spell), LOG_ATTACK, color: "orange");
        if (uses <= 0)
            user.visible_message(span_warning(I18n.T("[] glows dark for a second!", this)));
    }

    // tg: code/game/objects/items/granters/magic/_spell_granter.dm:56
    public override async DmTask recoil(mob_living user = null)
    {
        user.visible_message(span_warning(I18n.T("[] glows in a black light!", this)));
    }
}
