using System;
using System.Collections.Generic;
using System.Text;

namespace DTK.Sim
{
    /// <summary>Shared state of one running game: clock, RNG, viewers, output events and the screen "gates".</summary>
    public class SimContext
    {
        public readonly SimConfig Cfg;
        public readonly SimRandom Rng;
        public double Now;
        public readonly List<SimEvent> Out = new List<SimEvent>();
        public readonly FanRegistry Fans;
        public readonly AnnouncementGate Announcer;
        public readonly NameTagGate NameTags;
        public readonly CameraDirector Camera;
        public readonly SimStats Stats = new SimStats();
        /// <summary>When false, SimEvents are not stored (long headless test runs).</summary>
        public bool RecordEvents = true;

        /// <summary>
        /// Raw power (GP) of the whole stream over the last autoscaleWindowSec. It belongs to the stream, not to a match,
        /// so the ball speed does not jump at every kick-off (DESIGN §6.1 automatic k).
        /// </summary>
        public readonly RollingSum GpWindow;
        public double RawPending;

        public SimContext(SimConfig cfg, NameFilter filter = null)
        {
            Cfg = cfg;
            Rng = new SimRandom(cfg.sim.seed);
            Fans = new FanRegistry(cfg, filter ?? new NameFilter());
            Announcer = new AnnouncementGate(this);
            NameTags = new NameTagGate(this);
            Camera = new CameraDirector(this);
            GpWindow = new RollingSum((int)Math.Round(cfg.ball.autoscaleWindowSec * Math.Max(1, cfg.sim.tickHz)));
        }

        /// <summary>Average raw GP per second of the stream (uses the elapsed time while the window is still filling).</summary>
        public double AvgGpPerSec => GpWindow.Sum / Math.Max(1.0, Math.Min(Cfg.ball.autoscaleWindowSec, Now));

        public void Emit(SimEvent e)
        {
            e.time = Now;
            if (RecordEvents) Out.Add(e);
        }

        public void Tick(float dt)
        {
            Now += dt;
            GpWindow.Push(RawPending);
            RawPending = 0;
            Announcer.Tick();
            NameTags.Tick();
        }
    }

    /// <summary>Keeps every viewer who interacted. Display names pass through the name filter once.</summary>
    public class FanRegistry
    {
        readonly Dictionary<string, Fan> byId = new Dictionary<string, Fan>();
        readonly SimConfig cfg;
        readonly NameFilter filter;

        public FanRegistry(SimConfig cfg, NameFilter filter) { this.cfg = cfg; this.filter = filter; }

        public IEnumerable<Fan> All => byId.Values;
        public int Count => byId.Count;

        public Fan GetOrCreate(GameEvent e)
        {
            string id = string.IsNullOrEmpty(e.userId) ? (e.nickname ?? "anon") : e.userId;
            if (!byId.TryGetValue(id, out var fan))
            {
                fan = new Fan { userId = id };
                byId[id] = fan;
            }
            if (!string.IsNullOrEmpty(e.nickname)) fan.nickname = filter.Clean(e.nickname, cfg.moderation.maxNameLength);
            if (string.IsNullOrEmpty(fan.nickname)) fan.nickname = "Taraftar";
            if (!string.IsNullOrEmpty(e.avatarUrl)) fan.avatarUrl = e.avatarUrl;
            return fan;
        }

        public bool TryGet(string id, out Fan fan) => byId.TryGetValue(id, out fan);

        public int CountFans(string code)
        {
            int n = 0;
            foreach (var f in byId.Values) if (f.team == code) n++;
            return n;
        }

        public Dictionary<string, int> FanCounts()
        {
            var d = new Dictionary<string, int>();
            foreach (var f in byId.Values)
            {
                if (f.team == null) continue;
                d.TryGetValue(f.team, out int n);
                d[f.team] = n + 1;
            }
            return d;
        }

        public void ResetTournament()
        {
            foreach (var f in byId.Values) f.tournamentCoins = 0;
        }

        public void Remove(string userId) => byId.Remove(userId);
    }

    /// <summary>Hides profanity in display names (DESIGN §15): a filtered name becomes "Taraftar".</summary>
    public class NameFilter
    {
        readonly List<string> banned = new List<string>();

        public NameFilter(IEnumerable<string> bannedWords = null)
        {
            if (bannedWords == null) return;
            foreach (var w in bannedWords)
            {
                var t = (w ?? "").Trim();
                if (t.Length == 0 || t.StartsWith("#")) continue;
                banned.Add(CountryResolver.Normalize(t).Replace(" ", ""));
            }
        }

        public bool IsBanned(string text)
        {
            if (banned.Count == 0 || string.IsNullOrEmpty(text)) return false;
            string n = CountryResolver.Normalize(text).Replace(" ", "");
            foreach (var b in banned) if (b.Length > 0 && n.Contains(b)) return true;
            return false;
        }

