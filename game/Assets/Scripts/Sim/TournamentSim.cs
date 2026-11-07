using System;
using System.Collections.Generic;

namespace DTK.Sim
{
    public enum TournamentPhase { Registration, Draw, Match, Break, Ceremony }

    /// <summary>
    /// Endless tournament loop (DESIGN §10): registration → draw → semi 1 → break → semi 2 → break → final → ceremony → again.
    /// Also the single entry point for viewer events and the owner of the two "seats" (captain lives in MatchSim, sponsor here).
    /// </summary>
    public class TournamentSim
    {
        public readonly SimContext Ctx;
        readonly SimConfig cfg;

        public TournamentPhase Phase { get; private set; }
        public float PhaseLeft { get; private set; }
        public string CupName { get; private set; }
        public int TournamentNumber { get; private set; } = 1;
        public bool Paused { get; private set; }

        public readonly List<string> Bracket = new List<string>();
        public readonly HashSet<string> Guests = new HashSet<string>();
        public MatchSim Match { get; private set; }
        public int MatchIndex { get; private set; } = -1;
        public readonly string[] SemiWinners = new string[2];
        public readonly List<string> Results = new List<string>();
        public string Champion { get; private set; }
        public string LastChampion { get; private set; }

        /// <summary>Persistent cup counts per country (saved between streams by the game layer).</summary>
        public readonly Dictionary<string, int> Cups = new Dictionary<string, int>();
        public readonly Dictionary<string, long> RegistrationCoins = new Dictionary<string, long>();
        public readonly Dictionary<string, int> ScorerGoals = new Dictionary<string, int>();
        public string SponsorId { get; private set; }
        string nextCupOwner;

        /// <summary>Raised when a tournament is won (champion code). The game layer saves the cup table.</summary>
        public event Action<string> ChampionCrowned;

        public TournamentSim(SimConfig cfg, NameFilter filter = null, IDictionary<string, int> cups = null)
        {
            this.cfg = cfg;
            Ctx = new SimContext(cfg, filter);
            if (cups != null) foreach (var kv in cups) Cups[kv.Key] = kv.Value;
            StartRegistration();
        }

        // ------------------------------------------------------------------ queries

        public Fan Sponsor => SponsorId != null && Ctx.Fans.TryGet(SponsorId, out var f) ? f : null;

        public List<Fan> TopGifters(int n)
        {
            var list = new List<Fan>();
            foreach (var f in Ctx.Fans.All) if (f.tournamentCoins > 0) list.Add(f);
            list.Sort((a, b) => b.tournamentCoins.CompareTo(a.tournamentCoins));
            if (list.Count > n) list.RemoveRange(n, list.Count - n);
            return list;
        }

        /// <summary>Country ranking used by the draw: fans * weight + registration coins * weight.</summary>
        public List<KeyValuePair<string, double>> RegistrationRanking()
        {
            var counts = Ctx.Fans.FanCounts();
            var keys = new HashSet<string>(counts.Keys);
            foreach (var k in RegistrationCoins.Keys) keys.Add(k);
            var list = new List<KeyValuePair<string, double>>();
            foreach (var code in keys)
            {
                counts.TryGetValue(code, out int fans);
                RegistrationCoins.TryGetValue(code, out long coins);
                double score = fans * cfg.tournament.registrationFanWeight + coins * cfg.tournament.registrationCoinWeight;
                list.Add(new KeyValuePair<string, double>(code, score));
            }
            list.Sort((a, b) =>
            {
                int c = b.Value.CompareTo(a.Value);
                if (c != 0) return c;
                int ia = cfg.featuredCountries.IndexOf(a.Key), ib = cfg.featuredCountries.IndexOf(b.Key);
                if (ia < 0) ia = 999; if (ib < 0) ib = 999;
                return ia != ib ? ia.CompareTo(ib) : string.CompareOrdinal(a.Key, b.Key);
            });
            return list;
        }

