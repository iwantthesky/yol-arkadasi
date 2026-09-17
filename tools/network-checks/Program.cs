// Real TCP socket/transport tests using the actual linked CoopSession.cs source.
// UnityEngine and the game data contracts below are stubs for this standalone .NET harness.
// JsonUtility is emulated with System.Text.Json. This does not validate Unity's serializer,
// player loop, rendering, or genuine multi-instance Unity runtime; run the game QA separately.
using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using DortCuce.UnityGame;

namespace UnityEngine
{
    public class MonoBehaviour { }
    public static class Mathf
    {
        public static float Clamp(float x, float a, float b) { return Math.Min(b, Math.Max(a, x)); }
        public static int Clamp(int x, int a, int b) { return Math.Min(b, Math.Max(a, x)); }
    }
    public static class Debug { public static void Log(string text) { Console.WriteLine(text); } }
    public static class Time
    {
        static readonly Stopwatch Watch = Stopwatch.StartNew();
        public static float unscaledTime { get { return (float)Watch.Elapsed.TotalSeconds; } }
    }
    public static class JsonUtility
    {
        static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };
        public static string ToJson<T>(T item) { return JsonSerializer.Serialize(item, Options); }
        public static T FromJson<T>(string json) { return JsonSerializer.Deserialize<T>(json, Options); }
    }
}

namespace DortCuce.UnityGame
{
    [Serializable] public struct MotorInput { public float throttle, brake, steer, balance, clutch; public int shift; public bool ignition, reset; }
    [Serializable] public class BikeSnapshot
    {
        public float distance,lateral,elevation,verticalSpeed,speed,lean,leanVelocity,heading,steeringAngle,rpm,elapsed;
        public int gear,checkpoint,crashes;
        public bool engineRunning,crashed,finished,grounded;
        public string message;
    }
}

