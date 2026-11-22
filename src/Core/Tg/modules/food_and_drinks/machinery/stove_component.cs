// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/machinery/stove_component.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the burners of the stove and the range. The var block is generated (D111).

/**
 * # Stove Component
 *
 * Makes the attached object a stove
 *
 * Pots can be put on the stove to make soup, and attack-handing it will start processing
 * where it will heat up the pot's reagents inside
 */
// tg: code/modules/food_and_drinks/machinery/stove_component.dm:9
public partial class datum_component_stove : datum_component
{
    // tg: code/modules/food_and_drinks/machinery/stove_component.dm:28
    [DmParams("container_x", "container_y", "spawn_container")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object container_x = Dm.arg(__extra, 0) ?? 0;
        object container_y = Dm.arg(__extra, 1) ?? 8;
        obj_item spawn_container = Dm.arg(__extra, 2) as obj_item;
        if (!ismachinery(parent))
            return COMPONENT_INCOMPATIBLE;

        this.container_x = Dm.num(container_x);
        this.container_y = Dm.num(container_y);

        // To allow maploaded pots on top of your stove.
        if (spawn_container != null)
        {
            await spawn_container.forceMove(parent as atom);
            add_container(spawn_container);
        }

        soup_sound = Dm.@new<datum_looping_sound_soup>(parent);
        return null;
    }

