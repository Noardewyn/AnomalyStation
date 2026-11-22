// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/flatpacks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps OD2 (D333): the file whole. A flatpack holds a machine's board (and the parts the flatpacker packed with it), is named after
// the machine, slows its carrier, and deploys the machine with a multitool on a free open floor after a second (the mook dust, the
// terminal's eject sound), the packed parts as the machine's parts. The flatpack cart holds up to three flatpacks, drawn stacked
// on it ("flatcart_flat" overlays), hands back the topmost, and drops them when taken apart. The var blocks are generated (D111);
// the flatpack's board holds the board's type until Initialize makes the board (Generator.Members.cs ObjectHoldingVars).

// tg: code/game/objects/items/flatpacks.dm:1
public partial class obj_item_flatpack : obj_item
{
    // tg: code/game/objects/items/flatpacks.dm:17
    [DmParams("mapload", "new_board")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_item_circuitboard_machine new_board = Dm.arg(__extra, 0) as obj_item_circuitboard_machine;
        if (board == null && new_board == null)
            return INITIALIZE_HINT_QDEL; //how

        object dot = base.Initialize(mapload, Dm.forward(__extra, new_board));

        Waiting.NoSleep(AddElement(typeof(datum_element_contextual_screentip_tools), Initialize__tool_behaviors));

        board = new_board != null ? new_board : Dm.@new((Type)board, this); // i got board
        if (((obj_item_circuitboard_machine)board).loc != this)
            Waiting.NoSleep(((obj_item_circuitboard_machine)board).forceMove(this));
        Type build = Dm.initial(board, (obj_item_circuitboard p) => p.build_path);
        name = I18n.T("flatpack ([])", Dm.initial(build, (atom p) => p.name));
        return dot;
    }

    // tg: code/game/objects/items/flatpacks.dm:23  Initialize's var/static/list/tool_behaviors (rules 10.6)
    private static DmList Initialize__tool_behaviors => TgState.Current.obj_item_flatpack__Initialize__tool_behaviors;

    // tg: code/game/objects/items/flatpacks.dm:36
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref board);
        object dot = base.Destroy(force);
        return dot;
    }

    // tg: code/game/objects/items/flatpacks.dm:40
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!in_range(user, this) && !isobserver(user))
            return dot;

        if (loc == user)
            Dm.append(ref dot, span_warning(I18n.T("You can't deploy while holding it in your hand.")));
        else if (isturf(loc))
        {
            turf location = loc as turf;
            if (!isopenturf(location))
                Dm.append(ref dot, span_warning(I18n.T("Can't deploy in this location")));
            else if (Dm.truthy(await location.is_blocked_turf(source_atom: this)))
                Dm.append(ref dot, span_warning(I18n.T("No space for deployment")));
        }
        return dot;
    }

    // tg: code/game/objects/items/flatpacks.dm:54
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = NONE;

        if (board == null)
            return ITEM_INTERACT_BLOCKING;
        if (!isturf(loc))
        {
            balloon_alert(user, I18n.T("must deploy on the floor"));
            return ITEM_INTERACT_BLOCKING;
        }
        turf location = loc as turf;
        if (!isopenturf(location))
        {
            balloon_alert(user, I18n.T("can't deploy here"));
            return ITEM_INTERACT_BLOCKING;
        }
        else if (Dm.truthy(await location.is_blocked_turf(source_atom: this)))
        {
            balloon_alert(user, I18n.T("no space for deployment"));
            return ITEM_INTERACT_BLOCKING;
        }
        balloon_alert_to_viewers(I18n.T("deploying!"));
        if (!Dm.truthy(await do_after(user, 1 * SECONDS, target: this)))
            return ITEM_INTERACT_BLOCKING;

        Dm.@new<obj_effect_temp_visual_mook_dust>(loc);
        obj_item_circuitboard_machine leaving_circuit = board as obj_item_circuitboard_machine;
        if (contents.len > 1)
        {
            leaving_circuit.replacement_parts = leaving_circuit.flatten_component_list() as DmList;
            foreach (obj_item flatpack_component in Dm.each<obj_item>(this))
            {
                if (flatpack_component == leaving_circuit)
                    continue;
                for (float i = 1, i__end = leaving_circuit.replacement_parts.len; i <= i__end; i++)
                {
                    object machine_component = leaving_circuit.replacement_parts[i];
                    if (!ispath(machine_component, typeof(obj_item)))
                        continue;
                    if (Dm.eq(flatpack_component.type, machine_component))
                    {
                        leaving_circuit.replacement_parts[i] = flatpack_component;
                        break;
                    }
                }
            }
        }

        board = null;
        obj_machinery new_machine = Dm.@new(leaving_circuit.build_path, loc, leaving_circuit) as obj_machinery; // new leaving_circuit.build_path(loc, board = leaving_circuit), as machine_frame.cs
        await new_machine.on_construction(user);
        loc.visible_message(span_warning(I18n.T("[] deploys!", this)));
        playsound(this, Dm.resource("sound/machines/terminal/terminal_eject.ogg"), 70, TRUE);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/game/objects/items/flatpacks.dm:99
