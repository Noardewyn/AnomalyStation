// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/fancy.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps J3, D298): the fancy base, the cigarette packs, the rolling paper pack and the cigar cases; gaps H2 (D333): the
// donut box, the nugget box, the pickle jar and the coffee condiments display. Left: the cigarette pack's screentip (a
// live-partial row). The var blocks are generated.

// tg: code/game/objects/items/storage/fancy.dm:17
public partial class obj_item_storage_fancy : obj_item_storage
{
    // tg: code/game/objects/items/storage/fancy.dm:34
    public override async DmTask PopulateContents()
    {
        if (!Dm.truthy(spawn_type))
            return;
        for (float i = 1; i <= spawn_count; i++)
        {
            object thing_in_box = spawn_type is DmList spawn_list ? pick(spawn_list) : spawn_type;
            Dm.@new((Type)thing_in_box, this);
        }
    }

    // tg: code/game/objects/items/storage/fancy.dm:41
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]", base_icon_state) + (Dm.truthy(has_open_closed_states) && Dm.truthy(open_status) ? Dm.text("[]", Dm.length(contents)) : "");
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/storage/fancy.dm:45
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!Dm.truthy(open_status))
            return dot;
        if (Dm.length(contents) == 1)
            dot.Add(I18n.T("There is one [] left.", contents_tag));
        else
            dot.Add(I18n.T("There are [] []s left.", Dm.length(contents) <= 0 ? "no" : Dm.text("[]", Dm.length(contents)), contents_tag));
        return dot;
    }

    // tg: code/game/objects/items/storage/fancy.dm:54
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (open_status == FANCY_CONTAINER_CLOSED)
            open_status = FANCY_CONTAINER_OPEN;
        else if (open_status == FANCY_CONTAINER_OPEN)
            open_status = FANCY_CONTAINER_CLOSED;

        update_appearance();
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(Dm.length(contents)))
            return dot;
        if (foldable_result == null || Dm.truthy(Dm.bitand(flags_1, HOLOGRAM_1)))
            return dot;
        obj_item result = Dm.@new(foldable_result, user.drop_location()) as obj_item;
        balloon_alert(user, I18n.T("folded"));
        // Gotta delete first, so then the cardboard appears in the same hand
        qdel(this);
        await user.put_in_hands(result);
        return dot;
    }

    // tg: code/game/objects/items/storage/fancy.dm:72
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (open_status == FANCY_CONTAINER_CLOSED)
            open_status = FANCY_CONTAINER_OPEN;
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/storage/fancy.dm:78
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        if (open_status == FANCY_CONTAINER_CLOSED)
            open_status = FANCY_CONTAINER_OPEN;
        update_appearance();
        return dot;
    }
}

/*
 * Donut Box
 */
// tg: code/game/objects/items/storage/fancy.dm:84  #define DONUT_INBOX_SPRITE_WIDTH 4 (file-local)
// tg: code/game/objects/items/storage/fancy.dm:90
public partial class obj_item_storage_fancy_donut_box : obj_item_storage_fancy
{
    private const float DONUT_INBOX_SPRITE_WIDTH = 4;

    // tg: code/game/objects/items/storage/fancy.dm:104
    public override async DmTask PopulateContents()
    {
        await base.PopulateContents();
        update_appearance();
    }

    // tg: code/game/objects/items/storage/fancy.dm:108
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(open_status) ? "_inner" : null);
        return dot;
    }

    // tg: code/game/objects/items/storage/fancy.dm:112
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(open_status))
            return dot;

        float donuts = 0;
        foreach (object _donut in Dm.each(contents))
        {
            obj_item_food_donut donut = _donut as obj_item_food_donut;
            if (donut == null)
                continue;

            image donut_image = image(icon: Dm.initial(this, (atom a) => a.icon), icon_state: donut.in_box_sprite());
            donut_image.pixel_w = donuts * DONUT_INBOX_SPRITE_WIDTH;
            Dm.append(ref dot, donut_image);
            donuts += 1;
        }

        Dm.append(ref dot, image(icon: Dm.initial(this, (atom a) => a.icon), icon_state: Dm.text("[]_top", base_icon_state)));
        return dot;
    }
}

////////////
//CIG PACK//
////////////

