// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/crossbreeding/selfsustaining.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the self-sustaining extracts, which split into four autoslimes, extracts that run the reaction their user picks
// without any reagent. The var blocks are generated (D111).

/*
Self-sustaining extracts:
	Produces 4 extracts that do not need reagents.
*/
// tg: code/modules/research/xenobiology/crossbreeding/selfsustaining.dm:6
public partial class obj_item_slimecross_selfsustaining : obj_item_slimecross
{
    //Just divides into the actual item.
    // tg: code/modules/research/xenobiology/crossbreeding/selfsustaining.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        visible_message(span_warning(I18n.T("The [] shudders, and splits into four smaller extracts.", this)));
        for (int i = 1; i <= 4; i++)
        {
            obj_item_autoslime A = Dm.@new<obj_item_autoslime>(loc);
            obj_item_slime_extract X = Dm.@new(extract_type, A) as obj_item_slime_extract;
            A.extract = X;
            A.icon = icon;
            A.icon_state = icon_state;
            A.color = color;
            A.name = "self-sustaining " + colour + " extract";
        }
        return INITIALIZE_HINT_QDEL;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/selfsustaining.dm:12
public partial class obj_item_autoslime : obj_item
{
    // tg: code/modules/research/xenobiology/crossbreeding/selfsustaining.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/selfsustaining.dm:34
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        DmList slime_reactions = GLOB.slime_extract_auto_activate_reactions[extract.type] as DmList;
        if (slime_reactions == null)
            return null;

        DmList choices = Dm.list();
        foreach (datum_chemical_reaction_slime recipe in Dm.each_cast<datum_chemical_reaction_slime>(slime_reactions))
        {
            DmList req_chem_names = Dm.list();
            foreach (Type chem in Dm.each_cast<Type>(recipe.required_reagents))
                Dm.append(ref req_chem_names, Dm.initial(chem, (datum_reagent r) => r.name));
            object choice = english_list(req_chem_names, and_text: ", ");
            choices[choice] = recipe;
        }

        object selectName = await tgui_input_list(user, I18n.T("Reagent(s) the extract will produce."), I18n.T("Self-sustaining Reaction"), choices);
        if (selectName == null)
            return null;

        datum_chemical_reaction_slime recipeselect = choices[selectName] as datum_chemical_reaction_slime;
        if (recipeselect == null)
            return null;

        if (!Dm.truthy(user.is_holding(this)) || !Dm.truthy(await can_interact(user)))
            return null;

        await extract.forceMove(user.drop_location() as atom);
        qdel(this);
        await user.put_in_active_hand(extract);

        await extract.auto_activate_reaction(recipeselect);
        return null;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/selfsustaining.dm:64
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(effect_desc))
            Dm.append(ref dot, span_notice(I18n.T(effect_desc)));
        return dot;
    }
}
