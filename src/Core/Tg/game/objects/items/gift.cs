// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/gift.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R3 (D327): the whole file: the gifts (a random toy or prize inside, seen by Santa's present vision) and Santa's "anything"
// gifts with their recipient. The gift's var block is hand-written, as contains_type, a type path in a var typed /obj/item, is a Type
// here (rules 3.5); the anything gift's is generated (D111).

/* Gifts
 * Contains:
 * Gifts
 */

/// Gifts to give to players, will contain a nice toy or other fun item for them to play with.
// tg: code/game/objects/items/gift.dm:7
public partial class obj_item_gift : obj_item
{
    /// What type of thing are we guaranteed to spawn in with?
    // tg: code/game/objects/items/gift.dm:16
    public Type contains_type = null;

    // tg: code/game/objects/items/gift.dm:8  (a hand-written var block: contains_type holds a type path, types-converter.md 3.4)
    public obj_item_gift()
    {
        name = "gift";
        desc = "PRESENTS!!!! eek!";
        icon = Dm.resource("icons/obj/storage/wrapping.dmi");
        icon_state = "giftdeliverypackage3";
        inhand_icon_state = "gift";
        resistance_flags = FLAMMABLE;
    }

    // tg: code/game/objects/items/gift.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        pixel_x = rand(-10, 10);
        pixel_y = rand(-10, 10);
        icon_state = Dm.text("giftdeliverypackage[]", rand(1, 5));

        if (contains_type == null)
            contains_type = get_gift_type() as Type;
        return dot;
    }

    // tg: code/game/objects/items/gift.dm:27
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] peeks inside [] and cries []self to death! It looks like [] [] on the naughty list...", user, this, user.p_them(), user.p_they(), user.p_were())));
        await DmTask.CompletedTask;
        return BRUTELOSS;
    }

    // tg: code/game/objects/items/gift.dm:31
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (HAS_MIND_TRAIT(user, TRAIT_PRESENT_VISION) || isobserver(user))
            Dm.append(ref dot, span_notice(I18n.T("It contains \\a [].", Dm.initial(contains_type, (obj_item p) => p.name))));
        return dot;
    }

    // tg: code/game/objects/items/gift.dm:36
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (HAS_MIND_TRAIT(user, TRAIT_CANNOT_OPEN_PRESENTS))
        {
            to_chat(user, span_warning(I18n.T("You're supposed to be spreading gifts, not opening them yourself!")));
            return null;
        }

        await moveToNullspace();

        var thing = (obj_item)Dm.@new(contains_type, get_turf(user));

        if (QDELETED(thing)) //might contain something like metal rods that might merge with a stack on the ground
            user.visible_message(span_danger(I18n.T("Oh no! The present that [] opened had nothing inside it!", user)));
        else
        {
            user.visible_message(span_notice(I18n.T("[] unwraps \\the [], finding \\a [] inside!", user, this, thing)));
            user.investigate_log(Dm.text("has unwrapped a present containing [].", thing.GetType()), INVESTIGATE_PRESENTS);
            await user.put_in_hands(thing);
            thing.add_fingerprint(user);
            if (Dm.truthy(thing._listen_lookup?[COMSIG_ITEM_OPENED_FROM_GIFT]))
                thing._SendSignal(COMSIG_ITEM_OPENED_FROM_GIFT, Dm.list(thing, user));
        }

        qdel(this);
        return null;
    }

    // tg: code/game/objects/items/gift.dm:56
    public virtual object get_gift_type()
    {
        if (get_gift_type__gift_type_list == null)
        {
            get_gift_type__gift_type_list = Dm.list(
                typeof(obj_item_banhammer),
                typeof(obj_item_bikehorn),
                typeof(obj_item_book_manual_chef_recipes),
                typeof(obj_item_book_manual_wiki_barman_recipes),
                typeof(obj_item_clothing_head_costume_snowman),
                typeof(obj_item_clothing_neck_tie_horrible),
                typeof(obj_item_clothing_suit_costume_poncho),
                typeof(obj_item_clothing_suit_costume_poncho_green),
                typeof(obj_item_clothing_suit_costume_poncho_red),
                typeof(obj_item_clothing_suit_costume_snowman),
                typeof(obj_item_clothing_suit_jacket_leather),
                typeof(obj_item_clothing_suit_jacket_leather_biker),
                typeof(obj_item_food_grown_ambrosia_deus),
                typeof(obj_item_food_grown_ambrosia_vulgaris),
                typeof(obj_item_grenade_smokebomb),
                typeof(obj_item_grown_corncob),
                typeof(obj_item_instrument_guitar),
                typeof(obj_item_instrument_violin),
                typeof(obj_item_lipstick_random),
                typeof(obj_item_pai_card),
                typeof(obj_item_pen_invisible),
                typeof(obj_item_pickaxe_diamond),
                typeof(obj_item_poster_random_contraband),
                typeof(obj_item_poster_random_official),
                typeof(obj_item_soap_deluxe),
                typeof(obj_item_sord),
                typeof(obj_item_stack_sheet_mineral_coal),
                typeof(obj_item_storage_backpack_holding),
                typeof(obj_item_storage_belt_champion),
                typeof(obj_item_storage_belt_utility_full),
                typeof(obj_item_storage_box_snappops),
                typeof(obj_item_storage_crayons),
                typeof(obj_item_storage_photo_album),
                typeof(obj_item_storage_wallet),
                typeof(obj_item_toy_basketball),
                typeof(obj_item_toy_beach_ball));

            Dm.append(ref get_gift_type__gift_type_list, subtypesof(typeof(obj_item_clothing_head_collectable)));
            //Add all toys, except for abstract types and syndicate cards.
            Dm.append(ref get_gift_type__gift_type_list, Dm.sub(subtypesof(typeof(obj_item_toy)), Dm.add(Dm.add(Dm.sub(typesof(typeof(obj_item_toy_cards)), typeof(obj_item_toy_cards_deck)), typeof(obj_item_toy_figure)), typeof(obj_item_toy_ammo))));
        }

        object gift_type = pick(get_gift_type__gift_type_list);
        return gift_type;
    }

    // tg: code/game/objects/items/gift.dm:57  var/static/list/gift_type_list = null (a proc-level static, rules 10.6)
    private static ref DmList get_gift_type__gift_type_list => ref TgState.Current.obj_item_gift__get_gift_type__gift_type_list;
}

