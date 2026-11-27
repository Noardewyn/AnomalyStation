// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapfluff/ruins/objects_and_mobs/sin_ruins.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: gluttony's wall, which lets only the fat (and morphs) through, and envy's knife, which steals the looks of whom it
// strikes. The var blocks are generated.

//These objects are used in the cardinal sin-themed ruins (i.e. Gluttony, Pride...)

// tg: code/modules/mapfluff/ruins/objects_and_mobs/sin_ruins.dm:4
public partial class obj_effect_gluttony : obj_effect //Gluttony's wall: Used in the Gluttony ruin. Only lets the overweight through.
{
    // tg: code/modules/mapfluff/ruins/objects_and_mobs/sin_ruins.dm:13
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null) //So bullets will fly over and stuff.
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        if (mover is mob_living_carbon_human H)
        {
            if (H.nutrition >= NUTRITION_LEVEL_FAT)
            {
                H.visible_message(span_warning(I18n.T("[] pushes through []!", H, this)), span_notice(I18n.T("You've seen and eaten worse than this.")));
                return TRUE;
            }
            else
            {
                to_chat(H, span_warning(I18n.T("You're repulsed by even looking at []. Only a pig could force themselves to go through it.", this)));
            }
        }
        if (mover is mob_living_basic_morph)
            return TRUE;
        return dot;
    }
}

//can't be bothered to do sloth right now, will make later

// tg: code/modules/mapfluff/ruins/objects_and_mobs/sin_ruins.dm:27
public partial class obj_item_knife_envy : obj_item_knife //Envy's knife: Found in the Envy ruin. Attackers take on the appearance of whoever they strike.
{
    // tg: code/modules/mapfluff/ruins/objects_and_mobs/sin_ruins.dm:41
    public override async DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (user is not mob_living_carbon_human human_user || target is not mob_living_carbon_human || QDELETED(target))
            return;

        var H = (mob_living_carbon_human)target;
        if (Dm.eq(human_user.real_name, H.dna.real_name))
            return;

        human_user.real_name = Dm.str(H.dna.real_name);
        await H.dna.copy_dna(human_user.dna, Dm.bitor(COPY_DNA_SE, COPY_DNA_SPECIES));
        await human_user.updateappearance(mutcolor_update: 1);
        await human_user.domutcheck();
        human_user.visible_message(span_warning(I18n.T("[]'s appearance shifts into []'s!", human_user, H)),
            span_bolddanger(I18n.T("[] think[] [] <i>sooo</i> much better than you. Not anymore, [] won't.", H.p_They(), H.p_s(), H.p_theyre(), H.p_they())));
    }
}
