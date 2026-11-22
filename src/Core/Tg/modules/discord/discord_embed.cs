// Translated from tgstation (snapshot 1d99bd7f): code/modules/discord/discord_embed.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10x slice G6: the whole file (the admin help's Discord webhook).

/// Documentation for the embed object and all of its variables can be found at
/// https://discord.com/developers/docs/resources/channel#embed-object
/// It is recommended to read the documentation on the discord website, as the information below could become outdated in the future.
// tg: code/modules/discord/discord_embed.dm:4
public partial class datum_discord_embed : datum
{
    // tg: code/modules/discord/discord_embed.dm:33
    public virtual DmList convert_to_list()
    {
        if (Dm.truthy(color) && !Dm.isnum(color))
            throw Dm.CRASH(Dm.text("Color on [] is not a number! Expected a number, got [] instead.", type, color), "code/modules/discord/discord_embed.dm", 35);
        DmList data_to_list = Dm.list();
        if (Dm.truthy(title))
            data_to_list["title"] = title;
        if (Dm.truthy(description))
        {
            object new_desc = replacetext(replacetext(description, "\\proper", ""), "\\improper", "");
            new_desc = has_discord_embeddable_links.Replace(Dm.str(replacetext(new_desc, "`", "")), " ```$1``` ");
            data_to_list["description"] = new_desc;
        }
        if (Dm.truthy(url))
            data_to_list["url"] = url;
        if (Dm.truthy(color))
            data_to_list["color"] = color;
        if (Dm.truthy(footer))
            data_to_list["footer"] = Dm.list(Dm.kv("text", footer));
        if (Dm.truthy(image))
            data_to_list["image"] = Dm.list(Dm.kv("url", image));
        if (Dm.truthy(thumbnail))
            data_to_list["thumbnail"] = Dm.list(Dm.kv("url", thumbnail));
        if (Dm.truthy(video))
            data_to_list["video"] = Dm.list(Dm.kv("url", video));
        if (Dm.truthy(provider))
            data_to_list["provider"] = Dm.list(Dm.kv("name", provider), Dm.kv("url", provider_url));
        if (Dm.truthy(author))
            data_to_list["author"] = Dm.list(Dm.kv("name", author)); // DEVIATION(DV219): tg's key is "author"; Discord's embed author is "name"
        if (fields != null)
        {
            DmList list = Dm.list();
            data_to_list["fields"] = list;
            foreach (object data in Dm.each(fields))
            {
                if (!Dm.truthy(fields[data]))
                    continue;
                list.Add(Dm.list(Dm.list(
                    Dm.kv("name", data),
                    Dm.kv("value", has_discord_embeddable_links.Replace(Dm.str(replacetext(fields[data], "`", "")), " ```$1``` ")))));
            }
        }
        return data_to_list;
    }
}

public static partial class GlobalProcs
{
    // tg: code/_globalvars/regexes.dm  GLOBAL_DATUM_INIT(has_discord_embeddable_links, regex, regex(@"(https?://[^\s|<>]+)", "g")):
    // BYOND's regex as .NET's (a global Replace replaces every match)
    public static readonly System.Text.RegularExpressions.Regex has_discord_embeddable_links = new(@"(https?://[^\s|<>]+)", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}
