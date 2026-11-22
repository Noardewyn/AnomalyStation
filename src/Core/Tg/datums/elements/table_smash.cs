// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/table_smash.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps L1 (D249, D327): the whole file, the table slam: a pulled thing that passes tables is placed on the table; a pulled mob is
// placed gently (a 3.5 s do_after out of combat mode), slammed on it from an aggressive grab (knocked down, stamina and brute,
// TRAIT_PASSTABLE for the move) or has a limb smashed against it from a neck grab and up (the table takes damage too); a mob carried
// in a fireman's carry is placed or limb-smashed; a mob shoved into the table's tile is knocked onto it. The table's after_smash
// follows (the wood table may smash into bits). The var block is generated (D111).

/// Component which allows mobs to be smashed onto this surface like a wrestling move
// tg: code/datums/elements/table_smash.dm:2
public partial class datum_element_table_smash : datum_element
{
    // tg: code/datums/elements/table_smash.dm:10
    [DmParams("target", "gentle_push", "after_smash_proccall")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object gentle_push = Dm.arg(__extra, 0) ?? FALSE;
        object after_smash_proccall = Dm.arg(__extra, 1);
        object dot = await base.Attach(target, Dm.forward(__extra, gentle_push, after_smash_proccall));
        if (!isobj(target))
            return ELEMENT_INCOMPATIBLE;

        this.gentle_push = gentle_push;
        this.after_smash_proccall = after_smash_proccall;

        RegisterSignal(target, COMSIG_ATOM_ATTACK_HAND, nameof(on_interaction));
        RegisterSignal(target, COMSIG_ATOM_ITEM_INTERACTION, nameof(on_item_interaction));

        DmList loc_connections = TgState.Current.datum_element_table_smash__Attach__loc_connections ??= Dm.list(
            Dm.kv(COMSIG_LIVING_DISARM_COLLIDE, nameof(obj_structure.on_disarm_shoved_into)));
        await ((atom)target).AddElement(typeof(datum_element_connect_loc), loc_connections);
        return dot;
    }

