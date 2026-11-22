// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/skin_tone.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/client/preferences/skin_tone.dm:1
[DmPath("/datum/preference/choiced/skin_tone")]
public partial class datum_preference_choiced_skin_tone : datum_preference_choiced
{
    // tg: code/modules/client/preferences/skin_tone.dm:7
    public override DmList init_possible_values()
    {
        return GLOB.skin_tones;
    }

    // tg: code/modules/client/preferences/skin_tone.dm:10
    public override object compile_constant_data()
    {
        DmList data = (DmList)base.compile_constant_data();

        data[CHOICED_PREFERENCE_DISPLAY_NAMES] = GLOB.skin_tone_names;

        DmList to_hex = Dm.list();
        foreach (object choice in Dm.each(get_choices()))
        {
            string hex_value = Dm.str(skintone2hex(choice));
            DmList hsl = rgb2num(hex_value, COLORSPACE_HSL);

            DmList entry = Dm.list();
            entry["lightness"] = hsl[3];
            entry["value"] = hex_value;
            to_hex[choice] = entry;
        }

        data["to_hex"] = to_hex;

        return data;
    }

    // tg: code/modules/client/preferences/skin_tone.dm:29
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        target.skin_tone = Dm.str(value);
    }
}
