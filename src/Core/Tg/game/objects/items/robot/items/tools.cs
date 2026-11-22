// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/robot/items/tools.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2, D326) but the omnitool's context tips (add_context: screentips, deferred by the owner, D264; a
// live-partial row): the plating repair tool, the peacekeeper's hyperkinetic dampening projector and the omni-toolsets. The var
// blocks are generated (D111).

//Peacekeeper Cyborg Projectile Dampenening Field
// tg: code/game/objects/items/robot/items/tools.dm:17
public partial class obj_item_borg_projectile_dampen : obj_item_borg
{
    private const float PKBORG_DAMPEN_CYCLE_DELAY = 2 * SECONDS;
    private const float POWER_RECHARGE_CYBORG_DRAIN_MULTIPLIER = 0.0004f * STANDARD_CELL_RATE;

    // tg: code/game/objects/items/robot/items/tools.dm:51
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        START_PROCESSING(SSfastprocess, this);
        host = loc as mob_living_silicon_robot;
        RegisterSignal(host, COMSIG_LIVING_DEATH, nameof(on_death));
        return base.Initialize(mapload, __extra);
    }

    // tg: code/game/objects/items/robot/items/tools.dm:57
    [SignalHandler]
    public virtual void on_death(datum source = null, object gibbed = null)
    {
        deactivate_field();
    }

    // tg: code/game/objects/items/robot/items/tools.dm:62
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSfastprocess, this);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/robot/items/tools.dm:66
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!(cycle_cooldown <= world.time)) // COOLDOWN_FINISHED, inline
        {
            to_chat(user, span_boldwarning(I18n.T("[] is still recycling its projectors!", this)));
            return null;
        }
        cycle_cooldown = world.time + PKBORG_DAMPEN_CYCLE_DELAY; // COOLDOWN_START, inline
        if (!Dm.truthy(active))
        {
            if (!Dm.truthy(user.has_buckled_mobs()))
                await activate_field();
            else
                to_chat(user, span_warning(I18n.T("[]'s safety cutoff prevents you from activating it due to living beings being ontop of you!", this)));
        }
        else
            deactivate_field();
        update_appearance();
        to_chat(user, span_boldnotice(Dm.truthy(active) ? I18n.T("You activate [].", this) : I18n.T("You deactivate [].", this)));
        return null;
    }

    // tg: code/game/objects/items/robot/items/tools.dm:81
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", base_icon_state, active);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/robot/items/tools.dm:85
    public virtual async DmTask activate_field()
    {
        if (dampening_field is datum_proximity_monitor_advanced_bubble_projectile_dampener_peaceborg)
            QDEL_NULL(ref dampening_field);
        mob_living_silicon_robot owner = get_host() as mob_living_silicon_robot;
        dampening_field = Dm.@new<datum_proximity_monitor_advanced_bubble_projectile_dampener_peaceborg>(owner, field_radius, TRUE, this, typeof(datum_dampener_projectile_effects_peacekeeper));
        RegisterSignal(dampening_field, COMSIG_DAMPENER_CAPTURE, nameof(dampen_projectile));
        RegisterSignal(dampening_field, COMSIG_DAMPENER_RELEASE, nameof(restore_projectile));
        if (owner?.model != null)
            owner.model.allow_riding = FALSE;
        active = TRUE;
    }

    // tg: code/game/objects/items/robot/items/tools.dm:95
    public virtual void deactivate_field()
    {
        QDEL_NULL(ref dampening_field);
        visible_message(span_warning(I18n.T("\\The [] shuts off!", this)));
        tracked_bullet_cost.Cut();
        active = FALSE;

        mob_living_silicon_robot owner = get_host() as mob_living_silicon_robot;
        if (owner != null)
            owner.model.allow_riding = TRUE;
    }

    // tg: code/game/objects/items/robot/items/tools.dm:105
    public virtual object get_host()
    {
        if (host is mob_living_silicon_robot)
            return host;
        else
        {
            if (iscyborg(host.loc))
                return host.loc;
        }
        return null;
    }

    // tg: code/game/objects/items/robot/items/tools.dm:113
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        host = loc as mob_living_silicon_robot;
        await base.equipped__proc(user, slot, initial);
    }

    // tg: code/game/objects/items/robot/items/tools.dm:117
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        host = loc as mob_living_silicon_robot;
        return await base.dropped(user, silent, __extra);
    }

    // tg: code/game/objects/items/robot/items/tools.dm:121
    public override async DmTask cyborg_unequip(mob user = null)
    {
        deactivate_field();
        await base.cyborg_unequip(user);
    }

    // tg: code/game/objects/items/robot/items/tools.dm:125
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        Waiting.NoSleep(process_recharge(seconds_per_tick));
        process_usage(seconds_per_tick);
        return null;
    }

    // tg: code/game/objects/items/robot/items/tools.dm:129
    public virtual void process_usage(object seconds_per_tick = null)
    {
        float usage = 0;
        foreach (object projectile in Dm.each(tracked_bullet_cost))
        {
            usage += projectile_tick_speed_ecost * Dm.num(seconds_per_tick);
            usage += Dm.num(tracked_bullet_cost[projectile]) * projectile_damage_tick_ecost_coefficient * Dm.num(seconds_per_tick);
        }
        energy = clamp(energy - usage, 0, maxenergy);
        if (energy <= 0)
        {
            deactivate_field();
            visible_message(span_warning(I18n.T("[] blinks \"ENERGY DEPLETED\".", this)));
        }
    }

    // tg: code/game/objects/items/robot/items/tools.dm:139
    public virtual async DmTask process_recharge(object seconds_per_tick = null)
    {
        if (host is not mob_living_silicon_robot)
        {
            if (iscyborg(host.loc))
                host = host.loc as mob_living_silicon_robot;
            else
            {
                energy = clamp(energy + energy_recharge * Dm.num(seconds_per_tick), 0, maxenergy);
                return;
            }
        }
        obj_item_stock_parts_power_store host_cell = host.cell as obj_item_stock_parts_power_store;
        if (host_cell != null && (Dm.num(host_cell.charge) >= (Dm.num(host_cell.maxcharge) * cyborg_cell_critical_percentage)) && (energy < maxenergy))
        {
            await host_cell.use(energy_recharge * Dm.num(seconds_per_tick) * POWER_RECHARGE_CYBORG_DRAIN_MULTIPLIER);
            energy += energy_recharge * Dm.num(seconds_per_tick);
        }
    }

    // tg: code/game/objects/items/robot/items/tools.dm:150
    [SignalHandler]
    public virtual void dampen_projectile(datum source = null, obj_projectile projectile = null)
    {
        if (Dm.truthy(projectile.is_hostile_projectile()))
            tracked_bullet_cost[REF(projectile)] = projectile.damage;
    }

    // tg: code/game/objects/items/robot/items/tools.dm:156
    [SignalHandler]
    public virtual void restore_projectile(datum source = null, obj_projectile projectile = null)
    {
        Dm.remove(ref tracked_bullet_cost, REF(projectile));
    }
}

