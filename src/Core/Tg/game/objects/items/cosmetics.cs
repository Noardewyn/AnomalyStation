// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/cosmetics.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps S3, D298): the paper's lipstick wiping, a proc of /obj/item/paper; the lipsticks (syndicate G3b: the syndie lipstick
// with its kin), the electric razor (silicons SI2: the service cyborg's; the barber's other tools come with them), the surgical razor's
// tray picture (gaps OD1, D333). The var blocks are generated (D111).

//you can wipe off lipstick with paper!
// tg: code/game/objects/items/cosmetics.dm:171
public partial class obj_item_paper : obj_item
{
    // tg: code/game/objects/items/cosmetics.dm:171
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (!Dm.eq(user.zone_selected, BODY_ZONE_PRECISE_MOUTH) || !ishuman(target_mob))
            return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);

        mob_living_carbon_human target = (mob_living_carbon_human)target_mob;
        if (target == user)
        {
            to_chat(user, span_notice(I18n.T("You wipe off the lipstick with [].", this)));
            await target.update_lips(null);
            return null;
        }

        user.visible_message(span_warning(I18n.T("[] begins to wipe []'s lipstick off with \\the [].", user, target, this)),
            span_notice(I18n.T("You begin to wipe off []'s lipstick...", target)));
        if (!Dm.truthy(await do_after(user, 1 * SECONDS, target: target)))
            return null;
        user.visible_message(span_notice(I18n.T("[] wipes []'s lipstick off with \\the [].", user, target, this)),
            span_notice(I18n.T("You wipe off []'s lipstick.", target)));
        await target.update_lips(null);
        return null;
    }
}

// tg: code/game/objects/items/cosmetics.dm:1
public partial class obj_item_lipstick : obj_item
{
    // tg: code/game/objects/items/cosmetics.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        update_appearance(UPDATE_ICON);
        return dot;
    }

    // tg: :26-29  vv_edit_var (an admin's var edit of open updates the icon): a live-partial row (the variable editor, M10+)
    // tg: code/game/objects/items/cosmetics.dm:31
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        dot.Add(I18n.T("Alt-click to change the style."));
        return dot;
    }

    // tg: code/game/objects/items/cosmetics.dm:35
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(open) ? "_uncap" : null);
        inhand_icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(open) ? "open" : null);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/cosmetics.dm:40
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (!Dm.truthy(open))
            return dot;
        mutable_appearance colored_overlay = mutable_appearance(icon, "lipstick_uncap_color") as mutable_appearance;
        colored_overlay.color = lipstick_color;
        Dm.append(ref dot, colored_overlay);
        return dot;
    }

    // tg: code/game/objects/items/cosmetics.dm:48
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        await display_radial_menu(user as mob_living_carbon_human);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/cosmetics.dm:52
    public virtual async DmTask<object> display_radial_menu(mob_living_carbon_human user = null)
    {
        DmList style_options = Dm.list(
            Dm.kv(UPPER_LIP, icon(Dm.resource("icons/hud/radial.dmi"), UPPER_LIP)),
            Dm.kv(MIDDLE_LIP, icon(Dm.resource("icons/hud/radial.dmi"), MIDDLE_LIP)),
            Dm.kv(LOWER_LIP, icon(Dm.resource("icons/hud/radial.dmi"), LOWER_LIP)));
        object pick = await show_radial_menu(user, this, style_options, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user), radius: 36, require_near: TRUE);
        if (!Dm.truthy(pick))
            return TRUE;

        switch (Dm.str(pick))
        {
            case MIDDLE_LIP:
                style = "lipstick";
                break;
            case LOWER_LIP:
                style = "lipstick_lower";
                break;
            case UPPER_LIP:
                style = "lipstick_upper";
                break;
        }
        return TRUE;
    }

    // tg: code/game/objects/items/cosmetics.dm:70
    public virtual object check_menu(mob_living user = null)
    {
        if (user == null)
            return FALSE;
        if (Dm.truthy(user.incapacitated) || !Dm.truthy(user.is_holding(this)))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/objects/items/cosmetics.dm:134
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        to_chat(user, span_notice(I18n.T("You twist [] [].", this, Dm.truthy(open) ? "closed" : "open")));
        open = Dm.truthy(open) ? FALSE : TRUE;
        update_appearance(UPDATE_ICON);
        return null;
    }

    // tg: code/game/objects/items/cosmetics.dm:139
    public override async DmTask<object> attack__proc(mob_living target = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        mob M = target;
        if (!Dm.truthy(open) || !ismob(M))
            return null;

        if (!ishuman(M))
        {
            to_chat(user, span_warning(I18n.T("Where are the lips on that?")));
            return null;
        }

        mob_living_carbon_human human_target = (mob_living_carbon_human)M;
        if (Dm.truthy(human_target.is_mouth_covered()))
        {
            to_chat(user, span_warning(I18n.T("Remove [] mask!", human_target == user ? "your" : Dm.text("[]", human_target.p_their()))));
            return null;
        }
        if (Dm.truthy(human_target.lip_style)) //if they already have lipstick on
        {
            to_chat(user, span_warning(I18n.T("You need to wipe off the old lipstick first!")));
            return null;
        }

        if (human_target == user)
        {
            user.visible_message(span_notice(I18n.T("[] does [] lips with \\the [].", user, user.p_their(), this)),
                span_notice(I18n.T("You take a moment to apply \\the []. Perfect!", this)));
            await human_target.update_lips(style, lipstick_color, lipstick_trait);
            return null;
        }

        user.visible_message(span_warning(I18n.T("[] begins to do []'s lips with \\the [].", user, human_target, this)),
            span_notice(I18n.T("You begin to apply \\the [] on []'s lips...", this, human_target)));
        if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: human_target)))
            return null;
        user.visible_message(span_notice(I18n.T("[] does []'s lips with \\the [].", user, human_target, this)),
            span_notice(I18n.T("You apply \\the [] on []'s lips.", this, human_target)));
        await human_target.update_lips(style, lipstick_color, lipstick_trait);
        return null;
    }
}