    // tg: code/modules/food_and_drinks/machinery/stove_component.dm:42
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref soup_sound);
        return base.Destroy(force);
    }

    // tg: code/modules/food_and_drinks/machinery/stove_component.dm:46
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_ATTACKBY, nameof(on_attackby));
        RegisterSignal(parent, COMSIG_ATOM_ATTACK_HAND_SECONDARY, nameof(on_attack_hand_secondary));
        RegisterSignal(parent, COMSIG_ATOM_ATTACK_ROBOT_SECONDARY, nameof(on_attack_robot_secondary));
        RegisterSignal(parent, COMSIG_ATOM_EXITED, nameof(on_exited));
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(on_overlay_update));
        RegisterSignal(parent, COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM, nameof(on_requesting_context));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(parent, COMSIG_MACHINERY_REFRESH_PARTS, nameof(on_refresh_parts));

        obj_machinery real_parent = (obj_machinery)parent;
        Dm.combine(ref real_parent.flags_1, HAS_CONTEXTUAL_SCREENTIPS_1);
    }

    // tg: code/modules/food_and_drinks/machinery/stove_component.dm:59
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        obj_machinery real_parent = (obj_machinery)parent;
        if (container != null && !QDELING(parent))
            await container.forceMove(real_parent.drop_location() as atom);

        if (particle_type != null)
            real_parent.remove_shared_particles(Dm.text("[]_stove_[]", particle_type, container_x));

        UnregisterSignal(parent, Dm.list(
            COMSIG_ATOM_ATTACK_HAND_SECONDARY,
            COMSIG_ATOM_EXITED,
            COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM,
            COMSIG_ATOM_UPDATE_OVERLAYS,
            COMSIG_ATOM_ATTACKBY,
            COMSIG_ATOM_EXAMINE,
            COMSIG_MACHINERY_REFRESH_PARTS
        ));
    }

    // tg: code/modules/food_and_drinks/machinery/stove_component.dm:77
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        obj_machinery real_parent = (obj_machinery)parent;
        if (Dm.truthy(Dm.bitand(real_parent.machine_stat, NOPOWER)))
        {
            turn_off();
            return null;
        }

        if (container != null)
            Waiting.NoSleep(((datum_reagents)container.reagents).expose_temperature(SOUP_BURN_TEMP + 80, heat_coefficient));
        Waiting.NoSleep(real_parent.use_energy(real_parent.active_power_usage));

        object stove_spot = real_parent.loc;
        if (isturf(stove_spot))
            ((turf)stove_spot).hotspot_expose(SOUP_BURN_TEMP + 80, 100);
        return null;
    }

    // tg: code/modules/food_and_drinks/machinery/stove_component.dm:90
    public virtual void turn_on()
    {
        obj_machinery real_parent = (obj_machinery)parent;
        if (Dm.truthy(Dm.bitand(real_parent.machine_stat, BROKEN | NOPOWER)))
            return;
        START_PROCESSING(SSmachines, this);
        on = TRUE;
        real_parent.update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/modules/food_and_drinks/machinery/stove_component.dm:98
    public virtual void turn_off()
    {
        obj_machinery real_parent = (obj_machinery)parent;
        STOP_PROCESSING(SSmachines, this);
        on = FALSE;
        real_parent.update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/modules/food_and_drinks/machinery/stove_component.dm:104
    [SignalHandler]
    public virtual object on_attack_hand_secondary(obj_machinery source = null)
    {
        toggle_mode();

        return COMPONENT_SECONDARY_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/food_and_drinks/machinery/stove_component.dm:111
    [SignalHandler]
    public virtual object on_attack_robot_secondary(obj_machinery source = null)
    {
        toggle_mode();

        return COMPONENT_SECONDARY_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/food_and_drinks/machinery/stove_component.dm:118
    public virtual void toggle_mode()
    {
        obj_machinery real_parent = (obj_machinery)parent;
        if (Dm.truthy(on))
            turn_off();
        else if (Dm.truthy(Dm.bitand(real_parent.machine_stat, BROKEN | NOPOWER)))
        {
            real_parent.balloon_alert_to_viewers(I18n.T("no power!"));
            return;
        }
        else
            turn_on();

        real_parent.balloon_alert_to_viewers(I18n.T("burners []", Dm.truthy(on) ? I18n.T("on") : I18n.T("off")));
        playsound(real_parent, Dm.resource("sound/machines/click.ogg"), 30, TRUE);
        playsound(real_parent, Dm.truthy(on) ? Dm.resource("sound/items/tools/welderactivate.ogg") : Dm.resource("sound/items/tools/welderdeactivate.ogg"), 15, TRUE);
    }

    // tg: code/modules/food_and_drinks/machinery/stove_component.dm:134
    [SignalHandler]
    public virtual object on_attackby(obj_machinery source = null, obj_item attacking_item = null, mob user = null, object @params = null)
    {
        if (source is obj_machinery_oven_range && attacking_item is obj_item_storage_bag_tray && container != null)
        {
            obj_machinery_oven_range range = (obj_machinery_oven_range)source;
            obj_item_reagent_containers_cup_soup_pot soup_pot = container as obj_item_reagent_containers_cup_soup_pot;

            if (!Dm.truthy(range.open))
            {
                soup_pot.transfer_from_container_to_pot(attacking_item, user);
                return COMPONENT_NO_AFTERATTACK;
            }
        }

        if (!Dm.truthy(attacking_item.is_open_container()))
            return null;
        if (container != null)
        {
            to_chat(user, span_warning(I18n.T("You wouldn't dare try to cook two things on the same stove simultaneously. What if it cross contaminates?")));
            return COMPONENT_NO_AFTERATTACK;
        }

        if (Dm.truthy(Waiting.NoSleep(user.transferItemToLoc(attacking_item, parent))))
        {
            add_container(attacking_item, user);
            to_chat(user, span_notice(I18n.T("You put [] onto [].", attacking_item, parent)));
        }
        return COMPONENT_NO_AFTERATTACK;
    }

    // tg: code/modules/food_and_drinks/machinery/stove_component.dm:157
    [SignalHandler]
    public virtual void on_exited(obj_machinery source = null, atom_movable gone = null, object direction = null)
    {
        if (gone == container)
            remove_container();
    }

    // tg: code/modules/food_and_drinks/machinery/stove_component.dm:163
    [SignalHandler]
    public virtual void on_overlay_update(obj_machinery source = null, DmList overlays = null)
    {
        update_smoke();

        if (!Dm.truthy(on))
            return;

        obj real_parent = (obj)parent;

        // Flames around the pot
        mutable_appearance flames = mutable_appearance(real_parent.icon, Dm.text("[]_on_flame", real_parent.base_icon_state), alpha: real_parent.alpha) as mutable_appearance;
        flames.color = flame_color;
        Dm.append(ref overlays, flames);
        // DEVIATION(DV12): overlays += emissive_appearance(real_parent.icon, "[base_icon_state]_on_flame", real_parent, alpha = real_parent.alpha), the glow on the emissive plane, is not created

        // A green light that shows it's active
        mutable_appearance light = mutable_appearance(real_parent.icon, Dm.text("[]_on_overlay", real_parent.base_icon_state), alpha: real_parent.alpha) as mutable_appearance;
        light.color = "#00ff00";
        Dm.append(ref overlays, light);
        // DEVIATION(DV12): overlays += emissive_appearance(real_parent.icon, "[base_icon_state]_on_overlay", real_parent, alpha = real_parent.alpha) is not created
    }

    // tg: code/modules/food_and_drinks/machinery/stove_component.dm:185
    [SignalHandler]
    public virtual object on_requesting_context(obj_machinery source = null, DmList context = null, obj_item held_item = null)
    {
        if (held_item == null)
        {
            context[SCREENTIP_CONTEXT_RMB] = Dm.text("Turn [] burner", Dm.truthy(on) ? "off" : "on");
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (Dm.truthy(held_item.is_open_container()))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Place container";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return null;
    }

    // tg: code/modules/food_and_drinks/machinery/stove_component.dm:196
    [SignalHandler]
    public virtual void on_examine(obj_machinery source = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(ref examine_list, span_notice(I18n.T("You can turn the stovetop burners [] with <i>right click</i>.", Dm.truthy(on) ? I18n.T("off") : I18n.T("on"))));
    }

    // tg: code/modules/food_and_drinks/machinery/stove_component.dm:201
    [SignalHandler]
    public virtual void on_refresh_parts(obj_machinery source = null)
    {
        float new_multiplier = 0;
        foreach (datum_stock_part_micro_laser part in Dm.each<datum_stock_part_micro_laser>(source.component_parts))
            new_multiplier += Dm.num(part.tier) * 0.5f;

        heat_coefficient = Dm.initial(this, (datum_component_stove s) => s.heat_coefficient) * max(round(new_multiplier), 1);
    }

    // tg: code/modules/food_and_drinks/machinery/stove_component.dm:210
    public virtual void add_container(obj_item new_container = null, mob user = null)
    {
        obj real_parent = (obj)parent;
        Dm.append(real_parent.vis_contents, new_container);
        ADD_TRAIT(new_container, TRAIT_SKIP_BASIC_REACH_CHECK, REF(this));
        new_container.vis_flags = Dm.bitor(new_container.vis_flags, VIS_INHERIT_PLANE);

        container = new_container;
        container.pixel_x = container_x;
        container.pixel_y = container_y;

        update_smoke_type();
        RegisterSignal(container.reagents as datum, COMSIG_REAGENTS_TEMP_CHANGE, nameof(update_smoke_type));
        real_parent.update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/modules/food_and_drinks/machinery/stove_component.dm:224
    public virtual void remove_container()
    {
        obj real_parent = (obj)parent;
        REMOVE_TRAIT(container, TRAIT_SKIP_BASIC_REACH_CHECK, REF(this));
        container.vis_flags = Dm.bitand(container.vis_flags, Dm.bitnot(VIS_INHERIT_PLANE));
        Dm.remove(real_parent.vis_contents, container);

        UnregisterSignal(container.reagents as datum, COMSIG_REAGENTS_TEMP_CHANGE);

        container.pixel_x = container.base_pixel_x;
        container.pixel_y = container.base_pixel_y;
        container = null;

        update_smoke_type();
        real_parent.update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/modules/food_and_drinks/machinery/stove_component.dm:239
    [SignalHandler]
    public virtual void update_smoke_type(datum source = null, params object[] __extra)
    {
        float existing_temp = container != null ? ((datum_reagents)container.reagents).chem_temp : 0; // container?.reagents.chem_temp || 0
        Type old_type = particle_type;
        if (existing_temp >= SOUP_BURN_TEMP)
            particle_type = typeof(particles_smoke_steam_bad);
        else if (existing_temp >= WATER_BOILING_POINT)
            particle_type = typeof(particles_smoke_steam_mild);
        else
            particle_type = null;

        update_smoke(old_type);
    }

    // tg: code/modules/food_and_drinks/machinery/stove_component.dm:253
    public virtual void update_smoke(object old_type = null)
    {
        obj obj_parent = (obj)parent;

        if (Dm.truthy(old_type))
            obj_parent.remove_shared_particles(Dm.text("[]_stove_[]", old_type, container_x));

        if (!Dm.truthy(on) || !Dm.truthy(container != null ? ((datum_reagents)container.reagents).total_volume : 0))
        {
            soup_sound?.stop();
            if (particle_type != null)
                obj_parent.remove_shared_particles(Dm.text("[]_stove_[]", particle_type, container_x));
            return;
        }

        soup_sound.start();
        if (particle_type == null)
            return;
        obj_effect_abstract_shared_particle_holder soup_smoke = obj_parent.add_shared_particles(particle_type, Dm.text("[]_stove_[]", particle_type, container_x)) as obj_effect_abstract_shared_particle_holder;
        // soup_smoke.particles.position = list(container_x, round(ICON_SIZE_Y * 0.66), 0): BYOND's /particles vars are not declared
        // (particles.builtin.cs) and particles are not drawn
        Dm.Deferred("code/modules/food_and_drinks/machinery/stove_component.dm:269", "update_smoke: the steam particles' position over the pot (BYOND's /particles vars are not declared; particles are not drawn)");
    }
}
