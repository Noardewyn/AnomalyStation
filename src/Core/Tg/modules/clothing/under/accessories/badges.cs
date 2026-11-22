// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/under/accessories/badges.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// J1 (D298): the lawyer's badge (the bubble, the point at it, the objection at a table slam; the stealing objective is a live-partial
// row), the pocket protector (its storage; the full one with its pens); the subversive pin with the black market's hostages (gaps P4d).
// Gaps CL1 (D333): the rest of the file (the clown's and the mime's pins with their fan HUD and moods, the cosmetology protector's
// lipsticks, the dogtags read up close, the press badge filled in and shown); the pride pin's reskin menu waits for the reskinnable
// items (not-translated.md 209). The var blocks are generated.

// tg: code/modules/clothing/under/accessories/badges.dm:3
public partial class obj_item_clothing_accessory_lawyers_badge : obj_item_clothing_accessory
{
    // tg: code/modules/clothing/under/accessories/badges.dm:7
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_bubble_icon_override), "lawyer", BUBBLE_ICON_PRIORITY_ACCESSORY));
        return dot;
    }

    // tg: code/modules/clothing/under/accessories/badges.dm:11
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        object dot = await base.interact(user, __extra);
        if (prob(1))
            await user.say("The testimony contradicts the evidence!", forced: Dm.text("[]", this));
        await user.point_at(this);
        return dot;
    }

    // tg: code/modules/clothing/under/accessories/badges.dm:17
    public override async DmTask accessory_equipped(obj_item_clothing_under clothes = null, mob_living user = null)
    {
        RegisterSignal(user, COMSIG_LIVING_SLAM_TABLE, nameof(table_slam));
    }

    // tg: code/modules/clothing/under/accessories/badges.dm:20
    public override async DmTask accessory_dropped(obj_item_clothing_under clothes = null, mob_living user = null)
    {
        UnregisterSignal(user, COMSIG_LIVING_SLAM_TABLE);
    }

    // tg: code/modules/clothing/under/accessories/badges.dm:23
    [SignalHandler]
    public virtual void table_slam(mob_living source = null, obj_structure_table the_table = null)
    {
        Waiting.Detach(source.say("Objection!!", spans: Dm.list(SPAN_YELL), forced: Dm.text("[]", this)));
    }
}

// tg: code/modules/clothing/under/accessories/badges.dm:29
public partial class obj_item_clothing_accessory_clown_enjoyer_pin : obj_item_clothing_accessory
{
    // tg: code/modules/clothing/under/accessories/badges.dm:34
    public override object can_attach_accessory(obj_item_clothing_under attach_to = null, mob_living user = null)
    {
        object dot = base.can_attach_accessory(attach_to, user);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.locate_type(typeof(obj_item_clothing_accessory_mime_fan_pin), attach_to.attached_accessories) != null)
        {
            if (user != null)
                attach_to.balloon_alert(user, I18n.T("can't pick both sides!"));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/clothing/under/accessories/badges.dm:44
    public override async DmTask accessory_equipped(obj_item_clothing_under clothes = null, mob_living user = null)
    {
        if (HAS_TRAIT(user, TRAIT_CLOWN_ENJOYER))
            await user.add_mood_event("clown_enjoyer_pin", typeof(datum_mood_event_clown_enjoyer_pin));
        if (ishuman(user))
        {
            mob_living_carbon_human human_equipper = (mob_living_carbon_human)user;
            human_equipper.fan_hud_set_fandom();
        }
    }

    // tg: code/modules/clothing/under/accessories/badges.dm:51
    public override async DmTask accessory_dropped(obj_item_clothing_under clothes = null, mob_living user = null)
    {
        user.clear_mood_event("clown_enjoyer_pin");
        if (ishuman(user))
        {
            mob_living_carbon_human human_equipper = (mob_living_carbon_human)user;
            human_equipper.fan_hud_set_fandom();
        }
    }
}

// tg: code/modules/clothing/under/accessories/badges.dm:57
public partial class obj_item_clothing_accessory_mime_fan_pin : obj_item_clothing_accessory
{
    // tg: code/modules/clothing/under/accessories/badges.dm:62
    public override object can_attach_accessory(obj_item_clothing_under attach_to = null, mob_living user = null)
    {
        object dot = base.can_attach_accessory(attach_to, user);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.locate_type(typeof(obj_item_clothing_accessory_clown_enjoyer_pin), attach_to.attached_accessories) != null)
        {
            if (user != null)
                attach_to.balloon_alert(user, I18n.T("can't pick both sides!"));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/clothing/under/accessories/badges.dm:72
    public override async DmTask accessory_equipped(obj_item_clothing_under clothes = null, mob_living user = null)
    {
        if (HAS_TRAIT(user, TRAIT_MIME_FAN))
            await user.add_mood_event("mime_fan_pin", typeof(datum_mood_event_mime_fan_pin));
        if (ishuman(user))
        {
            mob_living_carbon_human human_equipper = (mob_living_carbon_human)user;
            human_equipper.fan_hud_set_fandom();
        }
    }

