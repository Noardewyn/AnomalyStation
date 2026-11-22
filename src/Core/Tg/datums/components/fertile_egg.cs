// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/fertile_egg.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

/**
 * ### A fertile egg component!
 *
 * This component tracks over time if the atom is in ideal conditions,
 * and eventually hatches into the embryonic type.
 */
// tg: code/datums/components/fertile_egg.dm:9
public partial class datum_component_fertile_egg : datum_component
{
    // tg: code/datums/components/fertile_egg.dm:34
    [DmParams("embryo_type", "minimum_growth_rate", "maximum_growth_rate", "total_growth_required", "current_growth", "location_allowlist", "spoilable", "examine_message", "post_hatch")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        // Quite how an _area_ can be a fertile egg is an open question, but it still has a location. Technically.
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;

        this.embryo_type = (Type)Dm.arg(__extra, 0);
        this.minimum_growth_rate = Dm.arg(__extra, 1);
        this.maximum_growth_rate = Dm.arg(__extra, 2);
        this.total_growth_required = Dm.arg(__extra, 3);
        this.current_growth = Dm.arg(__extra, 4);
        this.location_allowlist = Dm.arg(__extra, 5) as DmList;
        this.spoilable = Dm.arg(__extra, 6);
        this.post_hatch = Dm.arg(__extra, 8) as datum_callback;

        START_PROCESSING(SSobj, this);
        return null;
    }

    // tg: code/datums/components/fertile_egg.dm:50
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/datums/components/fertile_egg.dm:54
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        atom parent_atom = parent as atom;

        if (location_allowlist != null && !Dm.truthy(is_type_in_typecache(parent_atom.loc, location_allowlist)))
        {
            // In a zone that is not allowed, do nothing, and possibly self destruct
            if (Dm.truthy(spoilable))
                qdel(this);
            return null;
        }

        current_growth = Dm.num(current_growth) + rand(Dm.num(minimum_growth_rate), Dm.num(maximum_growth_rate)) * Dm.num(seconds_per_tick);
        if (Dm.num(current_growth) < Dm.num(total_growth_required))
            return null;
        parent_atom.visible_message(span_notice(Dm.text("[] hatches with a quiet cracking sound.", parent)));
        Dm.@new(embryo_type, get_turf(parent_atom));
        if (post_hatch != null)
            Waiting.NoSleep(post_hatch.Invoke(embryo_type));
        // We destroy the parent on hatch, which will destroy the component as well, which will stop us processing.
        qdel(parent_atom);
        return null;
    }
}
