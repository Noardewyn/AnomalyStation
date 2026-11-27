// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/computers/machinery/modular_computer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the engineering package E2, D287): the console that hosts a modular computer's processor (NtOS on a desk), its
// screen overlays and light, the frame or computer interaction toggle, and the tools, hits and EMPs it passes to the processor.
// The processor's circuit shell sits on the console, with a USB port; emag_act came with the emag (G1a, D316). The var block is
// generated (D111).

// Modular Computer - A machinery that is mostly just a host to the Modular Computer item.
// tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:4
public partial class obj_machinery_modular_computer : obj_machinery
{
    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:1  #define CPU_INTERACTABLE(user) (file-local, rules 11.4)
    private bool CPU_INTERACTABLE(mob user) => cpu != null && !HAS_TRAIT_FROM(this, TRAIT_MODPC_INTERACTING_WITH_FRAME, REF(user));

    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:41
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        cpu = Dm.@new<obj_item_modular_computer_processor>(this);
        cpu.screen_on = TRUE;
        Waiting.NoSleep(cpu.add_shell_component(SHELL_CAPACITY_LARGE, SHELL_FLAG_USB_PORT));

        update_appearance();
        register_context();
        return dot;
    }

    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:49
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref cpu);
        return base.Destroy(force);
    }

    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:53
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = NONE;

        if (held_item == null)
        {
            context[SCREENTIP_CONTEXT_RMB] = "Toggle processor interaction";
            dot = Dm.bitor(dot, CONTEXTUAL_SCREENTIP_SET);
        }

        if (CPU_INTERACTABLE(user))
            dot = Dm.bitor(dot, cpu?.add_context(source, context, held_item, user));

        return dot;
    }

    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:65
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;
        if (HAS_TRAIT_FROM(this, TRAIT_MODPC_INTERACTING_WITH_FRAME, REF(user)))
        {
            REMOVE_TRAIT(this, TRAIT_MODPC_INTERACTING_WITH_FRAME, REF(user));
            balloon_alert(user, I18n.T("now interacting with computer"));
        }
        else
        {
            ADD_TRAIT(this, TRAIT_MODPC_INTERACTING_WITH_FRAME, REF(user));
            balloon_alert(user, I18n.T("now interacting with frame"));
        }
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:77
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = cpu != null ? await cpu.examine(user) : null;
        if (!Dm.truthy(dot))
            dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_info(I18n.T("You can toggle interaction between computer and its machinery frame with [] while empty-handed.", EXAMINE_HINT("Right-Click"))));
        string frame_or_pc = HAS_TRAIT_FROM(this, TRAIT_MODPC_INTERACTING_WITH_FRAME, REF(user)) ? "frame" : "computer";
        Dm.append(ref dot, span_info(I18n.T("Currently interacting with [].", EXAMINE_HINT(frame_or_pc))));
        return dot;
    }

    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:83
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        object dot = await base.attack_ghost(user);
        if (Dm.truthy(dot))
            return dot;
        if (cpu != null)
            await cpu.attack_ghost(user);
        return dot;
    }

    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:89
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (cpu == null)
        {
            balloon_alert(user, "turn it on first!");
            return FALSE;
        }
        return await cpu.emag_act(user);
    }

    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:95
    public override object update_appearance(float? updates = null)
    {
        updates ??= ALL;
        object dot = base.update_appearance(updates);
        Waiting.NoSleep(set_light(Dm.truthy(cpu?.enabled) ? light_strength : 0));
        return dot;
    }

    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:99
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (cpu == null || !Dm.truthy(cpu.enabled) || Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
            icon_state = icon_state_unpowered;
        else
            icon_state = icon_state_powered;
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:106
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (cpu == null)
            return dot;

        if (Dm.truthy(cpu.enabled))
        {
            object program_open_overlay = cpu.active_program?.program_open_overlay;
            Dm.append(ref dot, Dm.truthy(program_open_overlay) ? program_open_overlay : screen_icon_state_menu);
        }
        else if (!Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
            Dm.append(ref dot, screen_icon_screensaver);

        if (Dm.num(cpu.get_integrity()) <= Dm.num(cpu.integrity_failure) * Dm.num(cpu.max_integrity))
        {
            Dm.append(ref dot, "bsod");
            Dm.append(ref dot, "broken");
        }
        return dot;
    }

    /// Eats the "source" arg because update_icon actually expects args now.
    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:122
    [SignalHandler]
    public virtual object relay_icon_update(datum source = null, object updates = null, object updated = null)
    {
        return Waiting.NoSleep(update_icon(Dm.num(updates)));
    }

    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:126
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!CPU_INTERACTABLE(user) || !Dm.truthy(await can_interact(user)))
            return NONE;
        await cpu.click_alt(user);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:132
    public override async DmTask<object> click_alt_secondary(mob user = null)
    {
        if (!CPU_INTERACTABLE(user) || !Dm.truthy(await can_interact(user)))
            return NONE;
        await cpu.click_alt_secondary(user);
        return CLICK_ACTION_SUCCESS;
    }

    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // On-click handling. Turns on the computer if it's off and opens the GUI.
    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:140
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        return CPU_INTERACTABLE(user) ? await cpu.interact(user) : await base.interact(user, __extra);
    }

    // Modular computers can have battery in them, we handle power in previous proc, so prevent this from messing it up for us.
    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:144
    public override object power_change()
    {
        if (Dm.truthy(cpu?.use_energy())) // If it still has a power source, PC wouldn't go offline.
        {
            Waiting.NoSleep(set_machine_stat(Dm.bitand(machine_stat, Dm.bitnot(NOPOWER))));
            update_appearance();
            return null;
        }
        return base.power_change();
    }

    ///Try to recharge our internal cell if it isn't fully charged.
    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:152
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        obj_item_stock_parts_power_store cell = get_cell() as obj_item_stock_parts_power_store;
        if (cell == null || Dm.num(cell.percent()) >= 100)
            return null;
        Waiting.NoSleep(charge_cell__proc(idle_power_usage * Dm.num(seconds_per_tick), cell));
        return null;
    }

    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:158
    public override object get_cell(atom_movable @interface = null, mob user = null)
    {
        return cpu?.internal_cell;
    }

    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:161
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return CPU_INTERACTABLE(user) ? await cpu.screwdriver_act(user, tool) : await base.screwdriver_act(user, tool);
    }

    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:164
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        return CPU_INTERACTABLE(user) ? await cpu.wrench_act_secondary(user, tool) : await base.wrench_act_secondary(user, tool);
    }

    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:167
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        return CPU_INTERACTABLE(user) ? await cpu.welder_act(user, tool) : await base.welder_act(user, tool);
    }

    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:170
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        return CPU_INTERACTABLE(user) && !Dm.truthy(user.combat_mode) ? await cpu.item_interaction(user, tool, modifiers) : await base.item_interaction(user, tool, modifiers, __extra);
    }

    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:173
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        return CPU_INTERACTABLE(user) && !Dm.truthy(user.combat_mode) ? await cpu.item_interaction_secondary(user, tool, modifiers) : await base.item_interaction_secondary(user, tool, modifiers);
    }

    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:176
    public override async DmTask<object> attacked_by(obj_item attacking_item = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        return CPU_INTERACTABLE(user) ? await cpu.attacked_by(attacking_item, user, modifiers, attack_modifiers) : await base.attacked_by(attacking_item, user, modifiers, attack_modifiers);
    }

    // Stronger explosions cause serious damage to internal components
    // Minor explosions are mostly mitigitated by casing.
    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:181
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        if (cpu == null)
            return base.ex_act(severity, target, __extra);

        switch (Dm.num(severity))
        {
            case EXPLODE_DEVASTATE:
                Dm.append(ref SSexplosions.high_mov_atom, cpu);
                break;
            case EXPLODE_HEAVY:
                Dm.append(ref SSexplosions.med_mov_atom, cpu);
                break;
            case EXPLODE_LIGHT:
                Dm.append(ref SSexplosions.low_mov_atom, cpu);
                break;
        }
        return base.ex_act(severity, target, __extra);
    }

    // EMPs are similar to explosions, but don't cause physical damage to the casing. Instead they screw up the components
    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:195
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_CONTENTS)))
            return dot;
        if (cpu != null)
            await cpu.emp_act(severity);
        return dot;
    }

    // "Stun" weapons can cause minor damage to components (short-circuits?)
    // "Burn" damage is equally strong against internal components and exterior casing
    // "Brute" damage mostly damages the casing.
    // tg: code/modules/modular_computers/computers/machinery/modular_computer.dm:205
    public override async DmTask<object> projectile_hit(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, object blocked = null)
    {
        object dot = cpu != null ? await cpu.projectile_hit(hitting_projectile, def_zone, piercing_hit, blocked) : null;
        return Dm.truthy(dot) ? dot : await base.projectile_hit(hitting_projectile, def_zone, piercing_hit, blocked);
    }
}