    // tg: code/modules/clothing/under/accessories/badges.dm:79
    public override async DmTask accessory_dropped(obj_item_clothing_under clothes = null, mob_living user = null)
    {
        user.clear_mood_event("mime_fan_pin");
        if (ishuman(user))
        {
            mob_living_carbon_human human_equipper = (mob_living_carbon_human)user;
            human_equipper.fan_hud_set_fandom();
        }
    }
}

// tg: code/modules/clothing/under/accessories/badges.dm:85
public partial class obj_item_clothing_accessory_pocketprotector : obj_item_clothing_accessory
{
    // tg: code/modules/clothing/under/accessories/badges.dm:94
    public override object can_attach_accessory(obj_item_clothing_under attach_to = null, mob_living user = null)
    {
        object dot = null;
        dot = base.can_attach_accessory(attach_to, user);
        if (!Dm.truthy(dot))
            return dot;

        if (attach_to.atom_storage != null)
        {
            if (Dm.truthy(user))
                attach_to.balloon_alert(user, I18n.T("not compatible!"));
            return FALSE;
        }
        return dot;
    }

    // tg: code/modules/clothing/under/accessories/badges.dm:90
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_pockets_pocketprotector)));
        return dot;
    }
}

// tg: code/modules/clothing/under/accessories/badges.dm:105
public partial class obj_item_clothing_accessory_pocketprotector_full : obj_item_clothing_accessory_pocketprotector
{
    // tg: code/modules/clothing/under/accessories/badges.dm:107
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.@new<obj_item_pen_red>(this);
        Dm.@new<obj_item_pen>(this);
        Dm.@new<obj_item_pen_blue>(this);
        return dot;
    }
}

// tg: code/modules/clothing/under/accessories/badges.dm:113
public partial class obj_item_clothing_accessory_pocketprotector_cosmetology : obj_item_clothing_accessory_pocketprotector
{
    // tg: code/modules/clothing/under/accessories/badges.dm:115
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        for (int i = 1; i <= 3; i++)
            Dm.@new<obj_item_lipstick_random>(this);
        return dot;
    }
}

// tg: code/modules/clothing/under/accessories/badges.dm:120
public partial class obj_item_clothing_accessory_dogtag : obj_item_clothing_accessory
{
    // tg: code/modules/clothing/under/accessories/badges.dm:129
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, display);
        return dot;
    }

    // tg: code/modules/clothing/under/accessories/badges.dm:134
    public override async DmTask attach(obj_item_clothing_under attached_to = null)
    {
        await base.attach(attached_to);
        RegisterSignal(attached_to, COMSIG_ATOM_EXAMINE, nameof(on_examine));
    }

    // tg: code/modules/clothing/under/accessories/badges.dm:138
    public override object detach(obj_item_clothing_under detach_from = null, float? update = null)
    {
        object dot = base.detach(detach_from, update);
        UnregisterSignal(detach_from, COMSIG_ATOM_EXAMINE);
        return dot;
    }

    // tg: code/modules/clothing/under/accessories/badges.dm:143
    public override async DmTask accessory_equipped(obj_item_clothing_under clothes = null, mob_living user = null)
    {
        RegisterSignal(user, COMSIG_ATOM_EXAMINE_MORE, nameof(on_examine));
        await DmTask.CompletedTask;
    }

    // tg: code/modules/clothing/under/accessories/badges.dm:146
    public override async DmTask accessory_dropped(obj_item_clothing_under clothes = null, mob_living user = null)
    {
        UnregisterSignal(user, COMSIG_ATOM_EXAMINE_MORE);
        await DmTask.CompletedTask;
    }

    /// Adds the examine message to the clothes and mob.
    // tg: code/modules/clothing/under/accessories/badges.dm:150
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        // Only show the examine message if we're close (2 tiles)
        if (!IN_GIVEN_RANGE(get_turf(user), get_turf(this), 2))
            return;

        if (ismob(source))
        {
            // Examining a mob wearing the clothes, wearing the dogtag will also show the message
            Dm.append(examine_list, I18n.T("A dogtag is hanging around [] neck: []", ((mob)source).p_their(), display));
        }
        else
            Dm.append(examine_list, I18n.T("A dogtag is attached to []: []", source, display));
    }
}

