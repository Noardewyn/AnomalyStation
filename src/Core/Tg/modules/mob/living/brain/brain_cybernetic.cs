// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/brain/brain_cybernetic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI10 (D326): the whole file (the android's brain). The var block is generated (D111).

// tg: code/modules/mob/living/brain/brain_cybernetic.dm:1
public partial class obj_item_organ_brain_cybernetic : obj_item_organ_brain
{
    // tg: code/modules/mob/living/brain/brain_cybernetic.dm:9
    public override object brain_damage_examine()
    {
        if (Dm.truthy(suicided))
            return span_info(I18n.T("Its circuitry is smoking slightly. They must not have been able to handle the stress of it all."));
        if (brainmob != null && (Dm.truthy(decoy_override) || brainmob.client != null || Dm.truthy(brainmob.get_ghost())))
        {
            if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)))
                return span_info(I18n.T("It seems to still have a bit of energy within it, but it's rather damaged... You may be able to repair it with a <b>multitool</b>."));
            else if (damage >= BRAIN_DAMAGE_DEATH * 0.5f)
                return span_info(I18n.T("You can feel the small spark of life still left in this one, but it's got some dents. You may be able to restore it with a <b>multitool</b>."));
            else
                return span_info(I18n.T("You can feel the small spark of life still left in this one."));
        }
        return span_info(I18n.T("This one is completely devoid of life."));
    }

    // tg: code/modules/mob/living/brain/brain_cybernetic.dm:22
    public override async DmTask<object> check_for_repair(obj_item item = null, mob user = null)
    {
        if (Dm.eq(item.tool_behaviour, TOOL_MULTITOOL)) //attempt to repair the brain
        {
            if (brainmob?.health <= brainmob?.dead_threshold) //if the brain is fucked anyway, do nothing
            {
                to_chat(user, span_warning(I18n.T("[] is far too damaged, there's nothing else we can do for it!", this)));
                return TRUE;
            }

            if (Dm.truthy(DOING_INTERACTION(user, this)))
            {
                to_chat(user, span_warning(I18n.T("you're already repairing []!", this)));
                return TRUE;
            }

            user.visible_message(span_notice(I18n.T("[] slowly starts to repair [] with [].", user, this, item)), span_notice(I18n.T("You slowly start to repair [] with [].", this, item)));
            bool did_repair = false;
            while (damage > 0)
            {
                if (Dm.truthy(await item.use_tool(this, user as mob_living, 3 * SECONDS, volume: 50)))
                {
                    did_repair = true;
                    await set_organ_damage(Math.Max(0, Dm.num(damage) - 20));
                }
                else
                    break;
            }

            if (did_repair)
            {
                if (damage > 0)
                    user.visible_message(span_notice(I18n.T("[] partially repairs [] with [].", user, this, item)), span_notice(I18n.T("You partially repair [] with [].", this, item)));
                else
                    user.visible_message(span_notice(I18n.T("[] fully repairs [] with [], causing its warning light to stop flashing.", user, this, item)), span_notice(I18n.T("You fully repair [] with [], causing its warning light to stop flashing.", this, item)));
            }
            else
                to_chat(user, span_warning(I18n.T("You failed to repair [] with []!", this, item)));

            return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/mob/living/brain/brain_cybernetic.dm:52
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        switch ((int)Dm.num(severity)) // Hard cap on brain damage from EMP
        {
            case EMP_HEAVY:
                await apply_organ_damage(20, BRAIN_DAMAGE_SEVERE);
                break;
            case EMP_LIGHT:
                await apply_organ_damage(10, BRAIN_DAMAGE_MILD);
                break;
        }
        return dot;
    }
}