        public KeyValuePair<string, int> TopScorer()
        {
            string best = null; int goals = 0;
            foreach (var kv in ScorerGoals) if (kv.Value > goals) { goals = kv.Value; best = kv.Key; }
            return new KeyValuePair<string, int>(best, goals);
        }

        // ------------------------------------------------------------------ input

        public void HandleEvent(GameEvent e)
        {
            if (e == null || string.IsNullOrEmpty(e.type)) return;
            var fan = Ctx.Fans.GetOrCreate(e);
            switch (e.type.ToLowerInvariant())
            {
                case "comment":
                    {
                        string code = CountryResolver.Resolve(e.text, cfg.featuredCountries);
                        if (code != null && code != fan.team && (cfg.teamJoin.lastFlagWins || fan.team == null))
                        {
                            fan.team = code;
                            Ctx.Emit(new SimEvent { kind = SimEventKind.FanJoined, teamCode = code, userId = fan.userId, nickname = fan.nickname, avatarUrl = fan.avatarUrl });
                            if (Match != null && Phase == TournamentPhase.Match)
                            {
                                int idx = Match.IndexOf(code);
                                if (idx >= 0 && Match.Teams[idx].isGuest) Match.MakeReal(idx);
                            }
                        }
                        if (fan.team != null)
                            Ctx.Emit(new SimEvent { kind = SimEventKind.FanCommented, teamCode = fan.team, userId = fan.userId, nickname = fan.nickname, avatarUrl = fan.avatarUrl });
                        if (Phase == TournamentPhase.Match) Match?.HandleComment(fan);
                        break;
                    }
                case "like":
                    if (Phase == TournamentPhase.Match) Match?.HandleLike(fan, Math.Max(1, e.count));
                    break;
                case "follow":
                case "share":
                    Ctx.Emit(new SimEvent { kind = SimEventKind.FanFollowed, teamCode = fan.team, userId = fan.userId, nickname = fan.nickname });
                    break;
                case "gift":
                    HandleGift(fan, e);
                    break;
            }
        }

        void HandleGift(Fan fan, GameEvent e)
        {
            int count = Math.Max(1, e.count);
            int coins = e.coins > 0 ? e.coins : Math.Max(0, e.unitCoins) * count;
            if (coins <= 0) return;
            int unit = e.unitCoins > 0 ? e.unitCoins : Math.Max(1, coins / count);
            int tier = cfg.TierOf(unit);
            fan.tournamentCoins += coins;
            fan.totalCoins += coins;
            if (tier >= 4) fan.vip = true;

            if (Phase == TournamentPhase.Match && Match != null && Match.Phase != MatchPhase.Ended)
            {
                Match.HandleGift(fan, coins, unit); // the gift's own moment (Yıldız/Efsane) is announced first
                UpdateSponsor();
                return;
            }
            UpdateSponsor();
            Ctx.RawPending += coins * cfg.power.gpPerCoin; // between matches the crowd still counts for ball speed
            if (Phase == TournamentPhase.Registration || Phase == TournamentPhase.Draw)
            {
                string c = fan.team ?? cfg.homeTeam;
                RegistrationCoins.TryGetValue(c, out long prev);
                RegistrationCoins[c] = prev + coins;
            }
            if (tier >= 4)
                Ctx.Announcer.Request(new SimEvent
                {
                    style = tier >= 5 ? "efsane" : "yildiz", teamCode = fan.team, nickname = fan.nickname, userId = fan.userId,
                    avatarUrl = fan.avatarUrl, tier = tier, text = tier >= 5 ? "EFSANE ANI" : "YILDIZ HAMLESİ"
                }, tier >= 5 ? 80 : 70);
        }

