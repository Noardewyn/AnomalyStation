// Translated from tgstation (snapshot 1d99bd7f): code/datums/dog_fashion.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

// tg: code/datums/dog_fashion.dm:3
public partial class datum_dog_fashion : datum
{
    // tg: code/datums/dog_fashion.dm:26
    [DmParams("fashionable_mob")]
    public override async DmTask New(params object[] __extra)
    {
        mob fashionable_mob = Dm.arg<mob>(__extra, 0);
        await base.New(__extra);
        //replace the placeholder for the real unmodified name in the name of the dog
        name = replacetext(name, "%REAL_NAME%", fashionable_mob.real_name);
        //replace the placeholder for the capitalized real unmodified name in the name of the dog
        name = replacetext(name, "%CAPITAL_REAL_NAME%", capitalize(fashionable_mob.real_name));
        //replace the placeholder for the current full name, which includes our name modifiers
        desc = replacetext(desc, "%NAME%", name);
        //replace the placeholder for the real unmodified name in the description of the dog
        desc = replacetext(desc, "%REAL_NAME%", fashionable_mob.real_name);
        //replace the placeholder for the capitalized real unmodified name in the description of the dog
        desc = replacetext(desc, "%CAPITAL_REAL_NAME%", capitalize(fashionable_mob.real_name));
    }

    ///Applies the name, description and speak emote modifiers to the dog
    // tg: code/datums/dog_fashion.dm:39
    public virtual void apply(mob_living_basic_pet_dog dressup_doggy = null)
    {
        if (Dm.truthy(name))
            dressup_doggy.name = name as string;
        if (Dm.truthy(desc))
            dressup_doggy.desc = desc as string;
        if (Dm.truthy(LAZYLEN(speak_emote)))
            dressup_doggy.speak_emote = string_list(speak_emote) as DmList;
    }

    ///Applies random speech modifiers to the dog
    // tg: code/datums/dog_fashion.dm:48
    public virtual void apply_to_speech(DmList speech_data = null)
    {
        if (Dm.truthy(LAZYLEN(emote_see)))
            speech_data[BB_EMOTE_SEE] = string_list(emote_see);
        if (Dm.truthy(LAZYLEN(emote_hear)))
            speech_data[BB_EMOTE_HEAR] = string_list(emote_hear);
        if (Dm.truthy(LAZYLEN(speak)))
            speech_data[BB_EMOTE_SAY] = string_list(speak);
    }

    /**
     * Generates the icon overlay for the equipped item
     * dir: passed direction for the sprite, e.g. to apply to a dead dog, we use the EAST dir and just flip it 180.
     */
    // tg: code/datums/dog_fashion.dm:60
    public virtual image get_overlay(object dir = null)
    {
        if (!Dm.truthy(icon_file) || !Dm.truthy(obj_icon_state))
            return null;
        image corgI = Dm.image(icon_file, icon_state: obj_icon_state, dir: dir);
        corgI.alpha = Dm.num(obj_alpha);
        corgI.color = obj_color;
        return corgI;
    }
}

// tg: code/datums/dog_fashion.dm:125
public partial class datum_dog_fashion_head_pirate : datum_dog_fashion_head
{
    // tg: code/datums/dog_fashion.dm:131
    [DmParams("M")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        name = Dm.text("[] []", pick("Ol'", "Scurvy", "Black", "Rum", "Gammy", "Bloody", "Gangrene", "Death", "Long-John"), pick("kibble", "leg", "beard", "tooth", "poop-deck", "Threepwood", "Le Chuck", "corsair", "Silver", "Crusoe"));
    }
}

// tg: code/datums/dog_fashion.dm:136
public partial class datum_dog_fashion_head_ushanka : datum_dog_fashion_head
{
    // tg: code/datums/dog_fashion.dm:140
    [DmParams("M")]
    public override async DmTask New(params object[] __extra)
    {
        name = Dm.text("[] %REAL_NAME%", pick("Comrade", "Commissar", "Glorious Leader"));
        await base.New(__extra);
    }
}
