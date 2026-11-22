// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/shoes/bananashoes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N3, D316: the clown operatives' mk-honk combat shoes and their prototype). The var block is generated (D111).

//banana flavored chaos and horror ahead

// tg: code/modules/clothing/shoes/bananashoes.dm:3
public partial class obj_item_clothing_shoes_clown_shoes_banana_shoes : obj_item_clothing_shoes_clown_shoes
{
    // tg: code/modules/clothing/shoes/bananashoes.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (Dm.truthy(always_noslip))
            LAZYOR(ref clothing_traits, TRAIT_NO_SLIP_WATER);
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        bananium = Dm.@new<datum_material_container>(
            this,
            Dm.list(typeof(datum_material_bananium)),
            100 * SHEET_MATERIAL_AMOUNT,
            MATCONTAINER_EXAMINE | MATCONTAINER_ANY_INTENT | MATCONTAINER_SILENT,
            Dm.named("allowed_items", typeof(obj_item_stack)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_squeak), Dm.list(Dm.kv(Dm.resource("sound/items/bikehorn.ogg"), 1)), 75, Dm.named("falloff_exponent", 20)));
        RegisterSignal(this, COMSIG_SHOES_STEP_ACTION, nameof(on_step));
        return dot;
    }

    // tg: code/modules/clothing/shoes/bananashoes.dm:34
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref bananium);
        return base.Destroy(force);
    }

    // tg: code/modules/clothing/shoes/bananashoes.dm:38
    public virtual void toggle_clowning_action()
    {
        on = Dm.truthy(on) ? FALSE : TRUE;
        update_appearance();
        if (Dm.truthy(always_noslip))
            return;

        if (Dm.truthy(on))
            attach_clothing_traits(TRAIT_NO_SLIP_WATER);
        else
            detach_clothing_traits(TRAIT_NO_SLIP_WATER);
    }

    // tg: code/modules/clothing/shoes/bananashoes.dm:49
    [SignalHandler]
    public virtual void on_step(params object[] __extra)
    {
        mob wearer = loc as mob;
        if (!Dm.truthy(on) || wearer == null)
            return;

        if (Dm.truthy(Waiting.NoSleep(bananium.use_amount_mat(material_per_banana, typeof(datum_material_bananium)))))
        {
            Dm.@new(banana_type, get_step(this, REVERSE_DIR(wearer.dir))); //honk
            return;
        }

        toggle_clowning_action();
        to_chat(wearer, span_warning(I18n.T("You ran out of bananium!")));
    }

    // tg: code/modules/clothing/shoes/bananashoes.dm:63
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        float sheet_amount = await bananium.retrieve_all();
        if (Dm.truthy(sheet_amount))
            to_chat(user, span_notice(I18n.T("You retrieve [] sheets of bananium from the prototype shoes.", sheet_amount)));
        else
            to_chat(user, span_warning(I18n.T("You cannot retrieve any bananium from the prototype shoes!")));
        return null;
    }

    // tg: code/modules/clothing/shoes/bananashoes.dm:70
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(dot, span_notice(I18n.T("The shoes are [].", Dm.truthy(on) ? I18n.T("enabled") : I18n.T("disabled"))));
        return dot;
    }

    // tg: code/modules/clothing/shoes/bananashoes.dm:74
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        if (await bananium.get_material_amount(typeof(datum_material_bananium)) >= material_per_banana)
        {
            toggle_clowning_action();
            to_chat(user, span_notice(I18n.T("You [] the prototype shoes.", Dm.truthy(on) ? I18n.T("activate") : I18n.T("deactivate"))));
        }
        else
            to_chat(user, span_warning(I18n.T("You need bananium to turn the prototype shoes on!")));
        return null;
    }

    // tg: code/modules/clothing/shoes/bananashoes.dm:81
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("clown_prototype_[]", Dm.truthy(on) ? "on" : "off");
        return await base.update_icon_state(__extra);
    }
}
