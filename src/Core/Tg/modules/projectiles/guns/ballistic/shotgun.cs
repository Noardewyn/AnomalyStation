// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/ballistic/shotgun.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice D: the pump shotgun, the riot shotgun, the combat shotguns and the double-barrel (the station set). Syndicate N2a (D316): the
// operatives' Bulldog with its secondary magazine. Gaps GU1 (D333): the hook-modified shotgun of the deathmatch loadouts (its right-click
// hook). The dual-feed (cycler) shotgun is the fugitive hunters' and the spies' (docs/ai/specs/not-translated.md section 215, D333); the
// monkey and musket shotguns and the pump-action grenade launcher keep generated var blocks only (section 49). The seclight points are
// a deferred leaf (the attachment component, M10+); the double-barrel's reskins are live. The var blocks are generated (D111) but
// the hook shotgun's (below).

// tg: code/modules/projectiles/guns/ballistic/shotgun.dm:1
public partial class obj_item_gun_ballistic_shotgun : obj_item_gun_ballistic
{
    // tg: code/modules/projectiles/guns/ballistic/shotgun.dm:33
    public override async DmTask<object> blow_up(mob user = null)
    {
        object dot = 0;
        if (chambered?.loaded_projectile != null)
        {
            await process_fire(user, user as mob_living, FALSE);
            dot = 1;
        }
        return dot;
    }
}

//component for seclight attachment
// tg: code/modules/projectiles/guns/ballistic/shotgun.dm:56
public partial class obj_item_gun_ballistic_shotgun_riot : obj_item_gun_ballistic_shotgun
{
    // tg: code/modules/projectiles/guns/ballistic/shotgun.dm:56
    public override async DmTask add_seclight_point()
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_seclite_attachable), Dm.named("light_overlay_icon", Dm.resource("icons/obj/weapons/guns/flashlights.dmi")), Dm.named("light_overlay", "flight"), Dm.named("overlay_x", 20), Dm.named("overlay_y", 11)));
    }
}

// tg: code/modules/projectiles/guns/ballistic/shotgun.dm:82
public partial class obj_item_gun_ballistic_shotgun_automatic : obj_item_gun_ballistic_shotgun
{
    // tg: code/modules/projectiles/guns/ballistic/shotgun.dm:82
    public override async DmTask<object> shoot_live_shot(mob_living user = null, float? pointblank = null, atom pbtarget = null, float? message = null)
    {
        await base.shoot_live_shot(user, pointblank, pbtarget, message);
        await rack();
        return null;
    }
}

// tg: code/modules/projectiles/guns/ballistic/shotgun.dm:96
public partial class obj_item_gun_ballistic_shotgun_automatic_combat : obj_item_gun_ballistic_shotgun_automatic
{
    // tg: code/modules/projectiles/guns/ballistic/shotgun.dm:96
    public override async DmTask add_seclight_point()
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_seclite_attachable), Dm.named("light_overlay_icon", Dm.resource("icons/obj/weapons/guns/flashlights.dmi")), Dm.named("light_overlay", "flight"), Dm.named("overlay_x", 20), Dm.named("overlay_y", 11)));
    }
}

// tg: code/modules/projectiles/guns/ballistic/shotgun.dm:112
public partial class obj_item_gun_ballistic_shotgun_automatic_combat_compact : obj_item_gun_ballistic_shotgun_automatic_combat
{
    // tg: code/modules/projectiles/guns/ballistic/shotgun.dm:112
    public override async DmTask add_seclight_point()
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_seclite_attachable), Dm.named("light_overlay_icon", Dm.resource("icons/obj/weapons/guns/flashlights.dmi")), Dm.named("light_overlay", "flight"), Dm.named("overlay_x", 16), Dm.named("overlay_y", 11)));
    }
}

// tg: code/modules/projectiles/guns/ballistic/shotgun.dm:339
public partial class obj_item_gun_ballistic_shotgun_doublebarrel : obj_item_gun_ballistic_shotgun
{
    // tg: code/modules/projectiles/guns/ballistic/shotgun.dm:362
    public override async DmTask setup_reskins()
    {
        await AddComponent(typeof(datum_component_reskinable_item), typeof(datum_atom_skin_bar_shotgun));
    }

