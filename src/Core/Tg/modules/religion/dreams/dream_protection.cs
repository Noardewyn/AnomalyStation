// Translated from tgstation (snapshot 1d99bd7f): code/modules/religion/dreams/dream_protection.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the dream sect's protection rite and its status effects (less damage while asleep and more while dreaming, a
// cyan outline; the temporary one a blessing gives a sleeper, the one it gives the dead). The var blocks are generated (D111).

// tg: code/modules/religion/dreams/dream_protection.dm:1
public partial class datum_religion_rites_dream_protection : datum_religion_rites
{
    // tg: code/modules/religion/dreams/dream_protection.dm:9
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        ritual_invocations = Dm.list(
            I18n.T("Protect our flock from harm, great shepherd []!..", GLOB.deity),
            I18n.T("Grant us peaceful slumber, free from nightmares and those who would do us harm!.."),
            I18n.T("Our sleepers shall be safe to dream to their heart's desire!.."));
    }

    // tg: code/modules/religion/dreams/dream_protection.dm:17
    public override async DmTask post_invoke_effects(mob_living user = null, atom religious_tool = null)
    {
        await base.post_invoke_effects(user, religious_tool);
        if (GLOB.religious_sect is not datum_religion_sect_dreams sect)
            return;

        sect.dream_protection = TRUE;

        foreach (mob_living follower in Dm.each_cast<mob_living>(GLOB.mob_living_list))
        {
            if (Dm.truthy(follower.mind?.holy_role))
                await follower.apply_status_effect(typeof(datum_status_effect_dream_protection));
        }
    }
}

// tg: code/modules/religion/dreams/dream_protection.dm:29
public partial class datum_status_effect_dream_protection : datum_status_effect
{
    // tg: code/modules/religion/dreams/dream_protection.dm:39
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        object dot = await base.on_apply(__extra);
        RegisterSignal(owner, COMSIG_MOB_APPLY_DAMAGE_MODIFIERS, nameof(modify_damage));
        RegisterSignals(owner, Dm.list(
            COMSIG_MOB_STATCHANGE,
            SIGNAL_ADDTRAIT(TRAIT_DREAMING),
            SIGNAL_REMOVETRAIT(TRAIT_DREAMING)), nameof(check_protection));
        check_protection();
        return dot;
    }

    // tg: code/modules/religion/dreams/dream_protection.dm:49
    public override async DmTask<object> on_remove()
    {
        object dot = await base.on_remove();
        UnregisterSignal(owner, COMSIG_MOB_APPLY_DAMAGE_MODIFIERS);
        REMOVE_TRAIT(owner, TRAIT_HOLY, TRAIT_STATUS_EFFECT(id));

        if (!QDELING(owner))
        {
            object filter = owner.get_filter(id);
            animate(filter, Dm.named("alpha", 0), Dm.named("time", 1 * SECONDS), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_OUT)));
            _addtimer(Dm.@new<datum_callback>(owner, nameof(datum.remove_filter), id), 1 * SECONDS, file: "code/modules/religion/dreams/dream_protection.dm", line: 57); // delay the filter removal to let the transition finish
        }
        return dot;
    }

    // tg: code/modules/religion/dreams/dream_protection.dm:59
    [SignalHandler]
    public virtual object check_protection()
    {
        if (IS_UNCONSCIOUS_AND_ALIVE(owner) || HAS_TRAIT(owner, TRAIT_DREAMING))
        {
            if (!Dm.truthy(has_filter))
            {
                owner.add_filter(id, 2, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", "#bde0dc"), Dm.kv("alpha", 0), Dm.kv("size", 2)));
                object filter = owner.get_filter(id);
                animate(filter, Dm.named("alpha", 150), Dm.named("time", 2 * SECONDS), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_IN)), Dm.named("loop", -1));
                animate(Dm.named("alpha", 0), Dm.named("time", 2 * SECONDS), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_OUT)));
                has_filter = TRUE;
            }
            ADD_TRAIT(owner, TRAIT_HOLY, TRAIT_STATUS_EFFECT(id));
        }
        else
        {
            if (Dm.truthy(has_filter))
            {
                object filter = owner.get_filter(id);
                animate(filter, Dm.named("alpha", 0), Dm.named("time", 1 * SECONDS), Dm.named("easing", Dm.bitor(SINE_EASING, EASE_OUT)));
                has_filter = FALSE;
            }
            REMOVE_TRAIT(owner, TRAIT_HOLY, TRAIT_STATUS_EFFECT(id));
        }
        return null;
    }

    // tg: code/modules/religion/dreams/dream_protection.dm:78
    [SignalHandler]
    public virtual void modify_damage(mob_living source = null, DmList damage_mods = null, params object[] __extra)
    {
        if (IS_UNCONSCIOUS_AND_ALIVE(owner))
            Dm.append(ref damage_mods, damage_mod);
        if (HAS_TRAIT(owner, TRAIT_DREAMING))
            Dm.append(ref damage_mods, damage_mod);
    }

    // tg: code/modules/religion/dreams/dream_protection.dm:85
    public override object get_examine_text(mob examiner = null)
    {
        if (IS_UNCONSCIOUS_AND_ALIVE(owner) || HAS_TRAIT(owner, TRAIT_DREAMING))
            return I18n.T("A soft cyan glow envelops [], reflecting light.", owner.p_them());
        return null;
    }
}

// Version that only lasts until they wake up (with a set duration backup)
// tg: code/modules/religion/dreams/dream_protection.dm:90
public partial class datum_status_effect_dream_protection_temporary : datum_status_effect_dream_protection
{
    // tg: code/modules/religion/dreams/dream_protection.dm:95
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (!IS_UNCONSCIOUS_AND_ALIVE(owner))
            return FALSE;
        if (Dm.truthy(owner.has_status_effect(typeof(datum_status_effect_dream_protection))))
            return FALSE;

        return await base.on_apply(__extra);
    }

    // tg: code/modules/religion/dreams/dream_protection.dm:103
    [SignalHandler]
    public override object check_protection()
    {
        object dot = base.check_protection();
        if (!Dm.truthy(has_filter)) // soon as it goes, we go
            qdel(this);
        return dot;
    }
}

// Version that works on dead mobs and lasts until they revive (with a set duration backup)
// tg: code/modules/religion/dreams/dream_protection.dm:109
public partial class datum_status_effect_dream_protection_deceased : datum_status_effect_dream_protection
{
    // tg: code/modules/religion/dreams/dream_protection.dm:113
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (owner.stat != DEAD)
            return FALSE;
        if (Dm.truthy(owner.has_status_effect(typeof(datum_status_effect_dream_protection))))
            return FALSE;

        RegisterSignal(owner, COMSIG_LIVING_REVIVE, nameof(mob_revived));
        ADD_TRAIT(owner, TRAIT_DREAMING, TRAIT_STATUS_EFFECT(id)); // "permanent" dreaming
        return await base.on_apply(__extra);
    }

    // tg: code/modules/religion/dreams/dream_protection.dm:123
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, COMSIG_LIVING_REVIVE);
        REMOVE_TRAIT(owner, TRAIT_DREAMING, TRAIT_STATUS_EFFECT(id));
        return await base.on_remove();
    }

    // tg: code/modules/religion/dreams/dream_protection.dm:128
    [SignalHandler]
    public virtual void mob_revived()
    {
        qdel(this);
    }
}
