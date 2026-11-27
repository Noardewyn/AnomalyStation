// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/posters/official.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The official posters are data (their var blocks are generated); the one proc of the file is the "No ERP" poster's: once in a hundred
// rips, or on April Fools' Day, it peels off to show "Yes ERP" beneath it (and back again).

// tg: code/game/objects/effects/posters/official.dm:254
public partial class obj_structure_sign_poster_official_no_erp : obj_structure_sign_poster_official
{
    // tg: code/game/objects/effects/posters/official.dm:261
    public override async DmTask tear_poster(mob user = null)
    {
        if (prob(99) && !Dm.truthy(check_holidays(APRIL_FOOLS)))
        {
            await base.tear_poster(user);
            return;
        }

        visible_message(span_notice(I18n.T("[] rips [] in a single, decisive motion... revealing another poster?", user, this)));
        playsound(this, Dm.resource("sound/items/poster/poster_ripped.ogg"), 100, TRUE);
        if (Dm.truthy(corrupted))
        {
            name = Dm.initial(type, (obj_structure_sign_poster_official_no_erp p) => p.name);
            desc = Dm.initial(type, (obj_structure_sign_poster_official_no_erp p) => p.desc);
            icon_state = Dm.initial(type, (obj_structure_sign_poster_official_no_erp p) => p.icon_state);
            corrupted = FALSE;
        }
        else
        {
            name = "Yes ERP";
            desc = "This poster reminds the crew that Enterprise Resource Planning is both effective and critical for the station's operations.";
            icon_state = "yes_erp";
            corrupted = TRUE;
        }
    }
}
