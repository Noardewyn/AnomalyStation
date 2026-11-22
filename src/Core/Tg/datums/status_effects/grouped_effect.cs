// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/grouped_effect.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Status effect from multiple sources, when all sources are removed, so is the effect
// tg: code/datums/status_effects/grouped_effect.dm:2
[DmPath("/datum/status_effect/grouped")]
public partial class datum_status_effect_grouped : datum_status_effect
{
    // tg: code/datums/status_effects/grouped_effect.dm:3-6  the var overrides of the type block (rules 10.2)
    public datum_status_effect_grouped()
    {
        id = STATUS_EFFECT_ID_ABSTRACT;
        alert_type = null;
        // Grouped effects adds itself to [var/sources] and destroys itself if one exists already, there are never actually multiple
        status_type = STATUS_EFFECT_MULTIPLE;
    }

    // tg: code/datums/status_effects/grouped_effect.dm:8  the object-creating default (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        sources = Dm.list();
    }

    /// A list of all sources applying this status effect. Sources are a list of keys
    // tg: code/datums/status_effects/grouped_effect.dm:8 (list(): dm_init_vars above)
    public DmList sources;
}

// tg: code/datums/status_effects/grouped_effect.dm:10
public partial class datum_status_effect_grouped : datum_status_effect
{
    // tg: code/datums/status_effects/grouped_effect.dm:10
    [DmParams("new_owner", "source")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object source = Dm.arg(__extra, 0);
        object dot = null;
        //Get our supplied arguments, without new_owner
        DmList args = Dm.args(new_owner, Dm.forward(__extra, source));
        DmList new_source_args = args.Copy(2);

        if (source == null)
        {
            stack_trace("Grouped status effect applied without a source", "code/datums/status_effects/grouped_effect.dm", 15);
            qdel(this);
            return FALSE;
        }

        datum_status_effect_grouped existing = new_owner.has_status_effect(type) as datum_status_effect_grouped;
        if (Dm.truthy(existing))
        {
            Dm.combine(ref existing.sources, source);
            await Dm.Call(existing, nameof(source_added), Dm.arglist(new_source_args));
            qdel(this);
            return FALSE;
        }

        /* We are the original */

        dot = await base.on_creation(new_owner, Dm.forward(__extra, source));
        if (Dm.truthy(dot))
        {
            Dm.combine(ref sources, source);
            await Dm.Call(this, nameof(source_added), Dm.arglist(new_source_args));
        }
        return dot;
    }

    /**
     * Called after a source is added to the status effect,
     * this includes the first source added after creation.
     */
    // tg: code/datums/status_effects/grouped_effect.dm:37
    public virtual void source_added(object source = null, params object[] __extra)
    {
        return;
    }

    /**
     * Called after a source is removed from the status effect. \
     * `removing` will be TRUE if this is the last source, which means
     * the effect will be deleted.
     */
    // tg: code/datums/status_effects/grouped_effect.dm:45
    public virtual void source_removed(object source = null, object removing = null)
    {
        return;
    }

    // tg: code/datums/status_effects/grouped_effect.dm:48
    [DmParams("source")]
    public override object before_remove(params object[] __extra)
    {
        object source = Dm.arg(__extra, 0);
        Dm.remove(ref sources, source);
        float was_last_source = Dm.not(length(sources));
        source_removed(source, was_last_source);
        return was_last_source;
    }
}