    // tg: code/modules/projectiles/guns/ballistic/shotgun.dm:365
    public override object do_sawoff()
    {
        object dot = base.do_sawoff();
        weapon_weight = WEAPON_MEDIUM;
        return dot;
    }
}

// tg: code/modules/projectiles/guns/ballistic/shotgun.dm:186
public partial class obj_item_gun_ballistic_shotgun_bulldog : obj_item_gun_ballistic_shotgun
{
    // tg: code/modules/projectiles/guns/ballistic/shotgun.dm:222
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        secondary_magazine_type = Dm.truthy(secondary_magazine_type) ? secondary_magazine_type : spawn_magazine_type;
        secondary_magazine = Dm.@new(secondary_magazine_type as Type, this) as obj_item_ammo_box_magazine;
        update_appearance();
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/shotgun.dm:228
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref secondary_magazine);
        return base.Destroy(force);
    }

    // tg: code/modules/projectiles/guns/ballistic/shotgun.dm:232
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (secondary_magazine != null)
        {
            float secondary_ammo_count = secondary_magazine.ammo_count();
            Dm.append(ref dot, I18n.T("There is a secondary magazine."));
            Dm.append(ref dot, I18n.T("It has [] round[] remaining.", secondary_ammo_count, secondary_ammo_count == 1 ? "" : "s"));
            Dm.append(ref dot, I18n.T("Shoot with right-click to swap to the secondary magazine after firing."));
            Dm.append(ref dot, I18n.T("If the magazine is empty, [] will automatically swap to the secondary magazine.", this));
        }
        Dm.append(ref dot, I18n.T("You can load a secondary magazine by right-clicking [] with the magazine you want to load.", this));
        Dm.append(ref dot, I18n.T("You can remove a secondary magazine by alt-right-clicking [].", this));
        Dm.append(ref dot, I18n.T("Right-click to swap the magazine to the secondary position, and vice versa."));
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/shotgun.dm:244
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (secondary_magazine != null)
        {
            Dm.append(ref dot, Dm.text("[]_secondary_mag_[]", icon_state, Dm.initial(secondary_magazine, (obj_item_ammo_box_magazine m) => m.icon_state)));
            if (secondary_magazine.ammo_count() == 0)
                Dm.append(ref dot, Dm.text("[]_secondary_mag_empty", icon_state));
        }
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/shotgun.dm:251
    public override async DmTask handle_chamber(float? empty_chamber = null, float? from_firing = null, float? chamber_next_round = null)
    {
        empty_chamber ??= TRUE;
        from_firing ??= TRUE;
        chamber_next_round ??= TRUE;
        if (secondary_magazine == null)
        {
            await base.handle_chamber(empty_chamber, from_firing, chamber_next_round);
            return;
        }
        float secondary_shells_left = Dm.length(secondary_magazine.stored_ammo);
        if (magazine != null)
        {
            float shells_left = Dm.length(magazine.stored_ammo);
            if (shells_left <= 0 && secondary_shells_left >= 1)
                toggle_magazine();
        }
        else
            toggle_magazine();
        await base.handle_chamber(empty_chamber, from_firing, chamber_next_round);
    }

    // tg: code/modules/projectiles/guns/ballistic/shotgun.dm:263
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        toggle_magazine();
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/projectiles/guns/ballistic/shotgun.dm:267
    public override async DmTask<object> ranged_interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (secondary_magazine != null)
            toggle_magazine();
        return await base.ranged_interact_with_atom_secondary(interacting_with, user, modifiers);
    }

    // tg: code/modules/projectiles/guns/ballistic/shotgun.dm:272
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (!istype(tool, secondary_magazine_type as Type))
            return await base.item_interaction_secondary(user, tool, modifiers);
        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;
        obj_item_ammo_box_magazine old_mag = secondary_magazine;
        secondary_magazine = tool as obj_item_ammo_box_magazine;
        if (old_mag != null)
            await user.put_in_hands(old_mag);
        balloon_alert(user, I18n.T("secondary [] loaded", magazine_wording));
        playsound(this, load_empty_sound, load_sound_volume, load_sound_vary);
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/projectiles/guns/ballistic/shotgun.dm:286
    public override async DmTask<object> click_alt_secondary(mob user = null)
    {
        if (secondary_magazine != null)
        {
            obj_item_ammo_box_magazine old_mag = secondary_magazine;
            secondary_magazine = null;
            await user.put_in_hands(old_mag);
            update_appearance();
            playsound(this, load_empty_sound, load_sound_volume, load_sound_vary);
        }
        return null;
    }

    // tg: code/modules/projectiles/guns/ballistic/shotgun.dm:294
    public virtual void toggle_magazine()
    {
        obj_item_ammo_box_magazine primary_magazine = magazine;
        obj_item_ammo_box_magazine alternative_magazine = secondary_magazine;
        magazine = alternative_magazine;
        secondary_magazine = primary_magazine;
        playsound(this, load_empty_sound, load_sound_volume, load_sound_vary);
        update_appearance();
    }
}

