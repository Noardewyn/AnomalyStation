using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client;

/// <summary>
/// The client's play loop on a fake clock (M2 P8): a feed and a <see cref="PlaySession"/> driven frame by frame the way the
/// game scene drives them, with keys and a yaw. Used by the headless prediction and interpolation tests.
/// </summary>
internal sealed class PlayRig
{
    public PlayRig(IWorldFeed feed, double fps = 90, PlayOptions options = null)
    {
        Feed = feed;
        Frame = 1.0 / fps;
        Feed.Poll(Now);
        Session = new PlaySession(feed, options ?? new PlayOptions());
    }

    public IWorldFeed Feed { get; }
    public PlaySession Session { get; }
    public double Frame { get; }
    public double Now { get; private set; }
    public MoveKeys Keys { get; set; }
    public double Yaw { get; set; }
    public bool Controlling { get; set; } = true;

    /// <summary>Runs <paramref name="seconds"/> of frames.</summary>
    public void Run(double seconds, Action<int> perFrame = null)
    {
        int frames = (int)Math.Round(seconds / Frame);
        for (int i = 0; i < frames; i++)
        {
            Now += Frame;
            Session.Frame(Now, Keys, Yaw, Controlling);
            perFrame?.Invoke(i);
        }
    }

    public ReplicaEntity Own => Feed.Replica.TryGet(Feed.OwnEntity, out ReplicaEntity e) ? e : null;
}