//bare minimum omni-toolset for modularity
// tg: code/game/objects/items/robot/items/tools.dm:161
public partial class obj_item_borg_cyborg_omnitool : obj_item_borg
{
    // tg: code/game/objects/items/robot/items/tools.dm:176
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        return dot;
    }

    // tg: code/game/objects/items/robot/items/tools.dm:180
    public override object Destroy(float? force = null)
    {
        foreach (object tool_path in Dm.each(atoms))
        {
            obj_item tool = atoms[tool_path] as obj_item;
            if (!QDELETED(tool)) //if we are sharing tools from our other omnitool brothers we don't want to re delete them if they got deleted first
                qdel(tool);
        }
        atoms.Cut();

        return base.Destroy(force);
    }

    /**
     * Sets the new internal tool to be used
     * Arguments
     *
     * * obj/item/ref - typepath for the new internal omnitool
     */
    // tg: code/game/objects/items/robot/items/tools.dm:203
    public virtual void set_internal_tool(object tool = null)
    {
        foreach (object internal_tool in Dm.each(omni_toolkit))
        {
            if (Dm.eq(internal_tool, tool))
            {
                reference = internal_tool as Type;
                tool_behaviour = Dm.str(Dm.initial((Type)internal_tool, "tool_behaviour"));
                break;
            }
        }
    }

    // tg: code/game/objects/items/robot/items/tools.dm:212
    public override object get_all_tool_behaviours()
    {
        DmList dot = Dm.list();
        foreach (object tool in Dm.each(omni_toolkit))
            Dm.append(ref dot, Dm.initial((Type)tool, "tool_behaviour"));
        return dot;
    }

    ///The omnitool interacts with real world objects based on the state it has assumed
    // tg: code/game/objects/items/robot/items/tools.dm:218
    public override object get_proxy_attacker_for(atom target = null, mob user = null)
    {
        if (reference == null)
            return this;

        //first check if we have the tool
        obj_item tool = atoms[reference] as obj_item;
        if (!QDELETED(tool))
            return tool;

        //else try to borrow an in-built tool from our other omnitool brothers to save & share memory & such
        mob_living_silicon_robot borg = user as mob_living_silicon_robot;
        foreach (obj_item_borg_cyborg_omnitool omni_tool in Dm.each<obj_item_borg_cyborg_omnitool>(borg.model.basic_modules))
        {
            if (omni_tool == this)
                continue;
            tool = omni_tool.atoms[reference] as obj_item;
            if (!QDELETED(tool))
            {
                atoms[reference] = tool;
                return tool;
            }
        }

        //if all else fails just make a new one from scratch
        tool = Dm.@new(reference, user) as obj_item;
        //assign the upgraded toolspeed, if engi omnitool upgrade was applied.
        tool.toolspeed = Dm.num(Dm.initial(tool, "toolspeed")) - Dm.num(upgraded) * 0.3f;
        //the internal tool is considered part of the tool itself, so don't let it be dropped.
        tool.item_flags = Dm.num(Dm.bitor(tool.item_flags, ABSTRACT));
        ADD_TRAIT(tool, TRAIT_NODROP, INNATE_TRAIT);
        //assign the upgraded toolspeed, if any.
        tool.toolspeed = Dm.num(Dm.initial(tool, "toolspeed")) - Dm.num(upgraded) * 0.3f;
        //store tool for future use
        atoms[reference] = tool;

        return tool;
    }

    // tg: code/game/objects/items/robot/items/tools.dm:251
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        //build the radial menu options
        DmList radial_menu_options = Dm.list();
        DmList tool_map = Dm.list();
        foreach (object tool in Dm.each(omni_toolkit))
        {
            if (Dm.eq(Dm.initial((Type)tool, "tool_behaviour"), tool_behaviour))
                continue;
            object tool_name = Dm.initial((Type)tool, "name");
            radial_menu_options[tool_name] = image(icon: Dm.initial((Type)tool, "icon"), icon_state: Dm.initial((Type)tool, "icon_state"));
            tool_map[tool_name] = tool;
        }

        //assign the new tool behaviour
        object internal_tool_name = await show_radial_menu(user, this, radial_menu_options, require_near: TRUE, tooltips: TRUE);
        if (!Dm.truthy(internal_tool_name))
            return null;

        //set the reference and update appearance
        set_internal_tool(tool_map[internal_tool_name]);
        update_appearance(UPDATE_ICON_STATE);
        playsound(this, Dm.resource("sound/items/tools/change_jaws.ogg"), 50, TRUE);
        return null;
    }

    // tg: code/game/objects/items/robot/items/tools.dm:272
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        DmList modifiers = params2list(@params);
        if (!Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)) || !iscyborg(usr))
            return await base.Click(location, control, @params);
        mob_living_silicon_robot user = usr as mob_living_silicon_robot;
        await attack_self(user, modifiers);
        return await base.Click(location, control, @params);
    }

    // tg: code/game/objects/items/robot/items/tools.dm:280
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (reference != null)
            icon_state = Dm.str(Dm.initial(reference, "icon_state")); // reference.icon_state: the typepath's initial value
        return await base.update_icon_state(__extra);
    }

    /**
     * Is this omni tool upgraded or not
     * Arguments
     *
     * * upgrade - TRUE/FALSE for upgraded
     */
    // tg: code/game/objects/items/robot/items/tools.dm:291
    public virtual void set_upgraded(object upgrade = null)
    {
        upgraded = Dm.num(upgrade);
        foreach (object tool_path in Dm.each(atoms))
        {
            obj_item tool = atoms[tool_path] as obj_item;
            tool.toolspeed = Dm.num(Dm.initial(tool, "toolspeed")) - upgraded * 0.3f;
        }
        playsound(this, Dm.resource("sound/items/tools/change_jaws.ogg"), 50, TRUE);
    }
}

// tg: code/game/objects/items/robot/items/tools.dm:313
public partial class obj_item_borg_cyborg_omnitool_engineering : obj_item_borg_cyborg_omnitool
{
    // tg: code/game/objects/items/robot/items/tools.dm:326
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.eq(tool_behaviour, TOOL_MULTITOOL))
        {
            obj_item_multitool tool = atoms[typeof(obj_item_multitool_cyborg)] as obj_item_multitool;
            if (tool?.buffer != null)
                Dm.append(ref dot, span_notice(I18n.T("Multitool buffer contains [].", tool.buffer)));
        }
        return dot;
    }
}
