using System;
using System.Linq;
using NUnit.Framework;

namespace DTK.Sim.Tests
{
    /// <summary>Rules of a single match (DESIGN §6–7), driven directly without the tournament.</summary>
    public class MatchRuleTests
    {
        SimConfig cfg;
        SimContext ctx;
        MatchSim m;
        float Dt => 1f / cfg.sim.tickHz;

        MatchSim NewMatch(string a = "TR", string b = "VN", float duration = 180, Action<SimConfig> tweak = null)
        {
            cfg = new SimConfig();
            cfg.sim.seed = 3;
            tweak?.Invoke(cfg);
            ctx = new SimContext(cfg);
            m = new MatchSim(ctx, a, false, b, false, duration, false, 0);
            Run(cfg.tournament.kickoffSec + 0.2f);
            return m;
        }

        Fan Fan(string id, string team)
        {
            var f = ctx.Fans.GetOrCreate(new GameEvent { userId = id, nickname = id });
            f.team = team;
            return f;
        }

        void Run(float seconds)
        {
            int n = (int)Math.Round(seconds * cfg.sim.tickHz);
            for (int i = 0; i < n; i++) { ctx.Tick(Dt); m.Tick(Dt); }
        }

        void RunUntil(Func<bool> cond, float limit = 60)
        {
            int n = (int)(limit * cfg.sim.tickHz);
            for (int i = 0; i < n && !cond(); i++) { ctx.Tick(Dt); m.Tick(Dt); }
        }

        [Test]
        public void Tiers_FollowCoinValue()
        {
            var c = new SimConfig();
            Assert.AreEqual(1, c.TierOf(1));
            Assert.AreEqual(1, c.TierOf(9));
            Assert.AreEqual(2, c.TierOf(10));
            Assert.AreEqual(3, c.TierOf(100));
            Assert.AreEqual(4, c.TierOf(500));
            Assert.AreEqual(5, c.TierOf(5000));
            Assert.AreEqual(5, c.TierOf(30000));
            Assert.AreEqual("frikik", c.MoveFor(4, 600));
            Assert.AreEqual("uzaktan_fuze", c.MoveFor(4, 2000));
            Assert.AreEqual("rovasata", c.MoveFor(4, 3500));
        }

        [Test]
        public void Chance_NobodyActs_IsGoal()
        {
            NewMatch();
            m.ForceNaturalChance(0);
            RunUntil(() => m.Phase != MatchPhase.Chance);
            Assert.AreEqual(ChanceResult.Goal, m.LastResult);
            Assert.AreEqual(1, m.Teams[0].score);
        }

        [Test]
        public void Chance_TenRosesFromDefence_IsPost()
        {
            NewMatch();
            var vn = Fan("vn1", "VN");
            m.ForceNaturalChance(0);
            m.HandleGift(vn, 10, 1); // 10 roses = 100 GP -> 400 vs 400
            RunUntil(() => m.Phase != MatchPhase.Chance);
            Assert.AreEqual(ChanceResult.Post, m.LastResult);
            Assert.AreEqual(0, m.Teams[0].score);
        }

        [Test]
        public void Chance_FifteenRosesFromDefence_IsSave()
        {
            NewMatch();
            var vn = Fan("vn1", "VN");
            m.ForceNaturalChance(0);
            m.HandleGift(vn, 15, 1); // 400 vs 450
            RunUntil(() => m.Phase != MatchPhase.Chance);
            Assert.AreEqual(ChanceResult.Save, m.LastResult);
        }

        [Test]
        public void CommentSpam_CountsAtMostFourInTenSeconds()
        {
            NewMatch();
            var tr = Fan("spammer", "TR");
            int before = ctx.Stats.commentsCounted;
            for (int i = 0; i < 100; i++) { m.HandleComment(tr); Run(0.1f); }
            Assert.AreEqual(4, ctx.Stats.commentsCounted - before);
            Assert.AreEqual(0, ctx.Out.Count(e => e.kind == SimEventKind.Announcement), "comments never create on-screen text");
        }

        [Test]
        public void TeamlessGift_GoesToHomeTeam()
        {
            NewMatch("VN", "TR");
            var nobody = Fan("anon", null);
            Assert.AreEqual(m.IndexOf("TR"), m.HandleGift(nobody, 1, 1));
        }

        [Test]
        public void TeamlessGift_HomeNotPlaying_GoesToTrailingTeam()
        {
            NewMatch("AZ", "VN");
            m.ForceNaturalChance(0); // AZ scores
            RunUntil(() => m.Teams[0].score == 1);
            var nobody = Fan("anon", null);
            Assert.AreEqual(1, m.HandleGift(nobody, 1, 1));
        }

