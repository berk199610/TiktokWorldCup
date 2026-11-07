using System.Collections.Generic;

namespace DTK.Sim
{
    /// <summary>
    /// All tunable numbers of the game rules. Mirrors StreamingAssets/config.json (DESIGN §14).
    /// Field defaults are the design document's starting values; JSON overrides them.
    /// </summary>
    public class SimConfig
    {
        public string gameName = "Dünya Taraftar Kupası";
        public string homeTeam = "TR";
        public List<string> languages = new List<string> { "tr", "en" };
        public List<string> featuredCountries = new List<string> { "TR", "VN", "AZ", "ID", "BR", "AR", "MX", "MA", "PK", "PH" };
        public TournamentConfig tournament = new TournamentConfig();
        public TeamJoinConfig teamJoin = new TeamJoinConfig();
        public PowerConfig power = new PowerConfig();
        public ChoreoConfig choreo = new ChoreoConfig();
        public List<GiftTierConfig> giftTiers = GiftTierConfig.Defaults();
        public Dictionary<string, string> tierGuideGifts = new Dictionary<string, string>();
        public List<MoveRangeConfig> k4Moves = MoveRangeConfig.Defaults();
        public BallConfig ball = new BallConfig();
        public ChanceConfig chance = new ChanceConfig();
        public RubberBandConfig rubberBand = new RubberBandConfig();
        public QueueConfig queue = new QueueConfig();
        public SponsorConfig sponsor = new SponsorConfig();
        public ScreenConfig screen = new ScreenConfig();
        public CameraConfig camera = new CameraConfig();
        public StandsConfig stands = new StandsConfig();
        public ModerationConfig moderation = new ModerationConfig();
        public SimTimingConfig sim = new SimTimingConfig();

        /// <summary>Tier index 1..5 for a single gift's unit coin value (0 when below K1).</summary>
        public int TierOf(int unitCoins)
        {
            for (int i = giftTiers.Count - 1; i >= 0; i--)
            {
                var t = giftTiers[i];
                if (unitCoins >= t.minCoins && (t.maxCoins == null || unitCoins <= t.maxCoins.Value)) return i + 1;
            }
            return unitCoins > 0 ? 1 : 0;
        }

        public GiftTierConfig Tier(int tier)
        {
            if (tier < 1) tier = 1;
            if (tier > giftTiers.Count) tier = giftTiers.Count;
            return giftTiers[tier - 1];
        }

        public string MoveFor(int tier, int unitCoins)
        {
            if (tier >= 5) return "efsane";
            if (tier == 4)
            {
                foreach (var m in k4Moves)
                    if (unitCoins >= m.minCoins && (m.maxCoins == null || unitCoins <= m.maxCoins.Value)) return m.move;
                return "frikik";
            }
            return tier == 3 ? "sut" : (tier == 2 ? "calim" : "pas");
        }
    }

    public class TournamentConfig
    {
        public int teams = 4;
        public float registrationSec = 60, drawAnimSec = 5, matchSec = 180, finalSec = 180, breakSec = 15, ceremonySec = 30, goldenGoalSec = 60;
        public int penaltiesPerTeam = 3, mercyGoals = 5, gameMinutesPerMatch = 90;
        public float goalCelebrationSec = 6f, kickoffSec = 1.2f;
        /// <summary>After this many sudden-death rounds without a winner the side with more power wins (silent streams).</summary>
        public int maxSuddenDeathRounds = 5;
        /// <summary>Registration ranking score = fans * fanWeight + coins * coinWeight.</summary>
        public float registrationFanWeight = 10f, registrationCoinWeight = 1f;
    }

    public class TeamJoinConfig
    {
        public bool lastFlagWins = true;
        public int newCountryMinFans = 3, cityModeMinCountries = 2, cityModeMinFans = 3, frontRowSeats = 10;
    }

    public class PowerConfig
    {
        public float gpPerCoin = 10f, comment = 2f, commentCooldownSec = 3f, like = 0f, follow = 0f, share = 0f;
    }

    public class ChoreoConfig
    {
        public int likesPerChoreo = 1000;
        public float choreoSec = 5f, boostSec = 10f, boostMult = 1.25f;
    }