        void UpdateSponsor()
        {
            Fan best = null;
            foreach (var f in Ctx.Fans.All)
                if (f.tournamentCoins > 0 && (best == null || f.tournamentCoins > best.tournamentCoins)) best = f;
            if (best == null || best.userId == SponsorId) return;
            bool takeover = SponsorId != null;
            SponsorId = best.userId;
            Ctx.Emit(new SimEvent { kind = SimEventKind.SponsorChanged, userId = best.userId, nickname = best.nickname, avatarUrl = best.avatarUrl, teamCode = best.team });
            if (takeover)
                Ctx.Announcer.Request(new SimEvent
                {
                    style = "sponsor", teamCode = best.team, nickname = best.nickname, userId = best.userId, avatarUrl = best.avatarUrl, text = "YENİ ANA SPONSOR"
                }, 40, "sponsor");
        }

        // ------------------------------------------------------------------ streamer controls

        public void SetPaused(bool paused)
        {
            if (Paused == paused) return;
            Paused = paused;
            Ctx.Emit(new SimEvent { kind = SimEventKind.Paused, value = paused ? 1 : 0 });
        }

        public void SkipPhase()
        {
            switch (Phase)
            {
                case TournamentPhase.Registration: PhaseLeft = Math.Min(PhaseLeft, cfg.tournament.drawAnimSec); break;
                case TournamentPhase.Match: Match?.ForceEnd(); break;
                default: PhaseLeft = 0; break;
            }
        }

        public void KickUser(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return;
            if (Ctx.Fans.TryGet(userId, out var f)) { f.team = null; f.tournamentCoins = 0; }
            if (SponsorId == userId) { SponsorId = null; UpdateSponsor(); }
        }

        // ------------------------------------------------------------------ tick

        public void Tick(float dt)
        {
            if (Paused) return;
            Ctx.Tick(dt);
            PhaseLeft -= dt;
            switch (Phase)
            {
                case TournamentPhase.Registration:
                    if (PhaseLeft <= cfg.tournament.drawAnimSec) DoDraw();
                    break;
                case TournamentPhase.Draw:
                    if (PhaseLeft <= 0) StartMatch(0);
                    break;
                case TournamentPhase.Match:
                    Match.Tick(dt);
                    if (Match.Phase == MatchPhase.Ended) OnMatchEnded();
                    break;
                case TournamentPhase.Break:
                    if (PhaseLeft <= 0) StartMatch(MatchIndex + 1);
                    break;
                case TournamentPhase.Ceremony:
                    if (PhaseLeft <= 0) FinishTournament();
                    break;
            }
        }

        void SetPhase(TournamentPhase p, float seconds)
        {
            Phase = p;
            PhaseLeft = seconds;
            Ctx.Emit(new SimEvent { kind = SimEventKind.TournamentPhase, text = p.ToString(), duration = seconds, value = MatchIndex });
        }

        void StartRegistration()
        {
            CupName = string.IsNullOrEmpty(nextCupOwner) ? cfg.gameName : nextCupOwner + " Kupası";
            Bracket.Clear();
            Guests.Clear();
            Results.Clear();
            RegistrationCoins.Clear();
            SemiWinners[0] = SemiWinners[1] = null;
            Champion = null;
            Match = null;
            MatchIndex = -1;
            SetPhase(TournamentPhase.Registration, cfg.tournament.registrationSec);
            Ctx.Camera.Request("main");
        }

        void DoDraw()
        {
            var ranking = RegistrationRanking();
            var counts = Ctx.Fans.FanCounts();
            var real = new List<string>();
            foreach (var kv in ranking)
            {
                counts.TryGetValue(kv.Key, out int fans);
                if (fans >= cfg.teamJoin.newCountryMinFans) real.Add(kv.Key);
                if (real.Count == cfg.tournament.teams) break;
            }
            var seeded = new List<string>(real);
            // Empty seats are filled by "Misafir" AI teams (DESIGN §4).
            var fillOrder = new List<string>();
            if (!string.IsNullOrEmpty(cfg.homeTeam)) fillOrder.Add(cfg.homeTeam);
            fillOrder.AddRange(cfg.featuredCountries);
            foreach (var code in fillOrder)
            {
                if (seeded.Count >= cfg.tournament.teams) break;
                if (seeded.Contains(code)) continue;
                seeded.Add(code);
                Guests.Add(code);
            }
            while (seeded.Count < 4) { string g = "G" + seeded.Count; seeded.Add(g); Guests.Add(g); }
            // 1 v 4 and 2 v 3
            Bracket.Clear();
            Bracket.Add(seeded[0]); Bracket.Add(seeded[3]);
            Bracket.Add(seeded[1]); Bracket.Add(seeded[2]);
            Ctx.Emit(new SimEvent { kind = SimEventKind.Draw, text = string.Join(",", Bracket), duration = cfg.tournament.drawAnimSec });
            SetPhase(TournamentPhase.Draw, Math.Max(0.1f, PhaseLeft));
        }

