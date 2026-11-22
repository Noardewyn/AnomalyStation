// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/decals/turfdecal/weakpoint.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign G2 (D316): the weakpoint cracks the earthcracker leaves: an explosion strong enough cracks the floor
// along a meandering chain and spawns new cracks; a welder or duct tape seals one. The screentip and the admin message are
// rows.

// tg: code/game/objects/effects/decals/turfdecal/weakpoint.dm:6  GLOBAL_LIST_EMPTY(weakpoints_spawned)
public partial class datum_controller_global_vars
{
    // tg: code/game/objects/effects/decals/turfdecal/weakpoint.dm:6
    public DmList weakpoints_spawned;

    // tg: code/game/objects/effects/decals/turfdecal/weakpoint.dm:6  (GLOBAL_MANAGED(weakpoints_spawned, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalweakpoints_spawned()
    {
        weakpoints_spawned = Dm.list();
        Dm.append(ref gvars_datum_init_order, "weakpoints_spawned");
    }
}

// tg: code/game/objects/effects/decals/turfdecal/weakpoint.dm:8
public partial class obj_effect_weakpoint : obj_effect
{
    // tg: code/game/objects/effects/decals/turfdecal/weakpoint.dm:1
    private const float CRACK_PROPAGATION_DELAY = 0.1f * SECONDS;
    private const float CRACK_TURN_CHANCE = 50;
    private const float CRACK_DELAY_CHANCE = 33;
    private const float CRACK_LENGTH_DEFAULT = 8;

    /// These turfs are things we don't want to spawn new cracks onto.
    // tg: code/game/objects/effects/decals/turfdecal/weakpoint.dm:30  var/static/list/skip_turfs (rules 10.6)
    private static DmList skip_turfs => TgState.Current.obj_effect_weakpoint__skip_turfs ??= typecacheof(Dm.list(
        typeof(turf_open_space),
        typeof(turf_open_misc_asteroid),
        typeof(turf_open_misc_snow))) as DmList;