        public string Clean(string name, int maxLen)
        {
            if (IsBanned(name)) return "Taraftar";
            var sb = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsSurrogate(c)) continue;            // emoji and other astral symbols
                if (char.IsControl(c)) continue;
                if (c >= '︀' && c <= '️') continue; // variation selectors
                if (c == '‍') continue;                  // zero-width joiner
                sb.Append(c);
            }
            string s = sb.ToString().Trim();
            if (s.Length == 0) return "Taraftar";
            if (s.Length > maxLen) s = s.Substring(0, maxLen - 1) + "…";
            return s;
        }
    }

    /// <summary>
    /// The single "duyuru yuvası" (DESIGN §12): at most one announcement on screen, short queue,
    /// sponsor/captain changes merged so a seat war produces at most one line per announceMergeSec.
    /// </summary>
    public class AnnouncementGate
    {
        class Item { public SimEvent ev; public int priority; public string mergeKey; }

        readonly SimContext ctx;
        readonly List<Item> queue = new List<Item>();
        double busyUntil = double.NegativeInfinity;
        readonly Dictionary<string, double> lastShownByKey = new Dictionary<string, double>();

        public AnnouncementGate(SimContext ctx) { this.ctx = ctx; }

        public bool Busy => ctx.Now < busyUntil;
        public int Pending => queue.Count;

        /// <param name="priority">higher shows first (gol=100, şampiyon=100, efsane=80, yıldız=70, kaptan/sponsor=40)</param>
        /// <param name="mergeKey">announcements with the same key replace each other while waiting and are rate limited</param>
        public void Request(SimEvent ev, int priority, string mergeKey = null)
        {
            ev.kind = SimEventKind.Announcement;
            if (ev.duration <= 0) ev.duration = ctx.Cfg.screen.announcementSec;
            if (mergeKey != null)
            {
                for (int i = queue.Count - 1; i >= 0; i--)
                    if (queue[i].mergeKey == mergeKey) queue.RemoveAt(i);
            }
            queue.Add(new Item { ev = ev, priority = priority, mergeKey = mergeKey });
            // stable sort: priority desc, then arrival
            queue.Sort((a, b) => b.priority.CompareTo(a.priority));
            while (queue.Count > Math.Max(1, ctx.Cfg.screen.announcementQueueMax)) queue.RemoveAt(queue.Count - 1);
            Tick();
        }

        /// <summary>High-priority moments (goal, champion) interrupt whatever is on screen.</summary>
        public void Interrupt(SimEvent ev)
        {
            ev.kind = SimEventKind.Announcement;
            if (ev.duration <= 0) ev.duration = ctx.Cfg.screen.announcementSec;
            busyUntil = double.NegativeInfinity;
            queue.Insert(0, new Item { ev = ev, priority = 1000 });
            Tick();
        }

        public void Clear() { queue.Clear(); }

        public void Tick()
        {
            if (Busy || queue.Count == 0) return;
            for (int i = 0; i < queue.Count; i++)
            {
                var it = queue[i];
                if (it.mergeKey != null && lastShownByKey.TryGetValue(it.mergeKey, out double last)
                    && ctx.Now - last < ctx.Cfg.sponsor.announceMergeSec)
                    continue; // still inside the merge window; wait
                queue.RemoveAt(i);
                if (it.mergeKey != null) lastShownByKey[it.mergeKey] = ctx.Now;
                busyUntil = ctx.Now + it.ev.duration;
                ctx.Emit(it.ev);
                ctx.Stats.maxConcurrentAnnouncements = Math.Max(ctx.Stats.maxConcurrentAnnouncements, 1);
                return;
            }
        }
    }

    /// <summary>Name tags above players (DESIGN §8): never more than maxNameTags at the same time.</summary>
    public class NameTagGate
    {
        readonly SimContext ctx;
        readonly List<double> activeUntil = new List<double>();

        public NameTagGate(SimContext ctx) { this.ctx = ctx; }

        public int Active
        {
            get { Tick(); return activeUntil.Count; }
        }

        public void Show(int team, Fan fan, float seconds, string style)
        {
            Tick();
            int max = Math.Max(1, ctx.Cfg.screen.maxNameTags);
            while (activeUntil.Count >= max) activeUntil.RemoveAt(0); // oldest tag gives way
            activeUntil.Add(ctx.Now + seconds);
            ctx.Stats.maxConcurrentNameTags = Math.Max(ctx.Stats.maxConcurrentNameTags, activeUntil.Count);
            ctx.Emit(new SimEvent
            {
                kind = SimEventKind.NameTag, team = team, userId = fan?.userId, nickname = fan?.nickname ?? "Taraftar",
                avatarUrl = fan?.avatarUrl, duration = seconds, style = style
            });
        }

        public void Tick()
        {
            for (int i = activeUntil.Count - 1; i >= 0; i--)
                if (activeUntil[i] <= ctx.Now) activeUntil.RemoveAt(i);
        }
    }

    /// <summary>
    /// Decides camera cuts (DESIGN §11). The renderer only executes them.
    /// Non-exempt moves are at least minSecBetweenMoves apart; returning to the main shot is always allowed.
    /// </summary>
    public class CameraDirector
    {
        readonly SimContext ctx;
        double lastMove = double.NegativeInfinity;
        public string Current { get; private set; } = "main";

        public CameraDirector(SimContext ctx) { this.ctx = ctx; }

        public bool Request(string shot, int team = -1, bool exempt = false, float duration = 0f)
        {
            if (shot == "main")
            {
                if (Current == "main") return false;
                Current = "main";
                ctx.Emit(new SimEvent { kind = SimEventKind.CameraShot, style = "main", team = team });
                return true;
            }
            if (!exempt && ctx.Now - lastMove < ctx.Cfg.camera.minSecBetweenMoves) return false;
            lastMove = ctx.Now;
            Current = shot;
            ctx.Stats.cameraMoveTimes.Add(ctx.Now);
            ctx.Emit(new SimEvent { kind = SimEventKind.CameraShot, style = shot, team = team, duration = duration });
            return true;
        }
    }
}
