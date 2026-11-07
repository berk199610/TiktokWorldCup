using System;
using System.Collections.Generic;

namespace DTK.Sim
{
    public enum MatchPhase { Kickoff, Flow, Chance, Resolving, GoalCelebration, Ended }

    public class TeamState
    {
        public string code;
        public bool isGuest;
        public int score;
        public long likes;
        public int choreoCount;
        public double boostUntil = double.NegativeInfinity;
        public string captainId, captainName, captainAvatar;
        public bool flame;
        public double gpThisMatch;
        public readonly Dictionary<string, long> coinsByUser = new Dictionary<string, long>();
        public readonly List<bool> penaltyKicks = new List<bool>();
        public int PenaltyGoals { get { int n = 0; foreach (var k in penaltyKicks) if (k) n++; return n; } }
    }

    public class ChanceState
    {
        public int attacker;
        public string source;        // natural | gift | penalty
        public double shot, save;
        public float preDelay, window, timeLeft;
        public bool windowStarted, cinematic;
        public int tier;
        public string move;
        public Fan opener;
        public readonly Dictionary<string, double> shotContrib = new Dictionary<string, double>();
        public int Defender => 1 - attacker;
        public double ShotShare => shot + save <= 0 ? 0.5 : shot / (shot + save);
    }

    /// <summary>
    /// One match (DESIGN §6–7). The whole outcome depends on a single number, the ball position
    /// (-100 = team 0's goal, +100 = team 1's goal) and the shot/save bars of a chance.
    /// Team 0 is drawn at the bottom and attacks upwards (+).
    /// </summary>
    public class MatchSim
    {
        class PendingGift { public Fan fan; public int coins, unitCoins; }

        readonly SimContext ctx;
        readonly SimConfig cfg;
        public readonly TeamState[] Teams = { new TeamState(), new TeamState() };
        public readonly float Duration;
        public readonly bool IsFinal;
        public readonly int Index;

        public MatchPhase Phase { get; private set; }
        public float Elapsed { get; private set; }
        public float GoldenElapsed { get; private set; }
        public bool IsGoldenGoal { get; private set; }
        public bool IsPenalties { get; private set; }
        public double BallPos { get; private set; }
        public double BallTarget { get; private set; }
        public ChanceState Chance { get; private set; }
        public ChanceState LastChance { get; private set; }
        public ChanceResult LastResult { get; private set; }
        public int Winner { get; private set; } = -1;
        public int LastGoalTeam { get; private set; } = -1;
        public int LastGoalMinute { get; private set; }
        public string LastGoalName { get; private set; }
        public int KickoffTeam { get; private set; }
        public int IdleDirection => idleDir;

        readonly double[] pendingGp = new double[2];
        readonly double[] penaltyHeld = new double[2];
        readonly List<PendingGift> deferred = new List<PendingGift>();
        readonly Queue<PendingGift> cineQueue = new Queue<PendingGift>();
        float phaseTimer, chanceCooldown;
        int idleDir = 1;
        int penaltyNextTeam;
        bool penaltyDecided;
        readonly List<Fan>[] penaltyOrder = { new List<Fan>(), new List<Fan>() };

        public MatchSim(SimContext ctx, string codeA, bool guestA, string codeB, bool guestB, float durationSec, bool isFinal, int index)
        {
            this.ctx = ctx;
            cfg = ctx.Cfg;
            Teams[0].code = codeA; Teams[0].isGuest = guestA;
            Teams[1].code = codeB; Teams[1].isGuest = guestB;
            Duration = durationSec;
            IsFinal = isFinal;
            Index = index;
            StartKickoff(ctx.Rng.Chance(0.5) ? 0 : 1);
        }

        // ------------------------------------------------------------------ queries

        public int IndexOf(string code)
        {
            if (code == null) return -1;
            if (Teams[0].code == code) return 0;
            if (Teams[1].code == code) return 1;
            return -1;
        }

        public bool IsLastSeconds
        {
            get
            {
                if (IsPenalties) return false;
                float left = IsGoldenGoal ? cfg.tournament.goldenGoalSec - GoldenElapsed : Duration - Elapsed;
                return left <= cfg.rubberBand.lastSec && left > 0;
            }
        }

