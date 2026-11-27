// Translated from tgstation (snapshot 1d99bd7f): code/modules/religion/dreams/slumber_party.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the dream sect's slumber party, which puts everyone around the altar to sleep in one shared dream that heals
// them, and the shared dream. The var blocks are generated (D111).

// tg: code/modules/religion/dreams/slumber_party.dm:1
public partial class datum_religion_rites_slumber_party : datum_religion_rites
{
    // tg: code/modules/religion/dreams/slumber_party.dm:10
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        ritual_invocations = Dm.list(
            I18n.T("Sleep now, flock of [], and share in each other's dreams...", GLOB.deity),
            I18n.T("Our slumber shall rejuvenate us for the trials ahead..."),
            I18n.T("May we all wake up refreshed and renewed!"));
    }

    // tg: code/modules/religion/dreams/slumber_party.dm:18
    public override async DmTask post_invoke_effects(mob_living user = null, atom religious_tool = null)
    {
        await base.post_invoke_effects(user, religious_tool);
        datum_dream_random base_dream = Dm.@new<datum_dream_random>();
        foreach (mob_living_carbon nearby_guy in Dm.each<mob_living_carbon>(view(5, get_turf(religious_tool))))
            await nearby_guy.apply_status_effect(typeof(datum_status_effect_slumber_party), base_dream.GenerateDream(user as mob_living_carbon));
        qdel(base_dream);
    }
}

// tg: code/modules/religion/dreams/slumber_party.dm:25
public partial class datum_status_effect_slumber_party : datum_status_effect
{
    // tg: code/modules/religion/dreams/slumber_party.dm:34
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        this.shared_dream = Dm.arg(__extra, 0) as DmList;
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/modules/religion/dreams/slumber_party.dm:38
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (Dm.truthy(IS_CULTIST(owner)))
        {
            datum_antagonist_cult cultist = GET_CULTIST(owner);
            if (Dm.truthy(cultist.cult_team?.cult_ascendent))
                return FALSE;
        }

        if (IS_HERETIC(owner))
        {
            datum_antagonist_heretic heretic = GET_HERETIC(owner);
            if (Dm.truthy(heretic?.ascended))
                return FALSE;
        }

        if (!Dm.truthy(Dm.bitand(owner.mob_biotypes, MOB_ORGANIC)))
            return FALSE;

        if (!Dm.truthy(await owner.SetSleeping(duration)))
            return FALSE;

        if (Dm.truthy(owner.mind?.holy_role))
            healing *= 2;
        else if (Dm.truthy(owner.can_block_magic(Dm.bitor(MAGIC_RESISTANCE_HOLY, MAGIC_RESISTANCE_MIND), 1)))
            return FALSE;

        RegisterSignal(owner, COMSIG_PRE_DREAMING, nameof(add_shared_dream));
        RegisterSignal(owner, COMSIG_START_DREAMING, nameof(start_shared_dream));
        RegisterSignal(owner, COMSIG_MOB_APPLY_DAMAGE, nameof(damage_applied));
        if (iscarbon(owner))
            _addtimer(Dm.@new<datum_callback>(this, nameof(force_dream)), rand(4, 8) * SECONDS, TIMER_DELETE_ME, file: "code/modules/religion/dreams/slumber_party.dm", line: 64);
        return TRUE;
    }

    // tg: code/modules/religion/dreams/slumber_party.dm:68
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, COMSIG_PRE_DREAMING);
        UnregisterSignal(owner, COMSIG_START_DREAMING);
        UnregisterSignal(owner, COMSIG_MOB_APPLY_DAMAGE);
        REMOVE_TRAIT(owner, TRAIT_DREAMING, TRAIT_STATUS_EFFECT(id));
        await owner.adjust_drowsiness(20 * SECONDS);
        return null;
    }

    // tg: code/modules/religion/dreams/slumber_party.dm:75
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (!IS_UNCONSCIOUS_AND_ALIVE(owner))
        {
            qdel(this);
            return null;
        }

        float seconds = Dm.num(seconds_between_ticks);
        await owner.heal_overall_damage(healing * seconds, healing * seconds, required_bodytype: BODYTYPE_ORGANIC);
        await owner.adjust_tox_loss(healing * seconds * 0.50f, required_biotype: MOB_ORGANIC);
        await owner.adjust_oxy_loss(healing * seconds * 0.25f, required_biotype: MOB_ORGANIC);
        return null;
    }

    // tg: code/modules/religion/dreams/slumber_party.dm:84
    public virtual void force_dream()
    {
        mob_living_carbon dreamer = owner as mob_living_carbon;
        if (HAS_TRAIT(dreamer, TRAIT_DREAMING))
            return; // dreamed naturally already
        dreamer.dream();
    }

    // tg: code/modules/religion/dreams/slumber_party.dm:90
    [SignalHandler]
    public virtual void add_shared_dream(datum source = null, DmList dream_pool = null)
    {
        dream_pool[Dm.@new<datum_dream_shared>(shared_dream)] = 2000f;
    }

    // tg: code/modules/religion/dreams/slumber_party.dm:94
    [SignalHandler]
    public virtual void start_shared_dream(datum source = null, datum_dream current_dream = null)
    {
        ADD_TRAIT(owner, TRAIT_DREAMING, TRAIT_STATUS_EFFECT(id)); // so they don't have any OTHER dreams
    }

    // tg: code/modules/religion/dreams/slumber_party.dm:98
    [SignalHandler]
    public virtual void damage_applied(mob_living source = null, object damage_amount = null, params object[] __extra)
    {
        Waiting.NoSleep(owner.AdjustSleeping(-Dm.num(damage_amount) * 0.5f * SECONDS));
    }
}

// tg: code/modules/religion/dreams/slumber_party.dm:102
public partial class datum_dream_shared : datum_dream
{
    // tg: code/modules/religion/dreams/slumber_party.dm:107
    public override async DmTask New(params object[] __extra)
    {
        DmList shared_dream = Dm.arg(__extra, 0) as DmList;
        await base.New(__extra);
        generated_dream = LAZYLISTDUPLICATE(shared_dream);
    }

    // tg: code/modules/religion/dreams/slumber_party.dm:111
    public override object GenerateDream(mob_living_carbon dreamer = null)
    {
        if (!Dm.truthy(LAZYLEN(generated_dream)))
            throw Dm.CRASH("Shared dream has no generated dream fragments!", "code/modules/religion/dreams/slumber_party.dm", 113);
        return generated_dream;
    }
}
