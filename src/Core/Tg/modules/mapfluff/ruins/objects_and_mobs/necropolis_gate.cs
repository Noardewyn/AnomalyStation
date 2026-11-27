// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

//The necropolis gate is used to call forth Legion from the Necropolis.
// tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:2
public partial class obj_structure_necropolis_gate : obj_structure
{
    // tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:23  var/static/mutable_appearance/top_overlay
    public static ref object top_overlay => ref TgState.Current.obj_structure_necropolis_gate__top_overlay;
    // tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:24  var/static/mutable_appearance/door_overlay
    public static ref object door_overlay => ref TgState.Current.obj_structure_necropolis_gate__door_overlay;

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:49  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_structure_necropolis_gate__Initialize__loc_connections;

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:28
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(setDir(SOUTH));
        turf sight_blocker_turf = get_turf(this);
        if (Dm.truthy(sight_blocker_distance))
        {
            for (int i = 1; i <= sight_blocker_distance; i++)
            {
                if (sight_blocker_turf == null)
                    break;
                sight_blocker_turf = get_step(sight_blocker_turf, NORTH);
            }
        }
        if (sight_blocker_turf != null)
        {
            sight_blocker = Dm.@new<obj_structure_opacity_blocker>(sight_blocker_turf); //we need to block sight in a different spot than most things do
            sight_blocker.pixel_y = Dm.num(Dm.initial(sight_blocker, "pixel_y")) - (32 * sight_blocker_distance);
        }
        icon_state = "gate_bottom";
        top_overlay = mutable_appearance(Dm.resource("icons/effects/96x96.dmi"), "gate_top");
        ((mutable_appearance)top_overlay).layer = EDGED_TURF_LAYER;
        add_overlay(top_overlay);
        door_overlay = mutable_appearance(Dm.resource("icons/effects/96x96.dmi"), "door");
        ((mutable_appearance)door_overlay).layer = EDGED_TURF_LAYER;
        add_overlay(door_overlay);

        Dm.@new<obj_effect_decal_necropolis_gate_decal>(loc);

        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        // clickthrough = FALSE, passed in its place: the see-through component is inert, and its undeclared Initialize binds no names
        Waiting.NoSleep(AddComponent(typeof(datum_component_seethrough), SEE_THROUGH_MAP_DEFAULT_TWO_TALL, 100, 0.5f * SECONDS, 2 * SECONDS, FALSE));
        return dot;
    }

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:57
    public override object Destroy(float? force = null)
    {
        qdel(sight_blocker);
        return base.Destroy(force);
    }

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:61
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        await DmTask.CompletedTask;
        return 0;
    }

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:64
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        if (!Dm.eq(border_dir, dir))
            return TRUE;
        return dot;
    }

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:69
    [SignalHandler]
    public virtual object on_exit(datum source = null, atom_movable leaving = null, object direction = null)
    {
        if (Dm.truthy(Dm.bitand(leaving.movement_type, PHASING)))
            return null;

        if (leaving == this)
            return null; // Let's not block ourselves.

        if (Dm.eq(direction, dir) && Dm.truthy(density))
        {
            Waiting.NoSleep(leaving.Bump(this));
            return COMPONENT_ATOM_BLOCK_EXIT;
        }
        return null;
    }

    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:96
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(locked))
        {
            to_chat(user, span_bolddanger(Dm.truthy(open) ? I18n.T("It's stuck open.") : I18n.T("It's locked.")));
            return null;
        }
        await toggle_the_gate(user);
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:103
    public virtual async DmTask<object> toggle_the_gate(mob user = null, object legion_damaged = null)
    {
        if (Dm.truthy(changing_openness))
            return null;
        changing_openness = TRUE;
        turf T = get_turf(this);
        if (Dm.truthy(open))
        {
            Dm.@new<obj_effect_temp_visual_necropolis>(T);
            visible_message(span_boldwarning(I18n.T("The door slams closed!")));
            await sleep(0.1f * SECONDS);
            playsound(T, Dm.resource("sound/effects/stonedoor_openclose.ogg"), 300, TRUE, frequency: 80000);
            await sleep(0.1f * SECONDS);
            set_density(TRUE);
            await sleep(0.1f * SECONDS);
            turf sight_blocker_turf = get_turf(this);
            if (Dm.truthy(sight_blocker_distance))
            {
                for (int i = 1; i <= sight_blocker_distance; i++)
                {
                    if (sight_blocker_turf == null)
                        break;
                    sight_blocker_turf = get_step(sight_blocker_turf, NORTH);
                }
            }
            if (sight_blocker_turf != null)
            {
                sight_blocker.pixel_y = Dm.num(Dm.initial(sight_blocker, "pixel_y")) - (32 * sight_blocker_distance);
                await sight_blocker.forceMove(sight_blocker_turf);
            }
            await sleep(0.25f * SECONDS);
            playsound(T, Dm.resource("sound/effects/magic/clockwork/invoke_general.ogg"), 30, TRUE, frequency: 15000);
            add_overlay(door_overlay);
            open = FALSE;
        }
        else
        {
            cut_overlay(door_overlay);
            Dm.@new<obj_effect_temp_visual_necropolis_open>(T);
            await sleep(0.2f * SECONDS);
            visible_message(span_warning(I18n.T("The door starts to grind open...")));
            playsound(T, Dm.resource("sound/effects/stonedoor_openclose.ogg"), 300, TRUE, frequency: 20000);
            await sleep(2.2f * SECONDS);
            await sight_blocker.forceMove(this);
            await sleep(0.5f * SECONDS);
            set_density(FALSE);
            await sleep(0.5f * SECONDS);
            open = TRUE;
        }
        changing_openness = FALSE;
        return TRUE;
    }
}

// tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:83
public partial class obj_structure_opacity_blocker : obj_structure
{
    // tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:92
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        await DmTask.CompletedTask;
        return FALSE;
    }
}

// tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:148
public partial class obj_structure_necropolis_gate_legion_gate : obj_structure_necropolis_gate
{
    // tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:152
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        GLOB.necropolis_gate = this;
        return dot;
    }

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:156
    public override object Destroy(float? force = null)
    {
        if (GLOB.necropolis_gate == this)
            GLOB.necropolis_gate = null;
        return base.Destroy(force);
    }

    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:162
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(open) && !Dm.truthy(changing_openness))
        {
            object safety = await tgui_alert(user, I18n.T("You think this might be a bad idea..."), I18n.T("Knock on the door?"), Dm.list("Proceed", "Abort"));
            if (Dm.eq(safety, "Abort") || !in_range(this, user) || QDELETED(this) || Dm.truthy(open) || Dm.truthy(changing_openness) || Dm.truthy(user.incapacitated))
                return null;
            user.visible_message(span_warning(I18n.T("[] knocks on []...", user, this)), span_bolddanger(I18n.T("You tentatively knock on []...", this)));
            playsound(user.loc, Dm.resource("sound/effects/shieldbash.ogg"), 100, TRUE);
            await sleep(5 * SECONDS);
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:172
    public override async DmTask<object> toggle_the_gate(mob user = null, object legion_damaged = null)
    {
        if (Dm.truthy(open))
            return null;
        object dot = await base.toggle_the_gate(user, legion_damaged);
        if (Dm.truthy(dot))
        {
            locked = TRUE;
            turf T = get_turf(this);
            visible_message(span_userdanger(I18n.T("Something horrible emerges from the Necropolis!")));
            if (Dm.truthy(legion_damaged))
            {
                message_admins("Legion took damage while the necropolis gate was closed, and has released itself!");
                log_game("Legion took damage while the necropolis gate was closed and released itself.");
            }
            else
            {
                message_admins(Dm.text("[] has released Legion!", user != null ? ADMIN_LOOKUPFLW(user) : "Unknown"));
                user.log_message("released Legion.", LOG_GAME);
            }

            sound legion_sound = sound(Dm.resource("sound/mobs/non-humanoids/legion/legion_spawn.ogg"));
            foreach (mob M in Dm.each<mob>(GLOB.player_list))
            {
                if (Dm.truthy(is_valid_z_level(get_turf(M), T)))
                {
                    to_chat(M, span_userdanger(I18n.T("Discordant whispers flood your mind in a thousand voices. Each one speaks your name, over and over. Something horrible has been released.")));
                    M.playsound_local(T, null, 100, FALSE, 0, FALSE, pressure_affected: FALSE, sound_to_use: legion_sound);
                    flash_color(M, "#FF0000", 50);
                }
            }
            mutable_appearance release_overlay = mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "legiondoor") as mutable_appearance;
            notify_ghosts(
                I18n.T("Legion has been released in the []!", get_area(this)),
                source: this,
                alert_overlay: release_overlay,
                notify_flags: NOTIFY_CATEGORY_NOFLASH);
        }
        return dot;
    }
}

// tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:222
public partial class obj_structure_necropolis_arch : obj_structure
{
    // tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:235  var/static/mutable_appearance/top_overlay
    public static ref object top_overlay => ref TgState.Current.obj_structure_necropolis_arch__top_overlay;

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:237
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = "arch_bottom";
        top_overlay = mutable_appearance(Dm.resource("icons/effects/160x160.dmi"), "arch_top");
        ((mutable_appearance)top_overlay).layer = EDGED_TURF_LAYER;
        add_overlay(top_overlay);

        Waiting.NoSleep(AddComponent(typeof(datum_component_seethrough), SEE_THROUGH_MAP_DEFAULT_TWO_TALL));
        return dot;
    }

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:246
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        await DmTask.CompletedTask;
        return 0;
    }
}

//stone tiles for boss arenas
// tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:251
public partial class obj_structure_stone_tile : obj_structure
{
    // tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:262
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("[][]", tile_key, rand(1, tile_random_sprite_max));

        TgState state = TgState.Current;
        state.obj_structure_stone_tile_Initialize__give_turf_traits ??= string_list(Dm.list(TRAIT_TURF_IGNORE_SLOWDOWN, TRAIT_LAVA_STOPPED, TRAIT_CHASM_STOPPED, TRAIT_IMMERSE_STOPPED));
        Waiting.NoSleep(AddElement(typeof(datum_element_give_turf_traits), state.obj_structure_stone_tile_Initialize__give_turf_traits));
        return dot;
    }

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:271
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        await DmTask.CompletedTask;
        return null;
    }
}

// rules 10.6: the proc-level static above, made on first use
public sealed partial class TgState
{
    internal object obj_structure_stone_tile_Initialize__give_turf_traits;
    internal object obj_structure_necropolis_gate__top_overlay;
    internal object obj_structure_necropolis_gate__door_overlay;
    internal object obj_structure_necropolis_arch__top_overlay;
    // tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:49  var/static/list/loc_connections = list(COMSIG_ATOM_EXIT = PROC_REF(on_exit))
    internal DmList obj_structure_necropolis_gate__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_EXIT, nameof(obj_structure_necropolis_gate.on_exit)));
}

public partial class datum_controller_global_vars
{
    // tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:147  GLOBAL_DATUM(necropolis_gate, /obj/structure/necropolis_gate/legion_gate)
    public obj_structure_necropolis_gate_legion_gate necropolis_gate;

    // tg: code/modules/mapfluff/ruins/objects_and_mobs/necropolis_gate.dm:147  (GLOBAL_UNMANAGED(necropolis_gate), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalnecropolis_gate()
    {
        return;
    }
}
