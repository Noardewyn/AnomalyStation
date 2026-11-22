// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/spawners/xeno_egg_delivery.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file, the station trait's research specimen egg (its delivery, the footnote of the command report and
// the captivity area the captive xenomorphs are judged by). The var blocks are generated (D111).

// tg: code/game/objects/effects/spawners/xeno_egg_delivery.dm:1
public partial class obj_effect_spawner_xeno_egg_delivery : obj_effect_spawner
{
    // tg: code/game/objects/effects/spawners/xeno_egg_delivery.dm:6
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        turf spawn_turf = get_turf(this);
        Dm.@new<obj_structure_alien_egg_delivery>(spawn_turf);
        Dm.@new<obj_effect_temp_visual_gravpush>(spawn_turf);
        playsound(spawn_turf, Dm.resource("sound/items/party_horn.ogg"), 50, TRUE, -1);
        if (Dm.truthy(SSticker.HasRoundStarted()))
            return dot;

        message_admins(Dm.text("An alien egg has been delivered to [].", ADMIN_VERBOSEJMP(spawn_turf)));
        log_game(Dm.text("An alien egg has been delivered to []", AREACOORD(spawn_turf)));

        datum_command_footnote footnote = Dm.@new<datum_command_footnote>();
        footnote.message = I18n.T("We have entrusted your crew with a research specimen in []. Remember to follow all safety precautions when dealing with the specimen.", get_area(this));
        footnote.signature = I18n.T("Central Command");

        Dm.append(GLOB.communications_controller.command_report_footnotes, footnote);
        return dot;
    }
}

// tg: code/game/objects/effects/spawners/xeno_egg_delivery.dm:25
public partial class obj_structure_alien_egg_delivery : obj_structure_alien_egg
{
    // tg: code/game/objects/effects/spawners/xeno_egg_delivery.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        GLOB.communications_controller.xenomorph_egg_delivered = TRUE;
        GLOB.communications_controller.captivity_area = get_area(this) as area;
        return dot;
    }
}
