// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/crates/large.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N2b, D316: the large crate a crowbar opens, and the clown operatives' crate of hats). The var blocks are
// generated (D111).

// For ClownOps.
// tg: code/game/objects/structures/crates_lockers/crates/large.dm:1
public partial class obj_structure_closet_crate_large : obj_structure_closet_crate
{
    // tg: code/game/objects/structures/crates_lockers/crates/large.dm:23
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NO_MISSING_ITEM_ERROR, TRAIT_GENERIC);
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/crates/large.dm:27
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        add_fingerprint(user);
        if (!Dm.truthy(await tear_manifest(user)))
            to_chat(user, span_warning(I18n.T("You need a crowbar to pry this open!")));
        return null;
    }

    // tg: code/game/objects/structures/crates_lockers/crates/large.dm:32
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode))
            return ITEM_INTERACT_SKIP_TO_ATTACK; //Stops it from opening and turning invisible when items are used on it.

        to_chat(user, span_warning(I18n.T("You need a crowbar to pry this open!")));
        return ITEM_INTERACT_BLOCKING; //Just stop. Do nothing. Don't turn into an invisible sprite. Don't open like a locker.
                                      //The large crate has no non-attack interactions other than the crowbar, anyway.
    }

    // tg: code/game/objects/structures/crates_lockers/crates/large.dm:40
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (manifest != null)
            await tear_manifest(user);
        if (!Dm.truthy(await open(user)))
            return ITEM_INTERACT_BLOCKING;
        user.visible_message(span_notice(I18n.T("[] pries \\the [] open.", user, this)),
            span_notice(I18n.T("You pry open \\the [].", this)),
            span_hear(I18n.T("You hear splitting wood.")));
        playsound(loc, "sound/items/weapons/slashmiss.ogg", 75, TRUE);

        turf dump = get_turf(this);
        for (int i = 1; i <= material_drop_amount; i++)
            Dm.@new(material_drop, this);
        foreach (atom_movable stuff in Dm.each<atom_movable>(contents)) // a snapshot (rules 6.5)
            await stuff.forceMove(dump);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/game/objects/structures/crates_lockers/crates/large.dm:58
public partial class obj_structure_closet_crate_large_hats : obj_structure_closet_crate_large
{
    // tg: code/game/objects/structures/crates_lockers/crates/large.dm:58
    public override async DmTask PopulateContents(params object[] __extra)
    {
        await base.PopulateContents(__extra);
        for (int i = 1; i <= 5; i++)
            Dm.@new<obj_effect_spawner_random_clothing_funny_hats>(this);
        if (prob(1))
        {
            DmList our_contents = Dm.list();
            foreach (obj_item_clothing_head any_hat in Dm.each<obj_item_clothing_head>(contents))
                our_contents[any_hat] = Dm.num(our_contents[any_hat]) + 1;
            if (Dm.truthy(our_contents))
            {
                var lucky_hat = pick(our_contents) as obj_item_clothing_head;
                await lucky_hat.AddComponent(typeof(datum_component_unusual_effect), Dm.named("color", "#FFEA0030"), Dm.named("include_particles", TRUE));
                lucky_hat.name = Dm.text("unusual []", name);
            }
        }
    }
}
