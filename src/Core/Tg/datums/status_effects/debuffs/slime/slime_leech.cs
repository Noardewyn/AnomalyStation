// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/slime/slime_leech.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

///A slime is latched on and is draining the owner's life
// tg: code/datums/status_effects/debuffs/slime/slime_leech.dm:8
public partial class datum_status_effect_slime_leech : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/slime/slime_leech.dm:14
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        this.our_slime = Dm.arg(__extra, 0) as mob_living_basic_slime;
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/datums/status_effects/debuffs/slime/slime_leech.dm:18
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (our_slime == null)
            return FALSE;
        if (!isslime(our_slime))
            return FALSE;
        RegisterSignals(our_slime, Dm.list(COMSIG_LIVING_DEATH, COMSIG_MOB_UNBUCKLED, COMSIG_QDELETING), nameof(on_buckle_end));
        return await base.on_apply(__extra);
    }

    ///Handles the slime being unbuckled: the victim is slimed unless protected
    // tg: code/datums/status_effects/debuffs/slime/slime_leech.dm:26
    [SignalHandler]
    public virtual void on_buckle_end()
    {
        // tg runtimes here (a null owner) when an earlier listener of the same signal already ended the feeding and deleted this
        // effect: SEND_SIGNAL queues its calls before making them (signals.dm), so the deleted effect is still called, as when
        // a feeding slime is deleted (its AI controller's COMSIG_QDELETING comes first). Ending here is that runtime's effect.
        if (QDELETED(this))
            return;
        float bio_protection = 100 - Dm.num(owner.getarmor(null, BIO));
        if (prob(bio_protection))
            Waiting.NoSleep(owner.apply_status_effect(typeof(datum_status_effect_slimed), our_slime.slime_type.rgb_code, Dm.eq(our_slime.slime_type.colour, SLIME_TYPE_RAINBOW) ? TRUE : FALSE));
        UnregisterSignal(our_slime, Dm.list(COMSIG_LIVING_DEATH, COMSIG_MOB_UNBUCKLED, COMSIG_QDELETING));
        if (!QDELETED(our_slime))
            our_slime.stop_feeding();
        qdel(this);
    }

    // tg: code/datums/status_effects/debuffs/slime/slime_leech.dm:35
    public override async DmTask<object> on_remove()
    {
        our_slime = null;
        return null;
    }

    // tg: code/datums/status_effects/debuffs/slime/slime_leech.dm:38
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        float spt = Dm.num(seconds_between_ticks);
        if (QDELETED(our_slime))
        {
            qdel(this);
            return null;
        }

        if (IS_UNCONSCIOUS_OR_CRIT(our_slime) || owner == null)
        {
            our_slime.stop_feeding(TRUE);
            return null;
        }

        if (owner.stat == DEAD) // our victim died
        {
            if (our_slime.client != null)
                to_chat(our_slime, span_info("This subject does not have a strong enough life energy anymore..."));

            if (Dm.truthy(owner._listen_lookup?[COMSIG_SLIME_DRAINED]))

                owner._SendSignal(COMSIG_SLIME_DRAINED, Dm.list(owner, our_slime));

            if (prob(60) && ishuman(owner) && owner.client != null && !Dm.truthy((our_slime.ai_controller as datum_ai_controller).blackboard[BB_SLIME_RABID]))
                (our_slime.ai_controller as datum_ai_controller)?.set_blackboard_key(BB_SLIME_RABID, TRUE); //we might go rabid after finishing to feed on a human with a client.

            our_slime?.stop_feeding();
            return null;
        }

        float totaldamage = 0; //total damage done to this unfortunate soul

        if (iscarbon(owner))
        {
            totaldamage += Dm.num(await (owner.adjust_brute_loss(rand(2, 4) * 0.5f * spt)));
            totaldamage += Dm.num(await (owner.adjust_tox_loss(rand(1, 2) * 0.5f * spt)));
        }

        if (isanimal_or_basicmob(owner))
        {
            float need_mob_update;
            need_mob_update = totaldamage += Dm.num(await (owner.adjust_brute_loss(rand(2, 4) * 0.5f * spt, updating_health: FALSE)));
            need_mob_update += totaldamage += Dm.num(await (owner.adjust_tox_loss(rand(1, 2) * 0.5f * spt, updating_health: FALSE)));
            if (need_mob_update != 0)
                await owner.updatehealth();
        }

        if (totaldamage >= 0) // adjust_brute_loss() returns a negative value on successful damage adjustment
        {
            our_slime.balloon_alert(our_slime, "not food!");
            our_slime.stop_feeding();
            return null;
        }

        if (totaldamage < 0 && SPT_PROB(5, spt) && owner.client != null)
        {
            DmList pain_lines = TgState.Current.datum_status_effect_slime_leech__pain_lines ??= Dm.list(
                "You can feel your body becoming weak!",
                "You feel like you're about to die!",
                "You feel every part of your body screaming in agony!",
                "A low, rolling pain passes through your body!",
                "Your body feels as if it's falling apart!",
                "You feel extremely weak!",
                "A sharp, deep pain bathes every inch of your body!");
            to_chat(owner, span_userdanger(Dm.text("[]", pick(pain_lines))));
        }

        await (our_slime.adjust_nutrition(-1 * 1.8f * totaldamage)); //damage is already modified by seconds_between_ticks
        await our_slime.adjust_brute_loss(-1.5f * spt);
        return null;
    }
}

public sealed partial class TgState
{
    internal DmList datum_status_effect_slime_leech__pain_lines;
}
