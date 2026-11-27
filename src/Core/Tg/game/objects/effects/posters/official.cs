// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/posters/official.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The official posters are data (their var blocks are generated); the procs of the file are the "Plasma and the Body" poster's closer
// look at its text, and the "No ERP" poster's: once in a hundred rips, or on April Fools' Day, it peels off to show "Yes ERP" beneath
// it (and back again).

// tg: code/game/objects/effects/posters/official.dm:254
public partial class obj_structure_sign_poster_official_no_erp : obj_structure_sign_poster_official
{
    // tg: code/game/objects/effects/posters/official.dm:261
    public override async DmTask tear_poster(mob user = null)
    {
        if (prob(99) && !Dm.truthy(check_holidays(APRIL_FOOLS)))
        {
            await base.tear_poster(user);
            return;
        }

        visible_message(span_notice(I18n.T("[] rips [] in a single, decisive motion... revealing another poster?", user, this)));
        playsound(this, Dm.resource("sound/items/poster/poster_ripped.ogg"), 100, TRUE);
        if (Dm.truthy(corrupted))
        {
            name = Dm.initial(type, (obj_structure_sign_poster_official_no_erp p) => p.name);
            desc = Dm.initial(type, (obj_structure_sign_poster_official_no_erp p) => p.desc);
            icon_state = Dm.initial(type, (obj_structure_sign_poster_official_no_erp p) => p.icon_state);
            corrupted = FALSE;
        }
        else
        {
            name = "Yes ERP";
            desc = "This poster reminds the crew that Enterprise Resource Planning is both effective and critical for the station's operations.";
            icon_state = "yes_erp";
            corrupted = TRUE;
        }
    }
}

// tg: code/game/objects/effects/posters/official.dm:309
public partial class obj_structure_sign_poster_official_plasma_effects : obj_structure_sign_poster_official
{
    // tg: code/game/objects/effects/posters/official.dm:314
    public override async DmTask<object> examine_more(mob user = null)
    {
        DmList dot = await base.examine_more(user) as DmList ?? Dm.list();
        Dm.append(ref dot, span_notice(I18n.T("<i>You browse some of the poster's information...</i>")));
        Dm.append(ref dot, "\t" + span_info(I18n.T("Plasma (scientific name Amenthium) is classified by TerraGov as a Grade 1 Health Hazard, and has significant risks to health associated with chronic exposure.")));
        Dm.append(ref dot, "\t" + span_info(I18n.T("Plasma is known to cross the blood/brain barrier and bioaccumulate in brain tissue, where it begins to result in degradation of brain function. The mechanism for attack is not yet fully known, and as such no concrete preventative advice is available barring proper use of PPE (gloves + protective jumpsuit + respirator).")));
        Dm.append(ref dot, "\t" + span_info(I18n.T("In small doses, plasma induces confusion, short-term amnesia, and heightened aggression. These effects persist with continual exposure.")));
        Dm.append(ref dot, "\t" + span_info(I18n.T("In individuals with chronic exposure, severe effects have been noted. Further heightened aggression, long-term amnesia, Alzheimer's symptoms, schizophrenia, macular degeneration, aneurysms, heightened risk of stroke, and Parkinsons symptoms have all been noted.")));
        Dm.append(ref dot, "\t" + span_info(I18n.T("It is recommended that all individuals in unprotected contact with raw plasma regularly check with company health officials.")));
        Dm.append(ref dot, "\t" + span_info(I18n.T("For more information, please check with TerraGov's extranet site on Amenthium: www.terra.gov/health_and_safety/amenthium/, or our internal risk-assessment documents (document numbers #47582-b (Plasma safety data sheets) and #64210 through #64225 (PPE regulations for working with Plasma), available via NanoDoc to all employees).")));
        Dm.append(ref dot, "\t" + span_info(I18n.T("Nanotrasen: Always looking after your health.")));
        return dot;
    }
}
