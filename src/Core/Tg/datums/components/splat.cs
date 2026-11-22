// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/splat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4b, D316: the pie cannon's cream pies). The var block is generated (D111).

// tg: code/datums/components/splat.dm:1
public partial class datum_component_splat : datum_component
{
    // tg: code/datums/components/splat.dm:17
    [DmParams("icon_state", "layer", "memory_type", "smudge_type", "moodlet_type", "splat_color", "hit_callback")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object icon_state = Dm.arg(__extra, 0) ?? "creampie";
        object layer = Dm.arg(__extra, 1) ?? Dm.list(Dm.kv(EXTERNAL_FRONT, BODY_FRONT_LAYER));
        object memory_type = Dm.arg(__extra, 2) ?? typeof(datum_memory_witnessed_creampie);
        object smudge_type = Dm.arg(__extra, 3) ?? typeof(obj_effect_decal_cleanable_food_pie_smudge);
        object moodlet_type = Dm.arg(__extra, 4) ?? typeof(datum_mood_event_creampie);
        object splat_color = Dm.arg(__extra, 5);
        datum_callback hit_callback = Dm.arg(__extra, 6) as datum_callback;
        object dot = await base.Initialize(Dm.forward(__extra, icon_state, layer, memory_type, smudge_type, moodlet_type, splat_color, hit_callback));
        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;

        this.icon_state = icon_state;
        this.layer = layer;
        this.memory_type = memory_type as System.Type;
        this.smudge_type = smudge_type as System.Type;
        this.moodlet_type = moodlet_type as System.Type;
        this.hit_callback = hit_callback;
        this.splat_color = splat_color;
        return dot;
    }

    // tg: code/datums/components/splat.dm:38
    public override object Destroy(float? force = null)
    {
        hit_callback = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/splat.dm:42
    public override async DmTask RegisterWithParent()
    {
        if (isprojectile(parent))
            RegisterSignal(parent, COMSIG_PROJECTILE_SELF_ON_HIT, nameof(projectile_splat));
        else
            RegisterSignal(parent, COMSIG_MOVABLE_IMPACT, nameof(throw_splat));
    }

    // tg: code/datums/components/splat.dm:48
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_MOVABLE_IMPACT, COMSIG_PROJECTILE_SELF_ON_HIT));
    }

    // tg: code/datums/components/splat.dm:51
    [SignalHandler]
    public virtual void projectile_splat(obj_projectile source = null, atom firer = null, atom target = null, object angle = null, object hit_limb_zone = null, object blocked = null, object pierce_hit = null)
    {
        if (Dm.num(blocked) != 100 && !Dm.truthy(pierce_hit))
            Waiting.NoSleep(splat(source, target));
    }

    // tg: code/datums/components/splat.dm:56
    [SignalHandler]
    public virtual void throw_splat(atom_movable source = null, atom hit_atom = null, datum_thrownthing throwing_datum = null, object caught = null)
    {
        if (Dm.truthy(caught)) //someone caught us!
            return;
        Waiting.Detach(splat(source, hit_atom)); // the victim's paralysis and the decal's head overlay may wait (async in this port)
    }

    // tg: code/datums/components/splat.dm:62
    public virtual async DmTask splat(atom_movable source = null, atom hit_atom = null)
    {
        turf hit_turf = get_turf(hit_atom);
        Dm.@new(smudge_type, hit_turf);
        bool can_splat_on = true;
        if (isliving(hit_atom))
        {
            mob_living living_target_getting_hit = (mob_living)hit_atom;
            if (iscarbon(living_target_getting_hit))
                can_splat_on = Dm.truthy(living_target_getting_hit.get_bodypart(BODY_ZONE_HEAD));
            if (hit_callback != null)
                await hit_callback.Invoke(living_target_getting_hit, can_splat_on ? TRUE : FALSE);
        }
        if (can_splat_on && Dm.truthy(is_type_in_typecache(hit_atom, GLOB.splattable)))
            await hit_atom.AddComponent(typeof(datum_component_face_decal_splat), icon_state, layer, Dm.truthy(splat_color) ? splat_color : source.color, memory_type, moodlet_type);
        if (Dm.truthy(source._listen_lookup?[COMSIG_MOVABLE_SPLAT]))
            source._SendSignal(COMSIG_MOVABLE_SPLAT, Dm.list(source, hit_atom));
        if (!isprojectile(source))
            qdel(source);
    }
}
