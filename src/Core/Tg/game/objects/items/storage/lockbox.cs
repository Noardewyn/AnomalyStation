// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/lockbox.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P3: the PopulateContents of Meta's containers in this file (D133); the base lockbox, the medal box and the security
// boxes with gaps J4 (D298), the order lockbox with gaps P4b; gaps SR1 (D333): the clusterbang lockbox, the service award box and the
// encrypted curiosity; gaps GU1 (D333): the dueling pistol case with its two paired pistols. Gaps BR1 (D339): the
// decrypted curiosity (bitrunning's secondary loot).

// tg: code/game/objects/items/storage/lockbox.dm:1
public partial class obj_item_storage_lockbox : obj_item_storage
{
    // tg: code/game/objects/items/storage/lockbox.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        // register_context: the screentips (a row)
        Waiting.NoSleep(update_icon_state());
        Dm.Deferred("code/game/objects/items/storage/lockbox.dm:25", "Initialize: AddElement(/datum/element/cuffable_item): cuffing the box to the wrist (the element and the cuffed_item status effect)");
        return dot;
    }

    // tg: code/game/objects/items/storage/lockbox.dm:38
    public override async DmTask<object> tool_act(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        obj_item_card card = tool.GetID() as obj_item_card;
        if (card == null)
            return await base.tool_act(user, tool, modifiers);

        if (Dm.truthy(can_unlock(user, card as obj_item_card_id)))
        {
            toggle_locked(user);
            return ITEM_INTERACT_SUCCESS;
        }

        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/game/objects/items/storage/lockbox.dm:49
    public virtual object can_unlock(mob_living user = null, obj_item_card_id id_card = null, float? silent = null)
    {
        if (Dm.truthy(check_access(id_card)))
            return TRUE;
        if (!Dm.truthy(silent))
            balloon_alert(user, I18n.T("access denied!"));
        return FALSE;
    }

    // tg: code/game/objects/items/storage/lockbox.dm:56
    public virtual void toggle_locked(mob_living user = null)
    {
        atom_storage.set_locked(Dm.truthy(atom_storage.locked) ? STORAGE_NOT_LOCKED : STORAGE_FULLY_LOCKED);
        balloon_alert(user, Dm.truthy(atom_storage.locked) ? I18n.T("locked") : I18n.T("unlocked"));
    }

    // tg: code/game/objects/items/storage/lockbox.dm:60
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(broken))
            icon_state = icon_broken;
        else if (Dm.truthy(atom_storage?.locked))
            icon_state = icon_locked;
        else if (Dm.truthy(open))
            icon_state = icon_open;
        else
            icon_state = icon_closed;
        return dot;
    }

    // tg: code/game/objects/items/storage/lockbox.dm:71
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (!Dm.truthy(broken))
        {
            broken = TRUE;
            atom_storage.set_locked(STORAGE_NOT_LOCKED);
            balloon_alert(user, I18n.T("lock destroyed"));
            if (emag_card != null && user != null)
                user.visible_message(span_warning(I18n.T("[] swipes [] over [], breaking it!", user, emag_card, this)));
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/objects/items/storage/lockbox.dm:81
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(broken))
            dot.Add(span_notice(I18n.T("It appears to be broken.")));
        return dot;
    }

    // tg: code/game/objects/items/storage/lockbox.dm:86
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        open = TRUE;
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/storage/lockbox.dm:91
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        open = TRUE;
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/items/storage/lockbox.dm:100
public partial class obj_item_storage_lockbox_loyalty : obj_item_storage_lockbox
{
    // tg: code/game/objects/items/storage/lockbox.dm:100
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/items/storage/lockbox.dm:101
        {
            Dm.@new<obj_item_implantcase_mindshield>(this); // tg: code/game/objects/items/storage/lockbox.dm:102
        }
        Dm.@new<obj_item_implanter_mindshield>(this); // tg: code/game/objects/items/storage/lockbox.dm:103
    }
}

