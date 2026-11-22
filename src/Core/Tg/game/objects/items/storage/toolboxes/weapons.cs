// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/storage/toolboxes/weapons.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the gun cases translated (the syndicate campaign G2, D316) and the syndicate toolbox (G3b): the base case and the traitor's Makarov and
// Donksoft cases with their evidence disposal bomb; G4c: the monkey gun case; G4d: the turret toolbox (the sentry gun). The case's screentip
// is a live-partial row; N2a: the nuclear operatives' cases with their own contents; gaps SR1 (D333): the ammunition canisters.

// tg: code/game/objects/items/storage/toolboxes/weapons.dm:55
public partial class obj_item_storage_toolbox_guncase : obj_item_storage_toolbox
{
    // tg: code/game/objects/items/storage/toolboxes/weapons.dm:71
    public override async DmTask PopulateContents()
    {
        if (weapon_to_spawn != null)
            Dm.@new(weapon_to_spawn, this);
        if (extra_to_spawn != null)
        {
            for (int iterate = 1; iterate <= 3; iterate++)
                Dm.@new(extra_to_spawn, this);
        }
    }
}

// tg: code/game/objects/items/storage/toolboxes/weapons.dm:78
public partial class obj_item_storage_toolbox_guncase_traitor : obj_item_storage_toolbox_guncase
{
    // tg: code/game/objects/items/storage/toolboxes/weapons.dm:90
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        return dot;
    }

    // tg: code/game/objects/items/storage/toolboxes/weapons.dm:94
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        dot.Add(span_notice(I18n.T("Activate the Evidence Disposal Explosive using Alt-Right-Click.")));
        return dot;
    }

    // tg: code/game/objects/items/storage/toolboxes/weapons.dm:104
    public override async DmTask PopulateContents()
    {
        Dm.@new(weapon_to_spawn, this);
        for (int i = 1; i <= 2; i++)
            Dm.@new(extra_to_spawn, this);
        Dm.@new(ammo_box_to_spawn, this);
    }

    // tg: code/game/objects/items/storage/toolboxes/weapons.dm:110
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(currently_exploding))
            icon_state = Dm.text("[]_exploding", base_icon_state);
        else
            icon_state = Dm.text("[]", base_icon_state);
        return dot;
    }

    // tg: code/game/objects/items/storage/toolboxes/weapons.dm:117
    public override async DmTask<object> click_alt_secondary(mob user = null)
    {
        object dot = await base.click_alt_secondary(user);
        if (Dm.truthy(currently_exploding))
        {
            user.balloon_alert(user, I18n.T("already exploding!"));
            return dot;
        }

        object i_dont_even_think_once_about_blowing_stuff_up = await tgui_alert(user, I18n.T("Would you like to activate the evidence disposal bomb now?"), I18n.T("BYE BYE"), Dm.list("Yes", "No"));

        if (!Dm.eq(i_dont_even_think_once_about_blowing_stuff_up, "Yes") || Dm.truthy(currently_exploding) || QDELETED(user) || QDELETED(this) || !Dm.truthy(await user.can_perform_action(this, Dm.bitor(Dm.bitor(NEED_DEXTERITY, NEED_HANDS), ALLOW_RESTING))))
            return dot;

        explosion_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(think_fast_chucklenuts)), 5 * SECONDS, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/game/objects/items/storage/toolboxes/weapons.dm", line: 129);
        to_chat(user, span_warning(I18n.T("You prime []'s evidence disposal bomb!", this)));
        log_bomber(user, "has activated a", this, "for detonation");
        playsound(this, Dm.resource("sound/items/weapons/armbomb.ogg"), 50, TRUE);
        currently_exploding = TRUE;
        update_appearance();
        return dot;
    }

    /// proc to handle our detonation
    // tg: code/game/objects/items/storage/toolboxes/weapons.dm:136
    public virtual async DmTask think_fast_chucklenuts()
    {
        await explosion(this, devastation_range: 0, heavy_impact_range: 0, light_impact_range: 2, explosion_cause: this);
        qdel(this);
    }
}

// tg: code/game/objects/items/storage/toolboxes/weapons.dm:1
public partial class obj_item_storage_toolbox_syndicate : obj_item_storage_toolbox
{
    // tg: code/game/objects/items/storage/toolboxes/weapons.dm:10
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_screwdriver_nuke>(this);
        Dm.@new<obj_item_wrench>(this);
        Dm.@new<obj_item_weldingtool_largetank>(this);
        Dm.@new<obj_item_crowbar_red>(this);
        Dm.@new<obj_item_wirecutters>(this, "red");
        Dm.@new<obj_item_multitool>(this);
        Dm.@new<obj_item_clothing_gloves_combat>(this);
    }
}

