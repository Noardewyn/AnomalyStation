// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/nearby_fake_item.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file. The image hangs on the nearby human and the client draws it over him (DV110). The var blocks are generated (D111).

/// A hallucination that delivers the illusion that someone nearby has pulled out a weapon or item.
// tg: code/modules/hallucination/nearby_fake_item.dm:2
public partial class datum_hallucination_nearby_fake_item : datum_hallucination
{
    // tg: code/modules/hallucination/nearby_fake_item.dm:17
    public override object Destroy(float? force = null)
    {
        if (generated_image != null)
        {
            if (hallucinator.client != null)
                Dm.remove(hallucinator.client.images, generated_image);
            generated_image = null;
        }
        return base.Destroy(force);
    }

    // tg: code/modules/hallucination/nearby_fake_item.dm:24
    public override async DmTask<object> start()
    {
        // This hallucination is purely visual, so we don't need to bother for clientless mobs
        if (hallucinator.client == null || IS_UNCONSCIOUS(hallucinator))
            return FALSE;

        DmList mob_pool = Dm.list();
        foreach (mob_living_carbon_human nearby_mob in Dm.each<mob_living_carbon_human>(view(7, hallucinator)))
        {
            if (nearby_mob == hallucinator)
                continue;
            Dm.append(mob_pool, nearby_mob);
        }

        if (length(mob_pool) == 0)
            return FALSE;

        mob_living_carbon_human who_has_the_item = pick(mob_pool) as mob_living_carbon_human;
        feedback_details += Dm.text("Mob: []", who_has_the_item.real_name);

        if (Dm.truthy(who_has_the_item.get_empty_held_index_for_side(LEFT_HANDS)))
            generated_image = generate_fake_image(who_has_the_item, file: left_hand_file) as image;

        else if (Dm.truthy(who_has_the_item.get_empty_held_index_for_side(RIGHT_HANDS)))
            generated_image = generate_fake_image(who_has_the_item, file: right_hand_file) as image;

        if (generated_image != null)
        {
            if (hallucinator.client != null)
                Dm.append(hallucinator.client.images, generated_image);
            _addtimer(Dm.@new<datum_callback>(this, nameof(remove_image), who_has_the_item), rand(15 * SECONDS, 25 * SECONDS), file: "code/modules/hallucination/nearby_fake_item.dm", line: 49);
            return TRUE;
        }

        return FALSE;
    }

    /// Generates the image with the given file on the passed mob.
    // tg: code/modules/hallucination/nearby_fake_item.dm:56
    public virtual object generate_fake_image(mob_living_carbon_human holder = null, object file = null)
    {
        image fake = image(file, holder, image_icon_state, ABOVE_MOB_LAYER);
        SET_PLANE_EXPLICIT(fake, ABOVE_GAME_PLANE, holder);
        return fake;
    }

    /// Remove the image when all's said and done.
    // tg: code/modules/hallucination/nearby_fake_item.dm:62
    public virtual void remove_image(mob_living_carbon_human holder = null)
    {
        if (QDELETED(this) || QDELETED(hallucinator) || generated_image == null)
            return;

        if (hallucinator.client != null)
            Dm.remove(hallucinator.client.images, generated_image);
        generated_image = null;
        qdel(this);
    }
}

// tg: code/modules/hallucination/nearby_fake_item.dm:70
public partial class datum_hallucination_nearby_fake_item_e_sword : datum_hallucination_nearby_fake_item
{
    // tg: code/modules/hallucination/nearby_fake_item.dm:75
    public override object generate_fake_image(mob_living_carbon_human holder = null, object file = null)
    {
        hallucinator.playsound_local(get_turf(holder), Dm.resource("sound/items/weapons/saberon.ogg"), 35, TRUE);
        return base.generate_fake_image(holder, file);
    }

    // tg: code/modules/hallucination/nearby_fake_item.dm:79
    public override void remove_image(mob_living_carbon_human holder = null)
    {
        if (!QDELETED(holder))
            hallucinator.playsound_local(get_turf(holder), Dm.resource("sound/items/weapons/saberoff.ogg"), 35, TRUE);
        base.remove_image(holder);
    }
}

// tg: code/modules/hallucination/nearby_fake_item.dm:96
public partial class datum_hallucination_nearby_fake_item_baton : datum_hallucination_nearby_fake_item
{
    // tg: code/modules/hallucination/nearby_fake_item.dm:101
    public override object generate_fake_image(mob_living_carbon_human holder = null, object file = null)
    {
        hallucinator.playsound_local(get_turf(holder), SFX_SPARKS, 75, TRUE, -1);
        return base.generate_fake_image(holder, file);
    }
}

// tg: code/modules/hallucination/nearby_fake_item.dm:105
public partial class datum_hallucination_nearby_fake_item_flash : datum_hallucination_nearby_fake_item
{
    // tg: code/modules/hallucination/nearby_fake_item.dm:110
    public override object generate_fake_image(mob_living_carbon_human holder = null, object file = null)
    {
        hallucinator.playsound_local(get_turf(holder), Dm.resource("sound/items/handling/component_pickup.ogg"), 35, vary: FALSE);
        return base.generate_fake_image(holder, file);
    }

    // tg: code/modules/hallucination/nearby_fake_item.dm:114
    public override void remove_image(mob_living_carbon_human holder = null)
    {
        if (!QDELETED(holder))
            hallucinator.playsound_local(get_turf(holder), Dm.resource("sound/items/handling/component_drop.ogg"), 35, vary: FALSE);
        base.remove_image(holder);
    }
}

// tg: code/modules/hallucination/nearby_fake_item.dm:118
public partial class datum_hallucination_nearby_fake_item_armblade : datum_hallucination_nearby_fake_item
{
    // tg: code/modules/hallucination/nearby_fake_item.dm:123
    public override object generate_fake_image(mob_living_carbon_human holder = null, object file = null)
    {
        hallucinator.playsound_local(get_turf(holder), Dm.resource("sound/effects/blob/blobattack.ogg"), 35, TRUE);
        return base.generate_fake_image(holder, file);
    }

    // tg: code/modules/hallucination/nearby_fake_item.dm:127
    public override void remove_image(mob_living_carbon_human holder = null)
    {
        if (!QDELETED(holder))
            hallucinator.playsound_local(get_turf(holder), Dm.resource("sound/effects/blob/blobattack.ogg"), 35, TRUE);
        base.remove_image(holder);
    }
}
