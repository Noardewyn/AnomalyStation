// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/radio/encryptionkey.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice D: the radio): the encryption keys, their channels and what their examine lists. The keys
// themselves are var blocks (generated, D111).

// tg: code/game/objects/items/devices/radio/encryptionkey.dm:1
public partial class obj_item_encryptionkey : obj_item
{
    // tg: code/game/objects/items/devices/radio/encryptionkey.dm:17
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(LAZYLEN(channels)) && !Dm.truthy(Dm.bitand(special_channels, RADIO_SPECIAL_BINARY)) && !Dm.truthy(LAZYLEN(language_data)))
        {
            Dm.append(ref dot, span_warning(I18n.T("Has no special codes in it. You should probably tell a coder!")));
            return dot;
        }

        DmList examine_text_list = Dm.list();
        foreach (object i in Dm.each(channels))
            Dm.append(ref examine_text_list, Dm.text("[] - []", GLOB.channel_tokens[i], LOWER_TEXT(i)));

        if (Dm.truthy(Dm.bitand(special_channels, RADIO_SPECIAL_BINARY)))
            Dm.append(ref examine_text_list, Dm.text("[] - []", GLOB.channel_tokens[MODE_BINARY], MODE_BINARY));

        if (Dm.truthy(length(examine_text_list)))
            Dm.append(ref dot, span_notice(I18n.T("It can access the following channels; [].", jointext(examine_text_list, ", "))));

        DmList language_text_list = Dm.list();
        foreach (object lang in Dm.each(language_data))
        {
            string langstring = Dm.text("[]", ((datum_language)GLOB.language_datum_instances[lang]).name);
            float understood = Dm.num(language_data[lang]);
            if (understood >= 25 && understood <= 50)
                langstring = Dm.text("[] (poor)", langstring);
            else if (understood >= 50 && understood <= 75)
                langstring = Dm.text("[] (average)", langstring);
            else if (understood >= 75 && understood <= 100)
                langstring = Dm.text("[] (good)", langstring);
            Dm.append(ref language_text_list, langstring);
        }

        if (Dm.truthy(length(language_text_list)))
            Dm.append(ref dot, span_notice(I18n.T("It can translate the following languages; [].", jointext(language_text_list, ", "))));
        return dot;
    }
}
