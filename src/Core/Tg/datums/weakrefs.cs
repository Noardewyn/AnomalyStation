// Translated from tgstation (snapshot 1d99bd7f): code/datums/weakrefs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public static partial class GlobalProcs
{
    /// Creates a weakref to the given input.
    /// See /datum/weakref's documentation for more information.
    // tg: code/datums/weakrefs.dm:3
    public static object WEAKREF(datum input = null)
    {
        if (input is datum && !QDELETED(input))
        {
            if (isweakref(input))
                return input;

            if (!Dm.truthy(input.weak_reference))
                input.weak_reference = Dm.@new<datum_weakref>(input);
            return input.weak_reference;
        }
        return null;
    }
}

public partial class datum
{
    // tg: code/datums/weakrefs.dm:12
    public virtual object create_weakref() //Forced creation for admin proccalls
    {
        return WEAKREF(this);
    }
}

/**
 * A weakref holds a non-owning reference to a datum.
 * The datum can be referenced again using `resolve()`.
 *
 * To figure out why this is important, you must understand how deletion in
 * BYOND works.
 *
 * Imagine a datum as a TV in a living room. When one person enters to watch
 * TV, they turn it on. Others can come into the room and watch the TV.
 * When the last person leaves the room, they turn off the TV because it's
 * no longer being used.
 *
 * A datum being deleted tells everyone who's watching the TV to stop.
 * If everyone leaves properly (AKA cleaning up their references), then the
 * last person will turn off the TV, and everything is well.
 * However, if someone is resistant (holds a hard reference after deletion),
 * then someone has to walk in, drag them away, and turn off the TV forecefully.
 * This process is very slow, and it's known as hard deletion.
 *
 * This is where weak references come in. Weak references don't count as someone
 * watching the TV. Thus, when what it's referencing is destroyed, it will
 * hopefully clean up properly, and limit hard deletions.
 *
 * A common use case for weak references is holding onto what created itself.
 * For example, if a machine wanted to know what its last user was, it might
 * create a `var/mob/living/last_user`. However, this is a strong reference to
 * the mob, and thus will force a hard deletion when that mob is deleted.
 * It is often better in this case to instead create a weakref to the user,
 * meaning this type definition becomes `var/datum/weakref/last_user`.
 *
 * A good rule of thumb is that you should hold strong references to things
 * that you *own*. For example, a dog holding a chew toy would be the owner
 * of that chew toy, and thus a `var/obj/item/chew_toy` reference is fine
 * (as long as it is cleaned up properly).
 * However, a chew toy does not own its dog, so a `var/mob/living/dog/owner`
 * might be inferior to a weakref.
 * This is also a good rule of thumb to avoid circular references, such as the
 * chew toy example. A circular reference that doesn't clean itself up properly
 * will always hard delete.
 */
// tg: code/datums/weakrefs.dm:55
[DmPath("/datum/weakref")]
public partial class datum_weakref : datum
{
    // tg: code/datums/weakrefs.dm:56
    public object reference;

    // DEVIATION(DV90): the weakref also holds its datum, so one that only weakrefs still reach resolves until it is deleted.
    // BYOND frees a datum whose refcount drops to 0 at once (resolve then gives null); the C# GC frees it when it likes, and
    // resolve, locating it in the weak registry, would follow the GC's timing (QDEL_IN's weakref, the state hash).
    [NotHashed]
    private datum dm_target;

    // tg: code/datums/weakrefs.dm:58
    [DmParams("thing")]
    public override async DmTask New(params object[] __extra)
    {
        datum thing = Dm.arg<datum>(__extra, 0);
        reference = REF(thing);
        dm_target = thing;
    }

    // tg: code/datums/weakrefs.dm:61
    public override object Destroy(float? force = null)
    {
        datum target = resolve() as datum;
        qdel(target);

        if (!Dm.truthy(force))
            return QDEL_HINT_LETMELIVE; //Let BYOND autoGC thiswhen nothing is using it anymore.
        target?.weak_reference = null;
        return base.Destroy(force);
    }

    /**
     * Retrieves the datum that this weakref is referencing.
     *
     * This will return `null` if the datum was deleted. This MUST be respected.
     */
    // tg: code/datums/weakrefs.dm:75
    public virtual object resolve()
    {
        datum D = Dm.locate_ref(reference) as datum;
        if (D == null)
            dm_target = null; // DV90: held, so not collected: it left the registry for good (deleted), stop holding it
        return (!QDELETED(D) && D.weak_reference == this) ? D : null;
    }

    /**
     * SERIOUSLY READ THE AUTODOC COMMENT FOR THIS PROC BEFORE EVEN THINKING ABOUT USING IT
     *
     * Like resolve, but doesn't care if the datum is being qdeleted but hasn't been deleted yet.
     *
     * The return value of this proc leaves hanging references if the datum is being qdeleted but hasn't been deleted yet.
     *
     * Do not do anything that would create a lasting reference to the return value, such as giving it a tag, putting it on the map,
     * adding it to an atom's contents or vis_contents, giving it a key (if it's a mob), attaching it to an atom (if it's an image),
     * or assigning it to a datum or list referenced somewhere other than a temporary value.
     *
     * Unless you're resolving a weakref to a datum in a COMSIG_QDELETING signal handler registered on that very same datum,
     * just use resolve instead.
     */
    // tg: code/datums/weakrefs.dm:93
    public virtual object hard_resolve()
    {
        datum D = Dm.locate_ref(reference) as datum;
        return (D?.weak_reference == this) ? D : null;
    }

    // tg: code/datums/weakrefs.dm:97
    public override object vv_get_dropdown()
    {
        Dm.NotTranslated("code/datums/weakrefs.dm:97", "vv_get_dropdown: VV is admin tooling (translation-rules.md 13)");
        return default;
    }

    // tg: code/datums/weakrefs.dm:102
    public override async DmTask<object> vv_do_topic(DmList href_list = null)
    {
        Dm.NotTranslated("code/datums/weakrefs.dm:102", "vv_do_topic: VV is admin tooling (translation-rules.md 13)");
        return default;
    }
}
