// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/rcd/RLD.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// E13 (the RCD family): the whole file, the rapid lighting device and its mini variant: its radial menu (the light fixture, the
// glowstick launcher, the deconstruct mode and tg's colour picker that tints the menu's pictures), a light tube mounted on the wall
// it is aimed at (on the free side nearest to the user) or a floor light on a floor, removing a light, and a glowstick of the
// nearest fluid colour thrown at the target, each with tg's beam, delays, costs and sounds.

// RAPID LIGHTING DEVICE

// tg: code/game/objects/items/rcd/RLD.dm:18
public partial class obj_item_construction_rld : obj_item_construction
{
    //modes of operation
    // tg: code/game/objects/items/rcd/RLD.dm:4-6  (file-local defines, rules 11.4)
    private const float GLOW_MODE = 1;
    private const float LIGHT_MODE = 2;
    private const float REMOVE_MODE = 3;

    //operation costs
    // tg: code/game/objects/items/rcd/RLD.dm:9-12
    private const float LIGHT_TUBE_COST = 10;
    private const float FLOOR_LIGHT_COST = 15;
    private const float GLOW_STICK_COST = 5;
    private const float DECONSTRUCT_COST = 10;

    //operation delays
    // tg: code/game/objects/items/rcd/RLD.dm:15-16
    private const float BUILD_DELAY = 10;
    private const float REMOVE_DELAY = 15;

    ///reference to thr original icons
    // tg: code/game/objects/items/rcd/RLD.dm:37  var/static/list/original_options (rules 10.6)
    public static ref DmList original_options => ref TgState.Current.obj_item_construction_rld__original_options;

