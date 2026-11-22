// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/vehicle_key.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H4 (D333, the janitor): the whole file: the vehicle keys, the security key's and the janitor key's suicides (the janitor's by
// its cleaning skill, gaps SL1). The var blocks and the janitor key's embedding are generated (D111).

// tg: code/modules/vehicles/vehicle_key.dm:16
public partial class obj_item_key_security : obj_item_key
{
    // tg: code/modules/vehicles/vehicle_key.dm:16
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (!Dm.truthy(await user.emote("spin"))) //In the off chance that someone attempts this suicide while under the effects of mime's bane they deserve the silliness.
        {
            user.visible_message(span_suicide(I18n.T("[] is putting \\the [] in [] ear and starts [] motor! It looks like [] trying to commit suicide... But [] sputters and stalls out! ",
                user, this, user.p_their(), user.p_their(), user.p_theyre(), user.p_they())));
            playsound(this, Dm.resource("sound/misc/sadtrombone.ogg"), 50, TRUE, -1);
            return SHAME;
        }
        user.visible_message(span_suicide(I18n.T("[] is putting \\the [] in [] ear and starts [] motor! It looks like [] trying to commit suicide!",
            user, this, user.p_their(), user.p_their(), user.p_theyre())));
        await user.say("Vroom vroom!!", forced: "secway key suicide"); //Not doing a shamestate here, because even if they fail to speak they're spinning.
        _addtimer(Dm.@new<datum_callback>(user, nameof(mob_living.gib)), 2 * SECONDS, file: "code/modules/vehicles/vehicle_key.dm", line: 28);
        return MANUAL_SUICIDE;
    }
}

// tg: code/modules/vehicles/vehicle_key.dm:31
public partial class obj_item_key_janitor : obj_item_key
{
    // tg: code/modules/vehicles/vehicle_key.dm:46
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        object sw__1 = user.mind?.get_skill_level(typeof(datum_skill_cleaning));
        if (Dm.in_to(sw__1, SKILL_LEVEL_APPRENTICE, SKILL_LEVEL_JOURNEYMAN)) //At least they tried
        {
            user.visible_message(span_suicide(I18n.T("[] is putting \\the [] in [] mouth and has inefficiently become one with the janicart! It looks like [] trying to commit suicide!",
                user, this, user.p_their(), user.p_theyre())));
            await user.AddElement(typeof(datum_element_cleaning));
            _addtimer(Dm.@new<datum_callback>(this, nameof(manual_suicide), user), 5.1f * SECONDS, file: "code/modules/vehicles/vehicle_key.dm", line: 56);
            return MANUAL_SUICIDE;
        }
        else if (Dm.in_to(sw__1, SKILL_LEVEL_EXPERT, SKILL_LEVEL_MASTER)) //They are worthy enough, but can it go even further beyond?
        {
            user.visible_message(span_suicide(I18n.T("[] is putting \\the [] in [] mouth and has skillfully become one with the janicart! It looks like [] trying to commit suicide!",
                user, this, user.p_their(), user.p_theyre())));
            await user.AddElement(typeof(datum_element_cleaning));
            for (int i = 1; i <= 100; i++)
                _addtimer(Dm.@new<datum_callback>(user, nameof(atom.add_atom_colour), i % 2 != 0 ? "#a245bb" : "#7a7d82", ADMIN_COLOUR_PRIORITY), i,
                    file: "code/modules/vehicles/vehicle_key.dm", line: 63);
            _addtimer(Dm.@new<datum_callback>(this, nameof(manual_suicide), user), 101, file: "code/modules/vehicles/vehicle_key.dm", line: 64);
            return MANUAL_SUICIDE;
        }
        else if (Dm.in_to(sw__1, SKILL_LEVEL_LEGENDARY, INFINITY)) //Holy shit, look at that janny go!
        {
            user.visible_message(span_suicide(I18n.T("[] is putting \\the [] in [] mouth and has epically become one with the janicart, and they're even in overdrive mode! It looks like [] trying to commit suicide!",
                user, this, user.p_their(), user.p_theyre())));
            await user.AddElement(typeof(datum_element_cleaning));
            playsound(this, Dm.resource("sound/effects/magic/lightning_chargeup.ogg"), 50, TRUE, -1);
            await ((datum_reagents)user.reagents).add_reagent(typeof(datum_reagent_drug_methamphetamine), 10); //Gotta go fast!
            for (int i = 1; i <= 150; i++)
                _addtimer(Dm.@new<datum_callback>(user, nameof(atom.add_atom_colour), i % 2 != 0 ? "#a245bb" : "#7a7d82", ADMIN_COLOUR_PRIORITY), i,
                    file: "code/modules/vehicles/vehicle_key.dm", line: 71);
            _addtimer(Dm.@new<datum_callback>(this, nameof(manual_suicide), user), 151, file: "code/modules/vehicles/vehicle_key.dm", line: 72);
            return MANUAL_SUICIDE;
        }

        //Their mind is too weak to ascend as a janny
        user.visible_message(span_suicide(I18n.T("[] is putting \\the [] in [] mouth and is trying to become one with the janicart, but has no idea where to start! It looks like [] trying to commit suicide!",
            user, this, user.p_their(), user.p_theyre())));
        await user.gib(DROP_ALL_REMAINS);
        return MANUAL_SUICIDE;
    }
}

// tg: code/modules/vehicles/vehicle_key.dm:1
public partial class obj_item_key : obj_item
{
    // tg: code/modules/vehicles/vehicle_key.dm:75
    public virtual async DmTask manual_suicide(mob_living user = null)
    {
        if (user != null)
        {
            await user.remove_atom_colour(ADMIN_COLOUR_PRIORITY);
            user.visible_message(span_suicide(I18n.T("[] forgot [] []n't actually a janicart! That's a paddlin'!", user, user.p_they(), user.p_are())));
            if (Dm.ge(user.mind?.get_skill_level(typeof(datum_skill_cleaning)), SKILL_LEVEL_LEGENDARY)) //Janny janny janny janny janny
                playsound(this, Dm.resource("sound/effects/adminhelp.ogg"), 50, TRUE, -1);
            await user.adjust_oxy_loss(200);
            await user.death(FALSE);
        }
    }
}
