// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui/states/book_state.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3b, D327: the DNA infusion book): a book's window closes for a reader who cannot read or see it, updates
// only out of reach (two tiles, or any reach with telekinesis) and stays usable in hand.

/**
 * tgui state: book_state
 *
 * A state specifically intended for book or book-like objects.
 * Checks for state related to being able to read.
 */
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/tgui/states/book_state.dm:7  GLOBAL_DATUM_INIT(book_state, /datum/ui_state/book, new) (GLOBAL_RAW, __DEFINES/__globals.dm:32)
    public datum_ui_state_book book_state;

    // tg: code/modules/tgui/states/book_state.dm:7  (GLOBAL_MANAGED(book_state, new), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalbook_state()
    {
        book_state = Dm.@new<datum_ui_state_book>();
        Dm.append(ref gvars_datum_init_order, "book_state");
    }
}

// tg: code/modules/tgui/states/book_state.dm:9
public partial class datum_ui_state_book : datum_ui_state
{
    // tg: code/modules/tgui/states/book_state.dm:9
    public override async DmTask<object> can_use_topic(object src_object = null, mob user = null)
    {
        if (!Dm.truthy(user.can_read(src_object as atom)))
            return UI_CLOSE;
        if (Dm.truthy(user.is_blind()))
            return UI_CLOSE;

        if (Dm.truthy(user.incapacitated))
            return UI_UPDATE;

        if (isitem(src_object))
        {
            obj_item book = src_object as obj_item;
            if (Dm.truthy(Dm.bitand(book.resistance_flags, ON_FIRE)))
                return UI_CLOSE;
        }

        if (isatom(src_object))
        {
            atom booklike = src_object as atom;
            if (Dm.truthy(await booklike.IsReachableBy(user)))
                return UI_INTERACTIVE;
            if (get_dist(user, booklike) <= 2)
            {
                if (iscarbon(user))
                {
                    mob_living_carbon carbon_user = user as mob_living_carbon;
                    if (Dm.truthy(carbon_user.dna?.check_mutation(typeof(datum_mutation_telekinesis))))
                        return UI_INTERACTIVE;
                }
                return UI_UPDATE;
            }
            return UI_CLOSE;
        }

        return UI_INTERACTIVE;
    }
}