        public int Minute
        {
            get
            {
                int total = Math.Max(1, cfg.tournament.gameMinutesPerMatch);
                if (IsGoldenGoal || IsPenalties) return total;
                return Math.Max(1, Math.Min(total, (int)Math.Ceiling(Elapsed / Math.Max(1f, Duration) * total)));
            }
        }

        public float GoldenLeft => Math.Max(0f, cfg.tournament.goldenGoalSec - GoldenElapsed);
        public int CinematicLoad => (Phase == MatchPhase.Chance && Chance != null && Chance.cinematic && !Chance.windowStarted ? 1 : 0) + cineQueue.Count;
        public double AvgGpPerSec => ctx.AvgGpPerSec;

        public double K
        {
            get
            {
                double avg = Math.Max(AvgGpPerSec, cfg.ball.minAvgGpPerSec);
                return Clamp(cfg.ball.targetUnitsPerSec / avg, cfg.ball.kMin, cfg.ball.kMax);
            }
        }

        public double Multiplier(int team)
        {
            double m = 1;
            var me = Teams[team];
            var other = Teams[1 - team];
            if (other.score - me.score >= 2) m *= cfg.rubberBand.behindBy2Mult;
            if (IsLastSeconds) m *= cfg.rubberBand.last30Mult;
            if (me.boostUntil > ctx.Now) m *= cfg.choreo.boostMult;
            return m;
        }

        // ------------------------------------------------------------------ input

        /// <summary>Which team a gift helps (DESIGN §4.5). Fans of a guest team turn it into a real team.</summary>
        public int ResolveGiftTeam(Fan fan)
        {
            int idx = IndexOf(fan.team);
            if (idx >= 0)
            {
                if (Teams[idx].isGuest) MakeReal(idx);
                return idx;
            }
            int home = IndexOf(cfg.homeTeam);
            if (home >= 0)
            {
                if (Teams[home].isGuest) MakeReal(home);
                return home;
            }
            bool g0 = Teams[0].isGuest, g1 = Teams[1].isGuest;
            if (g0 && !g1) return 1;
            if (g1 && !g0) return 0;
            if (Teams[0].score != Teams[1].score) return Teams[0].score < Teams[1].score ? 0 : 1;
            return Teams[0].gpThisMatch <= Teams[1].gpThisMatch ? 0 : 1;
        }

        public void MakeReal(int team)
        {
            if (!Teams[team].isGuest) return;
            Teams[team].isGuest = false;
            ctx.Emit(new SimEvent { kind = SimEventKind.GuestBecameReal, team = team, teamCode = Teams[team].code });
        }

        /// <summary>Returns the team index that received the gift, or -1 when the match is over.</summary>
        public int HandleGift(Fan fan, int coins, int unitCoins)
        {
            if (Phase == MatchPhase.Ended || coins <= 0) return -1;
            if (unitCoins <= 0) unitCoins = coins;
            int team = ResolveGiftTeam(fan);
            double raw = coins * cfg.power.gpPerCoin;
            ctx.RawPending += raw;
            AddCoins(team, fan, coins);
            Dispatch(team, fan, coins, unitCoins);
            return team;
        }

