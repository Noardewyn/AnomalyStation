// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/dcs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/controllers/subsystem/dcs.dm:1 PROCESSING_SUBSYSTEM_DEF(dcs)
[DmPath("/datum/controller/subsystem/processing/dcs")]
public partial class datum_controller_subsystem_processing_dcs : datum_controller_subsystem_processing
{
    // tg: code/controllers/subsystem/dcs.dm:2
    public datum_controller_subsystem_processing_dcs()
    {
        name = "Datum Component System";
        ss_flags = SS_NO_INIT;
        wait = 1 * SECONDS;
    }

    // tg: code/controllers/subsystem/dcs.dm:6
    public DmList elements_by_type;

    /**
     * A nested assoc list of bespoke element types (keys) and superlists containing all lists used as arguments (values).
     * Inside the superlists, lists that've been sorted alphabetically are keys, while the original unsorted lists are values.
     *
     * e.g. list(
     *	/datum/element/first = list(list(A, B, C) = list(B, A, C), list(A, B) = list(A, B)),
     *	/datum/element/second = list(list(B, C) = list(C, B), list(D) = list(D)),
     * )
     *
     * Used by the dcs_check_list_arguments unit test.
     */
    // tg: code/controllers/subsystem/dcs.dm:19
    public DmList arguments_that_are_lists_by_element;
    /**
     * An assoc list of list instances and their sorted counterparts.
     *
     * e.g. list(
     *	list(B, A, C) = list(A, B, C),
     *	list(C, B) = list(B, C),
     * )
     *
     * Used to make sure each list instance is sorted no more than once, or the unit test won't work.
     */
    // tg: code/controllers/subsystem/dcs.dm:30
    public DmList sorted_arguments_that_are_lists;

