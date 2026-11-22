// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/reagent_containers/blood_pack.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice C (D192): the whole file (the blood packs filled with their type's blood, named after it, relabelled with a
// pen; the random pack). The var blocks are generated.

// tg: code/modules/reagents/reagent_containers/blood_pack.dm:1
public partial class obj_item_reagent_containers_blood : obj_item_reagent_containers
{
    // tg: code/modules/reagents/reagent_containers/blood_pack.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (blood_type == null)
            return dot;

        datum_blood_type bloodtype = get_blood_type(blood_type) as datum_blood_type;
        DmList blood_data = Dm.bitor(bloodtype.get_default_blood_data(), Dm.list(Dm.kv(BLOOD_DATA_SYNTH_CONTENT, 1))) as DmList;
        Waiting.NoSleep(((datum_reagents)reagents).add_reagent(bloodtype.reagent_type, volume, blood_data, creation_callback: Dm.@new<datum_callback>(this, nameof(on_blood_created))));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/blood_pack.dm:22
    public virtual async DmTask on_blood_created(datum_reagent new_blood = null)
    {
        await new_blood.AddElement(typeof(datum_element_blood_reagent), null, get_blood_type(blood_type));
        update_appearance();
    }

    // tg: code/modules/reagents/reagent_containers/blood_pack.dm:26
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        if (!Dm.truthy(labeled))
            name = I18n.T("blood pack[]", blood_type != null ? Dm.text(" - []", Dm.initial(blood_type, (datum_blood_type p) => p.name)) : "");
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/blood_pack.dm:88
    public override async DmTask<object> nameformat(object input = null, mob_living user = null)
    {
        playsound(this, SFX_WRITING_PEN, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE, SOUND_FALLOFF_EXPONENT + 3, ignore_walls: FALSE);
        labeled = TRUE;
        return Dm.text("blood pack[]", Dm.truthy(input) ? Dm.text(" - []", input) : null);
    }
}

// tg: code/modules/reagents/reagent_containers/blood_pack.dm:31
public partial class obj_item_reagent_containers_blood_random : obj_item_reagent_containers_blood
{
    // tg: code/modules/reagents/reagent_containers/blood_pack.dm:34
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        icon_state = "bloodpack";
        blood_type = pick(get_roundstart_blood_types()) as Type;
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/reagents/reagent_containers/blood_pack.dm:63
public partial class obj_item_reagent_containers_blood_snail : obj_item_reagent_containers_blood
{
    // tg: code/modules/reagents/reagent_containers/blood_pack.dm:66
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("It's a bit slimy... The label indicates that this is meant for snails.")));
        return dot;
    }
}

// tg: code/modules/reagents/reagent_containers/blood_pack.dm:70
public partial class obj_item_reagent_containers_blood_podperson : obj_item_reagent_containers_blood
{
    // tg: code/modules/reagents/reagent_containers/blood_pack.dm:73
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("This appears to be some very overpriced water.")));
        return dot;
    }
}

// for slimepeople
// tg: code/modules/reagents/reagent_containers/blood_pack.dm:78
public partial class obj_item_reagent_containers_blood_toxin : obj_item_reagent_containers_blood
{
    // tg: code/modules/reagents/reagent_containers/blood_pack.dm:81
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("There is a toxin warning on the label. This is for slimepeople.")));
        return dot;
    }
}
