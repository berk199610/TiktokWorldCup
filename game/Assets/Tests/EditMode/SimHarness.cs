using System;
using System.Collections.Generic;

namespace DTK.Sim.Tests
{
    /// <summary>Drives a match or a tournament in fast-forward with optional bot traffic.</summary>
    public class SimHarness
    {
        public readonly SimConfig Cfg;
        public readonly TournamentSim T;
        public SimContext Ctx => T.Ctx;
        public float Dt => 1f / Cfg.sim.tickHz;

        public SimHarness(int seed = 42, Action<SimConfig> tweak = null)
        {
            Cfg = new SimConfig();
            Cfg.sim.seed = seed;
            tweak?.Invoke(Cfg);
            T = new TournamentSim(Cfg);
        }

        public void Ev(GameEvent e) => T.HandleEvent(e);

        /// <summary>Gives the country n fans so it qualifies for the draw.</summary>
        public void AddFans(string code, int n)
        {
            for (int i = 0; i < n; i++) Ev(GameEvent.Comment($"{code}_fan{i}", code == "TR" ? "🇹🇷" : code));
        }

        /// <summary>Runs registration until the first match has started.</summary>
        public MatchSim StartFirstMatch()
        {
            int guard = 0;
            while (T.Phase != TournamentPhase.Match && guard++ < 100000) T.Tick(Dt);
            return T.Match;
        }

        public void Run(float seconds, TrafficBot bot = null)
        {
            int steps = (int)Math.Round(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                bot?.Step(Dt, this);
                T.Tick(Dt);
            }
        }

        /// <summary>Runs until the current match ends (or the limit).</summary>
        public void RunMatchToEnd(TrafficBot bot = null, float limitSec = 1200)
        {
            var m = T.Match;
            int steps = (int)(limitSec / Dt);
            for (int i = 0; i < steps && m.Phase != MatchPhase.Ended; i++)
            {
                bot?.Step(Dt, this);
                T.Tick(Dt);
            }
        }

        public int Count(SimEventKind kind, Func<SimEvent, bool> where = null)
        {
            int n = 0;
            foreach (var e in Ctx.Out) if (e.kind == kind && (where == null || where(e))) n++;
            return n;
        }
    }

    /// <summary>
    /// Realistic-ish crowd traffic for a given number of viewers (same model as the bridge's crowd bot).
    /// Gift unit prices: 80% 1 coin, 8% 5, 9.5% 10–99, 2% 100–499, 0.45% 500–4999, 0.05% 5000+.
    /// Fans also react to an open chance: the defending side sends roses, the attacking side a little less.
    /// </summary>
    public class TrafficBot
    {
        public int Viewers;
        public string[] Countries = { "TR", "VN" };
        public double LikesPerViewerSec = 0.3, CommentsPerViewerSec = 0.01, GiftsPerViewerSec = 0.0015, ReactPerViewerSec = 0.002;
        readonly SimRandom rng;
        int nextId;
        readonly List<string> known = new List<string>();
        readonly Dictionary<string, List<string>> byCountry = new Dictionary<string, List<string>>();
        double carryLikes, carryComments, carryGifts;

        public TrafficBot(int viewers, int seed = 7) { Viewers = viewers; rng = new SimRandom(seed); }

        string NewFan(SimHarness h, string country)
        {
            string id = "bot" + (nextId++);
            known.Add(id);
            if (!byCountry.TryGetValue(country, out var list)) byCountry[country] = list = new List<string>();
            list.Add(id);
            h.Ev(GameEvent.Comment(id, country == "TR" ? "🇹🇷 hadi" : country));
            return id;
        }

        string AnyFan(SimHarness h)
        {
            if (known.Count < Viewers && (known.Count == 0 || rng.Chance(0.3)))
                return NewFan(h, Countries[rng.Range(0, Countries.Length)]);
            return known[rng.Range(0, known.Count)];
        }

        string FanOf(SimHarness h, string country)
        {
            if (byCountry.TryGetValue(country, out var list) && list.Count > 0 && rng.Chance(0.85)) return list[rng.Range(0, list.Count)];
            return NewFan(h, country);
        }

        public void Step(float dt, SimHarness h)
        {
            if (Viewers <= 0) return;
            carryLikes += Viewers * LikesPerViewerSec * dt;
            carryComments += Viewers * CommentsPerViewerSec * dt;
            carryGifts += Viewers * GiftsPerViewerSec * dt;
            while (carryComments >= 1 || (carryComments > 0 && rng.Chance(carryComments)))
            {
                carryComments = Math.Max(0, carryComments - 1);
                h.Ev(GameEvent.Comment(AnyFan(h), "gooool"));
            }
            if (carryLikes >= 1)
            {
                int n = (int)carryLikes;
                carryLikes -= n;
                h.Ev(GameEvent.Like(AnyFan(h), n));
            }
            while (carryGifts >= 1 || (carryGifts > 0 && rng.Chance(carryGifts)))
            {
                carryGifts = Math.Max(0, carryGifts - 1);
                string id = AnyFan(h);
                double r = rng.NextDouble();
                int unit, count = 1;
                if (r < 0.80) { unit = 1; count = rng.Range(1, 6); }
                else if (r < 0.88) unit = 5;
                else if (r < 0.975) unit = rng.Range(10, 100);
                else if (r < 0.995) unit = rng.Range(100, 500);
                else if (r < 0.9995) unit = rng.Range(500, 5000);
                else unit = rng.Range(5000, 30000);
                h.Ev(GameEvent.Gift(id, unit, count));
            }
            // Reaction to the shot/save bar (DESIGN §6.3: "10 gül = kurtarış").
            var m = h.T.Match;
            if (m != null && m.Phase == MatchPhase.Chance && m.Chance != null && m.Chance.windowStarted && m.Chance.source != "penalty")
            {
                double p = Math.Min(0.9, Viewers * ReactPerViewerSec) * dt;
                if (rng.Chance(p)) h.Ev(GameEvent.Gift(FanOf(h, m.Teams[m.Chance.Defender].code), 1, rng.Range(3, 16)));
                if (rng.Chance(p * 0.6)) h.Ev(GameEvent.Gift(FanOf(h, m.Teams[m.Chance.attacker].code), 1, rng.Range(3, 12)));
            }
        }
    }
}
