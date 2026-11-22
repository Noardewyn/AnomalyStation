// Translated from tgstation (snapshot 1d99bd7f): code/modules/detectivework/evidence.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J4, D298).

// tg: code/modules/detectivework/evidence.dm:4
public partial class obj_item_evidencebag : obj_item
{
    // tg: code/modules/detectivework/evidence.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(create_storage(max_slots: 1, max_specific_storage: WEIGHT_CLASS_NORMAL, rustle_sound: Dm.resource("sound/items/evidence_bag/evidence_bag_zip.ogg"), remove_rustle_sound: Dm.resource("sound/items/evidence_bag/evidence_bag_unzip.ogg")));
        atom_storage.allow_quick_gather = TRUE;
        atom_storage.collection_mode = COLLECT_ONE;
        RegisterSignal(atom_storage, COMSIG_STORAGE_STORED_ITEM, nameof(on_insert));
        RegisterSignal(atom_storage, COMSIG_STORAGE_REMOVED_ITEM, nameof(on_remove));
        return dot;
    }

    // tg: code/modules/detectivework/evidence.dm:29
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        if (!Dm.truthy(atom_storage.get_total_weight()))
        {
            desc = Dm.initial(this, (obj_item_evidencebag p) => p.desc);
            return dot;
        }
        obj_item inserted = Dm.locate_type(typeof(obj_item), atom_storage.real_location) as obj_item;
        desc = I18n.T("An evidence bag containing []. []", inserted, inserted?.desc);
        return dot;
    }

    // tg: code/modules/detectivework/evidence.dm:37
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (!Dm.truthy(atom_storage.get_total_weight()))
        {
            icon_state = "evidenceobj";
            return dot;
        }
        icon_state = "evidence";
        return dot;
    }

    // tg: code/modules/detectivework/evidence.dm:44
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (!Dm.truthy(atom_storage.get_total_weight()))
            return dot;
        obj_item inserted = Dm.locate_type(typeof(obj_item), atom_storage.real_location) as obj_item;
        mutable_appearance in_evidence = Dm.@new<mutable_appearance>(inserted);
        in_evidence.plane = FLOAT_PLANE;
        in_evidence.layer = FLOAT_LAYER;
        in_evidence.pixel_x = 0;
        in_evidence.pixel_y = 0;
        dot.Add(in_evidence);
        dot.Add("evidence");
        return dot;
    }

    // tg: code/modules/detectivework/evidence.dm:58
    [SignalHandler]
    public virtual void on_insert(datum_storage storage = null, obj_item to_insert = null, mob user = null, object force = null)
    {
        update_weight_class(to_insert.w_class);
    }

    // tg: code/modules/detectivework/evidence.dm:63
    [SignalHandler]
    public virtual void on_remove(datum_storage storage = null, obj_item to_remove = null, atom remove_to_loc = null, object silent = null)
    {
        if (!Dm.truthy(atom_storage.get_total_weight()))
            update_weight_class(WEIGHT_CLASS_TINY);
    }

    // tg: code/modules/detectivework/evidence.dm:69
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(atom_storage.get_total_weight()))
        {
            to_chat(user, span_notice(I18n.T("[] is empty.", this)));
            return null;
        }
        user.visible_message(span_notice(I18n.T("[] empties [].", user, this)), span_notice(I18n.T("You empty [].", this)), span_hear(I18n.T("You hear someone rustle around in a plastic bag, and remove something.")));
        playsound(this, Dm.resource("sound/items/evidence_bag/evidence_bag_unzip.ogg"), 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE, ignore_walls: FALSE);
        await atom_storage.remove_all();
        return null;
    }
}
