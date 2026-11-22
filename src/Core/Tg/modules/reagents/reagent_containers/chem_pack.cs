// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/reagent_containers/chem_pack.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P5): the intravenous medicine bag, sealed by an alt-click. The var block is generated.

// tg: code/modules/reagents/reagent_containers/chem_pack.dm:1
public partial class obj_item_reagent_containers_chem_pack : obj_item_reagent_containers
{
    // tg: code/modules/reagents/reagent_containers/chem_pack.dm:15
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand((reagents as datum_reagents).flags, SEALED_CONTAINER)))
        {
            balloon_alert(user, "already sealed!");
            return CLICK_ACTION_BLOCKING;
        }

        if (iscarbon(user) && (HAS_TRAIT(user, TRAIT_CLUMSY) && prob(50)))
        {
            to_chat(user, span_warning(I18n.T("Uh... whoops! You accidentally spill the content of the bag onto yourself.")));
            await splash_reagents(user, user, allow_closed_splash: TRUE);
            return CLICK_ACTION_BLOCKING;
        }

        update_container_flags(Dm.bitor(Dm.bitor(SEALED_CONTAINER, DRAWABLE), INJECTABLE));
        balloon_alert(user, "sealed");
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/reagents/reagent_containers/chem_pack.dm:29
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(Dm.bitand((reagents as datum_reagents).flags, SEALED_CONTAINER)))
            Dm.append(ref dot, span_notice(I18n.T("The bag is sealed shut.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("Alt-click to seal it.")));
        return dot;
    }
}