    public class GiftTierConfig
    {
        public string id;
        public string name;
        public int minCoins;
        public int? maxCoins;
        public float nameTagSec = 2f;
        public bool opensChance;
        public float chanceSec = 10f;
        public float cinematicSec;

        public static List<GiftTierConfig> Defaults()
        {
            return new List<GiftTierConfig>
            {
                new GiftTierConfig { id = "K1", name = "PAS", minCoins = 1, maxCoins = 9, nameTagSec = 1.5f },
                new GiftTierConfig { id = "K2", name = "ÇALIM", minCoins = 10, maxCoins = 99, nameTagSec = 2f },
                new GiftTierConfig { id = "K3", name = "ŞUT", minCoins = 100, maxCoins = 499, opensChance = true, chanceSec = 8f },
                new GiftTierConfig { id = "K4", name = "YILDIZ", minCoins = 500, maxCoins = 4999, opensChance = true, chanceSec = 10f, cinematicSec = 4f },
                new GiftTierConfig { id = "K5", name = "EFSANE", minCoins = 5000, maxCoins = null, opensChance = true, chanceSec = 10f, cinematicSec = 6f },
            };
        }
    }

    public class MoveRangeConfig
    {
        public int minCoins;
        public int? maxCoins;
        public string move;

        public static List<MoveRangeConfig> Defaults()
        {
            return new List<MoveRangeConfig>
            {
                new MoveRangeConfig { minCoins = 500, maxCoins = 1499, move = "frikik" },
                new MoveRangeConfig { minCoins = 1500, maxCoins = 2999, move = "uzaktan_fuze" },
                new MoveRangeConfig { minCoins = 3000, maxCoins = 4999, move = "rovasata" },
            };
        }
    }

    public class BallConfig
    {
        public float targetUnitsPerSec = 4f, minAvgGpPerSec = 50f, kMin = 0.002f, kMax = 0.05f;
        public float autoscaleWindowSec = 60f, maxDisplaySpeed = 25f, idleChanceEverySec = 45f;
        /// <summary>Idle push wobble (0 = straight push). Makes the self-playing ball drift back and forth.</summary>
        public float idleWobble = 1.3f, idleWobblePeriodSec = 7f;
        /// <summary>Display speed used for the 1-second fast attack before a gift chance.</summary>
        public float fastAttackSpeed = 160f;
    }

    public class ChanceConfig
    {
        public float zoneStart = 85f, windowSec = 6f, attackBaseGP = 400f, keeperBaseGP = 300f;
        public float postMarginPct = 10f, saveClearTo = 0f, postBounceTo = 65f, cooldownSec = 3f;
        public float penaltyBaseGP = 300f, penaltyWindowSec = 5f;
        /// <summary>Seconds of the run-up before a K3 window (DESIGN §6.2: 1 sn hızlı atak).</summary>
        public float fastAttackSec = 1f;
        /// <summary>Seconds the result animation (goal shot / save / post) takes before play continues.</summary>
        public float resolveSec = 1.5f;
        /// <summary>Where the ball waits during a chance (units from centre).</summary>
        public float ballSpot = 90f;
        public float penaltyRunUpSec = 1.2f;
    }

    public class RubberBandConfig
    {
        public float behindBy2Mult = 1.3f, last30Mult = 1.5f, lastSec = 30f;
    }

    public class QueueConfig { public int maxCinematics = 3; }

    public class SponsorConfig
    {
        public int ledSlots = 8;
        public float ledRotateSec = 5f, announceMergeSec = 10f;
    }

    public class ScreenConfig
    {
        public int maxAnnouncements = 1, maxNameTags = 2;
        public float announcementSec = 2.5f;
        public int announcementQueueMax = 4;
    }

    public class CameraConfig
    {
        public float minSecBetweenMoves = 10f, chanceZoomSec = 1f, goalReplaySec = 3f, blendSec = 0.6f;
    }

    public class StandsConfig { public int maxAvatarsPerTeam = 300; }

    public class ModerationConfig
    {
        public string bannedWordsFile = "banned_words.txt";
        public bool showCommentText = false, tts = false;
        public int maxNameLength = 16;
    }

    public class SimTimingConfig
    {
        public int tickHz = 10;
        public int seed = 0;
    }
}
