// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/crayons.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J3, D298): the crayons, their box and the spray cans, with the Crayon window. Left: the suicide's atom colour keeps tg's filter,
// the cyborg's spray can (no cyborgs; the palette, the spray can's colour wheel, came with silicons SI4d),
// the coolant slapcraft, the face decal of a sprayed face. The var blocks are generated.

/*
 * Crayons
 */
// tg: code/game/objects/items/crayons.dm:25
public partial class obj_item_toy_crayon : obj_item_toy
{
    private const string RANDOM_GRAFFITI = "Random Graffiti";
    private const string RANDOM_LETTER = "Random Letter";
    private const string RANDOM_PUNCTUATION = "Random Punctuation";
    private const string RANDOM_NUMBER = "Random Number";
    private const string RANDOM_SYMBOL = "Random Symbol";
    private const string RANDOM_DRAWING = "Random Drawing";
    private const string RANDOM_ORIENTED = "Random Oriented";
    private const string RANDOM_RUNE = "Random Rune";
    private const string RANDOM_ANY = "Random Anything";

    private const int PAINT_NORMAL = 1;
    private const int PAINT_LARGE_HORIZONTAL = 2;
    private const int AVAILABLE_SPRAYCAN_SPACE = 8; // enough to fill one radial menu page

    protected const float DRAW_TIME = 5 * SECONDS;
    protected const int INFINITE_CHARGES = -1;

    // tg: code/game/objects/items/crayons.dm:97  var/static/list/graffiti and its siblings (made at the first use, rules 10.6)
    internal static DmList graffiti => TgState.Current.obj_item_toy_crayon__graffiti ??= Dm.list("amyjon", "antilizard", "body", "cyka", "dwarf", "end", "engie", "face", "guy", "matt", "prolizard", "revolution", "star", "uboa");
    internal static DmList symbols => TgState.Current.obj_item_toy_crayon__symbols ??= Dm.list("biohazard", "credit", "danger", "electricdanger", "firedanger", "evac", "food", "heart", "like", "med", "nay", "peace", "radiation", "safe", "shop", "skull", "space", "trade");
    internal static DmList drawings => TgState.Current.obj_item_toy_crayon__drawings ??= Dm.list("beepsky", "blueprint", "bottle", "brush", "carp", "cat", "clown", "corgi", "disk", "fireaxe", "ghost", "largebrush", "scroll", "shotgun", Dm.kv("smallbrush", CRAYON_COST_SMALL), "snake", "splatter", "stickman", "taser", "toilet", "toolbox");
    internal static DmList oriented => TgState.Current.obj_item_toy_crayon__oriented ??= Dm.list(Dm.kv("arrow", CRAYON_COST_SMALL), "body", Dm.kv("chevron", CRAYON_COST_SMALL), Dm.kv("clawprint", CRAYON_COST_SMALL), Dm.kv("footprint", CRAYON_COST_SMALL), "line", Dm.kv("pawprint", CRAYON_COST_SMALL), Dm.kv("shortline", CRAYON_COST_SMALL), "thinline");
    internal static DmList runes => TgState.Current.obj_item_toy_crayon__runes ??= Dm.list("rune1", "rune2", "rune3", "rune4", "rune5", "rune6");
    internal static DmList randoms => TgState.Current.obj_item_toy_crayon__randoms ??= Dm.list(RANDOM_ANY, RANDOM_DRAWING, RANDOM_GRAFFITI, RANDOM_ORIENTED, RANDOM_LETTER, RANDOM_NUMBER, RANDOM_PUNCTUATION, RANDOM_RUNE, RANDOM_SYMBOL);
    internal static DmList graffiti_large_h => TgState.Current.obj_item_toy_crayon__graffiti_large_h ??= Dm.list(Dm.kv("furrypride", CRAYON_COST_LARGE), Dm.kv("paint", CRAYON_COST_LARGE), Dm.kv("secborg", CRAYON_COST_LARGE), Dm.kv("yiffhell", CRAYON_COST_LARGE));
    internal static DmList all_drawables => TgState.Current.obj_item_toy_crayon__all_drawables ??= (DmList)Dm.add(Dm.add(Dm.add(Dm.add(Dm.add(graffiti, symbols), drawings), oriented), runes), graffiti_large_h);

    // tg: code/game/objects/items/crayons.dm:201
    public virtual object isValidSurface(object surface = null)
    {
        return isfloorturf(surface);
    }