// tg: code/game/objects/items/storage/toolboxes/weapons.dm:254
public partial class obj_item_storage_toolbox_guncase_monkeycase : obj_item_storage_toolbox_guncase
{
    // tg: code/game/objects/items/storage/toolboxes/weapons.dm:259
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(monkey_check(user)))
            return null;
        return await base.attack_self(user, modifiers, __extra);
    }

    // tg: code/game/objects/items/storage/toolboxes/weapons.dm:264
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        await attack_self(user, modifiers);
        return null;
    }

    // tg: code/game/objects/items/storage/toolboxes/weapons.dm:268
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(monkey_check(user)))
            return null;
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/game/objects/items/storage/toolboxes/weapons.dm:273
    public virtual object monkey_check(mob user)
    {
        if (Dm.eq(atom_storage.locked, STORAGE_NOT_LOCKED))
            return TRUE;

        if (HAS_TRAIT(user, TRAIT_SIMIAN))
        {
            atom_storage.locked = STORAGE_NOT_LOCKED;
            to_chat(user, span_notice(I18n.T("You place your paw on the paw scanner, and hear a soft click as [] unlocks!", this)));
            playsound(this, Dm.resource("sound/items/click.ogg"), 25, TRUE);
            return TRUE;
        }
        to_chat(user, span_warning(I18n.T("You put your hand on the hand scanner, and it rejects it with an angry chimpanzee screech!")));
        playsound(this, SFX_SCREECH, 75, TRUE);
        return FALSE;
    }

    // tg: code/game/objects/items/storage/toolboxes/weapons.dm:286
    public override async DmTask PopulateContents()
    {
        switch (rand(1, 3))
        {
            case 1:
                // Uzi with a boxcutter.
                Dm.@new<obj_item_gun_ballistic_automatic_mini_uzi_chimpgun>(this);
                Dm.@new<obj_item_ammo_box_magazine_uzim9mm>(this);
                Dm.@new<obj_item_ammo_box_magazine_uzim9mm>(this);
                Dm.@new<obj_item_boxcutter_extended>(this);
                break;
            case 2:
                // Thompson with a boxcutter.
                Dm.@new<obj_item_gun_ballistic_automatic_tommygun_chimpgun>(this);
                Dm.@new<obj_item_ammo_box_magazine_tommygunm45>(this);
                Dm.@new<obj_item_ammo_box_magazine_tommygunm45>(this);
                Dm.@new<obj_item_boxcutter_extended>(this);
                break;
            case 3:
                // M1911 with a switchblade and an extra banana bomb.
                Dm.@new<obj_item_gun_ballistic_automatic_pistol_m1911_chimpgun>(this);
                Dm.@new<obj_item_ammo_box_magazine_m45>(this);
                Dm.@new<obj_item_ammo_box_magazine_m45>(this);
                Dm.@new<obj_item_switchblade_extended>(this);
                Dm.@new<obj_item_food_grown_banana_bunch_monkeybomb>(this);
                break;
        }

        // Banana bomb! Basically a tiny flashbang for monkeys.
        Dm.@new<obj_item_food_grown_banana_bunch_monkeybomb>(this);
        // Somewhere to store it all.
        Dm.@new<obj_item_storage_backpack_messenger>(this);
    }
}

// tg: code/game/objects/items/storage/toolboxes/weapons.dm:313
public partial class obj_item_storage_toolbox_emergency_turret : obj_item_storage_toolbox_emergency
{
    // tg: code/game/objects/items/storage/toolboxes/weapons.dm:316
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_screwdriver>(this);
        Dm.@new<obj_item_wrench_combat>(this);
        Dm.@new<obj_item_weldingtool>(this);
        Dm.@new<obj_item_crowbar>(this);
        Dm.@new<obj_item_analyzer>(this);
        Dm.@new<obj_item_wirecutters>(this);
    }

    // tg: code/game/objects/items/storage/toolboxes/weapons.dm:324
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_wrench_combat)))
            return NONE;
        if (!Dm.truthy(user.combat_mode))
            return NONE;
        if (!Dm.truthy(tool.toolspeed))
            return ITEM_INTERACT_BLOCKING;
        balloon_alert(user, I18n.T("constructing..."));
        if (!Dm.truthy(await tool.use_tool(this, user, 2 * SECONDS, volume: 20)))
            return ITEM_INTERACT_BLOCKING;

        balloon_alert(user, I18n.T("constructed!"));
        user.visible_message(
            span_danger(I18n.T("[] bashes [] with []!", user, this, tool)),
            span_danger(I18n.T("You bash [] with []!", this, tool)),
            null,
            COMBAT_MESSAGE_RANGE);

        playsound(this, Dm.resource("sound/items/tools/drill_use.ogg"), 80, TRUE, -1);
        obj_machinery_porta_turret_syndicate_toolbox turret = Dm.@new<obj_machinery_porta_turret_syndicate_toolbox>(get_turf(loc));
        set_toolbox_faction(turret, user);
        turret.toolbox = this;
        await forceMove(turret);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/storage/toolboxes/weapons.dm:350
    public virtual void set_toolbox_faction(obj_machinery_porta_turret turret = null, mob user = null)
    {
        turret.set_allies(Dm.list(Dm.text("[]", REF(user))));
        turret.set_faction(null);
    }
}