        void Dispatch(int team, Fan fan, int coins, int unitCoins)
        {
            int tier = Math.Max(1, cfg.TierOf(unitCoins));
            double gp = coins * cfg.power.gpPerCoin * Multiplier(team);

            if (IsPenalties)
            {
                if (Phase == MatchPhase.Chance && Chance != null) AddToBars(team, fan, gp, tier, unitCoins);
                else
                {
                    penaltyHeld[team] += gp;
                    ShowGiftName(team, fan, tier);
                    if (tier >= 4) AnnounceStar(team, fan, tier);
                }
                Teams[team].gpThisMatch += gp;
                return;
            }

            if (Phase == MatchPhase.GoalCelebration || Phase == MatchPhase.Kickoff || Phase == MatchPhase.Resolving)
            {
                // DESIGN §6.4: the name shows now, the power is applied after the kick-off.
                // K4/K5 fame (announcement + scene) comes when their cinematic actually plays.
                ShowGiftName(team, fan, tier);
                deferred.Add(new PendingGift { fan = fan, coins = coins, unitCoins = unitCoins });
                return;
            }

            Teams[team].gpThisMatch += gp;

            if (Phase == MatchPhase.Chance && Chance != null)
            {
                if (tier >= 4 && Chance.cinematic && !Chance.windowStarted)
                {
                    if (CinematicLoad < Math.Max(1, cfg.queue.maxCinematics))
                    {
                        cineQueue.Enqueue(new PendingGift { fan = fan, coins = coins, unitCoins = unitCoins });
                        ctx.Stats.cinematicsQueued++;
                        ctx.NameTags.Show(team, fan, 2f, "kuyruk");
                        Teams[team].gpThisMatch -= gp; // counted when it plays
                        return;
                    }
                    ctx.Stats.cinematicOverflowApplied++;
                }
                AddToBars(team, fan, gp, tier, unitCoins);
                return;
            }

            // Flow
            if (tier <= 2)
            {
                pendingGp[team] += gp;
                ShowGiftName(team, fan, tier);
                return;
            }
            var t = cfg.Tier(tier);
            if (tier == 3)
                OpenChance(team, "gift", fan, gp, 3, cfg.chance.fastAttackSec, t.chanceSec, false, "sut");
            else
                OpenChance(team, "gift", fan, gp, tier, t.cinematicSec, t.chanceSec, true, cfg.MoveFor(tier, unitCoins));
        }

        public void HandleComment(Fan fan)
        {
            int team = IndexOf(fan.team);
            if (team < 0 || Phase == MatchPhase.Ended) return;
            if (ctx.Now - fan.lastCountedCommentAt < cfg.power.commentCooldownSec) return;
            fan.lastCountedCommentAt = ctx.Now;
            ctx.Stats.commentsCounted++;
            if (cfg.power.comment <= 0) return;
            double raw = cfg.power.comment;
            ctx.RawPending += raw;
            double gp = raw * Multiplier(team);
            if (Phase == MatchPhase.Chance && Chance != null)
            {
                if (team == Chance.attacker) Chance.shot += gp; else Chance.save += gp;
            }
            else if (Phase == MatchPhase.Flow) pendingGp[team] += gp;
        }

        public void HandleLike(Fan fan, int count)
        {
            int team = IndexOf(fan.team);
            if (team < 0 || Phase == MatchPhase.Ended || count <= 0) return;
            var ts = Teams[team];
            ts.likes += count;
            int per = Math.Max(1, cfg.choreo.likesPerChoreo);
            while (ts.likes >= (long)(ts.choreoCount + 1) * per)
            {
                ts.choreoCount++;
                ts.boostUntil = ctx.Now + cfg.choreo.boostSec;
                ctx.Emit(new SimEvent { kind = SimEventKind.Choreo, team = team, teamCode = ts.code, duration = cfg.choreo.choreoSec });
            }
        }

        // ------------------------------------------------------------------ tick

        public void Tick(float dt)
        {
            if (Phase == MatchPhase.Ended) return;
            Elapsed += dt;
            if (IsGoldenGoal) GoldenElapsed += dt;
            UpdateFlames();

            switch (Phase)
            {
                case MatchPhase.Kickoff:
                    phaseTimer -= dt;
                    if (phaseTimer <= 0)
                    {
                        Phase = MatchPhase.Flow;
                        StartQueuedCinematic();
                        ReleaseDeferred();
                    }
                    break;
                case MatchPhase.Flow:
                    FlowTick(dt);
                    break;
                case MatchPhase.Chance:
                    ChanceTick(dt);
                    break;
                case MatchPhase.Resolving:
                    phaseTimer -= dt;
                    MoveDisplay(dt, cfg.ball.maxDisplaySpeed * 1.6);
                    if (phaseTimer <= 0) AfterResolve();
                    break;
                case MatchPhase.GoalCelebration:
                    phaseTimer -= dt;
                    if (phaseTimer <= 0) AfterCelebration();
                    break;
            }
        }

