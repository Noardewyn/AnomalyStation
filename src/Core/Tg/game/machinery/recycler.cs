// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/recycler.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H1: the whole file (the recycler: it eats what enters from its back, recycles items into the station's materials (its sheets
// drop on its tile), deconstructs other objects, crushes the living when emagged and stops for them when not, its safety mode,
// parts, lights and the blood on it; the old crusher of the derelicts; the janitor's garbage duty guide). The var blocks are
// generated (D111).

// tg: code/game/machinery/recycler.dm:3
public partial class obj_machinery_recycler : obj_machinery
{
    // tg: code/game/machinery/recycler.dm:1  #define SAFETY_COOLDOWN 100
    private const float SAFETY_COOLDOWN = 100;

    private static ref DmList post_machine_initialize__loc_connections => ref TgState.Current.obj_machinery_recycler__post_machine_initialize__loc_connections;

    // tg: code/game/machinery/recycler.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        materials = Dm.@new<datum_material_container>(
            this,
            SSmaterials.get_materials_by_flag(MATERIAL_SILO_STORED),
            INFINITY,
            MATCONTAINER_NO_INSERT);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_butchering_recycler),
            Dm.named("speed", 0.1f * SECONDS),
            Dm.named("effectiveness", amount_produced),
            Dm.named("bonus_modifier", amount_produced / 5)));
        base.Initialize(mapload, __extra);
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/game/machinery/recycler.dm:38
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        update_appearance();
        // a room with no SSid_access (a toy room's sweep; in tg it always initializes first), as the control wand: no access list
        if (SSid_access?.accesses_by_region != null)
            req_one_access = SSid_access.get_region_access_list(Dm.list(REGION_ALL_STATION, REGION_CENTCOM)) as DmList;
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), post_machine_initialize__loc_connections));
    }

    // tg: code/game/machinery/recycler.dm:47
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref materials);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/recycler.dm:51
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        float amt_made = 0;
        foreach (datum_stock_part_servo servo in Dm.each<datum_stock_part_servo>(component_parts))
            amt_made = 12.5f * Dm.num(servo.tier); //% of materials salvaged
        amount_produced = min(50, amt_made) + 50;
        var butchering = GetComponent(typeof(datum_component_butchering_recycler)) as datum_component_butchering;
        butchering.effectiveness = amount_produced;
        butchering.bonus_modifier = amount_produced / 5;
        return dot;
    }

    // tg: code/game/machinery/recycler.dm:61
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("Reclaiming <b>[]%</b> of materials salvaged.", amount_produced)));
        Dm.append(ref dot, I18n.T("The power light is [].\n\tThe safety-mode light is [].\n\tThe safety-sensors status light is [].",
            Dm.truthy(Dm.bitand(machine_stat, NOPOWER)) ? I18n.T("off") : I18n.T("on"),
            Dm.truthy(safety_mode) ? I18n.T("on") : I18n.T("off"),
            Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) ? I18n.T("off") : I18n.T("on")));
        return dot;
    }

    // tg: code/game/machinery/recycler.dm:68
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/recycler.dm:72
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/game/machinery/recycler.dm:75
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/recycler.dm:78
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        if (Dm.truthy(safety_mode))
        {
            safety_mode = FALSE;
            update_appearance();
        }
        playsound(this, SFX_SPARKS, 75, TRUE, SILENCED_SOUND_EXTRARANGE);
        balloon_alert(user, "safeties disabled");
        await DmTask.CompletedTask;
        return FALSE;
    }

    // tg: code/game/machinery/recycler.dm:89
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(panel_open))
            icon_state = base_icon_state + "Open";
        else
            icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(is_operational) && !Dm.truthy(safety_mode) ? 1 : 0);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/recycler.dm:96
    public override async DmTask on_set_is_operational(object old_value = null)
    {
        update_appearance();
        await DmTask.CompletedTask;
    }

    // tg: code/game/machinery/recycler.dm:99
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (!Dm.truthy(bloody) || !Dm.truthy(GET_ATOM_BLOOD_DECAL_LENGTH(this)))
            return dot;

        var blood_overlay = (mutable_appearance)mutable_appearance(icon, Dm.text("[]bld", icon_state), appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART));
        blood_overlay.color = get_blood_dna_color();
        Dm.append(ref dot, blood_overlay);
        return dot;
    }

    // tg: code/game/machinery/recycler.dm:108
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        if (!Dm.truthy(anchored))
            return dot;
        if (Dm.eq(border_dir, dir))
            return TRUE;
        return dot;
    }

    // tg: code/game/machinery/recycler.dm:115
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable enterer = null, object old_loc = null)
    {
        Waiting.Detach(eat(enterer)); // INVOKE_ASYNC
    }

    // tg: code/game/machinery/recycler.dm:120
    public virtual async DmTask eat(atom_movable morsel = null, float? sound = null)
    {
        sound ??= TRUE;
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER))) || Dm.truthy(safety_mode))
            return;
        if (!isturf(morsel.loc))
        {
            stack_trace(Dm.text("on_entered() called with invalid location: []", morsel.loc), "code/game/machinery/recycler.dm", 125); // I don't know how you called Entered() but stop it.
            return;
        }
        if (Dm.truthy(Dm.bitand(morsel.resistance_flags, INDESTRUCTIBLE)))
            return;

        /// Queue of objects to process.
        DmList to_eat = Dm.list(morsel);
        /// Regular items to be recycled.
        DmList nom = Dm.list();
        /// Living mobs to be crushed.
        DmList crunchy_nom = Dm.list(); // Mobs have to be handled differently so they get a different list instead of checking them multiple times.
        /// Count of items that couldn't be processed.
        float not_eaten = 0;

        while (Dm.truthy(LAZYLEN(to_eat)))
        {
            var thing = popleft(to_eat) as atom_movable;

            if (Dm.truthy(triggers_safety_shutdown(thing)))
            {
                emergency_stop();
                return;
            }

            if (Dm.truthy(Dm.bitand(thing.resistance_flags, INDESTRUCTIBLE)))
            {
                if (!isturf(thing.loc) && recursive_loc_check(thing, typeof(mob_living)) == null)
                    await thing.forceMove(loc);
                not_eaten++;
                continue;
            }

            if (Dm.truthy(Dm.bitand(thing.flags_1, HOLOGRAM_1)))
            {
                foreach (atom_movable hologram_content in Dm.each<atom_movable>(thing.contents.Copy()))
                    await hologram_content.forceMove(loc); // we shouldn't qdel() the non-holographic content of the hologram.
                visible_message(span_notice(I18n.T("[] fades away!", thing)));
                qdel(thing);
                continue;
            }

            if (isliving(thing))
            {
                LAZYADD(ref crunchy_nom, thing);
                if (!issilicon(thing))
                    LAZYOR(ref to_eat, thing.contents);
                continue;
            }

            if (!isobj(thing))
            {
                not_eaten++;
                continue;
            }

            if (thing is obj_item item_thing)
            {
                if (Dm.truthy(Dm.bitand(item_thing.item_flags, ABSTRACT)))
                {
                    not_eaten++;
                    continue;
                }
            }

            if (thing is obj_structure_closet closet_thing)
            {
                if (Dm.truthy(closet_thing.secure) && Dm.truthy(closet_thing.locked)) // Prevent blindly deconstructing locked secure closets (head closets, important departmental orders, etc.)
                {
                    not_eaten++;                                // unless they have already been unlocked to prevent exploiting the recycler to bypass closet access.
                    continue;
                }
            }

            LAZYADD(ref nom, thing);
            LAZYOR(ref to_eat, thing.contents);
        }

        foreach (mob_living living_mob in Dm.each<mob_living>(crunchy_nom))
        {
            if (!Dm.truthy(is_operational)) //we ran out of power after recycling a large amount to living stuff, time to stop
                break;
            if (Dm.truthy(living_mob.incorporeal_move))
                continue;

            await crush_living(living_mob);
            await use_energy(active_power_usage);
        }

        float nom_length = LAZYLEN(nom);

        /**
         * we process the list in reverse so that atoms without parents/contents are deleted first & their parents are deleted next & so on.
         * this is the reverse order in which get_all_contents() returns its list
         * if we delete an atom containing stuff then all its stuff are deleted with it as well so we will end recycling deleted items down the list and gain nothing from them
         */
        for (int i = (int)nom_length; i >= 1; i--)
        {
            if (!Dm.truthy(is_operational)) //we ran out of power after recycling a large amount to items, time to stop
                break;

            bool full_power_usage = true;
            var nom_obj = nom[i] as obj;

            if (nom_obj is obj_item nom_item)
            {
                // Whether or not items consume full power depends on if they produced a material when recycled.
                full_power_usage = await recycle_item(nom_item);
            }
            else
            {
                // When a non-item is eaten, we deconstruct it with dismantled = FALSE so that
                // it and its contents aren't just deleted. These always consume full power.
                await nom_obj.deconstruct(FALSE);
            }

            await use_energy(Dm.num(active_power_usage) / (full_power_usage ? 1 : 2));
        }

        if (Dm.truthy(nom_length) && Dm.truthy(sound))
        {
            float sound_volume = clamp(nom_length * 5, 50, 100);
            float walls_ignoring = max(nom_length - 10, 0);
            playsound(this, item_recycle_sound, sound_volume, TRUE, nom_length, ignore_walls: walls_ignoring); // As a substitute for playing 50 sounds at once.
        }

        if (Dm.truthy(not_eaten))
        {
            float sound_volume = clamp(not_eaten * 5, 50, 100);
            float walls_ignoring = max(not_eaten - 10, 0);
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), sound_volume, FALSE, not_eaten, ignore_walls: walls_ignoring); // Ditto.
        }
    }

    /// Determines if the target should trigger an emergency stop due to safety concerns.
    // tg: code/game/machinery/recycler.dm:227
    public virtual object triggers_safety_shutdown(atom_movable target = null)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE; // Emagged recycler ignores all safety checks.

        if (isliving(target))
            return TRUE;

        if (isbrain(target) || target is obj_item_dullahan_relay)
            return TRUE;

        if (target is obj_item_brain_processor_organic mmi_thing)
            return mmi_thing.brain != null ? TRUE : FALSE;

        return FALSE;
    }

    // tg: code/game/machinery/recycler.dm:243
    public virtual async DmTask<bool> recycle_item(obj_item target = null)
    {
        if (target is obj_item_grown_log wood)
        {
            float seed_modifier = wood.seed is obj_item_seeds wood_seed ? round(Dm.num(wood_seed.potency) / 25) : 0;
            Dm.@new(wood.plank_type, loc, 1 + seed_modifier);
            qdel(target);
            return true;
        }

        float retrieved = Dm.num(await materials.insert_item(target, multiplier: amount_produced / 100));
        if (retrieved > 0) //item was salvaged i.e. deleted
        {
            await materials.retrieve_all();
            qdel(target);
            return true;
        }

        qdel(target);
        return false;
    }

    // tg: code/game/machinery/recycler.dm:260
    public virtual void emergency_stop()
    {
        playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, FALSE);
        safety_mode = TRUE;
        update_appearance();
        _addtimer(Dm.@new<datum_callback>(this, nameof(reboot)), SAFETY_COOLDOWN, file: "code/game/machinery/recycler.dm", line: 264);
    }

    // tg: code/game/machinery/recycler.dm:266
    public virtual void reboot()
    {
        playsound(this, Dm.resource("sound/machines/ping.ogg"), 50, FALSE);
        safety_mode = FALSE;
        update_appearance();
    }

    // tg: code/game/machinery/recycler.dm:271
    public virtual async DmTask crush_living(mob_living living_mob = null)
    {
        if (issilicon(living_mob))
            playsound(this, Dm.resource("sound/items/tools/welder.ogg"), 50, TRUE);
        else
            playsound(this, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);

        if (iscarbon(living_mob) && !IS_UNCONSCIOUS_OR_CRIT(living_mob))
            await living_mob.say("ARRRRRRRRRRRGH!!!", forced: "recycler grinding");

        if (!issilicon(living_mob))
        {
            await add_mob_blood(living_mob);
            bloody = TRUE;
        }

        // Instantly lie down, also go unconscious from the pain, before you die.
        await living_mob.Unconscious(100);
        await living_mob.adjust_brute_loss(crush_damage);
        update_appearance();
    }

    // tg: code/game/machinery/recycler.dm:289
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        safety_mode = TRUE;
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/game/machinery/recycler.dm:292
public partial class obj_machinery_recycler_deathtrap : obj_machinery_recycler
{
    // tg: code/game/machinery/recycler.dm:297
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_blocker), TOOL_SCREWDRIVER));
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_blocker), TOOL_CROWBAR));
        return dot;
    }
}

