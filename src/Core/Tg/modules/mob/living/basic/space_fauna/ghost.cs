// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/ghost.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

// tg: code/modules/mob/living/basic/space_fauna/ghost.dm:1
public partial class mob_living_basic_ghost : mob_living_basic
{
    // tg: code/modules/mob/living/basic/space_fauna/ghost.dm:47
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), typeof(obj_item_ectoplasm)));
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_flying)));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        ADD_TRAIT(this, TRAIT_GHOSTLY_MOB, INNATE_TRAIT);

        give_identity();
        return dot;
    }

    /// Generates hair, facial hair, and a random name for ghosts if one is needed.
    // tg: code/modules/mob/living/basic/space_fauna/ghost.dm:66
    public virtual void give_identity()
    {
        if (Dm.truthy(random_identity))
        {
            ghost_hairstyle = random_hairstyle(); //This only gives us the hairstyle name, not the icon_state (which we need).
            ghost_hair_color = random_hair_color();

            if (prob(50)) //Only a chance at also getting facial hair
            {
                ghost_facial_hairstyle = random_facial_hairstyle();
                ghost_facial_hair_color = ghost_hair_color;
            }
        }

        if (ghost_hairstyle != null && !Dm.eq(ghost_hairstyle, "Bald")) //Bald hairstyle and the Shaved facial hairstyle lack an associated sprite and will not properly generate hair, and just cause runtimes.
        {
            datum_sprite_accessory_hair hair_style = SSaccessories.hairstyles_list[ghost_hairstyle] as datum_sprite_accessory_hair; //We use the hairstyle name to get the sprite accessory, which we copy the icon_state from.
            ghost_hair = mutable_appearance(Dm.resource("icons/mob/human/human_face.dmi"), Dm.text("[]", hair_style.icon_state), -HAIR_LAYER) as mutable_appearance;
            ghost_hair.alpha = 200;
            ghost_hair.color = ghost_hair_color;
            ghost_hair.pixel_z = hair_style.y_offset;
            add_overlay(ghost_hair);
        }

        if (ghost_facial_hairstyle != null && !Dm.eq(ghost_facial_hairstyle, "Shaved"))
        {
            datum_sprite_accessory facial_hair_style = SSaccessories.facial_hairstyles_list[ghost_facial_hairstyle] as datum_sprite_accessory;
            ghost_facial_hair = mutable_appearance(Dm.resource("icons/mob/human/human_face.dmi"), Dm.text("[]", facial_hair_style.icon_state), -HAIR_LAYER) as mutable_appearance;
            ghost_facial_hair.alpha = 200;
            ghost_facial_hair.color = ghost_facial_hair_color;
            add_overlay(ghost_facial_hair);
        }

        if (Dm.truthy(random_identity))
        {
            if (rand(0, 1) == 0)
                name = Dm.text("ghost of [] []", pick(GLOB.first_names_male), pick(GLOB.last_names));
            else
                name = Dm.text("ghost of [] []", pick(GLOB.first_names_female), pick(GLOB.last_names));
        }
    }
}
