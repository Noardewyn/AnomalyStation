// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/lavaland/mining_loot/equipment.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// Rod of Asclepius
// tg: code/modules/mining/lavaland/mining_loot/equipment.dm:2
public partial class obj_item_rod_of_asclepius : obj_item
{
    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:13 (var/static/list/oath_lines)
    public static DmList oath_lines => TgState.Current.obj_item_rod_of_asclepius__oath_lines ??= Dm.list(
        "I swear to fulfill, to the best of my ability and judgment, this covenant:",
        "I will apply, for the benefit of the sick, all measures that are required, avoiding those twin traps of overtreatment and therapeutic nihilism.",
        "I will remember that I remain a member of society, with special obligations to all my fellow human beings, those sound of mind and body as well as the infirm.",
        "If I do not violate this oath, may I enjoy life and art, respected while I live and remembered with affection thereafter. May I always act so as to preserve the finest traditions of my calling and may I long experience the joy of healing those who seek my help.");

    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:23
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        desc = Dm.truthy(activated) ? I18n.T("A short wooden rod with a mystical snake inseparably gripping itself and the rod to your forearm. It flows with a healing energy that disperses amongst yourself and those around you.") : Dm.initial(this, (atom p) => p.desc);
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:27
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("asclepius_[]", Dm.truthy(activated) ? "active" : "dormant");
        inhand_icon_state = icon_state;
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:31
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        object dot = await base.vv_edit_var(var_name, var_value);
        if (Dm.eq(var_name, nameof(activated)) && Dm.truthy(activated))
            activated__proc();
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:36
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(activated))
            return null;
        if (!iscarbon(user))
        {
            to_chat(user, span_warning(I18n.T("The snake carving seems to come alive, if only for a moment, before returning to its dormant state, almost as if it finds you incapable of holding its oath.")));
            return null;
        }
        mob_living_carbon itemUser = user as mob_living_carbon;
        if (Dm.truthy(itemUser.has_status_effect(typeof(datum_status_effect_hippocratic_oath))))
        {
            to_chat(user, span_warning(I18n.T("You can't possibly handle the responsibility of more than one rod!")));
            return null;
        }

        // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:47 (var/static/failText)
        string failText = TgState.Current.obj_item_rod_of_asclepius__attack_self__failText ??= span_warning(I18n.T("The snake seems unsatisfied with your incomplete oath and returns to its previous place on the rod, returning to its dormant, wooden state. You must stand still while completing your oath!"));
        to_chat(itemUser, span_notice(I18n.T("The wooden snake that was carved into the rod seems to suddenly come alive and begins to slither down your arm! The compulsion to help others grows abnormally strong...")));
        foreach (object oath_line in Dm.each(oath_lines))
        {
            if (Dm.truthy(await do_after(itemUser, 3 * SECONDS, target: itemUser)))
            {
                await itemUser.say(oath_line, forced: "hippocratic oath");
                continue;
            }

            balloon_alert(itemUser, "interrupted!");
            to_chat(itemUser, failText);
            return null;
        }

        await apply_oath(itemUser);
        return null;
    }

    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:60
    public virtual async DmTask apply_oath(mob_living_carbon user = null)
    {
        to_chat(user, span_notice(I18n.T("The snake, satisfied with your oath, attaches itself and the rod to your forearm with an inseparable grip. Your thoughts seem to only revolve around the core idea of helping others, and harm is nothing more than a distant, wicked memory...")));
        datum_status_effect_hippocratic_oath effect = await user.apply_status_effect(typeof(datum_status_effect_hippocratic_oath)) as datum_status_effect_hippocratic_oath;
        effect.hand = user.get_held_index_of_item(this);
        activated__proc();
    }

    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:66
    public virtual void activated__proc()
    {
        item_flags = Dm.bitor(DROPDEL, ABSTRACT);
        ADD_TRAIT(this, TRAIT_NODROP, CURSED_ITEM_TRAIT(type));
        activated = TRUE;
        update_appearance();
    }
}

// Red/Blue Cubes

// tg: code/modules/mining/lavaland/mining_loot/equipment.dm:74
public partial class obj_item_warp_cube : obj_item
{
    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:83
    public override object Destroy(float? force = null)
    {
        if (!QDELETED(linked))
            qdel(linked);
        linked = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:89
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        turf current_location = get_turf(user);
        if (linked == null || get_turf(linked) == null || !Dm.truthy(check_teleport_valid(this, current_location)))
        {
            to_chat(user, span_warning(I18n.T("[] fizzles uselessly.", this)));
            return null;
        }
        if (Dm.truthy(teleporting))
            return null;
        teleporting = TRUE;
        linked.teleporting = TRUE;
        turf T = get_turf(this);
        Dm.@new<obj_effect_temp_visual_warp_cube>(T, user, teleport_color, TRUE);
        SSblackbox?.record_feedback("tally", "warp_cube", 1, type);
        Dm.@new<obj_effect_temp_visual_warp_cube>(get_turf(linked), user, linked.teleport_color, FALSE);
        obj_effect_warp_cube link_holder = Dm.@new<obj_effect_warp_cube>(T);
        if (user.buckled is atom_movable buckled_to)
            await buckled_to.unbuckle_mob(user as mob_living, TRUE);
        await user.forceMove(link_holder); //mess around with loc so the user can't wander around
        await sleep(0.25f * SECONDS);
        if (QDELETED(user))
        {
            qdel(link_holder);
            return null;
        }
        if (QDELETED(linked) || get_turf(linked) == null)
        {
            await user.forceMove(get_turf(link_holder));
            qdel(link_holder);
            return null;
        }
        await link_holder.forceMove(get_turf(linked));
        await sleep(0.25f * SECONDS);
        if (QDELETED(user))
        {
            qdel(link_holder);
            return null;
        }
        teleporting = FALSE;
        if (!QDELETED(linked))
            linked.teleporting = FALSE;
        await user.forceMove(get_turf(link_holder));
        qdel(link_holder);
        return null;
    }
}

