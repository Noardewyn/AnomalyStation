// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/bitflag_lists.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M3 P4, with icon smoothing): GLOB.bitflag_lists and SET_SMOOTHING_GROUPS, which docs/ai/specs/m3-ownership.md
// lists under __DEFINES/icon_smoothing.cs but tg defines here (rules 11.1: the member lives in the mirror of its .dm).

// tg: code/__HELPERS/bitflag_lists.dm:1  GLOBAL_LIST_EMPTY(bitflag_lists)
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/__HELPERS/bitflag_lists.dm:1  (GLOBAL_RAW(/list/bitflag_lists), __DEFINES/__globals.dm:32)
    public DmList bitflag_lists;

    // tg: code/__HELPERS/bitflag_lists.dm:1  (GLOBAL_MANAGED(bitflag_lists, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalbitflag_lists()
    {
        bitflag_lists = Dm.list();
        Dm.append(ref gvars_datum_init_order, "bitflag_lists");
    }
}

public static partial class GlobalProcs
{
    /**
     * System for storing bitflags past the 24 limit, making use of an associative list.
     *
     * Macro converts a string of integers into an associative list of bitflag entries for quicker comparison.
     * Example: "0, 4, 26, 32" => list( "0" = ( (1<<0) | (1<<4) ), "1" = ( (1<<2) | (1<<8) ) )
     * Lists are cached into a global list of lists to avoid identical duplicates.
     * This system makes value comparisons faster than pairing every element of one list with every element of the other for evaluation.
     *
     * Arguments:
     * * read_from - String of integers.
     * * set_into - Variable to set equal to a list of integers
     */
    // tg: code/__HELPERS/bitflag_lists.dm:15  #define SET_SMOOTHING_GROUPS(read_from, set_into)  (a member, rules 11.1: set_into by ref)
    public static void SET_SMOOTHING_GROUPS(object read_from, ref object set_into)
    {
        object txt_signature = read_from;
        if ((set_into = GLOB.bitflag_lists[txt_signature]) == null)
        {
            DmList new_bitflag_list = Dm.list();
            // UNWRAP_SMOOTHING_GROUPS(txt_signature, decoded) (__DEFINES/icon_smoothing.dm:261), expanded inline (rules 11.1 (a):
            // it names the caller's local): json_decode("\[[txt_signature]0\]"); decoded.len--;
            DmList decoded = json_decode(Dm.text("\\[[]0\\]", txt_signature)) as DmList;
            decoded.len--;
            foreach (object value__it in Dm.each(decoded))
            {
                object value = value__it;
                if (Dm.lt(value, 0))
                    value = MAX_S_TURF + 1 + abs(Dm.num(value));
                Dm.combine_at(new_bitflag_list, Dm.text("[]", round(Dm.num(value) / 24)), Dm.shl(1, Dm.mod(value, 24)));
            }
            set_into = GLOB.bitflag_lists[txt_signature] = new_bitflag_list;
        }
    }
}