    // tg: code/game/objects/items/crayons.dm:204
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is jamming [] up [] nose and into [] brain. It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_their(), user.p_theyre())));
        await user.add_atom_colour(color_transition_filter(paint_color, SATURATION_OVERRIDE), ADMIN_COLOUR_PRIORITY);
        return Dm.bitor(BRUTELOSS, OXYLOSS);
    }

    // tg: code/game/objects/items/crayons.dm:209
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        dye_color = crayon_color;

        drawtype = pick(all_drawables);

        Waiting.NoSleep(AddElement(typeof(datum_element_venue_price), FOOD_PRICE_EXOTIC));
        //This makes sure that spraycans do not rename stuff instead of painting
        if (!Dm.truthy(can_change_colour))
            Waiting.NoSleep(AddElement(typeof(datum_element_tool_renaming))); // gaps S3: renaming things with a crayon
        else
            Waiting.NoSleep(AddComponent(typeof(datum_component_palette), AVAILABLE_SPRAYCAN_SPACE, paint_color)); // silicons SI4d

        Waiting.NoSleep(refill());
        if (Dm.truthy(edible))
        {
            datum_reagents holder = (datum_reagents)reagents;
            Waiting.NoSleep(AddComponentFrom(
                SOURCE_EDIBLE_INNATE,
                typeof(datum_component_edible),
                Dm.named("bite_consumption", holder.total_volume / (Dm.num(charges_left) / 5)),
                Dm.named("after_eat", Dm.@new<datum_callback>(this, nameof(after_eat)))));
        }
        return dot;
    }

    /// Used for edible component to reduce charges_left on bite.
    // tg: code/game/objects/items/crayons.dm:233
    public virtual async DmTask after_eat(mob user = null)
    {
        await use_charges(user, amount: 5, requires_full: FALSE, override_infinity: TRUE);
        if (Dm.truthy(check_empty(user, override_infinity: TRUE))) //Prevents division by zero
            return;
    }

    /// Sets painting color and updates appearance.
    // tg: code/game/objects/items/crayons.dm:239
    public override void set_painting_tool_color(object chosen_color = null)
    {
        base.set_painting_tool_color(chosen_color);
        if (!Dm.truthy(can_change_colour))
            return;
        paint_color = Dm.str(chosen_color);
        update_appearance();
    }

    /**
     * Refills charges_left in infinite crayons on use.
     * Sets charges_left in infinite crayons to 100 for spawning reagents.
     * Spawns reagents in crayons based on the amount of charges_left if not spawned yet.
     */
    // tg: code/game/objects/items/crayons.dm:251
    public virtual async DmTask refill()
    {
        if (charges == INFINITE_CHARGES)
            charges_left = 100;
        else
            charges_left = charges;

        if (reagents == null)
            create_reagents(Dm.num(charges_left) * volume_multiplier);
        datum_reagents holder = (datum_reagents)reagents;
        await holder.clear_reagents();

        float total_weight = 0;
        foreach (object key in Dm.each(reagent_contents))
            total_weight += Dm.num(reagent_contents[key]);

        float units_per_weight = holder.maximum_volume / total_weight;
        foreach (object reagent in Dm.each(reagent_contents))
        {
            float weight = Dm.num(reagent_contents[reagent]);
            float amount = weight * units_per_weight;
            await holder.add_reagent(reagent, amount);
        }
    }

    /**
     * Returns number of charges actually used.
     *
     * Arguments:
     * * user - the user.
     * * amount - how much charges do we reduce.
     * * requires_full - Seems to transfer its data to the same argument on check_empty(). I'm not sure tho.
     * * override_infinity - if TRUE stops infinite crayons from refilling.
     */
    // tg: code/game/objects/items/crayons.dm:280
    public virtual async DmTask<object> use_charges(mob user = null, float? amount = null, float? requires_full = null, float? override_infinity = null)
    {
        amount ??= 1;
        requires_full ??= TRUE;
        override_infinity ??= FALSE;
        if (charges == INFINITE_CHARGES && !Dm.truthy(override_infinity))
        {
            await refill();
            return TRUE;
        }
        if (Dm.truthy(check_empty(user, amount, requires_full)))
            return FALSE;
        charges_left = Dm.num(charges_left) - min(Dm.num(charges_left), amount.Value);
        return TRUE;
    }

    /**
     * When eating a crayon, check_empty() can be called twice producing two messages unless we check for being deleted first.
     *
     * Arguments:
     * * user - the user.
     * * amount - used for use_on() and when requires_full is TRUE
     * * requires_full - if TRUE and charges_left < amount it will balloon_alert you. Used just for borgs spraycan it seems.
     * * override_infinity - if TRUE it will override checks for infinite crayons.
     */
    // tg: code/game/objects/items/crayons.dm:298
    public virtual object check_empty(mob user = null, float? amount = null, float? requires_full = null, float? override_infinity = null)
    {
        amount ??= 1;
        requires_full ??= TRUE;
        override_infinity ??= FALSE;
        if (QDELETED(this))
            return TRUE;

        // INFINITE_CHARGES is unlimited charges
        if (charges == INFINITE_CHARGES && !Dm.truthy(override_infinity))
            return FALSE;
        if (!Dm.truthy(charges_left))
        {
            if (Dm.truthy(self_contained))
                qdel(this);
            else
                balloon_alert(user, I18n.T("empty!"));
            return TRUE;
        }
        if (Dm.num(charges_left) < amount && Dm.truthy(requires_full))
        {
            balloon_alert(user, I18n.T("not enough left!"));
            return TRUE;
        }

        return FALSE;
    }

    // tg: code/game/objects/items/crayons.dm:317
    public override object ui_state(mob user = null)
    {
        return GLOB.hands_state;
    }

    // tg: code/game/objects/items/crayons.dm:320
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (!Dm.truthy(actually_paints))
            return null;
        // tgui is a plague upon this codebase
        // no u
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Crayon", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/crayons.dm:330
    public virtual object staticDrawables(object is_literate_user = null)
    {
        DmList dot = Dm.list();

        DmList section(string title, DmList source, bool skip_spelling = false)
        {
            DmList items = Dm.list();
            dot.Add(Dm.list(Dm.list(Dm.kv("name", title), Dm.kv("items", items))));
            foreach (object entry in Dm.each(source))
            {
                if (skip_spelling && !Dm.truthy(is_literate_user)) // no spelling allowed
                {
                    if (Dm.eq(entry, RANDOM_LETTER) || Dm.eq(entry, RANDOM_NUMBER) || Dm.eq(entry, RANDOM_PUNCTUATION))
                        continue;
                }
                items.Add(Dm.list(Dm.list(Dm.kv("item", entry))));
            }
            return items;
        }

        section("Graffiti", graffiti);
        section("Graffiti Large Horizontal", graffiti_large_h);
        section("Symbols", symbols);
        section("Drawings", drawings);
        section("Oriented", oriented);
        section("Runes", runes);
        section("Random", randoms, skip_spelling: true);
        return dot;
    }

    // tg: code/game/objects/items/crayons.dm:371
    public override async DmTask<object> ui_data(mob user = null)
    {
        object is_literate_user = user.is_literate();
        object crayon_drawables = staticDrawables(is_literate_user);

        DmList dot = Dm.list();
        dot["drawables"] = crayon_drawables;
        dot["selected_stencil"] = drawtype;
        dot["text_buffer"] = text_buffer;
        dot["is_literate_user"] = is_literate_user;
        dot["has_cap"] = has_cap;
        dot["is_capped"] = is_capped;
        dot["can_change_colour"] = can_change_colour;
        dot["selected_color"] = GLOB.pipe_color_name[paint_color] ?? paint_color;
        dot["paint_colors"] = GLOB.pipe_paint_colors;
        return dot;
    }

    // tg: code/game/objects/items/crayons.dm:389
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "toggle_cap":
                if (Dm.truthy(has_cap))
                {
                    is_capped = Dm.truthy(is_capped) ? FALSE : TRUE;
                    dot = TRUE;
                }
                break;
            case "select_stencil":
            {
                object stencil = @params["item"];
                if (Dm.@in(stencil, all_drawables) || Dm.@in(stencil, randoms))
                {
                    drawtype = stencil;
                    dot = TRUE;
                    text_buffer = "";
                }
                if (Dm.@in(stencil, graffiti_large_h))
                {
                    paint_mode = PAINT_LARGE_HORIZONTAL;
                    text_buffer = "";
                }
                else
                    paint_mode = PAINT_NORMAL;
                break;
            }
            case "custom_color":
                if (Dm.truthy(can_change_colour))
                {
                    await pick_painting_tool_color(usr, paint_color);
                    dot = TRUE;
                }
                else
                    dot = FALSE;
                break;
            case "color":
                if (!Dm.truthy(can_change_colour))
                    return null;
                paint_color = Dm.str(GLOB.pipe_paint_colors[@params["paint_color"]]);
                set_painting_tool_color(paint_color);
                dot = TRUE;
                break;
            case "enter_text":
            {
                object txt = await tgui_input_text(usr, I18n.T("Choose what to write"), I18n.T("Scribbles"), text_buffer, max_length: MAX_MESSAGE_LEN);
                if (txt == null)
                    return null;
                txt = crayon_text_strip(txt);
                if (Dm.eq(text_buffer, txt))
                    return null; // No valid changes.
                text_buffer = Dm.str(txt);

                dot = TRUE;
                paint_mode = PAINT_NORMAL;
                drawtype = "a";
                break;
            }
        }
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/crayons.dm:432
    public virtual object crayon_text_strip(object text = null)
    {
        text = copytext(text, 1, MAX_MESSAGE_LEN);
        regex crayon_regex = TgState.Current.obj_item_toy_crayon__crayon_regex ??= Dm.regex(@"[^\w!?,.=&%#+/\-]", "ig");
        return LOWER_TEXT(crayon_regex.Replace(text, ""));
    }

    /// Is this a valid object for use_on to run on?
    // tg: code/game/objects/items/crayons.dm:438
    public virtual object can_use_on(atom target = null, mob user = null, DmList modifiers = null)
    {
        if (!isturf(target) && !istype(target, typeof(obj_effect_decal_cleanable)))
            return FALSE;
        return TRUE;
    }

    /// Attempts to color the target.
    // tg: code/game/objects/items/crayons.dm:444
    public virtual async DmTask<object> use_on(atom target = null, mob user = null, DmList modifiers = null)
    {
        DmList punctuation = Dm.list("!", "?", ".", ",", "/", "+", "-", "=", "%", "#", "&");

        if (istype(target, typeof(obj_effect_decal_cleanable)))
            target = target.loc;

        if (!Dm.truthy(isValidSurface(target)))
        {
            target.balloon_alert(user, I18n.T("can't use there!"));
            return ITEM_INTERACT_BLOCKING;
        }

        object drawing = drawtype;
        switch (drawtype)
        {
            case RANDOM_LETTER:
                drawing = ascii2text(rand(97, 122)); // a-z
                break;
            case RANDOM_PUNCTUATION:
                drawing = pick(punctuation);
                break;
            case RANDOM_SYMBOL:
                drawing = pick(symbols);
                break;
            case RANDOM_DRAWING:
                drawing = pick(drawings);
                break;
            case RANDOM_GRAFFITI:
                drawing = pick(graffiti);
                break;
            case RANDOM_RUNE:
                drawing = pick(runes);
                break;
            case RANDOM_ORIENTED:
                drawing = pick(oriented);
                break;
            case RANDOM_NUMBER:
                drawing = ascii2text(rand(48, 57)); // 0-9
                break;
            case RANDOM_ANY:
                drawing = pick(all_drawables);
                break;
        }

        if (Dm.@in(drawing, graffiti_large_h))
        {
            paint_mode = PAINT_LARGE_HORIZONTAL;
            text_buffer = "";
        }
        else
            paint_mode = PAINT_NORMAL;

        bool istagger = HAS_TRAIT(user, TRAIT_TAGGER);
        float cost = Dm.num(all_drawables[drawing] ?? CRAYON_COST_DEFAULT);
        if (istype(target, typeof(obj_item_canvas)))
            cost = 0;
        if (istagger)
            cost *= 0.5f;
        if (Dm.truthy(check_empty(user, cost)))
            return ITEM_INTERACT_BLOCKING;

        string temp = "rune";
        bool ascii = Dm.length(drawing) == 1;
        if (ascii && text2ascii(drawing) <= 122 && text2ascii(drawing) >= 97) // is_lowercase_character (__HELPERS/text.dm:1043), inline
            temp = "letter";
        else if (ascii && text2num(drawing) != null) // is_digit (__HELPERS/text.dm:1047), inline
            temp = "number";
        else if (Dm.@in(drawing, punctuation))
            temp = "punctuation mark";
        else if (Dm.@in(drawing, symbols))
            temp = "symbol";
        else if (Dm.@in(drawing, drawings))
            temp = "drawing";
        else if (Dm.@in(drawing, graffiti) || Dm.@in(drawing, oriented))
            temp = "graffiti";

        object graf_rot = null;
        if (Dm.@in(drawing, oriented))
        {
            switch (user.dir)
            {
                case EAST:
                    graf_rot = 90;
                    break;
                case SOUTH:
                    graf_rot = 180;
                    break;
                case WEST:
                    graf_rot = 270;
                    break;
                default:
                    graf_rot = 0;
                    break;
            }
        }

        object clickx = null;
        object clicky = null;

        if (Dm.truthy(modifiers?[ICON_X]) && Dm.truthy(modifiers?[ICON_Y]))
        {
            clickx = clamp(Dm.num(text2num(modifiers[ICON_X])) - 16, -(ICON_SIZE_X / 2), ICON_SIZE_X / 2);
            clicky = clamp(Dm.num(text2num(modifiers[ICON_Y])) - 16, -(ICON_SIZE_Y / 2), ICON_SIZE_Y / 2);
        }

        if (!Dm.truthy(instant))
            to_chat(user, span_notice(I18n.T("You start drawing a [] on \\the []...", temp, target)));

        if (Dm.truthy(pre_noise))
        {
            audible_message(span_notice(I18n.T("You hear spraying.")));
            playsound(user.loc, Dm.resource("sound/effects/spray.ogg"), 5, TRUE, 5);
        }

        float wait_time = DRAW_TIME;
        if (paint_mode == PAINT_LARGE_HORIZONTAL)
            wait_time *= 3;
        if (istagger)
            wait_time *= 0.5f;

        if (!Dm.truthy(instant) && !Dm.truthy(await do_after(user, wait_time, target: target, max_interact_count: 4, extra_checks: Dm.@new<datum_callback>(this, nameof(adjacency_check), user, target))))
            return ITEM_INTERACT_BLOCKING;

        if (!Dm.truthy(await use_charges(user, cost)))
            return ITEM_INTERACT_BLOCKING;

        if (Dm.truthy(Dm.length(text_buffer)))
            drawing = copytext(text_buffer, 1, 2);

        DmList affected_turfs = Dm.list(target);

        if (Dm.truthy(actually_paints))
        {
            obj_effect_decal_cleanable_crayon created_art = null;
            switch (paint_mode)
            {
                case PAINT_NORMAL:
                    created_art = await Dm.new_async<obj_effect_decal_cleanable_crayon>(target, paint_color, drawing, temp, graf_rot);
                    created_art.pixel_x = Dm.num(clickx);
                    created_art.pixel_y = Dm.num(clicky);
                    break;
                case PAINT_LARGE_HORIZONTAL:
                {
                    turf left = locate(target.x - 1, target.y, target.z);
                    turf right = locate(target.x + 1, target.y, target.z);
                    if (Dm.truthy(isValidSurface(left)) && Dm.truthy(isValidSurface(right)))
                    {
                        created_art = await Dm.new_async<obj_effect_decal_cleanable_crayon>(left, paint_color, drawing, temp, graf_rot, Dm.resource("icons/effects/96x32.dmi"));
                        affected_turfs.Add(left);
                        affected_turfs.Add(right);
                    }
                    else
                    {
                        balloon_alert(user, I18n.T("no room!"));
                        return ITEM_INTERACT_BLOCKING;
                    }
                    break;
                }
            }
            created_art.add_hiddenprint(user);
            if (istagger)
                await created_art.AddElement(typeof(datum_element_art), GOOD_ART);
            else
                await created_art.AddElement(typeof(datum_element_art), BAD_ART);
        }

        if (!Dm.truthy(instant))
            to_chat(user, span_notice(I18n.T("You finish drawing \\the [].", temp)));
        else
            to_chat(user, span_notice(I18n.T("You spray a [] on \\the []", temp, target.name)));

        if (Dm.length(text_buffer) > 1)
        {
            text_buffer = copytext(text_buffer, 2);
            SStgui.update_uis(this);
        }

        if (Dm.truthy(post_noise))
        {
            audible_message(span_hear(I18n.T("You hear spraying.")));
            playsound(user.loc, Dm.resource("sound/effects/spray.ogg"), 5, TRUE, 5);
        }

        // var/fraction = min(1, . / reagents.maximum_volume): tg never uses it past here (. is null at this point)
        if (Dm.truthy(expose_turfs))
        {
            foreach (turf draw_turf in Dm.each<turf>(affected_turfs))
                await ((datum_reagents)reagents).expose(draw_turf, methods: TOUCH, volume_modifier: volume_multiplier);
        }
        check_empty(user);
        return ITEM_INTERACT_SUCCESS;
    }

    ///Checks if the user is still adjacent to the target (used for do_after extra_checks)
    // tg: code/game/objects/items/crayons.dm:595
    public virtual async DmTask<object> adjacency_check(mob user = null, atom target = null)
    {
        if (!Dm.truthy(await user.Adjacent(target)))
        {
            user.balloon_alert(user, I18n.T("moved too far away!"));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/game/objects/items/crayons.dm:601
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(check_allowed_items(interacting_with)))
            return NONE;

        if (Dm.truthy(can_use_on(interacting_with, user, modifiers)))
            return await use_on(interacting_with, user, modifiers);

        if (!ishuman(interacting_with) || Dm.truthy(user.combat_mode))
            return NONE;

        mob_living_carbon_human pwned_human = (mob_living_carbon_human)interacting_with;
        if (!IS_DEAD_OR_FAKING(pwned_human))
            return NONE;

        interacting_with.balloon_alert(user, I18n.T("drawing outline..."));
        if (!Dm.truthy(await do_after(user, DRAW_TIME, target: pwned_human)))
            return ITEM_INTERACT_FAILURE;
        if (!Dm.truthy(await use_charges(user, 1)))
            return ITEM_INTERACT_FAILURE;

        to_chat(user, span_notice(I18n.T("You draw a chalk outline around [].", pwned_human)));
        obj_effect_decal_cleanable_crayon chalk_line = await Dm.new_async<obj_effect_decal_cleanable_crayon>(get_turf(pwned_human), paint_color, "body", "chalk outline", null, null, I18n.T("A vaguely [] shaped body outline.", pwned_human), outline_strength);
        chalk_line.pixel_y = pwned_human.pixel_y + pwned_human.pixel_z;
        chalk_line.pixel_x = pwned_human.pixel_x + pwned_human.pixel_w;
        chalk_line.create_outline(pwned_human, add_mouse_opacity: TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/crayons.dm:628
    public override async DmTask<object> get_writing_implement_details()
    {
        return Dm.list(
            Dm.kv("interaction_mode", MODE_WRITING),
            Dm.kv("font", CRAYON_FONT),
            Dm.kv("color", paint_color),
            Dm.kv("use_bold", TRUE));
    }
}

// tg: code/game/objects/items/crayons.dm:713
public partial class obj_item_toy_crayon_rainbow : obj_item_toy_crayon
{
    // tg: code/game/objects/items/crayons.dm:722
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        set_painting_tool_color(rgb(rand(0, 255), rand(0, 255), rand(0, 255)));
        return await base.interact_with_atom(interacting_with, user, modifiers);
    }
}