    // tg: code/datums/elements/table_smash.dm:26
    [DmParams("source")]
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, Dm.list(COMSIG_ATOM_ATTACK_HAND, COMSIG_ATOM_ITEM_INTERACTION));
        return dot;
    }

    /// Called when someone clicks on our surface
    // tg: code/datums/elements/table_smash.dm:31
    [SignalHandler]
    public virtual object on_interaction(datum source = null, mob user = null)
    {
        var source_obj = (obj)source;
        if (!Dm.truthy(Waiting.NoSleep(source_obj.Adjacent(user))) || user.pulling == null)
            return null;

        if (!isliving(user.pulling))
        {
            if (!Dm.truthy(Dm.bitand(user.pulling.pass_flags, PASSTABLE)))
                return null;

            Waiting.NoSleep(user.Move_Pulled(source_obj));

            if (user.pulling.loc == source_obj.loc)
            {
                user.visible_message(span_notice(I18n.T("[] places [] onto [].", user, user.pulling, source_obj)),
                    span_notice(I18n.T("You place [] onto [].", user.pulling, source_obj)));
                Waiting.NoSleep(user.stop_pulling());
            }

            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }

        var pushed_mob = (mob_living)user.pulling;
        if (pushed_mob.buckled != null)
        {
            if (pushed_mob.buckled == source_obj)
            {
                //Already buckled to the object, you probably meant to unbuckle them
                return null;
            }

            to_chat(user, span_warning(I18n.T("[] is buckled to []!", pushed_mob, pushed_mob.buckled)));
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }

        Waiting.Detach(perform_table_smash(source_obj as obj_structure_table, (mob_living)user)); // INVOKE_ASYNC
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    /// We have a mob being pressed onto the table, but how strongly?
    // tg: code/datums/elements/table_smash.dm:61
    public virtual async DmTask perform_table_smash(obj_structure_table table = null, mob_living user = null)
    {
        var pushed_mob = (mob_living)user.pulling;
        if (Dm.truthy(user.combat_mode))
        {
            float grab = Dm.num(user.grab_state);
            if (grab == GRAB_PASSIVE)
            {
                to_chat(user, span_warning(I18n.T("You need a better grip to do that!")));
                return;
            }
            else if (grab == GRAB_AGGRESSIVE)
            {
                if (Dm.truthy(gentle_push))
                    await tableplace(user, pushed_mob, table);
                else
                    await tablepush(user, pushed_mob, table);
            }
            else if (grab >= GRAB_NECK && grab <= GRAB_KILL)
                await tablelimbsmash(user, pushed_mob, table);
        }
        else
        {
            pushed_mob.visible_message(span_notice(I18n.T("[] begins to place [] onto []...", user, pushed_mob, table)),
                span_userdanger(I18n.T("[] begins to place [] onto []...", user, pushed_mob, table)));
            if (Dm.truthy(await do_after(user, 3.5f * SECONDS, target: pushed_mob)))
                await tableplace(user, pushed_mob, table);
            else
                return;
        }

        await user.stop_pulling();
    }

    /// Called when someone clicks on our surface with an item
    // tg: code/datums/elements/table_smash.dm:84
    [SignalHandler]
    public virtual object on_item_interaction(obj_structure_table table = null, mob_living user = null, obj_item item = null, DmList modifiers = null)
    {
        if (item is not obj_item_riding_offhand riding_item)
            return NONE;

        mob_living carried_mob = riding_item.rider;
        if (carried_mob == user) //Piggyback user.
            return NONE;

        Waiting.Detach(riding_offhand_act(user, riding_item, table)); // INVOKE_ASYNC
        return ITEM_INTERACT_BLOCKING;
    }

    /// Called when someone clicks on our surface using a fireman's carry
    // tg: code/datums/elements/table_smash.dm:98
    public virtual async DmTask<object> riding_offhand_act(mob_living user = null, obj_item_riding_offhand riding_item = null, obj_structure_table table = null)
    {
        mob_living carried_mob = riding_item.rider;
        if (Dm.truthy(user.combat_mode))
        {
            await user.unbuckle_mob(carried_mob);
            await tablelimbsmash(user, carried_mob);
            return ITEM_INTERACT_SUCCESS;
        }

        float tableplace_delay = 3.5f * SECONDS;
        string skills_space = "";
        if (HAS_TRAIT(user, TRAIT_QUICKER_CARRY))
        {
            tableplace_delay = 2 * SECONDS;
            skills_space = I18n.T(" expertly");
        }
        else if (HAS_TRAIT(user, TRAIT_QUICK_CARRY))
        {
            tableplace_delay = 2.75f * SECONDS;
            skills_space = I18n.T(" quickly");
        }

        if (user.get_organ_slot(ORGAN_SLOT_SPINE) is obj_item_organ_cyberimp_chest_spine potential_spine)
            tableplace_delay *= Dm.num(potential_spine.athletics_boost_multiplier);

        carried_mob.visible_message(span_notice(I18n.T("[] begins to[] place [] onto []...", user, skills_space, carried_mob, table)),
            span_userdanger(I18n.T("[] begins to[] place [] onto []...", user, skills_space, carried_mob, table)));
        if (!Dm.truthy(await do_after(user, tableplace_delay, target: carried_mob)))
            return ITEM_INTERACT_BLOCKING;
        await user.unbuckle_mob(carried_mob);
        await tableplace(user, carried_mob, table);
        return ITEM_INTERACT_SUCCESS;
    }

    /// Gently place the mob onto the table
    // tg: code/datums/elements/table_smash.dm:126
    public virtual async DmTask tableplace(mob_living user = null, mob_living pushed_mob = null, obj_structure_table table = null)
    {
        await pushed_mob.forceMove(table.loc);
        await pushed_mob.set_resting(TRUE, TRUE);
        pushed_mob.visible_message(span_notice(I18n.T("[] places [] onto [].", user, pushed_mob, table)),
            span_notice(I18n.T("[] places [] onto [].", user, pushed_mob, table)));
        log_combat(user, pushed_mob, "placed", null, Dm.text("onto []", table));
    }

    /// Aggressively smash the mob onto the table
    // tg: code/datums/elements/table_smash.dm:133
    public virtual async DmTask tablepush(mob_living user = null, mob_living pushed_mob = null, obj_structure_table table = null)
    {
        if (HAS_TRAIT(user, TRAIT_PACIFISM))
        {
            to_chat(user, span_danger(I18n.T("Throwing [] onto the table might hurt them!", pushed_mob)));
            return;
        }

        string passtable_key = Dm.str(REF(user));
        ADD_TRAIT(pushed_mob, TRAIT_PASSTABLE, passtable_key);
        foreach (obj obj in Dm.each<obj>(user.loc.contents))
        {
            if (!Dm.truthy(await obj.CanAllowThrough(pushed_mob)))
                return;
        }

        await pushed_mob.Move(table.loc);
        REMOVE_TRAIT(pushed_mob, TRAIT_PASSTABLE, passtable_key);
        if (pushed_mob.loc != table.loc) //Something prevented the tabling
            return;

        playsound(pushed_mob, Dm.resource("sound/effects/tableslam.ogg"), 90, TRUE);
        pushed_mob.visible_message(span_danger(I18n.T("[] slams [] onto \\the []!", user, pushed_mob, table)),
            span_userdanger(I18n.T("[] slams you onto \\the []!", user, table)));
        log_combat(user, pushed_mob, "tabled", null, Dm.text("onto []", table));
        await pushed_mob.add_mood_event("table", typeof(datum_mood_event_table));
        if (Dm.truthy(user._listen_lookup?[COMSIG_LIVING_TABLE_SLAMMING]))
            user._SendSignal(COMSIG_LIVING_TABLE_SLAMMING, Dm.list(user, pushed_mob, table));
        if (Dm.truthy(after_smash_proccall))
            await Dm.Call(table, Dm.str(after_smash_proccall), pushed_mob);

        await pushed_mob.Knockdown(3 * SECONDS);
        await pushed_mob.apply_damage(40, STAMINA);
        await pushed_mob.apply_damage(10, BRUTE);
    }

    /// Even more aggressively smash a single part of a mob onto the table
    // tg: code/datums/elements/table_smash.dm:162
    public virtual async DmTask tablelimbsmash(mob_living user = null, mob_living pushed_mob = null, obj_structure_table table = null)
    {
        var banged_limb = (pushed_mob.get_bodypart(user.zone_selected) ?? pushed_mob.get_bodypart(BODY_ZONE_HEAD)) as obj_item_bodypart;
        playsound(pushed_mob, Dm.resource("sound/effects/bang.ogg"), 90, TRUE);
        pushed_mob.visible_message(span_danger(I18n.T("[] smashes []'s [] against \\the []!", user, pushed_mob, banged_limb.plaintext_zone, table)),
            span_userdanger(I18n.T("[] smashes your [] against \\the []", user, banged_limb.plaintext_zone, table)));
        log_combat(user, pushed_mob, "head slammed", null, Dm.text("against []", table));
        await pushed_mob.add_mood_event("table", typeof(datum_mood_event_table_limbsmash), banged_limb);
        await table.take_damage(50);
        if (Dm.truthy(user._listen_lookup?[COMSIG_LIVING_TABLE_LIMB_SLAMMING]))
            user._SendSignal(COMSIG_LIVING_TABLE_LIMB_SLAMMING, Dm.list(user, pushed_mob, table));
        if (Dm.truthy(after_smash_proccall))
            await Dm.Call(table, Dm.str(after_smash_proccall), pushed_mob);

        await pushed_mob.Knockdown(3 * SECONDS);
        await pushed_mob.apply_damage(60, STAMINA);
        await pushed_mob.apply_damage(30, BRUTE, banged_limb, wound_bonus: HAS_TRAIT(user, TRAIT_HULK) ? 20 : 0);
    }
}

