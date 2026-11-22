// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/signs/_signs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M3 I1a): the sign's init and its wall mount, so that /obj/structure/sign is live and the posters under it run
// their Initialize (random posters pick a poster, poster.dm). M5 slice 7: the context tips, unwrenching a sign into its
// sign item, the welder's repair and atom_deconstruct. Examine on a click (M9) is a generated
// live-partial stub (docs/ai/specs/live-partial.txt); the pen's sign change came with the paperwork (gaps S3). Gaps ST5 (D333): the
// sign item (/obj/item/sign, the unwrenched sign and the plastic recipe's blank sign): laid flat, a pen picks its design, placed on
// a wall it stands in line with (or a screwdriver on the wall ahead), the welder's repair, and the random sign. The var blocks are
// generated (D111).

// tg: code/game/objects/structures/signs/_signs.dm:1
public partial class obj_structure_sign : obj_structure
{
    // tg: code/game/objects/structures/signs/_signs.dm:23
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        register_context();
        if (Dm.truthy(mapload) && !Dm.truthy(Waiting.NoSleep(find_and_mount_on_atom(mark_for_late_init: TRUE))))
            return INITIALIZE_HINT_LATELOAD;
        return dot;
    }

    // tg: code/game/objects/structures/signs/_signs.dm:29
    // set waitfor = FALSE (inherited from /atom/LateInitialize): the mount adds a component, so this body is the wrapper of
    // waiting-model.md 5.4.
    [ShouldNotOverride]
    public override void LateInitialize(params object[] __extra)
    {
        Waiting.WaitforFalse(LateInitialize__body(__extra));
    }

    private async DmTask LateInitialize__body(object[] __extra)
    {
        await find_and_mount_on_atom(late_init: TRUE);
    }

    // tg: code/game/objects/structures/signs/_signs.dm:32
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);
        object sw__1 = held_item?.tool_behaviour;
        if (Dm.eq(sw__1, TOOL_WELDER))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Repair";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        else if (Dm.eq(sw__1, TOOL_WRENCH))
        {
            if (!Dm.truthy(buildable_sign))
                return dot; ///Cannot be unfastened regardless.
            context[SCREENTIP_CONTEXT_LMB] = "Unfasten";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return NONE;
    }

    // tg: code/game/objects/structures/signs/_signs.dm:50
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item I = null)
    {
        await base.wrench_act(user, I);
        if (!Dm.truthy(buildable_sign))
            return ITEM_INTERACT_FAILURE;
        user.visible_message(span_notice(I18n.T("[] starts removing []...", user, this)),
            span_notice(I18n.T("You start unfastening [].", this)));
        I.play_tool_sound(this);
        if (!Dm.truthy(await I.use_tool(this, user, 4 * SECONDS)))
            return ITEM_INTERACT_FAILURE;
        playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE); // tg: code/game/objects/structures/signs/_signs.dm:59
        user.visible_message(span_notice(I18n.T("[] unfastens [].", user, this)),
            span_notice(I18n.T("You unfasten [].", this)));
        await deconstruct(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/signs/_signs.dm:65
    public override async DmTask<object> welder_act(mob_living user = null, obj_item I = null)
    {
        await base.welder_act(user, I);
        if (Dm.truthy(user.combat_mode))
            return FALSE;
        if (Dm.eq(atom_integrity, max_integrity))
        {
            to_chat(user, span_warning(I18n.T("This sign is already in perfect condition.")));
            return TRUE;
        }
        if (!Dm.truthy(I.tool_start_check(user, amount: 1)))
            return TRUE;
        user.visible_message(span_notice(I18n.T("[] starts repairing []...", user, this)),
            span_notice(I18n.T("You start repairing [].", this)));
        if (!Dm.truthy(await I.use_tool(this, user, 4 * SECONDS, volume: 50)))
            return TRUE;
        user.visible_message(span_notice(I18n.T("[] finishes repairing [].", user, this)),
            span_notice(I18n.T("You finish repairing [].", this)));
        atom_integrity = max_integrity;
        return TRUE;
    }

    // tg: code/game/objects/structures/signs/_signs.dm:113
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        atom drop_turf = drop_location() as atom;
        obj_item_sign unwrenched_sign = Dm.@new<obj_item_sign>(drop_turf);
        if (GetType() != typeof(obj_structure_sign_blank)) //If it's still just a basic sign backing, we can (and should) skip some of the below variable transfers.
        {
            unwrenched_sign.name = name; //Copy over the sign structure variables to the sign item we're creating when we unwrench a sign.
            unwrenched_sign.desc = Dm.text("[] It can be placed on a wall.", desc);
            unwrenched_sign.icon = icon;
            unwrenched_sign.icon_state = icon_state;
            unwrenched_sign.sign_path = GetType();
            await unwrenched_sign.set_custom_materials(custom_materials); //This is here so picture frames and wooden things don't get messed up.
            unwrenched_sign.is_editable = is_editable;
        }
        await unwrenched_sign.setDir(dir);
    }

    // tg: code/game/objects/structures/signs/_signs.dm:45  (the tram port: an empty hand opens what a sign shows, the tram plate's window, else examines it)
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers);
        if (Dm.truthy(dot) || Dm.truthy(user.is_blind()))
            return dot;
        user.examinate(this);
        return dot;
    }

    // tg: code/game/objects/structures/signs/_signs.dm:84  (gaps S3: a pen changes an editable sign's design)
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(is_editable) || !IS_WRITING_UTENSIL(tool))
            return NONE;

        if (!Dm.truthy(length(GLOB.editable_sign_types)))
            throw Dm.CRASH("GLOB.editable_sign_types failed to populate", "code/game/objects/structures/signs/_signs.dm", 89);

        object choice = await tgui_input_list(user, I18n.T("Select a sign type"), I18n.T("Sign Customization"), GLOB.editable_sign_types);
        if (choice == null)
            return ITEM_INTERACT_BLOCKING;

        if (!Dm.truthy(await Adjacent(user))) //Make sure user is adjacent still.
        {
            to_chat(user, span_warning(I18n.T("You need to stand next to the sign to change it!")));
            return ITEM_INTERACT_BLOCKING;
        }

        user.visible_message(span_notice(I18n.T("[] begins changing [].", user, this)),
            span_notice(I18n.T("You begin changing [].", this)));
        if (!Dm.truthy(await do_after(user, 4 * SECONDS, target: this))) //Small delay for changing signs instead of it being instant, so somebody could be shoved or stunned to prevent them from doing so.
            return ITEM_INTERACT_BLOCKING;

        Type sign_type = GLOB.editable_sign_types[choice] as Type;
        //It's import to clone the pixel layout information.
        //Otherwise signs revert to being on the turf and
        //move jarringly.
        obj_structure_sign changedsign = Dm.@new(sign_type, get_turf(this)) as obj_structure_sign;
        changedsign.pixel_x = pixel_x;
        changedsign.pixel_y = pixel_y;
        changedsign.atom_integrity = atom_integrity;
        qdel(this);
        user.visible_message(span_notice(I18n.T("[] finishes changing the sign.", user)),
            span_notice(I18n.T("You finish changing the sign.")));
        return ITEM_INTERACT_SUCCESS;
    }
}

