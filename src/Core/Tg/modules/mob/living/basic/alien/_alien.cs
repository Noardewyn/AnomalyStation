// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/alien/_alien.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the basic xenomorph (the hunter), the simple alien of the ruins and the bitrunning xeno nest, with its claw
// footsteps, its loot, its weeds and eggs and its xeno blood and gibs. The var blocks are generated.

// tg: code/modules/mob/living/basic/alien/_alien.dm:1
public partial class mob_living_basic_alien : mob_living_basic
{
    // tg: code/modules/mob/living/basic/alien/_alien.dm:59
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(length(loot)))
        {
            basic_mob_flags = Dm.bitor(basic_mob_flags, DEL_ON_DEATH);
            loot = string_list(loot) as DmList;
            Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), loot));
        }
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), Dm.named("footstep_type", FOOTSTEP_MOB_CLAW)));
        return dot;
    }

    // tg: code/modules/mob/living/basic/alien/_alien.dm:67
    public override object get_butt_sprite()
    {
        return Dm.icon(Dm.resource("icons/mob/butts.dmi"), BUTT_SPRITE_XENOMORPH);
    }

    ///Places alien weeds on the turf the mob is currently standing on. Returns TRUE if weeds were placed.
    // tg: code/modules/mob/living/basic/alien/_alien.dm:71
    public virtual object place_weeds()
    {
        if (!isturf(loc) || isspaceturf(loc))
            return FALSE;
        if (Dm.locate<obj_structure_alien_weeds_node>(get_turf(this)) != null)
            return FALSE;
        visible_message(span_alertalien(I18n.T("[] plants some alien weeds!", this)));
        Dm.@new<obj_structure_alien_weeds_node>(loc);
        return TRUE;
    }

    ///Lays an egg on the turf the mob is currently standing on. Returns TRUE if an egg was laid.
    // tg: code/modules/mob/living/basic/alien/_alien.dm:81
    public virtual object lay_alien_egg()
    {
        if (!isturf(loc) || isspaceturf(loc))
            return FALSE;
        if (Dm.locate<obj_structure_alien_egg>(get_turf(this)) != null)
            return FALSE;
        visible_message(span_alertalien(I18n.T("[] lays an egg!", this)));
        Dm.@new<obj_structure_alien_egg>(loc);
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/alien/_alien.dm:90
    public override object get_bloodtype()
    {
        return get_blood_type(typeof(datum_blood_type_xeno));
    }

    // tg: code/modules/mob/living/basic/alien/_alien.dm:93
    public override object get_gibs_type(float? drop_bitflags = null)
    {
        drop_bitflags ??= NONE;
        if (Dm.truthy(Dm.bitand(drop_bitflags, DROP_BODYPARTS)))
            return typeof(obj_effect_gibspawner_xeno);
        return typeof(obj_effect_gibspawner_xeno_bodypartless);
    }
}
