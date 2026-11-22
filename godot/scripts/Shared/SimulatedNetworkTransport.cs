using Anomaly.Net;

namespace Anomaly.Client.Shared;

// Owner: M2 P8 (client play). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// The client's <c>--sim-latency</c> and <c>--sim-loss</c> (scope 2d: the owner can feel prediction on localhost with a
/// network that is not instant): a decorator over any <see cref="IClientTransport"/> that holds every message back by a fixed
/// time in each direction and drops a share of the unreliable <see cref="Channel.Input"/> messages going out (the reliable
/// channels lose nothing; LiteNetLib's own simulation is dead in the NuGet build, as the loopback's remarks say). The round
/// trip grows by the sum of the two delays. Time comes from the caller's clock, so a test drives it with a fake one.
/// </summary>
public sealed class SimulatedNetworkTransport : IClientTransport
{
    private readonly IClientTransport _inner;
    private readonly Func<double> _clock;
    private readonly double _sendDelay;
    private readonly double _receiveDelay;
    private readonly double _outLoss;
    private readonly Random _random;
    private readonly Queue<(double Due, Channel Channel, byte[] Data)> _out = new();
    private readonly Queue<(double Due, Channel Channel, byte[] Data)> _in = new();
    private long _dropped;

    /// <param name="inner">The real transport.</param>
    /// <param name="clock">Monotonic seconds.</param>
    /// <param name="roundTripMs">The extra round trip in milliseconds, split evenly between the two directions.</param>
    /// <param name="inputLoss">The share (0 to 1) of outgoing Input channel messages that are dropped.</param>
    /// <param name="seed">Seeds the loss draws.</param>
    public SimulatedNetworkTransport(IClientTransport inner, Func<double> clock, double roundTripMs, double inputLoss = 0, int seed = 1)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        if (roundTripMs < 0 || inputLoss < 0 || inputLoss > 1)
            throw new ArgumentOutOfRangeException(nameof(roundTripMs));
        _sendDelay = _receiveDelay = roundTripMs / 2000.0;
        _outLoss = inputLoss;
        _random = new Random(seed);
        _inner.Connected += () => Connected?.Invoke();
        _inner.Received += OnInnerReceived;
        _inner.Disconnected += d => Disconnected?.Invoke(d);
    }

    /// <summary>Outgoing Input messages the simulation dropped.</summary>
    public long DroppedInput => _dropped;

    public ClientTransportState State => _inner.State;

    public int RoundTripMs => _inner.RoundTripMs + (int)Math.Round((_sendDelay + _receiveDelay) * 1000.0);

    public long BytesSent => _inner.BytesSent;

    public long BytesReceived => _inner.BytesReceived;

    public event Action Connected;

    public event ClientReceivedHandler Received;

    public event Action<TransportDisconnect> Disconnected;

    public void Connect(string host, int port, ReadOnlySpan<byte> hello) => _inner.Connect(host, port, hello);

    public void Send(Channel channel, ReadOnlySpan<byte> message)
    {
        if (channel == Channel.Input && _outLoss > 0 && _random.NextDouble() < _outLoss)
        {
            _dropped++;
            return;
        }
        if (_sendDelay <= 0 && _out.Count == 0)
        {
            _inner.Send(channel, message);
            return;
        }
        _out.Enqueue((_clock() + _sendDelay, channel, message.ToArray()));
    }

    public void Poll()
    {
        _inner.Poll();
        double now = _clock();
        while (_out.Count > 0 && _out.Peek().Due <= now)
        {
            var item = _out.Dequeue();
            _inner.Send(item.Channel, item.Data);
        }
        while (_in.Count > 0 && _in.Peek().Due <= now)
        {
            var item = _in.Dequeue();
            Received?.Invoke(item.Channel, item.Data);
        }
    }

    public void Disconnect() => _inner.Disconnect();

    public void Dispose() => _inner.Dispose();

    private void OnInnerReceived(Channel channel, ReadOnlySpan<byte> message)
    {
        if (_receiveDelay <= 0 && _in.Count == 0)
        {
            Received?.Invoke(channel, message);
            return;
        }
        _in.Enqueue((_clock() + _receiveDelay, channel, message.ToArray()));
    }
}
