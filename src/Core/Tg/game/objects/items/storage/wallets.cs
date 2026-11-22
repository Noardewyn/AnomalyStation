// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/wallets.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R2 (D327): the whole file (the Misplaced Wallet and the Wallets! station traits hand them out): the wallet in the ID slot, its
// front ID (the card with the most command access) shown on it, named after it and read for access, the random and the toilet
// wallets. Deferred: the strip menu's reach into the wallet (/datum/element/strip_accessible_storage is not ported). The var blocks
// are generated (D111).

// tg: code/game/objects/items/storage/wallets.dm:1
public partial class obj_item_storage_wallet : obj_item_storage
{
    // tg: code/game/objects/items/storage/wallets.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // AddElement(/datum/element/strip_accessible_storage): the strip menu's reach into storage is not ported (inert)
        Dm.Deferred("code/game/objects/items/storage/wallets.dm:17", "wallet Initialize: AddElement(/datum/element/strip_accessible_storage), the strip menu's storage access (not ported)");
        return dot;
    }

    // tg: code/game/objects/items/storage/wallets.dm:19
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (isidcard(gone))
            refreshID();
        return dot;
    }

    /**
     * Calculates the new front ID.
     *
     * Picks the ID card that has the most combined command or higher tier accesses.
     */
    // tg: code/game/objects/items/storage/wallets.dm:29
    public virtual void refreshID()
    {
        LAZYCLEARLIST(combined_access);

        front_id = null;
        float winning_tally = 0;
        bool is_magnetic_found = false;
        foreach (obj_item_card_id id_card in Dm.each<obj_item_card_id>(contents))
        {
            // Certain IDs can forcibly jump to the front so they can disguise other cards in wallets. Chameleon/Agent ID cards are an example of this.
            if (!is_magnetic_found && HAS_TRAIT(id_card, TRAIT_MAGNETIC_ID_CARD))
            {
                front_id = id_card;
                is_magnetic_found = true;
            }

            if (!is_magnetic_found)
            {
                float card_tally = Dm.num(SSid_access.tally_access(id_card, ACCESS_FLAG_PRV_COMMAND));
                if (card_tally > winning_tally)
                {
                    winning_tally = card_tally;
                    front_id = id_card;
                }
            }

            LAZYINITLIST(ref combined_access);
            Dm.combine(ref combined_access, id_card.access);
        }

        // If we didn't pick a front ID - Maybe none of our cards have any command accesses? Just grab the first card (if we even have one).
        // We could also have no ID card in the wallet at all, which will mean we end up with a null front_id and that's fine too.
        if (front_id == null)
            front_id = Dm.locate<obj_item_card_id>(contents);

        if (ishuman(loc))
        {
            var wearing_human = (mob_living_carbon_human)loc;
            if (wearing_human.wear_id == this)
                wearing_human.update_ID_card();
        }

        update_label();
        update_appearance(UPDATE_ICON);
        update_slot_icon();
    }

    // tg: code/game/objects/items/storage/wallets.dm:63
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        if (isidcard(arrived))
            refreshID();
        return dot;
    }

    // tg: code/game/objects/items/storage/wallets.dm:68
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        cached_flat_icon = null;
        if (front_id == null)
            return dot;
        Dm.append(ref dot, mutable_appearance(front_id.icon, front_id.icon_state));
        Dm.append(ref dot, front_id.overlays);
        Dm.append(ref dot, mutable_appearance(icon, overlay_icon_state));
        return dot;
    }

    // tg: code/game/objects/items/storage/wallets.dm:77
    public virtual object get_cached_flat_icon()
    {
        // getFlatIcon(src): the wallet's flattened picture for the chat (the 3D client's chat is text: the chat icons, as the ID card's)
        Dm.Deferred("code/game/objects/items/storage/wallets.dm:79", "get_cached_flat_icon: getFlatIcon, the wallet's picture in chat (the chat icons)");
        return cached_flat_icon;
    }

    // tg: code/game/objects/items/storage/wallets.dm:82
    public override object get_examine_icon(mob user = null)
    {
        // icon2html(get_cached_flat_icon(), user): the wallet's picture in the examine line (the chat icons, as the ID card's)
        Dm.Deferred("code/game/objects/items/storage/wallets.dm:83", "get_examine_icon: icon2html of the wallet's flat icon (the 3D client's chat is text: the chat icons)");
        return null;
    }

    // tg: code/game/objects/items/storage/wallets.dm:85
    public virtual void update_label()
    {
        if (front_id != null)
            name = I18n.T("[] displaying []", Dm.initial(this, (obj_item_storage_wallet p) => p.name), front_id);
        else
            name = Dm.initial(this, (obj_item_storage_wallet p) => p.name);
    }

    // tg: code/game/objects/items/storage/wallets.dm:91
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (front_id != null)
            Dm.append(ref dot, span_notice(I18n.T("Alt-click to remove the id.")));
        return dot;
    }

    // tg: code/game/objects/items/storage/wallets.dm:96
    public override object get_id_examine_strings(mob user = null)
    {
        DmList dot = base.get_id_examine_strings(user) as DmList;
        if (front_id != null)
            Dm.append(ref dot, front_id.get_id_examine_strings(user));
        return dot;
    }

    // tg: code/game/objects/items/storage/wallets.dm:101
    public override object GetID()
    {
        return front_id;
    }

    // tg: code/game/objects/items/storage/wallets.dm:104
    public override async DmTask<object> remove_id(params object[] __extra)
    {
        if (front_id == null)
            return null;
        object dot = front_id;
        await front_id.forceMove(get_turf(this));
        return dot;
    }

    // tg: code/game/objects/items/storage/wallets.dm:110
    [DmParams("inserting_item")]
    public override async DmTask<object> insert_id(params object[] __extra)
    {
        var inserting_item = Dm.arg<obj_item>(__extra, 0);
        var inserting_id = await inserting_item.remove_id() as obj_item_card;
        if (inserting_id == null)
            return FALSE;
        await attackby(inserting_id);
        if (Dm.@in(inserting_id, contents))
            return TRUE;
        return FALSE;
    }

    // tg: code/game/objects/items/storage/wallets.dm:119
    public override object GetAccess()
    {
        if (Dm.truthy(LAZYLEN(combined_access)))
            return combined_access;
        else
            return base.GetAccess();
    }
}

// tg: code/game/objects/items/storage/wallets.dm:125
public partial class obj_item_storage_wallet_random : obj_item_storage_wallet
{
    // tg: code/game/objects/items/storage/wallets.dm:129
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = "wallet";
        return dot;
    }

    // tg: code/game/objects/items/storage/wallets.dm:133
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_holochip>(this, rand(5, 30));
        Dm.@new<obj_effect_spawner_random_entertainment_wallet_storage>(this);
        await DmTask.CompletedTask;
    }
}

///Used by the toilet fish source.
// tg: code/game/objects/items/storage/wallets.dm:138
public partial class obj_item_storage_wallet_money : obj_item_storage_wallet
{
    // tg: code/game/objects/items/storage/wallets.dm:141
    public override async DmTask PopulateContents()
    {
        float iterations = Dm.num(pick(3, 4));
        for (float iteration = 1; iteration <= iterations; iteration++)
            Dm.@new<obj_item_holochip>(this, rand(50, 450));
        await DmTask.CompletedTask;
    }
}