        [Test]
        public void GiftStorm_TenK4_ThreeInQueueSevenImmediate()
        {
            NewMatch();
            for (int i = 0; i < 10; i++) m.HandleGift(Fan("k4_" + i, "TR"), 500, 500);
            Assert.AreEqual(3, m.CinematicLoad, "1 playing + 2 waiting");
            Assert.AreEqual(2, ctx.Stats.cinematicsQueued);
            Assert.AreEqual(7, ctx.Stats.cinematicOverflowApplied);
            Assert.LessOrEqual(ctx.Stats.maxConcurrentNameTags, cfg.screen.maxNameTags);
        }

        [Test]
        public void Whale_GetsExactlyOneChance_AndTrailingTeamGetsFlame()
        {
            NewMatch();
            m.ForceNaturalChance(0);
            RunUntil(() => m.Phase == MatchPhase.Flow && m.Teams[0].score == 1, 30);
            Assert.AreEqual(1, m.Teams[0].score);
            int chancesBefore = ctx.Stats.chancesOpened;
            var vn = Fan("vn_small", "VN");
            for (int i = 0; i < 5; i++) m.HandleGift(vn, 1, 1);
            m.HandleGift(Fan("whale", "TR"), 30000, 30000);
            RunUntil(() => m.Teams[0].score == 2, 30);
            Assert.AreEqual(2, m.Teams[0].score);
            Run(0.3f);
            Assert.AreEqual(chancesBefore + 1, ctx.Stats.chancesOpened, "one gift = at most one chance");
            Assert.IsTrue(m.Teams[1].flame, "two goals behind -> flame (Geri Dönüş)");
            Assert.AreEqual(1.3, m.Multiplier(1), 1e-6);
            RunUntil(() => m.Phase == MatchPhase.Flow, 20);
            Assert.AreEqual(1, m.KickoffTeam, "the team that conceded kicks off");
        }

        [Test]
        public void SilentStream_PlaysItself_ChanceRoughlyEvery45s()
        {
            NewMatch();
            Run(178);
            int chances = ctx.Stats.naturalChances;
            Assert.That(chances, Is.InRange(2, 7), $"natural chances in 3 minutes: {chances}");
            Assert.Greater(ctx.Stats.ballDistanceTravelled / ctx.Stats.ballMeasureSeconds, 0.5, "the ball never stands still");
        }

        [Test]
        public void DrawnMatch_GoesToGoldenGoalThenPenalties_AndAlwaysEnds()
        {
            NewMatch(duration: 5, tweak: c => c.tournament.goldenGoalSec = 4);
            RunUntil(() => m.IsGoldenGoal, 20);
            Assert.IsTrue(m.IsGoldenGoal);
            RunUntil(() => m.IsPenalties, 20);
            Assert.IsTrue(m.IsPenalties);
            RunUntil(() => m.Phase == MatchPhase.Ended, 600);
            Assert.AreEqual(MatchPhase.Ended, m.Phase, "a silent shoot-out still finishes");
            Assert.AreNotEqual(m.Teams[0].PenaltyGoals, m.Teams[1].PenaltyGoals);
        }

        [Test]
        public void Penalty_NobodyActs_IsMiss()
        {
            NewMatch(duration: 1, tweak: c => c.tournament.goldenGoalSec = 0.5f);
            RunUntil(() => m.IsPenalties && m.Chance == null && m.Teams[0].penaltyKicks.Count + m.Teams[1].penaltyKicks.Count > 0, 60);
            Assert.AreEqual(0, m.Teams[0].PenaltyGoals + m.Teams[1].PenaltyGoals);
        }

        [Test]
        public void MercyRule_FiveGoalsEndsMatch()
        {
            NewMatch();
            for (int g = 0; g < 5; g++)
            {
                RunUntil(() => m.Phase == MatchPhase.Flow, 30);
                m.ForceNaturalChance(0);
                RunUntil(() => m.Teams[0].score == g + 1, 30);
            }
            RunUntil(() => m.Phase == MatchPhase.Ended, 30);
            Assert.AreEqual(MatchPhase.Ended, m.Phase);
            Assert.AreEqual(0, m.Winner);
        }

        [Test]
        public void Choreo_EveryThousandLikes_GivesBoost()
        {
            NewMatch();
            var tr = Fan("liker", "TR");
            m.HandleLike(tr, 999);
            Assert.AreEqual(1.0, m.Multiplier(0), 1e-6);
            m.HandleLike(tr, 1);
            Assert.AreEqual(1.25, m.Multiplier(0), 1e-6);
            Run(cfg.choreo.boostSec + 0.5f);
            Assert.AreEqual(1.0, m.Multiplier(0), 1e-6);
        }

