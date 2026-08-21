using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace CoopKit
{
    /// <summary>
    /// A localhost request/response channel into a running game.
    ///
    /// This is the piece that makes a mod end-to-end testable. A harness can
    /// launch the game and inject input without it, but without a way to *read*
    /// live state it can only assert on log lines and pixels — neither of which
    /// can answer "is there a second player, and where is he". This is the
    /// moral equivalent of a browser's DevTools protocol: one socket, JSON in,
    /// JSON out, driven by the program under test.
    ///
    /// Three properties are deliberate:
    ///
    ///  * <b>Loopback only.</b> Bound to 127.0.0.1, never a wildcard. This
    ///    exposes internal game state and, through commands, control of it.
    ///  * <b>Off by default.</b> The consuming mod decides; nothing here starts
    ///    on its own. Released builds ship with it disabled.
    ///  * <b>Handlers run on the game thread.</b> Almost every engine's object
    ///    model explodes when touched from a background thread. The socket
    ///    thread parks the request on a queue; <see cref="Pump"/>, called from
    ///    the mod's per-frame update, runs the handler and wakes the waiter.
    ///
    /// Works under Wine/Proton: the game's TCP stack is the host's, so a
    /// Windows build listening on 127.0.0.1 is reachable from Linux tooling.
    /// </summary>
    public sealed class DebugServer : IDisposable
    {
        /// <summary>Handler for one route. Runs on the game thread. Returns a JSON body.</summary>
        public delegate string Handler(IDictionary<string, string> query);

        private const int JobTimeoutMs = 5000;
        private const int MaxRequestBytes = 16 * 1024;

        private readonly Dictionary<string, Handler> _routes =
            new Dictionary<string, Handler>(StringComparer.OrdinalIgnoreCase);
        private readonly Queue<Job> _queue = new Queue<Job>();
        private readonly Action<string> _log;

        private TcpListener _listener;
        private Thread _accept;
        private volatile bool _running;

        private string _snapshotPath;
        private string _snapshotRoute;
        private double _snapshotEvery;
        private double _snapshotNext;

        public int Port { get; private set; }

        public DebugServer(Action<string> log = null) { _log = log; }

        /// <summary>Register a route. Paths are matched exactly, leading slash included.</summary>
        public DebugServer Route(string path, Handler handler)
        {
            _routes[path] = handler;
            return this;
        }

        /// <summary>
        /// Also write one route's output to a file every <paramref name="everySeconds"/>.
        /// A belt-and-braces channel: it survives a firewalled or unroutable
        /// socket, and leaves a post-mortem trace of the last live state after
        /// a crash. Written atomically (temp + replace) so a reader never sees
        /// half a document.
        /// </summary>
        public DebugServer Snapshot(string path, string route, double everySeconds = 0.5)
        {
            _snapshotPath = path; _snapshotRoute = route; _snapshotEvery = everySeconds;
            return this;
        }

        /// <summary>
        /// Bind and start serving. Returns false (and logs) rather than throwing
        /// if the port is taken — a debug aid must never keep the game from
        /// starting.
        /// </summary>
        public bool Start(int port)
        {
            try
            {
                _listener = new TcpListener(IPAddress.Loopback, port);
                _listener.Start();
                Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
                _running = true;
                _accept = new Thread(AcceptLoop) { IsBackground = true, Name = "CoopKit.DebugServer" };
                _accept.Start();
                _log?.Invoke($"Debug server listening on 127.0.0.1:{Port} ({_routes.Count} routes)");
                return true;
            }
            catch (Exception e)
            {
                _log?.Invoke($"Debug server could not bind port {port}: {e.Message}");
                _listener = null;
                return false;
            }
        }

        /// <summary>
        /// Call every frame from the mod's update. Runs queued handlers on the
        /// game thread and refreshes the snapshot file.
        /// </summary>
        public void Pump(double now)
        {
            for (;;)
            {
                Job job;
                lock (_queue)
                {
                    if (_queue.Count == 0) break;
                    job = _queue.Dequeue();
                }
                try { job.Result = job.Work(job.Query); }
                catch (Exception e) { job.Error = e.ToString(); }
                finally { job.Done.Set(); }
            }

            if (_snapshotPath == null || now < _snapshotNext) return;
            _snapshotNext = now + _snapshotEvery;
            Handler h;
            if (!_routes.TryGetValue(_snapshotRoute, out h)) return;
            try
            {
                var body = h(Empty);
                var tmp = _snapshotPath + ".tmp";
                File.WriteAllText(tmp, body, new UTF8Encoding(false));
                if (File.Exists(_snapshotPath)) File.Delete(_snapshotPath);
                File.Move(tmp, _snapshotPath);
            }
            catch { /* a debug artefact is never worth a frame */ }
        }

        public void Stop()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
            _listener = null;
            // Unblock anyone parked on a request we will now never run.
            lock (_queue)
            {
                while (_queue.Count > 0)
                {
                    var j = _queue.Dequeue();
                    j.Error = "server stopping";
                    j.Done.Set();
                }
            }
        }

        public void Dispose() => Stop();

        // ---- socket side -------------------------------------------------

        private static readonly Dictionary<string, string> Empty = new Dictionary<string, string>();

        private void AcceptLoop()
        {
            while (_running)
            {
                TcpClient client;
                try { client = _listener.AcceptTcpClient(); }
                catch { return; }            // Stop() closed the listener
                var t = new Thread(() => Serve(client)) { IsBackground = true };
                t.Start();
            }
        }

        private void Serve(TcpClient client)
        {
            try
            {
                using (client)
                {
                    client.ReceiveTimeout = 5000;
                    client.SendTimeout = 5000;
                    var stream = client.GetStream();
                    var head = ReadHead(stream);
                    if (head == null) { Respond(stream, 400, "{\"error\":\"bad request\"}"); return; }

                    string path, rawQuery;
                    if (!ParseRequestLine(head, out path, out rawQuery))
                    {
                        Respond(stream, 400, "{\"error\":\"bad request line\"}");
                        return;
                    }

                    Handler handler;
                    if (!_routes.TryGetValue(path, out handler))
                    {
                        Respond(stream, 404, "{\"error\":\"no such route\",\"routes\":"
                            + Json.Array(RouteNames()) + "}");
                        return;
                    }

                    var job = new Job { Work = handler, Query = ParseQuery(rawQuery) };
                    lock (_queue) _queue.Enqueue(job);

                    if (!job.Done.WaitOne(JobTimeoutMs))
                        Respond(stream, 504, "{\"error\":\"game thread did not answer in time\"}");
                    else if (job.Error != null)
                        Respond(stream, 500, Json.Object().Add("error", job.Error).Close());
                    else
                        Respond(stream, 200, job.Result ?? "null");
                }
            }
            catch { /* a dropped client is not an event */ }
        }

        private List<string> RouteNames()
        {
            var names = new List<string>();
            foreach (var k in _routes.Keys) names.Add(Json.Quote(k));
            names.Sort(StringComparer.Ordinal);
            return names;
        }

        /// <summary>Read up to the blank line that ends the request head.</summary>
        private static string ReadHead(NetworkStream stream)
        {
            var buf = new byte[1];
            var sb = new StringBuilder(256);
            var total = 0;
            while (total < MaxRequestBytes)
            {
                var n = stream.Read(buf, 0, 1);
                if (n <= 0) break;
                total++;
                sb.Append((char)buf[0]);
                if (sb.Length >= 4
                    && sb[sb.Length - 1] == '\n' && sb[sb.Length - 2] == '\r'
                    && sb[sb.Length - 3] == '\n' && sb[sb.Length - 4] == '\r')
                    return sb.ToString();
                // Tolerate bare-LF clients (netcat, hand-typed requests).
                if (sb.Length >= 2 && sb[sb.Length - 1] == '\n' && sb[sb.Length - 2] == '\n')
                    return sb.ToString();
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }

        private static bool ParseRequestLine(string head, out string path, out string query)
        {
            path = null; query = "";
            var eol = head.IndexOf('\n');
            var line = (eol < 0 ? head : head.Substring(0, eol)).Trim();
            var parts = line.Split(' ');
            if (parts.Length < 2) return false;
            var target = parts[1];
            var q = target.IndexOf('?');
            if (q >= 0) { path = target.Substring(0, q); query = target.Substring(q + 1); }
            else path = target;
            return path.Length > 0;
        }

        private static Dictionary<string, string> ParseQuery(string raw)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(raw)) return d;
            foreach (var pair in raw.Split('&'))
            {
                if (pair.Length == 0) continue;
                var eq = pair.IndexOf('=');
                var k = eq < 0 ? pair : pair.Substring(0, eq);
                var v = eq < 0 ? "" : pair.Substring(eq + 1);
                d[Unescape(k)] = Unescape(v);
            }
            return d;
        }

        private static string Unescape(string s)
        {
            if (s.IndexOf('%') < 0 && s.IndexOf('+') < 0) return s;
            try { return Uri.UnescapeDataString(s.Replace("+", " ")); }
            catch { return s; }
        }

        private static void Respond(NetworkStream stream, int status, string body)
        {
            var payload = Encoding.UTF8.GetBytes(body ?? "");
            var head = Encoding.ASCII.GetBytes(
                "HTTP/1.1 " + status + " " + Reason(status) + "\r\n" +
                "Content-Type: application/json; charset=utf-8\r\n" +
                "Content-Length: " + payload.Length + "\r\n" +
                "Cache-Control: no-store\r\n" +
                "Connection: close\r\n\r\n");
            stream.Write(head, 0, head.Length);
            stream.Write(payload, 0, payload.Length);
            stream.Flush();
        }

        private static string Reason(int status)
        {
            switch (status)
            {
                case 200: return "OK";
                case 400: return "Bad Request";
                case 404: return "Not Found";
                case 500: return "Internal Server Error";
                case 504: return "Gateway Timeout";
                default: return "Status";
            }
        }

        private sealed class Job
        {
            internal Handler Work;
            internal IDictionary<string, string> Query;
            internal string Result;
            internal string Error;
            internal readonly ManualResetEvent Done = new ManualResetEvent(false);
        }
    }
}
