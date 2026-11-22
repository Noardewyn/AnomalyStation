// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/animal_variety.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

///Picks a sprite from a list of variations for an animal.
// tg: code/datums/elements/animal_variety.dm:9
public partial class datum_element_animal_variety : datum_element
{
    // tg: code/datums/elements/animal_variety.dm:12
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        object icon_prefix = Dm.arg(__extra, 0);
        object chosen_sprite_suffix = Dm.arg(__extra, 1);
        object modify_pixels = Dm.arg(__extra, 2) ?? FALSE;
        if (isbasicmob(target))
        {
            mob_living_basic animal = target as mob_living_basic;
            animal.icon_state = Dm.text("[]_[]", icon_prefix, chosen_sprite_suffix);
            animal.icon_living = Dm.text("[]_[]", icon_prefix, chosen_sprite_suffix);
            animal.icon_dead = Dm.text("[]_[]_dead", icon_prefix, chosen_sprite_suffix);
            if (Dm.truthy(modify_pixels)) // Yeah I know I didn't actually need to copy and paste this part but basicmob's entire existence is copypaste
            {
                animal.pixel_x = animal.base_pixel_x + rand(-6, 6);
                animal.pixel_y = animal.base_pixel_y + rand(0, 10);
            }
            return dot;
        }
        return ELEMENT_INCOMPATIBLE;
    }

    // tg: code/datums/elements/animal_variety.dm:32
    public override object Detach(datum target = null, params object[] __extra)
    {
        if (isbasicmob(target))
        {
            mob_living_basic animal = target as mob_living_basic;
            animal.icon_state = initial(animal, "icon_state") as string;
            animal.icon_living = initial(animal, "icon_state") as string;
            animal.icon_dead = initial(animal, "icon_living") as string;
            animal.pixel_x = animal.base_pixel_x;
            animal.pixel_y = animal.base_pixel_y;
        }
        return base.Detach(target, __extra);
    }
}
