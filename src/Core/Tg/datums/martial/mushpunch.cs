// Translated from tgstation (snapshot 1d99bd7f): code/datums/martial/mushpunch.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X3b (D316): the Mushroom Punch (the wound-up punch that throws) and the odd mushroom that teaches it, whole.
// The mushroom people who grow it are not ported (not-translated.md section 147). The var blocks are generated (D111).

// tg: code/datums/martial/mushpunch.dm:1
public partial class datum_martial_art_mushpunch : datum_martial_art
{
    // tg: code/datums/martial/mushpunch.dm:5
    public override DmTask<object> harm_act(mob_living attacker = null, mob_living defender = null)
    {
        Waiting.Detach(charge_up_attack(attacker, defender)); // INVOKE_ASYNC
        return DmTask<object>.FromResult(MARTIAL_ATTACK_SUCCESS);
    }

    // tg: code/datums/martial/mushpunch.dm:9
    public virtual async DmTask charge_up_attack(mob_living attacker = null, mob_living defender = null)
    {
        to_chat(attacker, span_spiderbroodmother(I18n.T("You begin to wind up an attack...")));
        if (!Dm.truthy(await do_after(attacker, 2.5f * SECONDS, defender)))
        {
            to_chat(attacker, span_spiderbroodmother(I18n.T("<b>Your attack was interrupted!</b>")));
            return;
        }

        float final_damage = rand(15, 30);
        string atk_verb = Dm.str(pick("punch", "smash", "crack"));
        if (Dm.truthy(await defender.check_block(attacker, final_damage, Dm.text("[]'s []", attacker, atk_verb), UNARMED_ATTACK)))
            return;

        await attacker.do_attack_animation(defender, ATTACK_EFFECT_PUNCH);
        defender.visible_message(
            span_danger(I18n.T("[] []ed [] with such inhuman strength that it sends [] flying backwards!", attacker, atk_verb, defender, defender.p_them())),
            span_userdanger(I18n.T("You're []ed by [] with such inhuman strength that it sends you flying backwards!", atk_verb, attacker)),
            span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")),
            null,
            attacker);
        to_chat(attacker, span_danger(I18n.T("You [] [] with such inhuman strength that it sends [] flying backwards!", atk_verb, defender, defender.p_them())));
        await defender.apply_damage(final_damage, Dm.str(attacker.get_attack_type()));
        playsound(defender, "sound/effects/meteorimpact.ogg", 25, TRUE, -1);
        var throwtarget = get_edge_target_turf(attacker, get_dir(attacker, get_step_away(defender, attacker))) as atom;
        await defender.throw_at(throwtarget, 4, 2, attacker); //So stuff gets tossed around at the same time.
        await defender.Paralyze(2 * SECONDS);
        log_combat(attacker, defender, Dm.text("[] (Mushroom Punch)", atk_verb));
    }
}

// tg: code/datums/martial/mushpunch.dm:37
public partial class obj_item_mushpunch : obj_item
{
    // tg: code/datums/martial/mushpunch.dm:44
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (user is not mob_living living_user)
            return null;
        to_chat(user, span_spiderbroodmother(I18n.T("You devour [], and a confluence of skill and power from the mushroom enhances your punches! You do need a short moment to charge these powerful punches.", this)));
        var mush = Dm.@new<datum_martial_art_mushpunch>(user);
        await mush.teach(living_user);
        visible_message(
            span_warning(I18n.T("[] devours [].", user, this)),
            span_notice(I18n.T("You devour [].", this)));

        qdel(this);
        return null;
    }
}