// tg: code/game/objects/items/storage/toolboxes/weapons.dm:313
public partial class obj_item_storage_toolbox_emergency_turret_nukie : obj_item_storage_toolbox_emergency_turret
{
    // tg: code/game/objects/items/storage/toolboxes/weapons.dm:354
    public override void set_toolbox_faction(obj_machinery_porta_turret turret = null, mob user = null)
    {
        turret.set_faction(Dm.list(ROLE_SYNDICATE));
    }
}

// tg: code/game/objects/items/storage/toolboxes/weapons.dm:176
public partial class obj_item_storage_toolbox_guncase_m90gl : obj_item_storage_toolbox_guncase
{
    // tg: code/game/objects/items/storage/toolboxes/weapons.dm:181
    public override async DmTask PopulateContents()
    {
        Dm.@new(weapon_to_spawn, this);
        for (int i = 1; i <= 2; i++)
            Dm.@new(extra_to_spawn, this);
        Dm.@new<obj_item_ammo_box_a40mm_rubber>(this);
    }
}

// tg: code/game/objects/items/storage/toolboxes/weapons.dm:187
public partial class obj_item_storage_toolbox_guncase_rocketlauncher : obj_item_storage_toolbox_guncase
{
    // tg: code/game/objects/items/storage/toolboxes/weapons.dm:192
    public override async DmTask PopulateContents()
    {
        Dm.@new(weapon_to_spawn, this);
        Dm.@new(extra_to_spawn, this);
    }
}

// tg: code/game/objects/items/storage/toolboxes/weapons.dm:201
public partial class obj_item_storage_toolbox_guncase_sword_and_board : obj_item_storage_toolbox_guncase
{
    // tg: code/game/objects/items/storage/toolboxes/weapons.dm:206
    public override async DmTask PopulateContents()
    {
        Dm.@new(weapon_to_spawn, this);
        Dm.@new(extra_to_spawn, this);
        Dm.@new<obj_item_clothing_head_costume_knight>(this);
    }
}

// tg: code/game/objects/items/storage/toolboxes/weapons.dm:211
public partial class obj_item_storage_toolbox_guncase_cqc : obj_item_storage_toolbox_guncase
{
    // tg: code/game/objects/items/storage/toolboxes/weapons.dm:216
    public override async DmTask PopulateContents()
    {
        Dm.@new(weapon_to_spawn, this);
        Dm.@new(extra_to_spawn, this);
        Dm.@new<obj_item_clothing_head_costume_snakeeater>(this);
        Dm.@new<obj_item_storage_fancy_cigarettes_cigpack_syndicate>(this);
    }
}

// tg: code/game/objects/items/storage/toolboxes/weapons.dm:222
public partial class obj_item_storage_toolbox_guncase_doublesword : obj_item_storage_toolbox_guncase
{
    // tg: code/game/objects/items/storage/toolboxes/weapons.dm:228
    public override async DmTask PopulateContents()
    {
        Dm.@new(weapon_to_spawn, this);
        Dm.@new(extra_to_spawn, this);
        Dm.@new<obj_item_mod_module_noslip>(this);
        Dm.@new<obj_item_reagent_containers_hypospray_medipen_methamphetamine>(this);
        Dm.@new<obj_item_clothing_under_rank_prisoner_nosensor>(this);
    }
}

// tg: code/game/objects/items/storage/toolboxes/weapons.dm:235
public partial class obj_item_storage_toolbox_guncase_china_lake : obj_item_storage_toolbox_guncase
{
    // tg: code/game/objects/items/storage/toolboxes/weapons.dm:240
    public override async DmTask PopulateContents()
    {
        Dm.@new(weapon_to_spawn, this);
        Dm.@new(extra_to_spawn, this);
        Dm.@new<obj_item_clothing_under_rank_prisoner_nosensor>(this);
        Dm.@new<obj_item_storage_fancy_cigarettes_cigpack_syndicate>(this);
    }
}

// tg: code/game/objects/items/storage/toolboxes/weapons.dm:22
public partial class obj_item_storage_toolbox_ammobox : obj_item_storage_toolbox
{
    // tg: code/game/objects/items/storage/toolboxes/weapons.dm:33
    public override async DmTask PopulateContents()
    {
        if (ammo_to_spawn != null)
        {
            for (float i = 1; i <= 6; i++)
                Dm.@new(ammo_to_spawn, this);
        }
    }
}
