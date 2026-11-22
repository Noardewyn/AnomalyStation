// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/mines.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST5 (D333): the file whole. The landmines: armed after their delay (tg's angry beep and the "-inactive" sprite before), a
// step onto the pressure plate clicks and the mine goes off when that foot steps off (a border thing passing over sets it off at
// once), damage sets it off too; then sparks and the mine's effect: tg's explosions (the light, incendiary and blinding kinds), the
// stun, the kick (the victim's client is dropped), the gases, the honk and bwoink, the shrapnel rings (the AP mine's lit red).
// The landmine deployment device drops an AP mine in a puff of smoke after three seconds. The mine's icon in its chat line
// (icon2html) is left out: the 3D client's chat is text. The var blocks are generated (D111).

// tg: code/game/objects/effects/mines.dm:1
public partial class obj_effect_mine : obj_effect
{
    // tg: code/game/objects/effects/mines.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(arm_delay))
        {
            armed = FALSE;
            update_appearance(UPDATE_ICON_STATE);
            _addtimer(Dm.@new<datum_callback>(this, nameof(now_armed)), Dm.num(arm_delay), file: "code/game/objects/effects/mines.dm", line: 26);
        }

        DmList loc_connections = TgState.Current.obj_effect_mine__Initialize__loc_connections ??= Dm.list(
            Dm.kv(COMSIG_ATOM_ENTERED, nameof(on_entered)),
            Dm.kv(COMSIG_ATOM_EXITED, nameof(on_exited)));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), loc_connections));
        return dot;
    }

    // tg: code/game/objects/effects/mines.dm:34
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList ?? Dm.list();
        if (!Dm.truthy(armed))
            Dm.append(ref dot, span_info(I18n.T("It appears to be inactive...")));

        atom_movable unlucky_sod = foot_on_mine?.resolve() as atom_movable;
        if (user == unlucky_sod)
            Dm.append(ref dot, span_bolddanger(I18n.T("The pressure plate is depressed. Any movement you make will set it off now.")));
        else if (unlucky_sod != null)
            Dm.append(ref dot, span_danger(I18n.T("The pressure plate is depressed by []. Any move they make'll set it off now.", unlucky_sod)));
        return dot;
    }

    // tg: code/game/objects/effects/mines.dm:45
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(armed))
            icon_state = (string)base_icon_state;
        else
            icon_state = Dm.text("[]-inactive", base_icon_state);
        return dot;
    }

    /// The effect of the mine
    // tg: code/game/objects/effects/mines.dm:53
    public virtual async DmTask mineEffect(mob victim = null)
    {
        await DmTask.CompletedTask;
    }

    /// If the landmine was previously inactive, this beeps and displays a message marking it active
    // tg: code/game/objects/effects/mines.dm:57
    public virtual void now_armed()
    {
        armed = TRUE;
        update_appearance(UPDATE_ICON_STATE);
        playsound(this, Dm.resource("sound/machines/nuke/angry_beep.ogg"), 40, FALSE, -2);
        visible_message(span_danger(I18n.T("\\The [] beeps softly, indicating it is now active.", this)), vision_distance: COMBAT_MESSAGE_RANGE);
    }

    /// Can this mine trigger on the passed movable?
    // tg: code/game/objects/effects/mines.dm:64
    public virtual object can_trigger(atom_movable on_who = null)
    {
        if (Dm.truthy(triggered) || !isturf(loc) || iseffect(on_who) || !Dm.truthy(armed))
            return FALSE;

        if (Dm.truthy(on_who.anchored) || HAS_TRAIT(on_who, TRAIT_WALLMOUNTED))
            return FALSE;

        mob_living living_mob = null;
        if (ismob(on_who))
        {
            if (!isliving(on_who)) //no ghosties.
                return FALSE;
            living_mob = (mob_living)on_who;
        }

        if (Dm.truthy(living_mob?.incorporeal_move) || Dm.truthy(Dm.bitand(on_who.movement_type, MOVETYPES_NOT_TOUCHING_GROUND)))
            return foot_on_mine != null ? Dm.num(IS_WEAKREF_OF(on_who, foot_on_mine)) : FALSE; //Only go boom if their foot was on the mine PRIOR to flying/phasing. You fucked up, you live with the consequences.

        return TRUE;
    }

    // tg: code/game/objects/effects/mines.dm:82
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable arrived = null, atom old_loc = null)
    {
        if (!Dm.truthy(can_trigger(arrived)))
            return;
        // Someone already on it
        if (Dm.truthy(foot_on_mine?.resolve()))
            return;

        float gonna_blow = FALSE;
        if (Dm.truthy(Dm.bitand(arrived.flags_1, ON_BORDER_1)))
        {
            if (Dm.eq(arrived.dir, get_dir(old_loc, this))) //see if a partial tile atom has passed the mine
                gonna_blow = TRUE;
            else
                return; //it didn't actually touch the mine, don't blow
        }

        // tg: "[icon2html(src, viewers(src))] *click*": the mine's picture in the chat line is left out (the 3D client's chat is text)
        visible_message(span_danger(I18n.T("*click*")));
        playsound(this, Dm.resource("sound/machines/click.ogg"), 60, TRUE);
        if (Dm.truthy(gonna_blow))
        {
            RegisterSignal(arrived, COMSIG_MOVABLE_MOVED, nameof(triggermine)); //wait for it to finish the movement before blowing so it takes proper damage
            return;
        }

        foot_on_mine = WEAKREF(arrived) as datum_weakref;
    }

    // tg: code/game/objects/effects/mines.dm:107
    [SignalHandler]
    public virtual void on_exited(datum source = null, atom_movable gone = null, object direction = null)
    {
        if (!Dm.truthy(can_trigger(gone)))
            return;

        if (foot_on_mine == null && Dm.truthy(Dm.bitand(gone.flags_1, ON_BORDER_1)))
        {
            if (Dm.eq(gone.dir, REVERSE_DIR(Dm.num(direction)))) //see if a north facing border atom (ie window) travels south (and other directions as needed)
            {
                // tg: "[icon2html(src, viewers(src))] *click*" (the chat icon left out)
                visible_message(span_danger(I18n.T("*click*")));
                playsound(this, Dm.resource("sound/machines/click.ogg"), 60, TRUE);
                triggermine(); //it "passed" over the mine briefly, triggering it in the process
            }
            return; //either it blew up the mine, or it didn't and we don't have to worry about anything else.
        }

        // Check that the guy who's on it is stepping off
        if (foot_on_mine != null && !IS_WEAKREF_OF(gone, foot_on_mine))
            return;

        triggermine(gone);
        foot_on_mine = null;
    }

    // tg: code/game/objects/effects/mines.dm:127
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        object dot = await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        triggermine();
        return dot;
    }

    /// When something sets off a mine
    // tg: code/game/objects/effects/mines.dm:132
    [SignalHandler]
    public virtual void triggermine(atom_movable triggerer = null)
    {
        if (Dm.truthy(triggered)) //too busy detonating to detonate again
            return;
        // tg's icon2html(src, viewers(src)) before the mine's name: the chat icon is left out (the 3D client's chat is text)
        if (triggerer != null)
            visible_message(span_danger(I18n.T("[] sets off []!", triggerer, this)));
        else
            visible_message(span_danger(I18n.T("[] detonates!", this)));

        do_sparks(3, TRUE, this);
        Waiting.NoSleep(mineEffect(triggerer as mob));
        triggered = TRUE;
        SEND_SIGNAL(COMSIG_MINE_TRIGGERED, Dm.list(this, triggerer));
        qdel(this);
    }
}

