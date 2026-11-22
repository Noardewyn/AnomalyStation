// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/outfit_wardrobe.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST5 (D333): the file whole. The outfit wardrobe (a ruin's dress-up cabinet): a human picks one of its outfits on a radial
// menu, the cabinet opens (its "_open" sprite, the zip and the wooden closet's sounds), and after three seconds the human is
// stripped and dressed in it; each wardrobe id is used once per human, a limited outfit runs out. The var block is generated (D111).

// tg: code/game/objects/structures/outfit_wardrobe.dm:4
public partial class obj_structure_outfit_wardrobe : obj_structure
{
    // if you need to access this elsewhere you already fucked up
    // tg: code/game/objects/structures/outfit_wardrobe.dm:2
    internal const string TRAIT_WARDROBE_USED = "wardrobe_used";

    // tg: code/game/objects/structures/outfit_wardrobe.dm:30
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers);
        if (Dm.truthy(dot))
            return dot;

        if (user is not mob_living_carbon_human human_user)
            return dot;

        if (Dm.truthy(one_use) && HAS_TRAIT_FROM(human_user, TRAIT_WARDROBE_USED, wardrobe_id))
        {
            to_chat(human_user, span_notice(I18n.T("You already picked an outfit!")));
            return dot;
        }

        DmList display_classes = Dm.list();
        Type chosen_class;

        foreach (Type dressup in Dm.each<Type>(selectable_outfits_to_amount))
        {
            if (Dm.eq(selectable_outfits_to_amount[dressup], 0))
                continue;
            datum_radial_menu_choice option = Dm.@new<datum_radial_menu_choice>(this);
            // We take the type of either the suit storage or head item (Likely to be relevant) or barring that the uniform
            Type sprite_path = (Dm.initial(dressup, (datum_outfit p) => p.suit_store) as Type) ?? (Dm.initial(dressup, (datum_outfit p) => p.head) as Type) ?? (Dm.initial(dressup, (datum_outfit p) => p.uniform) as Type);
            if (sprite_path == null)
            {
                stack_trace(Dm.text("[] outfit got no god damn items to use for sprite", dressup), "code/game/objects/structures/outfit_wardrobe.dm", 53);
                sprite_path = typeof(obj_item_food_grown_citrus_orange_3d);
            }
            option.image = image(icon: Dm.initial(sprite_path, (atom p) => p.icon), icon_state: Dm.initial(sprite_path, (atom p) => p.icon_state));
            option.info = span_boldnotice(Dm.text("[]", Dm.initial(dressup, (datum_outfit p) => p.name))); // no desc..
            display_classes[dressup] = option;
        }

        if (!Dm.truthy(Dm.length(display_classes)))
        {
            to_chat(human_user, span_warning(I18n.T("There are no available outfits!")));
            return dot;
        }

        sort_list(display_classes);
        object choice = await show_radial_menu(human_user, this, display_classes, radius: 38, require_near: TRUE);
        if (!Dm.truthy(choice))
            return dot;

        chosen_class = choice as Type;

        human_user.balloon_alert(human_user, LOWER_TEXT(Dm.initial(chosen_class, (datum_outfit p) => p.name)));
        playsound(human_user, Dm.resource("sound/items/zip/un_zip.ogg"), 33);
        playsound(this, Dm.resource("sound/machines/closet/wooden_closet_open.ogg"), 25);
        icon_state = "fullcabinet_open";
        if (!Dm.truthy(await do_after(human_user, 3 * SECONDS)) || Dm.eq(selectable_outfits_to_amount[choice], 0))
        {
            playsound(this, Dm.resource("sound/machines/closet/wooden_closet_close.ogg"), 50);
            icon_state = base_icon_state as string;
            return dot;
        }
        selectable_outfits_to_amount[choice] = Dm.num(selectable_outfits_to_amount[choice]) - 1;
        playsound(this, Dm.resource("sound/machines/closet/wooden_closet_close.ogg"), 50);
        icon_state = base_icon_state as string;
        playsound(human_user, Dm.resource("sound/items/zip/zip_up.ogg"), 33);

        await human_user.drop_everything();
        await human_user.equipOutfit(chosen_class);
        ADD_TRAIT(human_user, TRAIT_WARDROBE_USED, wardrobe_id);
        return dot;
    }
}