// tg: code/game/objects/items/storage/fancy.dm:184
public partial class obj_item_storage_fancy_cigarettes : obj_item_storage_fancy
{
    // tg: code/game/objects/items/storage/fancy.dm:214
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.length(contents) != 0 || !Dm.truthy(spawn_coupon))
            return await base.attack_self(user, modifiers, __extra);

        playsound(this, Dm.initial(storage_type, (datum_storage p) => p.rustle_sound), 50, TRUE);

        balloon_alert(user, I18n.T("ooh, free coupon"));
        obj_item_coupon attached_coupon = Dm.@new<obj_item_coupon>();
        await user.put_in_hands(attached_coupon);
        await attached_coupon.generate(Dm.truthy(rigged_omen) ? COUPON_OMEN : null, null, user);
        attached_coupon = null;
        spawn_coupon = FALSE;
        name = "discarded cigarette packet";
        desc = "An old cigarette packet with the back torn off, worth less than nothing now.";
        atom_storage.max_slots = 0;
        return null;
    }

    // tg: code/game/objects/items/storage/fancy.dm:230
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        // register_context(): the context tips (screentips, not now)
        if (!Dm.truthy(spawn_count))
            update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/storage/fancy.dm:237
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (interacting_with != user) // you can quickly put a cigarette in your mouth only
            return await base.interact_with_atom(interacting_with, user, modifiers);
        await quick_remove_item(Dm.locate<obj_item_cigarette>(contents), user, equip_to_mouth: TRUE);
        return dot;
    }

    // tg: code/game/objects/items/storage/fancy.dm:243
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        await base.attack_hand_secondary(user, modifiers);
        await quick_remove_item(Dm.locate<obj_item_cigarette>(contents), user);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/objects/items/storage/fancy.dm:248
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        obj_item lighter = Dm.locate<obj_item_lighter>(contents);
        if (lighter != null)
            await quick_remove_item(lighter, user);
        else
            await quick_remove_item(Dm.locate<obj_item_cigarette>(contents), user);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/storage/fancy.dm:263
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;

        if (Dm.truthy(spawn_coupon))
            dot.Add(span_notice(I18n.T("There's a coupon on the back of the pack! You can tear it off once it's empty.")));
        return dot;
    }

    // tg: code/game/objects/items/storage/fancy.dm:269
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[]", base_icon_state) + (Dm.truthy(Dm.length(contents)) ? "" : "_empty");
        return dot;
    }

    // tg: code/game/objects/items/storage/fancy.dm:273
    public virtual object open_icon_state()
    {
        return Dm.text("[]_open", icon_state);
    }

    // tg: code/game/objects/items/storage/fancy.dm:276
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (!Dm.truthy(open_status) || !Dm.truthy(Dm.length(contents)))
            return dot;

        dot.Add(open_icon_state());

        if (!Dm.truthy(display_cigs))
            return dot;

        int cig_position = 1;
        foreach (object C in Dm.each(contents))
        {
            string use_icon_state = "";

            if (istype(C, typeof(obj_item_lighter_greyscale)))
                use_icon_state = "lighter_in";
            else if (istype(C, typeof(obj_item_lighter)))
                use_icon_state = "zippo_in";
            else if (Dm.truthy(candy))
                use_icon_state = "candy";
            else
                use_icon_state = "cigarette";

            dot.Add(Dm.text("[]_[]", use_icon_state, cig_position));
            cig_position++;
        }
        return dot;
    }
}

// tg: code/game/objects/items/storage/fancy.dm:404
public partial class obj_item_storage_fancy_cigarettes_cigpack_candy : obj_item_storage_fancy_cigarettes
{
    // tg: code/game/objects/items/storage/fancy.dm:413
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(7))
            spawn_type = typeof(obj_item_cigarette_candy_nicotine); //uh oh!
        return dot;
    }
}

// tg: code/game/objects/items/storage/fancy.dm:475
public partial class obj_item_storage_fancy_rollingpapers : obj_item_storage_fancy
{
    // tg: code/game/objects/items/storage/fancy.dm:488
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (!Dm.truthy(spawn_count))
            update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/storage/fancy.dm:494
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (!Dm.truthy(Dm.length(contents)))
            dot.Add(Dm.text("[]_empty", base_icon_state));
        return dot;
    }
}

