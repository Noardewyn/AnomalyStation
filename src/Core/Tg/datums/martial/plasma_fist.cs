// Translated from tgstation (snapshot 1d99bd7f): code/datums/martial/plasma_fist.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X3b (D316): the Plasma Fist (the Tornado Sweep, the Throwback, the Plasma Fist's gib and its plasma soul, the
// Apotheosis), whole but the Apotheosis' plasmaman (the species is not ported; the screen's purple flash since gaps MO2 translated
// flash_color); not-translated.md section 147. The var blocks are generated (D111).

// tg: code/datums/martial/plasma_fist.dm:5
public partial class datum_martial_art_plasma_fist : datum_martial_art
{
    // tg: code/datums/martial/plasma_fist.dm:1-3  #define TORNADO_COMBO "HHD" ... (file-local, #undef at :208-210)
    private const string TORNADO_COMBO = "HHD";
    private const string THROWBACK_COMBO = "DHD";
    private const string PLASMA_COMBO = "HDDDH";

    // tg: code/datums/martial/plasma_fist.dm:16
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        tornado_spell = Dm.@new<datum_action_cooldown_spell_aoe_repulse>(this);
    }

    // tg: code/datums/martial/plasma_fist.dm:20
    public override object Destroy(float? force = null)
    {
        tornado_spell = null;
        return base.Destroy(force);
    }

    // tg: code/datums/martial/plasma_fist.dm:24
    public virtual async DmTask<object> check_streak(mob_living attacker = null, mob_living defender = null)
    {
        if (Dm.truthy(findtext(streak, TORNADO_COMBO)))
        {
            if (attacker == defender) //helps using apotheosis
                return FALSE;
            await reset_streak();
            return await Tornado(attacker, defender);
        }
        if (Dm.truthy(findtext(streak, THROWBACK_COMBO)))
        {
            if (attacker == defender) //helps using apotheosis
                return FALSE;
            await reset_streak();
            return await Throwback(attacker, defender);
        }
        if (Dm.truthy(findtext(streak, PLASMA_COMBO)))
        {
            await reset_streak();
            if (attacker == defender && !Dm.truthy(nobomb))
                return await Apotheosis(attacker, defender);
            return await Plasma(attacker, defender);
        }
        return FALSE;
    }

    // tg: code/datums/martial/plasma_fist.dm:42
    public virtual async DmTask<object> Tornado(mob_living attacker = null, mob_living defender = null)
    {
        await attacker.say("TORNADO SWEEP!", forced: "plasma fist");
        // dance_rotate sets waitfor = FALSE (__HELPERS/mobs.dm)
        Waiting.Detach(dance_rotate(attacker, Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.playsound), attacker, "sound/items/weapons/punch1.ogg", 15, TRUE, -1)));
        await tornado_spell.cast(attacker);
        log_combat(attacker, defender, "tornado sweeped (Plasma Fist)");
        return TRUE;
    }

    // tg: code/datums/martial/plasma_fist.dm:49
    public virtual async DmTask<object> Throwback(mob_living attacker = null, mob_living defender = null)
    {
        defender.visible_message(
            span_danger(I18n.T("[] hits [] with Plasma Punch!", attacker, defender)),
            span_userdanger(I18n.T("You're hit with a Plasma Punch by []!", attacker)),
            span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")),
            null,
            attacker);
        to_chat(attacker, span_danger(I18n.T("You hit [] with Plasma Punch!", defender)));
        playsound(defender, "sound/items/weapons/punch1.ogg", 50, TRUE, -1);
        var throw_target = get_edge_target_turf(defender, get_dir(defender, get_step_away(defender, attacker))) as atom;
        await defender.throw_at(throw_target, 200, 4, attacker);
        await attacker.say("HYAH!", forced: "plasma fist");
        log_combat(attacker, defender, "threw back (Plasma Fist)");
        return TRUE;
    }

    // tg: code/datums/martial/plasma_fist.dm:65
    public virtual async DmTask<object> Plasma(mob_living attacker = null, mob_living defender = null)
    {
        bool hasclient = defender.client != null;

        await attacker.do_attack_animation(defender, ATTACK_EFFECT_PUNCH);
        playsound(defender, "sound/items/weapons/punch1.ogg", 50, TRUE, -1);
        await attacker.say("PLASMA FIST!", forced: "plasma fist");
        defender.visible_message(
            span_danger(I18n.T("[] hits [] with THE PLASMA FIST TECHNIQUE!", attacker, defender)),
            span_userdanger(I18n.T("You're suddenly hit with THE PLASMA FIST TECHNIQUE by []!", attacker)),
            span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")),
            null,
            attacker);
        to_chat(attacker, span_danger(I18n.T("You hit [] with THE PLASMA FIST TECHNIQUE!", defender)));
        log_combat(attacker, defender, "gibbed (Plasma Fist)");
        var Dturf = get_turf(defender) as turf;
        defender.investigate_log("has been gibbed by plasma fist.", INVESTIGATE_DEATHS);
        await defender.gib(DROP_ALL_REMAINS);
        if (Dm.truthy(nobomb))
            return null;

        if (!hasclient)
        {
            to_chat(attacker, span_warning(I18n.T("Taking this plasma energy for your </span>[]<span class='warning'> would bring dishonor to the clan!", span_notice("Apotheosis"))));
            Dm.@new<obj_effect_temp_visual_plasma_soul>(Dturf); //doesn't beam to you, so it just hangs around and poofs.
        }
        else if (plasma_power >= plasma_cap)
        {
            to_chat(attacker, span_warning(I18n.T("You cannot power up your </span>[]<span class='warning'> any more!", span_notice("Apotheosis"))));
            Dm.@new<obj_effect_temp_visual_plasma_soul>(Dturf); //doesn't beam to you, so it just hangs around and poofs.
        }
        else
        {
            plasma_power += plasma_increment;
            to_chat(attacker, span_nicegreen(I18n.T("Power increasing! Your </span>[]<span class='nicegreen'> is now at power level []!", span_notice("Apotheosis"), plasma_power)));
            Dm.@new<obj_effect_temp_visual_plasma_soul>(Dturf, attacker);
            object oldcolor = attacker.color;
            attacker.color = "#9C00FF";
            flash_color(attacker, flash_color: "#9C00FF", flash_time: 3 * SECONDS);
            Dm.animate(attacker, Dm.named("color", oldcolor), Dm.named("time", 3 * SECONDS));
        }

        return TRUE;
    }

    // tg: code/datums/martial/plasma_fist.dm:105
    public virtual async DmTask<object> Apotheosis(mob_living user = null, mob_living target = null)
    {
        await user.say("APOTHEOSIS!!", forced: "plasma fist");
        if (user is mob_living_carbon_human human_attacker)
        {
            // human_attacker.set_species(/datum/species/plasmaman)
            Dm.Deferred("code/datums/martial/plasma_fist.dm:109", "Apotheosis: set_species(/datum/species/plasmaman), the plasmaman body before the blast (the plasmaman species is not ported)");
            human_attacker.add_traits(Dm.list(TRAIT_FORCED_STANDING, TRAIT_BOMBIMMUNE), GetType());
            await human_attacker.unequip_everything();
            human_attacker.underwear = "Nude";
            human_attacker.undershirt = "Nude";
            human_attacker.socks = "Nude";
            await human_attacker.update_body();
        }

        var boomspot = get_turf(user) as turf;
        //before ghosting to prevent issues
        log_combat(user, user, Dm.text("triggered final plasma explosion with size [], [], [] (Plasma Fist)", plasma_power, plasma_power * 2, plasma_power * 4));
        message_admins(Dm.text("[] triggered final plasma explosion with size [], [], [].", key_name_admin(user), plasma_power, plasma_power * 2, plasma_power * 4));

        to_chat(user, span_userdanger(I18n.T("The explosion knocks your soul out of your body!")));
        user.ghostize(FALSE); //prevents... horrible memes just believe me

        await user.apply_damage(rand(50, 70), BRUTE, wound_bonus: CANT_WOUND);

        _addtimer(Dm.@new<datum_callback>(this, nameof(Apotheosis_end), user), 6 * SECONDS, file: "code/datums/martial/plasma_fist.dm", line: 127);
        playsound(boomspot, "sound/items/weapons/punch1.ogg", 50, TRUE, -1);
        // explosion_cause = src: the port's explosion() takes an atom cause (its log names it), and the martial art is a datum
        await explosion(user, devastation_range: plasma_power, heavy_impact_range: plasma_power * 2, light_impact_range: plasma_power * 4, ignorecap: TRUE, explosion_cause: null);
        plasma_power = 1; //just in case there is any clever way to cause it to happen again
        return TRUE;
    }

    // tg: code/datums/martial/plasma_fist.dm:133
    public virtual async DmTask Apotheosis_end(mob_living dying = null)
    {
        dying.remove_traits(Dm.list(TRAIT_FORCED_STANDING, TRAIT_BOMBIMMUNE), GetType());
        if (Dm.eq(dying.stat, DEAD))
            return;
        dying.investigate_log("has been killed by plasma fist apotheosis.", INVESTIGATE_DEATHS);
        await dying.death();
    }

    // tg: code/datums/martial/plasma_fist.dm:140
    public override async DmTask<object> harm_act(mob_living attacker = null, mob_living defender = null)
    {
        if (Dm.truthy(await defender.check_block(attacker, 10, attacker.name, UNARMED_ATTACK)))
            return MARTIAL_ATTACK_FAIL;

        await add_to_streak("H", defender);
        return Dm.truthy(await check_streak(attacker, defender)) ? MARTIAL_ATTACK_SUCCESS : MARTIAL_ATTACK_INVALID;
    }

    // tg: code/datums/martial/plasma_fist.dm:147
    public override async DmTask<object> disarm_act(mob_living attacker = null, mob_living defender = null)
    {
        if (Dm.truthy(await defender.check_block(attacker, 0, attacker.name, UNARMED_ATTACK)))
            return MARTIAL_ATTACK_FAIL;
        await add_to_streak("D", defender);
        if (Dm.truthy(await check_streak(attacker, defender)))
            return MARTIAL_ATTACK_SUCCESS;
        if (attacker == defender) //there is no disarming yourself, so we need to let plasma fist user know
        {
            to_chat(attacker, span_notice(I18n.T("You have added a disarm to your streak.")));
            return MARTIAL_ATTACK_FAIL;
        }
        return MARTIAL_ATTACK_INVALID;
    }

    // tg: code/datums/martial/plasma_fist.dm:158
    public override async DmTask<object> grab_act(mob_living attacker = null, mob_living defender = null)
    {
        if (Dm.truthy(await defender.check_block(attacker, 0, Dm.text("[]'s grab", attacker), UNARMED_ATTACK)))
            return MARTIAL_ATTACK_FAIL;

        await add_to_streak("G", defender);
        return Dm.truthy(await check_streak(attacker, defender)) ? MARTIAL_ATTACK_SUCCESS : MARTIAL_ATTACK_INVALID;
    }

    // tg: code/datums/martial/plasma_fist.dm:165
    public override object get_style_help()
    {
        DmList dot = Dm.list();

        var martial = LAZYACCESS(holder.martial_arts, 1) as datum_martial_art_plasma_fist; // GET_ACTIVE_MARTIAL_ART(holder) (__DEFINES/combat.dm:353), inline
        Dm.append(dot, I18n.T("<b><i>You clench your fists and have a flashback of knowledge...</i></b>"));
        Dm.append(dot, I18n.T("[]: Punch Punch Shove. Repulses opponent and everyone back.", span_notice("Tornado Sweep")));
        Dm.append(dot, I18n.T("[]: Shove Punch Shove. Throws the opponent and an item at them.", span_notice("Throwback")));
        Dm.append(dot, I18n.T("[]: Punch Shove Shove Shove Punch. Instantly gibs an opponent.[]", span_notice("The Plasma Fist"), Dm.truthy(martial.nobomb) ? "" : I18n.T(" Each kill with this grows your [] explosion size.", span_notice("Apotheosis"))));
        if (!Dm.truthy(martial.nobomb))
            Dm.append(dot, I18n.T("[]: Use [] on yourself. Sends you away in a glorious explosion.", span_notice("Apotheosis"), span_notice("The Plasma Fist")));
        return dot;
    }
}

// tg: code/datums/martial/plasma_fist.dm:178
public partial class obj_effect_temp_visual_plasma_soul : obj_effect_temp_visual
{
    // tg: code/datums/martial/plasma_fist.dm:186
    [DmParams("mapload", "_beam_target")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object _beam_target = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        beam_target = _beam_target as atom_movable;
        if (beam_target != null)
        {
            var beam = Beam(beam_target, "plasmabeam", beam_type: typeof(obj_effect_ebeam_plasma_fist), time: 3 * SECONDS) as datum_beam;
            Dm.animate(beam.visuals, Dm.named("alpha", 0), Dm.named("time", 3 * SECONDS));
        }
        Dm.animate(this, Dm.named("alpha", 0), Dm.named("transform", Dm.@new<matrix>().Scale(0.5f)), Dm.named("time", 3 * SECONDS));
        return dot;
    }

    // tg: code/datums/martial/plasma_fist.dm:194
    public override object Destroy(float? force = null)
    {
        if (beam_target == null)
            visible_message(span_notice(I18n.T("[] fades away...", this)));
        return base.Destroy(force);
    }
}
