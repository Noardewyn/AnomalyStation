// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/bodyparts/bodypart_effects/bodypart_effect.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP2a (D349): the whole file: the base of the bodypart effects (the plasmaman's limbs set him alight in oxygen); the var block is
// generated (D111).

/// Effects added to a carbon focused on the bodyparts itself, such as adding a photosynthesis component that
// tg: code/modules/surgery/bodyparts/bodypart_effects/bodypart_effect.dm:2
public partial class datum_status_effect_grouped_bodypart_effect : datum_status_effect_grouped
{
    // tg: code/modules/surgery/bodyparts/bodypart_effects/bodypart_effect.dm:15
    [DmParams("source", "bodypart", "special", "lazy")]
    public override void source_added(object source = null, params object[] __extra)
    {
        object bodypart = Dm.arg(__extra, 0);
        object special = Dm.arg(__extra, 1);
        object lazy = Dm.arg(__extra, 2);
        add_bodypart(bodypart as obj_item_bodypart, special, lazy);
    }

    /// Merge a bodypart into the effect
    // tg: code/modules/surgery/bodyparts/bodypart_effects/bodypart_effect.dm:19
    public virtual void add_bodypart(obj_item_bodypart bodypart = null, object special = null, object lazy = null)
    {
        RegisterSignal(bodypart, COMSIG_BODYPART_REMOVED, nameof(on_bodypart_removed));
        RegisterSignal(bodypart, COMSIG_QDELETING, nameof(on_bodypart_destroyed));

        Dm.append(ref bodyparts, bodypart);

        check_active();
    }

    /// Remove a bodypart from the effect. Deleting = TRUE is used during clean-up phase
    // tg: code/modules/surgery/bodyparts/bodypart_effects/bodypart_effect.dm:28
    public virtual void remove_bodypart(mob_living_carbon old_owner = null, obj_item_bodypart bodypart = null, object deleting = null)
    {
        UnregisterSignal(bodypart, Dm.list(COMSIG_BODYPART_REMOVED, COMSIG_QDELETING));

        Dm.remove(ref bodyparts, bodypart);

        if (Dm.truthy(deleting))
            return;

        if (bodyparts.Count == 0 && !QDELETED(old_owner))
            qdel(this);
        else
            check_active();
    }

    /// Checks if we should be active or not
    // tg: code/modules/surgery/bodyparts/bodypart_effects/bodypart_effect.dm:43
    public virtual object should_activate()
    {
        return bodyparts.Count >= minimum_bodyparts;
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/bodypart_effect.dm:46
    [SignalHandler]
    public virtual void check_active(params object[] __extra)
    {
        // SIGNAL_HANDLER

        if (Dm.truthy(is_active))
        {
            if (!Dm.truthy(should_activate()))
                Waiting.NoSleep(deactivate());
        }
        else
        {
            if (Dm.truthy(should_activate()))
                activate();
        }
    }

    /// Signal called when a bodypart is removed
    // tg: code/modules/surgery/bodyparts/bodypart_effects/bodypart_effect.dm:58
    [SignalHandler]
    public virtual void on_bodypart_removed(obj_item_bodypart bodypart = null, object special = null)
    {
        // SIGNAL_HANDLER

        remove_bodypart(owner as mob_living_carbon, bodypart);
    }

    /// Signal called when a bodypart is destroyed. Destruction of a bodypart doesn't necessarily drop it
    // tg: code/modules/surgery/bodyparts/bodypart_effects/bodypart_effect.dm:64
    [SignalHandler]
    public virtual void on_bodypart_destroyed(obj_item_bodypart bodypart = null)
    {
        // SIGNAL_HANDLER

        remove_bodypart(bodypart.owner, bodypart);
    }

    /// Activate some sort of effect when a threshold is reached
    // tg: code/modules/surgery/bodyparts/bodypart_effects/bodypart_effect.dm:70
    [DmProtected]
    public virtual void activate()
    {
        is_active = TRUE;
        // Maybe add support for different stages? AKA add a stronger effect when there are more limbs
    }

    /// Remove an effect whenever a threshold is no longer reached
    // tg: code/modules/surgery/bodyparts/bodypart_effects/bodypart_effect.dm:78
    [DmProtected]
    public virtual async DmTask deactivate()
    {
        is_active = FALSE;
    }

    // tg: code/modules/surgery/bodyparts/bodypart_effects/bodypart_effect.dm:84
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        if (Dm.truthy(is_active))
            await deactivate();
        foreach (obj_item_bodypart bodypart in Dm.each_cast<obj_item_bodypart>(bodyparts.Copy()))
            remove_bodypart(bodypart.owner, bodypart, deleting: TRUE);
        return dot;
    }

    // This should be redundant given on_remove, but there are a few edge cases where it gets skipped, so this is a safety net
    // tg: code/modules/surgery/bodyparts/bodypart_effects/bodypart_effect.dm:92
    public override object Destroy(float? force = null)
    {
        foreach (obj_item_bodypart bodypart in Dm.each_cast<obj_item_bodypart>(bodyparts.Copy()))
            remove_bodypart(bodypart.owner, bodypart, deleting: TRUE);
        return base.Destroy(force);
    }
}