/*
 * Crayon Box
 */
// tg: code/game/objects/items/crayons.dm:731
public partial class obj_item_storage_crayons : obj_item_storage
{
    // tg: code/game/objects/items/crayons.dm:739
    public override async DmTask PopulateContents()
    {
        Dm.@new<obj_item_toy_crayon_red>(this);
        Dm.@new<obj_item_toy_crayon_orange>(this);
        Dm.@new<obj_item_toy_crayon_yellow>(this);
        Dm.@new<obj_item_toy_crayon_green>(this);
        Dm.@new<obj_item_toy_crayon_blue>(this);
        Dm.@new<obj_item_toy_crayon_purple>(this);
        Dm.@new<obj_item_toy_crayon_black>(this);
        update_appearance();
    }

    // tg: code/game/objects/items/crayons.dm:749
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        foreach (obj_item_toy_crayon crayon in Dm.each<obj_item_toy_crayon>(contents))
            dot.Add(mutable_appearance(Dm.resource("icons/obj/art/crayons.dmi"), crayon.crayon_color));
        return dot;
    }

    // tg: code/game/objects/items/crayons.dm:754
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.length(contents) > 0)
        {
            balloon_alert(user, I18n.T("too full to fold!"));
            return dot;
        }
        if (Dm.truthy(Dm.bitand(flags_1, HOLOGRAM_1)))
            return dot;

        obj_item_stack_sheet_cardboard cardboard = Dm.@new<obj_item_stack_sheet_cardboard>(user.drop_location());
        to_chat(user, span_notice(I18n.T("You fold the [] into cardboard.", this)));
        await user.put_in_active_hand(cardboard);
        qdel(this);
        return dot;
    }
}