    // tg: code/game/objects/items/rcd/RLD.dm:48
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        foreach (object option in Dm.each(original_options))
            display_options[option] = Dm.icon(original_options[option]);
        return dot;
    }

    // tg: code/game/objects/items/rcd/RLD.dm:53
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);

        if (Dm.truthy(Dm.bitand(construction_upgrades, RCD_UPGRADE_SILO_LINK)) && display_options["Silo Link"] == null) //silo upgrade instaled but option was not updated then update it just one
            display_options["Silo Link"] = Dm.icon(Dm.resource("icons/obj/machines/ore_silo.dmi"), "silo");

        object choice = await show_radial_menu(user, this, display_options, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user), require_near: TRUE, tooltips: TRUE);
        if (!Dm.truthy(check_menu(user as mob_living)))
            return dot;
        if (!Dm.truthy(choice))
            return dot;

        switch (choice)
        {
            case "Light Fixture":
                mode = LIGHT_MODE;
                to_chat(user, span_notice(I18n.T("You change RLD's mode to 'Permanent Light Construction'.")));
                break;
            case "Glow Stick":
                mode = GLOW_MODE;
                to_chat(user, span_notice(I18n.T("You change RLD's mode to 'Light Launcher'.")));
                break;
            case "Color Pick":
            {
                object new_choice = await tgui_color_picker(user, "", I18n.T("Choose Color"), color_choice);
                if (new_choice == null)
                    return dot;

                DmList new_rgb = rgb2num(Dm.str(new_choice));
                foreach (object option in Dm.each(original_options))
                {
                    if (Dm.eq(option, "Color Pick") || Dm.eq(option, "Deconstruct") || Dm.eq(option, "Silo Link"))
                        continue;
                    icon icon = Dm.icon(original_options[option]);
                    icon.SetIntensity(Dm.num(new_rgb[1]) / 255, Dm.num(new_rgb[2]) / 255, Dm.num(new_rgb[3]) / 255); //apply new scale
                    display_options[option] = icon;
                }

                color_choice = Dm.str(new_choice);
                break;
            }
            case "Deconstruct":
                mode = REMOVE_MODE;
                to_chat(user, span_notice(I18n.T("You change RLD's mode to 'Deconstruct'.")));
                break;
            default:
                toggle_silo(user);
                break;
        }
        return dot;
    }

    // tg: code/game/objects/items/rcd/RLD.dm:92
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(range_check(interacting_with, user)))
            return NONE;
        return await try_lighting(interacting_with, user);
    }

    // tg: code/game/objects/items/rcd/RLD.dm:97
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (Dm.truthy(Dm.bitand(Dm.num(dot), ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        return await try_lighting(interacting_with, user);
    }

    /**
     * Try to place/remove a light or throw a glowstick
     *
     * Arguments
     * * atom/interacting_with - the target atom to light or throw glowsticks at
     * * mob/user - the player doing this action
     */
    // tg: code/game/objects/items/rcd/RLD.dm:110
    public virtual async DmTask<object> try_lighting(atom interacting_with = null, mob user = null)
    {
        if (HAS_TRAIT(interacting_with, TRAIT_COMBAT_MODE_SKIP_INTERACTION))
            return NONE;

        turf start = get_turf(this);
        object sw__1 = mode;
        if (Dm.eq(sw__1, REMOVE_MODE))
        {
            if (!istype(interacting_with, typeof(obj_machinery_light)))
                return NONE;

            //resource sanity checks before & after delay
            if (!Dm.truthy(await useResource(DECONSTRUCT_COST, user, TRUE)))
                return ITEM_INTERACT_BLOCKING;
            object beam = user.Beam(interacting_with, icon_state: "light_beam", time: 15);
            playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
            if (!Dm.truthy(await do_after(user, REMOVE_DELAY, target: interacting_with)))
            {
                qdel(beam);
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await useResource(DECONSTRUCT_COST, user, TRUE)))
                return ITEM_INTERACT_BLOCKING;
            qdel(interacting_with);
            await useResource(DECONSTRUCT_COST, user);
            return ITEM_INTERACT_SUCCESS;
        }
        else if (Dm.eq(sw__1, LIGHT_MODE))
        {
            //resource sanity checks before & after delay
            float cost = iswallturf(interacting_with) ? LIGHT_TUBE_COST : FLOOR_LIGHT_COST;

            if (!Dm.truthy(await useResource(cost, user, TRUE)))
                return ITEM_INTERACT_BLOCKING;
            object beam = user.Beam(interacting_with, icon_state: "light_beam", time: BUILD_DELAY);
            playsound(loc as atom, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
            playsound(loc as atom, Dm.resource("sound/effects/light_flicker.ogg"), 50, FALSE);
            if (!Dm.truthy(await do_after(user, BUILD_DELAY, target: interacting_with)))
            {
                qdel(beam);
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await useResource(cost, user, TRUE)))
                return ITEM_INTERACT_BLOCKING;

            if (iswallturf(interacting_with))
            {
                turf_open winner = null;
                float winning_dist = 0;
                foreach (object direction in Dm.each(GLOB.cardinals))
                {
                    turf C = get_step(interacting_with, Dm.num(direction));
                    //turf already has a light
                    if (Dm.truthy(Dm.locate<obj_machinery_light>(C)))
                        continue;
                    //can't put a light here
                    if (!(isspaceturf(C) || Dm.truthy(TURF_SHARES(C))))
                        continue;
                    //find turf closest to our player
                    float x0 = Dm.num(C.x);
                    float y0 = Dm.num(C.y);
                    float contender = CHEAP_HYPOTENUSE(Dm.num(start.x), Dm.num(start.y), x0, y0);
                    if (!Dm.truthy(winner))
                    {
                        winner = C as turf_open;
                        winning_dist = contender;
                    }
                    else if (contender < winning_dist) // lower is better
                    {
                        winner = C as turf_open;
                        winning_dist = contender;
                    }
                }
                if (!Dm.truthy(winner))
                {
                    balloon_alert(user, I18n.T("no valid target!"));
                    return ITEM_INTERACT_BLOCKING;
                }
                obj_machinery_light L = Dm.@new<obj_machinery_light>(get_turf(winner));
                await L.setDir(get_dir(winner, interacting_with));
                L.color = color_choice;
                L.set_light_color(color_choice);
                await L.find_and_mount_on_atom();
                await useResource(cost, user);
                return ITEM_INTERACT_SUCCESS;
            }

            if (isfloorturf(interacting_with))
            {
                turf target = get_turf(interacting_with);
                if (Dm.truthy(Dm.locate<obj_machinery_light_floor>(target)))
                    return ITEM_INTERACT_BLOCKING;
                obj_machinery_light_floor FL = Dm.@new<obj_machinery_light_floor>(target);
                FL.color = color_choice;
                FL.set_light_color(color_choice);
                await FL.find_and_mount_on_atom();
                await useResource(cost, user);
                return ITEM_INTERACT_SUCCESS;
            }
        }
        else if (Dm.eq(sw__1, GLOW_MODE))
        {
            if (!Dm.truthy(await useResource(GLOW_STICK_COST, user)))
                return ITEM_INTERACT_BLOCKING;
            // Picks the closest fitting color for the fluid by hue
            object closest_diff = null;
            Type closest_fluid = null;
            DmList unwrapped_color = rgb2num(color_choice, COLORSPACE_HSV);
            float chosen_hue = Dm.num(unwrapped_color[1]);
            foreach (Type glowstick_fluid in Dm.each_cast<Type>(typesof(typeof(datum_reagent_luminescent_fluid))))
            {
                unwrapped_color = rgb2num(Dm.str(Dm.initial(glowstick_fluid, (datum_reagent r) => r.color)), COLORSPACE_HSV);
                float hue_diff = abs(chosen_hue - Dm.num(unwrapped_color[1]));
                if (hue_diff > 180)
                    hue_diff = 360 - hue_diff;
                if (closest_diff == null || hue_diff < Dm.num(closest_diff))
                {
                    closest_diff = hue_diff;
                    closest_fluid = glowstick_fluid;
                }
            }
            obj_item_flashlight_glowstick new_stick = Dm.@new<obj_item_flashlight_glowstick>(start, null, closest_fluid);
            new_stick.color = color_choice;
            new_stick.set_light_color(new_stick.color);
            await new_stick.throw_at(interacting_with, 9, 3, user);
            await new_stick.turn_on();
            new_stick.update_brightness();
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/rcd/RLD.dm:37  /obj/item/construction/rld var/static/list/original_options (icons are values, no datum)
    internal DmList obj_item_construction_rld__original_options = Dm.list(
        Dm.kv("Color Pick", Dm.icon(icon: Dm.resource("icons/hud/radial.dmi"), state: "omni")),
        Dm.kv("Glow Stick", Dm.icon(icon: Dm.resource("icons/obj/lighting.dmi"), state: "glowstick")),
        Dm.kv("Deconstruct", Dm.icon(icon: Dm.resource("icons/obj/tools.dmi"), state: "wrench")),
        Dm.kv("Light Fixture", Dm.icon(icon: Dm.resource("icons/obj/lighting.dmi"), state: "ltube")));
}
