// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/mecha_wreckage.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

///////////////////////////////////
////////  Mecha wreckage   ////////
///////////////////////////////////

// tg: code/modules/vehicles/mecha/mecha_wreckage.dm:6
public partial class obj_structure_mecha_wreckage : obj_structure
{
    // tg: code/modules/vehicles/mecha/mecha_wreckage.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload);
        if (parts != null)
        {
            for (int i = 1; i <= 2; i++)
            {
                if (parts.Count == 0)
                    break;
                if (prob(60))
                    continue;
                object part = pick(parts);
                welder_salvage.Add(part);
            }
            parts = null;
        }
        // an AI pilot's recovery beacon comes with the AI's mech control (mecha_ai_interaction.dm): the AI_pilot argument is always null until then
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mecha_wreckage.dm:45
    public override object Destroy(float? force = null)
    {
        if (ai_pilot != null)
            QDEL_NULL(ref ai_pilot);
        QDEL_LIST(crowbar_salvage);
        visible_message(span_danger(I18n.T("[]'s superstructure folds in on itself, collapsing into a heap of unsalvageable scrap!", this)));
        playsound(this, Dm.resource("sound/effects/meteorimpact.ogg"), 100, TRUE);
        foreach (mob_living witness in Dm.each<mob_living>(range(2, this)))
            shake_camera(witness, 2, 1);
        Dm.@new<obj_effect_decal_cleanable_blood_gibs_robot_debris>(get_turf(this));
        return base.Destroy(force);
    }

    // tg: code/modules/vehicles/mecha/mecha_wreckage.dm:56
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (ai_pilot == null)
            return dot;
        Dm.append(ref dot, span_notice(I18n.T("The AI recovery beacon is active.")));
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mecha_wreckage.dm:62
    public override async DmTask<object> welder_act(mob_living user = null, obj_item I = null)
    {
        await base.welder_act(user, I);
        if (salvage_num <= 0 || !Dm.truthy(length(welder_salvage)))
        {
            to_chat(user, span_notice(I18n.T("You don't see anything that can be cut with []!", I)));
            return TRUE;
        }
        if (!Dm.truthy(await I.use_tool(this, user, 0, volume: 50)))
            return TRUE;
        if (prob(30))
        {
            to_chat(user, span_notice(I18n.T("You fail to salvage anything valuable from []!", this)));
            return TRUE;
        }
        Type salvage_type = pick(welder_salvage) as Type;
        object N = Dm.@new(salvage_type, get_turf(user));
        user.visible_message(span_notice(I18n.T("[] cuts [] from [].", user, N, this)), span_notice(I18n.T("You cut [] from [].", N, this)));
        if (!isstack(N))
            Dm.remove(welder_salvage, salvage_type);
        salvage_num--;
        return TRUE;
    }

    // tg: code/modules/vehicles/mecha/mecha_wreckage.dm:79
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item I = null)
    {
        await base.wirecutter_act(user, I);
        if (Dm.truthy(wires_removed))
        {
            to_chat(user, span_notice(I18n.T("You don't see anything that can be cut with []!", I)));
            return TRUE;
        }
        object N = Dm.@new<obj_item_stack_cable_coil>(get_turf(user), rand(1, 3));
        user.visible_message(span_notice(I18n.T("[] cuts [] from [].", user, N, this)), span_notice(I18n.T("You cut [] from [].", N, this)));
        wires_removed = TRUE;
        return TRUE;
    }

    // tg: code/modules/vehicles/mecha/mecha_wreckage.dm:89
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item I = null)
    {
        await base.crowbar_act(user, I);
        if (crowbar_salvage.Count != 0)
        {
            obj S = pick(crowbar_salvage) as obj;
            await S.forceMove(user.drop_location() as atom);
            user.visible_message(span_notice(I18n.T("[] pries [] from [].", user, S, this)), span_notice(I18n.T("You pry [] from [].", S, this)));
            Dm.remove(crowbar_salvage, S);
            return TRUE;
        }
        to_chat(user, span_notice(I18n.T("You don't see anything that can be pried with []!", I)));
        return TRUE;
    }
}
