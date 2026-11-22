// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/magic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate G4b (D316): the magic gun base, whole but the fantasy affixes and the variable editor (live-partial rows), for the
// meat hook. The wands and staves themselves stay with the wizard.

// tg: code/modules/projectiles/guns/magic.dm:2
public partial class obj_item_gun_magic : obj_item_gun
{
    // The first definition of Initialize (tg defines it twice on this type; the second's ..() calls this one, rules 2.5).
    // tg: code/modules/projectiles/guns/magic.dm:38
    public virtual object Initialize__d0(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_ITEM_MAGICALLY_CHARGED, nameof(on_magic_charge));
        return dot;
    }

    // tg: code/modules/projectiles/guns/magic.dm:54
    public override void fire_sounds()
    {
        float pitch_to_use = 1;

        if (Dm.truthy(pitch_with_charges) && max_charges > 1)
            pitch_to_use = LERP(1, 0.4f, Dm.num(Dm.pow(1 - (charges / max_charges), 2)));

        sound playing_sound = sound(Dm.truthy(suppressed) ? suppressed_sound : fire_sound);
        playing_sound.pitch = pitch_to_use;

        if (Dm.truthy(suppressed))
            playsound(this, playing_sound, suppressed_volume, vary_fire_sound, ignore_walls: FALSE, extrarange: SILENCED_SOUND_EXTRARANGE, falloff_distance: 0);
        else
            playsound(this, playing_sound, fire_sound_volume, vary_fire_sound);
    }

    /**
     * Signal proc for [COMSIG_ITEM_MAGICALLY_CHARGED]
     *
     * Adds uses to wands or staffs.
     */
    // tg: code/modules/projectiles/guns/magic.dm:73
    [SignalHandler]
    public virtual object on_magic_charge(datum source = null, datum spell = null, mob_living caster = null)
    {
        object dot = COMPONENT_ITEM_CHARGED;

        // Non-self charging staves and wands can potentially expire
        if (!Dm.truthy(self_charging) && Dm.truthy(max_charges) && prob(80))
            max_charges--;

        if (max_charges <= 0)
        {
            max_charges = 0;
            dot = Dm.bitor(dot, COMPONENT_ITEM_BURNT_OUT);
        }

        charges = max_charges;
        update_appearance(UPDATE_ICON_STATE);
        Waiting.NoSleep(recharge_newshot());

        return dot;
    }

    // tg: code/modules/projectiles/guns/magic.dm:92
    public override async DmTask<object> process_fire(atom target = null, mob_living user = null, float? message = null, object @params = null, string zone_override = null, float? bonus_spread = null)
    {
        if (Dm.truthy(no_den_usage))
        {
            area A = get_area(user) as area;
            if (istype(A, typeof(area_centcom_wizard_station)))
            {
                add_fingerprint(user);
                to_chat(user, span_warning(I18n.T("You know better than to violate the security of The Den, best wait until you leave to use [].", this)));
                return null;
            }
            else
            {
                no_den_usage = FALSE; // Well you're probably not going back
            }
        }
        if (!Dm.truthy(user.can_cast_magic(antimagic_flags)))
        {
            add_fingerprint(user);
            return null;
        }
        return await base.process_fire(target, user, message, @params, zone_override, bonus_spread);
    }

    // tg: code/modules/projectiles/guns/magic.dm:106
    public override object can_shoot()
    {
        return charges;
    }

    // tg: code/modules/projectiles/guns/magic.dm:109
    public override async DmTask recharge_newshot(params object[] __extra)
    {
        if (!Dm.truthy(charges) || chambered == null || chambered.loaded_projectile != null)
            return;
        chambered.newshot();
        await base.recharge_newshot(__extra);
    }

    // tg: code/modules/projectiles/guns/magic.dm:115
    public override async DmTask handle_chamber(float? empty_chamber = null, float? from_firing = null, float? chamber_next_round = null)
    {
        if (chambered != null && chambered.loaded_projectile == null) //if BB is null, i.e the shot has been fired...
        {
            charges--; //... drain a charge
            await recharge_newshot();
        }
    }

    // The second definition of Initialize (rules 2.5: its ..() is the first definition, Initialize__d0).
    // tg: code/modules/projectiles/guns/magic.dm:120
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = Initialize__d0(mapload, __extra);
        charges = max_charges;
        if (ammo_type != null)
            chambered = Dm.@new(ammo_type, this) as obj_item_ammo_casing;
        if (Dm.truthy(self_charging))
            START_PROCESSING(SSobj, this);
        RegisterSignal(this, COMSIG_ITEM_RECHARGED, nameof(instant_recharge));
        return dot;
    }

    // tg: code/modules/projectiles/guns/magic.dm:129
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(self_charging))
            STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/modules/projectiles/guns/magic.dm:134
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (charges >= max_charges)
        {
            charge_timer = 0;
            return null;
        }
        charge_timer += Dm.num(seconds_per_tick);
        if (charge_timer < recharge_rate)
            return 0;
        charge_timer = 0;
        charges++;
        if (charges == 1)
            Waiting.NoSleep(recharge_newshot());
        return 1;
    }

    // tg: code/modules/projectiles/guns/magic.dm:147
    public override async DmTask shoot_with_empty_chamber(mob_living user = null)
    {
        to_chat(user, span_warning(I18n.T("\\The [] whizzles quietly.", this)));
    }

    // tg: code/modules/projectiles/guns/magic.dm:150
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is twisting [] above [] head, releasing a magical blast! It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));
        if (Dm.truthy(can_user_shoot(user)))
        {
            charges--;
            return await do_suicide(user);
        }
        user.visible_message(span_suicide(I18n.T("...but nothing happens.")));
        return SHAME;
    }

    /// Extend to do something funny
    // tg: code/modules/projectiles/guns/magic.dm:159
    public virtual async DmTask<object> do_suicide(mob_living user = null)
    {
        playsound(loc as atom, fire_sound, 50, TRUE, -1);
        return FIRELOSS;
    }

    /// Returns true if specified mob can fire this weapon
    // tg: code/modules/projectiles/guns/magic.dm:164
    public virtual object can_user_shoot(mob_living user = null)
    {
        return Dm.truthy(can_shoot()) && Dm.truthy(user.can_cast_magic(antimagic_flags)) ? TRUE : FALSE;
    }

    // tg: code/modules/projectiles/guns/magic.dm:173
    [SignalHandler]
    public virtual void instant_recharge()
    {
        charges = max_charges;
        Waiting.NoSleep(recharge_newshot());
        update_appearance();
    }
}
