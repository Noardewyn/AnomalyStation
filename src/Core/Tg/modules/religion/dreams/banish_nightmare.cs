// Translated from tgstation (snapshot 1d99bd7f): code/modules/religion/dreams/banish_nightmare.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the dream sect's rite that banishes a nightmare's corpse or heart for favour, its mood and the blessed sacred heart
// it rewards. The var blocks are generated (D111).

// tg: code/modules/religion/dreams/banish_nightmare.dm:1
public partial class datum_religion_rites_banish_nightmare : datum_religion_rites
{
    // tg: code/modules/religion/dreams/banish_nightmare.dm:8
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        ritual_invocations = Dm.list(
            I18n.T("We have bested a terrible Nightmare, a plague upon our station!.."),
            I18n.T("With the power of [], we cast it out!..", GLOB.deity),
            I18n.T("This invader of dreams has no place here..."),
            I18n.T("May it trouble our flock no longer."));
    }

    // tg: code/modules/religion/dreams/banish_nightmare.dm:17
    public override async DmTask<object> perform_rite(mob_living user = null, atom religious_tool = null)
    {
        bool has_nightmare = false;
        foreach (mob_living_carbon_human nightmare in Dm.each<mob_living_carbon_human>(get_turf(religious_tool)))
        {
            if (HAS_TRAIT(nightmare, TRAIT_NIGHTMARISH))
            {
                has_nightmare = true;
                break;
            }
        }

        foreach (obj_item_organ organ in Dm.each<obj_item_organ>(get_turf(religious_tool)))
        {
            if (organ is obj_item_organ_heart_nightmare)
            {
                has_nightmare = true;
                break;
            }
        }

        if (!has_nightmare)
        {
            to_chat(user, span_warning(I18n.T("There is no Nightmare corpse or heart to banish!")));
            return FALSE;
        }

        return await base.perform_rite(user, religious_tool);
    }

    // tg: code/modules/religion/dreams/banish_nightmare.dm:35
    public override async DmTask post_invoke_effects(mob_living user = null, atom religious_tool = null)
    {
        await base.post_invoke_effects(user, religious_tool);
        float favor = 0;
        float give_heart = 0;
        foreach (mob_living_carbon_human nightmare in Dm.each<mob_living_carbon_human>(get_turf(religious_tool)))
        {
            if (!HAS_TRAIT(nightmare, TRAIT_NIGHTMARISH))
                continue;

            if (nightmare.get_organ_slot(ORGAN_SLOT_HEART) is obj_item_organ_heart_nightmare)
            {
                give_heart += 1;
                favor += 100;
            }

            await nightmare.dust(just_ash: TRUE, drop_items: TRUE, give_moodlet: FALSE, force: TRUE);
            favor += 200;
        }

        foreach (obj_item_organ organ in Dm.each<obj_item_organ>(get_turf(religious_tool)))
        {
            if (organ is not obj_item_organ_heart_nightmare)
                continue;

            qdel(organ);
            favor += 100;
            give_heart += 1;
        }

        if (favor <= 0)
            throw Dm.CRASH("Banish nightmare rite invoked without finding a nightmare or nightmare heart to banish.", "code/modules/religion/dreams/banish_nightmare.dm", 63);

        GLOB.religious_sect.adjust_favor(favor, user);
        if (give_heart != 0)
        {
            for (float i = 1; i <= give_heart; i++)
                Dm.@new<obj_item_organ_heart_evolved_sacred_dreamer>(get_turf(religious_tool));
            playsound(religious_tool, Dm.resource("sound/effects/pray.ogg"), 50, TRUE, frequency: 0.5f);
            to_chat(user, span_hypnophrase(I18n.T("[] blesses you.", GLOB.deity)));
        }
        else
        {
            to_chat(user, span_hypnophrase(I18n.T("[] smiles upon you.", GLOB.deity)));
        }
        await user.add_mood_event("banish_nightmare", typeof(datum_mood_event_banish_nightmare));
    }
}

// tg: code/modules/religion/dreams/banish_nightmare.dm:76
public partial class obj_item_organ_heart_evolved_sacred_dreamer : obj_item_organ_heart_evolved_sacred
{
    // tg: code/modules/religion/dreams/banish_nightmare.dm:83
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        healing_probability = 5;
        if (HAS_TRAIT(owner, TRAIT_DREAMING))
            healing_probability += 7.5f;
        if (IS_UNCONSCIOUS(owner))
            healing_probability += 7.5f;
        await base.on_life(seconds_per_tick);
    }

    // tg: code/modules/religion/dreams/banish_nightmare.dm:91
    public override async DmTask on_blocked()
    {
        charges -= 1;
        _addtimer(Dm.@new<datum_callback>(this, nameof(recharge)), 1 * MINUTES, file: "code/modules/religion/dreams/banish_nightmare.dm", line: 95);
        playsound(owner, Dm.resource("sound/effects/health/slowbeat.ogg"), 80);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/religion/dreams/banish_nightmare.dm:96
    public override object check_block()
    {
        return charges > 0 ? TRUE : FALSE;
    }

    // tg: code/modules/religion/dreams/banish_nightmare.dm:99
    public virtual void recharge()
    {
        charges += 1;
    }
}
