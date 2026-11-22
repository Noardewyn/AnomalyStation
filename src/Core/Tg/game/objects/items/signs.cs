// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/signs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI2 (D326): the picket signs and the cyborg's nano sign, whole (the crafting recipe's datum is generated). The var blocks are
// generated (D111).

// tg: code/game/objects/items/signs.dm:1
public partial class obj_item_picket_sign : obj_item
{
    // tg: code/game/objects/items/signs.dm:24
    public override async DmTask<object> nameformat(object input = null, mob_living user = null)
    {
        playsound(this, SFX_WRITING_PEN, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE, SOUND_FALLOFF_EXPONENT + 3, ignore_walls: FALSE);
        label = input as string;
        await AddComponent(typeof(datum_component_rename), name, Dm.text("It reads: []", input));
        return Dm.text("[] sign", input);
    }

    // tg: code/game/objects/items/signs.dm:30
    public override async DmTask rename_reset()
    {
        label = Dm.initial(this, (obj_item_picket_sign p) => p.label);
    }

    // tg: code/game/objects/items/signs.dm:33
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!(picket_sign_cooldown <= world.time)) // COOLDOWN_FINISHED
            return null;
        picket_sign_cooldown = world.time + 5 * SECONDS; // COOLDOWN_START
        if (Dm.truthy(label))
            user.manual_emote(Dm.text("waves around \\the \"[]\" sign.", label));
        else
            user.manual_emote("waves around a blank sign.");
        float direction = prob(50) ? -1 : 1;
        if (Dm.truthy(NSCOMPONENT(user.dir))) //So signs are waved horizontally relative to what way the player waving it is facing.
        {
            animate(user, Dm.named("pixel_w", 1 * direction), Dm.named("time", 0.1f * SECONDS), Dm.named("easing", SINE_EASING), Dm.named("flags", Dm.bitor(ANIMATION_RELATIVE, ANIMATION_PARALLEL)));
            animate(Dm.named("pixel_w", -2 * direction), Dm.named("time", 0.1f * SECONDS), Dm.named("easing", SINE_EASING), Dm.named("flags", ANIMATION_RELATIVE));
            animate(Dm.named("pixel_w", 2 * direction), Dm.named("time", 0.1f * SECONDS), Dm.named("easing", SINE_EASING), Dm.named("flags", ANIMATION_RELATIVE));
            animate(Dm.named("pixel_w", -2 * direction), Dm.named("time", 0.1f * SECONDS), Dm.named("easing", SINE_EASING), Dm.named("flags", ANIMATION_RELATIVE));
            animate(Dm.named("pixel_w", 1 * direction), Dm.named("time", 0.1f * SECONDS), Dm.named("easing", SINE_EASING), Dm.named("flags", ANIMATION_RELATIVE));
        }
        else
        {
            animate(user, Dm.named("pixel_z", 1 * direction), Dm.named("time", 0.1f * SECONDS), Dm.named("easing", SINE_EASING), Dm.named("flags", Dm.bitor(ANIMATION_RELATIVE, ANIMATION_PARALLEL)));
            animate(Dm.named("pixel_z", -2 * direction), Dm.named("time", 0.1f * SECONDS), Dm.named("easing", SINE_EASING), Dm.named("flags", ANIMATION_RELATIVE));
            animate(Dm.named("pixel_z", 2 * direction), Dm.named("time", 0.1f * SECONDS), Dm.named("easing", SINE_EASING), Dm.named("flags", ANIMATION_RELATIVE));
            animate(Dm.named("pixel_z", -2 * direction), Dm.named("time", 0.1f * SECONDS), Dm.named("easing", SINE_EASING), Dm.named("flags", ANIMATION_RELATIVE));
            animate(Dm.named("pixel_z", 1 * direction), Dm.named("time", 0.1f * SECONDS), Dm.named("easing", SINE_EASING), Dm.named("flags", ANIMATION_RELATIVE));
        }
        user.changeNext_move(CLICK_CD_MELEE);
        return null;
    }
}

// tg: code/game/objects/items/signs.dm:56
public partial class datum_action_item_action_nano_picket_sign : datum_action_item_action
{
    // tg: code/game/objects/items/signs.dm:59
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        if (target is not obj_item_picket_sign sign)
            return FALSE;
        object input = await tgui_input_text(owner, I18n.T("What would you like to write on the sign?"), I18n.T("Sign Label"), max_length: 30);
        if (Dm.truthy(input) && Dm.truthy(await ((mob)owner).can_perform_action(sign)))
        {
            sign.label = input as string;
            await sign.AddComponent(typeof(datum_component_rename), Dm.text("[] sign", input), Dm.text("It reads: []", input));
        }
        return TRUE;
    }
}