    // tg: code/controllers/subsystem/dcs.dm:6, :19, :30 (object-creating defaults, rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        elements_by_type = Dm.list();
        arguments_that_are_lists_by_element = Dm.list();
        sorted_arguments_that_are_lists = Dm.list();
    }

    // The New() that PROCESSING_SUBSYSTEM_DEF(dcs) defines (__DEFINES/MC.dm:116-119), with NEW_SS_GLOBAL(SSdcs)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/dcs.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSdcs != this)
        {
            if (SSdcs is datum_controller_subsystem_processing_dcs)
            {
                Recover();
                qdel(SSdcs);
            }
            SSdcs = this;
        }
        PreInit();
    }

    // The fire() that PROCESSING_SUBSYSTEM_DEF(dcs) defines (__DEFINES/MC.dm:120): ..() /*just so it shows up on the profiler*/
    // tg: code/controllers/subsystem/dcs.dm:1
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }

    // tg: code/controllers/subsystem/dcs.dm:32
    public override void Recover()
    {
        Dm.NotTranslated("code/controllers/subsystem/dcs.dm:32", "Recover: not translated (translation-rules.md 13)");
    }

    // tg: code/controllers/subsystem/dcs.dm:35
    public virtual object GetElement(DmList arguments = null, float? init_element = null)
    {
        init_element ??= TRUE;
        object dot = null;
        // var/datum/element/eletype holds a typepath, tested with ispath and printed when it is none: object (rules 3.4).
        object eletype = arguments[1];
        object element_id = eletype;

        if (!ispath(eletype, typeof(datum_element)))
            throw Dm.CRASH(Dm.text("Attempted to instantiate [] as a /datum/element", eletype), "code/controllers/subsystem/dcs.dm", 40);

        if (Dm.truthy(Dm.bitand(Dm.initial(eletype, (datum_element p) => p.element_flags), ELEMENT_BESPOKE)))
            element_id = (length(arguments) == 1 ? (object)Dm.text("[]", arguments[1]) : GetIdFromArguments(arguments));

        dot = elements_by_type[element_id];
        if (Dm.truthy(dot) || !Dm.truthy(init_element))
            return dot;
        dot = elements_by_type[element_id] = Dm.@new(eletype as Type);
        return dot;
    }

    /****
    	* Generates an id for bespoke elements when given the argument list
    	* Generating the id here is a bit complex because we need to support named arguments
    	* Named arguments can appear in any order and we need them to appear after ordered arguments
    	* We assume that no one will pass in a named argument with a value of null
    	**/
    // tg: code/controllers/subsystem/dcs.dm:56
    public virtual object GetIdFromArguments(DmList arguments = null)
    {
        // var/datum/element/eletype holds the element's typepath, read from the argument list: Type (rules 3.2, 3.4).
        Type eletype = arguments[1] as Type;
        DmList fullid = Dm.list(eletype);
        DmList named_arguments = null;
        for (float i = Dm.initial(eletype, (datum_element p) => p.argument_hash_start_idx), i__end = (Dm.initial(eletype, (datum_element p) => p.argument_hash_end_idx) is var or__1 && Dm.truthy(or__1) ? or__1 : length(arguments)); i <= i__end; i++)
        {
            object key = arguments[i];

            if (istext(key))
            {
                object value = arguments[key];
                if (value == null)
                    Dm.append(ref fullid, key);
                else
                {
                    if (!istext(value) && !isnum(value))
                    {
                        // PERFORM_ALL_TESTS(dcs_check_list_arguments) is its production expansion FALSE (rules 1.7).
                        if (Dm.truthy(FALSE) && islist(value))
                            add_to_arguments_that_are_lists(value as DmList, eletype);
                        value = REF(value);
                    }

                    if (!Dm.truthy(named_arguments))
                        named_arguments = Dm.list();

                    named_arguments[key] = value;
                }
                continue;
            }

            if (isnum(key))
                Dm.append(ref fullid, key);
            else
            {
                // PERFORM_ALL_TESTS(dcs_check_list_arguments): expanded inline to FALSE (as above).
                if (Dm.truthy(FALSE) && islist(key))
                    add_to_arguments_that_are_lists(key as DmList, eletype);
                Dm.append(ref fullid, REF(key));
            }
        }

        if (Dm.truthy(named_arguments))
        {
            sortTim(named_arguments, "/proc/" + nameof(GlobalProcs.cmp_text_asc));
            Dm.append(ref fullid, named_arguments);
        }

        return list2params(fullid);
    }

    /**
     * Offloading the first half of the dcs_check_list_arguments here, which is populating the superlist
     * with sublists that will be later compared with each other by the dcs_check_list_arguments unit test.
     */
    // tg: code/controllers/subsystem/dcs.dm:96
    // Per-slot decision (rules 3.3 P1, 7.1; reported to the lead for Appendix A): slots.tsv lists element_type as
    // undecided (initial() reads an instance or a type); it is Type: its only caller passes the element's typepath.
    public virtual void add_to_arguments_that_are_lists(DmList argument = null, Type element_type = null)
    {
        if (Dm.truthy(Dm.bitand(Dm.initial(element_type, (datum_element p) => p.element_flags), ELEMENT_NO_LIST_UNIT_TEST)))
            return;
        DmList element_type_superlist = arguments_that_are_lists_by_element[element_type] as DmList;
        if (!Dm.truthy(element_type_superlist))
            arguments_that_are_lists_by_element[element_type] = element_type_superlist = Dm.list();

        DmList sorted_argument = argument;
        if (!Dm.truthy(Dm.bitand(Dm.initial(element_type, (datum_element p) => p.element_flags), ELEMENT_DONT_SORT_LIST_ARGS)))
        {
            sorted_argument = sorted_arguments_that_are_lists[argument] as DmList;
            if (!Dm.truthy(sorted_argument))
                sorted_arguments_that_are_lists[argument] = sorted_argument = sortTim(argument.Copy(), "/proc/" + nameof(GlobalProcs.cmp_embed_text_asc)) as DmList;
        }

        element_type_superlist[sorted_argument] = argument;
    }
}

public static partial class GlobalProcs
{
    /// tg <c>PROCESSING_SUBSYSTEM_DEF(dcs)</c>: <c>GLOBAL_REAL(SSdcs, /datum/controller/subsystem/processing/dcs)</c>
    /// (rules 10.6). <c>SEND_GLOBAL_SIGNAL</c> sends its signals from it (__DEFINES/dcs/helpers.dm:7).
    // tg: code/controllers/subsystem/dcs.dm:1
    public static ref datum_controller_subsystem_processing_dcs SSdcs => ref TgState.Current.SSdcs;
}

public sealed partial class TgState
{
    /// tg <c>PROCESSING_SUBSYSTEM_DEF(dcs)</c>: <c>GLOBAL_REAL(SSdcs, /datum/controller/subsystem/processing/dcs)</c>.
    // tg: code/controllers/subsystem/dcs.dm:1
    // Internal, with the public accessor GlobalProcs.SSdcs, as SSgarbage (controllers/subsystem/garbage.cs).
    internal datum_controller_subsystem_processing_dcs SSdcs;
}
