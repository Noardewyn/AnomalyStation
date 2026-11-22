// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/decals/decal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the [DmPath] of /obj/effect/decal (package B, wave 4: the signatures of its turf and area files name the type), its
// own procs (NeverShouldHaveComeHere, the ChangeTurf hook), its Initialize (M3 I1a, with the cleanable decals) and
// /obj/effect/turf_decal (M3 P4), fire_act (M4b slice 3) and ex_act (M5x slice 1: explosions) translated; blob_act is a
// generated live-partial stub (docs/ai/specs/live-partial.txt, ## I1).

// tg: code/game/objects/effects/decals/decal.dm:1
[DmPath("/obj/effect/decal")]
public partial class obj_effect_decal : obj_effect
{
}

// M3 P4: the roots of /obj/effect/decal's own procs (the phase B skeleton). M3 I1a: its Initialize, so that the type and
// its cleanable subtypes are live (D112).

// tg: code/game/objects/effects/decals/decal.dm:1
public partial class obj_effect_decal : obj_effect
{
    // tg: code/game/objects/effects/decals/decal.dm:8  var/static/list/decal_move_connections (a TgState field, rules 10.6)
    private static ref DmList decal_move_connections => ref TgState.Current.obj_effect_decal__decal_move_connections;

    // tg: code/game/objects/effects/decals/decal.dm:32
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        qdel(this);
        return TRUE;
    }

    // tg: code/game/objects/effects/decals/decal.dm:36
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        if (!Dm.truthy(Dm.bitand(resistance_flags, FIRE_PROOF))) //non fire proof decal or being burned by lava
            qdel(this);
        return null;
    }

    // tg: code/game/objects/effects/decals/decal.dm:12
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(NeverShouldHaveComeHere(loc as turf)))
        {
            if (Dm.truthy(mapload))
                // stack_trace("[name] spawned in a bad turf ([loc]) at [AREACOORD(src)] in \the [get_area(src)]. ..."): AREACOORD
                // (__DEFINES/admin.dm:67) needs Admin_Coordinates_Readable, which the port does not have yet
                Dm.Deferred("code/game/objects/effects/decals/decal.dm:16", "Initialize: the mapping error of a decal mapped on a bad turf (AREACOORD, the admin coordinates, M10+)");
            return INITIALIZE_HINT_QDEL;
        }
        while (isopenspaceturf(loc) && Dm.truthy(can_z_move(DOWN, z_move_flags: ZMOVE_ALLOW_ANCHORED)))
            Waiting.NoSleep(zMove(DOWN, z_move_flags: ZMOVE_ALLOW_ANCHORED));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), decal_move_connections));
        Waiting.NoSleep(AddElement(typeof(datum_element_force_move_pulled)));
        return dot;
    }

    ///Checks if we are allowed to be in `here_turf`, and returns that result. Subtypes should override this when necessary.
    // tg: code/game/objects/effects/decals/decal.dm:29
    public virtual object NeverShouldHaveComeHere(turf here_turf = null)
    {
        return isclosedturf(here_turf) || (isgroundlessturf(here_turf) && !Dm.truthy(GET_TURF_BELOW(here_turf))) ? TRUE : FALSE;
    }

    // tg: code/game/objects/effects/decals/decal.dm:40
    [SignalHandler]
    public virtual void on_decal_move(turf changed = null, object path = null, DmList new_baseturfs = null, object flags = null, DmList post_change_callbacks = null)
    {
        Dm.append(post_change_callbacks, Dm.@new<datum_callback>(this, nameof(sanity_check_self)));
    }

    // tg: code/game/objects/effects/decals/decal.dm:44
    public virtual void sanity_check_self(turf changed = null)
    {
        if (changed == loc && Dm.truthy(NeverShouldHaveComeHere(changed)))
            qdel(this);
    }
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

// M3 P4: /obj/effect/turf_decal is live: its init gives its turf the decal element (the decal's appearance in the turf's
// overlays, D115) and deletes the decal; its subtypes (markings, tile colouring, ...) hold only vars (the generated part).

// tg: code/game/objects/effects/decals/decal.dm:50
public partial class obj_effect_turf_decal : obj_effect
{
    // This is with the intent of optimizing mapload
    // See spawners for more details since we use the same pattern
    // Basically rather then creating and deleting ourselves, why not just do the bare minimum?
    // tg: code/game/objects/effects/decals/decal.dm:64  (SHOULD_CALL_PARENT(FALSE) is dropped, rules 7.9)
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
            stack_trace(Dm.text("Warning: []([]) initialized multiple times!", this, type), "code/game/objects/effects/decals/decal.dm", 67);
        Dm.combine(ref flags_1, INITIALIZED_1);

        // If the tile uses holiday colors, apply them here (#ifndef MAP_TEST: MAP_TEST is undefined in production, rules 1.7)
        if (Dm.truthy(use_holiday_colors))
        {
            object custom_color = request_decoration_colors(this, pattern); // gaps R3
            if (Dm.truthy(custom_color))
            {
                color = custom_color;
                alpha = DECAL_ALPHA;
            }
        }

        turf T = loc as turf;
        if (!(T is turf)) //you know this will happen somehow
            throw Dm.CRASH("Turf decal initialized in an object/nullspace", "code/game/objects/effects/decals/decal.dm", 81);
        Waiting.NoSleep(T.AddElement(typeof(datum_element_decal), icon, icon_state, dir, null, layer, alpha, color, null, FALSE, null));
        return INITIALIZE_HINT_QDEL;
    }

    // tg: code/game/objects/effects/decals/decal.dm:85  (SHOULD_CALL_PARENT(FALSE) is dropped, rules 7.9)
    public override object Destroy(float? force = null)
    {
        // tg: #ifdef UNIT_TESTS ... #endif not translated (inactive in production)
        // Intentionally used over moveToNullspace(), which calls doMove(), which fires
        // off an enormous amount of procs, signals, etc, that this temporary effect object
        // never needs or affects.
        loc = null;
        return QDEL_HINT_QUEUE;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/effects/decals/decal.dm:8  /obj/effect/decal var/static/list/decal_move_connections
    internal DmList obj_effect_decal__decal_move_connections = Dm.list(
        Dm.kv(COMSIG_TURF_CHANGE, nameof(obj_effect_decal.on_decal_move)));
}
