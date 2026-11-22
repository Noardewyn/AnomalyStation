// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/fireaxe.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 5 (windows, grilles, the fire axe): the fire axe cabinet and its subtypes (the mech removal tool cabinet, the jaws
// of recovery cabinet). A multitool unlocks it (or a cyborg), a locked one's glass is broken by hits (atom_break: the glass
// shatters, two shards) and two glass sheets repair it, a welder repairs the frame, the hand takes the axe and opens or closes
// the door. Gaps ST5: blob_act (a blob breaks it, dropping the axe) and attack_tk (telekinesis opens and shuts the door).
// The wall frames (/obj/item/wallframe/fireaxecabinet and its subtypes) and the directional
// helpers are var-only. The var block is generated (D111).
//
// GAP(types): tg's `var/obj/item/held_item` (:17) is generated as a Type (rules 3.2 V3: tg assigns typepaths to vars of that
// name elsewhere, the skeleton mobs); the cabinet keeps the held item in held_item_value.

// tg: code/game/objects/structures/fireaxe.dm:1
public partial class obj_structure_fireaxecabinet : obj_structure
{
    /// GAP(types): tg's `var/obj/item/held_item` (:17): the item we're holding.
    public obj_item held_item_value;

    // tg: code/game/objects/structures/fireaxe.dm:37
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(populate_contents))
            held_item_value = Dm.@new(item_path, this) as obj_item;
        update_appearance();
        if (Dm.truthy(mapload))
            Waiting.NoSleep(find_and_mount_on_atom());
        return dot;
    }

    // tg: code/game/objects/structures/fireaxe.dm:45
    public override object Destroy(float? force = null)
    {
        if (held_item_value != null)
            QDEL_NULL(ref held_item_value);
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/fireaxe.dm:50
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (iscyborg(user) || Dm.eq(tool.tool_behaviour, unlocking_tool_behavior))
        {
            await toggle_lock(user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_stack_sheet_glass)) && Dm.truthy(broken))
        {
            obj_item_stack_sheet_glass glass_stack = (obj_item_stack_sheet_glass)tool;
            if (Dm.lt(glass_stack.get_amount(), 2))
            {
                balloon_alert(user, I18n.T("need more glass!"));
                return ITEM_INTERACT_BLOCKING;
            }

            balloon_alert(user, I18n.T("repairing"));
            if (!Dm.truthy(await glass_stack.use_tool(this, user, 2 * SECONDS, 2)))
                return ITEM_INTERACT_BLOCKING;

            broken = FALSE;
            await repair_damage(max_integrity - Dm.num(get_integrity()));
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(open) || Dm.truthy(broken))
        {
            if (!istype(tool, item_path) || held_item_value != null)
            {
                if (!Dm.truthy(broken))
                {
                    toggle_open(); // Unsure why this is desired behaviour
                    return ITEM_INTERACT_SUCCESS;
                }
                return ITEM_INTERACT_BLOCKING;
            }

            if (HAS_TRAIT(tool, TRAIT_WIELDED))
            {
                balloon_alert(user, I18n.T("unwield it!"));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;

            held_item_value = tool;
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/objects/structures/fireaxe.dm:90
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode) || Dm.truthy(broken))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        if (Dm.eq(atom_integrity, max_integrity))
        {
            balloon_alert(user, I18n.T("already repaired!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(tool.tool_start_check(user, amount: 2)))
            return ITEM_INTERACT_BLOCKING;

        balloon_alert(user, I18n.T("repairing..."));
        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, volume: 50, amount: 2)))
            return ITEM_INTERACT_BLOCKING;

        await repair_damage(max_integrity - Dm.num(get_integrity()));
        update_appearance();
        balloon_alert(user, I18n.T("repaired"));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/fireaxe.dm:111
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (gone == held_item_value)
        {
            held_item_value = null;
            update_appearance();
        }
        return await base.Exited(gone, direction);
    }

    // tg: code/game/objects/structures/fireaxe.dm:117
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        object sw__1 = damage_type;
        if (Dm.eq(sw__1, BRUTE))
        {
            if (Dm.truthy(broken))
                playsound(loc, Dm.resource("sound/effects/hit_on_shattered_glass.ogg"), 90, TRUE);
            else
                playsound(loc, Dm.resource("sound/effects/glass/glasshit.ogg"), 90, TRUE);
        }
        else if (Dm.eq(sw__1, BURN))
            playsound(this.loc, Dm.resource("sound/items/tools/welder.ogg"), 100, TRUE);
        return null;
    }

    // tg: code/game/objects/structures/fireaxe.dm:127
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        if (Dm.truthy(open))
            return null;
        object dot = await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        if (Dm.truthy(dot))
            update_appearance();
        return dot;
    }

    // tg: code/game/objects/structures/fireaxe.dm:134
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        if (!Dm.truthy(broken))
        {
            update_appearance();
            broken = TRUE;
            playsound(this, Dm.resource("sound/effects/glass/glassbr3.ogg"), 100, TRUE);
            Dm.@new<obj_item_shard>(loc);
            Dm.@new<obj_item_shard>(loc);
        }
        return dot;
    }

    // tg: code/game/objects/structures/fireaxe.dm:143
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        if (held_item_value != null && loc != null)
            await held_item_value.forceMove(loc);
        Dm.@new<obj_item_wallframe_fireaxecabinet>(loc);
    }

    // tg: code/game/objects/structures/fireaxe.dm:148  (gaps ST5)
    public override async DmTask<object> blob_act(obj_structure_blob attacking_blob = null)
    {
        if (held_item_value != null)
            await held_item_value.forceMove(loc);
        qdel(this);
        return null;
    }

    // tg: code/game/objects/structures/fireaxe.dm:153
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if ((Dm.truthy(open) || Dm.truthy(broken)) && held_item_value != null)
        {
            await user.put_in_hands(held_item_value);
            // tg: code/game/objects/structures/fireaxe.dm:159
            add_fingerprint(user);
            update_appearance();
            return dot;
        }
        toggle_open(user);
        return dot;
    }

    // tg: code/game/objects/structures/fireaxe.dm:164
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        toggle_open(user);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/objects/structures/fireaxe.dm:168
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    // tg: code/game/objects/structures/fireaxe.dm:175  (gaps ST5)
    public override async DmTask<object> attack_tk(mob user = null)
    {
        object dot = COMPONENT_CANCEL_ATTACK_CHAIN;
        toggle_open(user);
        await DmTask.CompletedTask;
        return dot;
    }

    // tg: code/game/objects/structures/fireaxe.dm:179
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (held_item_value != null)
            Dm.append(ref dot, item_overlay);
        float hp_percent = Dm.num(Dm.div(atom_integrity, max_integrity)) * 100;

        if (Dm.truthy(open))
        {
            if (Dm.truthy(broken))
            {
                Dm.append(ref dot, "glass4_raised");
                return dot;
            }

            if (hp_percent <= 40)
                Dm.append(ref dot, "glass3_raised");
            else if (hp_percent <= 60)
                Dm.append(ref dot, "glass2_raised");
            else if (hp_percent <= 80)
                Dm.append(ref dot, "glass1_raised");
            else
                Dm.append(ref dot, "glass_raised");
            return dot;
        }

        if (Dm.truthy(broken))
            Dm.append(ref dot, "glass4");
        else
        {
            if (hp_percent <= 40)
                Dm.append(ref dot, "glass3");
            else if (hp_percent <= 60)
                Dm.append(ref dot, "glass2");
            else if (hp_percent <= 80)
                Dm.append(ref dot, "glass1");
            else
                Dm.append(ref dot, "glass");
        }

        Dm.append(ref dot, Dm.truthy(locked) ? "locked" : "unlocked");
        return dot;
    }

    // tg: code/game/objects/structures/fireaxe.dm:216
    public virtual async DmTask toggle_lock(mob user = null)
    {
        to_chat(user, span_notice(I18n.T("Resetting circuitry...")));
        playsound(this, Dm.resource("sound/machines/locktoggle.ogg"), 50, TRUE);
        if (Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
        {
            to_chat(user, span_notice(Dm.truthy(locked) ? I18n.T("You disable the locking modules.") : I18n.T("You re-enable the locking modules.")));
            locked = Dm.not(locked);
            update_appearance();
        }
    }

    // tg: code/game/objects/structures/fireaxe.dm:224
    public virtual void toggle_open(mob user = null)
    {
        if (Dm.truthy(locked))
        {
            balloon_alert(user, I18n.T("won't budge!"));
            return;
        }
        else
        {
            open = Dm.not(open);
            playsound(this, Dm.resource("sound/machines/click.ogg"), 30, TRUE);
            update_appearance();
            return;
        }
    }
}

// tg: code/game/objects/structures/fireaxe.dm:248
public partial class obj_structure_fireaxecabinet_mechremoval : obj_structure_fireaxecabinet
{
    // tg: code/game/objects/structures/fireaxe.dm:257
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        if (held_item_value != null && loc != null)
            await held_item_value.forceMove(loc);
        Dm.@new<obj_item_wallframe_fireaxecabinet_mechremoval>(loc);
    }
}

// tg: code/game/objects/structures/fireaxe.dm:273
public partial class obj_structure_fireaxecabinet_jawsofrecovery : obj_structure_fireaxecabinet
{
    // tg: code/game/objects/structures/fireaxe.dm:284
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        if (held_item_value != null && loc != null)
            await held_item_value.forceMove(loc);
        Dm.@new<obj_item_wallframe_fireaxecabinet_jawsofrecovery>(loc);
    }
}

// The AI's and cyborgs' clicks (silicons SI2, D326)
// tg: code/game/objects/structures/fireaxe.dm:171
public partial class obj_structure_fireaxecabinet : obj_structure
{
    // tg: code/game/objects/structures/fireaxe.dm:171
    public override async DmTask<object> attack_ai(mob user = null)
    {
        await toggle_lock(user);
        return null;
    }
}