public partial class obj_structure_flatpack_cart : obj_structure
{
    ///Maximum number of flatpacks in a cart
    private const int MAX_FLAT_PACKS = 3; // tg: flatpacks.dm:97 (file-local, #undef at :166)

    // tg: code/game/objects/items/flatpacks.dm:108
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        register_context();

        Waiting.NoSleep(AddElement(typeof(datum_element_noisy_movement), Dm.named("volume", 45))); // i hate noise
        return dot;
    }

    // tg: code/game/objects/items/flatpacks.dm:115
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        foreach (atom_movable content in Dm.each_cast<atom_movable>(contents))
            await content.forceMove(drop_location() as atom);
    }

    // tg: code/game/objects/items/flatpacks.dm:119
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = NONE;
        if (held_item == null)
            return dot;

        if (held_item is obj_item_flatpack)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Load pack";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/game/objects/items/flatpacks.dm:128
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!in_range(user, this) && !isobserver(user))
            return dot;

        Dm.append(ref dot, I18n.T("From bottom to top, this cart contains:"));
        foreach (obj_item flatpack in Dm.each_cast<obj_item>(contents))
            Dm.append(ref dot, flatpack.name);
        return dot;
    }

    // tg: code/game/objects/items/flatpacks.dm:137
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        float offset = 0;
        foreach (object item in Dm.each(contents))
        {
            mutable_appearance flatpack_overlay = mutable_appearance(icon, "flatcart_flat", layer: layer + (offset * 0.01f)) as mutable_appearance;
            flatpack_overlay.pixel_z = offset;
            offset += 4;
            Dm.append(ref dot, flatpack_overlay);
        }
        return dot;
    }

    // tg: code/game/objects/items/flatpacks.dm:147
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        await user.put_in_hands(contents[length(contents)] as obj_item); //topmost box
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/game/objects/items/flatpacks.dm:154
    [DmParams("user", "attacking_item", "params")]
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item attacking_item = null, DmList @params = null, params object[] __extra)
    {
        if (!(attacking_item is obj_item_flatpack) || Dm.truthy(user.combat_mode) || Dm.truthy(Dm.bitand(attacking_item.flags_1, HOLOGRAM_1)) || Dm.truthy(Dm.bitand(attacking_item.item_flags, ABSTRACT)))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        if (length(contents) >= MAX_FLAT_PACKS)
        {
            balloon_alert(user, I18n.T("full!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(attacking_item, this)))
            return ITEM_INTERACT_BLOCKING;
        update_appearance(UPDATE_OVERLAYS);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/game/objects/items/flatpacks.dm:23
public sealed partial class TgState
{
    internal DmList obj_item_flatpack__Initialize__tool_behaviors = Dm.list(
        Dm.kv(TOOL_MULTITOOL, Dm.list(
            Dm.kv(SCREENTIP_CONTEXT_LMB, "Deploy"))));
}