        void FlowTick(float dt)
        {
            double before = BallPos;
            double net = pendingGp[0] - pendingGp[1];
            pendingGp[0] = pendingGp[1] = 0;
            BallTarget += net * K;

            // Self-playing match (DESIGN §6.1): a gentle push that fades away as real activity grows.
            double activity = Clamp(AvgGpPerSec / Math.Max(1, cfg.ball.minAvgGpPerSec), 0, 1);
            double idleSpeed = cfg.chance.zoneStart / Math.Max(5.0, cfg.ball.idleChanceEverySec - cfg.chance.windowSec - cfg.chance.resolveSec - 2.0);
            double wobble = 1 + cfg.ball.idleWobble * Math.Sin(2 * Math.PI * Elapsed / Math.Max(1f, cfg.ball.idleWobblePeriodSec));
            BallTarget += idleDir * idleSpeed * (1 - activity) * wobble * dt;
            BallTarget = Clamp(BallTarget, -100, 100);
            MoveDisplay(dt, cfg.ball.maxDisplaySpeed);
            ctx.Stats.ballDistanceTravelled += Math.Abs(BallPos - before);
            ctx.Stats.ballMeasureSeconds += dt;

            chanceCooldown -= dt;
            if (chanceCooldown <= 0)
            {
                if (BallPos >= cfg.chance.zoneStart) { OpenChance(0, "natural", null, 0, 0, 0, cfg.chance.windowSec, false, "sut"); return; }
                if (BallPos <= -cfg.chance.zoneStart) { OpenChance(1, "natural", null, 0, 0, 0, cfg.chance.windowSec, false, "sut"); return; }
            }
            CheckTime();
        }

        void ChanceTick(float dt)
        {
            var c = Chance;
            BallTarget = (c.attacker == 0 ? 1 : -1) * cfg.chance.ballSpot;
            if (!c.windowStarted)
            {
                c.preDelay -= dt;
                double dist = Math.Abs(BallTarget - BallPos);
                MoveDisplay(dt, Math.Max(cfg.ball.maxDisplaySpeed, dist / Math.Max(0.15, c.preDelay)));
                if (c.preDelay <= 0) StartWindow();
                return;
            }
            MoveDisplay(dt, cfg.ball.fastAttackSpeed);
            c.timeLeft -= dt;
            if (c.timeLeft <= 0) Resolve();
        }

        void CheckTime()
        {
            if (IsPenalties) return;
            if (!IsGoldenGoal && Elapsed >= Duration) EndRegulation();
            else if (IsGoldenGoal && GoldenElapsed >= cfg.tournament.goldenGoalSec) StartPenalties();
        }

        // ------------------------------------------------------------------ chance

        void OpenChance(int attacker, string source, Fan opener, double openerGp, int tier, float preDelay, float window, bool cinematic, string move)
        {
            bool penalty = source == "penalty";
            var c = new ChanceState
            {
                attacker = attacker, source = source, opener = opener, tier = tier, move = move,
                preDelay = preDelay, window = window, timeLeft = window, cinematic = cinematic,
                shot = (penalty ? cfg.chance.penaltyBaseGP : cfg.chance.attackBaseGP) + openerGp,
                save = penalty ? cfg.chance.penaltyBaseGP : cfg.chance.keeperBaseGP,
            };
            if (opener != null && openerGp > 0) c.shotContrib[opener.userId] = openerGp;
            if (penalty)
            {
                c.shot += penaltyHeld[attacker];
                c.save += penaltyHeld[1 - attacker];
                penaltyHeld[0] = penaltyHeld[1] = 0;
            }
            Chance = c;
            Phase = MatchPhase.Chance;
            ctx.Stats.chancesOpened++;
            if (source == "natural") ctx.Stats.naturalChances++;
            if (source == "gift") ctx.Stats.giftChances++;

            ctx.Emit(new SimEvent
            {
                kind = SimEventKind.ChanceOpened, team = attacker, teamCode = Teams[attacker].code, style = source,
                nickname = opener?.nickname, userId = opener?.userId, avatarUrl = opener?.avatarUrl, tier = tier, duration = preDelay + window
            });

            if (cinematic)
            {
                ctx.Emit(new SimEvent
                {
                    kind = SimEventKind.Cinematic, team = attacker, teamCode = Teams[attacker].code, tier = tier, style = move,
                    nickname = opener?.nickname, userId = opener?.userId, avatarUrl = opener?.avatarUrl, duration = preDelay
                });
                ctx.Camera.Request("cinematic", attacker, exempt: true, duration: preDelay);
                AnnounceStar(attacker, opener, tier);
            }
            else if (source == "gift" && opener != null)
            {
                ctx.NameTags.Show(attacker, opener, preDelay + window, "sut");
            }
            if (c.preDelay <= 0) StartWindow();
        }

