// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/safe.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (2026-10-18, the owner: things tg hides under the floor showed on the station's floors), whole since the gaps package G1
// (D291): the safe's init (its combination, its icon and the items on its tile taken inside), its examine, icon, explosions and lock
// checks; the floor safe's undertile element (hidden under a floor tile as in tg) and the vault safe's piggy bank; resetting the lock
// with a wrench, putting items in, and the dial's tgui (ui_interact, ui_data, ui_act: the dial, the opening and the retrieving; the
// native Safe window, godot/scripts/Shared/Tgui/Interfaces/Safe.cs). The one leaf left is the wrench's screentip (the context
// tips, still deferred). The var blocks are generated.

// tg: code/game/objects/structures/safe.dm:13
public partial class obj_structure_safe : obj_structure
{
    /// Chance for a sound clue
    private const float SOUND_CHANCE = 10; // tg: code/game/objects/structures/safe.dm:8
    /// Explosion number threshold for opening safe
    private const float BROKEN_THRESHOLD = 3; // tg: code/game/objects/structures/safe.dm:10

    // tg: code/game/objects/structures/safe.dm:50
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Dm.Deferred("code/game/objects/structures/safe.dm:53", "Initialize: AddElement(/datum/element/contextual_screentip_tools, tool_behaviors): screentips (M10+)");

        // Combination generation
        for (float iterating = 1; iterating <= number_of_tumblers; iterating++)
            Dm.append(tumblers, rand(0, 99));

        if (Dm.truthy(density))
        {
            Waiting.NoSleep(AddElement(typeof(datum_element_climbable)));
            Dm.Deferred("code/game/objects/structures/safe.dm:65", "Initialize: AddElement(/datum/element/elevation, pixel_shift = 22): the raised position of what stands on it: the client draws a standing mob on the structure's top instead (D239, DV154)");
        }

        if (Dm.truthy(open) && !Dm.truthy(locked))
            return dot;

        update_appearance(UPDATE_ICON);

