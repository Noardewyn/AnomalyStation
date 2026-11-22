using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Logging;

/// <summary>
/// to_chat and to_chat_immediate (code/modules/tgchat/to_chat.dm), message_admins (modules/admin/admin.dm:2) and the
/// SSchat shim (controllers/subsystem/chat.dm; docs/ai/specs/presentation-seam.md section 3): every message reaches the
/// world's message sink, IMessageSink.Chat, with tg's target and tg's message list, in call order.
/// </summary>
public class ChatTests
{
    [Fact]
    public void ToChatWorldAndToChatMobReachTheMessageSinkInOrder()
    {
        var t = new LoggingTestWorld();
        long mobId;
        using (t.World.Enter())
        {
            t.Setup();
            Dm.@new<datum_controller_subsystem_chat>();
            var listener = Dm.@new<mob_test_logging_listener>();
            mobId = listener.dm_id;

            // No Master yet: to_chat sends at once through to_chat_immediate (to_chat.dm:63-65).
            to_chat(world, span_notice(I18n.T("Everyone, listen.")));
            to_chat(listener, I18n.T("Just you."), type: MESSAGE_TYPE_INFO);
            to_chat(listener, html: "<b>bold</b>", text: "plain", avoid_highlighting: TRUE);
            to_chat(world, 0); // "[html]" (to_chat.dm:22): the number 0 is the whole message
        }
        Assert.Equal(new[]
        {
            // to_chat(world) goes to GLOB.clients (to_chat.dm:29-30); no client is connected.
            "chat list() html=<span class='notice'>Everyone, listen.</span>",
            "chat mob_test_logging_listener#" + mobId + " type=info html=Just you.",
            "chat mob_test_logging_listener#" + mobId + " text=plain html=<b>bold</b> avoidHighlighting=1",
            "chat list() html=0",
        }, t.Chat.Lines);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void ToChatOfNothingCrashesAndToNoTargetSendsNothing()
    {
        var t = new LoggingTestWorld();
        using (t.World.Enter())
        {
            t.Setup();
            Dm.@new<datum_controller_subsystem_chat>();
            to_chat(null, "nobody");
            var crash = Assert.Throws<DmCrashException>(() => to_chat(world, ""));
            Assert.Equal("Empty or null string in to_chat proc call.", crash.Message);
            Assert.Equal("code/modules/tgchat/to_chat.dm", crash.DmFile);
            Assert.Equal(28, crash.DmLine); // through to_chat_immediate
        }
        Assert.Empty(t.Chat.Lines);
    }

    [Fact]
    public void MessageAdminsWrapsTheMessageAndSendsItToTheAdmins()
    {
        var t = new LoggingTestWorld();
        using (t.World.Enter())
        {
            t.Setup();
            Dm.@new<datum_controller_subsystem_chat>();
            message_admins("the round is on fire");
            relay_msg_admins("from elsewhere");
            Assert.Same(GLOB.admins, t.Chat.Calls[0].Target);
        }
        Assert.Equal(new[]
        {
            "chat list() type=adminlog html=<span class=\"admin\"><span class=\"prefix\">ADMIN LOG:</span> <span class=\"message\">the round is on fire</span></span>",
            "chat list() type=adminlog html=<span class=\"admin\"><span class=\"prefix\">RELAY:</span> <span class=\"message\">from elsewhere</span></span>",
        }, t.Chat.Lines);
    }

    [Fact]
    public void TheChatShimKeepsTgsIdentityAndForwardsQueueAndSendImmediateAtOnce()
    {
        var t = new LoggingTestWorld();
        using (t.World.Enter())
        {
            t.Setup();
            var ss = Dm.@new<datum_controller_subsystem_chat>();
            Assert.Same(ss, SSchat); // SUBSYSTEM_DEF's New (NEW_SS_GLOBAL)
            Assert.Equal("/datum/controller/subsystem/chat", DmTypes.PathOf(ss.GetType()));
            Assert.Equal("Chat", ss.name);
            Assert.Equal((float)(SS_TICKER | SS_NO_INIT), ss.ss_flags); // chat.dm:8, no longer an inert stub
            Assert.Equal(1f, ss.wait);
            Assert.Equal((float)FIRE_PRIORITY_CHAT, ss.priority);
            Assert.Equal((float)INITSTAGE_LAST, ss.init_stage);
            Assert.Equal(0f, ss.dependencies?.len ?? 0f);

            var listener = Dm.@new<mob_test_logging_listener>();
            DmList message = Dm.list(Dm.kv("type", MESSAGE_TYPE_INFO), Dm.kv("html", "queued"));
            ss.queue(listener, message);
            ss.send_immediate(Dm.list(listener), Dm.list(Dm.kv("html", "now")));
            Assert.Same(message, t.Chat.Calls[0].Message);
            Assert.Same(listener, t.Chat.Calls[0].Target);
            Assert.Equal(2, t.Chat.Calls.Count);

            // The queue is never filled, so the ticker's fire has nothing to send.
            Assert.Equal(0f, ss.client_to_payloads.len);
            Assert.True(ss.fire().IsCompleted);
            Assert.Equal(2, t.Chat.Calls.Count);

            // An object without a world id is not reported (presentation-seam.md section 1, rule 4).
            ss.queue(new mob_test_logging_listener(), message);
            Assert.Equal(2, t.Chat.Calls.Count);
        }
        Assert.Empty(t.Runtimes);
    }
}
