// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/sharpener.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/game/objects/items/sharpener.dm:9
public partial class obj_item_sharpener : obj_item
{
    // tg: code/game/objects/items/sharpener.dm:26
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (uses == 0)
        {
            to_chat(user, span_warning(I18n.T("The sharpening block is too worn to use again!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (tool.force >= max || tool.throwforce >= max) //So the whetstone never reduces force or throw_force
        {
            to_chat(user, span_warning(I18n.T("[] is much too powerful to sharpen further!", tool)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(requires_sharpness) && !Dm.truthy(tool.get_sharpness()))
        {
            to_chat(user, span_warning(I18n.T("You can only sharpen items that are already sharp, such as knives!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(is_type_in_list(tool, Dm.list(typeof(obj_item_melee_energy), typeof(obj_item_dualsaber))))) //You can't sharpen the photons in energy meelee weapons
        {
            to_chat(user, span_warning(I18n.T("You don't think \\the [] will be the thing getting modified if you use it on \\the []!", tool, this)));
            return ITEM_INTERACT_BLOCKING;
        }

        //This block is used to check more things if the item has a relevant component.
        object signal_out = (!Dm.truthy(tool._listen_lookup?[COMSIG_ITEM_SHARPEN_ACT]) ? (object)NONE : tool._SendSignal(COMSIG_ITEM_SHARPEN_ACT, Dm.list(tool, increment, max))); //Stores the bitflags returned by SEND_SIGNAL
        if (Dm.truthy(Dm.bitand(signal_out, COMPONENT_BLOCK_SHARPEN_MAXED))) //If the item's components enforce more limits on maximum power from sharpening,  we fail
        {
            to_chat(user, span_warning(I18n.T("[] is much too powerful to sharpen further!", tool)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(Dm.bitand(signal_out, COMPONENT_BLOCK_SHARPEN_BLOCKED)))
        {
            to_chat(user, span_warning(I18n.T("[] is not able to be sharpened right now!", tool)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(Dm.bitand(signal_out, COMPONENT_BLOCK_SHARPEN_ALREADY)) || (tool.force > Dm.num(Dm.initial(tool, "force")) && !Dm.truthy(signal_out))) //No sharpening stuff twice
        {
            to_chat(user, span_warning(I18n.T("[] has already been refined before. It cannot be sharpened further!", tool)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(Dm.bitand(signal_out, COMPONENT_BLOCK_SHARPEN_APPLIED))) //If the item has a relevant component and COMPONENT_BLOCK_SHARPEN_APPLIED is returned, the item only gets the throw force increase
        {
            tool.force = Dm.clamp(tool.force + increment, 0, max);
            tool.wound_bonus = tool.wound_bonus + increment; //wound_bonus has no cap
        }
        user.visible_message(span_notice(I18n.T("[] sharpens [] with []!", user, tool, this)), span_notice(I18n.T("You sharpen [], making it much more deadly than before.", tool)));
        playsound(this, Dm.resource("sound/items/unsheath.ogg"), 25, TRUE);
        if (!Dm.truthy(Dm.bitand(signal_out, COMPONENT_BLOCK_SHARPEN_SHARPNESS)))
            tool.sharpness = SHARP_EDGED; //When you whetstone something, it becomes an edged weapon, even if it was previously dull or pointy
        tool.throwforce = Dm.clamp(tool.throwforce + increment, 0, max);
        tool.name = Dm.text("[] []", prefix, tool.name); //This adds a prefix and a space to the item's name regardless of what the prefix is
        desc = Dm.text("[] At least, it used to.", desc);
        uses--; //this doesn't cause issues because we check if uses == 0 earlier in this proc
        if (uses == 0)
            name = Dm.text("worn out []", name); //whetstone becomes used whetstone
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/sharpener.dm:67
    public override object update_name(float? updates = null)
    {
        name = Dm.text("[][]", !Dm.truthy(uses) ? "worn out " : null, Dm.initial(this, "name"));
        return base.update_name(updates);
    }
}
