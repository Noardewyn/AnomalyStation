// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/grenades/smokebomb.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 *This is smoke bomb, mezum koman. It is a grenade subtype. All craftmanship is of the highest quality.
 *It menaces with spikes of iron. On it is a depiction of an assistant.
 *The assistant is bleeding. The assistant has a painful expression. The assistant is dead.
 */
// tg: code/game/objects/items/grenades/smokebomb.dm:6
public partial class obj_item_grenade_smokebomb : obj_item_grenade
{
    // tg: code/game/objects/items/grenades/smokebomb.dm:15  var/static/list/bruh_moment (rules 10.6)
    private static ref DmList bruh_moment => ref TgState.Current.obj_item_grenade_smokebomb__bruh_moment;

    ///Here we generate the extremely insightful description.
    // tg: code/game/objects/items/grenades/smokebomb.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        desc = I18n.T("The word '[]' is scribbled on it in crayon.", pick(bruh_moment));
        return dot;
    }

    ///Here we generate some smoke and also damage blobs??? for some reason. Honestly not sure why we do that.
    // tg: code/game/objects/items/grenades/smokebomb.dm:37
    public override async DmTask<object> detonate(mob_living lanced_by = null)
    {
        object dot = await base.detonate(lanced_by);
        if (!Dm.truthy(dot))
            return dot;

        await update_mob();
        playsound(this, Dm.resource("sound/effects/smoke.ogg"), 50, TRUE, -3);
        await do_smoke(4, this, loc, smoke_type: typeof(datum_effect_system_fluid_spread_smoke_bad));
        foreach (obj_structure_blob blob in Dm.each<obj_structure_blob>(view(8, this)))
        {
            float damage = round(30f / (get_dist(blob, this) + 1));
            await blob.take_damage(damage, BURN, MELEE, 0);
        }
        qdel(this);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/grenades/smokebomb.dm:15
    internal DmList obj_item_grenade_smokebomb__bruh_moment = Dm.list("Dank", "Hip", "Lit", "Based", "Robust", "Bruh", "Gamer", "Sigma", "Blud", "Fanum", "Gyatt", "Looksmaxxing", "Mewing", "Aura", "Crashout");
}
