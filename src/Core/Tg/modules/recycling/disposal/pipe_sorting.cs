// Translated from tgstation (snapshot 1d99bd7f): code/modules/recycling/disposal/pipe_sorting.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (D325: the disposal system; the mail junction's init M3 P4, D115, its examine X1).

// A three-way junction that sorts objects based on check_sorting(H) proc
// This is a base type, use subtypes on the map.

// tg: code/modules/recycling/disposal/pipe_sorting.dm:3
public partial class obj_structure_disposalpipe_sorting : obj_structure_disposalpipe
{
    // tg: code/modules/recycling/disposal/pipe_sorting.dm:9
    public override object nextdir(obj_structure_disposalholder H = null)
    {
        object sortdir = Dm.bitand(dpdir, Dm.bitnot(Dm.bitor(dir, REVERSE_DIR(dir))));
        if (!Dm.eq(H.dir, sortdir)) // probably came from the negdir
        {
            if (Dm.truthy(check_sorting(H))) // if destination matches filtered type...
                return sortdir; // exit through sortdirection
        }

        // go with the flow to positive direction
        return dir;
    }

    /// Sorting check, to be overridden in subtypes
    // tg: code/modules/recycling/disposal/pipe_sorting.dm:19
    public virtual object check_sorting(obj_structure_disposalholder H = null)
    {
        return FALSE;
    }
}

// Mail sorting junction, uses package tags to sort objects.

// tg: code/modules/recycling/disposal/pipe_sorting.dm:23
public partial class obj_structure_disposalpipe_sorting_mail : obj_structure_disposalpipe_sorting
{
    // tg: code/modules/recycling/disposal/pipe_sorting.dm:36
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        // Generate a list of soring tags.
        if (Dm.truthy(sortType))
        {
            if (isnum(sortType))
                Dm.combine(ref sortTypes, sortType);
            else if (istext(sortType))
            {
                DmList sorts = splittext(sortType, ";");
                foreach (object x in Dm.each(sorts))
                {
                    float? n = text2num(x);
                    if (Dm.truthy(n))
                        Dm.combine(ref sortTypes, n);
                }
            }
        }
        return dot;
    }

    // tg: code/modules/recycling/disposal/pipe_sorting.dm:49
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(length(sortTypes)))
        {
            Dm.append(ref dot, I18n.T("It is tagged with the following tags:"));
            foreach (object t in Dm.each(sortTypes))
                Dm.append(ref dot, I18n.T("\t[].", GLOB.TAGGERLOCATIONS[t]));
        }
        else
            Dm.append(ref dot, I18n.T("It has no sorting tags set."));
        return dot;
    }

    // tg: code/modules/recycling/disposal/pipe_sorting.dm:58
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_dest_tagger)))
            return NONE;
        object relevant_tag = (tool as obj_item_dest_tagger).currTag;

        if (!Dm.truthy(relevant_tag)) // Tagger has a tag set
            return ITEM_INTERACT_BLOCKING;
        if (Dm.@in(relevant_tag, sortTypes))
        {
            Dm.remove(sortTypes, relevant_tag);
            to_chat(user, span_notice(I18n.T("Removed \"[]\" filter.", GLOB.TAGGERLOCATIONS[relevant_tag])));
        }
        else
        {
            Dm.combine(ref sortTypes, relevant_tag);
            to_chat(user, span_notice(I18n.T("Added \"[]\" filter.", GLOB.TAGGERLOCATIONS[relevant_tag])));
        }
        playsound(this, Dm.resource("sound/machines/beep/twobeep_high.ogg"), 100, TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/recycling/disposal/pipe_sorting.dm:75
    public override object check_sorting(obj_structure_disposalholder H = null)
    {
        return Dm.@in(H.destinationTag, sortTypes) ? TRUE : FALSE;
    }
}

// Wrap sorting junction, sorts objects destined for the mail office mail table (tomail = TRUE)

// tg: code/modules/recycling/disposal/pipe_sorting.dm:82
public partial class obj_structure_disposalpipe_sorting_wrap : obj_structure_disposalpipe_sorting
{
    // tg: code/modules/recycling/disposal/pipe_sorting.dm:87
    public override object check_sorting(obj_structure_disposalholder H = null)
    {
        return H.tomail;
    }
}