        void StartMatch(int index)
        {
            MatchIndex = index;
            string a, b;
            if (index < 2) { a = Bracket[index * 2]; b = Bracket[index * 2 + 1]; }
            else { a = SemiWinners[0]; b = SemiWinners[1]; }
            if (b == cfg.homeTeam) { var t = a; a = b; b = t; } // home team plays at the bottom
            bool guestA = Guests.Contains(a) && Ctx.Fans.CountFans(a) == 0;
            bool guestB = Guests.Contains(b) && Ctx.Fans.CountFans(b) == 0;
            float dur = index == 2 ? cfg.tournament.finalSec : cfg.tournament.matchSec;
            Match = new MatchSim(Ctx, a, guestA, b, guestB, dur, index == 2, index);
            Match.GoalScored += (team, scorer) =>
            {
                if (scorer == null) return;
                ScorerGoals.TryGetValue(scorer.userId, out int g);
                ScorerGoals[scorer.userId] = g + 1;
            };
            Phase = TournamentPhase.Match;
            PhaseLeft = dur;
            Ctx.Emit(new SimEvent { kind = SimEventKind.MatchStarted, teamCode = a + "-" + b, value = index, text = index == 2 ? "FİNAL" : (index == 0 ? "YARI FİNAL 1" : "YARI FİNAL 2") });
            Ctx.Emit(new SimEvent { kind = SimEventKind.TournamentPhase, text = TournamentPhase.Match.ToString(), duration = dur, value = index });
        }

        void OnMatchEnded()
        {
            var m = Match;
            string winner = m.Teams[m.Winner].code;
            Results.Add($"{m.Teams[0].code} {m.Teams[0].score}-{m.Teams[1].score} {m.Teams[1].code}");
            if (MatchIndex < 2)
            {
                SemiWinners[MatchIndex] = winner;
                Ctx.Announcer.Request(new SimEvent { style = "mac", teamCode = winner, text = "MAÇ SONU", duration = 2.5f }, 95);
                SetPhase(TournamentPhase.Break, cfg.tournament.breakSec);
                return;
            }
            Champion = winner;
            LastChampion = winner;
            if (!m.Teams[m.Winner].isGuest)
            {
                Cups.TryGetValue(winner, out int c);
                Cups[winner] = c + 1;
            }
            ChampionCrowned?.Invoke(winner);
            var sponsor = Sponsor;
            Ctx.Emit(new SimEvent { kind = SimEventKind.Champion, teamCode = winner, nickname = sponsor?.nickname, avatarUrl = sponsor?.avatarUrl, userId = sponsor?.userId });
            Ctx.Announcer.Interrupt(new SimEvent { style = "sampiyon", teamCode = winner, text = "ŞAMPİYON", duration = 5f });
            Ctx.Camera.Request("ceremony", m.Winner, exempt: true, duration: cfg.tournament.ceremonySec);
            SetPhase(TournamentPhase.Ceremony, cfg.tournament.ceremonySec);
        }

        void FinishTournament()
        {
            nextCupOwner = Sponsor?.nickname;
            Ctx.Fans.ResetTournament();
            SponsorId = null;
            ScorerGoals.Clear();
            TournamentNumber++;
            StartRegistration();
        }
    }
}