// tg: code/datums/elements/table_smash.dm:184-202 (interleaved types, rules 1.9)
public partial class obj_structure : obj
{
    /// Called when someone is shoved into our tile
    // tg: code/datums/elements/table_smash.dm:185
    [SignalHandler]
    public virtual object on_disarm_shoved_into(datum source = null, mob_living shover = null, mob_living target = null, object shove_flags = null, obj_item weapon = null)
    {
        if (Dm.truthy(Dm.bitand(shove_flags, SHOVE_KNOCKDOWN_BLOCKED)) || !Dm.truthy(Dm.bitand(shove_flags, SHOVE_BLOCKED)))
            return null;
        Waiting.NoSleep(target.Knockdown(SHOVE_KNOCKDOWN_TABLE, daze_amount: 3 * SECONDS));
        target.visible_message(span_danger(I18n.T("[] shoves [] onto \\the []!", shover.name, target.name, this)),
            span_userdanger(I18n.T("You're shoved onto \\the [] by []!", this, shover.name)), span_hear(I18n.T("You hear aggressive shuffling followed by a loud thud!")), COMBAT_MESSAGE_RANGE, shover);
        to_chat(shover, span_danger(I18n.T("You shove [] onto \\the []!", target.name, this)));
        Waiting.Detach(target.throw_at(this, 1, 1, null, FALSE)); //1 speed throws with no spin are basically just forcemoves with a hard collision check
        log_combat(shover, target, "shoved", Dm.text("onto [] (table)[]", this, weapon != null ? Dm.text(" with []", weapon) : ""));
        Waiting.Detach(after_smash(target));
        return COMSIG_LIVING_SHOVE_HANDLED;
    }

    /// Called after someone is harmfully smashed onto us
    // tg: code/datums/elements/table_smash.dm:201
    public virtual async DmTask after_smash(mob_living smashed_onto = null)
    {
        return; // This is mostly for our children
    }
}

public sealed partial class TgState
{
    // tg: code/datums/elements/table_smash.dm:21  Attach's var/static/list/loc_connections (a proc static, rules 10.6)
    internal DmList datum_element_table_smash__Attach__loc_connections;
}
