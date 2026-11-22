// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/chat.dm
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

// The SSchat shim of docs/ai/specs/presentation-seam.md section 3 (rules 12): SSchat keeps tg's identity (name, flags, wait,
// priority, init_stage) and fires as a ticker like tg's, but its per-client payload machinery (the tgui chat panel) is
// not translated in M0: queue and send_immediate are replaced at proc level and hand the target and the message to the
// world's message sink at once (IMessageSink.Chat), in call order. So client_to_payloads stays empty and fire has
// nothing to send. Not declared: generate_payload, send_payload_to_client and handle_resend (:22-98, the payloads,
// their reliability history and resends over the tgui panel; with the chat panel, M9).

// tg: code/controllers/subsystem/chat.dm:6 SUBSYSTEM_DEF(chat)
[DmPath("/datum/controller/subsystem/chat")]
public partial class datum_controller_subsystem_chat : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/chat.dm:7
    public datum_controller_subsystem_chat()
    {
        name = "Chat";
        ss_flags = Dm.bitor(SS_TICKER, SS_NO_INIT);
        wait = 1;
        priority = FIRE_PRIORITY_CHAT;
        init_stage = INITSTAGE_LAST;
    }

    /// Assosciates a ckey with a list of messages to send to them.
    // tg: code/controllers/subsystem/chat.dm:14
    public DmList client_to_payloads;

    /// Associates a ckey with an assosciative list of their last CHAT_RELIABILITY_HISTORY_SIZE messages.
    // tg: code/controllers/subsystem/chat.dm:17
    public DmList client_to_reliability_history;

    /// Assosciates a ckey with their next sequence number.
    // tg: code/controllers/subsystem/chat.dm:20
    public DmList client_to_sequence_number;

    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        client_to_payloads = Dm.list();
        client_to_reliability_history = Dm.list();
        client_to_sequence_number = Dm.list();
    }

    // The New() that SUBSYSTEM_DEF(chat) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSchat) (__DEFINES/MC.dm:29)
    // expanded inline (it reads src).
    // tg: code/controllers/subsystem/chat.dm:6
    public override async DmTask New(params object[] __extra)
    {
        if (SSchat != this)
        {
            if (SSchat is datum_controller_subsystem_chat)
            {
                Recover();
                qdel(SSchat);
            }
            SSchat = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/chat.dm:48
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        // The shim's queue never fills client_to_payloads (it sends at once), so this loop finds nothing to send.
        foreach (object ckey in Dm.each(client_to_payloads))
        {
            Dm.NotTranslated("code/controllers/subsystem/chat.dm:50", "fire: the per-client payload delivery over the tgui chat panel (GLOB.directory, send_payload_to_client) comes with the chat panel (M10+)");
            return;
        }
    }

    // tg: code/controllers/subsystem/chat.dm:62
    public virtual void queue(object queue_target = null, DmList message_data = null)
    {
        // Seam (docs/ai/specs/presentation-seam.md section 3): tg turns each target into its client (CLIENT_FROM_VAR) and
        // queues a payload per client for fire; the port hands the target and the message to the message sink at once.
        if (!PresentationSinks.Reportable(queue_target))
            return;
        DmWorldState.Current.Sinks.Messages.Chat(queue_target, message_data);
    }

    // tg: code/controllers/subsystem/chat.dm:70
    public virtual void send_immediate(object send_target = null, DmList message_data = null)
    {
        // Seam (docs/ai/specs/presentation-seam.md section 3): tg sends a payload to each target's client at once
        // (CLIENT_FROM_VAR, generate_payload, send_payload_to_client); the port hands the target and the message to the
        // message sink.
        if (!PresentationSinks.Reportable(send_target))
            return;
        DmWorldState.Current.Sinks.Messages.Chat(send_target, message_data);
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/chat.dm:6  SUBSYSTEM_DEF(chat): GLOBAL_REAL(SSchat, /datum/controller/subsystem/chat)
    internal datum_controller_subsystem_chat SSchat;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_chat SSchat => ref TgState.Current.SSchat;
}
