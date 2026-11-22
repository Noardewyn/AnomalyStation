// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/_element.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * A holder for simple behaviour that can be attached to many different types
 *
 * Only one element of each type is instanced during game init.
 * Otherwise acts basically like a lightweight component.
 */
// tg: code/datums/elements/_element.dm:7
[DmPath("/datum/element")]
public partial class datum_element : datum
{
    /// Option flags for element behaviour
    // tg: code/datums/elements/_element.dm:9
    public float element_flags = NONE;
    /**
      * The index of the first attach argument to consider for duplicate elements
      *
      * All arguments from this index onwards (1 based, until `argument_hash_end_idx` is reached, if set)
      * are hashed into the key to determine if this is a new unique element or one already exists
      *
      * Is only used when flags contains [ELEMENT_BESPOKE]
      *
      * This is infinity so you must explicitly set this
      */
    // tg: code/datums/elements/_element.dm:20
    public float argument_hash_start_idx = INFINITY;
    /**
      * The index of the last attach argument to consider for duplicate elements
      * Only used when `element_flags` contains [ELEMENT_BESPOKE].
      * If not set, it'll copy every argument from `argument_hash_start_idx` onwards as normal
      */
    // tg: code/datums/elements/_element.dm:26
    public float argument_hash_end_idx = 0;

    /// Activates the functionality defined by the element on the given target datum
    // tg: code/datums/elements/_element.dm:29
    [ShouldCallParent]
    public virtual async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        if (type == typeof(datum_element))
            return ELEMENT_INCOMPATIBLE;
        if (Dm.truthy(target._listen_lookup?[COMSIG_ELEMENT_ATTACH]))
            target._SendSignal(COMSIG_ELEMENT_ATTACH, Dm.list(target, this));
        if (Dm.truthy(Dm.bitand(element_flags, ELEMENT_DETACH_ON_HOST_DESTROY)))
            RegisterSignal(target, COMSIG_QDELETING, nameof(OnTargetDelete), @override: TRUE);
        return null;
    }

    // tg: code/datums/elements/_element.dm:37
    [SignalHandler]
    public virtual void OnTargetDelete(datum source = null)
    {
        Detach(source);
    }

    /// Deactivates the functionality defines by the element on the given datum
    // tg: code/datums/elements/_element.dm:42
    // Per-slot decision (rules 3.3 P1, 7.1; reported to the lead for Appendix A): slots.tsv lists source as undecided
    // (initial() and :: read it in overrides); it keeps its declared type /datum: every caller passes the target datum.
    [SignalHandler, ShouldCallParent]
    public virtual object Detach(datum source = null, params object[] __extra)
    {
        if (Dm.truthy(source._listen_lookup?[COMSIG_ELEMENT_DETACH]))
            source._SendSignal(COMSIG_ELEMENT_DETACH, Dm.list(source, this));
        UnregisterSignal(source, COMSIG_QDELETING);
        return null;
    }

    // tg: code/datums/elements/_element.dm:49
    public override object Destroy(float? force = null)
    {
        if (!Dm.truthy(force))
            return QDEL_HINT_LETMELIVE;
        Dm.remove(ref SSdcs.elements_by_type, type);
        return base.Destroy(force);
    }
}

//DATUM PROCS

public partial class datum
{
    /// Finds the singleton for the element type given and attaches it to src
    // tg: code/datums/elements/_element.dm:58
    public virtual async DmTask _AddElement(DmList arguments = null, params object[] __extra)
    {
        if (QDELING(this))
        {
            // var/datum/element/element_type holds the element's typepath, read from the argument list: Type (rules 3.2).
            Type element_type = arguments[1] as Type;
            stack_trace(Dm.text("We just tried to add the element [] to a qdeleted datum, something is fucked", element_type), "code/datums/elements/_element.dm", 61);
            return;
        }

        datum_element ele = SSdcs.GetElement(arguments) as datum_element;
        if (!Dm.truthy(ele)) // We couldn't fetch the element, likely because it was not an element.
            return; // the crash message has already been sent
        arguments[1] = this;
        DmList args = Dm.args(arguments, __extra);
        if (Dm.eq(await Dm.Call(ele, nameof(datum_element.Attach), Dm.arglist(arguments)), ELEMENT_INCOMPATIBLE))
            throw Dm.CRASH(Dm.text("Incompatible element [] was assigned to a []! args: []", ele.type, type, json_encode(args)), "code/datums/elements/_element.dm", 69);
    }

    /**
     * Finds the singleton for the element type given and detaches it from src
     * You only need additional arguments beyond the type if you're using [ELEMENT_BESPOKE]
     */
    // tg: code/datums/elements/_element.dm:75
    public virtual void _RemoveElement(DmList arguments = null)
    {
        datum_element ele = SSdcs.GetElement(arguments, FALSE) as datum_element;
        if (!Dm.truthy(ele)) // We couldn't fetch the element, likely because it didn't exist.
            return;
        if (Dm.truthy(Dm.bitand(ele.element_flags, ELEMENT_COMPLEX_DETACH)))
        {
            arguments[1] = this;
            Waiting.NoSleep(Dm.Call(ele, nameof(datum_element.Detach), Dm.arglist(arguments)));
        }
        else
            ele.Detach(this);
    }

    /**
     * Used to manage (typically non_bespoke) elements with multiple sources through traits
     * so we don't have to make them a components again.
     * The element will be later removed once all trait sources are gone, there's no need of a
     * "RemoveElementTrait" counterpart.
     */
    // tg: code/datums/elements/_element.dm:91
    public virtual async DmTask AddElementTrait(object trait = null, object source = null, object eletype = null, params object[] __extra)
    {
        if (!ispath(eletype, typeof(datum_element)))
            throw Dm.CRASH(Dm.text("AddElementTrait called, but [] is not of a /datum/element path", eletype), "code/datums/elements/_element.dm", 93);
        ADD_TRAIT(this, trait, source);
        if (HAS_TRAIT_NOT_FROM(this, trait, source))
            return;
        DmList arguments = Dm.list(eletype);
        DmList args = Dm.args(trait, source, eletype, __extra);
        /// 3 is the length of fixed args of this proc, any further one is passed down to AddElement.
        if (length(args) > 3)
            Dm.append(ref arguments, args.Copy(4));
        /// We actually pass down a copy of the arguments since it's manipulated by the end of the proc.
        await _AddElement(arguments.Copy());
        datum ele = SSdcs.GetElement(arguments) as datum;
        ele.RegisterSignal(this, SIGNAL_REMOVETRAIT(trait), nameof(datum_element._detach_on_trait_removed));
    }
}

public partial class datum_element : datum
{
    // tg: code/datums/elements/_element.dm:106
    [SignalHandler]
    public virtual void _detach_on_trait_removed(datum source = null, object trait = null)
    {
        Detach(source);
        UnregisterSignal(source, SIGNAL_REMOVETRAIT(trait));
    }
}