/////////////
//CIGAR BOX//
/////////////
// tg: code/game/objects/items/storage/fancy.dm:507
public partial class obj_item_storage_fancy_cigarettes_cigars : obj_item_storage_fancy_cigarettes
{
    // tg: code/game/objects/items/storage/fancy.dm:520
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        //reset any changes the parent call may have made
        icon_state = Dm.str(base_icon_state);
        return dot;
    }

    // tg: code/game/objects/items/storage/fancy.dm:529
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (!Dm.truthy(open_status))
            return dot;
        int cigar_position = 1; //generate sprites for cigars in the box
        foreach (obj_item_cigarette_cigar smokes in Dm.each<obj_item_cigarette_cigar>(contents))
        {
            dot.Add(Dm.text("[]_[]", smokes.icon_off, cigar_position));
            cigar_position++;
        }
        return dot;
    }
}

// tg: code/game/objects/items/storage/fancy.dm:550
public partial class obj_item_storage_fancy_cigarettes_cigars_havana : obj_item_storage_fancy_cigarettes_cigars
{
    // tg: code/game/objects/items/storage/fancy.dm:557
    public override object open_icon_state()
    {
        return "cohibacase_open";
    }
}

// tg: code/game/objects/items/storage/fancy.dm:588
public partial class obj_item_storage_fancy_nugget_box : obj_item_storage_fancy
{
    // tg: code/game/objects/items/storage/fancy.dm:603
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // It's a safe place for the Fryish/Fritterish
        Waiting.NoSleep(AddElement(typeof(datum_element_fish_safe_storage)));
        return dot;
    }
}

/*
 * Jar of pickles
 */
// tg: code/game/objects/items/storage/fancy.dm:650
public partial class obj_item_storage_fancy_pickles_jar : obj_item_storage_fancy
{
    // tg: code/game/objects/items/storage/fancy.dm:665
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (!Dm.truthy(contents.len))
            icon_state = Dm.text("[]_empty", base_icon_state);
        else
        {
            if (contents.len < 5)
                icon_state = Dm.text("[]_[]", base_icon_state, contents.len);
            else
                icon_state = Dm.text("[]", base_icon_state);
        }
        return dot;
    }
}

/*
 * Coffee condiments display
 */
// tg: code/game/objects/items/storage/fancy.dm:679 (gaps H2, D333)
public partial class obj_item_storage_fancy_coffee_condi_display : obj_item_storage_fancy
{
    // tg: code/game/objects/items/storage/fancy.dm:693
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        float has_sugar = FALSE;
        float has_sweetener = FALSE;
        float has_creamer = FALSE;
        float has_chocolate = FALSE;

        foreach (object thing in Dm.each(contents))
        {
            if (thing is obj_item_reagent_containers_condiment_pack_sugar)
                has_sugar = TRUE;
            else if (thing is obj_item_reagent_containers_condiment_pack_astrotame)
                has_sweetener = TRUE;
            else if (thing is obj_item_reagent_containers_condiment_creamer)
                has_creamer = TRUE;
            else if (thing is obj_item_reagent_containers_condiment_chocolate)
                has_chocolate = TRUE;
        }

        if (Dm.truthy(has_sugar))
            Dm.append(ref dot, "condi_display_sugar");
        if (Dm.truthy(has_sweetener))
            Dm.append(ref dot, "condi_display_sweetener");
        if (Dm.truthy(has_creamer))
            Dm.append(ref dot, "condi_display_creamer");
        if (Dm.truthy(has_chocolate))
            Dm.append(ref dot, "condi_display_chocolate");
        return dot;
    }

    // tg: code/game/objects/items/storage/fancy.dm:719
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 4; i++)
            Dm.@new<obj_item_reagent_containers_condiment_pack_sugar>(this);
        for (float i = 1; i <= 3; i++)
            Dm.@new<obj_item_reagent_containers_condiment_pack_astrotame>(this);
        for (float i = 1; i <= 4; i++)
            Dm.@new<obj_item_reagent_containers_condiment_creamer>(this);
        for (float i = 1; i <= 3; i++)
            Dm.@new<obj_item_reagent_containers_condiment_chocolate>(this);
        update_appearance();
    }
}
