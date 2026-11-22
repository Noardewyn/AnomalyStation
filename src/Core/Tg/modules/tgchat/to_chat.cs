// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgchat/to_chat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/*!
 * Copyright (c) 2020 Aleksej Komarov
 * SPDX-License-Identifier: MIT
 */

// Both procs end in SSchat.send_immediate / SSchat.queue, which the SSchat shim forwards to the world's message sink
// (docs/ai/specs/presentation-seam.md section 3; controllers/subsystem/chat.cs).
// RULE-GAP: html = "[html]" and text = "[text]" only render the caller's text again (the caller already wrapped its
// player-visible text in I18n.T, rules 5.3), so they are Dm.text, not a new I18n.T template.

public static partial class GlobalProcs
{
    /**
     * Circumvents the message queue and sends the message
     * to the recipient (target) as soon as possible.
     */
    // tg: code/modules/tgchat/to_chat.dm:10
    public static void to_chat_immediate(object target = null, object html = null, object type = null, object text = null, float? avoid_highlighting = null, float? handle_whitespace = null, float? trailing_newline = null, float? confidential = null)
    {
        avoid_highlighting ??= FALSE;
        // FIXME: These flags are now pointless and have no effect
        handle_whitespace ??= TRUE;
        trailing_newline ??= TRUE;
        confidential ??= FALSE;
        // Useful where the integer 0 is the entire message. Use case is enabling to_chat(target, some_boolean) while preventing to_chat(target, "")
        html = Dm.text("[]", html);
        text = Dm.text("[]", text);

        if (!Dm.truthy(target))
            return;
        if (!Dm.truthy(html) && !Dm.truthy(text))
            throw Dm.CRASH("Empty or null string in to_chat proc call.", "code/modules/tgchat/to_chat.dm", 28);
        if (Dm.eq(target, world))
            target = GLOB.clients;

        // Build a message
        object message = Dm.list();
        if (Dm.truthy(type))
            ((DmList)message)["type"] = type;
        if (Dm.truthy(text))
            ((DmList)message)["text"] = text;
        if (Dm.truthy(html))
            ((DmList)message)["html"] = html;
        if (Dm.truthy(avoid_highlighting))
            ((DmList)message)["avoidHighlighting"] = avoid_highlighting;

        // send it immediately
        SSchat.send_immediate(target, message as DmList);
    }

    /**
     * Sends the message to the recipient (target).
     *
     * Recommended way to write to_chat calls:
     * ```
     * to_chat(client,
     *     type = MESSAGE_TYPE_INFO,
     *     html = "You have found <strong>[object]</strong>")
     * ```
     */
    // tg: code/modules/tgchat/to_chat.dm:52
    public static void to_chat(object target = null, object html = null, object type = null, object text = null, float? avoid_highlighting = null, float? handle_whitespace = null, float? trailing_newline = null, float? confidential = null)
    {
        avoid_highlighting ??= FALSE;
        // FIXME: These flags are now pointless and have no effect
        handle_whitespace ??= TRUE;
        trailing_newline ??= TRUE;
        confidential ??= FALSE;
        if (Master == null || !Dm.truthy(SSchat?.initialized) || !MC_RUNNING(SSchat.init_stage))
        {
            to_chat_immediate(target, html, type, text, avoid_highlighting);
            return;
        }

        // Useful where the integer 0 is the entire message. Use case is enabling to_chat(target, some_boolean) while preventing to_chat(target, "")
        html = Dm.text("[]", html);
        text = Dm.text("[]", text);

        if (!Dm.truthy(target))
            return;
        if (!Dm.truthy(html) && !Dm.truthy(text))
            throw Dm.CRASH("Empty or null string in to_chat proc call.", "code/modules/tgchat/to_chat.dm", 74);
        if (Dm.eq(target, world))
            target = GLOB.clients;

        // Build a message
        object message = Dm.list();
        if (Dm.truthy(type))
            ((DmList)message)["type"] = type;
        if (Dm.truthy(text))
            ((DmList)message)["text"] = text;
        if (Dm.truthy(html))
            ((DmList)message)["html"] = html;
        if (Dm.truthy(avoid_highlighting))
            ((DmList)message)["avoidHighlighting"] = avoid_highlighting;
        SSchat.queue(target, message as DmList);
    }
}