// tg: code/game/objects/items/storage/lockbox.dm:139
public partial class obj_item_storage_lockbox_medal : obj_item_storage_lockbox
{
    // tg: code/game/objects/items/storage/lockbox.dm:128
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!Dm.truthy(atom_storage.locked))
            dot.Add(span_notice(I18n.T("Alt-click to [] it.", Dm.truthy(open) ? I18n.T("close") : I18n.T("open"))));
        return dot;
    }

    // tg: code/game/objects/items/storage/lockbox.dm:133
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(atom_storage.locked))
        {
            open = Dm.truthy(open) ? FALSE : TRUE;
            update_appearance();
        }
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/storage/lockbox.dm:150
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (contents == null || !Dm.truthy(open))
            return dot;
        if (Dm.truthy(atom_storage?.locked))
            return dot;
        for (int i = 1; i <= length(contents); i++)
        {
            obj_item_clothing_accessory_medal M = contents[i] as obj_item_clothing_accessory_medal;
            mutable_appearance medalicon = mutable_appearance(Dm.initial(this, (obj_item_storage_lockbox_medal p) => p.icon), M?.medaltype) as mutable_appearance;
            if (i > 1 && i <= 5)
            {
                medalicon.pixel_w += (i - 1) * 3;
            }
            else if (i > 5)
            {
                medalicon.pixel_z -= 3;
                medalicon.pixel_w += (i - 6) * 3;
            }
            dot.Add(medalicon);
        }
        return dot;
    }

    // tg: code/game/objects/items/storage/lockbox.dm:139
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_accessory_medal_gold_captain>(this); // tg: code/game/objects/items/storage/lockbox.dm:140
        Dm.@new<obj_item_clothing_accessory_medal_silver_valor>(this); // tg: code/game/objects/items/storage/lockbox.dm:141
        Dm.@new<obj_item_clothing_accessory_medal_silver_valor>(this); // tg: code/game/objects/items/storage/lockbox.dm:142
        Dm.@new<obj_item_clothing_accessory_medal_silver_security>(this); // tg: code/game/objects/items/storage/lockbox.dm:143
        Dm.@new<obj_item_clothing_accessory_medal_bronze_heart>(this); // tg: code/game/objects/items/storage/lockbox.dm:144
        Dm.@new<obj_item_clothing_accessory_medal_plasma_nobel_science>(this); // tg: code/game/objects/items/storage/lockbox.dm:145
        Dm.@new<obj_item_clothing_accessory_medal_plasma_nobel_science>(this); // tg: code/game/objects/items/storage/lockbox.dm:146
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/items/storage/lockbox.dm:147
        {
            Dm.@new<obj_item_clothing_accessory_medal_conduct>(this); // tg: code/game/objects/items/storage/lockbox.dm:148
        }
    }
}

// tg: code/game/objects/items/storage/lockbox.dm:167
public partial class obj_item_storage_lockbox_medal_hop : obj_item_storage_lockbox_medal
{
    // tg: code/game/objects/items/storage/lockbox.dm:174
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 3; i++)
            Dm.@new<obj_item_clothing_accessory_medal_silver_bureaucracy>(this);
        Dm.@new<obj_item_clothing_accessory_medal_gold_ordom>(this);
    }
}

// tg: code/game/objects/items/storage/lockbox.dm:180
public partial class obj_item_storage_lockbox_medal_sec : obj_item_storage_lockbox_medal
{
    // tg: code/game/objects/items/storage/lockbox.dm:201
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 3; i++)
            Dm.@new<obj_item_clothing_accessory_medal_silver_security>(this);
    }
}

// tg: code/game/objects/items/storage/lockbox.dm:195  (gaps J5, D298)
public partial class obj_item_storage_lockbox_medal_med : obj_item_storage_lockbox_medal
{
    // tg: code/game/objects/items/storage/lockbox.dm:195
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_accessory_medal_med_medal>(this); // tg: code/game/objects/items/storage/lockbox.dm:196
        Dm.@new<obj_item_clothing_accessory_medal_med_medal2>(this); // tg: code/game/objects/items/storage/lockbox.dm:197
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/items/storage/lockbox.dm:198
            Dm.@new<obj_item_clothing_accessory_medal_silver_emergency_services_medical>(this);
    }
}

// tg: code/game/objects/items/storage/lockbox.dm:236  (gaps J5, D298)
public partial class obj_item_storage_lockbox_medal_sci : obj_item_storage_lockbox_medal
{
    // tg: code/game/objects/items/storage/lockbox.dm:236
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/items/storage/lockbox.dm:237
            Dm.@new<obj_item_clothing_accessory_medal_plasma_nobel_science>(this);
    }
}

// tg: code/game/objects/items/storage/lockbox.dm:248  (gaps J5, D298)
public partial class obj_item_storage_lockbox_medal_engineering : obj_item_storage_lockbox_medal
{
    // tg: code/game/objects/items/storage/lockbox.dm:248
    public override async DmTask PopulateContents()
    {
        for (float i = 1; i <= 3; i++) // tg: code/game/objects/items/storage/lockbox.dm:249
            Dm.@new<obj_item_clothing_accessory_medal_silver_emergency_services_engineering>(this);
        Dm.@new<obj_item_clothing_accessory_medal_silver_elder_atmosian>(this); // tg: code/game/objects/items/storage/lockbox.dm:251
    }
}

