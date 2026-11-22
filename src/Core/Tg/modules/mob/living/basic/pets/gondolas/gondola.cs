// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/pets/gondolas/gondola.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1a (D327): the whole file, the gondola (a strange vent clog's silent walker, its body, eyes and moustache drawn from random
// parts; its controller's tree is tg's gondola.bt.json). Its hide drops as tg's inert gondola hide (the hides' recipes stay with the
// hides). The var blocks are generated (D111).

// tg: code/modules/mob/living/basic/pets/gondolas/gondola.dm:6
public partial class mob_living_basic_pet_gondola : mob_living_basic_pet
{
    // tg: code/modules/mob/living/basic/pets/gondolas/gondola.dm:1-4  the file-local GONDOLA_HEIGHT, GONDOLA_COLOR, GONDOLA_MOUSTACHE and
    // GONDOLA_EYES picks
    private static string GONDOLA_HEIGHT() => (string)pick(Dm.list("gondola_body_long", "gondola_body_medium", "gondola_body_short"));
    private static string GONDOLA_COLOR() => (string)pick(Dm.list("A87855", "915E48", "683E2C"));
    private static string GONDOLA_MOUSTACHE() => (string)pick(Dm.list("gondola_moustache_large", "gondola_moustache_small"));
    private static string GONDOLA_EYES() => (string)pick(Dm.list("gondola_eyes_close", "gondola_eyes_far"));

    // tg: code/modules/mob/living/basic/pets/gondolas/gondola.dm:39
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_MUTE, INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_pet_bonus), "smile"));
        if (Dm.truthy(LAZYLEN(loot)))
        {
            loot = string_list(loot) as DmList;
            Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), loot));
        }
        create_gondola();
        return dot;
    }

    // tg: code/modules/mob/living/basic/pets/gondolas/gondola.dm:48
    public virtual void create_gondola()
    {
        icon_state = null;
        icon_living = null;
        string height = GONDOLA_HEIGHT();
        var body_overlay = (mutable_appearance)mutable_appearance(icon, height);
        var eyes_overlay = (mutable_appearance)mutable_appearance(icon, GONDOLA_EYES());
        var moustache_overlay = (mutable_appearance)mutable_appearance(icon, GONDOLA_MOUSTACHE());
        body_overlay.color = "#" + GONDOLA_COLOR();

        //Offset the face to match the Gondola's height.
        if (height == "gondola_body_medium")
        {
            eyes_overlay.pixel_z = -4;
            moustache_overlay.pixel_z = -4;
        }
        else if (height == "gondola_body_short")
        {
            eyes_overlay.pixel_z = -8;
            moustache_overlay.pixel_z = -8;
        }

        cut_overlays(); // cut_overlays(TRUE): tg's /atom/proc/cut_overlays() takes no argument, the TRUE is dropped
        add_overlay(body_overlay);
        add_overlay(eyes_overlay);
        add_overlay(moustache_overlay);
    }
}