// tg: code/game/objects/effects/mines.dm:147
public partial class obj_effect_mine_explosive : obj_effect_mine
{
    // tg: code/game/objects/effects/mines.dm:160
    public override async DmTask mineEffect(mob victim = null)
    {
        await explosion(this, range_devastation, range_heavy, range_light, range_flame, range_flash);
    }
}

// tg: code/game/objects/effects/mines.dm:181
public partial class obj_effect_mine_stun : obj_effect_mine
{
    // tg: code/game/objects/effects/mines.dm:185
    public override async DmTask mineEffect(mob victim = null)
    {
        if (isliving(victim) && Dm.truthy(await Adjacent(victim)))
            await ((mob_living)victim).Paralyze(stun_time);
    }
}

// tg: code/game/objects/effects/mines.dm:189
public partial class obj_effect_mine_kickmine : obj_effect_mine
{
    // tg: code/game/objects/effects/mines.dm:192
    public override async DmTask mineEffect(mob victim = null)
    {
        if (isliving(victim) && victim.client != null && Dm.truthy(await Adjacent(victim)))
        {
            to_chat(victim, span_userdanger(I18n.T("You have been kicked FOR NO REISIN!")));
            qdel(victim.client);
        }
    }
}

// tg: code/game/objects/effects/mines.dm:197
public partial class obj_effect_mine_gas : obj_effect_mine
{
    // tg: code/game/objects/effects/mines.dm:202
    public override async DmTask mineEffect(mob victim = null)
    {
        atmos_spawn_air(Dm.text("[]=[]", gas_type, gas_amount));
        await DmTask.CompletedTask;
    }
}

