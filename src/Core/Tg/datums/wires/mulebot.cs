// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/mulebot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

// tg: code/datums/wires/mulebot.dm:5
public partial class datum_wires_mulebot : datum_wires
{
    private const float FAST_MOTOR_SPEED = 1;
    private const float AVERAGE_MOTOR_SPEED = 2;
    private const float SLOW_MOTOR_SPEED = 3;

    // tg: code/datums/wires/mulebot.dm:10
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        wires = Dm.list(
            WIRE_POWER1, WIRE_POWER2,
            WIRE_AVOIDANCE, WIRE_LOADCHECK,
            WIRE_MOTOR1, WIRE_MOTOR2,
            WIRE_RX, WIRE_TX, WIRE_BEACON);
        await base.New(__extra);
    }

    // tg: code/datums/wires/mulebot.dm:18
    public override bool interactable(mob user)
    {
        if (!base.interactable(user))
            return false;
        mob_living_basic_bot_mulebot mule = (mob_living_basic_bot_mulebot)holder;
        return Dm.truthy(Dm.bitand(mule.bot_access_flags, BOT_COVER_MAINTS_OPEN));
    }

    // tg: code/datums/wires/mulebot.dm:25
    public override async DmTask on_cut(object wire = null, float? mend = null, mob_living source = null)
    {
        mob_living_basic_bot_mulebot mule = (mob_living_basic_bot_mulebot)holder;
        switch (wire as string)
        {
            case WIRE_MOTOR1: case WIRE_MOTOR2:
                if (Dm.truthy(is_cut(WIRE_MOTOR1)) && Dm.truthy(is_cut(WIRE_MOTOR2)))
                {
                    ADD_TRAIT(mule, TRAIT_IMMOBILIZED, MOTOR_LACK_TRAIT);
                    holder.audible_message(span_hear(Dm.text("The motors of [] go silent.", mule)), null, 1);
                }
                else if (HAS_TRAIT_FROM(mule, TRAIT_IMMOBILIZED, MOTOR_LACK_TRAIT))
                {
                    REMOVE_TRAIT(mule, TRAIT_IMMOBILIZED, MOTOR_LACK_TRAIT);
                    holder.audible_message(span_hear(Dm.text("The motors of [] whir to life!", mule)), null, 1);
                }

                if (Dm.truthy(is_cut(WIRE_MOTOR1)))
                {
                    await mule.set_varspeed(FAST_MOTOR_SPEED);
                    holder.audible_message(span_hear(Dm.text("The motors of [] speed up!", mule)), null, 1);
                }
                else if (Dm.truthy(is_cut(WIRE_MOTOR2)))
                {
                    await mule.set_varspeed(AVERAGE_MOTOR_SPEED);
                    holder.audible_message(span_hear(Dm.text("The motors of [] whir.", mule)), null, 1);
                }
                else
                {
                    await mule.set_varspeed(SLOW_MOTOR_SPEED);
                    holder.audible_message(span_hear(Dm.text("The motors of [] move gently.", mule)), null, 1);
                }
                break;
            case WIRE_AVOIDANCE:
                if (source != null)
                {
                    log_combat(source, mule, Dm.text("[] the MULE safety wire of", Dm.truthy(is_cut(WIRE_AVOIDANCE)) ? "cut" : "mended"));
                    holder.audible_message(span_hear(Dm.text("Something inside [] clicks ominously!", mule)), null, 1);
                }
                break;
        }
    }

    // tg: code/datums/wires/mulebot.dm:50
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        mob_living_basic_bot_mulebot mule = (mob_living_basic_bot_mulebot)holder;
        if (!Dm.truthy(mule.has_power()))
            return; //logically mulebots can't flash and beep if they don't have power.
        switch (wire as string)
        {
            case WIRE_POWER1: case WIRE_POWER2:
                holder.visible_message(span_notice("The charge light flickers."));
                break;
            case WIRE_AVOIDANCE:
                holder.visible_message(span_notice("The external warning lights flash briefly."));
                flick(Dm.text("[]1", mule.base_icon_state), mule);
                break;
            case WIRE_LOADCHECK:
                holder.visible_message(span_notice("The load platform clunks."));
                break;
            case WIRE_MOTOR1: case WIRE_MOTOR2:
                holder.visible_message(span_notice("The drive motor whines briefly."));
                break;
            default:
                holder.visible_message(span_notice("You hear a radio crackle."));
                break;
        }
    }
}
