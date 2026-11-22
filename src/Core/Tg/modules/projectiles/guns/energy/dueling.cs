// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/energy/dueling.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps GU1, D333): the duel (paired by using one pistol on the other, or in their case; begun with both pistols in
// hand, the duellists step exactly five tiles apart with a clear line, confirm by pulling the trigger, count down from ten and
// fire once at each other), the dueling pistols (their setting, wide, cone or pinpoint, drawn as the red, green or blue overlay and
// switched in hand during a duel; the safety that fires only at the opponent and only on the FIRE signal), the chaff left at the
// shooter's feet in the setting's colour, the duel casing and the beam (it homes on its target, is blocked by the chaff of the
// setting it loses to, and takes the head off whoever it hits). The var blocks are generated (D111).

// tg: code/modules/projectiles/guns/energy/dueling.dm:12
public partial class datum_duel : datum
{
    // tg: code/modules/projectiles/guns/energy/dueling.dm:1-10  the file-local #defines (#undef at :354-361); the pistol, the
    // chaff, the casing and the beam name them through this class (rules 11.4)
    internal const int DUEL_IDLE = 1;
    internal const int DUEL_PREPARATION = 2;
    internal const int DUEL_READY = 3;
    internal const int DUEL_COUNTDOWN = 4;
    internal const int DUEL_FIRING = 5;

    //paper rock scissors
    internal const string DUEL_SETTING_A = "wide";
    internal const string DUEL_SETTING_B = "cone";
    internal const string DUEL_SETTING_C = "pinpoint";

    // tg: code/modules/projectiles/guns/energy/dueling.dm:23
    [DmParams("new_gun_A", "new_gun_B")]
    public override async DmTask New(params object[] __extra)
    {
        object new_gun_A = Dm.arg(__extra, 0);
        object new_gun_B = Dm.arg(__extra, 1);
        pairing_code = (string)assign_random_name();

        gun_A = new_gun_A as obj_item_gun_energy_dueling;
        gun_B = new_gun_B as obj_item_gun_energy_dueling;
        gun_A.duel = this;
        gun_B.duel = this;

        await base.New(Dm.forward(__extra, new_gun_A, new_gun_B));
    }

    // tg: code/modules/projectiles/guns/energy/dueling.dm:33
    public virtual void try_begin()
    {
        //Check if both guns are held and if so begin.
        mob_living A = get_duelist(gun_A) as mob_living;
        mob_living B = get_duelist(gun_B) as mob_living;
        if (A == null || B == null)
        {
            message_duelists(span_warning(I18n.T("To begin the duel, both participants need to be holding paired dueling pistols.")));
            return;
        }
        begin();
    }

    // tg: code/modules/projectiles/guns/energy/dueling.dm:42
    public virtual void begin()
    {
        state = DUEL_PREPARATION;
        confirmations.Cut();
        fired.Cut();
        countdown_step = countdown_length;

        message_duelists(span_notice(I18n.T("Set your gun setting and move [] steps away from your opponent.", required_distance)));

        START_PROCESSING(SSobj, this);
    }

    // tg: code/modules/projectiles/guns/energy/dueling.dm:52
    public virtual object get_duelist(obj gun = null)
    {
        mob_living G = gun.loc as mob_living;
        if (!(G is mob_living) || !Dm.truthy(G.is_holding(gun as obj_item)))
            return null;
        return G;
    }

    // tg: code/modules/projectiles/guns/energy/dueling.dm:58
    public virtual void message_duelists(object message = null)
    {
        mob_living LA = get_duelist(gun_A) as mob_living;
        if (LA != null)
            to_chat(LA, message);
        mob_living LB = get_duelist(gun_B) as mob_living;
        if (LB != null)
            to_chat(LB, message);
    }

    // tg: code/modules/projectiles/guns/energy/dueling.dm:66
    public virtual object other_gun(obj_item_gun_energy_dueling G = null)
    {
        return G == gun_A ? gun_B : gun_A;
    }

    // tg: code/modules/projectiles/guns/energy/dueling.dm:69
    public virtual void end()
    {
        message_duelists(span_notice(I18n.T("Duel finished. Re-engaging safety.")));
        STOP_PROCESSING(SSobj, this);
        state = DUEL_IDLE;
    }

