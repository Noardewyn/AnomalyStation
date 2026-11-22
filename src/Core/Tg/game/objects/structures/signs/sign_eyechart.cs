// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/signs/sign_eyechart.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST5 (D333): the file whole. The eye chart (Meta's medbay): read close, it tells how well the eyes see (their damage, blur,
// nearsightedness and the distance), the colour blind miss its red and green, the blind feel its braille, the illiterate and the
// dark read nothing. The var block is generated (D111).

// tg: code/game/objects/structures/signs/sign_eyechart.dm:1
public partial class obj_structure_sign_eyechart : obj_structure_sign
{
    // tg: code/game/objects/structures/signs/sign_eyechart.dm:10
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (isobserver(user))
            return dot;

        if (!Dm.truthy(user.can_read(this, READING_CHECK_LITERACY, silent: TRUE)) || !Dm.truthy(user.has_language(typeof(datum_language_common), UNDERSTOOD_LANGUAGE)))
        {
            if (!Dm.truthy(user.is_blind()))
            {
                Dm.append(ref dot, I18n.T("<hr>You gaze at the wall of symbols, trying to make sense of them..."));
                Dm.append(ref dot, span_warning(I18n.T("...But you don't actually know what any of them mean.")));
            }
            return dot;
        }

        if (Dm.truthy(user.is_blind()))
        {
            Dm.append(ref dot, I18n.T("<hr>You feel the poster."));
            Dm.append(ref dot, span_notice(I18n.T("Yes, feels like... \"E, P, D...\" Good thing this chart has braille!")));
            return dot;
        }

        if (!Dm.truthy(user.can_read(this, READING_CHECK_LIGHT, silent: TRUE)))
        {
            Dm.append(ref dot, I18n.T("<hr>You squint at the chart."));
            Dm.append(ref dot, span_warning(I18n.T("...But it's too dark to make out anything.")));
            return dot;
        }

        bool colorblind = HAS_TRAIT(user, TRAIT_COLORBLIND);
        obj_item_organ_eyes eye = user.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        // eye null checks here are for mobs without eyes.
        // humans missing eyes will be caught by the is_blind check above.
        float eye_goodness = eye == null ? 0 : Dm.num(eye.damage);
        float little_bad = eye == null ? 20 : Dm.num(eye.low_threshold);
        float very_bad = eye == null ? 30 : Dm.num(eye.high_threshold);

        if (Dm.truthy(user.has_status_effect(typeof(datum_status_effect_eye_blur))))
            eye_goodness = Dm.max(eye_goodness, very_bad + 1);
        if (Dm.truthy(user.is_nearsighted_currently()))
            eye_goodness = Dm.max(eye_goodness, little_bad + 1);
        eye_goodness += (Dm.num(get_dist(user, this)) - 2) * 5; // add a modifier based on distance, so closer = "better", further = "worse"

        Dm.append(ref dot, I18n.T("<hr>You read through the chart, for old time's sake."));
        if (eye_goodness <= 0)
            Dm.append(ref dot, span_notice(I18n.T("\"E, F, P...\" Yep, you can read down to the [] line.", colorblind ? I18n.T("brown - wait, isn't it supposed to be red? -") : I18n.T("red"))));
        else if (eye_goodness < little_bad)
            Dm.append(ref dot, span_notice(I18n.T("\"E, F, P...\" You can make out most of the letters, but it gets a bit difficult past the [] line.", colorblind ? I18n.T("grey - wait, isn't it supposed to be green? -") : I18n.T("green"))));
        else if (eye_goodness < very_bad)
            Dm.append(ref dot, span_warning(I18n.T("\"E, F, P..?\" You can make out the big letters, but the smaller ones are a bit of a blur.")));
        else
            Dm.append(ref dot, span_warning(I18n.T("\"E, P, D..?\" You can hardly make out the big letters, let alone the smaller ones.")));
        return dot;
    }
}