class Program
{
    static readonly MethodInfo Update = typeof(CoopSession).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
    static int checks;
    static void Check(bool valid, string text) { checks++; if (!valid) throw new Exception("FAILED: " + text); Console.WriteLine("PASS: " + text); }
    static void Pump(float duration, params CoopSession[] sessions)
    {
        Stopwatch timer = Stopwatch.StartNew();
        do { foreach (CoopSession session in sessions) Update.Invoke(session, null); Thread.Sleep(2); } while (timer.Elapsed.TotalSeconds < duration);
    }
    static void Wait(Func<bool> ready, string label, params CoopSession[] sessions)
    {
        Stopwatch timer = Stopwatch.StartNew();
        while (!ready() && timer.Elapsed.TotalSeconds < 4) Pump(0.01f, sessions);
        Check(ready(), label);
    }
    static void Write(TcpClient peer, string json) { byte[] bytes = Encoding.UTF8.GetBytes(json + "\n"); peer.GetStream().Write(bytes, 0, bytes.Length); }
    static void Main()
    {
        CoopRoleChecks.Run();
        CoopSession solo = new CoopSession();
        solo.SubmitLocal(new MotorInput { shift = 1, ignition = true, throttle = 1 });
        Check(solo.CombinedInput().shift == 1 && !solo.CombinedInput().ignition, "Solo pulses consumed once");
        CoopSession host = new CoopSession();
        CoopSession first = new CoopSession();
        CoopSession second = new CoopSession();
        CoopSession third = new CoopSession();
        CoopSession extra = new CoopSession();
        CoopSession[] all = { host, first, second, third, extra };
        try
        {
            host.Host("Host", 4, 49773);
            Check(host.IsHost, "Host listens");
            first.Join("127.0.0.1", "First", 49773);
            Wait(() => first.IsConnected && first.IsClient && host.PlayerCount == 2, "Two-player join", all);
            second.Join("127.0.0.1", "Second", 49773);
            Wait(() => second.IsConnected && second.IsClient && host.PlayerCount == 3, "Three-player join", all);
            third.Join("127.0.0.1", "Third", 49773);
            Wait(() => third.IsConnected && third.IsClient && first.PlayerCount == 4 && second.PlayerCount == 4 && host.PlayerCount == 4, "Four-player roster reaches every peer", all);
            Check(first.LocalSlot == 1 && second.LocalSlot == 2 && third.LocalSlot == 3, "Unique slots");
            host.SubmitLocal(new MotorInput { throttle = 1, brake = 0.2f, steer = 1, reset = true });
            first.SubmitLocal(new MotorInput { throttle = 1, clutch = 1, shift = 1, ignition = true, reset = true });
            second.SubmitLocal(new MotorInput { steer = 0.75f, throttle = 1, reset = true });
            third.SubmitLocal(new MotorInput { balance = -0.25f, throttle = 1, reset = true });
            Pump(0.12f, all);
            MotorInput combined = host.CombinedInput();
            Check(combined.throttle == 1 && combined.brake == 0.2f && combined.steer == 0.75f && combined.balance == -0.25f && combined.clutch == 1, "All four assigned input channels combine");
            Check(combined.shift == 1 && combined.ignition && combined.reset, "Buffered transmission and host reset arrive");
            combined = host.CombinedInput();
            Check(combined.shift == 0 && !combined.ignition && !combined.reset, "Network pulses consumed once per physics tick");
            host.Publish(new BikeSnapshot { distance = 123f, speed = 8f, gear = 2, engineRunning = true, message = "Yol açık" }, true);
            Wait(() => first.RemotePlaying && second.RemoteState != null && third.RemoteState != null, "Host playing state reaches all peers", all);
            Check(third.RemoteState.distance == 123f && first.RemoteState.gear == 2, "Snapshot values preserved");
            Pump(0.6f, all);
            combined = host.CombinedInput();
            Check(combined.throttle == 0 && combined.steer == 0 && combined.balance == 0 && combined.clutch == 0, "Input becomes neutral after 0.5 seconds without samples");
            extra.Join("127.0.0.1", "Extra", 49773);
            Wait(() => !extra.IsClient && extra.Status.Contains("dolu"), "Full room gives explicit rejection", all);
            first.Leave();
            Wait(() => host.PlayerCount == 3 && second.LocalSlot == 1 && third.LocalSlot == 2, "Disconnect reallocates roles", all);
            first.Join("127.0.0.1", "Rejoined", 49773);
            Wait(() => first.IsClient && first.IsConnected && first.LocalSlot == 3 && first.RemotePlaying, "Rejoin during play receives latest state", all);
            Check(first.PlayerNames[3] == "Rejoined", "Rejoined name propagated");
            host.Leave();
            Wait(() => !first.IsClient && !second.IsClient && !third.IsClient, "Host shutdown cleans client connections", all);
            host.Host("Host", 2, 49773);
            Check(host.IsHost, "Immediate host restart reuses port");
            using (TcpClient raw = new TcpClient("127.0.0.1", 49773))
            {
                Write(raw, "{\"protocol\":2,\"kind\":\"hello\",\"name\":\"Raw\"}");
                Wait(() => host.PlayerCount == 2, "Raw peer handshake", host);
                int revision = host.RosterRevision;
                Write(raw, "{\"protocol\":2,\"kind\":\"input\",\"revision\":" + revision + ",\"sequence\":1,\"input\":{\"throttle\":1,\"steer\":1,\"clutch\":1,\"balance\":8,\"shift\":1,\"reset\":true}}");
                Pump(0.08f, host);
                combined = host.CombinedInput();
                Check(combined.throttle == 0 && combined.steer == 0 && !combined.reset && combined.balance == 1 && combined.shift == 1, "Host clamps peer input and prevents role/reset spoofing");
                Write(raw, "{\"protocol\":2,\"kind\":\"input\",\"revision\":" + revision + ",\"sequence\":1,\"input\":{\"shift\":1,\"ignition\":true}}");
                Pump(0.08f, host);
                combined = host.CombinedInput();
                Check(combined.shift == 0 && !combined.ignition, "Duplicate input sequence cannot replay a pulse");
                Write(raw, new string('x', 8193));
                Wait(() => host.PlayerCount == 1, "Oversized line disconnects only offending peer", host);
            }
            using (TcpClient raw = new TcpClient("127.0.0.1", 49773))
            {
                Write(raw, "{\"protocol\":2,\"kind\":\"hello\",\"name\":\"Flood\"}");
                Wait(() => host.PlayerCount == 2, "Fresh peer accepted after malformed input", host);
                try { for (int i = 0; i < 125; i++) Write(raw, "{\"protocol\":2,\"kind\":\"input\"}"); }
                catch (System.IO.IOException) { /* The flood limiter may close during the offending write. */ }
                Wait(() => host.PlayerCount == 1, "Message flood disconnects offending peer", host);
            }
            first.Join("127.0.0.1", "After abuse", 49773);
            Wait(() => first.IsClient && first.IsConnected, "Host remains joinable after peer abuse", all);
            Console.WriteLine("SUCCESS: " + checks + " transport checks plus static role checks. Exact transport source; Unity API stubs used only for this .NET harness.");
        }
        finally { foreach (CoopSession session in all) session.Leave(); solo.Leave(); }
    }
}