        // Put as many items on our turf inside as possible
        foreach (obj_item inserting_item in Dm.each<obj_item>(loc))
        {
            if (space >= maxspace)
                return dot;
            if (inserting_item.w_class + space <= maxspace)
            {
                space += inserting_item.w_class;
                Waiting.NoSleep(inserting_item.forceMove(this));
            }
        }
        return dot;
    }

    // tg: code/game/objects/structures/safe.dm:81
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("The locking mechanism gears are <b>wrenched</b> in place.")));
        return dot;
    }

    // tg: code/game/objects/structures/safe.dm:85
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        //uses the same icon as the captain's spare safe (therefore lockable storage) so keep it in line with that
        icon_state = Dm.text("[][]", Dm.initial(this, (obj_structure_safe p) => p.icon_state), Dm.truthy(open) ? null : "_locked");
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/structures/safe.dm:90
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(open))
        {
            balloon_alert(user, I18n.T("must be open!"));
            return ITEM_INTERACT_BLOCKING;
        }

        balloon_alert(user, I18n.T("resetting lock..."));
        to_chat(user, span_notice(I18n.T("You begin resetting the lock for []. You'll need to set [] numbers.", this, number_of_tumblers)));

        DmList new_tumblers = Dm.list();
        for (float tumbler_index = 1; tumbler_index <= number_of_tumblers; tumbler_index++)
        {
            object input_value = await tgui_input_number(user, I18n.T("Set tumbler #[] (0-99):", tumbler_index), I18n.T("Set Lock"), 0, 99, 0);
            if (input_value == null)
            {
                balloon_alert(user, I18n.T("reset cancelled!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.can_perform_action(this)))
            {
                balloon_alert(user, I18n.T("reset interrupted!"));
                return ITEM_INTERACT_BLOCKING;
            }
            Dm.append(new_tumblers, Dm.list(input_value));
        }

        tool.play_tool_sound(this);
        if (!Dm.truthy(await do_after(user, 10 * SECONDS, target: this)))
            return ITEM_INTERACT_BLOCKING;

        tumblers = new_tumblers;
        current_tumbler_index = 1;
        dial = 0;
        tool.play_tool_sound(this);
        to_chat(user, span_notice(I18n.T("You successfully reset the lock for []. The new combination is: [].", this, tumblers.Join("-"))));
        balloon_alert(user, I18n.T("lock set!"));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/safe.dm:121
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;

        if (!Dm.truthy(open))
        {
            if (!istype(tool, typeof(obj_item_clothing_neck_stethoscope)))
            {
                to_chat(user, span_warning(I18n.T("You can't put [] into the safe while it is closed!", tool)));
                return ITEM_INTERACT_BLOCKING;
            }

            await attack_hand(user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool.w_class + space > maxspace)
        {
            to_chat(user, span_warning(I18n.T("[] won't fit in [].", tool, this)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
        {
            to_chat(user, span_warning(I18n.T("\\The [] is stuck to your hand, you cannot put it in the safe!", tool)));
            return ITEM_INTERACT_BLOCKING;
        }

        space += tool.w_class;
        to_chat(user, span_notice(I18n.T("You put [] in [].", tool, this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/safe.dm:146
    public override DmTask<object> blob_act(obj_structure_blob attacking_blob = null) // the root slot (no live ancestor declares it yet)
    {
        return DmTask<object>.FromResult(null);
    }

    // tg: code/game/objects/structures/safe.dm:149
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        if (((Dm.eq(severity, EXPLODE_HEAVY) && target == this) || Dm.eq(severity, EXPLODE_DEVASTATE)) && explosion_count < BROKEN_THRESHOLD)
        {
            explosion_count++;
            switch (explosion_count)
            {
                case 1:
                    desc = Dm.initial(this, (obj_structure_safe p) => p.desc) + I18n.T("\nIt looks a little banged up.");
                    break;
                case 2:
                    desc = Dm.initial(this, (obj_structure_safe p) => p.desc) + I18n.T("\nIt's pretty heavily damaged.");
                    break;
                case 3:
                    desc = Dm.initial(this, (obj_structure_safe p) => p.desc) + I18n.T("\nThe lock seems to be broken.");
                    break;
            }

            return TRUE;
        }

        return FALSE;
    }

    // tg: code/game/objects/structures/safe.dm:164
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/simple/safe)): the asset transport is the client's (DV44); the Safe window draws the dial
        // from its own copy of tg's icons/ui/safe/safe_dial.png (the icon tool copies it).
        return Dm.list();
    }

    // tg: code/game/objects/structures/safe.dm:169
    public override object ui_state(mob user = null)
    {
        return GLOB.physical_state;
    }

    // tg: code/game/objects/structures/safe.dm:172
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Safe", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/structures/safe.dm:178
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["dial"] = dial;
        data["open"] = open;
        data["locked"] = locked;
        data["broken"] = check_broken();

        if (Dm.truthy(open))
        {
            DmList contents_names = Dm.list();
            data["contents"] = contents_names;
            foreach (obj jewel in Dm.each<obj>(contents))
            {
                // DEVIATION(DV150): tg sends the item's sprite name and `user << browse_rsc(icon(jewel.icon, jewel.icon_state), "[jewel.icon_state].png")`,
                // a picture the window shows by name; the native window draws the item's own icon file and state (D236), so the file is sent too.
                UNTYPED_LIST_ADD(ref contents_names, Dm.list(Dm.kv("name", jewel.name), Dm.kv("sprite", jewel.icon_state), Dm.kv("icon", jewel.icon)));
            }
        }

        return data;
    }

    // tg: code/game/objects/structures/safe.dm:194
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (!ishuman(usr))
            return null;
        mob_living_carbon_human user = usr as mob_living_carbon_human;
        if (!Dm.truthy(await user.can_perform_action(this)))
            return null;

        bool canhear = false;
        if (Dm.truthy(user.is_holding_item_of_type(typeof(obj_item_clothing_neck_stethoscope))))
            canhear = true;

        if (Dm.eq(action, "open"))
        {
            if (!Dm.truthy(check_unlocked()) && !Dm.truthy(open) && !Dm.truthy(broken))
            {
                to_chat(user, span_warning(I18n.T("You cannot open [], as its lock is engaged!", this)));
                return null;
            }
            to_chat(user, span_notice(I18n.T("You [] [].", Dm.truthy(open) ? I18n.T("close") : I18n.T("open"), this)));
            open = Dm.truthy(open) ? FALSE : TRUE;
            update_appearance();
            return TRUE;
        }
        else if (Dm.eq(action, "turnright"))
        {
            if (Dm.truthy(open))
                return null;
            if (Dm.truthy(broken))
            {
                to_chat(user, span_warning(I18n.T("The dial will not turn, as the mechanism is destroyed!")));
                return null;
            }
            // the client sends num: tg loops it unbounded (BYOND's tick limit stops a huge one); the port caps it at ten full turns
            float ticks = clamp(Dm.num(text2num(@params["num"])), 0, 1000);
            for (float iterate = 1; iterate <= ticks; iterate++)
            {
                dial = WRAP(dial - 1, 0, 100);

                bool invalid_turn = Dm.mod(current_tumbler_index, 2) == 0 || current_tumbler_index > number_of_tumblers;
                if (invalid_turn) // The moment you turn the wrong way or go too far, the tumblers reset
                    current_tumbler_index = 1;

                if (!invalid_turn && Dm.eq(dial, tumblers[current_tumbler_index]))
                {
                    notify_user(user, canhear, Dm.list("tink", "krink", "plink"), ticks, iterate);
                    current_tumbler_index++;
                }
                else
                    notify_user(user, canhear, Dm.list("clack", "scrape", "clank"), ticks, iterate);
            }
            check_unlocked();
            return TRUE;
        }
        else if (Dm.eq(action, "turnleft"))
        {
            if (Dm.truthy(open))
                return null;
            if (Dm.truthy(broken))
            {
                to_chat(user, span_warning(I18n.T("The dial will not turn, as the mechanism is destroyed!")));
                return null;
            }
            // the client sends num: tg loops it unbounded (BYOND's tick limit stops a huge one); the port caps it at ten full turns
            float ticks = clamp(Dm.num(text2num(@params["num"])), 0, 1000);
            for (float iterate = 1; iterate <= ticks; iterate++)
            {
                dial = WRAP(dial + 1, 0, 100);

                bool invalid_turn = Dm.mod(current_tumbler_index, 2) != 0 || current_tumbler_index > number_of_tumblers;
                if (invalid_turn) // The moment you turn the wrong way or go too far, the tumblers reset
                    current_tumbler_index = 1;

                if (!invalid_turn && Dm.eq(dial, tumblers[current_tumbler_index]))
                {
                    notify_user(user, canhear, Dm.list("tonk", "krunk", "plunk"), ticks, iterate);
                    current_tumbler_index++;
                }
                else
                    notify_user(user, canhear, Dm.list("click", "chink", "clink"), ticks, iterate);
            }
            check_unlocked();
            return TRUE;
        }
        else if (Dm.eq(action, "retrieve"))
        {
            if (!Dm.truthy(open))
                return null;
            object index = text2num(@params["index"]);
            if (!Dm.truthy(index))
                return null;
            obj_item retrieved_item = contents[index] as obj_item;
            if (retrieved_item == null || !in_range(this, user))
                return null;
            await user.put_in_hands(retrieved_item);
            space -= retrieved_item.w_class;
            return TRUE;
        }
        return null;
    }

    /**
     * Checks if safe is considered in a broken state for force-opening the safe
     */
    // tg: code/game/objects/structures/safe.dm:276
    public virtual object check_broken()
    {
        return Dm.truthy(broken) || explosion_count >= BROKEN_THRESHOLD ? TRUE : FALSE;
    }

    /**
     * Called every dial turn to determine whether the safe should unlock or not.
     */
    // tg: code/game/objects/structures/safe.dm:282
    public virtual object check_unlocked()
    {
        if (Dm.truthy(check_broken()))
            return TRUE;
        if (current_tumbler_index > number_of_tumblers)
        {
            locked = FALSE;
            visible_message(span_boldnotice(I18n.T("[]!", pick("Spring", "Sprang", "Sproing", "Clunk", "Krunk"))));
            return TRUE;
        }
        locked = TRUE;
        return FALSE;
    }

    /**
     * Called every dial turn to provide feedback if possible.
     */
    // tg: code/game/objects/structures/safe.dm:295
    public virtual void notify_user(object user = null, object canhear = null, object sounds = null, object total_ticks = null, object current_tick = null)
    {
        if (!Dm.truthy(canhear))
            return;
        if (Dm.eq(current_tick, 2))
            to_chat(user, span_italics(I18n.T("The sounds from [] are too fast and blend together.", this)));
        if (Dm.eq(total_ticks, 1) || prob(SOUND_CHANCE))
            (user as atom)?.balloon_alert(user as mob, pick(sounds));
    }
}

//FLOOR SAFES
// tg: code/game/objects/structures/safe.dm:308
public partial class obj_structure_safe_floor : obj_structure_safe
{
    // tg: code/game/objects/structures/safe.dm:319
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_undertile)));
        return dot;
    }
}

///Special safe for the station's vault. Not explicitly required, but the piggy bank inside it is.
// tg: code/game/objects/structures/safe.dm:328
public partial class obj_structure_safe_vault : obj_structure_safe
{
    // tg: code/game/objects/structures/safe.dm:330
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        obj_item piggy = Dm.@new<obj_item_piggy_bank_vault>(this);
        space += piggy.w_class;
        return dot;
    }
}
