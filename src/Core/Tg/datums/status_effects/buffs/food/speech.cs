// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/buffs/food/speech.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333: the crafted foods' speech buffs). The var blocks are generated (D111).

// tg: code/datums/status_effects/buffs/food/speech.dm:4
public partial class datum_status_effect_food_speech_italian : datum_status_effect_food_speech
{
    // tg: code/datums/status_effects/buffs/food/speech.dm:10
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        // tg adds the component to the status effect itself, not to its owner (the French one adds it to the owner)
        speechmod = await AddComponent(
            typeof(datum_component_speechmod),
            Dm.named("replacements", strings("italian_replacement.json", "italian")),
            Dm.named("end_string", Dm.list(
                I18n.T(" Ravioli, ravioli, give me the formuoli!"),
                I18n.T(" Mamma-mia!"),
                I18n.T(" Mamma-mia! That's a spicy meat-ball!"),
                I18n.T(" La la la la la funiculi funicula!")
                )),
            Dm.named("end_string_chance", 3)
            ) as datum_component;
        return dot;
    }

    // tg: code/datums/status_effects/buffs/food/speech.dm:24
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        QDEL_NULL(ref speechmod);
        return dot;
    }
}

// tg: code/datums/status_effects/buffs/food/speech.dm:34
public partial class datum_status_effect_food_speech_french : datum_status_effect_food_speech
{
    // tg: code/datums/status_effects/buffs/food/speech.dm:40
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        speechmod = await owner.AddComponent(
            typeof(datum_component_speechmod),
            Dm.named("replacements", strings("french_replacement.json", "french")),
            Dm.named("end_string", Dm.list(
                I18n.T(" Honh honh honh!"),
                I18n.T(" Honh!"),
                I18n.T(" Zut Alors!")
                )),
            Dm.named("end_string_chance", 3)
            ) as datum_component;
        return dot;
    }

    // tg: code/datums/status_effects/buffs/food/speech.dm:53
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        QDEL_NULL(ref speechmod);
        return dot;
    }
}