    // tg: code/modules/projectiles/guns/energy/dueling.dm:74
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        float? sw__1 = state;
        if (Dm.eq(sw__1, DUEL_PREPARATION))
        {
            if (Dm.truthy(Waiting.NoSleep(check_positioning())))
                confirm_positioning();
            else if (!Dm.truthy(get_duelist(gun_A)) && !Dm.truthy(get_duelist(gun_B)))
                end();
        }
        else if (Dm.eq(sw__1, DUEL_READY))
        {
            if (!Dm.truthy(Waiting.NoSleep(check_positioning())))
                back_to_prep();
            else if (confirmations.len == 2)
                confirm_ready();
        }
        else if (Dm.eq(sw__1, DUEL_COUNTDOWN))
        {
            if (!Dm.truthy(Waiting.NoSleep(check_positioning())))
                back_to_prep();
            else
                countdown_step__proc();
        }
        else if (Dm.eq(sw__1, DUEL_FIRING))
        {
            if (Dm.truthy(check_fired()))
                end();
        }
        return null;
    }

    // tg: code/modules/projectiles/guns/energy/dueling.dm:96
    public virtual void back_to_prep()
    {
        message_duelists(span_notice(I18n.T("Positions invalid. Please move to valid positions exactly [] steps away from each other to continue.", required_distance)));
        state = DUEL_PREPARATION;
        confirmations.Cut();
        countdown_step = countdown_length;
    }

    // tg: code/modules/projectiles/guns/energy/dueling.dm:102
    public virtual void confirm_positioning()
    {
        message_duelists(span_notice(I18n.T("Position confirmed. Confirm readiness by pulling the trigger once.")));
        state = DUEL_READY;
    }

    // tg: code/modules/projectiles/guns/energy/dueling.dm:106
    public virtual void confirm_ready()
    {
        message_duelists(span_notice(I18n.T("Readiness confirmed. Starting countdown. Commence firing at zero mark.")));
        state = DUEL_COUNTDOWN;
    }

    // tg: code/modules/projectiles/guns/energy/dueling.dm:110  (the proc clashes with the var countdown_step: rules 2.4)
    public virtual void countdown_step__proc()
    {
        countdown_step--;
        if (countdown_step == 0)
        {
            state = DUEL_FIRING;
            message_duelists(span_userdanger(I18n.T("Fire!")));
        }
        else
            message_duelists(span_userdanger(I18n.T("[]!", countdown_step)));
    }

    // tg: code/modules/projectiles/guns/energy/dueling.dm:118
    public virtual object check_fired()
    {
        if (fired.len == 2)
            return TRUE;
        //Let's say if gun was dropped/stowed the user is finished
        if (!Dm.truthy(get_duelist(gun_A)))
            return TRUE;
        if (!Dm.truthy(get_duelist(gun_B)))
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/projectiles/guns/energy/dueling.dm:128
    public virtual async DmTask<object> check_positioning()
    {
        mob_living A = get_duelist(gun_A) as mob_living;
        mob_living B = get_duelist(gun_B) as mob_living;
        if (A == null || B == null)
            return FALSE;
        if (!isturf(A.loc) || !isturf(B.loc))
            return FALSE;
        if (get_dist(A, B) != required_distance)
            return FALSE;
        foreach (turf T in Dm.each<turf>(get_line(get_turf(A), get_turf(B))))
        {
            if (Dm.truthy(await T.is_blocked_turf(TRUE)))
                return FALSE;
        }
        return TRUE;
    }

    ///For each linked gun that still exists, clear its reference to us, then delete.
    // tg: code/modules/projectiles/guns/energy/dueling.dm:143
    public virtual void clear_duel()
    {
        if (gun_A != null) // gun_A?.duel = null
            gun_A.duel = null;
        if (gun_B != null) // gun_B?.duel = null
            gun_B.duel = null;
        qdel(this);
    }
}

