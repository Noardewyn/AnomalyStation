using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Server;

/// <summary>The command queue (Commands.cs): due commands in (ApplyTick, Session, Seq) order, the per-tick count, dropping a session.</summary>
public class CommandQueueTests
{
    private static QueuedCommand Input(long tick, int session, uint seq) => new(tick, session, seq, CommandKind.Input, null, HeldDirs.North);

    [Fact]
    public void TakeDueReturnsTheDueCommandsInApplyOrder()
    {
        var q = new CommandQueue();
        q.Enqueue(Input(5, 2, 1));
        q.Enqueue(new QueuedCommand(5, 2, QueuedCommand.LeaveSeq, CommandKind.Leave));
        q.Enqueue(Input(6, 1, 9));
        q.Enqueue(Input(5, 1, 3));
        q.Enqueue(new QueuedCommand(5, 0, 1, CommandKind.Console, "list"));
        q.Enqueue(new QueuedCommand(4, 3, QueuedCommand.JoinSeq, CommandKind.Join, "Late"));
        q.Enqueue(Input(5, 1, 2));
        var due = new List<QueuedCommand>();
        Assert.Equal(6, q.TakeDue(5, due));
        Assert.Equal(new[] { (4L, 3, 0u), (5L, 0, 1u), (5L, 1, 2u), (5L, 1, 3u), (5L, 2, 1u), (5L, 2, uint.MaxValue) },
            due.Select(c => (c.ApplyTick, c.Session, c.Seq)));
        Assert.Equal(1, q.Count);
        due.Clear();
        Assert.Equal(1, q.TakeDue(6, due));
        Assert.Equal(0, q.Count);
    }

    [Fact]
    public void EqualKeysKeepTheirArrivalOrder()
    {
        var q = new CommandQueue();
        q.Enqueue(new QueuedCommand(1, 0, 1, CommandKind.Console, "a"));
        q.Enqueue(new QueuedCommand(1, 0, 1, CommandKind.Console, "b"));
        var due = new List<QueuedCommand>();
        q.TakeDue(1, due);
        Assert.Equal(new[] { "a", "b" }, due.Select(c => c.Text));
    }

    [Fact]
    public void CountForCountsTheCommandsOfASessionForOneTick()
    {
        var q = new CommandQueue();
        for (uint i = 1; i <= 3; i++) q.Enqueue(Input(7, 4, i));
        q.Enqueue(Input(8, 4, 4));
        q.Enqueue(Input(7, 5, 1));
        Assert.Equal(3, q.CountFor(4, 7));
        Assert.Equal(1, q.CountFor(4, 8));
        Assert.Equal(0, q.CountFor(4, 9));
        q.TakeDue(7, new List<QueuedCommand>());
        Assert.Equal(0, q.CountFor(4, 7));
        Assert.Equal(1, q.CountFor(4, 8));
    }

    [Fact]
    public void DropSessionKeepsItsLeave()
    {
        var q = new CommandQueue();
        q.Enqueue(Input(3, 1, 1));
        q.Enqueue(Input(3, 2, 1));
        q.Enqueue(new QueuedCommand(3, 1, QueuedCommand.LeaveSeq, CommandKind.Leave));
        q.Enqueue(Input(4, 1, 2));
        q.DropSession(1);
        Assert.Equal(2, q.Count);
        Assert.Equal(0, q.CountFor(1, 4));
        var due = new List<QueuedCommand>();
        q.TakeDue(10, due);
        Assert.Equal(new[] { (1, CommandKind.Leave), (2, CommandKind.Input) }, due.Select(c => (c.Session, c.Kind)));
    }

    [Fact]
    public void TheOrderIsTheSameForAnyArrivalOrder()
    {
        var commands = new List<QueuedCommand>();
        for (int s = 1; s <= 5; s++)
            for (uint seq = 1; seq <= 4; seq++)
                commands.Add(Input(10 + seq % 2, s, seq));
        List<QueuedCommand> Drain(IEnumerable<QueuedCommand> order)
        {
            var q = new CommandQueue();
            foreach (var c in order) q.Enqueue(c);
            var due = new List<QueuedCommand>();
            q.TakeDue(100, due);
            return due;
        }
        var expected = Drain(commands);
        var random = new Random(3);
        for (int i = 0; i < 20; i++)
            Assert.Equal(expected, Drain(commands.OrderBy(_ => random.Next()).ToList()));
    }
}