/// Gifts that typically only very OP stuff or admins or Santa Claus himself should be giving out, as they contain ANY valid subtype of `/obj/item`, including stuff like instagib rifles. Wow!
// tg: code/game/objects/items/gift.dm:107
public partial class obj_item_gift_anything : obj_item_gift
{
    // tg: code/game/objects/items/gift.dm:113
    public override object get_gift_type()
    {
        if (get_gift_type__possible_gifts == null)
            get_gift_type__possible_gifts = get_sane_item_types(typeof(obj_item)) as DmList;

        object gift_type = pick(get_gift_type__possible_gifts);
        return gift_type;
    }

    // tg: code/game/objects/items/gift.dm:114  var/static/list/obj/item/possible_gifts = null (a proc-level static, rules 10.6)
    private static ref DmList get_gift_type__possible_gifts => ref TgState.Current.obj_item_gift_anything__get_gift_type__possible_gifts;

    // tg: code/game/objects/items/gift.dm:122
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (recipient_ref == null)
            return await base.attack_self(user, modifiers, __extra);

        var recipient = recipient_ref.resolve() as datum_mind;
        if (recipient != null && recipient != user?.mind)
        {
            to_chat(user, span_notice(I18n.T("This gift isn't for you, and you don't want to get on Santa's bad side!")));
            return FALSE;
        }
        return await base.attack_self(user, modifiers, __extra);
    }

    /// Assign the mind of someone as the person this gift is for
    // tg: code/game/objects/items/gift.dm:133
    public virtual void assign_recipient(object recipient = null)
    {
        if (ismob(recipient)) // You're presumably at this point because you are an admin who fucked up but I will save you
        {
            var recipient_mob = (mob)recipient;
            recipient = recipient_mob.mind;
        }

        if (recipient == null)
            return;

        var recipient_mind = (datum_mind)recipient;
        name = I18n.T("[] for [] ([])", Dm.initial(this, (obj_item p) => p.name), recipient_mind.name, recipient_mind.assigned_role.title);
        recipient_ref = WEAKREF(recipient_mind) as datum_weakref;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/gift.dm:57  get_gift_type's var/static/list/gift_type_list = null
    internal DmList obj_item_gift__get_gift_type__gift_type_list = null;
    // tg: code/game/objects/items/gift.dm:114  /obj/item/gift/anything/get_gift_type's var/static/list/obj/item/possible_gifts = null
    internal DmList obj_item_gift_anything__get_gift_type__possible_gifts = null;
}
