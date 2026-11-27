// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/gloves/special.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// J1 (D298): the captain's gloves' and the nitrile gloves' Initialize (with its fishing difficulty element) and the
// coroner's gloves' add_blood_DNA (they never take blood). Gaps CL1 (D333): the cargo gauntlets (the wearer's strong pull,
// datums/components/strong_pull.cs) and the race gloves; the athletic fishing gloves and their fishing rod are
// left (not-translated.md 209). The var blocks are generated.

// tg: code/modules/clothing/gloves/special.dm:2
public partial class obj_item_clothing_gloves_cargo_gauntlet : obj_item_clothing_gloves
{
    // tg: code/modules/clothing/gloves/special.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_ITEM_EQUIPPED, nameof(on_glove_equip));
        RegisterSignal(this, COMSIG_ITEM_POST_UNEQUIP, nameof(on_glove_unequip));
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), 19));
        return dot;
    }

    // tg: code/modules/clothing/gloves/special.dm:20
    [SignalHandler]
    public virtual void on_glove_equip(datum source = null, mob equipper = null, object slot = null)
    {
        if (!Dm.truthy(Dm.bitand(slot, ITEM_SLOT_GLOVES)))
            return;

        datum_component_strong_pull pull_component = pull_component_weakref?.resolve() as datum_component_strong_pull;
        if (pull_component != null)
        {
            stack_trace(Dm.text("Gloves already have a pull component associated with \\[[]\\] when \\[[]\\] is trying to equip them.", pull_component.parent, equipper), "code/modules/clothing/gloves/special.dm", 28);
            QDEL_NULL(ref pull_component_weakref);
        }

        to_chat(equipper, span_notice(I18n.T("You feel the gauntlets activate as soon as you fit them on, making your pulls stronger!")));

        pull_component_weakref = WEAKREF(Waiting.NoSleep(equipper.AddComponent(typeof(datum_component_strong_pull))) as datum) as datum_weakref;
    }

    // tg: code/modules/clothing/gloves/special.dm:41
    [SignalHandler]
    public virtual void on_glove_unequip(datum source = null, object force = null, atom newloc = null, object no_move = null, object invdrop = null, object silent = null)
    {
        datum_component_strong_pull pull_component = pull_component_weakref?.resolve() as datum_component_strong_pull;

        if (pull_component == null)
            return;

        to_chat(pull_component.parent, span_warning(I18n.T("You have lost the grip power of []!", this)));

        QDEL_NULL(ref pull_component_weakref);
    }
}

// tg: code/modules/clothing/gloves/special.dm:73
public partial class obj_item_clothing_gloves_race : obj_item_clothing_gloves
{
    // tg: code/modules/clothing/gloves/special.dm:80
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), -9));
        return dot;
    }
}

// tg: code/modules/clothing/gloves/special.dm:52
public partial class obj_item_clothing_gloves_rapid : obj_item_clothing_gloves
{
    // tg: code/modules/clothing/gloves/special.dm:59  (the syndicate campaign G2, D316)
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_wearertargeting_punchcooldown)));
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), -9));
        return dot;
    }
}

// tg: code/modules/clothing/gloves/special.dm:85
public partial class obj_item_clothing_gloves_captain : obj_item_clothing_gloves
{
    // tg: code/modules/clothing/gloves/special.dm:100
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), -6));
        return dot;
    }
}

// tg: code/modules/clothing/gloves/special.dm:125
public partial class obj_item_clothing_gloves_latex_nitrile : obj_item_clothing_gloves_latex
{
    // tg: code/modules/clothing/gloves/special.dm:132
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), -6));
        return dot;
    }
}

// tg: code/modules/clothing/gloves/special.dm:137
public partial class obj_item_clothing_gloves_latex_coroner : obj_item_clothing_gloves_latex
{
    // tg: code/modules/clothing/gloves/special.dm:144
    public override async DmTask<object> add_blood_DNA(DmList blood_DNA_to_add = null, DmList diseases = null)
    {
        return FALSE;
    }
}
