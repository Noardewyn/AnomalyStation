using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 100 also (package T, slice T4c, DV690): the posture byte's bits 6 and 7, the side a lying body was turned to, and the radial's
/// Turn over, a context hint of the Extra action whose pick is a ContextPick of its label (<see cref="RadialExtra.TurnOver"/>, 10, is the server's own).
/// </summary>
public class ProtocolV100TurnOverTests
{
    private static Delta WithPosture(byte posture) => Msg.Delta(5, changed: new[] { new EntityChange { Id = 1, Mask = NetField.Posture, Posture = posture } });

    private static byte RoundTrip(byte posture)
    {
        Assert.True(Codec.TryDecode(Codec.Encode(WithPosture(posture)), out Delta back, out ProtocolError error), error.ToString());
        return Assert.Single(back.Changed).Posture;
    }

    [Fact]
    public void The_turned_side_round_trips_in_the_posture_byte()
    {
        Assert.Equal(64, PostureBits.SideFixed);
        Assert.Equal(128, PostureBits.FaceUp);
        // lying at 90 degrees (bits 0 and 1: 3), turned face up: 3 | 64 | 128; face down: 3 | 64
        Assert.Equal(0xC3, PostureBits.Encode(true, 90, turnedFaceUp: true));
        Assert.Equal(0x43, PostureBits.Encode(true, 90, turnedFaceUp: false));
        foreach (bool? turned in new bool?[] { true, false })
            foreach (bool limp in new[] { false, true })
                foreach (bool buckled in new[] { false, true })
                {
                    byte posture = PostureBits.Encode(true, 270, limp, buckled: buckled, turnedFaceUp: turned);
                    byte back = RoundTrip(posture);
                    Assert.Equal(posture, back);
                    Assert.Equal(turned, PostureBits.TurnedFaceUp(back));
                    Assert.Equal((true, 270, limp, buckled), (PostureBits.IsLying(back), PostureBits.LyingAngle(back), PostureBits.IsLimp(back), PostureBits.IsBuckled(back)));
                }
        // a standing body carries no side, whatever it is given
        Assert.Equal(0, PostureBits.Encode(false, 0, turnedFaceUp: true));
    }

    [Fact]
    public void Older_bytes_decode_as_before_and_a_side_without_its_meaning_is_refused()
    {
        // every byte of version 97 (bits 6 and 7 clear) means what it did: no side fixed
        for (int b = 0; b < 64; b++)
        {
            Assert.Equal(b, RoundTrip((byte)b));
            Assert.Null(PostureBits.TurnedFaceUp((byte)b));
        }
        Assert.Throws<ProtocolException>(() => Codec.Encode(WithPosture(64))); // a side on a standing body
        Assert.Throws<ProtocolException>(() => Codec.Encode(WithPosture(128 | 1))); // face up with no side fixed
        Assert.Throws<ProtocolException>(() => Codec.Encode(WithPosture(128)));
    }

    [Fact]
    public void Turn_over_is_an_extra_hint_and_its_pick_carries_the_label()
    {
        Assert.Equal(10, (int)RadialExtra.TurnOver);
        var hints = new ContextHintsMessage { TargetKind = TargetKind.Entity, Target = 42, Name = "Bob" };
        hints.Hints.Add(new ContextHint(ClickButton.Left, ClickModifiers.None, "Turn over", ContextHintAction.Extra));
        Assert.True(Codec.TryDecode(Codec.Encode(hints), out ContextHintsMessage hintsBack, out ProtocolError error), error.ToString());
        ContextHint hint = Assert.Single(hintsBack.Hints);
        Assert.Equal((ContextHintAction.Extra, "Turn over"), (hint.Action, hint.Label));
        var pick = new ActionMessage(9, ActionKind.ContextPick, 42, ClickButton.None, ClickModifiers.None, "Turn over") { TargetKind = TargetKind.Entity };
        Assert.True(Codec.TryDecode(Codec.Encode(pick), out ActionMessage back, out error), error.ToString());
        Assert.Equal(pick, back);
    }
}