    // tg: code/game/objects/effects/decals/turfdecal/weakpoint.dm:37
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        GLOB.weakpoints_spawned.Add(this);
        alpha = 0;
        Waiting.NoSleep(AddElement(typeof(datum_element_undertile), TRAIT_T_RAY_VISIBLE, INVISIBILITY_OBSERVER, Dm.named("use_anchor", TRUE)));
        RegisterSignal(this, COMSIG_TURF_CHANGE, nameof(turf_changed));
        register_context();
        animate(this, Dm.named("alpha", 255), Dm.named("time", 0.3f * SECONDS));
        return dot;
    }

    // tg: code/game/objects/effects/decals/turfdecal/weakpoint.dm:46
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        object dot = base.ex_act(severity, target, __extra);
        if (Dm.num(severity) < required_strength)
        {
            balloon_alert_to_hearers(I18n.T("*crack*"));
            playsound(source: this, soundin: SFX_HULL_CREAKING, vol: 50, vary: TRUE, pressure_affected: FALSE, ignore_walls: TRUE);
            return dot; //return ominous sounds when we're under the threshold.
        }

        DmList chain_turfs = get_crack_chain(get_turf(this), crack_length, TRUE) as DmList; // Get a nice chain of turfs
        foreach (atom along_length in Dm.each<atom>(chain_turfs.Copy()))
        {
            foreach (object extra_turfs in Dm.each(get_adjacent_turfs(chain_turfs[along_length] as atom))) //use get_adjacent turfs to help add extra turfs.
            {
                if (Dm.truthy(chain_turfs[extra_turfs]))
                    continue;
                chain_turfs.Add(extra_turfs);
            }
        }

        float crack_delay = 0;
        foreach (turf crack_turf in Dm.each<turf>(chain_turfs))
        {
            _addtimer(Dm.@new<datum_callback>(crack_turf, nameof(atom.ex_act), severity, crack_turf), CRACK_PROPAGATION_DELAY * crack_delay, file: "code/game/objects/effects/decals/turfdecal/weakpoint.dm", line: 61);
            playsound(source: crack_turf, soundin: SFX_HULL_CREAKING, vol: 35, vary: TRUE, pressure_affected: FALSE, ignore_walls: TRUE);
            if (prob(33))
                crack_delay++;
        }

        if (Dm.truthy(spawns_children))
            _addtimer(Dm.@new<datum_callback>(loc, nameof(turf.create_new_cracks), chain_turfs, new_weakpoints, skip_turfs, crack_length, crack_split_count), CRACK_PROPAGATION_DELAY * crack_delay, file: "code/game/objects/effects/decals/turfdecal/weakpoint.dm", line: 67);
        qdel(this);
        return dot;
    }

    // tg: code/game/objects/effects/decals/turfdecal/weakpoint.dm:71
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        to_chat(user, span_notice(I18n.T("You begin to strengthen []...", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, amount: 1, volume: 50)))
            return ITEM_INTERACT_BLOCKING;
        to_chat(user, span_notice(I18n.T("\\The [] is fully sealed, eliminating the risk of the weakpoint growing.", this)));
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/effects/decals/turfdecal/weakpoint.dm:79
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_stack_medical_wrap_sticky_tape)))
        {
            obj_item_stack_medical_wrap_sticky_tape duct_tape = (obj_item_stack_medical_wrap_sticky_tape)tool;
            if (!Dm.truthy(await duct_tape.use(1)))
                return ITEM_INTERACT_BLOCKING;
            to_chat(user, span_notice(I18n.T("\\The [] is sealed with a little elbow grease and a mound of [].", this, duct_tape)));
            qdel(this);
            return ITEM_INTERACT_SUCCESS;
        }
        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    // tg: code/game/objects/effects/decals/turfdecal/weakpoint.dm:96
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        dot.Add(span_notice(I18n.T("\\The [] could be repaired with a welder.", this)));
        dot.Add(span_warning(I18n.T("A strong enough explosion will cause [] to expand.", this)));
        return dot;
    }

    // tg: code/game/objects/effects/decals/turfdecal/weakpoint.dm:101
    public override object Destroy(float? force = null)
    {
        GLOB.weakpoints_spawned.Remove(this);
        return base.Destroy(force);
    }

    /**
     * Generates a list of turfs from the start location meandering along a randomized set of turns.
     * * start_location: The turf to begin the chain of turfs from.
     * * length: How many lengths this chain needs to be.
     * * add_splits: Should this crack chain apply additional instances of get_crack_chain while recursively cracking even further.
     */
    // tg: code/game/objects/effects/decals/turfdecal/weakpoint.dm:111
    public virtual object get_crack_chain(object start_location = null, object length = null, float? add_splits = null)
    {
        add_splits ??= TRUE;
        if (!Dm.truthy(length))
            throw Dm.CRASH("Weakpoint spawned with no length value!", "code/game/objects/effects/decals/turfdecal/weakpoint.dm", 113);
        if (!Dm.truthy(start_location))
            throw Dm.CRASH("No start location for crack specified!", "code/game/objects/effects/decals/turfdecal/weakpoint.dm", 115);

        DmList cracked_turfs = Dm.list();
        turf current = start_location as turf; //Start on top of ourselves
        float direction = Dm.num(pick(NORTH, SOUTH, EAST, WEST));

        for (float i = 1; i <= Dm.num(length); i++)
        {
            // Randomly branch or continue
            if (prob(CRACK_TURN_CHANCE))
                direction = turn(direction, Dm.num(pick(90, 135, 180, 225, 270)));
            current = get_turf(get_step(current, direction));
            if (!isturf(current))
                break;
            if (Dm.truthy(Dm.length(skip_turfs)) && Dm.truthy(is_type_in_typecache(current, skip_turfs)))
                continue;
            cracked_turfs.Add(current);
        }

        if (Dm.truthy(add_splits))
        {
            for (float subcrack = 1; subcrack <= crack_split_count; subcrack++)
                cracked_turfs.Add(get_crack_chain(pick(cracked_turfs), max(round(Dm.num(length) / 2), 1), FALSE)); //Stop recursion here
        }

        message_admins(Dm.text("Station weakpoint triggered, affecting [] turfs in [].", Dm.length(cracked_turfs), loc_name(start_location as atom)));
        log_game(Dm.text("Station weakpoint triggered, affecting [] turfs in [].", Dm.length(cracked_turfs), loc_name(start_location as atom)));
        return cracked_turfs;
    }

    /// If this turf becomes something we can't spawn a crack on, we should try and shift the crack or otherwise qdel.
    // tg: code/game/objects/effects/decals/turfdecal/weakpoint.dm:141
    [SignalHandler]
    public virtual void turf_changed(turf source = null)
    {
        turf option = null;
        DmList choices = get_adjacent_open_turfs(this) as DmList;
        while (option == null && Dm.truthy(Dm.length(choices)))
        {
            option = pick_n_take(get_adjacent_open_turfs(this) as DmList) as turf;
            if (Dm.locate_type(typeof(obj_effect_weakpoint), option) != null)
                continue;
            if (Dm.truthy(is_type_in_typecache(option, skip_turfs)))
                continue;
        }
        if (option == null)
        {
            qdel(this);
            return;
        }
        Waiting.NoSleep(forceMove(option));
    }
}