public static partial class GlobalProcs
{
    // tg: code/game/objects/structures/signs/_signs.dm:259  (gaps S3)
    public static object populate_editable_sign_types()
    {
        DmList output = Dm.list();
        foreach (Type s in Dm.each<Type>(subtypesof(typeof(obj_structure_sign))))
        {
            if (!Dm.truthy(Dm.initial(s, (obj_structure_sign p) => p.is_editable)))
                continue;
            object shown_name = Dm.initial(s, (obj_structure_sign p) => p.sign_change_name) is var change_name && Dm.truthy(change_name) ? change_name : capitalize(format_text(Dm.initial(s, (obj_structure_sign p) => p.name)));
            if (Dm.truthy(output[shown_name]))
            {
                if (!ispath(s, output[shown_name] as Type))
                    stack_trace(Dm.text("Two signs share the same sign_change_name: [] and []", output[shown_name], s), "code/game/objects/structures/signs/_signs.dm", 268);
                continue;
            }
            output[shown_name] = s;
        }
        output = sort_list(output) as DmList; //Alphabetizes the results.
        return output;
    }
}

// tg: code/game/objects/structures/signs/_signs.dm:136  (gaps ST5)
public partial class obj_item_sign : obj_item
{
    //Signs not attached to walls are always rotated so they look like they're laying horizontal.
    // tg: code/game/objects/structures/signs/_signs.dm:160
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        matrix M = Dm.matrix();
        M.Turn(90);
        transform = M;
        register_context();
        return dot;
    }

    // tg: code/game/objects/structures/signs/_signs.dm:167
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);
        if (Dm.truthy(is_editable) && IS_WRITING_UTENSIL(held_item))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Change design";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/game/objects/structures/signs/_signs.dm:173
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(is_editable) || !IS_WRITING_UTENSIL(tool))
            return await base.item_interaction(user, tool, modifiers, __extra);
        if (!Dm.truthy(length(GLOB.editable_sign_types)))
            throw Dm.CRASH("GLOB.editable_sign_types failed to populate", "code/game/objects/structures/signs/_signs.dm", 177);
        object choice = await tgui_input_list(user, I18n.T("Select a sign type"), I18n.T("Sign Customization"), GLOB.editable_sign_types);
        if (choice == null)
            return ITEM_INTERACT_BLOCKING;
        if (!Dm.truthy(await Adjacent(user))) //Make sure user is adjacent still.
        {
            to_chat(user, span_warning(I18n.T("You need to stand next to the sign to change it!")));
            return ITEM_INTERACT_BLOCKING;
        }
        user.visible_message(span_notice(I18n.T("[] begins changing [].", user, this)),
            span_notice(I18n.T("You begin changing [].", this)));
        if (!Dm.truthy(await do_after(user, 4 * SECONDS, target: this)))
            return ITEM_INTERACT_BLOCKING;
        set_sign_type(GLOB.editable_sign_types[choice] as Type);
        user.visible_message(span_notice(I18n.T("[] finishes changing the sign.", user)),
            span_notice(I18n.T("You finish changing the sign.")));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/signs/_signs.dm:193
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!iswallturf(interacting_with) && !istype(interacting_with, typeof(obj_structure_tram)))
            return NONE;
        atom target_turf = interacting_with;
        turf user_turf = get_turf(user);
        object dir = get_dir(user_turf, target_turf);
        if (!Dm.@in(dir, GLOB.cardinals))
        {
            balloon_alert(user, I18n.T("stand in line with wall!"));
            return ITEM_INTERACT_BLOCKING;
        }
        obj_structure_sign placed_sign = Dm.@new(sign_path, user_turf) as obj_structure_sign; //We place the sign on the turf the user is standing, and pixel shift it to the target wall, as below.
        //This is to mimic how signs and other wall objects are usually placed by mappers, and so they're only visible from one side of a wall.
        if (Dm.truthy(Dm.bitand(dir, NORTH)))
            placed_sign.pixel_y = 32;
        else if (Dm.truthy(Dm.bitand(dir, SOUTH)))
            placed_sign.pixel_y = -32;
        if (Dm.truthy(Dm.bitand(dir, EAST)))
            placed_sign.pixel_x = 32;
        else if (Dm.truthy(Dm.bitand(dir, WEST)))
            placed_sign.pixel_x = -32;
        user.visible_message(span_notice(I18n.T("[] fastens [] to [].", user, this, target_turf)),
            span_notice(I18n.T("You attach the sign to [].", target_turf)));
        playsound(target_turf, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
        placed_sign.update_integrity(get_integrity());
        await placed_sign.setDir(dir);
        await placed_sign.find_and_mount_on_atom();
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/signs/_signs.dm:221
    public override async DmTask<object> welder_act(mob_living user = null, obj_item I = null)
    {
        await base.welder_act(user, I);
        if (Dm.truthy(user.combat_mode))
            return FALSE;
        if (Dm.eq(atom_integrity, max_integrity))
        {
            to_chat(user, span_warning(I18n.T("This sign is already in perfect condition.")));
            return TRUE;
        }
        if (!Dm.truthy(I.tool_start_check(user, amount: 1)))
            return TRUE;
        user.visible_message(span_notice(I18n.T("[] starts repairing []...", user, this)),
            span_notice(I18n.T("You start repairing [].", this)));
        if (!Dm.truthy(await I.use_tool(this, user, 4 * SECONDS, volume: 50)))
            return TRUE;
        user.visible_message(span_notice(I18n.T("[] finishes repairing [].", user, this)),
            span_notice(I18n.T("You finish repairing [].", this)));
        atom_integrity = max_integrity;
        return TRUE;
    }

    // tg: code/game/objects/structures/signs/_signs.dm:239
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return await interact_with_atom(get_step(get_turf(user), user.dir), user);
    }

    // tg: code/game/objects/structures/signs/_signs.dm:242
    public virtual void set_sign_type(Type fake_type = null)
    {
        name = Dm.initial(fake_type, (obj_structure_sign p) => p.name);
        if (fake_type != typeof(obj_structure_sign_blank))
            desc = I18n.T("[] It can be placed on a wall.", Dm.initial(fake_type, (obj_structure_sign p) => p.desc));
        else
            desc = Dm.initial(this, (atom p) => p.desc);
        icon_state = Dm.initial(fake_type, (obj_structure_sign p) => p.icon_state);
        sign_path = fake_type;
    }
}

// tg: code/game/objects/structures/signs/_signs.dm:250  (gaps ST5)
public partial class obj_item_sign_random : obj_item_sign
{
    // tg: code/game/objects/structures/signs/_signs.dm:251
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_sign_type(GLOB.editable_sign_types[pick(GLOB.editable_sign_types)] as Type);
        return dot;
    }
}