// tg: code/game/objects/items/storage/lockbox.dm:207
public partial class obj_item_storage_lockbox_medal_cargo : obj_item_storage_lockbox_medal
{
    // tg: code/game/objects/items/storage/lockbox.dm:213
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_accessory_medal_ribbon_cargo>(this);
    }
}

// tg: code/game/objects/items/storage/lockbox.dm:253  (gaps P4b, D298)
public partial class obj_item_storage_lockbox_order : obj_item_storage_lockbox
{
    // tg: code/game/objects/items/storage/lockbox.dm:267
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        buyer_account = Dm.arg(__extra, 0) as datum_bank_account;
        ADD_TRAIT(this, TRAIT_NO_MISSING_ITEM_ERROR, TRAIT_GENERIC);
        ADD_TRAIT(this, TRAIT_NO_MANIFEST_CONTENTS_ERROR, TRAIT_GENERIC);
        return dot;
    }

    // tg: code/game/objects/items/storage/lockbox.dm:273
    public override object can_unlock(mob_living user = null, obj_item_card_id id_card = null, float? silent = null)
    {
        if (id_card.registered_account == buyer_account)
            return TRUE;
        if (!Dm.truthy(silent))
            balloon_alert(user, I18n.T("incorrect bank account!"));
        return FALSE;
    }
}

// tg: code/game/objects/items/storage/lockbox.dm:110
public partial class obj_item_storage_lockbox_clusterbang : obj_item_storage_lockbox
{
    // tg: code/game/objects/items/storage/lockbox.dm:110
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_grenade_clusterbuster>(this);
    }
}

// tg: code/game/objects/items/storage/lockbox.dm:224
public partial class obj_item_storage_lockbox_medal_service : obj_item_storage_lockbox_medal
{
    // tg: code/game/objects/items/storage/lockbox.dm:224
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_clothing_accessory_medal_silver_excellence>(this);
    }
}

// tg: code/game/objects/items/storage/lockbox.dm:280  (gaps GU1, D333)
public partial class obj_item_storage_lockbox_dueling : obj_item_storage_lockbox
{
    // tg: code/game/objects/items/storage/lockbox.dm:296
    public override async DmTask PopulateContents()
    {
        await base.PopulateContents();
        obj_item_gun_energy_dueling gun_A = Dm.@new<obj_item_gun_energy_dueling>(this);
        obj_item_gun_energy_dueling gun_B = Dm.@new<obj_item_gun_energy_dueling>(this);
        Dm.@new<datum_duel>(gun_A, gun_B);
    }
}

// tg: code/game/objects/items/storage/lockbox.dm:315
public partial class obj_item_storage_lockbox_bitrunning_encrypted : obj_item_storage_lockbox_bitrunning
{
    // tg: code/game/objects/items/storage/lockbox.dm:321
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        return FALSE;
    }
}

// tg: code/game/objects/items/storage/lockbox.dm:324
public partial class obj_item_storage_lockbox_bitrunning_decrypted : obj_item_storage_lockbox_bitrunning
{
    // tg: code/game/objects/items/storage/lockbox.dm:332
    [DmParams("mapload", "completed_domain")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object completed_domain = Dm.arg(__extra, 0);
        if (completed_domain == null)
        {
            log_runtime("Decrypted curiosity was created with no source domain.");
            return INITIALIZE_HINT_QDEL;
        }

        if (completed_domain is not datum_lazy_template_virtual_domain) // Check if this is a proper virtual domain before doing anything with it
        {
            log_runtime(Dm.text("Decrypted curiosity was created with an invalid source domain. [] ([]).", (completed_domain as datum_lazy_template_virtual_domain)?.name, (completed_domain as datum)?.type));
            return INITIALIZE_HINT_QDEL;
        }

        source_domain = completed_domain as datum_lazy_template_virtual_domain;

        object dot = base.Initialize(mapload, __extra);

        icon_state = icon_closed;
        playsound(this, Dm.resource("sound/effects/magic/blink.ogg"), 50, TRUE);
        return dot;
    }

    // tg: code/game/objects/items/storage/lockbox.dm:352
    public override async DmTask PopulateContents()
    {
        Type choice = SSbitrunning.pick_secondary_loot(source_domain) as Type;
        await Dm.new_async(choice, this);
    }
}
