using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DortCuce.UnityGame
{
    /// <summary>Small LAN transport. Only the host runs the simulation; clients send assigned controls.</summary>
    public sealed class CoopSession : MonoBehaviour
    {
        const int Protocol = 2;
        const int MaxLineBytes = 8192;
        const float InputTimeout = 0.5f;
        const float SendInputInterval = 1f / 30f;
        const float SendStateInterval = 1f / 20f;

        public bool IsHost { get; private set; }
        public bool IsClient { get; private set; }
        public bool IsOnline { get { return IsHost || IsClient; } }
        public bool IsConnected { get { return IsHost || (!IsClient && !IsOnline) || _assigned; } }
        public int LocalSlot { get; private set; }
        public int PlayerCount { get; private set; } = 1;
        public int Capacity { get; private set; } = 4;
        public string Status { get; private set; } = "Solo practice — you control everything.";
        public string[] PlayerNames { get; private set; } = new[] { "Driver" };
        public bool RemotePlaying { get; private set; }
        public BikeSnapshot RemoteState { get; private set; }
        public int RosterRevision { get; private set; }

        [Serializable]
        sealed class Packet
        {
            public int protocol = Protocol;
            public string kind;
            public string name;
            public int slot;
            public int count;
            public int capacity;
            public int revision;
            public string[] names;
            public long sequence;
            public MotorInput input;
            public BikeSnapshot state;
            public bool playing;
            public string reason;
        }

        sealed class Controls
        {
            public MotorInput Level;
            public float LastInput = float.NegativeInfinity;
            public long Sequence = -1;
            public readonly Queue<MotorInput> Pulses = new Queue<MotorInput>();

            public void Reset()
            {
                Level = default(MotorInput);
                LastInput = float.NegativeInfinity;
                Pulses.Clear();
            }

            public void Set(MotorInput input, float now)
            {
                Level = input;
                Level.shift = 0;
                Level.ignition = false;
                Level.reset = false;
                LastInput = now;
                if ((input.shift != 0 || input.ignition || input.reset) && Pulses.Count < 16)
                    Pulses.Enqueue(new MotorInput { shift = input.shift, ignition = input.ignition, reset = input.reset });
            }

            public MotorInput Take(float now)
            {
                if (now - LastInput > InputTimeout)
                {
                    Pulses.Clear();
                    return default(MotorInput);
                }
                MotorInput result = Level;
                if (Pulses.Count != 0)
                {
                    MotorInput pulse = Pulses.Dequeue();
                    result.shift = pulse.shift;
                    result.ignition = pulse.ignition;
                    result.reset = pulse.reset;
                }
                return result;
            }
        }

        sealed class Player
        {
            public Link Link;
            public string Name;
            public int Slot;
            public readonly Controls Controls = new Controls();
        }

        sealed class Pending
        {
            public Link Link;
            public float Since;
        }

        readonly Controls _local = new Controls();
        readonly List<Player> _players = new List<Player>();
        readonly Dictionary<int, Pending> _pending = new Dictionary<int, Pending>();
        Epoch _epoch;
        Link _server;
        bool _assigned;
        string _localName = "Driver";
        long _sendSequence;
        float _nextInput;
        float _nextSnapshot;
        float _nextPing;
        float _lastServerMessage;
        BikeSnapshot _publishedState;
        bool _publishedPlaying;
        static string _addresses;

        public void Host(string name, int capacity, int port = 47777)
        {
            Leave();
            _localName = CleanName(name);
            Capacity = Mathf.Clamp(capacity, 2, 4);
            if (port < 1024 || port > 65535)
            {
                Status = "Port must be between 1024 and 65535.";
                return;
            }
            Epoch epoch = new Epoch();
            try
            {
                epoch.Listener = new TcpListener(IPAddress.Any, port);
                epoch.Listener.Start(8);
                _epoch = epoch;
                IsHost = true;
                LocalSlot = 0;
                PlayerNames = new[] { _localName };
                RosterRevision++;
                Status = "Room open • " + port + " • 1/" + Capacity;
                Thread thread = new Thread(() => AcceptLoop(epoch)) { IsBackground = true, Name = "Motor LAN accept" };
                thread.Start();
            }
            catch (Exception error)
            {
                epoch.Dispose();
                _epoch = null;
                Status = "Could not open room: " + ShortError(error);
            }
        }

        public void Join(string address, string name, int port = 47777)
        {
            Leave();
            address = (address ?? "").Trim();
            if (address.Length == 0 || address.Length > 253 || port < 1024 || port > 65535)
            {
                Status = "Enter a valid IP/address and a port between 1024 and 65535.";
                return;
            }
            _localName = CleanName(name);
            IsClient = true;
            LocalSlot = -1;
            _assigned = false;
            Status = "Connecting: " + address + ":" + port;
            _lastServerMessage = Time.unscaledTime;
            Epoch epoch = new Epoch();
            _epoch = epoch;
            Thread thread = new Thread(() => ConnectLoop(epoch, address, port))
                { IsBackground = true, Name = "Motor LAN connect" };
            thread.Start();
        }

        public void Solo()
        {
            Leave();
            Status = "Solo practice — you control everything.";
        }

        public void Leave()
        {
            Epoch old = _epoch;
            _epoch = null;
            if (old != null) old.Dispose();
            IsHost = false;
            IsClient = false;
            _assigned = false;
            _server = null;
            _players.Clear();
            _pending.Clear();
            _local.Reset();
            _local.Sequence = -1;
            _sendSequence = 0;
            LocalSlot = 0;
            PlayerCount = 1;
            PlayerNames = new[] { _localName };
            RemoteState = null;
            RemotePlaying = false;
            _publishedState = null;
            _publishedPlaying = false;
            _nextInput = _nextSnapshot = _nextPing = 0f;
            RosterRevision++;
            Status = "You left the room.";
        }

        /// <summary>Call once per rendered frame. Key-down pulses are buffered until one host physics tick consumes them.</summary>
        public void SubmitLocal(MotorInput input)
        {
            if (IsClient && !_assigned) return;
            _local.Set(MaskInput(input, LocalSlot, PlayerCount), Time.unscaledTime);
        }

        /// <summary>Call exactly once per host/solo physics tick; clients must not simulate this result.</summary>
        public MotorInput CombinedInput()
        {
            if (IsClient) return default(MotorInput);
            float now = Time.unscaledTime;
            MotorInput result = MaskInput(_local.Take(now), 0, PlayerCount);
            for (int i = 0; i < _players.Count; i++)
            {
                Player player = _players[i];
                if (player.Link.Closed) continue;
                MotorInput other = MaskInput(player.Controls.Take(now), player.Slot, PlayerCount);
                result.throttle += other.throttle;
                result.brake += other.brake;
                result.steer += other.steer;
                result.balance += other.balance;
                result.clutch += other.clutch;
                result.shift += other.shift;
                result.ignition |= other.ignition;
            }
            return MaskInput(result, 0, 1);
        }

        public void Publish(BikeSnapshot snapshot, bool playing)
        {
            if (IsClient) return;
            _publishedState = snapshot;
            _publishedPlaying = playing;
        }

        void Update()
        {
            Epoch epoch = _epoch;
            if (epoch == null) return;
            NetEvent received;
            int budget = 128;
            while (budget-- > 0 && epoch.TryTake(out received))
            {
                HandleEvent(received);
                if (_epoch != epoch) return;
            }

            float now = Time.unscaledTime;
            if (IsHost)
            {
                List<int> timedOut = null;
                foreach (KeyValuePair<int, Pending> item in _pending)
                {
                    if (item.Value.Link.Closed || now - item.Value.Since > 5f)
                    {
                        if (timedOut == null) timedOut = new List<int>();
                        timedOut.Add(item.Key);
                        item.Value.Link.Close("Join request timed out.");
                    }
                }
                if (timedOut != null)
                    for (int i = 0; i < timedOut.Count; i++) _pending.Remove(timedOut[i]);
                bool removed = false;
                for (int i = _players.Count - 1; i >= 0; i--)
                    if (_players[i].Link.Closed) { _players.RemoveAt(i); removed = true; }
                if (removed) RebuildRoster();

                if (_publishedState != null && now >= _nextSnapshot)
                {
                    _nextSnapshot = now + SendStateInterval;
                    Broadcast(new Packet { kind = "state", state = _publishedState, playing = _publishedPlaying, revision = RosterRevision });
                }
                if (now >= _nextPing)
                {
                    _nextPing = now + 1f;
                    Broadcast(new Packet { kind = "ping" });
                }
            }
            else if (IsClient)
            {
                if ((_server != null && _server.Closed) || now - _lastServerMessage > 10f)
                {
                    DisconnectClient("Connection to the host was lost. You can join again.");
                    return;
                }
                if (_assigned && _server != null && now >= _nextInput)
                {
                    _nextInput = now + SendInputInterval;
                    Send(_server, new Packet {
                        kind = "input", revision = RosterRevision,
                        sequence = ++_sendSequence,
                        input = MaskInput(_local.Take(now), LocalSlot, PlayerCount)
                    });
                }
            }
        }

        void HandleEvent(NetEvent incoming)
        {
            if (incoming.Kind == "error")
            {
                if (IsClient) DisconnectClient("Could not connect: " + incoming.Text);
                else { Leave(); Status = "Network error: " + incoming.Text; }
                return;
            }
            Link link = incoming.Link;
            if (incoming.Kind == "accepted")
            {
                if (IsHost && !link.Closed) _pending[link.Id] = new Pending { Link = link, Since = Time.unscaledTime };
                return;
            }
            if (incoming.Kind == "connected")
            {
                if (!IsClient) { link.Close("Room closed."); return; }
                _server = link;
                _lastServerMessage = Time.unscaledTime;
                Send(link, new Packet { kind = "hello", name = _localName });
                Status = "Connected. Joining room…";
                return;
            }
            if (incoming.Kind == "closed")
            {
                _pending.Remove(link.Id);
                if (IsClient && link == _server)
                {
                    DisconnectClient("Connection to the host was lost. You can join again.");
                    return;
                }
                if (IsHost)
                {
                    int index = _players.FindIndex(p => p.Link == link);
                    if (index >= 0) { _players.RemoveAt(index); RebuildRoster(); }
                }
                return;
            }
            // A rejection can arrive immediately before EOF. Process queued client lines before the close event.
            if (incoming.Kind != "line" || (IsHost && link.Closed)) return;

            Packet packet;
            try { packet = JsonUtility.FromJson<Packet>(incoming.Text); }
            catch (Exception) { link.Close("Invalid network packet."); return; }
            if (packet == null || packet.protocol != Protocol || string.IsNullOrEmpty(packet.kind))
            {
                if (IsClient) DisconnectClient("Game versions do not match.");
                else Reject(link, "Game versions do not match.");
                return;
            }
            if (IsHost) HandleHostPacket(link, packet);
            else if (IsClient && link == _server) HandleClientPacket(packet);
        }

        void HandleHostPacket(Link link, Packet packet)
        {
            Player player = _players.Find(p => p.Link == link);
            if (packet.kind == "hello" && player == null && _pending.ContainsKey(link.Id))
            {
                _pending.Remove(link.Id);
                if (_players.Count + 1 >= Capacity) { Reject(link, "Room is full (" + Capacity + " players)."); return; }
                _players.Add(new Player { Link = link, Name = CleanName(packet.name) });
                RebuildRoster();
                return;
            }
            if (player == null) { Reject(link, "You must join the room first."); return; }
            if (packet.kind != "input") { link.Close("Unexpected network packet."); return; }
            if (packet.revision != RosterRevision || packet.sequence <= player.Controls.Sequence) return;
            player.Controls.Sequence = packet.sequence;
            player.Controls.Set(MaskInput(packet.input, player.Slot, PlayerCount), Time.unscaledTime);
        }

        void HandleClientPacket(Packet packet)
        {
            _lastServerMessage = Time.unscaledTime;
            if (packet.kind == "reject")
            {
                string reason = packet.reason ?? "Room entry was rejected.";
                DisconnectClient(reason.Length > 160 ? reason.Substring(0, 160) : reason);
            }
            else if (packet.kind == "roster")
            {
                if (packet.count < 2 || packet.count > 4 || packet.capacity < packet.count || packet.capacity > 4 ||
                    packet.slot < 1 || packet.slot >= packet.count || packet.names == null || packet.names.Length != packet.count)
                {
                    DisconnectClient("Invalid room information.");
                    return;
                }
                if (packet.revision != RosterRevision) _local.Reset();
                LocalSlot = packet.slot;
                PlayerCount = packet.count;
                Capacity = packet.capacity;
                RosterRevision = packet.revision;
                PlayerNames = new string[packet.count];
                for (int i = 0; i < packet.count; i++) PlayerNames[i] = CleanName(packet.names[i]);
                _assigned = true;
                Status = "Connected • " + PlayerCount + "/" + Capacity + " • " + RoleLabel(LocalSlot, PlayerCount);
            }
            else if (packet.kind == "state")
            {
                if (!_assigned || packet.revision != RosterRevision || !ValidSnapshot(packet.state)) return;
                RemoteState = packet.state;
                RemotePlaying = packet.playing;
            }
            else if (packet.kind != "ping") DisconnectClient("Beklenmeyen sunucu paketi.");
        }

        void RebuildRoster()
        {
            PlayerCount = _players.Count + 1;
            RosterRevision++;
            PlayerNames = new string[PlayerCount];
            PlayerNames[0] = _localName;
            _local.Reset();
            for (int i = 0; i < _players.Count; i++)
            {
                _players[i].Slot = i + 1;
                _players[i].Controls.Reset();
                PlayerNames[i + 1] = _players[i].Name;
            }
            for (int i = 0; i < _players.Count; i++)
                Send(_players[i].Link, new Packet { kind = "roster", slot = i + 1, count = PlayerCount,
                    capacity = Capacity, names = PlayerNames, revision = RosterRevision });
            _nextSnapshot = 0f;
            Status = "Room open • " + PlayerCount + "/" + Capacity + " • " + RoleLabel(0, PlayerCount);
        }

        void DisconnectClient(string reason)
        {
            Leave();
            Status = reason;
        }

        void Broadcast(Packet packet)
        {
            if (_players.Count == 0) return;
            string json = JsonUtility.ToJson(packet);
            for (int i = 0; i < _players.Count; i++) _players[i].Link.Send(json);
        }

        static void Send(Link link, Packet packet) { link.Send(JsonUtility.ToJson(packet)); }
        static void Reject(Link link, string reason)
        {
            Send(link, new Packet { kind = "reject", reason = reason });
            link.FinishWriting();
        }

        public static string RoleLabel(int slot, int count)
        {
            if (count < 1 || count > 4 || slot < 0 || slot >= count) return "Joining…";
            if (count == 1) return "All controls";
            if (count == 2) return slot == 0 ? "Throttle · brake · steering" : "Clutch · gears · balance";
            if (count == 3) return slot == 0 ? "Throttle · brake · steering" : slot == 1 ? "Clutch · gears" : "Balance";
            return slot == 0 ? "Throttle · brake" : slot == 1 ? "Clutch · gears" : slot == 2 ? "Steering" : "Balance";
        }

        public static MotorInput MaskInput(MotorInput input, int slot, int count)
        {
            if (count < 1 || count > 4 || slot < 0 || slot >= count) return default(MotorInput);
            bool solo = count == 1;
            bool driving = slot == 0;
            bool transmission = solo || slot == 1;
            bool steering = solo || (count < 4 ? slot == 0 : slot == 2);
            bool balancing = solo || slot == count - 1;
            return new MotorInput {
                throttle = driving ? SafeClamp(input.throttle, 0f, 1f) : 0f,
                brake = driving ? SafeClamp(input.brake, 0f, 1f) : 0f,
                steer = steering ? SafeClamp(input.steer, -1f, 1f) : 0f,
                balance = balancing ? SafeClamp(input.balance, -1f, 1f) : 0f,
                clutch = transmission ? SafeClamp(input.clutch, 0f, 1f) : 0f,
                shift = transmission ? (input.shift > 0 ? 1 : input.shift < 0 ? -1 : 0) : 0,
                ignition = transmission && input.ignition,
                reset = slot == 0 && input.reset
            };
        }

        static float SafeClamp(float value, float min, float max)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp(value, min, max);
        }

        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        static bool ValidSnapshot(BikeSnapshot state)
        {
            return state != null && Finite(state.distance) && Finite(state.lateral) && Finite(state.elevation) &&
                Finite(state.verticalSpeed) && Finite(state.speed) && Finite(state.lean) && Finite(state.leanVelocity) &&
                Finite(state.heading) && Finite(state.steeringAngle) &&
                Finite(state.rpm) && Finite(state.elapsed) && state.gear >= -1 && state.gear <= 12 &&
                (state.message == null || state.message.Length <= 1024);
        }

        static string CleanName(string name)
        {
            StringBuilder result = new StringBuilder(24);
            foreach (char letter in (name ?? "").Trim())
            {
                if (!char.IsControl(letter) && letter != '<' && letter != '>') result.Append(letter);
                if (result.Length == 24) break;
            }
            return result.Length == 0 ? "Driver" : result.ToString();
        }

        public static string LocalAddresses()
        {
            if (_addresses != null) return _addresses;
            List<string> result = new List<string>();
            try
            {
                foreach (IPAddress address in Dns.GetHostAddresses(Dns.GetHostName()))
                    if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address)) result.Add(address.ToString());
            }
            catch (SocketException) { }
            _addresses = result.Count == 0 ? "127.0.0.1" : string.Join(" / ", result.ToArray());
            return _addresses;
        }

        void OnDestroy() { Leave(); }
        void OnApplicationQuit() { Leave(); }

        // Everything below is socket I/O only. No Unity objects, Unity time, or JSON are touched by a worker thread.
        sealed class NetEvent
        {
            public string Kind;
            public Link Link;
            public string Text;
        }

        sealed class Epoch
        {
            public volatile bool Closed;
            public TcpListener Listener;
            public readonly ConcurrentDictionary<int, Link> Links = new ConcurrentDictionary<int, Link>();
            readonly ConcurrentQueue<NetEvent> _events = new ConcurrentQueue<NetEvent>();
            readonly object _connectLock = new object();
            TcpClient _connecting;
            int _eventCount;

            public bool Enqueue(NetEvent item)
            {
                if (Closed) return false;
                if (Interlocked.Increment(ref _eventCount) > 256) { Interlocked.Decrement(ref _eventCount); return false; }
                _events.Enqueue(item);
                return true;
            }
            public bool TryTake(out NetEvent item)
            {
                if (!_events.TryDequeue(out item)) return false;
                Interlocked.Decrement(ref _eventCount);
                return true;
            }
            public bool Connecting(TcpClient socket)
            {
                lock (_connectLock)
                {
                    if (Closed) { socket.Close(); return false; }
                    _connecting = socket;
                    return true;
                }
            }
            public void Dispose()
            {
                Closed = true;
                try { if (Listener != null) Listener.Stop(); } catch (SocketException) { }
                lock (_connectLock) { if (_connecting != null) _connecting.Close(); _connecting = null; }
                foreach (Link link in Links.Values) link.Close("Room closed.");
                Links.Clear();
            }
        }

        sealed class Link
        {
            static int _nextId;
            public readonly int Id = Interlocked.Increment(ref _nextId);
            public bool Closed { get { return Volatile.Read(ref _closed) != 0; } }
            readonly Epoch _epoch;
            readonly TcpClient _socket;
            readonly NetworkStream _stream;
            readonly ConcurrentQueue<string> _outgoing = new ConcurrentQueue<string>();
            readonly AutoResetEvent _signal = new AutoResetEvent(false);
            int _closed;
            int _queued;
            volatile bool _finishWriting;

            public Link(Epoch epoch, TcpClient socket)
            {
                _epoch = epoch;
                _socket = socket;
                socket.NoDelay = true;
                socket.ReceiveTimeout = 15000;
                socket.SendTimeout = 2500;
                _stream = socket.GetStream();
            }
            public void Start()
            {
                new Thread(ReadLoop) { IsBackground = true, Name = "Motor LAN read" }.Start();
                new Thread(WriteLoop) { IsBackground = true, Name = "Motor LAN write" }.Start();
            }
            public void Send(string line)
            {
                if (Closed || _finishWriting) return;
                if (Interlocked.Increment(ref _queued) > 32)
                {
                    Interlocked.Decrement(ref _queued);
                    Close("Network send queue is full.");
                    return;
                }
                _outgoing.Enqueue(line);
                _signal.Set();
            }
            public void FinishWriting() { _finishWriting = true; _signal.Set(); }
            public void Close(string reason)
            {
                if (Interlocked.Exchange(ref _closed, 1) != 0) return;
                try { _socket.Close(); } catch (SocketException) { }
                _signal.Set();
                Link removed;
                _epoch.Links.TryRemove(Id, out removed);
                _epoch.Enqueue(new NetEvent { Kind = "closed", Link = this, Text = reason });
            }
            void ReadLoop()
            {
                byte[] buffer = new byte[2048];
                byte[] line = new byte[MaxLineBytes];
                int length = 0;
                int messages = 0;
                Stopwatch rateWindow = Stopwatch.StartNew();
                UTF8Encoding utf8 = new UTF8Encoding(false, true);
                try
                {
                    while (!Closed && !_epoch.Closed)
                    {
                        int read = _stream.Read(buffer, 0, buffer.Length);
                        if (read == 0) break;
                        for (int i = 0; i < read; i++)
                        {
                            byte value = buffer[i];
                            if (value == 10)
                            {
                                if (rateWindow.ElapsedMilliseconds >= 1000) { messages = 0; rateWindow.Restart(); }
                                if (++messages > 120) { Close("Too many network packets."); return; }
                                if (length == 0) continue;
                                string decoded = utf8.GetString(line, 0, length);
                                length = 0;
                                if (!_epoch.Enqueue(new NetEvent { Kind = "line", Link = this, Text = decoded }))
                                { Close("Network receive queue is full."); return; }
                            }
                            else
                            {
                                if (length == MaxLineBytes) { Close("Network packet is too large."); return; }
                                line[length++] = value;
                            }
                        }
                    }
                }
                catch (Exception) { /* Socket closure, timeout, and invalid UTF-8 all disconnect this peer. */ }
                finally { Close("Connection closed."); }
            }
            void WriteLoop()
            {
                try
                {
                    while (!Closed && !_epoch.Closed)
                    {
                        string line;
                        while (_outgoing.TryDequeue(out line))
                        {
                            Interlocked.Decrement(ref _queued);
                            byte[] data = Encoding.UTF8.GetBytes(line + "\n");
                            if (data.Length > MaxLineBytes + 1) { Close("Network packet is too large."); return; }
                            _stream.Write(data, 0, data.Length);
                        }
                        if (_finishWriting) { Close("Join request rejected."); return; }
                        _signal.WaitOne(250);
                    }
                }
                catch (Exception) { Close("Network send interrupted."); }
                // The signal remains owned by the short-lived Link so a concurrent Close never touches a disposed handle.
            }
        }

        static void AcceptLoop(Epoch epoch)
        {
            try
            {
                while (!epoch.Closed)
                {
                    TcpClient socket = epoch.Listener.AcceptTcpClient();
                    if (epoch.Closed || epoch.Links.Count >= 8) { socket.Close(); continue; }
                    Link link = new Link(epoch, socket);
                    epoch.Links[link.Id] = link;
                    if (epoch.Closed || !epoch.Enqueue(new NetEvent { Kind = "accepted", Link = link }))
                    { link.Close("Room closed."); continue; }
                    link.Start();
                }
            }
            catch (Exception error)
            {
                if (!epoch.Closed) epoch.Enqueue(new NetEvent { Kind = "error", Text = ShortError(error) });
            }
        }

        static void ConnectLoop(Epoch epoch, string address, int port)
        {
            TcpClient socket = new TcpClient();
            if (!epoch.Connecting(socket)) return;
            try
            {
                IAsyncResult pending = socket.BeginConnect(address, port, null, null);
                using (WaitHandle wait = pending.AsyncWaitHandle)
                {
                    if (!wait.WaitOne(5000)) throw new TimeoutException("Could not connect within 5 seconds.");
                    socket.EndConnect(pending);
                }
                if (epoch.Closed) { socket.Close(); return; }
                Link link = new Link(epoch, socket);
                epoch.Links[link.Id] = link;
                if (epoch.Closed || !epoch.Enqueue(new NetEvent { Kind = "connected", Link = link }))
                { link.Close("Connection cancelled."); return; }
                link.Start();
            }
            catch (Exception error)
            {
                socket.Close();
                if (!epoch.Closed) epoch.Enqueue(new NetEvent { Kind = "error", Text = ShortError(error) });
            }
        }

        static string ShortError(Exception error)
        {
            SocketException socket = error as SocketException;
            if (socket != null)
            {
                if (socket.SocketErrorCode == SocketError.AddressAlreadyInUse) return "This port is already used by another room.";
                if (socket.SocketErrorCode == SocketError.ConnectionRefused) return "Room not found; check the IP and port.";
                return socket.SocketErrorCode.ToString();
            }
            return error is TimeoutException ? "Connection timed out." : error.GetType().Name;
        }
    }
}