// tg: code/game/objects/items/cosmetics.dm:116
public partial class obj_item_lipstick_random : obj_item_lipstick
{
    // tg: code/game/objects/items/cosmetics.dm:120
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = "lipstick";
        DmList possible_colors = TgState.Current.obj_item_lipstick_random__possible_colors;
        if (possible_colors == null)
        {
            possible_colors = TgState.Current.obj_item_lipstick_random__possible_colors = Dm.list();
            foreach (Type lipstick_path in Dm.each<Type>(Dm.sub(typesof(typeof(obj_item_lipstick)), type) as DmList))
            {
                if (!Dm.truthy(Dm.initial(lipstick_path, (obj_item_lipstick l) => l.lipstick_color)) || !Dm.truthy(Dm.initial(lipstick_path, (obj_item_lipstick l) => l.random_spawn)))
                    continue;
                possible_colors[Dm.initial(lipstick_path, (obj_item_lipstick l) => l.lipstick_color)] = Dm.initial(lipstick_path, (obj_item_lipstick l) => l.name);
            }
        }
        lipstick_color = pick(possible_colors) as string;
        name = possible_colors[lipstick_color] as string;
        update_appearance();
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/cosmetics.dm:123  var/static/list/possible_colors
    internal DmList obj_item_lipstick_random__possible_colors;
}

