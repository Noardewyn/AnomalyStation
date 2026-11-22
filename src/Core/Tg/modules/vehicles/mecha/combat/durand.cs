// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/combat/durand.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/vehicles/mecha/combat/durand.dm:1
public partial class obj_vehicle_sealed_mecha_durand : obj_vehicle_sealed_mecha
{
    // tg: code/modules/vehicles/mecha/combat/durand.dm:33
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        shield = Dm.@new<obj_durand_shield>(this, this);
        Dm.append(vis_contents, shield);
        return dot;
    }

    // tg: code/modules/vehicles/mecha/combat/durand.dm:38
    public override object Destroy(float? force = null)
    {
        if (shield != null)
            QDEL_NULL(ref shield);
        return base.Destroy(force);
    }

    // tg: code/modules/vehicles/mecha/combat/durand.dm:43
    public override void generate_actions()
    {
        base.generate_actions();
        initialize_passenger_action_type(typeof(datum_action_vehicle_sealed_mecha_mech_defense_mode));
    }

    // tg: code/modules/vehicles/mecha/combat/durand.dm:47
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        object dot = base.process(seconds_per_tick, __extra);
        // Defence mode can only be on with a occupant so we check if one of them can toggle it and toggle
        if (Dm.truthy(defense_mode) && !Dm.truthy(use_energy(0.01f * STANDARD_CELL_CHARGE)))
            Waiting.Detach(toggle_defense());
        return dot;
    }

    // tg: code/modules/vehicles/mecha/combat/durand.dm:53
    public override async DmTask<object> mob_exit(mob M = null, object silent = null, object randomstep = null, object forced = null)
    {
        if (Dm.truthy(defense_mode))
            await toggle_defense();
        return await base.mob_exit(M, silent, randomstep, forced);
    }

    //Redirects projectiles to the shield if defense_check decides they should be blocked and returns true.
    // tg: code/modules/vehicles/mecha/combat/durand.dm:59
    public override async DmTask<object> projectile_hit(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, object blocked = null)
    {
        if (defense_check(hitting_projectile.loc as atom) && shield != null)
            return await shield.projectile_hit(hitting_projectile, def_zone, piercing_hit, blocked);
        return await base.projectile_hit(hitting_projectile, def_zone, piercing_hit, blocked);
    }

    /**
     * Checks if defense mode is enabled, and if the attacker is standing in an area covered by the shield.
     * Expects a turf. Returns true if the attack should be blocked, false if not.
     **/
    // tg: code/modules/vehicles/mecha/combat/durand.dm:68
    public virtual bool defense_check(atom aloc = null)
    {
        if (!Dm.truthy(defense_mode) || shield == null || Dm.truthy(switching))
            return false;

        float dx = Dm.num(x) - Dm.num(aloc.x);
        float dy = Dm.num(y) - Dm.num(aloc.y);
        switch (Dm.num(dir))
        {
            case NORTH:
                return Math.Abs(dx) <= dy * -2;
            case SOUTH:
                return Math.Abs(dx) <= dy * 2;
            case EAST:
                return Math.Abs(dy) <= dx * -2;
            case WEST:
                return Math.Abs(dy) <= dx * 2;
        }
        return false;
    }

    // tg: code/modules/vehicles/mecha/combat/durand.dm:82
    public virtual async DmTask toggle_defense(mob_living user = null)
    {
        if (!Dm.truthy(LAZYLEN(occupants)))
            return;

        if (Dm.truthy(switching) && (user != null || !Dm.truthy(defense_mode))) // Allow force shutdowns during animation
            return;

        if (!Dm.truthy(defense_mode) && Dm.num(cell?.charge) < 100) // If it's off, and we have less than 100 units of power
        {
            if (user != null)
                balloon_alert(user, I18n.T("insufficient power"));
            return;
        }

        switching = TRUE;
        defense_mode = Dm.truthy(defense_mode) ? 0 : 1;

        if (user != null)
            balloon_alert(user, I18n.T("shield []", Dm.truthy(defense_mode) ? "enabled" : "disabled"));
        log_message(Dm.text("User has toggled defense mode -- now [].", Dm.truthy(defense_mode) ? "enabled" : "disabled"), LOG_MECHA);

        foreach (mob_living occupant in Dm.each<mob_living>(occupants))
        {
            datum_action button = (occupant_actions[occupant] as DmList)[typeof(datum_action_vehicle_sealed_mecha_mech_defense_mode)] as datum_action;
            button.button_icon_state = Dm.text("mech_defense_mode_[]", Dm.truthy(defense_mode) ? "on" : "off");
            await button.build_all_button_icons();
        }

        await shield.set_light_on(defense_mode);

        if (Dm.truthy(defense_mode))
        {
            shield.icon_state = "shield";
            flick("shield_raise", shield);
            playsound(this, Dm.resource("sound/vehicles/mecha/mech_shield_raise.ogg"), 50, FALSE);
        }
        else
        {
            shield.icon_state = "shield_null";
            flick("shield_drop", shield);
            playsound(this, Dm.resource("sound/vehicles/mecha/mech_shield_drop.ogg"), 50, FALSE);
        }

        _addtimer(Dm.@new<datum_callback>(this, nameof(switching_done)), 0.7f * SECONDS, file: "code/modules/vehicles/mecha/combat/durand.dm", line: 119); // Shield animation length
    }

    // tg: code/modules/vehicles/mecha/combat/durand.dm:119  VARSET_CALLBACK(src, switching, FALSE)
    public virtual void switching_done() => switching = FALSE;

    // tg: code/modules/vehicles/mecha/combat/durand.dm:121
    public override async DmTask<object> attack_generic(mob user = null, float? damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, float? armor_penetration = null)
    {
        if (defense_check(get_turf(user)))
        {
            log_message(Dm.text("Attack absorbed by defense field. Attacker - [].", user), LOG_MECHA, color: "orange");
            return await shield.attack_generic(user, damage_amount, damage_type, damage_flag, sound_effect, armor_penetration);
        }
        return await base.attack_generic(user, damage_amount, damage_type, damage_flag, sound_effect, armor_penetration);
    }

    // tg: code/modules/vehicles/mecha/combat/durand.dm:127
    public override async DmTask<object> blob_act(obj_structure_blob attacking_blob = null)
    {
        if (!defense_check(get_turf(attacking_blob)))
            return await base.blob_act(attacking_blob);
        log_message(Dm.text("Attack by blob. Attacker - [].", attacking_blob), LOG_MECHA, color: "red");
        log_message("Attack absorbed by defense field.", LOG_MECHA, color: "orange");
        return await shield.blob_act(attacking_blob);
    }

    // tg: code/modules/vehicles/mecha/combat/durand.dm:134
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (defense_check(get_turf(user)))
        {
            log_message(Dm.text("Attack absorbed by defense field. Attacker - [], with []", user, attacking_item), LOG_MECHA, color: "orange");
            return await shield.attackby(attacking_item, user, modifiers);
        }
        return await base.attackby(attacking_item, user, modifiers, attack_modifiers);
    }

    // tg: code/modules/vehicles/mecha/combat/durand.dm:140
    public override async DmTask<object> hitby(atom_movable hitting_atom = null, object skipcatch = null, object hitpush = null, object blocked = null, datum_thrownthing throwingdatum = null)
    {
        if (defense_check(get_turf(hitting_atom)))
        {
            log_message(Dm.text("Impact with [] absorbed by defense field.", hitting_atom), LOG_MECHA, color: "orange");
            return await shield.hitby(hitting_atom, skipcatch, hitpush, blocked, throwingdatum);
        }
        return await base.hitby(hitting_atom, skipcatch, hitpush, blocked, throwingdatum);
    }
}

