// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/tools/medical/cane.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps WP1 (D333): the file whole. The cane, the medical and wooden crutches (a waddle, more support) and the white cane, all
// walking aids that take part of a missing leg's slowdown away; the white cane folds out (7 force, tg's baton click, a balloon) and
// supports and guides the blind only while extended. The crutch's and the white cane's cuffable-item element waits with its status
// effect (not-translated.md section 46): Dm.Deferred as in the other cuffable items. The var blocks are generated (D111).

// tg: code/game/objects/items/tools/medical/cane.dm:1
public partial class obj_item_cane : obj_item
{
    // tg: code/game/objects/items/tools/medical/cane.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_walking_aid), limbless_slowdown_modifier, get_walking_aid_required_trait(), causes_waddling));
        return dot;
    }

    /// Determines if a trait is required to be used as a walking aid (ex. foldable canes)
    // tg: code/game/objects/items/tools/medical/cane.dm:26
    public virtual object get_walking_aid_required_trait()
    {
        return null;
    }
}

// tg: code/game/objects/items/tools/medical/cane.dm:29
public partial class obj_item_cane_crutch : obj_item_cane
{
    // tg: code/game/objects/items/tools/medical/cane.dm:47
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/game/objects/items/tools/medical/cane.dm:49", "Initialize: AddElement(/datum/element/cuffable_item): cuffing the crutch to the wrist (the element and its status effect, not-translated.md section 46)");
        return dot;
    }
}

// tg: code/game/objects/items/tools/medical/cane.dm:58
public partial class obj_item_cane_white : obj_item_cane
{
    // tg: code/game/objects/items/tools/medical/cane.dm:70
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/game/objects/items/tools/medical/cane.dm:72", "Initialize: AddElement(/datum/element/cuffable_item): cuffing the white cane to the wrist (the element and its status effect, not-translated.md section 46)");
        Waiting.NoSleep(AddComponent(typeof(datum_component_transforming),
            Dm.named("force_on", 7),
            Dm.named("hitsound_on", hitsound),
            Dm.named("w_class_on", WEIGHT_CLASS_BULKY),
            Dm.named("clumsy_check", FALSE),
            Dm.named("attack_verb_continuous_on", Dm.list("smacks", "strikes", "cracks", "beats")),
            Dm.named("attack_verb_simple_on", Dm.list("smack", "strike", "crack", "beat"))));
        RegisterSignal(this, COMSIG_TRANSFORMING_ON_TRANSFORM, nameof(on_transform));
        return dot;
    }

    // White canes only provide support while extended
    // tg: code/game/objects/items/tools/medical/cane.dm:85
    public override object get_walking_aid_required_trait()
    {
        return TRAIT_TRANSFORM_ACTIVE;
    }

    /*
     * Signal proc for [COMSIG_TRANSFORMING_ON_TRANSFORM].
     *
     * Gives feedback to the user and makes it show up inhand.
     */
    // tg: code/game/objects/items/tools/medical/cane.dm:93
    [SignalHandler]
    public virtual object on_transform(obj_item source = null, mob_living user = null, object active = null)
    {
        if (user != null)
            balloon_alert(user, Dm.truthy(active) ? I18n.T("extended") : I18n.T("collapsed"));

        if (!HAS_TRAIT(this, TRAIT_BLIND_TOOL))
            ADD_TRAIT(this, TRAIT_BLIND_TOOL, INNATE_TRAIT);
        else
            REMOVE_TRAIT(this, TRAIT_BLIND_TOOL, INNATE_TRAIT);

        playsound(this, Dm.resource("sound/items/weapons/batonextend.ogg"), 50, TRUE);
        return COMPONENT_NO_DEFAULT_MESSAGE;
    }
}