        [Test]
        public void LastThirtySeconds_DoublePowerForBoth()
        {
            NewMatch(duration: 40);
            Run(12);
            Assert.AreEqual(1.5, m.Multiplier(0), 1e-6);
            Assert.AreEqual(1.5, m.Multiplier(1), 1e-6);
        }

        [Test]
        public void Captain_IsBiggestSender_AndTakeoverIsAnnounced()
        {
            NewMatch();
            m.HandleGift(Fan("a", "TR"), 50, 50);
            Assert.AreEqual("a", m.Teams[0].captainId);
            m.HandleGift(Fan("b", "TR"), 60, 60);
            Assert.AreEqual("b", m.Teams[0].captainId);
            Run(1);
            Assert.AreEqual(1, ctx.Out.Count(e => e.kind == SimEventKind.Announcement && e.style == "kaptan"));
        }
    }

    public class TeamJoinTests
    {
        [Test]
        public void FlagEmoji_NameAndCode_AreRecognised()
        {
            var featured = new SimConfig().featuredCountries;
            Assert.AreEqual("TR", CountryResolver.Resolve("🇹🇷", featured));
            Assert.AreEqual("VN", CountryResolver.Resolve("hello 🇻🇳🇻🇳", featured));
            Assert.AreEqual("VN", CountryResolver.Resolve("Việt Nam vô địch", featured));
            Assert.AreEqual("TR", CountryResolver.Resolve("Türkiye!!!", featured));
            Assert.AreEqual("TR", CountryResolver.Resolve("tr", featured));
            Assert.AreEqual("TR", CountryResolver.Resolve("hadi TR", featured));
            Assert.AreEqual("AZ", CountryResolver.Resolve("Azərbaycan", featured));
            Assert.AreEqual("GB-ENG", CountryResolver.Resolve("🏴\U000E0067\U000E0062\U000E0065\U000E006E\U000E0067\U000E007F", featured));
            Assert.IsNull(CountryResolver.Resolve("it is fine no me", featured));
            Assert.IsNull(CountryResolver.Resolve("gooool", featured));
        }

        [Test]
        public void LastFlagWins()
        {
            var h = new SimHarness();
            h.Ev(GameEvent.Comment("u1", "🇹🇷"));
            h.Ev(GameEvent.Comment("u1", "🇻🇳"));
            Assert.IsTrue(h.Ctx.Fans.TryGet("u1", out var f));
            Assert.AreEqual("VN", f.team);
        }

        [Test]
        public void ProfaneName_IsShownAsTaraftar()
        {
            var cfg = new SimConfig();
            var ctx = new SimContext(cfg, new NameFilter(new[] { "kotukelime" }));
            var f = ctx.Fans.GetOrCreate(new GameEvent { userId = "x", nickname = "KötüKelime_99" });
            Assert.AreEqual("Taraftar", f.nickname);
            var g = ctx.Fans.GetOrCreate(new GameEvent { userId = "y", nickname = "Ahmet34 ⚽🔥" });
            Assert.AreEqual("Ahmet34 ⚽", g.nickname); // BMP symbols stay, astral emoji are removed
        }
    }

    /// <summary>Whole-tournament scenarios with bot traffic (DESIGN §16).</summary>
    public class TournamentScenarioTests
    {
        static SimHarness FourCountries(int seed = 11)
        {
            var h = new SimHarness(seed);
            h.AddFans("TR", 6);
            h.AddFans("VN", 5);
            h.AddFans("AZ", 4);
            h.AddFans("ID", 3);
            return h;
        }

        [Test]
        public void Draw_SeedsByFansAndFillsGuests()
        {
            var h = new SimHarness();
            h.AddFans("TR", 5);
            h.AddFans("VN", 4);
            h.StartFirstMatch();
            CollectionAssert.AreEqual(new[] { "TR", "ID", "VN", "AZ" }, h.T.Bracket);
            Assert.IsTrue(h.T.Guests.Contains("ID"));
            Assert.IsTrue(h.T.Match.Teams[1].isGuest);
        }

        [Test]
        public void ThreeTournamentsInARow_RunWithoutErrors()
        {
            var h = FourCountries();
            h.Ctx.RecordEvents = false;
            var bot = new TrafficBot(150) { Countries = new[] { "TR", "VN", "AZ", "ID" } };
            int guard = 0;
            while (h.T.TournamentNumber < 4 && guard++ < 120)
                h.Run(60, bot);
            Assert.GreaterOrEqual(h.T.TournamentNumber, 4, "three full tournaments finished");
        }