        void StartWindow()
        {
            var c = Chance;
            c.windowStarted = true;
            ctx.Emit(new SimEvent { kind = SimEventKind.ChanceWindow, team = c.attacker, teamCode = Teams[c.attacker].code, duration = c.window, style = c.source });
            ctx.Camera.Request("chance", c.attacker, exempt: c.cinematic, duration: c.window);
        }

        void AddToBars(int team, Fan fan, double gp, int tier, int unitCoins)
        {
            var c = Chance;
            if (team == c.attacker)
            {
                c.shot += gp;
                c.shotContrib.TryGetValue(fan.userId, out double prev);
                c.shotContrib[fan.userId] = prev + gp;
            }
            else c.save += gp;
            ctx.NameTags.Show(team, fan, 2f, "bar");
            if (tier >= 4) AnnounceStar(team, fan, tier);
        }

        void Resolve()
        {
            var c = Chance;
            double diff = c.shot - c.save;
            double thr = Math.Max(c.shot, c.save) * cfg.chance.postMarginPct / 100.0;
            var r = diff > thr ? ChanceResult.Goal : (diff < -thr ? ChanceResult.Save : ChanceResult.Post);
            LastChance = c;
            LastResult = r;
            Chance = null;
            ctx.Emit(new SimEvent { kind = SimEventKind.ChanceResolved, team = c.attacker, teamCode = Teams[c.attacker].code, value = (int)r, style = c.source });

            if (c.source == "penalty")
            {
                PenaltyResult(c, r);
                return;
            }

            float sign = c.attacker == 0 ? 1 : -1;
            switch (r)
            {
                case ChanceResult.Goal:
                    ScoreGoal(c.attacker, ScorerOf(c));
                    return;
                case ChanceResult.Save:
                    ctx.Stats.saves++;
                    BallTarget = cfg.chance.saveClearTo;
                    chanceCooldown = 0;
                    break;
                default:
                    ctx.Stats.posts++;
                    BallTarget = sign * cfg.chance.postBounceTo;
                    chanceCooldown = cfg.chance.cooldownSec + cfg.chance.resolveSec;
                    break;
            }
            Phase = MatchPhase.Resolving;
            phaseTimer = cfg.chance.resolveSec;
            ctx.Camera.Request("main");
        }

        void AfterResolve()
        {
            if (IsPenalties)
            {
                if (penaltyDecided) EndMatch(Teams[0].PenaltyGoals > Teams[1].PenaltyGoals ? 0 : 1);
                else StartNextPenalty();
                return;
            }
            Phase = MatchPhase.Flow;
            idleDir = ctx.Rng.Chance(0.5) ? 1 : -1;
            StartQueuedCinematic();
            ReleaseDeferred();
        }

        Fan ScorerOf(ChanceState c)
        {
            if (c.opener != null) return c.opener;
            string best = null; double bestGp = 0;
            foreach (var kv in c.shotContrib) if (kv.Value > bestGp) { bestGp = kv.Value; best = kv.Key; }
            if (best != null && ctx.Fans.TryGet(best, out var f)) return f;
            var team = Teams[c.attacker];
            if (team.captainId != null && ctx.Fans.TryGet(team.captainId, out var cap)) return cap;
            return null;
        }