public partial class turf : atom
{
    /**
     * Used by weakpoint cracks to spawn new cracks after the crack is finished propagating.
     * * chain_turfs: The list of turfs that we're going to pull from in order to generate a new weakpoint. Generated by ex_act on the parent weakpoint.
     */
    // tg: code/game/objects/effects/decals/turfdecal/weakpoint.dm:160
    public virtual void create_new_cracks(DmList chain_turfs = null, float? new_weakpoints = null, object skip_turfs = null, float? crack_length = null, float? crack_split_count = null)
    {
        new_weakpoints ??= 1;
        skip_turfs ??= Dm.list();
        crack_length ??= 2;
        crack_split_count ??= 1;
        if (chain_turfs == null || !Dm.truthy(Dm.length(chain_turfs)))
        {
            chain_turfs = Dm.list(this);
            chain_turfs.Add(get_adjacent_turfs(this));
        }
        if (Dm.truthy(skip_turfs))
            chain_turfs = typecache_filter_list_reverse(chain_turfs, skip_turfs as DmList) as DmList; //Filter out things that we don't want to spawn new weakpoints onto.

        float active_count = 0;
        DmList new_cracks = Dm.list();
        while (active_count < new_weakpoints)
        {
            if (!Dm.truthy(Dm.length(chain_turfs)))
                return;
            turf spawn_location = pick_n_take(chain_turfs) as turf;
            if (Dm.locate_type(typeof(obj_effect_weakpoint), spawn_location) != null)
                continue;
            if (Dm.truthy(skip_turfs))
            {
                if (Dm.truthy(is_type_in_typecache(spawn_location, skip_turfs)))
                    continue;
            }
            obj_effect_weakpoint newpoint = Dm.@new<obj_effect_weakpoint>(spawn_location);
            //inherit parent var values in case of var-editing.
            newpoint.new_weakpoints = new_weakpoints.Value;
            newpoint.crack_length = crack_length.Value;
            newpoint.crack_split_count = crack_split_count.Value;
            new_cracks.Add(newpoint);
            spawn_location.levelupdate();
            active_count++;
        }

        notify_ghosts(
            I18n.T("A new crack has been spawned in [].", get_area(this)),
            source: pick(new_cracks) as atom,
            header: I18n.T("Weakpoint created"),
            ghost_sound: "sound/effects/hit_kick.ogg");
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/effects/decals/turfdecal/weakpoint.dm:30
    internal DmList obj_effect_weakpoint__skip_turfs;
}
