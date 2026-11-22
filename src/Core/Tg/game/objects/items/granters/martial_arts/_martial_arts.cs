// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/granters/martial_arts/_martial_arts.dm and
// code/game/objects/items/granters/martial_arts/sleeping_carp.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X3a (D316): the martial arts' books (read, they teach their art to the reader's mind) and the sleeping carp
// scroll, whole. The var blocks are generated (D111).

// tg: code/game/objects/items/granters/martial_arts/_martial_arts.dm:1
public partial class obj_item_book_granter_martial : obj_item_book_granter
{
    // tg: code/game/objects/items/granters/martial_arts/_martial_arts.dm:9
    public override object can_learn(mob_living user = null)
    {
        if (martial == null)
            throw new DmRuntimeException(Dm.text("Someone attempted to learn [], which did not have a martial arts set.", GetType()));
        if (!isliving(user))
            return FALSE;
        if (Dm.locate_type(martial, user.martial_arts) != null)
        {
            to_chat(user, span_warning(I18n.T("You already know []!", martial_name)));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/game/objects/items/granters/martial_arts/_martial_arts.dm:19
    public override async DmTask<object> on_reading_start(mob_living user = null)
    {
        to_chat(user, span_notice(I18n.T("You start reading about []...", martial_name)));
        return TRUE;
    }

    // tg: code/game/objects/items/granters/martial_arts/_martial_arts.dm:23
    public override async DmTask on_reading_finished(mob_living user = null)
    {
        if (user.mind != null)
        {
            if (!Dm.truthy(await user.mind.AddComponent(typeof(datum_component_mindbound_martial_arts), martial)))
            {
                to_chat(user, span_warning(I18n.T("You attempt to learn [] from [], but it doesn't stick.", martial_name, this)));
                uses += 1; // Return the use
                return;
            }
        }
        else
        {
            var martial_art = Dm.@new(martial, user) as datum_martial_art;
            await martial_art.teach(user);
        }

        to_chat(user, Dm.text("[]", greet));
        user.log_message(Dm.text("learned the martial art [] ([])", martial_name, martial), LOG_ATTACK, color: "orange");
    }
}

// tg: code/game/objects/items/granters/martial_arts/sleeping_carp.dm:1
public partial class obj_item_book_granter_martial_carp : obj_item_book_granter_martial
{
    // tg: code/game/objects/items/granters/martial_arts/sleeping_carp.dm:24
    public override async DmTask on_reading_finished(mob_living user = null)
    {
        await base.on_reading_finished(user);
        update_appearance();
    }

    // tg: code/game/objects/items/granters/martial_arts/sleeping_carp.dm:28
    public override object update_appearance(float? updates = null)
    {
        object dot = base.update_appearance(updates);
        if (uses <= 0)
        {
            name = "empty scroll";
            desc = "It's completely blank.";
            icon_state = "blankscroll";
        }
        else
        {
            name = Dm.initial(this, (obj_item_book_granter_martial_carp c) => c.name);
            desc = Dm.initial(this, (obj_item_book_granter_martial_carp c) => c.desc);
            icon_state = Dm.initial(this, (obj_item_book_granter_martial_carp c) => c.icon_state);
        }
        return dot;
    }
}