//Spraycan stuff

// tg: code/game/objects/items/crayons.dm:770
public partial class obj_item_toy_crayon_spraycan : obj_item_toy_crayon
{
    /// Types which use their color var for additional logic, so we need to avoid using transition filters on them.
    // tg: code/game/objects/items/crayons.dm:801  var/static/list/direct_color_types
    private static DmList direct_color_types => TgState.Current.obj_item_toy_crayon_spraycan__direct_color_types ??= typecacheof(Dm.list(
        typeof(obj_item_paper), // Uses color for TGUI backgrounds, doesn't look very good either
        typeof(obj_item_fish), // Used for aquarium sprites
        typeof(obj_structure_window))) as DmList; // Does not play nice with window tint

    // tg: code/game/objects/items/crayons.dm:807
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // AddElement(/datum/element/slapcrafting, slapcraft_recipes = list(/datum/crafting_recipe/improvised_coolant)): the improvised coolant
        Dm.Deferred("code/game/objects/items/crayons.dm:811", "Initialize: AddElement(/datum/element/slapcrafting, improvised_coolant): the slapcrafting of the improvised coolant");
        // register_context(), register_item_context(): the context tips (screentips, not now)
        // If default crayon red colour, pick a more fun spraycan colour
        if (!Dm.truthy(paint_color))
            set_painting_tool_color(pick(Dm.list(COLOR_CRAYON_RED, COLOR_CRAYON_ORANGE, COLOR_CRAYON_YELLOW, COLOR_CRAYON_GREEN, COLOR_CRAYON_BLUE, COLOR_CRAYON_PURPLE)));
        Waiting.NoSleep(refill());
        return dot;
    }

    // tg: code/game/objects/items/crayons.dm:849
    public override object isValidSurface(object surface = null)
    {
        return isfloorturf(surface) || iswallturf(surface);
    }

    // tg: code/game/objects/items/crayons.dm:852
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        float used = min(Dm.num(charges_left), 10);
        if (Dm.truthy(is_capped) || !Dm.truthy(actually_paints) || !Dm.truthy(await use_charges(user, 10, FALSE)))
        {
            user.visible_message(span_suicide(I18n.T("[] shakes up [] with a rattle and lifts it to [] mouth, but nothing happens!", user, this, user.p_their())));
            await user.say("MEDIOCRE!!", forced: "spraycan suicide");
            return SHAME;
        }

        user.visible_message(span_suicide(I18n.T("[] shakes up [] with a rattle and lifts it to [] mouth, spraying paint across [] teeth!", user, this, user.p_their(), user.p_their())));
        await user.say("WITNESS ME!!", forced: "spraycan suicide");
        if (Dm.truthy(pre_noise) || Dm.truthy(post_noise))
            playsound(this, Dm.resource("sound/effects/spray.ogg"), 5, TRUE, 5);
        if (Dm.truthy(can_change_colour))
            set_painting_tool_color(COLOR_SILVER);
        update_appearance();
        if (Dm.truthy(actually_paints))
            Dm.Deferred("code/game/objects/items/crayons.dm:867", "suicide_act: AddComponent(/datum/component/face_decal, ...): the sprayed face's overlay (the face decal component)");
        await ((datum_reagents)reagents).trans_to(user, used, volume_multiplier, transferred_by: user, methods: VAPOR);
        return OXYLOSS;
    }

    // tg: code/game/objects/items/crayons.dm:871
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (charges != INFINITE_CHARGES)
        {
            if (Dm.truthy(charges_left))
                dot.Add(I18n.T("It's roughly []% full.", round(Dm.num(charges_left) / charges * 100, 0.1f)));
            else
                dot.Add(I18n.T("It is empty."));
        }
        dot.Add(span_notice(I18n.T("Alt-click [] to [].", this, Dm.truthy(is_capped) ? I18n.T("take the cap off") : I18n.T("put the cap on"))));
        return dot;
    }

    // tg: code/game/objects/items/crayons.dm:881
    public override object can_use_on(atom target = null, mob user = null, DmList modifiers = null)
    {
        if (iscarbon(target))
            return TRUE;
        if (Dm.truthy(is_capped) && HAS_TRAIT(target, TRAIT_COMBAT_MODE_SKIP_INTERACTION))
            // specifically don't try to use a capped spraycan on stuff like bags and tables, just place it
            return FALSE;
        if (ismob(target) && HAS_TRAIT(target, TRAIT_SPRAY_PAINTABLE))
            return TRUE;
        if (isobj(target) && !Dm.truthy(Dm.bitand(target.flags_1, UNPAINTABLE_1)))
            return TRUE;
        return base.can_use_on(target, user, modifiers);
    }

    // tg: code/game/objects/items/crayons.dm:893
    public override async DmTask<object> use_on(atom target = null, mob user = null, DmList modifiers = null)
    {
        if (Dm.truthy(is_capped))
        {
            balloon_alert(user, I18n.T("take the cap off first!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(check_empty(user)))
            return ITEM_INTERACT_BLOCKING;

        if (isbodypart(target))
        {
            if (Dm.truthy(await color_limb((obj_item_bodypart)target, user as mob_living)))
                return ITEM_INTERACT_SUCCESS;
        }

        datum_reagents holder = (datum_reagents)reagents;
        if (iscarbon(target))
        {
            if (Dm.truthy(pre_noise) || Dm.truthy(post_noise))
                playsound(user.loc, Dm.resource("sound/effects/spray.ogg"), 25, TRUE, 5);

            if (Dm.truthy(target._listen_lookup?[COMSIG_CARBON_SPRAYPAINTED]) && Dm.truthy(target._SendSignal(COMSIG_CARBON_SPRAYPAINTED, Dm.list(target, user, this))))
                return ITEM_INTERACT_BLOCKING;

            mob_living_carbon carbon_target = (mob_living_carbon)target;
            user.visible_message(span_danger(I18n.T("[] sprays [] into the face of []!", user, this, target)));
            to_chat(target, span_userdanger(I18n.T("[] sprays [] into your face!", user, this)));

            if (carbon_target.client != null)
            {
                await carbon_target.set_eye_blur_if_lower(6 * SECONDS);
                await carbon_target.adjust_temp_blindness(2 * SECONDS);
            }
            if (Dm.num(carbon_target.get_eye_protection()) <= 0 || Dm.truthy(carbon_target.is_eyes_covered())) // no eye protection? ARGH IT BURNS. Warning: don't add a stun here. It's a roundstart item with some quirks. added redundancy because gas masks don't give you eye protection
            {
                await carbon_target.adjust_jitter(1 * SECONDS);
                await carbon_target.adjust_eye_blur(0.5f * SECONDS);
                flash_color(carbon_target, flash_color: paint_color, flash_time: 40);
            }
            if (ishuman(carbon_target) && Dm.truthy(actually_paints))
                Dm.Deferred("code/game/objects/items/crayons.dm:925", "use_on: AddComponent(/datum/component/face_decal, ...): the sprayed face's overlay (the face decal component)");
            await use_charges(user, 10, FALSE);
            // var/fraction = min(1, . / reagents.maximum_volume): tg's . is null here, so the fraction is 0 (a runtime-free tg quirk, kept)
            await holder.expose(carbon_target, VAPOR, 0 * volume_multiplier);
        }
        else if (Dm.truthy(actually_paints) && Dm.truthy(target.is_atom_colour(paint_color, min_priority_index: WASHABLE_COLOUR_PRIORITY)))
        {
            balloon_alert(user, I18n.T("[] already that color!", target.p_theyre()));
            return ITEM_INTERACT_BLOCKING;
        }

        string saturation_mode = SATURATION_MULTIPLY;
        if (Dm.truthy(modifiers?[RIGHT_CLICK]))
            saturation_mode = SATURATION_OVERRIDE;

        if (ismob(target) && HAS_TRAIT(target, TRAIT_SPRAY_PAINTABLE))
        {
            if (Dm.truthy(actually_paints))
            {
                await target.add_atom_colour(color_transition_filter(paint_color, saturation_mode), WASHABLE_COLOUR_PRIORITY);
                if (Dm.truthy(target._listen_lookup?[COMSIG_LIVING_MOB_PAINTED]))
                    target._SendSignal(COMSIG_LIVING_MOB_PAINTED, Dm.list(target));
            }
            await use_charges(user, 2, requires_full: FALSE);
            await holder.trans_to(target, null, volume_multiplier, transferred_by: user, methods: VAPOR);

            if (Dm.truthy(pre_noise) || Dm.truthy(post_noise))
                playsound(user.loc, Dm.resource("sound/effects/spray.ogg"), 5, TRUE, 5);
            user.visible_message(span_notice(I18n.T("[] coats [] with spray paint!", user, target)), span_notice(I18n.T("You coat [] with spray paint.", target)));
            return ITEM_INTERACT_SUCCESS;
        }

        if (!isobj(target) || Dm.truthy(Dm.bitand(target.flags_1, UNPAINTABLE_1)))
            return await base.use_on(target, user, modifiers);

        bool color_is_dark = Dm.truthy(is_color_dark(paint_color));
        if (!Dm.truthy(actually_paints))
        {
            object painted = !Dm.truthy(target._listen_lookup?[COMSIG_OBJ_PAINTED]) ? NONE : target._SendSignal(COMSIG_OBJ_PAINTED, Dm.list(target, user, this, color_is_dark ? TRUE : FALSE));
            if (!Dm.truthy(Dm.bitand(painted, DONT_USE_SPRAYCAN_CHARGES)))
                await use_charges(user, 2, requires_full: FALSE);
            await holder.trans_to(target, null, volume_multiplier, transferred_by: user, methods: VAPOR);

            if (Dm.truthy(pre_noise) || Dm.truthy(post_noise))
                playsound(user.loc, Dm.resource("sound/effects/spray.ogg"), 5, TRUE, 5);
            user.visible_message(span_notice(I18n.T("[] coats [] with spray paint!", user, target)), span_notice(I18n.T("You coat [] with spray paint.", target)));
            return ITEM_INTERACT_SUCCESS;
        }

        if (color_is_dark && Dm.eq(saturation_mode, SATURATION_OVERRIDE) && !Dm.truthy(Dm.bitand(target.flags_1, ALLOW_DARK_PAINTS_1)))
        {
            to_chat(user, span_warning(I18n.T("A color that dark on an object like this? Surely not...")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (istype(target, typeof(obj_item_pipe)))
        {
            if (!Dm.truthy(GLOB.pipe_color_name.Find(paint_color)))
            {
                balloon_alert(user, I18n.T("invalid pipe color!"));
                return ITEM_INTERACT_BLOCKING;
            }
            obj_item_pipe target_pipe = (obj_item_pipe)target;
            target_pipe.pipe_color = paint_color;
            await target.add_atom_colour(paint_color, FIXED_COLOUR_PRIORITY);
            balloon_alert(user, I18n.T("painted in [] color", GLOB.pipe_color_name[paint_color]));
        }
        else if (istype(target, typeof(obj_machinery_atmospherics)))
        {
            if (!Dm.truthy(GLOB.pipe_color_name.Find(paint_color)))
            {
                balloon_alert(user, I18n.T("invalid pipe color!"));
                return ITEM_INTERACT_BLOCKING;
            }
            obj_machinery_atmospherics target_pipe = (obj_machinery_atmospherics)target;
            await target_pipe.paint(paint_color);
            balloon_alert(user, I18n.T("painted in [] color", GLOB.pipe_color_name[paint_color]));
        }
        else if (Dm.truthy(is_type_in_typecache(target, direct_color_types)))
            await target.add_atom_colour(paint_color, WASHABLE_COLOUR_PRIORITY);
        else
            await target.add_atom_colour(color_transition_filter(paint_color, saturation_mode), WASHABLE_COLOUR_PRIORITY);

        if (isitem(target) && isliving(target.loc))
        {
            obj_item target_item = (obj_item)target;
            mob_living holder_mob = (mob_living)target.loc;
            if (Dm.truthy(holder_mob.is_holding(target_item)))
                await holder_mob.update_held_items();
            else
                await holder_mob.update_clothing(target_item.slot_flags);
        }

        object painted_result = !Dm.truthy(target._listen_lookup?[COMSIG_OBJ_PAINTED]) ? NONE : target._SendSignal(COMSIG_OBJ_PAINTED, Dm.list(target, user, this, color_is_dark ? TRUE : FALSE));
        if (!Dm.truthy(Dm.bitand(painted_result, DONT_USE_SPRAYCAN_CHARGES)))
            await use_charges(user, 2, requires_full: FALSE);
        await holder.trans_to(target, null, volume_multiplier, transferred_by: user, methods: VAPOR);

        if (Dm.truthy(pre_noise) || Dm.truthy(post_noise))
            playsound(user.loc, Dm.resource("sound/effects/spray.ogg"), 5, TRUE, 5);
        user.visible_message(span_notice(I18n.T("[] coats [] with spray paint!", user, target)), span_notice(I18n.T("You coat [] with spray paint.", target)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/crayons.dm:1005
    public virtual async DmTask<object> color_limb(obj_item_bodypart limb = null, mob_living user = null)
    {
        if (!Dm.truthy(IS_ROBOTIC_LIMB(limb)))
            return FALSE;
        Dm.Deferred("code/game/objects/items/crayons.dm:1022", "color_limb: restyling a robotic limb by a radial menu (with the robotic limbs, J5 and P1)");
        return TRUE;
    }

    // tg: code/game/objects/items/crayons.dm:1028
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(has_cap))
            return CLICK_ACTION_BLOCKING;
        is_capped = Dm.truthy(is_capped) ? FALSE : TRUE;
        balloon_alert(user, Dm.truthy(is_capped) ? I18n.T("capped") : I18n.T("cap removed"));
        update_appearance();
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/crayons.dm:1036
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.str(Dm.truthy(is_capped) ? icon_capped : icon_uncapped);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/crayons.dm:1040
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (Dm.truthy(overlay_paint_colour))
        {
            mutable_appearance spray_overlay = (mutable_appearance)mutable_appearance(Dm.resource("icons/obj/art/crayons.dmi"), Dm.truthy(is_capped) ? "spraycan_cap_colors" : "spraycan_colors");
            spray_overlay.color = paint_color;
            dot.Add(spray_overlay);
        }
        return dot;
    }
}

// tg: code/game/objects/items/crayons.dm:1067
public partial class obj_item_toy_crayon_spraycan_hellcan : obj_item_toy_crayon_spraycan
{
    // tg: code/game/objects/items/crayons.dm:1081
    public override object isValidSurface(object surface = null)
    {
        return isfloorturf(surface);
    }
}

// tg: code/game/objects/items/crayons.dm:1085
public partial class obj_item_toy_crayon_spraycan_lubecan : obj_item_toy_crayon_spraycan
{
    // tg: code/game/objects/items/crayons.dm:1096
    public override object isValidSurface(object surface = null)
    {
        return isfloorturf(surface);
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/crayons.dm:97-199  the crayon's var/static lists (null until the first use)
    internal DmList obj_item_toy_crayon__graffiti;
    internal DmList obj_item_toy_crayon__symbols;
    internal DmList obj_item_toy_crayon__drawings;
    internal DmList obj_item_toy_crayon__oriented;
    internal DmList obj_item_toy_crayon__runes;
    internal DmList obj_item_toy_crayon__randoms;
    internal DmList obj_item_toy_crayon__graffiti_large_h;
    internal DmList obj_item_toy_crayon__all_drawables;
    // tg: code/game/objects/items/crayons.dm:434  crayon_text_strip's var/static/regex/crayon_regex
    internal regex obj_item_toy_crayon__crayon_regex;
    // tg: code/game/objects/items/crayons.dm:801
    internal DmList obj_item_toy_crayon_spraycan__direct_color_types;
}

// Silicons SI2 (D326): the cyborg's spraycan, which paints with its cell's charge.
// tg: code/game/objects/items/crayons.dm:1047
public partial class obj_item_toy_crayon_spraycan_borg : obj_item_toy_crayon_spraycan
{
    // tg: code/game/objects/items/crayons.dm:1052
    public override async DmTask<object> use_charges(mob user = null, float? amount = null, float? requires_full = null, float? override_infinity = null)
    {
        amount ??= 1;
        requires_full ??= TRUE;
        override_infinity ??= FALSE;
        if (!iscyborg(user))
        {
            to_chat(user, span_notice(I18n.T("How did you get this?")));
            qdel(this);
            return FALSE;
        }

        mob_living_silicon_robot borgy = (mob_living_silicon_robot)user;
        // 25 is our cost per unit of paint, making it cost 25 energy per
        // normal tag, 50 per window, and 250 per attack
        if (borgy.cell__obj == null || !Dm.truthy(await borgy.cell__obj.use(amount.Value * 25)))
            return FALSE;
        return await base.use_charges(user, amount, requires_full, override_infinity);
    }
}
