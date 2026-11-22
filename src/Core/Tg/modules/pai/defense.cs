// Translated from tgstation (snapshot 1d99bd7f): code/modules/pai/defense.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI6 (D326): the whole file.

public partial class mob_living_silicon_pai : mob_living_silicon
{
    // tg: code/modules/pai/defense.dm:1
    public override async DmTask<object> blob_act(obj_structure_blob attacking_blob = null)
    {
        await DmTask.CompletedTask;
        return FALSE;
    }

    // tg: code/modules/pai/defense.dm:4
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        float s = Dm.num(severity);
        take_holo_damage(50 / s);
        await Stun(400 / s);
        if (Dm.truthy(holoform))
            await fold_in(force: TRUE);
        //Need more effects that aren't instadeath or permanent law corruption.
        //Ask and you shall receive
        switch ((int)rand(1, 3))
        {
            case 1:
                await adjust_stutter(1 * MINUTES / s);
                to_chat(this, span_danger(I18n.T("Warning: Feedback loop detected in speech module.")));
                break;
            case 2:
                await adjust_slurring(float.PositiveInfinity);
                to_chat(this, span_danger(I18n.T("Warning: Audio synthesizer CPU stuck.")));
                break;
            case 3:
                await set_derpspeech(float.PositiveInfinity);
                to_chat(this, span_danger(I18n.T("Warning: Vocabulary databank corrupted.")));
                break;
        }
        if (prob(40))
            set_active_language(get_random_spoken_language());
        return dot;
    }

    // tg: code/modules/pai/defense.dm:28
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        float s = Dm.num(severity);
        take_holo_damage(50 * s);
        switch ((int)s)
        {
            case EXPLODE_DEVASTATE: //RIP
                qdel(card);
                qdel(this);
                break;
            case EXPLODE_HEAVY:
                Waiting.NoSleep(fold_in(force: 1));
                Waiting.NoSleep(Paralyze(400));
                break;
            case EXPLODE_LIGHT:
                Waiting.NoSleep(fold_in(force: 1));
                Waiting.NoSleep(Paralyze(200));
                break;
        }
        return TRUE;
    }

    // tg: code/modules/pai/defense.dm:43
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        var human = user as mob_living_carbon_human;
        if (!Dm.truthy(human?.combat_mode))
        {
            visible_message(span_notice(I18n.T("[] gently pats [] on the head, eliciting an off-putting buzzing from its holographic field.", user, this)));
            return null;
        }
        await human.do_attack_animation(this);
        if (!Dm.eq(user.name, master_name))
        {
            visible_message(span_danger(I18n.T("[] stomps on []!.", user, this)));
            take_holo_damage(2);
            return null;
        }
        visible_message(span_notice(I18n.T("Responding to its master's touch, [] disengages its holochassis emitter, rapidly losing coherence.", this)));
        if (!Dm.truthy(await do_after(user, 1 * SECONDS, this)))
            return null;
        await fold_in();
        if (Dm.truthy(await human.put_in_hands(card)))
            user.visible_message(span_notice(I18n.T("[] promptly scoops up [] pAI's card.", user, user.p_their())));
        return null;
    }

    // tg: code/modules/pai/defense.dm:59
    public override async DmTask<object> bullet_act(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        object dot = await base.bullet_act(hitting_projectile, def_zone, piercing_hit, blocked);
        if (Dm.eq(dot, BULLET_ACT_HIT) && (Dm.truthy(hitting_projectile.stun) || Dm.truthy(hitting_projectile.paralyze)))
        {
            await fold_in(force: TRUE);
            visible_message(span_warning(I18n.T("The electrically-charged projectile disrupts []'s holomatrix, forcing [] to fold in!", this, p_them())));
        }
        return dot;
    }

    // tg: code/modules/pai/defense.dm:65
    public override object ignite_mob(object silent = null) => FALSE;

    // tg: code/modules/pai/defense.dm:68
    public virtual float take_holo_damage(float amount = 0)
    {
        holochassis_health = Math.Clamp(holochassis_health - amount, -50, HOLOCHASSIS_MAX_HEALTH);
        if (holochassis_health < 0)
            Waiting.Detach(fold_in(force: TRUE));
        if (amount > 0)
            to_chat(this, span_userdanger(I18n.T("The impact degrades your holochassis!")));
        update_health_hud();
        return amount;
    }

    /// Called when we take burn or brute damage, pass it to the shell instead
    // tg: code/modules/pai/defense.dm:78
    public virtual object on_shell_damaged(datum hurt = null, object type = null, object amount = null, object forced = null)
    {
        take_holo_damage(Dm.num(amount));
        return COMPONENT_IGNORE_CHANGE;
    }

    /// Called when we take stamina damage, pass it to the shell instead
    // tg: code/modules/pai/defense.dm:84
    public virtual object on_shell_weakened(datum hurt = null, object type = null, object amount = null, object forced = null)
    {
        take_holo_damage(Dm.num(amount) * (Dm.truthy(forced) ? 1 : 0.25f));
        return COMPONENT_IGNORE_CHANGE;
    }

    // tg: code/modules/pai/defense.dm:89
    public override object get_brute_loss() => HOLOCHASSIS_MAX_HEALTH - holochassis_health;

    // tg: code/modules/pai/defense.dm:92
    public override object get_fire_loss() => HOLOCHASSIS_MAX_HEALTH - holochassis_health;
}
