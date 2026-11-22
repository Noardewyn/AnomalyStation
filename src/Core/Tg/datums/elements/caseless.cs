// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/caseless.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4b, D316: the rebar crossbow's rods). The var block is generated (D111).

/**
 * An element that deletes the casing when fired and, if reusable is true, adds the projectile_drop element to the bullet.
 * Just make sure to not add components or elements that also use COMSIG_FIRE_CASING after this one.
 * Not compatible with pellets (how the eff would that work in a senible way tho?).
 */
// tg: code/datums/elements/caseless.dm:6
public partial class datum_element_caseless : datum_element
{
    // tg: code/datums/elements/caseless.dm:11
    [DmParams("target", "reusable")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object reusable = Dm.arg(__extra, 0) ?? FALSE;
        object dot = await base.Attach(target, __extra);
        if (target is not obj_item_ammo_casing) // isammocasing (__DEFINES/is_helpers.dm), inline (rules 11.1 (a))
            return ELEMENT_INCOMPATIBLE;
        this.reusable = Dm.num(reusable);
        if (Dm.truthy(reusable))
            RegisterSignal(target, COMSIG_CASING_READY_PROJECTILE, nameof(on_ready_projectile));
        RegisterSignal(target, COMSIG_FIRE_CASING, nameof(on_fired_casing));
        return dot;
    }

    // tg: code/datums/elements/caseless.dm:20
    [SignalHandler]
    public virtual void on_ready_projectile(obj_item_ammo_casing shell = null, atom target = null, mob_living user = null, object quiet = null, object zone_override = null, atom fired_from = null)
    {
        obj_projectile proj = shell.loaded_projectile;
        if (proj == null)
            return;
        if (!ispath(proj.shrapnel_type))
            proj.shrapnel_type = shell.type;
        Waiting.NoSleep(proj.AddElement(typeof(datum_element_projectile_drop), shell.type));
    }

    // tg: code/datums/elements/caseless.dm:29
    [SignalHandler]
    public virtual void on_fired_casing(obj_item_ammo_casing shell = null, atom target = null, mob_living user = null, object fired_from = null, object randomspread = null, object spread = null, object zone_override = null, object @params = null, object distro = null, obj_projectile proj = null)
    {
        if (fired_from is obj_item_gun shot_from) // isgun, inline (rules 11.1 (a))
        {
            if (shot_from.chambered == shell)
                shot_from.chambered = null; //Nuke it. Nuke it now.
        }
        QDEL_NULL(ref shell);
    }
}
