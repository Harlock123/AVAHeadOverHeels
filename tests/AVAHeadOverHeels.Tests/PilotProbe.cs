using AVAHeadOverHeels.Engine;

namespace AVAHeadOverHeels.Tests;

/// <summary>How the computer player gets on in each castle (set HOH_PROBE to run it, for tuning).</summary>
public sealed class PilotProbe(ITestOutputHelper output)
{
    [Fact]
    public void How_the_computer_player_does()
    {
        if (Environment.GetEnvironmentVariable("HOH_PROBE") is not { } mode) return;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int level = 0; level < CastleBook.Count; level++)
        {
            var c = CastleBook.Build(level);
            output.WriteLine($"castle {level} built by {sw.ElapsedMilliseconds} ms");
            var g = new GameEngine(HighScoreTable.Load(Path.Combine(Path.GetTempPath(), "hoh-probe-" + Guid.NewGuid().ToString("N")))) { Silent = true };
            g.Go(level);
            g.SetLives(40);
            var deaths = new Dictionary<string, int>();
            var doing = new Dictionary<string, int>();
            var last = g.Phase;
            int t = 0, joinedAt = -1;
            for (; t < 60 * 600 && g.State == GameState.Playing && g.Cleared == 0; t++)
            {
                var ctl = mode == "lazy" ? new Controls(t / 120 % 2 == 0 ? 1 : 0, t / 120 % 2 == 0 ? 0 : 1, t % 50 == 0) : g.Pilot.Decide(g);
                g.Tick(ctl);
                if (g.World.Joined && joinedAt < 0) joinedAt = t;
                if (g.Phase == Phase.Dying && last != Phase.Dying) deaths[g.World.Cause] = deaths.GetValueOrDefault(g.World.Cause) + 1;
                last = g.Phase;
                doing[g.Pilot.Doing] = doing.GetValueOrDefault(g.Pilot.Doing) + 1;
            }
            output.WriteLine($"{c.Name}: {(g.Cleared > 0 ? "CROWN" : "no crown")} after {t / 60}s (joined at {joinedAt / 60}s), {deaths.Values.Sum()} deaths ({string.Join(", ", deaths.Select(d => $"{d.Key} x{d.Value}"))}); {string.Join(", ", doing.OrderByDescending(d => d.Value).Take(5).Select(d => $"{d.Key} {d.Value / 60}s"))}");
        }
        output.WriteLine($"total {sw.ElapsedMilliseconds} ms");
    }
}
