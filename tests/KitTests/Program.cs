using System;
using CoopKit;

class Player { public int HP; public int Soul; }

static class T
{
    static int fails;
    static void Check(bool ok, string name)
    {
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name);
        if (!ok) fails++;
    }

    static string sharedOwner = "P1";
    static int sharedHP = 9, sharedSoul = 66;

    static int Main()
    {
        // Masquerade: impersonate, restore, idempotency, exception path
        var masq = new Masquerade<string>(() => sharedOwner, v => sharedOwner = v);
        var scope = masq.Impersonate("P2");
        Check(sharedOwner == "P2", "masquerade swaps in");
        scope.Restore(); scope.Restore();
        Check(sharedOwner == "P1", "masquerade restores (idempotent)");
        try { masq.Run("P2", () => throw new Exception("boom")); } catch { }
        Check(sharedOwner == "P1", "masquerade restores on exception");
        Check(masq.Impersonate(null) == null, "null impostor is a no-op");

        // PoolSwap: swap in, vanilla mutates, write-back, P1 untouched
        var swap = new PoolSwap<Player>()
            .Add(() => sharedHP,   v => sharedHP = v,   p => p.HP,   (p, v) => p.HP = v)
            .Add(() => sharedSoul, v => sharedSoul = v, p => p.Soul, (p, v) => p.Soul = v);
        var p2 = new Player { HP = 5, Soul = 20 };
        var s = swap.Begin(p2);
        Check(sharedHP == 5 && sharedSoul == 20, "pools swapped in");
        sharedHP -= 2; sharedSoul += 13;         // "vanilla" computing
        s.End(); s.End();
        Check(p2.HP == 3 && p2.Soul == 33, "results written back to pool");
        Check(sharedHP == 9 && sharedSoul == 66, "shared state restored (idempotent)");
        Check(swap.Begin(null) == null, "null player is a no-op");

        // Nested swap (wrapped method calls wrapped method) unwinds correctly
        var outer = swap.Begin(p2);
        var inner = swap.Begin(p2);
        sharedHP = 1;
        inner.End(); outer.End();
        Check(p2.HP == 1 && sharedHP == 9, "nested scopes unwind");

        // Cross-player nesting (A's wrapped call triggers B's) unwinds by stack
        var p3 = new Player { HP = 7, Soul = 0 };
        var oa = swap.Begin(p2);
        var ob = swap.Begin(p3);
        sharedHP = 6;                       // vanilla acting on B
        ob.End();
        sharedHP -= 1;                      // vanilla continuing on A
        oa.End();
        Check(p3.HP == 6 && sharedHP == 9, "cross-player nesting: B kept, shared restored");
        Check(p2.HP == 0, "cross-player nesting: A kept its result");

        // VersionGate
        Check(Harness.VersionGate("1.0", "1.0", false, null, null), "version match passes");
        Check(!Harness.VersionGate("1.1", "1.0", false, _ => { }, null), "mismatch refuses");
        Check(Harness.VersionGate("1.1", "1.0", true, null, _ => { }), "ignore flag loads");

        // ThrottledLog
        int logged = 0;
        var tl = new ThrottledLog(10, _ => logged++);
        tl.Log(0, "a"); tl.Log(5, "b"); tl.Log(11, "c");
        Check(logged == 2, "throttle drops inside window");

        // PacketBus: typed dispatch, removal, reentrancy-safe snapshot
        var bus = new PacketBus();
        int got = 0;
        Action<string> h = _ => got++;
        bus.AddHandler(h);
        bus.AddHandler<int>(_ => got += 10);
        Check(bus.Dispatch("hi") == 1 && got == 1, "bus dispatches by type");
        Check(bus.Dispatch(5) == 1 && got == 11, "bus separates types");
        bus.RemoveHandler(h);
        Check(bus.Dispatch("hi") == 0, "bus removes handlers");
        Check(bus.Dispatch(null) == 0, "bus ignores null");

        // DeadReckoning: extrapolation + staleness fade + glide stop
        var dr = DeadReckoning.At(0, 0, now: 100, fadeSeconds: 2);
        dr.Update(10, 5, 2, 0, now: 100);
        var s1 = dr.Sample(100.5);
        Check(Math.Abs(s1.x - 11) < 1e-4 && Math.Abs(s1.alpha - 0.75f) < 1e-4, "reckons and fades");
        var s2 = dr.Sample(200);
        Check(s2.alpha == 0 && Math.Abs(s2.x - 14) < 1e-4, "fade hits zero, glide stops at fade horizon");


        // ── Json: the snapshot writer ─────────────────────────────────────
        Check(Json.Object().Add("a", 1).Add("b", true).Close() == "{\"a\":1,\"b\":true}",
            "json object");
        Check(Json.Object().Add("s", "he\"llo\n").Close() == "{\"s\":\"he\\\"llo\\n\"}",
            "json escapes quotes and control chars");
        Check(Json.Object().Add("f", 1.5f).Close() == "{\"f\":1.5}", "json float invariant");
        // A dead transform yields NaN; emitting it raw would produce a document
        // no parser accepts, silently breaking every later assertion.
        Check(Json.Object().Add("f", float.NaN).Close() == "{\"f\":null}", "json NaN -> null");
        Check(Json.Object().Add("s", (string)null).Close() == "{\"s\":null}", "json null string");
        Check(Json.Array(new[]{"1","2"}) == "[1,2]", "json array");

        // ── DebugServer: the state channel ────────────────────────────────
        var srv = new DebugServer(_ => { });
        var pumped = 0;
        srv.Route("/state", q => { pumped++;
                     return Json.Object().Add("ok", true)
                        .Add("echo", q.ContainsKey("n") ? q["n"] : "").Close(); })
           .Route("/boom", q => throw new Exception("handler blew up"));
        var snapFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "coopkit-snap-" + Guid.NewGuid().ToString("N") + ".json");
        srv.Snapshot(snapFile, "/state", 0.0);
        Check(srv.Start(0), "server binds an ephemeral port");

        // Handlers only run when the game thread pumps — that is the whole
        // point of the design, so prove requests really do wait for it.
        var stop = false;
        var pump = new System.Threading.Thread(() => {
            var t = 0.0;
            while (!stop) { srv.Pump(t); t += 1.0; System.Threading.Thread.Sleep(5); }
        }) { IsBackground = true };
        pump.Start();

        Func<string, (int, string)> get = path => {
            using (var c = new System.Net.Sockets.TcpClient("127.0.0.1", srv.Port)) {
                var st = c.GetStream();
                var req = System.Text.Encoding.ASCII.GetBytes(
                    "GET " + path + " HTTP/1.1\r\nHost: localhost\r\n\r\n");
                st.Write(req, 0, req.Length);
                using (var rd = new System.IO.StreamReader(st)) {
                    var all = rd.ReadToEnd();
                    var split = all.IndexOf("\r\n\r\n");
                    var head = split < 0 ? all : all.Substring(0, split);
                    var body = split < 0 ? "" : all.Substring(split + 4);
                    var code = int.Parse(head.Split(' ')[1]);
                    return (code, body);
                }
            }
        };

        var (code1, body1) = get("/state?n=hi");
        Check(code1 == 200 && body1 == "{\"ok\":true,\"echo\":\"hi\"}",
            "GET /state answers on the game thread with parsed query");
        Check(pumped > 0, "handler ran on the pump, not the socket thread");

        // Negative paths: a channel that answers 200 to everything is a channel
        // that cannot tell you anything went wrong.
        var (code2, body2) = get("/nope");
        Check(code2 == 404 && body2.Contains("/state"),
            "unknown route 404s and lists what exists");
        var (code3, body3) = get("/boom");
        Check(code3 == 500 && body3.Contains("handler blew up"),
            "a throwing handler 500s with its message, not a hang");

        srv.Pump(999.0);   // snapshot interval elapsed
        Check(System.IO.File.Exists(snapFile)
              && System.IO.File.ReadAllText(snapFile).Contains("\"ok\":true"),
            "snapshot file written for the socket-less fallback");

        stop = true;
        srv.Stop();
        var refused = false;
        try { get("/state"); } catch { refused = true; }
        Check(refused, "Stop() closes the listener");
        try { System.IO.File.Delete(snapFile); } catch { }

        Console.WriteLine(fails == 0 ? "ALL PASS" : $"{fails} FAILURES");
        return fails;
    }
}
