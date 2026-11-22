// Translated from tgstation (snapshot 1d99bd7f): code/datums/spawners_menu.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI7, D326: the ghost roles' menu, which the drone shells list in). The var block is generated (D111).

// tg: code/datums/spawners_menu.dm:1
public partial class datum_spawners_menu : datum
{
    // tg: code/datums/spawners_menu.dm:4
    public override async DmTask New(params object[] __extra)
    {
        mob_dead_observer new_owner = Dm.arg(__extra, 0) as mob_dead_observer;
        if (new_owner == null)
            qdel(this);
        owner = new_owner;
        await DmTask.CompletedTask;
    }

    // tg: code/datums/spawners_menu.dm:9
    public override object Destroy(float? force = null)
    {
        owner = null;
        return base.Destroy(force);
    }

    // tg: code/datums/spawners_menu.dm:13
    public override object ui_state(mob user = null) => GLOB.observer_state;

    // tg: code/datums/spawners_menu.dm:16
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "SpawnersMenu");
            await ui.open();
        }
        return null;
    }

    // tg: code/datums/spawners_menu.dm:22
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        DmList spawners = Dm.list();
        data["spawners"] = spawners;
        foreach (object spawner in Dm.each(GLOB.mob_spawners))
        {
            DmList @this = Dm.list();
            @this["name"] = spawner;
            @this["you_are_text"] = "";
            @this["flavor_text"] = "";
            @this["important_warning"] = "";
            @this["amount_left"] = 0;
            foreach (object spawner_obj in Dm.each(GLOB.mob_spawners[spawner]))
            {
                obj_effect_mob_spawn_ghost_role mob_spawner = spawner_obj as obj_effect_mob_spawn_ghost_role;
                if (!Dm.truthy(@this["desc"]))
                {
                    if (spawner_obj is obj_effect_mob_spawn)
                    {
                        if (!Dm.truthy(mob_spawner.allow_spawn(user, silent: TRUE)))
                            continue;
                        @this["you_are_text"] = mob_spawner.you_are_text;
                        @this["flavor_text"] = mob_spawner.flavour_text;
                        @this["important_text"] = mob_spawner.important_text;
                    }
                    else
                    {
                        obj @object = spawner_obj as obj;
                        @this["desc"] = @object.desc;
                    }
                }
                @this["amount_left"] = Dm.num(@this["amount_left"]) + mob_spawner.uses;
                @this["infinite"] = Dm.num(@this["infinite"]) + mob_spawner.infinite_use;
            }
            if (Dm.num(@this["amount_left"]) > 0 || Dm.truthy(@this["infinite"]))
                spawners.Add(Dm.list(@this));
        }
        foreach (object mob_type in Dm.each(GLOB.joinable_mobs))
        {
            DmList @this = Dm.list();
            @this["name"] = mob_type;
            @this["amount_left"] = 0;
            foreach (mob joinable_mob in Dm.each_cast<mob>(GLOB.joinable_mobs[mob_type]))
            {
                if (Dm.truthy(joinable_mob._listen_lookup?[COMSIG_LIVING_GHOSTROLE_INFO]))
                    joinable_mob._SendSignal(COMSIG_LIVING_GHOSTROLE_INFO, Dm.list(joinable_mob, @this));
                @this["amount_left"] = Dm.num(@this["amount_left"]) + 1;
                if (!Dm.truthy(@this["desc"]))
                    @this["desc"] = Dm.initial(joinable_mob.GetType(), (mob m) => m.desc);
            }

            if (Dm.num(@this["amount_left"]) > 0)
                spawners.Add(Dm.list(@this));
        }
        return data;
    }

    // tg: code/datums/spawners_menu.dm:60
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        object group_name = @params?["name"];
        if (!Dm.truthy(group_name))
            return null;

        DmList spawnerlist = Dm.list();

        if (Dm.@in(group_name, GLOB.mob_spawners))
        {
            spawnerlist = (GLOB.mob_spawners[group_name] as DmList)?.Copy();
            if (!Dm.truthy(length(spawnerlist)))
                return null;
            foreach (obj_effect_mob_spawn_ghost_role current_spawner in Dm.each_cast<obj_effect_mob_spawn_ghost_role>(spawnerlist.Copy()))
            {
                if (!Dm.truthy(current_spawner.allow_spawn(usr, silent: TRUE)))
                    spawnerlist.Remove(current_spawner);
            }
        }
        else if (Dm.@in(group_name, GLOB.joinable_mobs))
            spawnerlist = GLOB.joinable_mobs[group_name] as DmList;

        if (!Dm.truthy(length(spawnerlist)))
            return null;
        atom mob_spawner = pick(spawnerlist) as atom;
        if (!Dm.truthy(await SSpoints_of_interest.is_valid_poi(mob_spawner)))
            return null;

        switch (Dm.str(action))
        {
            case "jump":
                if (mob_spawner != null)
                {
                    await owner.forceMove(get_turf(mob_spawner));
                    return TRUE;
                }
                break;
            case "spawn":
                if (mob_spawner != null)
                {
                    await owner.ManualFollow(mob_spawner as atom_movable);
                    ui.close();
                    await mob_spawner.attack_ghost(owner);
                    return TRUE;
                }
                break;
        }
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/ghost.dm:9
public partial class atom_movable_screen_ghost_spawners_menu : atom_movable_screen_ghost
{
    // tg: code/_onclick/hud/screen_objects/ghost.dm:14
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        mob_dead_observer observer = usr as mob_dead_observer;
        observer.open_spawners_menu();
        await DmTask.CompletedTask;
        return null;
    }
}

public partial class mob_dead_observer
{
    // tg: code/modules/mob/dead/observer/observer.dm:967  GAME_VERB_PROC(/mob/dead/observer, open_spawners_menu, "Spawners Menu", null)
    // (__DEFINES/verb.dm:96-114): the body proc; the verb proc (open_spawners_menu) is below.
    public virtual async DmTask __gvb_open_spawners_menu(DmList structured_args = null)
    {
        spawners_menu ??= Dm.@new<datum_spawners_menu>(this);

        await spawners_menu.ui_interact(this);
    }

    // tg: code/modules/mob/dead/observer/observer.dm:967  the verb proc's INVOKE_ASYNC(SSverbs, invoke_verb, ...), as mob_sleep's
    // (living.cs): a detached call of the body; a caller without a client does nothing; the verb has no arguments.
    public virtual void open_spawners_menu(params object[] __extra)
    {
        mob resolved_caller = usr ?? this;
        if (resolved_caller.client == null)
            return;
        Waiting.Detach(__gvb_open_spawners_menu(Dm.list()));
    }
}