// tg: code/modules/mining/lavaland/mining_loot/equipment.dm:124
public partial class obj_item_warp_cube_red : obj_item_warp_cube
{
    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:130
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (linked == null)
        {
            obj_item_warp_cube blue = Dm.@new<obj_item_warp_cube>(this.loc);
            linked = blue;
            blue.linked = this;
        }
        return dot;
    }
}

// Immortality Talisman

// tg: code/modules/mining/lavaland/mining_loot/equipment.dm:143
public partial class obj_item_immortality_talisman : obj_item
{
    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:152
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_anti_magic), ALL));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:159
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (cooldown < world.time)
        {
            SSblackbox?.record_feedback("amount", "immortality_talisman_uses", 1);
            cooldown = world.time + 600;
            Dm.@new<obj_effect_immortality_talisman>(get_turf(user), user);
        }
        else
            to_chat(user, span_warning(I18n.T("[] is not ready yet!", this)));
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/mining/lavaland/mining_loot/equipment.dm:167
public partial class obj_effect_immortality_talisman : obj_effect
{
    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:176
    [DmParams("mapload", "new_user")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        mob new_user = Dm.arg(__extra, 0) as mob;
        object dot = base.Initialize(mapload, __extra);
        if (new_user != null)
            Waiting.NoSleep(vanish(new_user));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:181
    public override object Destroy(float? force = null)
    {
        // If we have a mob, we need to free it before cleanup
        // This is a safety to prevent nuking a human, not so much a good pattern in general
        Waiting.NoSleep(unvanish());
        return base.Destroy(force);
    }

    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:187
    public virtual async DmTask unvanish()
    {
        mob user = user_ref?.resolve() as mob;
        user_ref = null;

        if (user == null)
            return;

        user.remove_traits(Dm.list(TRAIT_GODMODE, TRAIT_NO_TRANSFORM), REF(this));
        await user.forceMove(get_turf(this));
        user.visible_message(span_danger(I18n.T("[] pops back into reality!", user)));
    }

    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:198
    public virtual async DmTask vanish(mob user = null)
    {
        user.visible_message(span_danger(I18n.T("[] [], leaving a hole in [] place!", user, vanish_description, user.p_their())));

        desc = I18n.T("It's shaped an awful lot like [].", user.name);
        await setDir(user.dir);

        await user.forceMove(this);
        user.add_traits(Dm.list(TRAIT_GODMODE, TRAIT_NO_TRANSFORM), REF(this));

        user_ref = WEAKREF(user) as datum_weakref;

        _addtimer(Dm.@new<datum_callback>(this, nameof(dissipate)), 10 * SECONDS, file: "code/modules/mining/lavaland/mining_loot/equipment.dm", line: 209);
    }

    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:211
    public virtual void dissipate()
    {
        qdel(this);
    }

    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:214
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:217
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        // Won't really come into play since our mob has TRAIT_NO_TRANSFORM and cannot move,
        // but regardless block all relayed moves, because no, you cannot move in the void.
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:222
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        await DmTask.CompletedTask;
        return null;
    }
}

// Shared Bag

// tg: code/modules/mining/lavaland/mining_loot/equipment.dm:239
public partial class obj_item_shared_storage_red : obj_item_shared_storage
{
    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:241
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(create_storage(max_total_storage: 15, max_slots: 21));

        Dm.@new<obj_item_shared_storage_blue>(drop_location(), this);
        return dot;
    }
}

// tg: code/modules/mining/lavaland/mining_loot/equipment.dm:248
public partial class obj_item_shared_storage_blue : obj_item_shared_storage
{
    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:248
    [DmParams("mapload", "master")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object master = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        if (!isatom(master))
            return INITIALIZE_HINT_QDEL;
        Waiting.NoSleep(create_storage(max_total_storage: 15, max_slots: 21));

        Waiting.NoSleep(atom_storage.set_real_location(master as atom));
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:47 (var/static/failText)
    internal string obj_item_rod_of_asclepius__attack_self__failText;

    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:13 (var/static/list/oath_lines)
    internal DmList obj_item_rod_of_asclepius__oath_lines;
}
