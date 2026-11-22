// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/pets/parrot/parrot_items.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

// tg: code/modules/mob/living/basic/pets/parrot/parrot_items.dm:2
public partial class datum_strippable_item_parrot_headset : datum_strippable_item
{
    // tg: code/modules/mob/living/basic/pets/parrot/parrot_items.dm:4
    public override object get_item(atom source = null)
    {
        return source is mob_living_basic_parrot parrot_source ? parrot_source.ears : null;
    }

    // tg: code/modules/mob/living/basic/pets/parrot/parrot_items.dm:8
    public override async DmTask<object> try_equip(atom source = null, obj_item equipping = null, mob user = null)
    {
        object dot = await base.try_equip(source, equipping, user);
        if (!Dm.truthy(dot))
            return FALSE;

        if (equipping is not obj_item_radio_headset)
        {
            to_chat(user, span_warning(Dm.text("[] won't fit!", equipping)));
            return FALSE;
        }

        return TRUE;
    }

    // There is no delay for putting a headset on a parrot.
    // tg: code/modules/mob/living/basic/pets/parrot/parrot_items.dm:20
    public override async DmTask<object> start_equip(atom source = null, obj_item equipping = null, mob user = null)
    {
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/pets/parrot/parrot_items.dm:23
    public override object finish_equip(atom source = null, obj_item equipping = null, mob user = null)
    {
        if (equipping is not obj_item_radio_headset radio)
            return null;

        if (source is not mob_living_basic_parrot parrot_source)
            return null;

        if (!Dm.truthy(Waiting.NoSleep(user.transferItemToLoc(radio, source))))
            return null;

        parrot_source.ears = radio;

        to_chat(user, span_notice(Dm.text("You fit [] onto [].", radio, source)));
        return null;
    }

    // tg: code/modules/mob/living/basic/pets/parrot/parrot_items.dm:39
    public override async DmTask<object> start_unequip(atom source = null, mob user = null)
    {
        object dot = await base.start_unequip(source, user);
        if (!Dm.truthy(dot))
            return FALSE;

        if (source is not mob_living_basic_parrot parrot_source)
            return dot;

        if (!IS_UNCONSCIOUS_OR_CRIT(parrot_source))
        {
            DmList list_of_channels = parrot_source.get_available_channels();
            await parrot_source.say(Dm.text("[]BAWWWWWK LEAVE THE HEADSET BAWKKKKK!", list_of_channels != null ? Dm.text("[] ", pick(list_of_channels)) : null), forced: "attempted headset removal");
        }

        return TRUE;
    }

    // tg: code/modules/mob/living/basic/pets/parrot/parrot_items.dm:54
    public override async DmTask<object> finish_unequip(atom source = null, mob user = null)
    {
        if (source is not mob_living_basic_parrot parrot_source)
            return null;

        await parrot_source.ears.forceMove(parrot_source.drop_location() as atom);
        parrot_source.ears = null;
        return null;
    }
}