        /// <summary>Test hook: opens a natural chance for the attacker right now (only during open play).</summary>
        internal void ForceNaturalChance(int attacker)
        {
            if (Phase != MatchPhase.Flow) return;
            BallPos = BallTarget = (attacker == 0 ? 1 : -1) * cfg.chance.zoneStart;
            OpenChance(attacker, "natural", null, 0, 0, 0, cfg.chance.windowSec, false, "sut");
        }

        /// <summary>Raised on every match goal (team index, scorer or null). The tournament keeps the top-scorer table.</summary>
        public event Action<int, Fan> GoalScored;

        /// <summary>Streamer panel "skip": ends the match now (leader wins, a draw goes to the team with more power).</summary>
        public void ForceEnd()
        {
            int lead = Teams[0].score - Teams[1].score;
            EndMatch(lead != 0 ? (lead > 0 ? 0 : 1) : (Teams[0].gpThisMatch >= Teams[1].gpThisMatch ? 0 : 1));
        }

        void ScoreGoal(int team, Fan scorer)
        {
            GoalScored?.Invoke(team, scorer);
            Teams[team].score++;
            ctx.Stats.goals++;
            LastGoalTeam = team;
            LastGoalMinute = Minute;
            LastGoalName = scorer?.nickname;
            ctx.Emit(new SimEvent
            {
                kind = SimEventKind.Goal, team = team, teamCode = Teams[team].code, nickname = scorer?.nickname,
                userId = scorer?.userId, avatarUrl = scorer?.avatarUrl, value = LastGoalMinute
            });
            ctx.Announcer.Interrupt(new SimEvent
            {
                style = "gol", team = team, teamCode = Teams[team].code, nickname = scorer?.nickname, userId = scorer?.userId,
                avatarUrl = scorer?.avatarUrl, value = LastGoalMinute, text = "GOL!", duration = 3f
            });
            ctx.Camera.Request("goal", team, exempt: true, duration: cfg.tournament.goalCelebrationSec);
            BallTarget = (team == 0 ? 1 : -1) * 104;
            Phase = MatchPhase.GoalCelebration;
            phaseTimer = cfg.tournament.goalCelebrationSec;
            KickoffTeam = 1 - team;
        }

        void AfterCelebration()
        {
            int lead = Teams[0].score - Teams[1].score;
            if (IsGoldenGoal && lead != 0) { EndMatch(lead > 0 ? 0 : 1); return; }
            if (Teams[0].score >= cfg.tournament.mercyGoals) { EndMatch(0); return; }
            if (Teams[1].score >= cfg.tournament.mercyGoals) { EndMatch(1); return; }
            if (!IsGoldenGoal && Elapsed >= Duration && lead != 0) { EndMatch(lead > 0 ? 0 : 1); return; }
            if (!IsGoldenGoal && Elapsed >= Duration) { StartGoldenGoal(); return; }
            StartKickoff(KickoffTeam);
        }

        void StartKickoff(int team)
        {
            KickoffTeam = team;
            BallPos = 0;
            BallTarget = 0;
            idleDir = team == 0 ? 1 : -1;
            Phase = MatchPhase.Kickoff;
            phaseTimer = cfg.tournament.kickoffSec;
            ctx.Emit(new SimEvent { kind = SimEventKind.Kickoff, team = team, teamCode = Teams[team].code });
            ctx.Camera.Request("main");
        }

        void ReleaseDeferred()
        {
            if (deferred.Count == 0) return;
            var list = new List<PendingGift>(deferred);
            deferred.Clear();
            foreach (var g in list)
            {
                if (Phase == MatchPhase.Ended) break;
                int team = ResolveGiftTeam(g.fan);
                // name and fame were shown already; apply the power silently through the normal path
                Dispatch(team, g.fan, g.coins, g.unitCoins);
            }
        }