// tg: code/datums/components/butchering.dm:491
public partial class datum_component_butchering_recycler : datum_component_butchering
{
    private static ref DmList Initialize__loc_connections => ref TgState.Current.datum_component_butchering_recycler__Initialize__loc_connections;

    // tg: code/datums/components/butchering.dm:493
    [DmParams("speed", "effectiveness", "bonus_modifier", "butcher_sound", "disabled", "can_be_blunt", "butcher_callback")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (parent is not obj_machinery_recycler) //EWWW
            return COMPONENT_INCOMPATIBLE;
        object dot = await base.Initialize(__extra);
        if (Dm.eq(dot, COMPONENT_INCOMPATIBLE))
            return dot;

        await AddComponent(typeof(datum_component_connect_loc_behalf), parent, Initialize__loc_connections);
        return dot;
    }

    // tg: code/datums/components/butchering.dm:513
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        if (!isliving(arrived))
            return;
        var victim = (mob_living)arrived;
        var eater = (obj_machinery_recycler)parent;
        if (Dm.truthy(eater.safety_mode) || Dm.truthy(Dm.bitand(eater.machine_stat, Dm.bitor(BROKEN, NOPOWER)))) //I'm so sorry.
            return;
        if (victim.stat == DEAD && (Dm.truthy(victim.butcher_results) || Dm.truthy(victim.guaranteed_butcher_results)))
            Waiting.Detach(on_butchering(parent as atom, victim));
    }
}

public sealed partial class TgState
{
    // tg: code/game/machinery/recycler.dm:42  var/static/list/loc_connections
    internal DmList obj_machinery_recycler__post_machine_initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_machinery_recycler.on_entered)));

    // tg: code/datums/components/butchering.dm:508  var/static/list/loc_connections
    internal DmList datum_component_butchering_recycler__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(datum_component_butchering_recycler.on_entered)));
}
