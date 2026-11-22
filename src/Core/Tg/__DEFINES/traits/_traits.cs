// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/traits/_traits.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Every macro of this file is a GlobalProcs member (rules 11.1): none uses an argument as a name, stringifies or
// pastes one, leaves the macro or refers to the caller's context, so each argument is evaluated once where tg's
// expansions read target and trait several times. A statement macro's do { ... } while (0) wrapper is the member's
// body; the SEND_SIGNAL inside it is expanded inline as a statement (rules 11.1 (e), __DEFINES/dcs/helpers.cs).

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/traits/_traits.dm:1
    public static object SIGNAL_ADDTRAIT(object trait_ref) => Dm.add("addtrait ", trait_ref);
    // tg: code/__DEFINES/traits/_traits.dm:2
    public static object SIGNAL_REMOVETRAIT(object trait_ref) => Dm.add("removetrait ", trait_ref);

    // trait accessor defines
    // tg: code/__DEFINES/traits/_traits.dm:5
    public static void ADD_TRAIT(object target, object trait, object source)
    {
        DmList _L = null;
        if (!Dm.truthy(((datum)target)._status_traits))
        {
            ((datum)target)._status_traits = Dm.list();
            _L = ((datum)target)._status_traits;
            _L[trait] = Dm.list(source);
            object t__1 = SIGNAL_ADDTRAIT(trait);
            if (Dm.truthy(((datum)target)._listen_lookup?[t__1]))
                ((datum)target)._SendSignal(t__1, Dm.list(target, trait));
        }
        else
        {
            _L = ((datum)target)._status_traits;
            if (Dm.truthy(_L[trait]))
                Dm.combine_at(_L, trait, Dm.list(source));
            else
            {
                _L[trait] = Dm.list(source);
                object t__2 = SIGNAL_ADDTRAIT(trait);
                if (Dm.truthy(((datum)target)._listen_lookup?[t__2]))
                    ((datum)target)._SendSignal(t__2, Dm.list(target, trait));
            }
        }
    }

    // tg: code/__DEFINES/traits/_traits.dm:23
    public static void REMOVE_TRAIT(object target, object trait, object sources)
    {
        DmList _L = ((datum)target)._status_traits;
        DmList _S = null;
        if (Dm.truthy(sources) && !islist(sources))
            _S = Dm.list(sources);
        else
            _S = sources as DmList;
        if (Dm.truthy(_L?[trait]))
        {
            foreach (object _T in Dm.each(_L[trait]))
            {
                if ((!Dm.truthy(_S) && !Dm.eq(_T, ROUNDSTART_TRAIT)) || Dm.@in(_T, _S))
                    Dm.remove_at(_L, trait, _T);
            }
            if (!Dm.truthy(length(_L[trait])))
            {
                Dm.remove(ref _L, trait);
                object t__1 = SIGNAL_REMOVETRAIT(trait);
                if (Dm.truthy(((datum)target)._listen_lookup?[t__1]))
                    ((datum)target)._SendSignal(t__1, Dm.list(target, trait));
            }
            if (!Dm.truthy(length(_L)))
                ((datum)target)._status_traits = null;
        }
    }

    // tg: code/__DEFINES/traits/_traits.dm:47
    public static void REMOVE_TRAIT_NOT_FROM(object target, object trait, object sources)
    {
        DmList _traits_list = ((datum)target)._status_traits;
        DmList _sources_list = null;
        if (Dm.truthy(sources) && !islist(sources))
            _sources_list = Dm.list(sources);
        else
            _sources_list = sources as DmList;
        if (Dm.truthy(_traits_list?[trait]))
        {
            foreach (object _trait_source in Dm.each(_traits_list[trait]))
            {
                if (!Dm.@in(_trait_source, _sources_list))
                    Dm.remove_at(_traits_list, trait, _trait_source);
            }
            if (!Dm.truthy(length(_traits_list[trait])))
            {
                Dm.remove(ref _traits_list, trait);
                object t__1 = SIGNAL_REMOVETRAIT(trait);
                if (Dm.truthy(((datum)target)._listen_lookup?[t__1]))
                    ((datum)target)._SendSignal(t__1, Dm.list(target, trait));
            }
            if (!Dm.truthy(length(_traits_list)))
                ((datum)target)._status_traits = null;
        }
    }

    // tg: code/__DEFINES/traits/_traits.dm:71
    public static void REMOVE_TRAITS_NOT_IN(object target, object sources)
    {
        DmList _L = ((datum)target)._status_traits;
        // var/list/_S also holds one text source (unit_tests/trait_addition_and_removal.dm:51): object (rules 3.4).
        object _S = sources;
        if (Dm.truthy(_L))
        {
            foreach (object _T in Dm.each(_L))
            {
                if (Dm.truthy(_L[_T]))
                    Dm.mask_at(_L, _T, _S);
                if (!Dm.truthy(length(_L[_T])))
                {
                    Dm.remove(ref _L, _T);
                    object t__1 = SIGNAL_REMOVETRAIT(_T);
                    if (Dm.truthy(((datum)target)._listen_lookup?[t__1]))
                        ((datum)target)._SendSignal(t__1, Dm.list(target, _T));
                }
            }
            if (!Dm.truthy(length(_L)))
                ((datum)target)._status_traits = null;
        }
    }

    // tg: code/__DEFINES/traits/_traits.dm:91
    public static void REMOVE_TRAITS_IN(object target, object sources)
    {
        DmList _L = ((datum)target)._status_traits;
        DmList _S = sources as DmList;
        if (Dm.truthy(sources) && !islist(sources))
            _S = Dm.list(sources);
        else
            _S = sources as DmList;
        if (Dm.truthy(_L))
        {
            foreach (object _T in Dm.each(_L))
            {
                if (Dm.truthy(_L[_T]))
                    Dm.remove_at(_L, _T, _S);
                if (!Dm.truthy(length(_L[_T])))
                {
                    Dm.remove(ref _L, _T);
                    object t__1 = SIGNAL_REMOVETRAIT(_T);
                    if (Dm.truthy(((datum)target)._listen_lookup?[t__1]))
                        ((datum)target)._SendSignal(t__1, Dm.list(target, _T));
                }
            }
            if (!Dm.truthy(length(_L)))
                ((datum)target)._status_traits = null;
        }
    }

    // tg: code/__DEFINES/traits/_traits.dm:116
    // Rules 11.1 (v1.2): the expansion (c ? TRUE : FALSE) is a condition, so the member is bool; a value use reads
    // Dm.num(HAS_TRAIT(...)) (rules 4.1).
    public static bool HAS_TRAIT(object target, object trait) => Dm.truthy(((datum)target)?._status_traits?[trait]);
    // tg: code/__DEFINES/traits/_traits.dm:117
    public static bool HAS_TRAIT_FROM(object target, object trait, object source) => (HAS_TRAIT(target, trait) && Dm.@in(source, ((datum)target)._status_traits[trait]));
    // tg: code/__DEFINES/traits/_traits.dm:118
    public static bool HAS_TRAIT_FROM_ONLY(object target, object trait, object source) => (HAS_TRAIT(target, trait) && Dm.@in(source, ((datum)target)._status_traits[trait]) && length(((datum)target)._status_traits[trait]) == 1);
    // tg: code/__DEFINES/traits/_traits.dm:119
    public static bool HAS_TRAIT_NOT_FROM(object target, object trait, object source) => (HAS_TRAIT(target, trait) && length(Dm.sub(((datum)target)._status_traits[trait], source)) > 0);
    /// Returns a list of trait sources for this trait. Only useful for wacko cases and internal futzing
    /// You should not be using this
    // tg: code/__DEFINES/traits/_traits.dm:122
    public static object GET_TRAIT_SOURCES(object target, object trait) => (((datum)target)?._status_traits?[trait] is var or__1 && Dm.truthy(or__1) ? or__1 : (object)Dm.list());
    /// Returns the amount of sources for a trait. useful if you don't want to have a "thing counter" stuck around all the time
    // tg: code/__DEFINES/traits/_traits.dm:124
    public static float COUNT_TRAIT_SOURCES(object target, object trait) => length(GET_TRAIT_SOURCES(target, trait));
    /// A simple helper for checking traits in a mob's mind
    // tg: code/__DEFINES/traits/_traits.dm:126
    // Rules 11.1 (v1.2): an || of conditions (the right one is c ? HAS_TRAIT(...) : FALSE, true only when both are), so
    // the member is bool like HAS_TRAIT; its DM value is TRUE or FALSE either way.
    public static bool HAS_MIND_TRAIT(object target, object trait) => HAS_TRAIT(target, trait) || (Dm.truthy(((mob)target).mind) && HAS_TRAIT(((mob)target).mind, trait));
}
