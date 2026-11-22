// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/util/service_style.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the agent's look of the cyber police (a business haircut, no beard).

// tg: code/modules/bitrunning/util/service_style.dm:1
public partial class mob_living_carbon_human
{
    // tg: code/modules/bitrunning/util/service_style.dm:3  var/static/list/approved_hair_colors
    private static DmList set_service_style__approved_hair_colors => TgState.Current.mob_living_carbon_human__set_service_style__approved_hair_colors ??= Dm.list(
        "#4B3D28",
        COLOR_BLACK,
        "#8D4A43",
        "#D2B48C");

    // tg: code/modules/bitrunning/util/service_style.dm:10  var/static/list/approved_hairstyles
    private static DmList set_service_style__approved_hairstyles => TgState.Current.mob_living_carbon_human__set_service_style__approved_hairstyles ??= Dm.list(
        typeof(datum_sprite_accessory_hair_business),
        typeof(datum_sprite_accessory_hair_business2),
        typeof(datum_sprite_accessory_hair_business3),
        typeof(datum_sprite_accessory_hair_business4),
        typeof(datum_sprite_accessory_hair_mulder));

    /// Gives the appearance of being an agent
    // tg: code/modules/bitrunning/util/service_style.dm:2
    public virtual void set_service_style()
    {
        Type picked_hair = pick(set_service_style__approved_hairstyles) as Type;
        object picked_color = pick(set_service_style__approved_hair_colors);

        set_facial_hairstyle("Shaved", update: FALSE);
        set_haircolor(picked_color, update: FALSE);
        set_hairstyle(Dm.initial(picked_hair, (datum_sprite_accessory p) => p.name));
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_carbon_human__set_service_style__approved_hair_colors;
    internal DmList mob_living_carbon_human__set_service_style__approved_hairstyles;
}
