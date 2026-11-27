// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/spawner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the monster nests (the base with its GPS holotag, the syndicate, skeleton, clown and mining nests, the netherworld
// link and the sentient spawner). Not here: the cult's eldritch gateway (`sentient/proteon_spawner`, :253-297), which waits for
// the cult (antagonist-only, D333). The var blocks are generated.

// tg: code/game/objects/structures/spawner.dm:1
public partial class obj_structure_spawner : obj_structure
{
    // tg: code/game/objects/structures/spawner.dm:21 (var/static/list/scanner_types)
    public static DmList scanner_types => TgState.Current.obj_structure_spawner__scanner_types ??= Dm.list(typeof(obj_item_mining_scanner), typeof(obj_item_t_scanner_adv_mining_scanner));

    // tg: code/game/objects/structures/spawner.dm:33
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!Dm.truthy(scanner_taggable))
            return dot;
        if (Dm.truthy(gps_tagged))
            Dm.append(ref dot, span_notice(I18n.T("A holotag's been attached, projecting \"<b>[]</b>\".", assigned_tag)));
        else
            Dm.append(ref dot, span_notice(I18n.T("It looks like you could probably scan and tag it with a <b>[]</b>.", scanner_descriptor)));
        return dot;
    }

    // tg: code/game/objects/structures/spawner.dm:42
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(scanner_taggable) && Dm.truthy(is_type_in_list(tool, scanner_types)))
        {
            await gps_tag(user);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    /// Tag the spawner, prefixing its GPS entry with an identifier - or giving it one, if nonexistent.
    // tg: code/game/objects/structures/spawner.dm:50
    public virtual async DmTask gps_tag(mob user = null)
    {
        if (Dm.truthy(gps_tagged))
        {
            to_chat(user, span_warning(I18n.T("[] already has a holotag attached!", this)));
            return;
        }
        to_chat(user, span_notice(I18n.T("You affix a holotag to [].", this)));
        playsound(this, Dm.resource("sound/machines/beep/twobeep.ogg"), 100);
        gps_tagged = TRUE;
        assigned_tag = Dm.text("\\[[]-[]\\] ", mob_gps_id, rand(100, 999)) + spawner_gps_id;
        datum_component_gps our_gps = GetComponent(typeof(datum_component_gps)) as datum_component_gps;
        if (our_gps != null)
        {
            our_gps.gpstag = assigned_tag as string;
            return;
        }
        await AddComponent(typeof(datum_component_gps), assigned_tag);
    }

    // tg: code/game/objects/structures/spawner.dm:64
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(
            spawner_type,
            Dm.named("spawn_types", mob_types),
            Dm.named("spawn_time", spawn_time),
            Dm.named("max_spawned", max_mobs),
            Dm.named("faction", faction),
            Dm.named("spawn_text", spawn_text),
            Dm.named("spawn_callback", Dm.@new<datum_callback>(this, nameof(on_mob_spawn))),
            Dm.named("initial_spawn_delay", Dm.truthy(mapload) ? FALSE : TRUE)));
        return dot;
    }

    // tg: code/game/objects/structures/spawner.dm:77
    public override async DmTask<object> attack_animal(mob user = null, DmList modifiers = null)
    {
        if (Dm.truthy(faction_check_atom(user)) && user.client == null)
            return null;
        return await base.attack_animal(user, modifiers);
    }

    // tg: code/game/objects/structures/spawner.dm:82
    public virtual void on_mob_spawn(atom created_atom = null)
    {
        return;
    }
}

// tg: code/game/objects/structures/spawner.dm:181
public partial class obj_structure_spawner_nether : obj_structure_spawner
{
    // tg: code/game/objects/structures/spawner.dm:200
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        START_PROCESSING(SSprocessing, this);
        return dot;
    }

    // tg: code/game/objects/structures/spawner.dm:204
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, I18n.T("A direct link to another dimension full of creatures not very happy to see you. []", span_warning(I18n.T("Entering the link would be a very bad idea."))));
        return dot;
    }

    // tg: code/game/objects/structures/spawner.dm:208
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        user.visible_message(
            span_warning(I18n.T("[] is violently pulled into the link!", user)),
            span_userdanger(I18n.T("Touching the portal, you are quickly pulled through into a world of unimaginable horror!")));
        contents.Add(user);
        return dot;
    }

    // tg: code/game/objects/structures/spawner.dm:216
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        foreach (mob_living living_mob in Dm.each<mob_living>(contents))
        {
            playsound(this, Dm.resource("sound/effects/magic/demon_consume.ogg"), 50, TRUE);
            Waiting.NoSleep(living_mob.adjust_brute_loss(60 * Dm.num(seconds_per_tick)));
            Dm.@new<obj_effect_gibspawner_generic>(get_turf(living_mob), living_mob);
            if (living_mob.stat == DEAD)
            {
                mob_living_basic_blankbody newmob = Dm.@new<mob_living_basic_blankbody>(loc);
                newmob.name = Dm.text("[]", living_mob);
                newmob.desc = I18n.T("It's [], but [] flesh has an ashy texture, and [] face is featureless save an eerie smile.", living_mob, living_mob.p_their(), living_mob.p_their());
                visible_message(span_warning(I18n.T("[] reemerges from the link!", living_mob)));
                qdel(living_mob);
            }
        }
        return null;
    }
}

// tg: code/game/objects/structures/spawner.dm:228
public partial class obj_structure_spawner_sentient : obj_structure_spawner
{
    // tg: code/game/objects/structures/spawner.dm:232
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        notify_ghosts(
            I18n.T("A [] has been created in \\the []!", name, get_area(this)),
            source: this,
            header: "Sentient Spawner Created",
            notify_flags: NOTIFY_CATEGORY_NOFLASH);
        return dot;
    }

    // tg: code/game/objects/structures/spawner.dm:241
    public override void on_mob_spawn(atom created_atom = null)
    {
        Waiting.NoSleep(created_atom.AddComponent(
            typeof(datum_component_ghost_direct_control),
            Dm.named("role_name", role_name),
            Dm.named("assumed_control_message", assumed_control_message),
            Dm.named("after_assumed_control", Dm.@new<datum_callback>(this, nameof(became_player_controlled)))));
    }

    // tg: code/game/objects/structures/spawner.dm:249
    public virtual void became_player_controlled(mob proteon = null)
    {
        return;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/spawner.dm:21 (var/static/list/scanner_types)
    internal DmList obj_structure_spawner__scanner_types;
}
