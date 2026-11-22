// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/footstep.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b audio, slice 4: the whole file. The var block (steps_for_living, volume, e_range, footstep_type, footstep_sounds,
// sound_vary) and the element flags are generated. Each step is a playsound from the COMSIG_MOVABLE_MOVED of a cardinal
// Move, the capsule's crossing included (LocomotionSeam.BridgeMove: one Move per tile, forced FALSE, momentum_change TRUE).

///Footstep element. Plays footsteps at parents location when it is appropriate.
// tg: code/datums/elements/footstep.dm:2
public partial class datum_element_footstep : datum_element
{
    // tg: code/datums/elements/footstep.dm:18
    [DmParams("target", "footstep_type", "volume", "e_range", "sound_vary")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object footstep_type = Dm.arg(__extra, 0);
        object volume = Dm.arg(__extra, 1);
        object e_range = Dm.arg(__extra, 2);
        object sound_vary = Dm.arg(__extra, 3);
        footstep_type ??= FOOTSTEP_MOB_BAREFOOT;
        volume ??= 0.5f;
        e_range ??= -8f;
        sound_vary ??= FALSE;
        object dot = null;
        dot = await base.Attach(target, Dm.forward(__extra, footstep_type, volume, e_range, sound_vary));
        if (!ismovable(target))
            return ELEMENT_INCOMPATIBLE;
        this.volume = volume;
        this.e_range = e_range;
        this.footstep_type = footstep_type;
        this.sound_vary = Dm.num(sound_vary);
        switch (footstep_type)
        {
            case FOOTSTEP_MOB_HUMAN:
                if (!ishuman(target))
                    return ELEMENT_INCOMPATIBLE;
                RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(play_humanstep));
                RegisterSignal(target, COMSIG_LIVING_CAPSULE_STRIDE, nameof(play_humanstep)); // DV233
                RegisterSignal(target, COMSIG_LIVING_CAPSULE_HALT, nameof(capsule_halt));
                steps_for_living[target] = 0f;
                return dot;
            case FOOTSTEP_MOB_CLAW:
                footstep_sounds = GLOB.clawfootstep;
                break;
            case FOOTSTEP_MOB_BAREFOOT:
                footstep_sounds = GLOB.barefootstep;
                break;
            case FOOTSTEP_MOB_HEAVY:
                footstep_sounds = GLOB.heavyfootstep;
                break;
            case FOOTSTEP_MOB_SHOE:
                footstep_sounds = GLOB.footstep;
                break;
            case FOOTSTEP_MOB_RUST:
                footstep_sounds = Dm.resource("sound/effects/footstep/rustystep1.ogg");
                this.volume = Dm.mul(90f, volume);
                break;
            case FOOTSTEP_MOB_SLIME:
                footstep_sounds = Dm.resource("sound/effects/footstep/slime1.ogg");
                this.volume = Dm.mul(20f, volume);
                break;
            case FOOTSTEP_OBJ_MACHINE:
                footstep_sounds = Dm.resource("sound/effects/bang.ogg");
                this.volume = Dm.mul(90f, volume);
                RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(play_simplestep_machine));
                RegisterSignal(target, COMSIG_LIVING_CAPSULE_STRIDE, nameof(play_simplestep_machine)); // DV233
                return dot;
            case FOOTSTEP_OBJ_ROBOT:
                footstep_sounds = Dm.resource("sound/effects/tank_treads.ogg");
                this.volume = Dm.mul(90f, volume);
                RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(play_simplestep_machine));
                RegisterSignal(target, COMSIG_LIVING_CAPSULE_STRIDE, nameof(play_simplestep_machine)); // DV233
                return dot;
        }
        RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(play_simplestep));
        RegisterSignal(target, COMSIG_LIVING_CAPSULE_STRIDE, nameof(play_simplestep)); // DV233
        RegisterSignal(target, COMSIG_LIVING_CAPSULE_HALT, nameof(capsule_halt));
        steps_for_living[target] = 0f;
        return dot;
    }

    // tg: code/datums/elements/footstep.dm:60
    [DmParams("source")]
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, COMSIG_MOVABLE_MOVED);
        UnregisterSignal(source, COMSIG_LIVING_CAPSULE_STRIDE);
        UnregisterSignal(source, COMSIG_LIVING_CAPSULE_HALT);
        Dm.remove(ref steps_for_living, source);
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/footstep.dm:65
    public virtual void play_craw_step(mob_living source = null)
    {
        turf_open turf = get_turf(source) as turf_open;
        if (!(turf is turf_open) || !Dm.truthy(turf.footstep))
            return;

        object sound = Dm.resource("sound/effects/footstep/crawl1.ogg");
        if (HAS_TRAIT(source, TRAIT_FLOPPING))
            sound = Dm.pick(SFX_FISH_PICKUP, Dm.resource("sound/mobs/non-humanoids/fish/fish_drop1.ogg"));
        playsound(turf, sound, Dm.mul(15f, volume), falloff_distance: 1, vary: sound_vary);
    }

    ///Prepares a footstep for living mobs. Determines if it should get played. Returns the turf it should get played on. Note that it is always a /turf/open
    // tg: code/datums/elements/footstep.dm:76
    public virtual DmList prepare_step(mob_living source = null)
    {
        turf_open turf = get_turf(source) as turf_open;
        if (!(turf is turf_open))
            return null;

        steps_for_living[source] = Dm.add(steps_for_living[source], 1f);
        float steps = Dm.num(steps_for_living[source]);

        if (steps >= 24)
        {
            // right foot = 0, 4, 8, 12, 16, 20
            // left foot = 2, 6, 10, 14, 18, 22
            // 24 -> return to 0 -> right foot, repeat
            steps_for_living[source] = 0f;
            steps = 0;
        }

        if (Dm.truthy(Dm.mod(steps, 2f)))
            // skipping every other step, anyways. gets noisy otherwise
            return null;

        if (Dm.mod(steps, 6f) != 0 && !Dm.truthy(source.has_gravity()))
            // don't need to step as often when you hop around
            return null;

        DmList footstep_data = Dm.list(
            Dm.kv(FOOTSTEP_MOB_SHOE, turf.footstep),
            Dm.kv(FOOTSTEP_MOB_BAREFOOT, turf.barefootstep),
            Dm.kv(FOOTSTEP_MOB_HEAVY, turf.heavyfootstep),
            Dm.kv(FOOTSTEP_MOB_CLAW, turf.clawfootstep),
            Dm.kv(STEP_SOUND_PRIORITY, STEP_SOUND_NO_PRIORITY));
        object sigreturn = (!Dm.truthy(turf._listen_lookup?[COMSIG_TURF_PREPARE_STEP_SOUND]) ? NONE : turf._SendSignal(COMSIG_TURF_PREPARE_STEP_SOUND, Dm.list(turf, footstep_data)));
        if (Dm.truthy(Dm.bitand(sigreturn, FOOTSTEP_OVERRIDEN)))
            return footstep_data;
        if (turf.footstep == null)
            // The turf has no footstep sound (e.g. open space)
            // and none of the objects on that turf (e.g. catwalks) overrides it
            return null;
        return footstep_data;
    }

    // tg: code/datums/elements/footstep.dm:115
    [SignalHandler]
    public virtual void play_simplestep(mob_living source = null, atom oldloc = null, object direction = null, object forced = null, DmList old_locs = null, object momentum_change = null)
    {
        // DEVIATION(DV233): a capsule's crossing plays no step; its strides do
        if (Dm.truthy(forced) || SHOULD_ATOM_DISABLE_FOOTSTEPS(source) || LocomotionSeam.IsBridging(source))
            return;

        if (source.body_position == LYING_DOWN)
        {
            play_craw_step(source);
            return;
        }

        if (HAS_TRAIT(source, TRAIT_SILENT_FOOTSTEPS) || SHOULD_MOB_DISABLE_FOOTSTEPS(source, walk_heard: true))
            return;

        DmList prepared_steps = prepare_step(source);
        if (prepared_steps == null)
            return;

        // DEVIATION(DV71): a walking carbon's steps are heard, quieter
        float walk_share = FOOTSTEP_WALKING(source) ? WALK_FOOTSTEP_VOLUME : 1f;
        if (isfile(footstep_sounds) || istext(footstep_sounds))
        {
            /// the volume for this is defined on attach when the sound gets set footstep_sounds
            playsound(source.loc, footstep_sounds, Dm.mul(volume, walk_share), falloff_distance: 1, vary: sound_vary);
            return;
        }

        object turf_footstep = prepared_steps[footstep_type];
        DmList sounds = footstep_sounds as DmList;
        if (turf_footstep == null || !Dm.truthy(sounds[turf_footstep]))
            return;
        // The positional TRUE is vary; the named vary = sound_vary after it replaces it (BYOND binds named arguments after
        // the positional ones: OpenDream DMProc's argument binding).
        DmList entry = sounds[turf_footstep] as DmList;
        playsound(source.loc, Dm.pick(entry[1]), Dm.mul(Dm.mul(entry[2], volume), walk_share), extrarange: Dm.add(entry[3], e_range), falloff_distance: 1, vary: sound_vary);
    }

    // tg: code/datums/elements/footstep.dm:142
    [SignalHandler]
    public virtual void play_humanstep(mob_living_carbon_human source = null, atom oldloc = null, object direction = null, object forced = null, DmList old_locs = null, object momentum_change = null)
    {
        // DEVIATION(DV233): a capsule's crossing plays no step; its strides do
        if (Dm.truthy(forced) || SHOULD_ATOM_DISABLE_FOOTSTEPS(source) || !Dm.truthy(momentum_change) || LocomotionSeam.IsBridging(source))
            return;

        if (source.body_position == LYING_DOWN)
        {
            play_craw_step(source);
            return;
        }

        if (HAS_TRAIT(source, TRAIT_SILENT_FOOTSTEPS) || SHOULD_MOB_DISABLE_FOOTSTEPS(source, walk_heard: true))
            return;

        DmList prepared_steps = prepare_step(source);
        if (prepared_steps == null)
            return;

        object footstep_type = null;
        DmList footstep_sounds = null;
        object stepcount = steps_for_living[source];
        // any leg covering sounds defaults to shoe sounds
        if (Dm.truthy(Dm.bitand(Dm.bitor(Dm.bitor(source.wear_suit?.body_parts_covered, source.w_uniform?.body_parts_covered), source.shoes?.body_parts_covered), FEET)))
            footstep_type = FOOTSTEP_MOB_SHOE;
        // now pick whether to draw from left foot or right foot sounds
        else
        {
            obj_item_bodypart_leg left_leg = source.get_bodypart(BODY_ZONE_L_LEG) as obj_item_bodypart_leg;
            obj_item_bodypart_leg right_leg = source.get_bodypart(BODY_ZONE_R_LEG) as obj_item_bodypart_leg;
            if (Dm.eq(stepcount, 2f) || Dm.eq(stepcount, 6f))
            {
                footstep_sounds = (left_leg?.special_footstep_sounds is var or__1 && Dm.truthy(or__1) ? or__1 : right_leg?.special_footstep_sounds);
                footstep_type = (left_leg?.footstep_type is var or__2 && Dm.truthy(or__2) ? or__2 : right_leg?.footstep_type);
            }
            else
            {
                footstep_sounds = (right_leg?.special_footstep_sounds is var or__3 && Dm.truthy(or__3) ? or__3 : left_leg?.special_footstep_sounds);
                footstep_type = (right_leg?.footstep_type is var or__4 && Dm.truthy(or__4) ? or__4 : left_leg?.footstep_type);
            }
        }

        // allow for snowflake effects to take priority
        if (!Dm.truthy(length(footstep_sounds)))
        {
            switch (footstep_type)
            {
                case FOOTSTEP_MOB_CLAW:
                    footstep_sounds = GLOB.clawfootstep[prepared_steps[footstep_type]] as DmList;
                    break;
                case FOOTSTEP_MOB_BAREFOOT:
                    footstep_sounds = GLOB.barefootstep[prepared_steps[footstep_type]] as DmList;
                    break;
                case FOOTSTEP_MOB_HEAVY:
                    footstep_sounds = GLOB.heavyfootstep[prepared_steps[footstep_type]] as DmList;
                    break;
                case FOOTSTEP_MOB_SHOE:
                    footstep_sounds = GLOB.footstep[prepared_steps[footstep_type]] as DmList;
                    break;
                case FOOTSTEP_MOB_SYNTHETIC:
                {
                    object barefoot_type = prepared_steps[FOOTSTEP_MOB_BAREFOOT];
                    // these categories will use the synthetic step over the normal barefoot steps
                    // (var/static/list/synthetic_footstep_types and synthetic_footsteps: proc-level statics, rules 10.6)
                    if (Dm.truthy(play_humanstep__synthetic_footstep_types[barefoot_type]))
                        footstep_sounds = play_humanstep__synthetic_footsteps;
                    else
                        footstep_sounds = GLOB.barefootstep[barefoot_type] as DmList;
                    break;
                }
                case null:
                    return;
                default:
                    // Got an unsupported type, somehow
                    throw Dm.CRASH(Dm.text("Invalid footstep type for human footstep: \\[[]\\]", footstep_type), "code/datums/elements/footstep.dm", 213);
            }
        }

        // no snowflake, and no (found) footstep sounds, nothing to do
        if (!Dm.truthy(length(footstep_sounds)))
            return;

        float volume_multiplier = 1;
        float range_adjustment = 0;

        if (HAS_TRAIT(source, TRAIT_LIGHT_STEP))
        {
            volume_multiplier = 0.6f;
            range_adjustment = -2;
        }
        // DEVIATION(DV71): a walking human's steps are heard, quieter
        if (FOOTSTEP_WALKING(source))
            volume_multiplier *= WALK_FOOTSTEP_VOLUME;

        // list returned by playsound() filled by client mobs who heard the footstep. given to play_fov_effect()
        object heard_clients = null;
        object picked_sound = Dm.pick(footstep_sounds[1]);
        object picked_volume = Dm.mul(Dm.mul(footstep_sounds[2], volume), volume_multiplier);
        object picked_range = Dm.add(Dm.add(footstep_sounds[3], e_range), range_adjustment);

        heard_clients = playsound(
            source: source,
            soundin: picked_sound,
            vol: picked_volume,
            vary: sound_vary,
            extrarange: picked_range,
            falloff_distance: 1);

        if (Dm.truthy(heard_clients))
            // play_fov_effect(source, 5, "footstep", direction, ignore_self = TRUE, override_list = heard_clients)
            // (modules/mob/living/fov.dm): the field of view cone's flash for the hearers; presentation, not declared (as
            // do_attack_animation's, atoms_movable.dm:1568).
            Dm.Deferred("code/datums/elements/footstep.dm:242", "play_humanstep: play_fov_effect(source, 5, \"footstep\", ...) for the hearers (presentation)");
    }

    // tg: code/datums/elements/footstep.dm:190  var/static/list/synthetic_footstep_types (a proc-level static, rules 10.6)
    private static ref DmList play_humanstep__synthetic_footstep_types => ref TgState.Current.datum_element_footstep__play_humanstep__synthetic_footstep_types;

    // tg: code/datums/elements/footstep.dm:196  var/static/list/synthetic_footsteps (a proc-level static, rules 10.6)
    private static ref DmList play_humanstep__synthetic_footsteps => ref TgState.Current.datum_element_footstep__play_humanstep__synthetic_footsteps;

    // DEVIATION(DV233): the owner (2026-10-31): a walk that stops lands its last step. prepare_step sounds every other stride, so a
    // count left odd is a foot still in the air: the stop plays that stride as a step; an even count has just sounded and stays quiet.
    [SignalHandler]
    public virtual void capsule_halt(mob_living source = null)
    {
        if (Dm.num(steps_for_living[source]) % 2 == 0)
            return;
        if (Dm.eq(footstep_type, FOOTSTEP_MOB_HUMAN))
            play_humanstep(source as mob_living_carbon_human, source.loc, source.dir, FALSE, null, TRUE);
        else
            play_simplestep(source, source.loc, source.dir, FALSE, null, TRUE);
    }

    ///Prepares a footstep for machine walking
    // tg: code/datums/elements/footstep.dm:246
    [SignalHandler]
    public virtual void play_simplestep_machine(atom_movable source = null, atom oldloc = null, object direction = null, object forced = null, DmList old_locs = null, object momentum_change = null)
    {
        // DEVIATION(DV233): a capsule's crossing plays no step; its strides do
        if (Dm.truthy(forced) || SHOULD_ATOM_DISABLE_FOOTSTEPS(source) || LocomotionSeam.IsBridging(source))
            return;

        if (HAS_TRAIT(source, TRAIT_SILENT_FOOTSTEPS))
            return;

        turf_open source_loc = get_turf(source) as turf_open;
        if (!(source_loc is turf_open))
            return;

        playsound(source_loc, footstep_sounds, 50, falloff_distance: 1, vary: sound_vary);
    }
}

public sealed partial class TgState
{
    // tg: code/datums/elements/footstep.dm:190  var/static/list/synthetic_footstep_types = list(FOOTSTEP_CARPET_BAREFOOT = 1, ...)
    internal DmList datum_element_footstep__play_humanstep__synthetic_footstep_types = Dm.list(
        Dm.kv(FOOTSTEP_CARPET_BAREFOOT, 1),
        Dm.kv(FOOTSTEP_HARD_BAREFOOT, 1),
        Dm.kv(FOOTSTEP_WOOD_BAREFOOT, 1));

    // the actual synthetic footstep sound
    // tg: code/datums/elements/footstep.dm:196  var/static/list/synthetic_footsteps = list(list(sounds), 50, 2)
    internal DmList datum_element_footstep__play_humanstep__synthetic_footsteps = Dm.list(
        /*sounds = */Dm.list(
            Dm.kv(Dm.resource("sound/items/modsuit/rigstep.ogg"), 1),
            Dm.kv(Dm.resource("sound/items/modsuit/rigstep.ogg"), 1)),
        /*volume = */50,
        /*extrarange= */2);
}