// tg: code/game/objects/effects/mines.dm:218
public partial class obj_effect_mine_sound : obj_effect_mine
{
    // tg: code/game/objects/effects/mines.dm:222
    public override async DmTask mineEffect(mob victim = null)
    {
        playsound(loc, sound, 100, TRUE);
        await DmTask.CompletedTask;
    }
}

/// These mines spawn pellet_clouds around them when triggered
// tg: code/game/objects/effects/mines.dm:230
public partial class obj_effect_mine_shrapnel : obj_effect_mine
{
    // tg: code/game/objects/effects/mines.dm:239
    public override async DmTask mineEffect(mob victim = null)
    {
        await DmTask.CompletedTask;
    }

    // tg: code/game/objects/effects/mines.dm:242
    [SignalHandler]
    public override void triggermine(atom_movable AM = null)
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_pellet_cloud), Dm.named("projectile_type", shrapnel_type), Dm.named("magnitude", shrapnel_magnitude)));
        base.triggermine(AM);
    }
}

// tg: code/game/objects/effects/mines.dm:250
public partial class obj_effect_mine_shrapnel_capspawn : obj_effect_mine_shrapnel
{
    // tg: code/game/objects/effects/mines.dm:261
    public override void now_armed()
    {
        base.now_armed();
        Waiting.NoSleep(set_light_on(TRUE));
    }
}

// tg: code/game/objects/effects/mines.dm:265
public partial class obj_item_minespawner : obj_item
{
    // tg: code/game/objects/effects/mines.dm:274
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(active))
            return dot;

        playsound(this, Dm.resource("sound/items/weapons/armbomb.ogg"), 70, TRUE);
        to_chat(user, span_warning(I18n.T("You arm \\the [], causing it to shake! It will deploy in 3 seconds.", this)));
        active = TRUE;
        _addtimer(Dm.@new<datum_callback>(this, nameof(deploy_mine)), 3 * SECONDS, file: "code/game/objects/effects/mines.dm", line: 282);
        return dot;
    }

    /// Deploys the mine and deletes itself
    // tg: code/game/objects/effects/mines.dm:285
    public virtual void deploy_mine()
    {
        do_alert_animation();
        playsound(loc, Dm.resource("sound/machines/chime.ogg"), 30, FALSE, -3);
        obj_effect_mine new_mine = Dm.@new(mine_type, get_turf(this)) as obj_effect_mine;
        visible_message(span_danger(I18n.T("\\The [] releases a puff of smoke, revealing \\a []!", this, new_mine)));
        obj_effect_particle_effect_fluid_smoke poof = Dm.@new<obj_effect_particle_effect_fluid_smoke>(get_turf(this));
        poof.lifetime = 3;
        qdel(this);
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/effects/mines.dm:28  /obj/effect/mine/Initialize's var/static/list/loc_connections
    internal DmList obj_effect_mine__Initialize__loc_connections;
}