        bool StartQueuedCinematic()
        {
            while (cineQueue.Count > 0)
            {
                var g = cineQueue.Dequeue();
                if (Phase == MatchPhase.Ended) return false;
                int team = ResolveGiftTeam(g.fan);
                int tier = Math.Max(4, cfg.TierOf(g.unitCoins));
                var t = cfg.Tier(tier);
                double gp = g.coins * cfg.power.gpPerCoin * Multiplier(team);
                Teams[team].gpThisMatch += gp;
                OpenChance(team, "gift", g.fan, gp, tier, t.cinematicSec, t.chanceSec, true, cfg.MoveFor(tier, g.unitCoins));
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ end of match

        void EndRegulation()
        {
            if (Teams[0].score != Teams[1].score) { EndMatch(Teams[0].score > Teams[1].score ? 0 : 1); return; }
            StartGoldenGoal();
        }

        void StartGoldenGoal()
        {
            IsGoldenGoal = true;
            GoldenElapsed = 0;
            ctx.Announcer.Request(new SimEvent { style = "bilgi", text = "ALTIN GOL", duration = 2.5f }, 90);
            StartKickoff(ctx.Rng.Chance(0.5) ? 0 : 1);
        }

        void StartPenalties()
        {
            IsPenalties = true;
            IsGoldenGoal = false;
            for (int t = 0; t < 2; t++)
            {
                penaltyOrder[t].Clear();
                var list = new List<KeyValuePair<string, long>>(Teams[t].coinsByUser);
                list.Sort((a, b) => b.Value.CompareTo(a.Value));
                foreach (var kv in list) if (ctx.Fans.TryGet(kv.Key, out var f)) penaltyOrder[t].Add(f);
            }
            penaltyNextTeam = ctx.Rng.Chance(0.5) ? 0 : 1;
            ctx.Announcer.Request(new SimEvent { style = "bilgi", text = "PENALTILAR", duration = 2.5f }, 90);
            StartNextPenalty();
        }

        void StartNextPenalty()
        {
            int t = penaltyNextTeam;
            var order = penaltyOrder[t];
            Fan shooter = order.Count > 0 ? order[Teams[t].penaltyKicks.Count % order.Count] : null;
            ctx.Emit(new SimEvent { kind = SimEventKind.Penalty, team = t, teamCode = Teams[t].code, nickname = shooter?.nickname, userId = shooter?.userId, avatarUrl = shooter?.avatarUrl, value = -1 });
            OpenChance(t, "penalty", shooter, 0, 0, cfg.chance.penaltyRunUpSec, cfg.chance.penaltyWindowSec, false, "penalti");
        }

        void PenaltyResult(ChanceState c, ChanceResult r)
        {
            int t = c.attacker;
            Teams[t].penaltyKicks.Add(r == ChanceResult.Goal);
            ctx.Emit(new SimEvent { kind = SimEventKind.Penalty, team = t, teamCode = Teams[t].code, nickname = c.opener?.nickname, value = (int)r });
            int n =Math.Max(1, cfg.tournament.penaltiesPerTeam);
            int a = Teams[0].penaltyKicks.Count, b = Teams[1].penaltyKicks.Count;
            int ga = Teams[0].PenaltyGoals, gb = Teams[1].PenaltyGoals;
            if (a <= n && b <= n)
            {
                if (ga + Math.Max(0, n - a) < gb || gb + Math.Max(0, n - b) < ga) penaltyDecided = true;
                else if (a == n && b == n && ga != gb) penaltyDecided = true;
            }
            else if (a == b && ga != gb) penaltyDecided = true;
            // Silent stream safety: sudden death cannot run forever. The side that gave more power wins.
            if (!penaltyDecided && a == b && a >= n + Math.Max(1, cfg.tournament.maxSuddenDeathRounds))
            {
                penaltyDecided = true;
                if (Teams[0].PenaltyGoals == Teams[1].PenaltyGoals)
                {
                    int w = Teams[0].gpThisMatch == Teams[1].gpThisMatch ? (ctx.Rng.Chance(0.5) ? 0 : 1)
                        : (Teams[0].gpThisMatch > Teams[1].gpThisMatch ? 0 : 1);
                    Teams[w].penaltyKicks.Add(true); // decisive extra kick shown in the shoot-out score
                }
            }
            penaltyNextTeam = 1 - t;
            Phase = MatchPhase.Resolving;
            phaseTimer = cfg.chance.resolveSec + 0.5f;
            BallTarget = (t == 0 ? 1 : -1) * (r == ChanceResult.Goal ? 104 : cfg.chance.ballSpot);
        }

        void EndMatch(int winner)
        {
            if (Phase == MatchPhase.Ended) return;
            Phase = MatchPhase.Ended;
            Winner = winner;
            Chance = null;
            // Queued K4/K5 that never got their scene still get their name on screen.
            while (cineQueue.Count > 0)
            {
                var g = cineQueue.Dequeue();
                AnnounceStar(ResolveGiftTeamSafe(g.fan), g.fan, Math.Max(4, cfg.TierOf(g.unitCoins)));
            }
            deferred.Clear();
            string score = $"{Teams[0].score}-{Teams[1].score}";
            if (Teams[0].penaltyKicks.Count > 0) score += $" (P {Teams[0].PenaltyGoals}-{Teams[1].PenaltyGoals})";
            ctx.Emit(new SimEvent { kind = SimEventKind.MatchEnded, team = winner, teamCode = Teams[winner].code, text = score, value = Index });
            ctx.Camera.Request("main");
        }

        int ResolveGiftTeamSafe(Fan f)
        {
            int idx = IndexOf(f.team);
            return idx >= 0 ? idx : 0;
        }

        // ------------------------------------------------------------------ helpers

        void AddCoins(int team, Fan fan, int coins)
        {
            var ts = Teams[team];
            ts.coinsByUser.TryGetValue(fan.userId, out long prev);
            ts.coinsByUser[fan.userId] = prev + coins;
            string best = null; long bestCoins = -1;
            foreach (var kv in ts.coinsByUser) if (kv.Value > bestCoins) { bestCoins = kv.Value; best = kv.Key; }
            if (best == ts.captainId) return;
            bool takeover = ts.captainId != null;
            ts.captainId = best;
            ctx.Fans.TryGet(best, out var cap);
            ts.captainName = cap?.nickname;
            ts.captainAvatar = cap?.avatarUrl;
            ctx.Emit(new SimEvent { kind = SimEventKind.CaptainChanged, team = team, teamCode = ts.code, userId = best, nickname = ts.captainName, avatarUrl = ts.captainAvatar });
            if (takeover)
                ctx.Announcer.Request(new SimEvent
                {
                    style = "kaptan", team = team, teamCode = ts.code, nickname = ts.captainName, avatarUrl = ts.captainAvatar, text = "YENİ KAPTAN"
                }, 40, "kaptan" + team);
        }

        void ShowGiftName(int team, Fan fan, int tier)
        {
            var t = cfg.Tier(Math.Min(tier, 2));
            string style = tier <= 1 ? "pas" : (tier == 2 ? "calim" : "sut");
            ctx.NameTags.Show(team, fan, tier <= 2 ? t.nameTagSec : 2f, style);
        }

        void AnnounceStar(int team, Fan fan, int tier)
        {
            if (fan == null) return;
            ctx.Announcer.Request(new SimEvent
            {
                style = tier >= 5 ? "efsane" : "yildiz", team = team, teamCode = Teams[team].code, nickname = fan.nickname,
                userId = fan.userId, avatarUrl = fan.avatarUrl, tier = tier, text = tier >= 5 ? "EFSANE ANI" : "YILDIZ HAMLESİ"
            }, tier >= 5 ? 80 : 70);
        }

        void UpdateFlames()
        {
            for (int t = 0; t < 2; t++)
            {
                bool on = Teams[1 - t].score - Teams[t].score >= 2;
                if (on == Teams[t].flame) continue;
                Teams[t].flame = on;
                ctx.Emit(new SimEvent { kind = SimEventKind.Flame, team = t, teamCode = Teams[t].code, value = on ? 1 : 0 });
            }
        }

        void MoveDisplay(float dt, double speed)
        {
            double delta = BallTarget - BallPos;
            double step = speed * dt;
            BallPos += Clamp(delta, -step, step);
        }

        static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
