// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/cracked.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1b, D316: the pressure tanks of the syndicate base). The var block is generated (D111).

/// Tracks damage to add or remove crack overlays, when none are needed this components is qdeleted
// tg: code/datums/components/cracked.dm:2
public partial class datum_component_cracked : datum_component
{
    // tg: code/datums/components/cracked.dm:8
    [DmParams("crack_appearances", "crack_integrity")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object dot = await base.Initialize(__extra);
        if (!isobj(parent))
            return COMPONENT_INCOMPATIBLE;
        crack_appearances = Dm.arg<DmList>(__extra, 0);
        crack_integrity = Dm.arg(__extra, 1);
        return dot;
    }

    // tg: code/datums/components/cracked.dm:15
    public override object Destroy(float? force = null)
    {
        RemoveCracks(parent as obj, length(applied_cracks));
        return base.Destroy(force);
    }

    // tg: code/datums/components/cracked.dm:19
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_ATOM_INTEGRITY_CHANGED, nameof(IntegrityChanged));
        var master = parent as obj;
        object integrity = master.get_integrity();
        IntegrityChanged(master, integrity, integrity);
    }

    // tg: code/datums/components/cracked.dm:26
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        UnregisterSignal(parent, COMSIG_ATOM_INTEGRITY_CHANGED);
    }

    // tg: code/datums/components/cracked.dm:30
    [SignalHandler]
    public virtual void IntegrityChanged(obj source = null, object old_value = null, object new_value = null)
    {
        float cracked_max_integrity = Dm.num(source.max_integrity) * Dm.num(crack_integrity);
        if (Dm.num(new_value) >= cracked_max_integrity)
        {
            qdel(this);
            return;
        }

        float current_percent = 1 - (Dm.num(new_value) / cracked_max_integrity);
        float cracks = MathF.Ceiling(10 * current_percent); // 1 crack per 10% integrity lost
        float current_cracks = length(applied_cracks);
        // tg makes the source's filters a list here (BYOND's filters var may hold a single filter); the port's filter_data is always a list

        if (current_cracks == cracks)
            return;

        if (current_cracks < cracks)
            AddCracks(source, cracks - current_cracks);
        else
            RemoveCracks(source, current_cracks - cracks);
    }

    // tg: code/datums/components/cracked.dm:52
    public virtual void AddCracks(obj source = null, float count = 0)
    {
        for (int i = 1; i <= count; i++)
        {
            float rand_x = rand(0, 20) - 10;
            float rand_y = rand(0, 20) - 10;
            var new_crack_overlay = Dm.@new<mutable_appearance>(pick(crack_appearances));
            // Now that we have our overlay, we need to give it a unique render source so we can use a filter against it
            int uuid = ++TgState.Current.datum_component_cracked__AddCracks__uuid;
            // * so it doesn't render on its own
            new_crack_overlay.render_target = Dm.text("*cracked_overlay_[]", uuid);
            string render_source = Dm.str(new_crack_overlay.render_target);

            DmList new_filter_data = alpha_mask_filter(render_source: render_source, x: rand_x, y: rand_y, flags: MASK_INVERSE) as DmList;
            applied_cracks[render_source] = new_crack_overlay;

            // We need to add it as an overlay so the render target from the filter knows what to point at
            source.add_overlay(new_crack_overlay);
            source.add_filter(render_source, 1, new_filter_data);
        }
    }

    // tg: code/datums/components/cracked.dm:71
    public virtual void RemoveCracks(obj source = null, float count = 0)
    {
        for (int i = 1; i <= count; i++)
        {
            object removed_filter = pick(applied_cracks);
            source.remove_filter(removed_filter);
            source.cut_overlay(applied_cracks[removed_filter]);
            Dm.remove(ref applied_cracks, removed_filter);
        }
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/cracked.dm:58  var/static/uuid = 0
    internal int datum_component_cracked__AddCracks__uuid;
}
