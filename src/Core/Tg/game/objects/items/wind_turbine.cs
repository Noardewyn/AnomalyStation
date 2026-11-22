// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/wind_turbine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DV2 (D333): the file whole. The portable wind turbine: worn on the back or the suit storage its rotor (tg's "rotor_N" frames
// over the turbine, in the world and worn) turns as its wearer walks through air, the faster the denser the air, with a woosh each
// full turn, storing power (more with a better capacitor) that it gives to an energy gun, a baton, a cell, a PDA or a circuit hung
// on it; wrenched to the floor it is turned by the space wind instead. A clumsy wearer gets whacked in the head by the blades; its
// suicide takes the head off. The var blocks are generated (D111).

// tg: code/game/objects/items/wind_turbine.dm:8
public partial class obj_item_portable_wind_turbine : obj_item
{
    private const float TURBINE_MAX_STORED_POWER = 0.2f * STANDARD_CELL_CHARGE; // tg: wind_turbine.dm:1
    private const float TURBINE_ANIMATION_TICKS_PER_TILE = 1; // tg: wind_turbine.dm:2
    private const float TURBINE_ANIMATION_TICKS = 4; // tg: wind_turbine.dm:3
    private const float TURBINE_CHARGE_PER_TILE = 0.002f * STANDARD_CELL_CHARGE; // tg: wind_turbine.dm:4
    private const float TURBINE_ANCHORED_POWER_PER_KPA = TURBINE_CHARGE_PER_TILE * 2; // tg: wind_turbine.dm:5
    private const float TURBINE_MIN_SECONDS_BETWEEN_SOUNDS = 0.33f * SECONDS; // tg: wind_turbine.dm:6

    // tg: code/game/objects/items/wind_turbine.dm:40  var/static/list/allowed_devices
    public static DmList allowed_devices => TgState.Current.obj_item_portable_wind_turbine__allowed_devices ??= typecacheof(Dm.list(
        typeof(obj_item_gun_energy),
        typeof(obj_item_compact_remote),
        typeof(obj_item_controller),
        typeof(obj_item_organ_cyberimp_bci),
        typeof(obj_item_integrated_circuit),
        typeof(obj_item_melee_baton_security),
        typeof(obj_item_modular_computer),
        typeof(obj_item_stock_parts_power_store_cell))) as DmList;