// tg: code/modules/clothing/under/accessories/badges.dm:163
public partial class obj_item_clothing_accessory_dogtag_allergy : obj_item_clothing_accessory_dogtag
{
    // tg: code/modules/clothing/under/accessories/badges.dm:167
    [DmParams("mapload", "allergy_string")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object allergy_string = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(allergy_string))
            display = span_notice(I18n.T("The dogtag has a listing of allergies: []", allergy_string));
        else
            display = span_notice(I18n.T("The dogtag is all scratched up."));
        return dot;
    }
}

// tg: code/modules/clothing/under/accessories/badges.dm:218
public partial class obj_item_clothing_accessory_pride : obj_item_clothing_accessory
{
    // tg: code/modules/clothing/under/accessories/badges.dm:224
    public override async DmTask setup_reskins()
    {
        // AddComponent(/datum/component/reskinable_item, /datum/atom_skin/pride_pin, infinite = TRUE)
        Dm.Deferred("code/modules/clothing/under/accessories/badges.dm:225", "setup_reskins: AddComponent(/datum/component/reskinable_item, /datum/atom_skin/pride_pin): the reskin menu (M10+)");
        await DmTask.CompletedTask;
    }
}

/// Self-identify as a dangerous subversive
// tg: code/modules/clothing/under/accessories/badges.dm:239  (gaps P4d: the black market's hostages wear it home)
public partial class obj_item_clothing_accessory_anti_sec_pin : obj_item_clothing_accessory
{
    // tg: code/modules/clothing/under/accessories/badges.dm:245
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_pinnable_accessory), Dm.named("silent", TRUE), Dm.named("pinning_time", 5 * SECONDS)));
        return dot;
    }

    // tg: code/modules/clothing/under/accessories/badges.dm:249
    public override async DmTask<object> try_attach(obj_item_clothing_under attach_to = null, mob_living attacher = null)
    {
        object dot = await base.try_attach(attach_to, attacher);
        if (!Dm.truthy(dot) || attacher == null)
            return dot;

        atom target = ishuman(attach_to.loc) ? attach_to.loc : attach_to;
        log_combat(attacher, target, "pinned an 'arrest me immediately' pin onto", this);
        return TRUE;
    }

    // tg: code/modules/clothing/under/accessories/badges.dm:258
    public override async DmTask accessory_equipped(obj_item_clothing_under clothes = null, mob_living user = null)
    {
        await base.accessory_equipped(clothes, user);
        if (user is mob_living_carbon_human human_wearer)
            human_wearer.sec_hud_set_security_status();
    }

    // tg: code/modules/clothing/under/accessories/badges.dm:264
    public override async DmTask accessory_dropped(obj_item_clothing_under clothes = null, mob_living user = null)
    {
        await base.accessory_dropped(clothes, user);
        if (user is mob_living_carbon_human human_wearer)
            human_wearer.sec_hud_set_security_status();
    }
}

// tg: code/modules/clothing/under/accessories/badges.dm:270
public partial class obj_item_clothing_accessory_press_badge : obj_item_clothing_accessory
{
    // tg: code/modules/clothing/under/accessories/badges.dm:281
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(journalist_name) || !Dm.truthy(press_name))
        {
            Dm.append(ref dot, span_notice(I18n.T("Use it in hand to input information")));
            return dot;
        }

        Dm.append(ref dot, span_notice(I18n.T("It belongs to <b>[]</b>, <b>[]</b>", journalist_name, press_name)));
        return dot;
    }

    // tg: code/modules/clothing/under/accessories/badges.dm:289
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (!Dm.truthy(journalist_name))
            journalist_name = await tgui_input_text(user, I18n.T("What is your name?"), I18n.T("Journalist Name"), Dm.text("[]", user.name), max_length: MAX_NAME_LEN);
        if (!Dm.truthy(press_name))
            press_name = await tgui_input_text(user, I18n.T("For what organization you work?"), I18n.T("Press Name"), "Nanotrasen", max_length: MAX_CHARTER_LEN);
        return dot;
    }

    // tg: code/modules/clothing/under/accessories/badges.dm:296
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (!isliving(interacting_with))
            return dot;

        mob_living interacting_living = (mob_living)interacting_with;
        if (Dm.truthy(user.combat_mode))
        {
            playsound(interacting_living, Dm.resource("sound/items/weapons/throw.ogg"), 30);
            interacting_living.examinate(this);
            to_chat(interacting_living, span_userdanger(I18n.T("[] shoves the [] up your face!", user, this)));
            user.visible_message(span_warning(I18n.T("[] have shoved a [] into [] face.", user, this, interacting_living)));
        }
        else
        {
            playsound(interacting_living, Dm.resource("sound/items/weapons/throwsoft.ogg"), 20);
            interacting_living.examinate(this);
            to_chat(interacting_living, span_boldwarning(I18n.T("[] shows the [] to you.", user, this)));
            user.visible_message(span_notice(I18n.T("[] shows a [] to [].", user, this, interacting_living)));
        }
        return dot;
    }
}
