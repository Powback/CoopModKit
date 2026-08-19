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

        Console.WriteLine(fails == 0 ? "ALL PASS" : $"{fails} FAILURES");
        return fails;
    }
}
