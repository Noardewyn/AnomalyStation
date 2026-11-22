// Translated from tgstation (snapshot 1d99bd7f): code/modules/paperwork/paper_cutter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the screentips (add_context: a live-partial row, D264), gaps S3 (D298). The blade is a hatchet subtype: it
// lives with the hatchet (hydroponics' tools), an inert prop in the cutter until then. The var blocks are generated.

// tg: code/modules/paperwork/paper_cutter.dm:1
public partial class obj_item_papercutter : obj_item
{
    // tg: code/modules/paperwork/paper_cutter.dm:24
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        stored_blade = Dm.@new<obj_item_hatchet_cutterblade>(this);
        register_context();
        update_appearance();
        Waiting.NoSleep(AddElement(typeof(datum_element_drag_pickup)));
        return dot;
    }

    // tg: code/modules/paperwork/paper_cutter.dm:31
    public override object Destroy(float? force = null)
    {
        if (stored_paper != null)
            QDEL_NULL(ref stored_paper);
        if (stored_blade != null)
            QDEL_NULL(ref stored_blade);
        return base.Destroy(force);
    }

    // tg: code/modules/paperwork/paper_cutter.dm:65
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        if (!Dm.truthy(disassembled))
            return;

        if (stored_paper != null)
            await stored_paper.forceMove(drop_location() as atom);
        if (stored_blade != null)
            await stored_blade.forceMove(drop_location() as atom);
    }

    // tg: code/modules/paperwork/paper_cutter.dm:74
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == stored_paper)
            stored_paper = null;
        if (gone == stored_blade)
            stored_blade = null;
        return dot;
    }

    // tg: code/modules/paperwork/paper_cutter.dm:81
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (iscarbon(user) && stored_blade != null)
        {
            mob_living_carbon carbon_user = (mob_living_carbon)user;
            obj_item_bodypart user_head = carbon_user.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;
            if (user_head == null) // So no head?
            {
                user.visible_message(span_suicide(I18n.T("[] tries to behead []self with [], but [] [] already missing it! How embarassing!", user, user.p_them(), this, user.p_they(), user.p_were())));
                return SHAME;
            }
            user.visible_message(span_suicide(I18n.T("[] is beheading []self with []! It looks like [] trying to commit suicide!", user, user.p_them(), this, user.p_theyre())));
            await user_head.drop_limb();
            playsound(loc as atom, SFX_DESECRATION, 50, TRUE, -1);
            return BRUTELOSS;
        }
        // If we have no blade, just beat ourselves up
        user.visible_message(span_suicide(I18n.T("[] repeatedly bashes [] against []self! It looks like [] trying to commit suicide!", user, this, user.p_them(), user.p_theyre())));
        playsound(loc as atom, Dm.resource("sound/items/gavel.ogg"), 50, TRUE, -1);
        return BRUTELOSS;
    }

    // tg: code/modules/paperwork/paper_cutter.dm:97
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (stored_paper != null)
            Dm.append(ref dot, "paper");
        if (stored_blade != null)
            Dm.append(ref dot, "cutter_overlay");
        return dot;
    }

    // tg: code/modules/paperwork/paper_cutter.dm:104
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (stored_blade == null && !Dm.truthy(blade_secured))
        {
            balloon_alert(user, I18n.T("no blade!"));
            return ITEM_INTERACT_BLOCKING;
        }

        tool.play_tool_sound(this);
        balloon_alert(user, I18n.T("[]secured", Dm.truthy(blade_secured) ? "un" : ""));
        blade_secured = Dm.truthy(blade_secured) ? FALSE : TRUE;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/paperwork/paper_cutter.dm:114
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_paper)))
        {
            if (Dm.truthy(is_type_in_list(tool, Dm.list(typeof(obj_item_paper_holy_writ), typeof(obj_item_paper_pamphlet), typeof(obj_item_paper_paperslip)))))
            {
                balloon_alert(user, I18n.T("won't fit!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (stored_paper != null)
            {
                balloon_alert(user, I18n.T("already paper inside!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            playsound(loc as atom, SFX_PAGE_TURN, 60, TRUE);
            balloon_alert(user, I18n.T("paper inserted"));
            stored_paper = (obj_item_paper)tool;
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_hatchet_cutterblade)))
        {
            if (stored_blade != null)
            {
                balloon_alert(user, I18n.T("already a blade inside!"));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            balloon_alert(user, I18n.T("blade inserted"));
            await tool.forceMove(this);
            stored_blade = (obj_item_hatchet_cutterblade)tool;
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/paperwork/paper_cutter.dm:145
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        // can only remove one at a time; paper goes first, as its most likely what players will want to be taking out
        if (stored_paper != null)
            await user.put_in_hands(stored_paper);
        else if (stored_blade != null && !Dm.truthy(blade_secured))
            await user.put_in_hands(stored_blade);
        update_appearance();
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/paperwork/paper_cutter.dm:154
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        if (stored_blade == null)
            balloon_alert(user, I18n.T("no blade!"));
        else if (!Dm.truthy(blade_secured))
            balloon_alert(user, I18n.T("blade unsecured!"));
        else if (stored_paper == null)
            balloon_alert(user, I18n.T("nothing to cut!"));
        else
            await cut_paper(user);

        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/paperwork/paper_cutter.dm:166
    public virtual async DmTask cut_paper(mob user = null)
    {
        playsound(loc as atom, Dm.resource("sound/items/weapons/slash.ogg"), 50, TRUE);
        bool clumsy = iscarbon(user) && HAS_TRAIT(user, TRAIT_CLUMSY) && prob(cut_self_chance);
        to_chat(user, span_userdanger(I18n.T("You neatly cut [][]", stored_paper, clumsy ? I18n.T("... and your finger in the process!") : ".")));
        if (clumsy)
        {
            obj_item_bodypart finger = ((mob_living)user).get_active_hand() as obj_item_bodypart;
            if (iscarbon(user))
            {
                mob_living_carbon carbon_user = (mob_living_carbon)user;
                await carbon_user.cause_wound_of_type_and_severity(WOUND_SLASH, finger, WOUND_SEVERITY_MODERATE, wound_source: "paper cut");
            }
        }
        // tg's own order: the reference is dropped before the qdel, so the cut paper stays inside the cutter, out of reach
        stored_paper = null;
        qdel(stored_paper);
        Dm.@new<obj_item_paper_paperslip>(get_turf(this));
        Dm.@new<obj_item_paper_paperslip>(get_turf(this));
        update_appearance();
    }
}

// tg: code/modules/paperwork/paper_cutter.dm:181
public partial class obj_item_paper_paperslip : obj_item_paper
{
    // tg: code/modules/paperwork/paper_cutter.dm:189
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_cellulose), 1.5f));
    }
}

// tg: code/modules/paperwork/paper_cutter.dm:192
public partial class obj_item_paper_paperslip_fortune : obj_item_paper_paperslip
{
    // tg: code/modules/paperwork/paper_cutter.dm:195
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        default_raw_text = pick(GLOB.wisdoms);
        return base.Initialize(mapload, __extra);
    }
}

///More fancy and sturdy paper slip which is a "plastic card", used for things like spare ID safe code
// tg: code/modules/paperwork/paper_cutter.dm:200
public partial class obj_item_paper_paperslip_corporate : obj_item_paper_paperslip
{
    // tg: code/modules/paperwork/paper_cutter.dm:211
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_plastic_polymers), 1.5f));
    }
}
