// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/pets/dog/strippable_items.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

//corgi's stippable items

public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/mob/living/basic/pets/dog/strippable_items.dm:3
    public DmList strippable_corgi_items;

    public virtual void InitGlobalstrippable_corgi_items()
    {
        strippable_corgi_items = create_strippable_list(Dm.list(typeof(datum_strippable_item_corgi_head), typeof(datum_strippable_item_corgi_back), typeof(datum_strippable_item_pet_collar), typeof(datum_strippable_item_corgi_id))) as DmList;
        Dm.append(ref gvars_datum_init_order, "strippable_corgi_items");
    }
}

// tg: code/modules/mob/living/basic/pets/dog/strippable_items.dm:11
public partial class datum_strippable_item_corgi_head : datum_strippable_item
{
    // tg: code/modules/mob/living/basic/pets/dog/strippable_items.dm:13
    public override object get_item(atom source = null)
    {
        if (source is not mob_living_basic_pet_dog_corgi corgi_source)
            return null;

        return corgi_source.inventory_head;
    }

    // tg: code/modules/mob/living/basic/pets/dog/strippable_items.dm:20
    public override object finish_equip(atom source = null, obj_item equipping = null, mob user = null)
    {
        if (source is not mob_living_basic_pet_dog_corgi corgi_source)
            return null;

        Waiting.NoSleep(corgi_source.place_on_head(equipping, user as mob_living));
        return null;
    }

    // tg: code/modules/mob/living/basic/pets/dog/strippable_items.dm:27
    public override async DmTask<object> finish_unequip(atom source = null, mob user = null)
    {
        if (source is not mob_living_basic_pet_dog_corgi corgi_source)
            return null;

        await user.put_in_hands(corgi_source.inventory_head);
        corgi_source.inventory_head = null;
        corgi_source.update_corgi_fluff();
        corgi_source.update_appearance(UPDATE_OVERLAYS);
        return null;
    }
}

// tg: code/modules/mob/living/basic/pets/dog/strippable_items.dm:38
public partial class datum_strippable_item_pet_collar : datum_strippable_item
{
    // tg: code/modules/mob/living/basic/pets/dog/strippable_items.dm:40
    public override object get_item(atom source = null)
    {
        if (source is not mob_living_basic)
            return null;

        return Dm.locate<obj_item_clothing_neck_petcollar>(source);
    }

    // tg: code/modules/mob/living/basic/pets/dog/strippable_items.dm:47
    public override async DmTask<object> try_equip(atom source = null, obj_item equipping = null, mob user = null)
    {
        object dot = await base.try_equip(source, equipping, user);
        if (!Dm.truthy(dot))
            return FALSE;

        if (equipping is not obj_item_clothing_neck_petcollar)
        {
            to_chat(user, span_warning("That's not a collar."));
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/modules/mob/living/basic/pets/dog/strippable_items.dm:58
    public override object finish_equip(atom source = null, obj_item equipping = null, mob user = null)
    {
        Waiting.NoSleep(user.transferItemToLoc(equipping, source));
        return null;
    }

    // tg: code/modules/mob/living/basic/pets/dog/strippable_items.dm:61
    public override async DmTask<object> finish_unequip(atom source = null, mob user = null)
    {
        obj_item_clothing_neck_petcollar collar = Dm.locate<obj_item_clothing_neck_petcollar>(source) as obj_item_clothing_neck_petcollar;
        await user.put_in_hands(collar);
        return null;
    }
}

// tg: code/modules/mob/living/basic/pets/dog/strippable_items.dm:66
public partial class datum_strippable_item_corgi_back : datum_strippable_item
{
    // tg: code/modules/mob/living/basic/pets/dog/strippable_items.dm:68
    public override object get_item(atom source = null)
    {
        if (source is not mob_living_basic_pet_dog_corgi corgi_source)
            return null;

        return corgi_source.inventory_back;
    }

    // tg: code/modules/mob/living/basic/pets/dog/strippable_items.dm:75
    public override async DmTask<object> try_equip(atom source = null, obj_item equipping = null, mob user = null)
    {
        object dot = await base.try_equip(source, equipping, user);
        if (!Dm.truthy(dot))
            return FALSE;

        if (!ispath(equipping.dog_fashion, typeof(datum_dog_fashion_back)))
        {
            to_chat(user, span_warning(Dm.text("You set [] on []'s back, but it falls off!", equipping, source)));
            await equipping.forceMove(source.drop_location() as atom);
            if (prob(25))
                await step_rand(equipping);
            Waiting.Detach(dance_rotate(source as atom_movable, set_original_dir: TRUE));

            return FALSE;
        }

        return TRUE;
    }

    // tg: code/modules/mob/living/basic/pets/dog/strippable_items.dm:91
    public override object finish_equip(atom source = null, obj_item equipping = null, mob user = null)
    {
        if (source is not mob_living_basic_pet_dog_corgi corgi_source)
            return null;

        Waiting.NoSleep(equipping.forceMove(corgi_source));
        corgi_source.inventory_back = equipping;
        corgi_source.update_corgi_fluff();
        corgi_source.update_appearance(UPDATE_OVERLAYS);
        return null;
    }

    // tg: code/modules/mob/living/basic/pets/dog/strippable_items.dm:101
    public override async DmTask<object> finish_unequip(atom source = null, mob user = null)
    {
        if (source is not mob_living_basic_pet_dog_corgi corgi_source)
            return null;

        await user.put_in_hands(corgi_source.inventory_back);
        corgi_source.inventory_back = null;
        corgi_source.update_corgi_fluff();
        corgi_source.update_appearance(UPDATE_OVERLAYS);
        return null;
    }
}

// tg: code/modules/mob/living/basic/pets/dog/strippable_items.dm:112
public partial class datum_strippable_item_corgi_id : datum_strippable_item
{
    // tg: code/modules/mob/living/basic/pets/dog/strippable_items.dm:114
    public override object get_item(atom source = null)
    {
        if (source is not mob_living_basic_pet_dog_corgi corgi_source)
            return null;

        return corgi_source.access_card;
    }

    // tg: code/modules/mob/living/basic/pets/dog/strippable_items.dm:121
    public override async DmTask<object> try_equip(atom source = null, obj_item equipping = null, mob user = null)
    {
        object dot = await base.try_equip(source, equipping, user);
        if (!Dm.truthy(dot))
            return FALSE;

        if (!isidcard(equipping))
        {
            to_chat(user, span_warning(Dm.text("You can't pin [] to []!", equipping, source)));
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/modules/mob/living/basic/pets/dog/strippable_items.dm:132
    public override object finish_equip(atom source = null, obj_item equipping = null, mob user = null)
    {
        if (source is not mob_living_basic_pet_dog_corgi corgi_source)
            return null;

        Waiting.NoSleep(equipping.forceMove(source));
        corgi_source.access_card = equipping as obj_item_card_id;
        return null;
    }

    // tg: code/modules/mob/living/basic/pets/dog/strippable_items.dm:140
    public override async DmTask<object> finish_unequip(atom source = null, mob user = null)
    {
        if (source is not mob_living_basic_pet_dog_corgi corgi_source)
            return null;

        await user.put_in_hands(corgi_source.access_card);
        corgi_source.access_card = null;
        corgi_source.update_corgi_fluff();
        corgi_source.update_appearance(UPDATE_OVERLAYS);
        return null;
    }
}
