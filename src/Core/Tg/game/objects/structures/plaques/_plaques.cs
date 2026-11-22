// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/plaques/_plaques.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps ST2 (D333): the whole file (the gold plaque: mounted on its wall at the map load, read by a click, engraved once with a title
// and a text by a fountain pen, repaired by a welder, unwrenched into the plaque item, which is engraved, repaired and fastened to a
// wall the same way). The var blocks are generated (D111).

// tg: code/game/objects/structures/plaques/_plaques.dm:1
public partial class obj_structure_plaque : obj_structure
{
    // tg: code/game/objects/structures/plaques/_plaques.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload))
            Waiting.NoSleep(find_and_mount_on_atom());
        register_context();
        return dot;
    }

    // tg: code/game/objects/structures/plaques/_plaques.dm:27
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);
        object sw__1 = held_item?.tool_behaviour;
        if (Dm.eq(sw__1, TOOL_WELDER))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Repair";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if (Dm.eq(sw__1, TOOL_WRENCH))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Unfasten";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if (istype(held_item, typeof(obj_item_pen_fountain)) && !Dm.truthy(engraved))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Engrave";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/game/objects/structures/plaques/_plaques.dm:40
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot) || Dm.truthy(user.is_blind()))
            return dot;
        user.examinate(this);
        return dot;
    }

    // tg: code/game/objects/structures/plaques/_plaques.dm:46
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        user.visible_message(span_notice(I18n.T("[] starts removing []...", user, this)),
            span_notice(I18n.T("You start unfastening [].", this)));
        tool.play_tool_sound(this);
        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS)))
            return TRUE;
        playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
        user.visible_message(span_notice(I18n.T("[] unfastens [].", user, this)),
            span_notice(I18n.T("You unfasten [].", this)));
        obj_item_plaque unwrenched_plaque = Dm.@new<obj_item_plaque>(get_turf(user));
        if (Dm.truthy(engraved)) //If it's still just a basic unengraved plaque, we can (and should) skip some of the below variable transfers.
        {
            unwrenched_plaque.name = name; //Copy over the plaque structure variables to the plaque item we're creating when we unwrench it.
            unwrenched_plaque.desc = desc;
            unwrenched_plaque.engraved = engraved;
        }
        unwrenched_plaque.icon_state = icon_state;
        unwrenched_plaque.update_integrity(get_integrity());
        await unwrenched_plaque.setDir(dir);
        qdel(this); //The plaque structure on the wall goes poof and only the plaque item from unwrenching remains.
        return TRUE;
    }

    // tg: code/game/objects/structures/plaques/_plaques.dm:67
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        await base.welder_act(user, tool);
        if (Dm.truthy(user.combat_mode))
            return FALSE;
        if (Dm.eq(atom_integrity, max_integrity))
        {
            to_chat(user, span_warning(I18n.T("This plaque is already in perfect condition.")));
            return TRUE;
        }
        if (!Dm.truthy(tool.tool_start_check(user, amount: 1)))
            return TRUE;
        user.visible_message(span_notice(I18n.T("[] starts repairing []...", user, this)),
            span_notice(I18n.T("You start repairing [].", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, volume: 50)))
            return TRUE;
        user.visible_message(span_notice(I18n.T("[] finishes repairing [].", user, this)),
            span_notice(I18n.T("You finish repairing [].", this)));
        atom_integrity = max_integrity;
        return TRUE;
    }

    // tg: code/game/objects/structures/plaques/_plaques.dm:85
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_pen)))
            return NONE;

        if (!istype(tool, typeof(obj_item_pen_fountain)))
        {
            if (Dm.truthy(engraved))
            {
                to_chat(user, span_warning(I18n.T("This plaque has already been engraved, and your pen isn't fancy enough to engrave it anyway! Find a fountain pen.")));
                return ITEM_INTERACT_BLOCKING;
            }

            to_chat(user, span_warning(I18n.T("Your pen isn't fancy enough to engrave this! Find a fountain pen."))); //Go steal the Curator's.
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(engraved))
        {
            to_chat(user, span_warning(I18n.T("This plaque has already been engraved.")));
            return ITEM_INTERACT_BLOCKING;
        }

        object namechoice = await tgui_input_text(user, I18n.T("Title this plaque. (e.g. 'Best HoP Award', 'Great Ashwalker War Memorial')"), I18n.T("Plaque Customization"), max_length: MAX_NAME_LEN);
        if (!Dm.truthy(namechoice))
            return ITEM_INTERACT_BLOCKING;

        object descriptionchoice = await tgui_input_text(user, I18n.T("Engrave this plaque's text"), I18n.T("Plaque Customization"), max_length: MAX_PLAQUE_LEN);
        if (!Dm.truthy(descriptionchoice))
            return ITEM_INTERACT_BLOCKING;

        if (!Dm.truthy(await Adjacent(user))) //Make sure user is adjacent still
        {
            to_chat(user, span_warning(I18n.T("You need to stand next to the plaque to engrave it!")));
            return ITEM_INTERACT_BLOCKING;
        }

        user.visible_message(span_notice(I18n.T("[] begins engraving [].", user, this)),
            span_notice(I18n.T("You begin engraving [].", this)));
        if (!Dm.truthy(await do_after(user, 4 * SECONDS, target: this))) //This spits out a visible message that somebody is engraving a plaque, then has a delay.
            return ITEM_INTERACT_BLOCKING;

        name = Dm.text("\\improper []", namechoice); //We want improper here so examine doesn't get weird if somebody capitalizes the plaque title.
        desc = I18n.T("The plaque reads: '[]'", descriptionchoice);
        engraved = TRUE; //The plaque now has a name, description, and can't be altered again.
        user.visible_message(span_notice(I18n.T("[] engraves [].", user, this)),
            span_notice(I18n.T("You engrave [].", this)));
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/game/objects/structures/plaques/_plaques.dm:125
public partial class obj_item_plaque : obj_item
{
    // tg: code/game/objects/structures/plaques/_plaques.dm:147
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        await base.welder_act(user, tool);
        if (Dm.truthy(user.combat_mode))
            return FALSE;
        if (Dm.eq(atom_integrity, max_integrity))
        {
            to_chat(user, span_warning(I18n.T("This plaque is already in perfect condition.")));
            return TRUE;
        }
        if (!Dm.truthy(tool.tool_start_check(user, amount: 1)))
            return TRUE;
        user.visible_message(span_notice(I18n.T("[] starts repairing []...", user, this)),
            span_notice(I18n.T("You start repairing [].", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, volume: 50)))
            return TRUE;
        user.visible_message(span_notice(I18n.T("[] finishes repairing [].", user, this)),
            span_notice(I18n.T("You finish repairing [].", this)));
        atom_integrity = max_integrity;
        return TRUE;
    }

    // tg: code/game/objects/structures/plaques/_plaques.dm:166
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        //Same as part of the above, except for the item in hand instead of the structure.
        if (!istype(tool, typeof(obj_item_pen)))
            return NONE;

        if (!istype(tool, typeof(obj_item_pen_fountain)))
        {
            if (Dm.truthy(engraved))
            {
                to_chat(user, span_warning(I18n.T("This plaque has already been engraved, and your pen isn't fancy enough to engrave it anyway! Find a fountain pen.")));
                return ITEM_INTERACT_BLOCKING;
            }

            to_chat(user, span_warning(I18n.T("Your pen isn't fancy enough to engrave this! Find a fountain pen."))); //Go steal the Curator's.
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(engraved))
        {
            to_chat(user, span_warning(I18n.T("This plaque has already been engraved.")));
            return ITEM_INTERACT_BLOCKING;
        }

        object namechoice = await tgui_input_text(user, I18n.T("Title this plaque. (e.g. 'Best HoP Award', 'Great Ashwalker War Memorial')"), I18n.T("Plaque Customization"), max_length: MAX_NAME_LEN);
        if (!Dm.truthy(namechoice))
            return ITEM_INTERACT_BLOCKING;

        object descriptionchoice = await tgui_input_text(user, I18n.T("Engrave this plaque's text"), I18n.T("Plaque Customization"), max_length: MAX_PLAQUE_LEN);
        if (!Dm.truthy(descriptionchoice))
            return ITEM_INTERACT_BLOCKING;

        if (!Dm.truthy(await Adjacent(user))) //Make sure user is adjacent still
        {
            to_chat(user, span_warning(I18n.T("You need to stand next to the plaque to engrave it!")));
            return ITEM_INTERACT_BLOCKING;
        }

        user.visible_message(span_notice(I18n.T("[] begins engraving [].", user, this)),
            span_notice(I18n.T("You begin engraving [].", this)));
        if (!Dm.truthy(await do_after(user, 4 * SECONDS, target: this))) //This spits out a visible message that somebody is engraving a plaque, then has a delay.
            return ITEM_INTERACT_BLOCKING;

        name = Dm.text("\\improper []", namechoice); //We want improper here so examine doesn't get weird if somebody capitalizes the plaque title.
        desc = I18n.T("The plaque reads: '[]'", descriptionchoice);
        engraved = TRUE; //The plaque now has a name, description, and can't be altered again.
        user.visible_message(span_notice(I18n.T("[] engraves [].", user, this)),
            span_notice(I18n.T("You engrave [].", this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/plaques/_plaques.dm:206
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!iswallturf(interacting_with))
            return NONE;
        turf target_turf = (turf)interacting_with;
        turf user_turf = get_turf(user) as turf;
        obj_structure_plaque placed_plaque = Dm.@new(plaque_path, user_turf) as obj_structure_plaque; //We place the plaque on the turf the user is standing, and pixel shift it to the target wall, as below.
        //This is to mimic how signs and other wall objects are usually placed by mappers, and so they're only visible from one side of a wall.
        float dir = get_dir(user_turf, target_turf);
        if (Dm.truthy(Dm.bitand(dir, NORTH)))
            placed_plaque.pixel_y = 32;
        else if (Dm.truthy(Dm.bitand(dir, SOUTH)))
            placed_plaque.pixel_y = -32;
        if (Dm.truthy(Dm.bitand(dir, EAST)))
            placed_plaque.pixel_x = 32;
        else if (Dm.truthy(Dm.bitand(dir, WEST)))
            placed_plaque.pixel_x = -32;
        user.visible_message(span_notice(I18n.T("[] fastens [] to [].", user, this, target_turf)),
            span_notice(I18n.T("You attach [] to [].", this, target_turf)));
        playsound(target_turf, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
        if (Dm.truthy(engraved))
        {
            placed_plaque.name = name;
            placed_plaque.desc = desc;
            placed_plaque.engraved = engraved;
        }
        placed_plaque.icon_state = icon_state;
        placed_plaque.update_integrity(get_integrity());
        await placed_plaque.setDir(dir);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}
