// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/lavaland/mining_loot/eye_of_god.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// tg: code/modules/mining/lavaland/mining_loot/eye_of_god.dm:1
public partial class obj_item_clothing_glasses_godeye : obj_item_clothing_glasses
{
    // tg: code/modules/mining/lavaland/mining_loot/eye_of_god.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        scan_ability = Dm.@new<datum_action_cooldown_spell_pointed_scan>(this);
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/eye_of_god.dm:17
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref scan_ability);
        return base.Destroy(force);
    }

    // tg: code/modules/mining/lavaland/mining_loot/eye_of_god.dm:21
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (ishuman(user) && Dm.truthy(Dm.bitand(slot, ITEM_SLOT_EYES)))
        {
            ADD_TRAIT(this, TRAIT_NODROP, EYE_OF_GOD_TRAIT);
            await pain(user as mob_living);
            await scan_ability.Grant(user);
        }
    }

    // tg: code/modules/mining/lavaland/mining_loot/eye_of_god.dm:28
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        // Behead someone, their "glasses" drop on the floor
        // and thus, the god eye should no longer be sticky
        REMOVE_TRAIT(this, TRAIT_NODROP, EYE_OF_GOD_TRAIT);
        // And remove the scan ability, note that if we're being called from Destroy
        // that this may already be nulled and removed
        if (scan_ability != null)
            await scan_ability.Remove(user);
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/eye_of_god.dm:37
    public virtual async DmTask pain(mob_living victim = null)
    {
        to_chat(victim, span_userdanger(I18n.T("You experience blinding pain, as [] burrows into your skull.", this)));
        await victim.emote("scream");
        await victim.flash_act();
    }
}

// tg: code/modules/mining/lavaland/mining_loot/eye_of_god.dm:42
public partial class datum_action_cooldown_spell_pointed_scan : datum_action_cooldown_spell_pointed
{
    // tg: code/modules/mining/lavaland/mining_loot/eye_of_god.dm:56
    public override async DmTask<object> is_valid_target(atom cast_on = null)
    {
        if (!isliving(cast_on))
        {
            owner.balloon_alert(owner, "not a valid target!");
            return FALSE;
        }
        mob_living living_cast_on = cast_on as mob_living;
        if (living_cast_on.stat == DEAD)
        {
            owner.balloon_alert(owner, "target is dead!");
            return FALSE;
        }

        await DmTask.CompletedTask;
        return TRUE;
    }

    // tg: code/modules/mining/lavaland/mining_loot/eye_of_god.dm:67
    public override async DmTask<object> cast(atom cast_on = null)
    {
        object dot = await base.cast(cast_on);

        mob_living cast_on_living = cast_on as mob_living;
        if (Dm.truthy(cast_on_living.can_block_magic(MAGIC_RESISTANCE_MIND, charge_cost: 0)))
        {
            to_chat(owner, span_warning(I18n.T("As we apply our dissecting vision, we are abruptly cut short. They have some kind of enigmatic mental defense. It seems we've been foiled.")));
            return dot;
        }

        if (cast_on == owner)
        {
            to_chat(owner, span_warning(I18n.T("The last time a god stared too closely into their own reflection, they became transfixed for all of time. Do not let us become like them.")));
            return dot;
        }

        mob_living living_owner = owner as mob_living;
        mob_living living_scanned = cast_on_living;
        await living_scanned.apply_status_effect(typeof(datum_status_effect_rebuked));
        datum_status_effect_agent_pinpointer scan_pinpointer = await living_owner.apply_status_effect(typeof(datum_status_effect_agent_pinpointer_scan)) as datum_status_effect_agent_pinpointer;
        scan_pinpointer.scan_target = living_scanned;

        to_chat(living_scanned, span_warning(I18n.T("You briefly see a flash of []'s face before being knocked off-balance by an unseen force!", living_owner)));
        living_scanned.add_filter("scan", 2, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", COLOR_RED), Dm.kv("size", 1)));
        _addtimer(Dm.@new<datum_callback>(living_scanned, nameof(datum.remove_filter), "scan"), 30 * SECONDS, file: "code/modules/mining/lavaland/mining_loot/eye_of_god.dm", line: 89);

        healthscan(living_owner, living_scanned, SCANNER_VERBOSE, SCANPOWER_ADVANCED);

        owner.playsound_local(get_turf(owner), Dm.resource("sound/effects/magic/smoke.ogg"), 50, TRUE);
        owner.balloon_alert(owner, Dm.text("[] scanned", living_scanned));
        _addtimer(Dm.@new<datum_callback>(this, nameof(send_cooldown_end_message)), Dm.num(cooldown_time), file: "code/modules/mining/lavaland/mining_loot/eye_of_god.dm", line: 95);
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/eye_of_god.dm:95
    public virtual void send_cooldown_end_message()
    {
        owner?.balloon_alert(owner, "scan recharged");
    }
}

// tg: code/modules/mining/lavaland/mining_loot/eye_of_god.dm:98
public partial class datum_status_effect_agent_pinpointer_scan : datum_status_effect_agent_pinpointer
{
    // tg: code/modules/mining/lavaland/mining_loot/eye_of_god.dm:107
    public override void scan_for_target()
    {
        return;
    }
}