// tg: code/modules/projectiles/guns/ballistic/shotgun.dm:381  (gaps GU1)
public partial class obj_item_gun_ballistic_shotgun_hook : obj_item_gun_ballistic_shotgun
{
    //our hook gun!
    // tg: code/modules/projectiles/guns/ballistic/shotgun.dm:400  var/obj/item/gun/magic/hook/bounty/hook
    // Hand-written: V3 types it Type (tg assigns typepaths to other vars named hook), but it holds the hook gun object.
    public obj_item_gun_magic_hook_bounty hook;

    // The hand-written field above puts the class in Hand mode (types-converter.md 3.2), so the generator no longer writes its
    // constructor and var initializer: tg's var defaults, as main's generated part gave them.
    // tg: code/modules/projectiles/guns/ballistic/shotgun.dm:382
    public obj_item_gun_ballistic_shotgun_hook()
    {
        name = "hook modified sawn-off shotgun";
        desc = "Range isn't an issue when you can bring your victim to you.";
        icon_state = "hookshotgun";
        inhand_icon_state = "hookshotgun";
        lefthand_file = Dm.resource("icons/mob/inhands/weapons/guns_lefthand.dmi");
        righthand_file = Dm.resource("icons/mob/inhands/weapons/guns_righthand.dmi");
        inhand_x_dimension = 32;
        inhand_y_dimension = 32;
        accepted_magazine_type = typeof(obj_item_ammo_box_magazine_internal_shot_bounty);
        weapon_weight = 2;
        semi_auto = 1;
        obj_flags = 4096;
        force = 18;
        sharpness = 2;
        hitsound = Dm.resource("sound/items/weapons/bladeslice.ogg");
    }

    // tg: code/modules/projectiles/guns/ballistic/shotgun.dm:396
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        attack_verb_continuous = Dm.list("slashes", "hooks", "stabs"); // tg: code/modules/projectiles/guns/ballistic/shotgun.dm:396
        attack_verb_simple = Dm.list("slash", "hook", "stab"); // tg: code/modules/projectiles/guns/ballistic/shotgun.dm:397
    }

    // tg: code/modules/projectiles/guns/ballistic/shotgun.dm:402
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        hook = Dm.@new<obj_item_gun_magic_hook_bounty>(this);
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/shotgun.dm:406
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref hook);
        return base.Destroy(force);
    }

    // tg: code/modules/projectiles/guns/ballistic/shotgun.dm:410
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Right-click to shoot the hook.")));
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/shotgun.dm:414
    public override async DmTask<object> try_fire_gun(atom target = null, mob_living user = null, object @params = null)
    {
        if (Dm.truthy(LAZYACCESS(params2list(@params), RIGHT_CLICK)))
            return await hook.try_fire_gun(target, user, @params);
        return await base.try_fire_gun(target, user, @params);
    }
}