        [Test]
        public void TenMatches_AverageTwoToFiveGoals()
        {
            var h = FourCountries(5);
            h.Ctx.RecordEvents = false;
            var bot = new TrafficBot(100) { Countries = new[] { "TR", "VN", "AZ", "ID" } };
            int matches = 0, goals = 0;
            MatchSim counted = null;
            int guard = 0;
            while (matches < 10 && guard++ < 200000)
            {
                bot.Step(h.Dt, h);
                h.T.Tick(h.Dt);
                var m = h.T.Match;
                if (m != null && m.Phase == MatchPhase.Ended && !ReferenceEquals(m, counted))
                {
                    counted = m;
                    matches++;
                    goals += m.Teams[0].score + m.Teams[1].score;
                }
            }
            double avg = goals / (double)Math.Max(1, matches);
            TestContext.WriteLine($"matches={matches} goals={goals} avg={avg:0.00}");
            Assert.AreEqual(10, matches);
            Assert.That(avg, Is.InRange(2.0, 5.0));
        }

        [Test]
        public void Crowd2000_BallStaysWatchable()
        {
            var h = FourCountries();
            var bot = new TrafficBot(2000) { Countries = new[] { "TR", "VN", "AZ", "ID" } };
            h.StartFirstMatch();
            h.Ctx.Stats.ballDistanceTravelled = 0;
            h.Ctx.Stats.ballMeasureSeconds = 0;
            h.Run(150, bot);
            double speed = h.Ctx.Stats.ballDistanceTravelled / Math.Max(1, h.Ctx.Stats.ballMeasureSeconds);
            TestContext.WriteLine($"avg ball speed {speed:0.00} u/s");
            Assert.That(speed, Is.InRange(0.5, 12.0));
        }

        [Test]
        public void Simplicity_OneAnnouncementTwoNameTags_CalmCamera()
        {
            var h = FourCountries();
            var bot = new TrafficBot(100) { Countries = new[] { "TR", "VN", "AZ", "ID" } };
            h.StartFirstMatch();
            h.Ctx.Stats.cameraMoveTimes.Clear();
            h.Run(300, bot);
            Assert.LessOrEqual(h.Ctx.Stats.maxConcurrentAnnouncements, 1);
            Assert.LessOrEqual(h.Ctx.Stats.maxConcurrentNameTags, 2);
            var chanceMoves = h.Ctx.Out.Where(e => e.kind == SimEventKind.CameraShot && e.style == "chance" && e.time >= h.Ctx.Now - 300).Select(e => e.time).ToList();
            for (int i = 1; i < chanceMoves.Count; i++)
                Assert.GreaterOrEqual(chanceMoves[i] - chanceMoves[i - 1], h.Cfg.camera.minSecBetweenMoves - 0.01);
            TestContext.WriteLine($"chance camera moves in 5 min: {chanceMoves.Count}");
            Assert.LessOrEqual(chanceMoves.Count / 5.0, 2.5, "about 1–2 camera moves per match minute");
        }

        [Test]
        public void SponsorWar_AnnouncementsMergeInTenSeconds()
        {
            var h = FourCountries();
            h.StartFirstMatch();
            int start = h.Ctx.Out.Count;
            // two people overtake each other every second for 10 seconds
            for (int i = 0; i < 10; i++)
            {
                h.Ev(GameEvent.Gift(i % 2 == 0 ? "ahmet" : "ece", 20 + i * 5));
                h.Run(1);
            }
            int sponsorAnnouncements = h.Ctx.Out.Skip(start).Count(e => e.kind == SimEventKind.Announcement && e.style == "sponsor");
            Assert.LessOrEqual(sponsorAnnouncements, 1);
        }

        [Test]
        public void ChampionGetsCup_AndNextCupIsNamedAfterSponsor()
        {
            var h = FourCountries();
            h.Ctx.RecordEvents = false;
            h.StartFirstMatch();
            h.Ev(GameEvent.Gift("bigboss", 50, 1, "BigBoss"));
            int guard = 0;
            while (h.T.Phase != TournamentPhase.Ceremony && guard++ < 100000) h.T.Tick(h.Dt);
            Assert.AreEqual(1, h.T.Cups.Values.Sum() + (h.T.Guests.Contains(h.T.Champion) ? 1 : 0));
            while (h.T.Phase != TournamentPhase.Registration && guard++ < 200000) h.T.Tick(h.Dt);
            Assert.AreEqual("BigBoss Kupası", h.T.CupName);
        }
    }
}
