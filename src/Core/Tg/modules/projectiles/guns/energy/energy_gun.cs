// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/energy/energy_gun.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The file whole: the energy gun and the HoS's (M7x slice B), the hybrid turret gun the portable turrets hold (syndicate G4d, D316),
// the DRAGnet synced to its beacon (gaps SE1, D333); gaps GU1 (D333): the miniature energy gun's built-in flashlight and the advanced
// energy gun's reactor (its self-charge, the failure chance an EMP raises, the heat that burns its holder and the overload that stops
// it charging, with the fail overlays). The tactical, prototype, practice phaser and lethal variants declare no procs. The var blocks
// are generated (D111).

// tg: code/modules/projectiles/guns/energy/energy_gun.dm:1
public partial class obj_item_gun_energy_e_gun : obj_item_gun_energy
{
    // tg: code/modules/projectiles/guns/energy/energy_gun.dm:25
    public override async DmTask add_seclight_point()
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_seclite_attachable), Dm.named("light_overlay_icon", Dm.resource("icons/obj/weapons/guns/flashlights.dmi")), Dm.named("light_overlay", "flight"), Dm.named("overlay_x", 15), Dm.named("overlay_y", 10)));
    }

    // tg: code/modules/projectiles/guns/energy/energy_gun.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // Only actual eguns can be converted
        if (type != typeof(obj_item_gun_energy_e_gun))
            return dot;
        // var/static/list/slapcraft_recipe_list (a proc-level static, rules 10.6)
        Waiting.NoSleep(AddElement(
            typeof(datum_element_slapcrafting),
            Dm.named("slapcraft_recipes", TgState.Current.obj_item_gun_energy_e_gun__Initialize__slapcraft_recipe_list)));
        return dot;
    }
}

// tg: code/modules/projectiles/guns/energy/energy_gun.dm:88
public partial class obj_item_gun_energy_e_gun_dragnet : obj_item_gun_energy_e_gun
{
    // tg: code/modules/projectiles/guns/energy/energy_gun.dm:109
    public override async DmTask add_seclight_point()
    {
        return;
    }

    // tg: code/modules/projectiles/guns/energy/energy_gun.dm:112
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_dragnet_beacon beacon)
            link_beacon(user, beacon);
        return null;
    }

    ///Sets the linked_beacon var on the dragnet, which becomes the snare round's teleport destination.
    // tg: code/modules/projectiles/guns/energy/energy_gun.dm:117
    public virtual void link_beacon(mob_living user = null, obj_item_dragnet_beacon our_beacon = null)
    {
        if (linked_beacon != null)
        {
            if (our_beacon == linked_beacon)
            {
                balloon_alert(user, I18n.T("already synced!"));
                return;
            }
            else
                UnregisterSignal(linked_beacon, COMSIG_QDELETING); //You're getting overridden dude.
        }

        linked_beacon = our_beacon;
        balloon_alert(user, I18n.T("beacon synced"));
        RegisterSignal(our_beacon, COMSIG_QDELETING, nameof(handle_beacon_disable));
    }

    ///Handles clearing the linked_beacon reference in the event that it is deleted.
    // tg: code/modules/projectiles/guns/energy/energy_gun.dm:130
    [SignalHandler]
    public virtual void handle_beacon_disable(datum source = null)
    {
        visible_message(span_warning(I18n.T("A light on the [] flashes, indicating that it is no longer linked with a DRAGnet beacon!", this)));
        linked_beacon = null;
    }
}

// tg: code/modules/projectiles/guns/energy/energy_gun.dm:135
public partial class obj_item_gun_energy_e_gun_turret : obj_item_gun_energy_e_gun
{
    // tg: code/modules/projectiles/guns/energy/energy_gun.dm:147
    public override async DmTask add_seclight_point()
    {
        return;
    }
}

// The static lists of the energy gun (rules 10.6: type-level statics live in TgState).
public sealed partial class TgState
{
    // tg: code/modules/projectiles/guns/energy/energy_gun.dm:18  var/static/list/slapcraft_recipe_list
    internal DmList obj_item_gun_energy_e_gun__Initialize__slapcraft_recipe_list = Dm.list(typeof(datum_crafting_recipe_advancedegun), typeof(datum_crafting_recipe_tempgun), typeof(datum_crafting_recipe_beam_rifle));
}

// tg: code/modules/projectiles/guns/energy/energy_gun.dm:32
public partial class obj_item_gun_energy_e_gun_mini : obj_item_gun_energy_e_gun
{
    // tg: code/modules/projectiles/guns/energy/energy_gun.dm:43
    public override async DmTask add_seclight_point()
    {
        // The mini energy gun's light comes attached but is unremovable.
        await AddComponent(typeof(datum_component_seclite_attachable),
            Dm.named("starting_light", Dm.@new<obj_item_flashlight_seclite>(this)),
            Dm.named("is_light_removable", FALSE),
            Dm.named("light_overlay_icon", Dm.resource("icons/obj/weapons/guns/flashlights.dmi")),
            Dm.named("light_overlay", "mini-light"),
            Dm.named("overlay_x", 19),
            Dm.named("overlay_y", 13));
    }
}

// tg: code/modules/projectiles/guns/energy/energy_gun.dm:150
public partial class obj_item_gun_energy_e_gun_nuclear : obj_item_gun_energy_e_gun
{
    // tg: code/modules/projectiles/guns/energy/energy_gun.dm:170
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (fail_tick > 0)
            fail_tick -= Dm.num(seconds_per_tick) * 0.5f;
        base.process(seconds_per_tick, __extra);
        return null;
    }

    // tg: code/modules/projectiles/guns/energy/energy_gun.dm:175
    public override async DmTask<object> shoot_live_shot(mob_living user = null, float? pointblank = null, atom pbtarget = null, float? message = null)
    {
        pointblank ??= 0;
        message ??= 1;
        await failcheck();
        update_appearance();
        await base.shoot_live_shot(user, pointblank, pbtarget, message);
        return null;
    }

    // tg: code/modules/projectiles/guns/energy/energy_gun.dm:180
    public virtual async DmTask failcheck()
    {
        if (prob(fail_chance) && isliving(loc))
        {
            mob_living M = loc as mob_living;
            switch (fail_tick)
            {
                case >= 0 and <= 200:
                    fail_tick += 2 * fail_chance;
                    await M.adjust_fire_loss(3);
                    to_chat(M, span_userdanger(I18n.T("Your [] feels warmer.", name)));
                    break;
                case >= 201 and <= INFINITY:
                    SSobj.processing.Remove(this);
                    await M.adjust_fire_loss(10);
                    reactor_overloaded = TRUE;
                    to_chat(M, span_userdanger(I18n.T("Your []'s reactor overloads!", name)));
                    break;
            }
        }
    }

    // tg: code/modules/projectiles/guns/energy/energy_gun.dm:194
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        fail_chance = min(fail_chance + round(15 / Dm.num(severity)), 100);
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/energy_gun.dm:200
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(reactor_overloaded))
        {
            Dm.append(ref dot, Dm.text("[]_fail_3", icon_state));
            return dot;
        }

        switch (fail_tick)
        {
            case 0:
                Dm.append(ref dot, Dm.text("[]_fail_0", icon_state));
                break;
            case >= 1 and <= 150:
                Dm.append(ref dot, Dm.text("[]_fail_1", icon_state));
                break;
            case >= 151 and <= INFINITY:
                Dm.append(ref dot, Dm.text("[]_fail_2", icon_state));
                break;
        }
        return dot;
    }
}
