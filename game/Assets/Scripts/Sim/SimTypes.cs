using System;
using System.Collections.Generic;

namespace DTK.Sim
{
    /// <summary>
    /// One viewer interaction, already normalised by the bridge (DESIGN §17 event schema).
    /// type: gift | like | comment | follow | share | join
    /// For gifts: coins = total coins of this event, unitCoins = value of a single gift (tier is decided by unitCoins).
    /// </summary>
    [Serializable]
    public class GameEvent
    {
        public string type;
        public string userId;
        public string nickname;
        public string avatarUrl;
        public int coins;
        public int unitCoins;
        public string giftName;
        public int count = 1;
        public string text;
        public double ts;

        public static GameEvent Gift(string user, int unitCoins, int count = 1, string nick = null)
            => new GameEvent { type = "gift", userId = user, nickname = nick ?? user, unitCoins = unitCoins, count = count, coins = unitCoins * count };
        public static GameEvent Comment(string user, string text, string nick = null)
            => new GameEvent { type = "comment", userId = user, nickname = nick ?? user, text = text };
        public static GameEvent Like(string user, int count = 1)
            => new GameEvent { type = "like", userId = user, nickname = user, count = count };
    }

    public enum SimEventKind
    {
        TournamentPhase,   // text = phase name
        Draw,              // text = comma separated team codes (seeded bracket order)
        MatchStarted,      // teamCode = "A-B", value = match index
        MatchEnded,        // team = winner index, text = score
        Kickoff,           // team = kicking-off team
        NameTag,           // team, nickname, duration, style (pas|calim|sut|bar|kuyruk)
        Announcement,      // style (gol|yildiz|efsane|kaptan|sponsor|mac|sampiyon|bilgi), text, nickname, avatarUrl, duration
        ChanceOpened,      // team = attacker, style = natural|gift|penalty, nickname = opener, tier
        ChanceWindow,      // bars visible now; duration = window
        ChanceResolved,    // team = attacker, value = (int)ChanceResult
        Goal,              // team = scorer team, nickname, value = minute
        Cinematic,         // team, tier, style = move, nickname, avatarUrl, duration
        CameraShot,        // style = main|chance|goal|cinematic|ceremony, team, duration
        CaptainChanged,    // team, nickname
        SponsorChanged,    // nickname, teamCode
        Choreo,            // team, duration
        Flame,             // team, value = 1 on / 0 off
        GuestBecameReal,   // team
        FanJoined,         // teamCode, nickname, userId
        FanCommented,      // teamCode, nickname, userId
        FanFollowed,       // teamCode, userId
        Penalty,           // team = shooter team, nickname, value = result after resolve (-1 pending)
        Champion,          // teamCode, nickname = sponsor
        Paused             // value = 1/0
    }

    public enum ChanceResult { None = 0, Goal = 1, Post = 2, Save = 3 }

    public class SimEvent
    {
        public SimEventKind kind;
        public double time;
        public int team = -1;
        public string teamCode;
        public string userId, nickname, avatarUrl;
        public string text, style;
        public int tier, value;
        public float duration;

        public override string ToString() => $"{time:0.0} {kind} team={team} {teamCode} {nickname} {style} {text} v={value}";
    }

    /// <summary>A viewer who interacted at least once.</summary>
    public class Fan
    {
        public string userId, nickname, avatarUrl, team;
        public double lastCountedCommentAt = double.NegativeInfinity;
        public long tournamentCoins, totalCoins;
        public bool vip;
    }

    /// <summary>Aggregated numbers that tests and the streamer panel read.</summary>
    public class SimStats
    {
        public int chancesOpened, goals, posts, saves, naturalChances, giftChances;
        public int cinematicsQueued, cinematicOverflowApplied;
        public int commentsCounted;
        public int maxConcurrentAnnouncements, maxConcurrentNameTags;
        public readonly List<double> cameraMoveTimes = new List<double>();
        public double ballDistanceTravelled, ballMeasureSeconds;
    }

    /// <summary>Small deterministic PRNG (xorshift) so tests are repeatable.</summary>
    public class SimRandom
    {
        uint state;
        public SimRandom(int seed) { state = (uint)(seed == 0 ? Environment.TickCount : seed) | 1u; }
        public uint NextUInt() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state; }
        public double NextDouble() => (NextUInt() & 0xFFFFFF) / (double)0x1000000;
        public int Range(int minInclusive, int maxExclusive) => minInclusive + (int)(NextDouble() * (maxExclusive - minInclusive));
        public bool Chance(double p) => NextDouble() < p;
    }

    /// <summary>Fixed-size moving sum over the last N ticks.</summary>
    public class RollingSum
    {
        readonly double[] buf; int idx; double sum;
        public RollingSum(int size) { buf = new double[Math.Max(1, size)]; }
        public void Push(double v) { sum += v - buf[idx]; buf[idx] = v; idx = (idx + 1) % buf.Length; }
        public double Sum => sum < 0 ? 0 : sum;
        public int Length => buf.Length;
    }
}
