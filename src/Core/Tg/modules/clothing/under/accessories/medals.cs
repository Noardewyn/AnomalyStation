// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/under/accessories/medals.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// J1 (D298): the medal whole but the round-end report: pinning one on oneself (try_attach) or, with the pinnable component, on someone else
// with a reason (provide_reason, the commendation: the memory and the inscription). The admin message and log, GLOB.commendations (the
// round-end report) are deferred leaves; the captain's medal's stealing objective is a live-partial row. The plasma medal and the other
// subtypes are translated with their mechanics. The var blocks are generated.

// tg: code/modules/clothing/under/accessories/medals.dm:1
public partial class obj_item_clothing_accessory_medal : obj_item_clothing_accessory
{
    // tg: code/modules/clothing/under/accessories/medals.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_pinnable_accessory), Dm.named("on_pre_pin", Dm.@new<datum_callback>(this, nameof(provide_reason)))));
        return dot;
    }

    /// Input a reason for the medal for the round end screen
    // tg: code/modules/clothing/under/accessories/medals.dm:21
    public virtual async DmTask<object> provide_reason(mob_living_carbon_human distinguished = null, mob user = null)
    {
        commendation_message = await tgui_input_text(user, I18n.T("Reason for this commendation? It will be recorded by Nanotrasen."), I18n.T("Commendation"), max_length: 140);
        return Dm.truthy(commendation_message) ? TRUE : FALSE;
    }

    // tg: code/modules/clothing/under/accessories/medals.dm:25
    public override async DmTask<object> try_attach(obj_item_clothing_under attach_to = null, mob_living attacher = null)
    {
        mob_living distinguished = attach_to.loc as mob_living;
        if (attacher == null || !(distinguished is mob_living) || distinguished == attacher || Dm.truthy(awarded_to))
        {
            // You can't be awarded by nothing, you can't award yourself, and you can't be awarded someone else's medal
            return await base.try_attach(attach_to, attacher);
        }

        awarder = attacher.real_name;
        awarded_to = distinguished.real_name;

        update_appearance(UPDATE_DESC);
        add_memory_in_range(distinguished, 7, typeof(datum_memory_received_medal), Dm.named("protagonist", distinguished), Dm.named("deuteragonist", attacher), Dm.named("medal_type", this), Dm.named("medal_text", commendation_message));
        distinguished.log_message(Dm.text("was given the following commendation by <b>[]</b>: []", key_name(attacher), commendation_message), LOG_GAME, color: "green");
        message_admins(Dm.text("<b>[]</b> was given the following commendation by <b>[]</b>: []", key_name_admin(distinguished), key_name_admin(attacher), commendation_message));
        Dm.append(ref GLOB.commendations, Dm.text("[] awarded <b>[]</b> the <span class='medaltext'>[]</span>! \n- []", awarder, awarded_to, name, commendation_message));
        SSblackbox.record_feedback("associative", "commendation", 1, Dm.list(Dm.kv("commender", Dm.text("[]", awarder)), Dm.kv("commendee", Dm.text("[]", awarded_to)), Dm.kv("medal", Dm.text("[]", this)), Dm.kv("reason", commendation_message)));

        return await base.try_attach(attach_to, attacher);
    }

    // tg: code/modules/clothing/under/accessories/medals.dm:43
    public override object update_desc(float? updates = null)
    {
        updates ??= ALL;
        object dot = base.update_desc(updates);
        if (Dm.truthy(commendation_message) && Dm.truthy(awarded_to) && Dm.truthy(awarder))
            desc += span_info(I18n.T("<br>The inscription reads: [] - Awarded to [] by []", commendation_message, awarded_to, awarder));
        return dot;
    }
}
