// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/bleeding.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341): the anticoagulant and hypertension. The var blocks are generated (D111).

// tg: code/datums/diseases/advance/symptoms/bleeding.dm:10
public partial class datum_symptom_bleeding : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/bleeding.dm:28
    [DmParams("A")]
    public override object Start(datum_disease_advance A = null)
    {
        object dot = null;
        dot = base.Start(A);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(A.totalStageSpeed()) >= 9)
            easybleed = TRUE;
        if (Dm.num(A.totalStealth()) >= 3)
            hidden = TRUE;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/bleeding.dm:37
    [DmParams("A")]
    public override async DmTask<object> Activate(datum_disease_advance A = null)
    {
        object dot = null;
        dot = await base.Activate(A);
        if (!Dm.truthy(dot))
            return dot;
        mob_living_carbon carbon_host = A.affected_mob;
        foreach (datum_wound possible_bleeding_wound in Dm.each_cast<datum_wound>(carbon_host.all_wounds))
        {
            if (Dm.truthy(possible_bleeding_wound.blood_flow) && !Dm.truthy(hidden))
            {
                // tg's chained comparison: (4 > stage) >= 2, kept as it is (never true)
                if (Dm.num(4 > A.stage) >= 2)
                    to_chat(carbon_host, span_warning(I18n.T("Your bleeding wounds start to itch.")));
                if (A.stage >= 4)
                    to_chat(carbon_host, span_warning(I18n.T("Your bleeding wounds itch like crazy as more blood leaves your body.")));
                return dot;
            }
        }
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/bleeding.dm:50
    public override object on_stage_change(datum_disease_advance A = null)
    {
        object dot = null;
        dot = base.on_stage_change(A);
        if (!Dm.truthy(dot))
            return dot;
        mob_living_carbon carbon_host = A.affected_mob;
        if (A.stage >= 4)
        {
            ADD_TRAIT(carbon_host, TRAIT_BLOOD_FOUNTAIN, DISEASE_TRAIT);
            if (Dm.truthy(easybleed))
                ADD_TRAIT(carbon_host, TRAIT_EASYBLEED, DISEASE_TRAIT);
            return dot;
        }
        REMOVE_TRAIT(carbon_host, TRAIT_BLOOD_FOUNTAIN, DISEASE_TRAIT);
        REMOVE_TRAIT(carbon_host, TRAIT_EASYBLEED, DISEASE_TRAIT);
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/bleeding.dm:64
    public override async DmTask<object> End(datum_disease_advance A = null)
    {
        object dot = null;
        dot = await base.End(A);
        if (!Dm.truthy(dot))
            return dot;
        mob_living_carbon carbon_host = A.affected_mob;
        REMOVE_TRAIT(carbon_host, TRAIT_BLOOD_FOUNTAIN, DISEASE_TRAIT);
        REMOVE_TRAIT(carbon_host, TRAIT_EASYBLEED, DISEASE_TRAIT);
        return dot;
    }
}

// tg: code/datums/diseases/advance/symptoms/bleeding.dm:72
public partial class datum_symptom_hypertension : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/bleeding.dm:89
    public override async DmTask New(params object[] __extra)
    {
        DmList list_of_names = Dm.list();
        foreach (Type poison in Dm.each_cast<Type>(chems))
            Dm.append(list_of_names, Dm.initial(poison, (datum_reagent p) => p.name));
        threshold_descs = Dm.list(
            Dm.kv("Resistance 7", Dm.text("Additionally synthesizes [] inside the host.", english_list(list_of_names))),
            Dm.kv("Stage Speed 9", "Increases the frequency of inflicted wounds."),
            Dm.kv("Transmission 8", "Randomly increases the severity of inflicted wounds and quantity of chems synthesized.")
        );
        await DmTask.CompletedTask;
    }

    // tg: code/datums/diseases/advance/symptoms/bleeding.dm:99
    [DmParams("our_disease")]
    public override object Start(datum_disease_advance our_disease = null)
    {
        object dot = null;
        dot = base.Start(our_disease);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(our_disease.totalResistance()) >= 7)
            adding_chems = TRUE;
        if (Dm.num(our_disease.totalStageSpeed()) >= 9)
            symptom_delay = 25;
        if (Dm.num(our_disease.totalTransmittable()) >= 8)
            crit_slash = TRUE;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/bleeding.dm:110
    [DmParams("our_disease")]
    public override async DmTask<object> Activate(datum_disease_advance our_disease = null)
    {
        object dot = null;
        dot = await base.Activate(our_disease);
        if (!Dm.truthy(dot))
            return dot;
        mob_living_carbon carbon_host = our_disease.affected_mob;
        if (our_disease.stage <= 2)
        {
            carbon_host.ominous_nosebleed();
            return dot;
        }
        DmList bleeding_bodyparts = Dm.list();
        foreach (obj_item_bodypart random_bodypart in Dm.each_cast<obj_item_bodypart>(carbon_host.get_bodyparts()))
        {
            if (Dm.truthy(random_bodypart.can_bleed()))
                Dm.append(bleeding_bodyparts, random_bodypart);
        }
        if (bleeding_bodyparts.len == 0)
            return dot; // full borg or borg with organic stumps
        obj_item_bodypart victim_limb = (obj_item_bodypart)pick(bleeding_bodyparts);
        object wound_severity = WOUND_SEVERITY_MODERATE;
        string warning = I18n.T("Your [] tears itself open!", victim_limb.name);
        if (our_disease.stage >= 4)
        {
            wound_severity = WOUND_SEVERITY_SEVERE;
            if (Dm.truthy(adding_chems))
                await add_reagents(carbon_host);
            if (Dm.truthy(crit_slash))
            {
                float chance = 33;
                if (our_disease.stage == 5)
                    chance = 66;
                if (prob(chance))
                {
                    wound_severity = WOUND_SEVERITY_CRITICAL;
                    warning = I18n.T("The blood vessels in your [] suddenly rip open!", victim_limb.name);
                    if (Dm.truthy(adding_chems))
                        await add_reagents(carbon_host); // Crit slashes add twice as many chems
                }
            }
        }
        if (Dm.truthy(await carbon_host.cause_wound_of_type_and_severity(WOUND_SLASH, victim_limb, wound_severity)))
        {
            to_chat(carbon_host, span_userdanger(warning));
            if (Dm.eq(wound_severity, WOUND_SEVERITY_CRITICAL))
                await carbon_host.emote("scream");
        }
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/bleeding.dm:145
    public virtual async DmTask add_reagents(mob_living_carbon carbon_host = null)
    {
        foreach (Type reagent_to_add in Dm.each_cast<Type>(chems))
            await ((datum_reagents)carbon_host.reagents).add_reagent(reagent_to_add, 3);
    }
}