// tg: code/game/objects/items/cosmetics.dm:189
public partial class obj_item_razor : obj_item
{
    // tg: code/game/objects/items/cosmetics.dm:202
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] begins shaving []self without the razor guard! It looks like [] trying to commit suicide!", user, user.p_them(), user.p_theyre())));
        if (ishuman(user))
        {
            shave((mob_living_carbon_human)user, BODY_ZONE_PRECISE_MOUTH);
            shave((mob_living_carbon_human)user, BODY_ZONE_HEAD); //doesn't need to be BODY_ZONE_HEAD specifically, but whatever
        }
        return BRUTELOSS;
    }

    // tg: code/game/objects/items/cosmetics.dm:209
    public virtual void shave(mob_living_carbon_human skinhead = null, object location = null)
    {
        location ??= BODY_ZONE_PRECISE_MOUTH;
        if (Dm.eq(location, BODY_ZONE_PRECISE_MOUTH))
            skinhead.set_facial_hairstyle("Shaved", update: TRUE);
        else
            skinhead.set_hairstyle("Skinhead", update: TRUE);
        playsound(loc, Dm.resource("sound/items/hair-clippers.ogg"), 20, TRUE);
    }

    // tg: code/game/objects/items/cosmetics.dm:216
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (!ishuman(target_mob))
            return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        mob_living_carbon_human human_target = (mob_living_carbon_human)target_mob;
        obj_item_bodypart_head noggin = human_target.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        object location = user.zone_selected;
        if (noggin == null && Dm.@in(location, TgState.Current.obj_item_razor__attack__head_zones))
        {
            to_chat(user, span_warning(I18n.T("[] doesn't have a head!", human_target)));
            return null;
        }
        if (Dm.eq(location, BODY_ZONE_PRECISE_MOUTH))
        {
            if (!Dm.truthy(user.combat_mode))
            {
                if (Dm.eq(human_target.gender, MALE))
                {
                    if (human_target == user)
                    {
                        to_chat(user, span_warning(I18n.T("You need a mirror to properly style your own facial hair!")));
                        return null;
                    }
                    if (!Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
                        return null;
                    object new_style = await tgui_input_list(user, I18n.T("Select a facial hairstyle"), I18n.T("Grooming"), SSaccessories.facial_hairstyles_list);
                    if (new_style == null)
                        return null;
                    object covering = human_target.is_mouth_covered();
                    if (Dm.truthy(covering))
                    {
                        to_chat(user, span_warning(I18n.T("[] is in the way!", covering)));
                        return null;
                    }
                    if (!Dm.truthy(Dm.bitand(noggin.head_flags, HEAD_FACIAL_HAIR)))
                    {
                        to_chat(user, span_warning(I18n.T("There is no facial hair to style!")));
                        return null;
                    }
                    if (HAS_TRAIT(human_target, TRAIT_SHAVED))
                    {
                        to_chat(user, span_warning(I18n.T("[] is just way too shaved. Like, really really shaved.", human_target)));
                        return null;
                    }
                    user.visible_message(span_notice(I18n.T("[] tries to change []'s facial hairstyle using [].", user, human_target, this)), span_notice(I18n.T("You try to change []'s facial hairstyle using [].", human_target, this)));
                    playsound(this, Dm.resource("sound/items/hair-clippers.ogg"), 50);
                    if (Dm.truthy(new_style) && Dm.truthy(await do_after(user, 6 * SECONDS, target: human_target)))
                    {
                        user.visible_message(span_notice(I18n.T("[] successfully changes []'s facial hairstyle using [].", user, human_target, this)), span_notice(I18n.T("You successfully change []'s facial hairstyle using [].", human_target, this)));
                        human_target.set_facial_hairstyle(new_style, update: TRUE);
                        return null;
                    }
                }
                else
                    return null;
            }
            else
            {
                object covering = human_target.is_mouth_covered();
                if (Dm.truthy(covering))
                {
                    to_chat(user, span_warning(I18n.T("[] is in the way!", covering)));
                    return null;
                }
                if (!Dm.truthy(Dm.bitand(noggin.head_flags, HEAD_FACIAL_HAIR)))
                {
                    to_chat(user, span_warning(I18n.T("There is no facial hair to shave!")));
                    return null;
                }
                if (Dm.eq(human_target.facial_hairstyle, "Shaved"))
                {
                    to_chat(user, span_warning(I18n.T("Already clean-shaven!")));
                    return null;
                }

                if (human_target == user) //shaving yourself
                {
                    user.visible_message(span_notice(I18n.T("[] starts to shave [] facial hair with [].", user, user.p_their(), this)),
                        span_notice(I18n.T("You take a moment to shave your facial hair with []...", this)));
                    playsound(this, Dm.resource("sound/items/hair-clippers.ogg"), 50);
                    if (Dm.truthy(await do_after(user, 5 * SECONDS, target: user)))
                    {
                        user.visible_message(span_notice(I18n.T("[] shaves [] facial hair clean with [].", user, user.p_their(), this)),
                            span_notice(I18n.T("You finish shaving with []. Fast and clean!", this)));
                        shave((mob_living_carbon_human)user, location);
                    }
                    return null;
                }
                else
                {
                    user.visible_message(span_warning(I18n.T("[] tries to shave []'s facial hair with [].", user, human_target, this)),
                        span_notice(I18n.T("You start shaving []'s facial hair...", human_target)));
                    playsound(this, Dm.resource("sound/items/hair-clippers.ogg"), 50);
                    if (Dm.truthy(await do_after(user, 5 * SECONDS, target: human_target)))
                    {
                        user.visible_message(span_warning(I18n.T("[] shaves off []'s facial hair with [].", user, human_target, this)),
                            span_notice(I18n.T("You shave []'s facial hair clean off.", human_target)));
                        shave(human_target, location);
                    }
                    return null;
                }
            }
        }
        else if (Dm.eq(location, BODY_ZONE_HEAD))
        {
            if (!Dm.truthy(user.combat_mode))
            {
                if (human_target == user)
                {
                    to_chat(user, span_warning(I18n.T("You need a mirror to properly style your own hair!")));
                    return null;
                }
                if (!Dm.truthy(await user.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
                    return null;
                object new_style = await tgui_input_list(user, I18n.T("Select a hairstyle"), I18n.T("Grooming"), SSaccessories.hairstyles_list);
                if (new_style == null)
                    return null;
                if (!Dm.truthy(human_target.is_location_accessible(location)))
                {
                    to_chat(user, span_warning(I18n.T("The headgear is in the way!")));
                    return null;
                }
                if (!Dm.truthy(Dm.bitand(noggin.head_flags, HEAD_HAIR)))
                {
                    to_chat(user, span_warning(I18n.T("There is no hair to style!")));
                    return null;
                }
                if (HAS_TRAIT(human_target, TRAIT_BALD))
                {
                    to_chat(user, span_warning(I18n.T("[] is just way too bald. Like, really really bald.", human_target)));
                    return null;
                }
                user.visible_message(span_notice(I18n.T("[] tries to change []'s hairstyle using [].", user, human_target, this)), span_notice(I18n.T("You try to change []'s hairstyle using [].", human_target, this)));
                playsound(this, Dm.resource("sound/items/hair-clippers.ogg"), 50);
                if (Dm.truthy(new_style) && Dm.truthy(await do_after(user, 6 * SECONDS, target: human_target)))
                {
                    user.visible_message(span_notice(I18n.T("[] successfully changes []'s hairstyle using [].", user, human_target, this)), span_notice(I18n.T("You successfully change []'s hairstyle using [].", human_target, this)));
                    human_target.set_hairstyle(new_style, update: TRUE);
                    return null;
                }
            }
            else
            {
                if (!Dm.truthy(human_target.is_location_accessible(location)))
                {
                    to_chat(user, span_warning(I18n.T("The headgear is in the way!")));
                    return null;
                }
                if (!Dm.truthy(Dm.bitand(noggin.head_flags, HEAD_HAIR)))
                {
                    to_chat(user, span_warning(I18n.T("There is no hair to shave!")));
                    return null;
                }
                if (Dm.eq(human_target.hairstyle, "Bald") || Dm.eq(human_target.hairstyle, "Balding Hair") || Dm.eq(human_target.hairstyle, "Skinhead"))
                {
                    to_chat(user, span_warning(I18n.T("There is not enough hair left to shave!")));
                    return null;
                }

                if (human_target == user) //shaving yourself
                {
                    user.visible_message(span_notice(I18n.T("[] starts to shave [] head with [].", user, user.p_their(), this)),
                        span_notice(I18n.T("You start to shave your head with []...", this)));
                    playsound(this, Dm.resource("sound/items/hair-clippers.ogg"), 50);
                    if (Dm.truthy(await do_after(user, 5 * SECONDS, target: user)))
                    {
                        user.visible_message(span_notice(I18n.T("[] shaves [] head with [].", user, user.p_their(), this)),
                            span_notice(I18n.T("You finish shaving with [].", this)));
                        shave((mob_living_carbon_human)user, location);
                    }
                    return null;
                }
                else
                {
                    user.visible_message(span_warning(I18n.T("[] tries to shave []'s head with []!", user, human_target, this)),
                        span_notice(I18n.T("You start shaving []'s head...", human_target)));
                    playsound(this, Dm.resource("sound/items/hair-clippers.ogg"), 50);
                    if (Dm.truthy(await do_after(user, 5 * SECONDS, target: human_target)))
                    {
                        user.visible_message(span_warning(I18n.T("[] shaves []'s head bald with []!", user, human_target, this)),
                            span_notice(I18n.T("You shave []'s head bald.", human_target)));
                        shave(human_target, location);
                    }
                    return null;
                }
            }
        }
        return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/cosmetics.dm:222  var/static/list/head_zones
    internal DmList obj_item_razor__attack__head_zones = Dm.list(BODY_ZONE_PRECISE_EYES, BODY_ZONE_PRECISE_MOUTH, BODY_ZONE_HEAD);
}

// tg: code/game/objects/items/cosmetics.dm:342  (gaps OD1)
public partial class obj_item_razor_surgery : obj_item_razor
{
    // tg: code/game/objects/items/cosmetics.dm:347
    public override object get_surgery_tool_overlay(object tray_extended = null)
    {
        return "razor";
    }
}