// tg: code/modules/vehicles/mecha/combat/durand.dm:146
public partial class datum_action_vehicle_sealed_mecha_mech_defense_mode : datum_action_vehicle_sealed_mecha
{
    // tg: code/modules/vehicles/mecha/combat/durand.dm:151
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        if (chassis != null && Dm.@in(owner, chassis.occupants))
            await (chassis as obj_vehicle_sealed_mecha_durand).toggle_defense(owner as mob_living);
        return dot;
    }
}

// Shield processing

/**
 * An object to take the hit for us when using the Durand's defense mode.
 * It is spawned in during the durand's initilization.
 * Normally invisible, until defense mode is actvated. When the durand detects an attack that should be blocked, the
 * attack is passed to the shield. The shield takes the damage, uses it to calculate charge cost, and then sets its
 * own integrity back to max. Shield is automatically dropped if we run out of power or the user gets out.
 **/
// tg: code/modules/vehicles/mecha/combat/durand.dm:169
public partial class obj_durand_shield : obj //projectiles get passed to this when defense mode is enabled
{
    // tg: code/modules/vehicles/mecha/combat/durand.dm:187
    [DmParams("mapload", "chassis")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload);
        chassis = Dm.arg(__extra, 0) as obj_vehicle_sealed_mecha_durand;
        return dot;
    }

    // tg: code/modules/vehicles/mecha/combat/durand.dm:191
    public override object Destroy(float? force = null)
    {
        if (chassis != null)
        {
            chassis.shield = null;
            chassis = null;
        }
        return base.Destroy(force);
    }

    // tg: code/modules/vehicles/mecha/combat/durand.dm:197
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        if (chassis == null)
        {
            qdel(this);
            return null;
        }
        if (!Dm.truthy(chassis.defense_mode)) //if defense mode is disabled, we're taking damage that we shouldn't be taking
            return null;
        object dot = await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        flick("shield_impact", this);
        if (!Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(chassis.use_energy(Dm.num(dot) * (STANDARD_CELL_CHARGE / 150))))
        {
            if (chassis.cell != null)
                chassis.cell.charge = 0;
            await chassis.toggle_defense();
        }
        atom_integrity = 10000;
        return dot;
    }

    // tg: code/modules/vehicles/mecha/combat/durand.dm:212
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        playsound(this, Dm.resource("sound/vehicles/mecha/mech_shield_deflect.ogg"), 100, TRUE);
        return null;
    }

    // tg: code/modules/vehicles/mecha/combat/durand.dm:215
    public override async DmTask<object> bullet_act(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        play_attack_sound();
        return await base.bullet_act(hitting_projectile, def_zone, piercing_hit, blocked);
    }
}
