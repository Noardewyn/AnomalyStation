// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/gloves/boxing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X3b (D316): the boxing gloves (boxing, the evil ones' evil boxing) and their traitor mail uppercut, whole
// with the fishing difficulty; the golden gloves' athletics skill reward came with gaps SL1 (D333). The var
// blocks are generated (D111).

// tg: code/modules/clothing/gloves/boxing.dm:1
public partial class obj_item_clothing_gloves_boxing : obj_item_clothing_gloves
{
    // tg: code/modules/clothing/gloves/boxing.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), TgState.Current.obj_item_clothing_gloves_boxing__slapcraft_recipe_list));

        Waiting.NoSleep(AddComponent(typeof(datum_component_martial_art_giver), style_to_give));
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), 19));
        RegisterSignal(this, COMSIG_ITEM_IN_UNWRAPPED_TRAITOR_MAIL, nameof(on_mail_unwrap));
        return dot;
    }

    // tg: code/modules/clothing/gloves/boxing.dm:28
    [SignalHandler]
    public virtual object on_mail_unwrap(atom source = null, mob user = null, obj letter = null)
    {
        to_chat(user, span_danger(I18n.T("As you open [], boxing gloves spring out and deliver you a swift uppercut!", letter)));
        var userasliving = user as mob_living;
        playsound(user, SFX_PUNCH, 25, TRUE);
        Waiting.NoSleep(userasliving.Knockdown((Dm.truthy(extrapower) ? 2 : 4) * SECONDS, (Dm.truthy(extrapower) ? 4 : 6) * SECONDS));
        Waiting.NoSleep(userasliving.adjust_stamina_loss(Dm.truthy(extrapower) ? 40 : 80));
        Waiting.NoSleep(forceMove(user.loc as atom));
        return COMPONENT_TRAITOR_MAIL_HANDLED;
    }
}

// tg: code/modules/clothing/gloves/boxing.dm:57
public partial class obj_item_clothing_gloves_boxing_golden : obj_item_clothing_gloves_boxing
{
    // tg: code/modules/clothing/gloves/boxing.dm:67
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_skill_reward), typeof(datum_skill_athletics)));
        return dot;
    }
}