// tg: code/modules/projectiles/guns/energy/dueling.dm:148
public partial class obj_item_gun_energy_dueling : obj_item_gun_energy
{
    // tg: code/modules/projectiles/guns/energy/dueling.dm:163
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        setting_overlay = mutable_appearance(icon, (string)setting_iconstate()) as mutable_appearance;
        add_overlay(setting_overlay);
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/dueling.dm:168
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_gun_energy_dueling)))
            return NONE;
        obj_item_gun_energy_dueling other_gun = tool as obj_item_gun_energy_dueling;
        if (Dm.truthy(check_valid_duel(user, FALSE)) || Dm.truthy(other_gun.check_valid_duel(user, FALSE)))
            return ITEM_INTERACT_BLOCKING;
        datum_duel newduel = Dm.@new<datum_duel>(this, other_gun);
        to_chat(user, span_notice(I18n.T("Pairing established. Pairing code: []", newduel.pairing_code)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/projectiles/guns/energy/dueling.dm:178
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);
        if (Dm.truthy(check_valid_duel(user as mob_living, FALSE)))
            Dm.append(ref dot, I18n.T("The pairing code is: []", duel.pairing_code));
        else
            Dm.append(ref dot, I18n.T("[] is currently unpaired.", this));
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/dueling.dm:185
    public virtual object setting_iconstate()
    {
        switch (setting)
        {
            case datum_duel.DUEL_SETTING_A:
                return "duel_red";
            case datum_duel.DUEL_SETTING_B:
                return "duel_green";
            case datum_duel.DUEL_SETTING_C:
                return "duel_blue";
        }
        return "duel_red";
    }

    // tg: code/modules/projectiles/guns/energy/dueling.dm:195
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (!Dm.truthy(check_valid_duel(user as mob_living, TRUE)))
            return dot;

        if (Dm.eq(duel.state, datum_duel.DUEL_IDLE))
            duel.try_begin();
        else
            toggle_setting(user as mob_living);
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/dueling.dm:205
    public virtual void toggle_setting(mob_living user = null)
    {
        switch (setting)
        {
            case datum_duel.DUEL_SETTING_A:
                setting = datum_duel.DUEL_SETTING_B;
                break;
            case datum_duel.DUEL_SETTING_B:
                setting = datum_duel.DUEL_SETTING_C;
                break;
            case datum_duel.DUEL_SETTING_C:
                setting = datum_duel.DUEL_SETTING_A;
                break;
        }
        to_chat(user, span_notice(I18n.T("You switch [] setting to [] mode.", this, setting)));
        update_appearance();
    }

    // tg: code/modules/projectiles/guns/energy/dueling.dm:216
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (setting_overlay != null)
        {
            setting_overlay.icon_state = (string)setting_iconstate();
            Dm.append(ref dot, setting_overlay);
        }
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/dueling.dm:222
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        duel?.clear_duel();
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/dueling.dm:226
    public override async DmTask<object> can_trigger_gun(mob_living user = null, object akimbo_usage = null)
    {
        if (Dm.truthy(akimbo_usage))
            return FALSE; //not honorable.
        object dot = await base.can_trigger_gun(user, akimbo_usage);
        if (!Dm.truthy(check_valid_duel(user, TRUE)))
            return FALSE;

        float? sw__1 = duel.state;
        if (Dm.eq(sw__1, datum_duel.DUEL_FIRING))
        {
            object and__1 = dot;
            return Dm.truthy(and__1) ? Dm.not(duel.fired[this]) : and__1; // . && !duel.fired[src]
        }
        else if (Dm.eq(sw__1, datum_duel.DUEL_READY))
            return dot;
        else
        {
            to_chat(user, span_warning(I18n.T("[] is locked. Wait for FIRE signal before shooting.", this)));
            return FALSE;
        }
    }

    // tg: code/modules/projectiles/guns/energy/dueling.dm:242
    public virtual object is_duelist(mob_living L = null)
    {
        if (!(L is mob_living))
            return FALSE;
        if (!Dm.truthy(L.is_holding(duel.other_gun(this) as obj_item)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/projectiles/guns/energy/dueling.dm:249
    public override async DmTask<object> process_fire(atom target = null, mob_living user = null, float? message = null, object @params = null, string zone_override = null, float? bonus_spread = null)
    {
        object dot = null;
        if (!Dm.truthy(check_valid_duel(user, TRUE)))
            return dot;
        if (Dm.eq(duel.state, datum_duel.DUEL_READY))
        {
            duel.confirmations[this] = TRUE;
            to_chat(user, span_notice(I18n.T("You confirm your readiness.")));
            return dot;
        }
        else if (!Dm.truthy(is_duelist(target as mob_living))) //I kinda want to leave this out just to see someone shoot a bystander or missing.
        {
            to_chat(user, span_warning(I18n.T("[] safety system prevents shooting anyone but your designated opponent.", this)));
            return dot;
        }
        else
        {
            duel.fired[this] = TRUE;
            dot = await base.process_fire(target, user, message, @params, zone_override, bonus_spread);
        }
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/dueling.dm:263
    public override void before_firing(atom target = null, mob user = null)
    {
        obj_item_ammo_casing_energy_duel D = chambered as obj_item_ammo_casing_energy_duel;
        D.setting = setting;
    }

    ///Return a boolean of whether or not the pistol has a valid duel datum, if false optionally warn the user
    // tg: code/modules/projectiles/guns/energy/dueling.dm:268
    public virtual object check_valid_duel(mob_living user = null, object do_warn = null)
    {
        if (duel == null)
        {
            if (Dm.truthy(do_warn))
                to_chat(user, span_warning(I18n.T("[] is currently unpaired.", this)));
            return FALSE;
        }
        return TRUE;
    }
}

// tg: code/modules/projectiles/guns/energy/dueling.dm:275
public partial class obj_effect_temp_visual_dueling_chaff : obj_effect_temp_visual
{
    // tg: code/modules/projectiles/guns/energy/dueling.dm:281
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        object dot = await base.update_icon(updates, __extra);
        object sw__1 = setting;
        if (Dm.eq(sw__1, datum_duel.DUEL_SETTING_A))
            color = "red";
        else if (Dm.eq(sw__1, datum_duel.DUEL_SETTING_B))
            color = "green";
        else if (Dm.eq(sw__1, datum_duel.DUEL_SETTING_C))
            color = "blue";
        return dot;
    }
}

//Casing

// tg: code/modules/projectiles/guns/energy/dueling.dm:293
public partial class obj_item_ammo_casing_energy_duel : obj_item_ammo_casing_energy
{
    // tg: code/modules/projectiles/guns/energy/dueling.dm:298
    public override async DmTask ready_proj(atom target = null, mob_living user = null, object quiet = null, string zone_override = null, atom fired_from = null)
    {
        await base.ready_proj(target, user, quiet, zone_override, fired_from);
        obj_projectile_energy_duel dueling_projectile = loaded_projectile as obj_projectile_energy_duel;
        dueling_projectile.setting = setting;
        dueling_projectile.update_appearance();
        if (!isturf(target))
            dueling_projectile.set_homing_target(target);
    }

    // tg: code/modules/projectiles/guns/energy/dueling.dm:306
    public override async DmTask<object> fire_casing(atom target = null, mob_living user = null, object @params = null, object distro = null, object quiet = null, object zone_override = null, object spread = null, atom fired_from = null)
    {
        object dot = await base.fire_casing(target, user, @params, distro, quiet, zone_override, spread, fired_from);
        obj_effect_temp_visual_dueling_chaff chaff = Dm.@new<obj_effect_temp_visual_dueling_chaff>(get_turf(user));
        chaff.setting = setting;
        chaff.update_appearance();
        return dot;
    }
}

//Projectile

// tg: code/modules/projectiles/guns/energy/dueling.dm:314
public partial class obj_projectile_energy_duel : obj_projectile_energy
{
    // tg: code/modules/projectiles/guns/energy/dueling.dm:320
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        object dot = await base.update_icon(updates, __extra);
        object sw__1 = setting;
        if (Dm.eq(sw__1, datum_duel.DUEL_SETTING_A))
            color = "red";
        else if (Dm.eq(sw__1, datum_duel.DUEL_SETTING_B))
            color = "green";
        else if (Dm.eq(sw__1, datum_duel.DUEL_SETTING_C))
            color = "blue";
        return dot;
    }

    // tg: code/modules/projectiles/guns/energy/dueling.dm:330
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = await base.on_hit(target, blocked, pierce_hit);
        turf T = get_turf(target);
        obj_effect_temp_visual_dueling_chaff C = Dm.locate<obj_effect_temp_visual_dueling_chaff>(T);
        if (C != null)
        {
            object counter_setting = null;
            object sw__1 = setting;
            if (Dm.eq(sw__1, datum_duel.DUEL_SETTING_A))
                counter_setting = datum_duel.DUEL_SETTING_B;
            else if (Dm.eq(sw__1, datum_duel.DUEL_SETTING_B))
                counter_setting = datum_duel.DUEL_SETTING_C;
            else if (Dm.eq(sw__1, datum_duel.DUEL_SETTING_C))
                counter_setting = datum_duel.DUEL_SETTING_A;
            if (Dm.eq(C.setting, counter_setting))
                return BULLET_ACT_BLOCK;
        }

        // tg tests istype(target), the atom, not L: a beam that hits a wall reaches L.get_bodypart and runtimes there, as in tg
        mob_living L = target as mob_living;
        if (!(target is atom))
            return BULLET_ACT_BLOCK;

        obj_item_bodypart B = L.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;
        await B.dismember();
        qdel(B);
        return dot;
    }
}