    // tg: code/game/objects/items/wind_turbine.dm:51
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_drag_pickup)));
        RegisterSignal(this, COMSIG_MOVABLE_SET_ANCHORED, nameof(on_anchor));
        RegisterSignal(this, COMSIG_ATOM_PRE_DIR_CHANGE, nameof(block_dir_changes_unanchored));
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/wind_turbine.dm:62
    public override object Destroy(float? force = null)
    {
        return base.Destroy(force);
    }

    ///Called when src is anchored
    // tg: code/game/objects/items/wind_turbine.dm:66
    [SignalHandler]
    public virtual void on_anchor(atom source = null, object is_anchored = null)
    {
        if (Dm.truthy(is_anchored))
        {
            RegisterSignal(this, COMSIG_MOVABLE_RESISTED_SPACEWIND, nameof(on_space_wind));
            UnregisterSignal(this, COMSIG_ATOM_PRE_DIR_CHANGE);
            Waiting.NoSleep(forceMove(get_turf(this)));
            pixel_x = 0;
            pixel_y = 0;
            Waiting.NoSleep(setDir(SOUTH));
        }
        else
        {
            UnregisterSignal(this, COMSIG_MOVABLE_RESISTED_SPACEWIND);
            RegisterSignal(this, COMSIG_ATOM_PRE_DIR_CHANGE, nameof(block_dir_changes_unanchored));
        }
    }

    ///Called when src changes direction when not equipped
    // tg: code/game/objects/items/wind_turbine.dm:81
    [SignalHandler]
    public virtual object block_dir_changes_unanchored(atom source = null, object old_dir = null, object new_dir = null)
    {
        return COMPONENT_ATOM_BLOCK_DIR_CHANGE;
    }

    ///Called when this resists space wind
    // tg: code/game/objects/items/wind_turbine.dm:87
    [SignalHandler]
    public virtual void on_space_wind(atom source = null, object pressure_difference = null, object pressure_direction = null)
    {
        obj_item_portable_wind_turbine turbine = source as obj_item_portable_wind_turbine;
        if (!Dm.truthy(turbine))
            return;
        if (cap == null)
            return;
        turbine.add_power(TURBINE_ANCHORED_POWER_PER_KPA * Dm.num(pressure_difference) * Dm.num(cap.rating), ignore_cap: TRUE);
        set_rotor_tick(rotor_tick + 1);
    }

    // tg: code/game/objects/items/wind_turbine.dm:98
    public override object update_appearance(float? updates = null)
    {
        object dot = base.update_appearance(updates);
        Waiting.NoSleep(update_back());
        return dot;
    }

    // tg: code/game/objects/items/wind_turbine.dm:102
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        update_appearance();
        if (Dm.truthy(Dm.bitand(slot, slot_flags)))
        {
            RegisterSignal(user, COMSIG_MOVABLE_MOVED, nameof(on_move));
            RegisterSignal(user, COMSIG_ATOM_POST_DIR_CHANGE, nameof(on_dir_change));
            RegisterSignal(user, COMSIG_LIVING_SUICIDE_ACT, nameof(on_suicide_act));
        }
        else
        {
            await setDir(SOUTH);
            UnregisterSignal(user, Dm.list(
                COMSIG_MOVABLE_MOVED,
                COMSIG_ATOM_POST_DIR_CHANGE,
                COMSIG_LIVING_SUICIDE_ACT));
        }
    }

    ///Called when the thing HOLDING the turbine changes direction
    // tg: code/game/objects/items/wind_turbine.dm:118
    [SignalHandler]
    public virtual void on_dir_change(datum source = null, object old_dir = null, object new_dir = null)
    {
        update_appearance();
    }

    // tg: code/game/objects/items/wind_turbine.dm:123
    [SignalHandler]
    public virtual object on_suicide_act(mob_living source = null)
    {
        if (Dm.truthy(source.get_active_held_item()))
            return NONE;
        return Waiting.NoSleep(suicide_act(source));
    }

    // tg: code/game/objects/items/wind_turbine.dm:130
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        obj_item_bodypart head = (user as mob_living_carbon)?.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;
        if (head == null)
            return NONE;

        playsound(user, Dm.resource("sound/items/weapons/bladeslice.ogg"), 50);
        user.visible_message(span_suicide(I18n.T("[] moves [] head in the way of []'s blades! It looks like [] trying to commit suicide!",
            user, user.p_their(), this, user.p_theyre())));
        user.set_suicide(TRUE);
        await user.apply_damage(75, BRUTE, BODY_ZONE_HEAD, wound_bonus: 100, forced: TRUE, sharpness: SHARP_EDGED, attacking_item: this);
        if (Dm.truthy(await head.dismember()))
        {
            await user.death(); // anti-ling check
            return MANUAL_SUICIDE;
        }

        user.visible_message(span_suicide(I18n.T("...but fails to separate [] head from [] body! Ouch!", user.p_their(), user.p_their())));
        return SHAME;
    }

    ///Updates the worn back icon for the current loc
    // tg: code/game/objects/items/wind_turbine.dm:148
    public virtual async DmTask update_back()
    {
        if (ishuman(loc))
        {
            mob_living_carbon_human human = (mob_living_carbon_human)loc;
            await human.update_worn_back();
        }
    }

    ///Tries to play the woosh sound effect. May not play it if it's been too soon since the last call.
    // tg: code/game/objects/items/wind_turbine.dm:154
    public virtual void try_playsound()
    {
        if ((world.time - last_sound_time) < TURBINE_MIN_SECONDS_BETWEEN_SOUNDS)
            return;
        playsound(this, Dm.resource("sound/machines/woosh.ogg"), 20, FALSE);
        last_sound_time = world.time;
    }

    ///Sets the rotor animation tick to a new value. Returns TRUE if the rotor made a full rotation.
    // tg: code/game/objects/items/wind_turbine.dm:161
    public virtual object set_rotor_tick(object new_tick = null)
    {
        float last_rotor_tick = floor(rotor_tick);
        rotor_tick = Dm.num(new_tick);
        float made_full_rotation = FALSE;
        if (rotor_tick >= TURBINE_ANIMATION_TICKS)
        {
            rotor_tick -= floor(rotor_tick / TURBINE_ANIMATION_TICKS) * TURBINE_ANIMATION_TICKS;
            try_playsound();
            made_full_rotation = TRUE;
        }
        float rounded_rotor_tick = floor(rotor_tick);
        if (rounded_rotor_tick != last_rotor_tick)
            update_appearance();
        return made_full_rotation;
    }

    ///Adds a certain amount of power to the internal buffer
    // tg: code/game/objects/items/wind_turbine.dm:175
    public virtual void add_power(object power = null, float? ignore_cap = null)
    {
        ignore_cap ??= FALSE;
        if (Dm.truthy(ignore_cap))
            available_power += Dm.num(power);
        else
            available_power = min(available_power + Dm.num(power), TURBINE_MAX_STORED_POWER * (cap?.rating == null ? 1 : Dm.num(cap.rating)));
    }

    ///Called when the thing HOLDING the turbine moves
    // tg: code/game/objects/items/wind_turbine.dm:182
    [SignalHandler]
    public virtual void on_move(atom thing = null, atom old_loc = null, object dir = null)
    {
        mob_living user = thing as mob_living;
        if (!Dm.truthy(user))
            return;
        if (cap == null)
            return;
        float distance = get_dist(old_loc, user.loc);
        turf_open open_turf = get_turf(user) as turf_open;
        if (!Dm.truthy(open_turf))
            return;
        float pressure_factor = Dm.num(open_turf.air.return_pressure()) / 101.0f;
        if (pressure_factor <= 0)
            return;
        object made_full_rotation = set_rotor_tick(rotor_tick + distance * TURBINE_ANIMATION_TICKS_PER_TILE * pressure_factor);
        add_power(distance * TURBINE_CHARGE_PER_TILE * pressure_factor * Dm.num(cap.rating));
        if (!Dm.truthy(made_full_rotation))
            return;
        if (!HAS_TRAIT(user, TRAIT_CLUMSY))
            return;
        obj_item_bodypart_head head_to_bash = (user as mob_living_carbon)?.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        if (!Dm.truthy(head_to_bash))
            return;
        Waiting.NoSleep(user.Paralyze(5));
        Waiting.NoSleep(user.Knockdown(10));
        user.visible_message(span_danger(I18n.T("[] gets whacked in the head by the []'s spinning blades!", user, this)), span_userdanger(I18n.T("You get hit in the head by the [] and fall over!", this)));
        // imaginary friends call sleep in their emotes
        // even though imaginary friends can't put on a wind turbine I still have to do this
        Waiting.Detach(user.emote("scream")); // INVOKE_ASYNC
        Waiting.NoSleep(user.apply_damage(5, BRUTE, head_to_bash, attacking_item: this));
        playsound(source: this, soundin: Dm.resource("sound/items/weapons/smash.ogg"), vol: get_clamped_volume(), vary: TRUE);
    }

    // tg: code/game/objects/items/wind_turbine.dm:215
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = NONE;
        object sw__1 = await default_unfasten_wrench(user, tool, 4 * SECONDS);
        if (Dm.num(sw__1) == SUCCESSFUL_UNFASTEN)
            return ITEM_INTERACT_SUCCESS;
        if (Dm.num(sw__1) == FAILED_UNFASTEN)
            return ITEM_INTERACT_BLOCKING;
        return dot;
    }

    // tg: code/game/objects/items/wind_turbine.dm:224
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        if (!in_range(user, this) && !issilicon(user) && !isobserver(user))
        {
            Dm.append(ref dot, span_warning(I18n.T("You're too far away to examine []'s contents! You can still watch it spin so wonderfully though...", this)));
            if (Dm.truthy(charging) && charging is obj_item_melee_baton_security)
                Dm.append(ref dot, span_info(I18n.T("You can see the [] hanging precariously off the charging port...", charging)));
            return dot;
        }

        if (Dm.truthy(cap))
            Dm.append(ref dot, span_info(I18n.T("Click it with a screwdriver to eject the [].", cap)));
        Dm.append(ref dot, span_info(I18n.T("Wrench it on a tile to anchor it and harness space wind.")));
        Dm.append(ref dot, span_info(I18n.T("The wind turbine is currently storing []kJ.", floor(available_power / 100) / 10)));
        if (Dm.truthy(charging))
            Dm.append(ref dot, Dm.text("[]\n[]", span_notice(I18n.T("\\The [] contains:", this)), span_notice(I18n.T("- \\A [].", charging))));
        return dot;
    }

    // tg: code/game/objects/items/wind_turbine.dm:241
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.screwdriver_act(user, tool);
        if (Dm.truthy(charging))
        {
            user.balloon_alert(user, I18n.T("remove the [] first!", charging));
            return FALSE;
        }
        if (Dm.truthy(cap))
        {
            tool.play_tool_sound(this, 50);
            user.balloon_alert(user, I18n.T("capacitor removed"));
            await cap.forceMove(drop_location() as atom);
            available_power = 0;
            cap = null;
            return TRUE;
        }
        else
        {
            user.balloon_alert(user, I18n.T("no capacitor!"));
            return FALSE;
        }
    }

    // tg: code/game/objects/items/wind_turbine.dm:257
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        if (Dm.truthy(is_type_in_typecache(arrived, allowed_devices)))
        {
            charging = (obj_item)arrived;
            START_PROCESSING(SSmachines, this);
            using_power = TRUE;
            update_appearance();
        }
        return await base.Entered(arrived, old_loc, old_locs);
    }

    // tg: code/game/objects/items/wind_turbine.dm:265
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (gone == charging)
        {
            if (!QDELING(charging))
                charging.update_appearance();
            charging = null;
            using_power = FALSE;
            update_appearance();
        }
        return await base.Exited(gone, direction);
    }

    // tg: code/game/objects/items/wind_turbine.dm:274
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_stock_parts_capacitor)
        {
            if (Dm.truthy(cap))
            {
                balloon_alert(user, I18n.T("already has a capacitor!"));
                return ITEM_INTERACT_BLOCKING;
            }
            await user.transferItemToLoc(tool, this);
            cap = (obj_item_stock_parts_capacitor)tool;
            balloon_alert(user, I18n.T("inserted the []", tool));
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(is_type_in_typecache(tool, allowed_devices)))
            return NONE;

        if (cap == null)
        {
            balloon_alert(user, I18n.T("no capacitor inserted!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(charging))
        {
            balloon_alert(user, I18n.T("already charging something!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (tool is obj_item_gun_energy energy_gun)
        {
            if (!Dm.truthy(energy_gun.can_charge))
            {
                balloon_alert(user, I18n.T("not rechargable!"));
                return ITEM_INTERACT_BLOCKING;
            }
        }
        await user.transferItemToLoc(tool, this);
        charging = tool;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/wind_turbine.dm:302
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (loc == user || (loc is turf && charging != null))
        {
            await take_charging_out(user);
            return TRUE;
        }
        add_fingerprint(user);
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/game/objects/items/wind_turbine.dm:309
    public override async DmTask handle_deconstruct(float? disassembled = null)
    {
        if (charging != null)
            await charging.forceMove(drop_location() as atom);
        if (cap != null)
            await cap.forceMove(drop_location() as atom);
        await base.handle_deconstruct(disassembled);
    }

    ///Takes charging item out if there is one
    // tg: code/game/objects/items/wind_turbine.dm:315
    public virtual async DmTask take_charging_out(mob user = null)
    {
        if (charging == null || Dm.truthy(await user.put_in_hands(charging)))
            return;
        await charging.forceMove(drop_location() as atom);
        update_appearance();
    }

    // tg: code/game/objects/items/wind_turbine.dm:321
    public override async DmTask<object> attack_tk(mob user = null)
    {
        if (charging == null)
            return null;
        await charging.forceMove(drop_location() as atom);
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/objects/items/wind_turbine.dm:327
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        using_power = FALSE;
        if (charging == null)
            return PROCESS_KILL;
        if (cap == null)
        {
            // this is easier than starting/stopping the process based on capacitor state
            // this code only charges the attached item, so stopping it early should not be a problem
            return null;
        }
        obj_item_stock_parts_power_store charging_cell = charging.get_cell() as obj_item_stock_parts_power_store;
        if (charging_cell == null && charging is obj_item_stock_parts_power_store_cell)
            charging_cell = (obj_item_stock_parts_power_store_cell)charging;
        if (charging_cell == null)
        {
            datum_component_shell shell = charging.GetComponent(typeof(datum_component_shell)) as datum_component_shell;
            charging_cell = shell?.attached_circuit?.get_cell() as obj_item_stock_parts_power_store;
        }
        if (charging_cell != null)
        {
            float wanted_power = min(charging_cell.maxcharge - charging_cell.charge, charging_cell.chargerate);
            if (wanted_power > 0)
            {
                float power_to_give = min(available_power, wanted_power) * Dm.num(seconds_per_tick) / 2;
                if (power_to_give > 0)
                {
                    Waiting.NoSleep(charging_cell.give(power_to_give));
                    available_power -= power_to_give;
                    if (charging_cell.charge == charging_cell.maxcharge)
                    {
                        playsound(this, Dm.resource("sound/machines/ping.ogg"), 30, TRUE);
                        Waiting.NoSleep(say(I18n.T("[] has finished recharging!", charging)));
                    }
                    else
                        using_power = TRUE;
                }
            }
            update_appearance();
        }
        return null;
    }

    // tg: code/game/objects/items/wind_turbine.dm:355
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_CONTENTS)))
            return dot;

        if (charging is obj_item_gun_energy energy_gun)
        {
            if (energy_gun?.cell != null)
                await energy_gun.cell.emp_act(severity);
        }
        else if (charging is obj_item_melee_baton_security batong)
        {
            if (batong?.cell != null)
                batong.cell.charge = 0;
        }
        return dot;
    }

    // tg: code/game/objects/items/wind_turbine.dm:368
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        mutable_appearance rotor = mutable_appearance(worn_icon, Dm.text("rotor_[]", floor(rotor_tick))) as mutable_appearance;
        rotor.pixel_y -= 8;
        Dm.append(ref dot, rotor);
        if (charging == null)
            return dot;
        if (charging is obj_item_melee_baton_security)
            Dm.append(ref dot, mutable_appearance(icon, "baton"));
        return dot;
    }

    // tg: code/game/objects/items/wind_turbine.dm:378
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        isinhands ??= FALSE;
        bodyshape ??= NONE;
        DmList dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape) as DmList;
        if (Dm.truthy(isinhands))
            return dot;
        mutable_appearance rotor = mutable_appearance(worn_icon, Dm.text("rotor_[]", floor(rotor_tick))) as mutable_appearance;
        rotor.layer = BELOW_MOB_LAYER;
        if (ishuman(loc))
        {
            mob_living_carbon_human user = (mob_living_carbon_human)loc;
            if (Dm.truthy(Dm.bitand(user.dir, NORTH)))
            {
                mutable_appearance just_render_above_other_things = mutable_appearance(worn_icon, worn_icon_state as string) as mutable_appearance;
                just_render_above_other_things.layer = ABOVE_MOB_LAYER;
                rotor.layer = ABOVE_MOB_LAYER;
                Dm.append(ref dot, just_render_above_other_things);
            }
        }
        Dm.append(ref dot, rotor);
        if (charging == null)
            return dot;
        if (charging is obj_item_melee_baton_security)
        {
            mutable_appearance baton_overlay = mutable_appearance(icon, "baton") as mutable_appearance;
            Dm.append(ref dot, baton_overlay);
        }
        return dot;
    }
}

// tg: code/game/objects/items/wind_turbine.dm:58
public partial class obj_item_portable_wind_turbine_loaded : obj_item_portable_wind_turbine
{
    // tg: code/game/objects/items/wind_turbine.dm:58
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        cap = Dm.@new<obj_item_stock_parts_capacitor>(this);
        return dot;
    }
}

public sealed partial class TgState
{
    internal DmList obj_item_portable_wind_turbine__allowed_devices;
}
