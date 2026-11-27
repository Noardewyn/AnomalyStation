// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/lavaland/mining_loot/megafauna/bubblegum.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// Mayhem in a bottle

// tg: code/modules/mining/lavaland/mining_loot/megafauna/bubblegum.dm:3
public partial class obj_item_mayhem : obj_item
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/bubblegum.dm:9
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.eq(await tgui_alert(user, I18n.T("Breaking the bottle will cause nearby crewmembers to go into a murderous frenzy. Be sure you know what you are doing..."), I18n.T("Break the bottle?"), Dm.list("Break it!", "DON'T")), "Break it!"))
            return null;

        if (QDELETED(this) || !Dm.truthy(user.is_holding(this)) || Dm.truthy(user.incapacitated))
            return null;

        foreach (mob_living_carbon_human target in Dm.each<mob_living_carbon_human>(range(7, user)))
            await target.apply_status_effect(typeof(datum_status_effect_mayhem));

        to_chat(user, span_notice(I18n.T("You shatter the bottle!")));
        playsound(user.loc, Dm.resource("sound/effects/glass/glassbr1.ogg"), 100, TRUE);
        message_admins(span_adminnotice(Dm.text("[] has activated a bottle of mayhem!", ADMIN_LOOKUPFLW(user))));
        user.log_message("activated a bottle of mayhem", LOG_ATTACK);
        qdel(this);
        return null;
    }
}

// H.E.C.K. Suit

// tg: code/modules/mining/lavaland/mining_loot/megafauna/bubblegum.dm:27
public partial class obj_item_clothing_suit_hooded_hostile_environment : obj_item_clothing_suit_hooded
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/bubblegum.dm:58
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_radiation_protected_clothing)));
        Waiting.NoSleep(AddElement(typeof(datum_element_gags_recolorable)));
        allowed = GLOB.mining_suit_allowed;
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/bubblegum.dm:64
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (loc is mob_living_carbon wearer && SPT_PROB(1, seconds_per_tick)) //cursed by bubblegum
        {
            if (prob(7.5f))
                Waiting.NoSleep(wearer.cause_hallucination(typeof(datum_hallucination_oh_yeah), "H.E.C.K suit", Dm.named("haunt_them", TRUE)));
            else
            {
                if (HAS_TRAIT(wearer, TRAIT_ANOSMIA)) //Anosmia quirk holder cannot fell any smell
                    to_chat(wearer, span_warning(Dm.text("[]", pick(I18n.T("You hear faint whispers."), I18n.T("You feel hot."), I18n.T("You hear a roar in the distance.")))));
                else
                    to_chat(wearer, span_warning(Dm.text("[]", pick(I18n.T("You hear faint whispers."), I18n.T("You smell ash."), I18n.T("You feel hot."), I18n.T("You hear a roar in the distance.")))));
            }
        }
        return null;
    }
}

// tg: code/modules/mining/lavaland/mining_loot/megafauna/bubblegum.dm:75
public partial class obj_item_clothing_head_hooded_hostile_environment : obj_item_clothing_head_hooded
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/bubblegum.dm:98
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering_wearable),
            Dm.named("speed", 0.5f * SECONDS),
            Dm.named("effectiveness", 150),
            Dm.named("bonus_modifier", 0),
            Dm.named("butcher_sound", null),
            Dm.named("disabled", null),
            Dm.named("can_be_blunt", TRUE),
            Dm.named("butcher_callback", Dm.@new<datum_callback>(this, nameof(consume)))));
        Waiting.NoSleep(AddElement(typeof(datum_element_radiation_protected_clothing)));
        Waiting.NoSleep(AddElement(typeof(datum_element_gags_recolorable)));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/bubblegum.dm:113
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        initial ??= FALSE;
        await base.equipped__proc(user, slot, initial);
        to_chat(user, span_notice(I18n.T("You feel a bloodlust. You can now butcher corpses with your bare arms.")));
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/bubblegum.dm:117
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        silent ??= FALSE;
        object dot = await base.dropped(user, silent, __extra);
        to_chat(user, span_notice(I18n.T("You lose your bloodlust.")));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/bubblegum.dm:121
    public virtual async DmTask consume(mob_living user = null, mob_living butchered = null)
    {
        if (Dm.truthy(Dm.bitand(butchered.mob_biotypes, Dm.bitor(MOB_ROBOTIC, MOB_SPIRIT))))
            return;
        float health_consumed = Dm.num(butchered.maxHealth) * 0.1f;
        await user.heal_ordered_damage(health_consumed, Dm.list(BRUTE, BURN, TOX));
        to_chat(user, span_notice(I18n.T("You heal from the corpse of [].", butchered)));
        object color_effect = user.add_client_colour(typeof(datum_client_colour_bloodlust), HELMET_TRAIT);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 1 * SECONDS > GC_FILTER_QUEUE ? WEAKREF(color_effect as datum) : color_effect), 1 * SECONDS, file: "code/modules/mining/lavaland/mining_loot/megafauna/bubblegum.dm", line: 129); // QDEL_IN(color_effect, 1 SECONDS)
    }
}
