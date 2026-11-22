// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/emote.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M9 slice B, docs/ai/specs/social.md): the procs of the human's emotes; the vars are generated. Gaps SP1 (D338): wag, the
// lizard's and the felinid's tail. Not translated: the emotes of the other species' parts (wing needs a moth's wings, glow, flare and
// flicker an ethereal's glow; gaps SP2): their types stay inert, so the emote list leaves their keys out (not-translated.md section 71).

// tg: code/modules/mob/living/carbon/human/emote.dm:15
public partial class datum_emote_living_carbon_human_glasses : datum_emote_living_carbon_human
{
    // tg: code/modules/mob/living/carbon/human/emote.dm:21
    public override async DmTask<object> can_run_emote(mob user = null, float? status_check = null, float? intentional = null, object @params = null)
    {
        var eyes_slot = user.get_item_by_slot(ITEM_SLOT_EYES) as obj;
        if (eyes_slot is obj_item_clothing_glasses_regular || eyes_slot is obj_item_clothing_glasses_sunglasses)
            return await base.can_run_emote(user, status_check, intentional, @params);
        return FALSE;
    }

    // tg: code/modules/mob/living/carbon/human/emote.dm:27
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        image emote_animation = image(Dm.resource("icons/mob/human/emote_visuals.dmi"), user, "glasses");
        flick_overlay_global(emote_animation, GLOB.clients, 1.6f * SECONDS);
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/human/emote.dm:60
public partial class datum_emote_living_carbon_human_screech : datum_emote_living_carbon_human
{
    // tg: code/modules/mob/living/carbon/human/emote.dm:69
    public override object get_sound(mob_living user = null)
    {
        var scream_emote = (GLOB.emote_list[Dm.initial(typeof(datum_emote_living_scream), (datum_emote_living_scream p) => p.key)] as DmList)[1] as datum_emote;
        return scream_emote.get_sound(user);
    }
}

// tg: code/modules/mob/living/carbon/human/emote.dm:238
public partial class datum_emote_living_carbon_human_blink : datum_emote_living_carbon_human
{
    // tg: code/modules/mob/living/carbon/human/emote.dm:243
    public override async DmTask<object> can_run_emote(mob user = null, float? status_check = null, float? intentional = null, object @params = null)
    {
        if (!ishuman(user) || HAS_TRAIT(user, TRAIT_PREVENT_BLINKING) || HAS_TRAIT(user, TRAIT_NO_EYELIDS))
            return FALSE;
        var eyes = user.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        if (eyes == null)
            return FALSE;
        return await base.can_run_emote(user, status_check, intentional, @params);
    }

    // tg: code/modules/mob/living/carbon/human/emote.dm:251
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        var eyes = user.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        eyes.blink();
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/human/emote.dm:256
public partial class datum_emote_living_carbon_human_blink_r : datum_emote_living_carbon_human
{
    // tg: code/modules/mob/living/carbon/human/emote.dm:261
    public override async DmTask<object> can_run_emote(mob user = null, float? status_check = null, float? intentional = null, object @params = null)
    {
        if (!ishuman(user) || HAS_TRAIT(user, TRAIT_PREVENT_BLINKING) || HAS_TRAIT(user, TRAIT_NO_EYELIDS))
            return FALSE;
        var eyes = user.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        if (eyes == null)
            return FALSE;
        return await base.can_run_emote(user, status_check, intentional, @params);
    }

    // tg: code/modules/mob/living/carbon/human/emote.dm:269
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        var eyes = user.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        for (int i = 1; i <= 3; i++)
            _addtimer(Dm.@new<datum_callback>(eyes, nameof(obj_item_organ_eyes.blink), 0.1f * SECONDS, FALSE), i * 0.2f * SECONDS, file: "code/modules/mob/living/carbon/human/emote.dm", line: 273);
        eyes.animate_eyelids(user as mob_living_carbon_human);
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/human/emote.dm:179
public partial class datum_emote_living_carbon_human_wag : datum_emote_living_carbon_human
{
    // tg: code/modules/mob/living/carbon/human/emote.dm:184
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        obj_item_organ_tail oranges_accessory = user.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL) as obj_item_organ_tail;
        //I am so sorry my son
        //We bypass helpers here cause we already have the tail
        if (Dm.truthy(Dm.bitand(oranges_accessory.wag_flags, WAG_WAGGING))) //We verified the tail exists in can_run_emote()
            await oranges_accessory.stop_wag(user as mob_living_carbon);
        else
            await oranges_accessory.start_wag(user as mob_living_carbon);
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/emote.dm:194
    public override object select_message_type(mob user = null, object msg = null, object intentional = null)
    {
        object dot = base.select_message_type(user, msg, intentional);
        obj_item_organ_tail oranges_accessory = user.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL) as obj_item_organ_tail;
        if (Dm.truthy(Dm.bitand(oranges_accessory.wag_flags, WAG_WAGGING)))
            dot = I18n.T("stops wagging");
        dot = Dm.add(dot, I18n.T(" [] tail.", user.p_their()));
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/emote.dm:201
    public override async DmTask<object> can_run_emote(mob user = null, float? status_check = null, float? intentional = null, object @params = null)
    {
        obj_item_organ_tail tail = user.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL) as obj_item_organ_tail;
        if (Dm.truthy(Dm.bitand(tail?.wag_flags, WAG_ABLE)))
            return await base.can_run_emote(user, status_check, intentional, @params);
        return FALSE;
    }
}
