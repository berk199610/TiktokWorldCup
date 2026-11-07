using System;
using System.Collections.Generic;
using System.IO;
using DTK.Sim;
using Newtonsoft.Json;
using UnityEngine;

namespace DTK.Game
{
    /// <summary>config.json root: the rule numbers (SimConfig) plus everything that is only about the picture.</summary>
    public class RootConfig : SimConfig
    {
        public PresentationConfig presentation = new PresentationConfig();
    }

    public class PresentationConfig
    {
        public WindowConfig window = new WindowConfig();
        public BridgeConfig bridge = new BridgeConfig();
        public LayoutConfig layout = new LayoutConfig();
        public CameraShotsConfig camera = new CameraShotsConfig();
        public Dictionary<string, string> teamNames = new Dictionary<string, string>();
        public Dictionary<string, string[]> teamColors = new Dictionary<string, string[]>();
        public int targetFrameRate = 60;
        public bool showFps = false;
    }

    public class WindowConfig
    {
        /// <summary>Game window size. 608x1080 fits a 1080p screen and is captured by TikTok LIVE Studio.</summary>
        public int width = 608, height = 1080;
        /// <summary>-1 = stick to the right edge of the screen.</summary>
        public int x = -1, y = 0;
        public bool applyOnStart = true;
    }

    public class BridgeConfig
    {
        public string url = "ws://127.0.0.1:8787/game";
        public float reconnectSec = 2f;
        /// <summary>Show "TEKNİK MOLA" and pause the match while the bridge is gone (DESIGN §15).</summary>
        public bool pauseWhenDisconnected = true;
        public float disconnectGraceSec = 3f;
    }

    /// <summary>Screen layout in fractions of the screen height from the top (DESIGN §3). Tune after a real phone test.</summary>
    public class LayoutConfig
    {
        public float scoreboardTop = 0.100f, scoreboardHeight = 0.064f;
        public float guideTop = 0.170f, guideHeight = 0.046f;
        public float sponsorTop = 0.220f, sponsorHeight = 0.024f;
        /// <summary>The bar never covers the attacked goal: lower when the top goal is attacked, higher for the bottom goal.</summary>
        public float chanceBarCenterTop = 0.47f, chanceBarCenterBottom = 0.36f;
        public float announceCenter = 0.505f;
        public float cardTop = 0.27f, cardBottom = 0.60f;
        /// <summary>Everything below this line is covered by TikTok comments; only decoration there.</summary>
        public float tiktokCommentsTop = 0.64f;
    }

    public class CameraShotsConfig
    {
        /// <summary>Portrait screens are narrow: a far, slightly telephoto camera shows the whole pitch without distortion.</summary>
        public float fov = 24f, closeFov = 40f;
        public float mainHeight = 77.2f, mainBack = 114.4f, mainLookZ = -7f, mainLookY = 0f;
        /// <summary>Chance push-in: narrower FOV and the look point moved towards the attacked goal (metres on z).</summary>
        public float chanceFov = 19f, chanceLookTopZ = 4f, chanceLookBottomZ = -12f;
        public float cinematicOrbitRadius = 9f, cinematicHeight = 3.2f;
    }

    /// <summary>Loads StreamingAssets/config.json and hot-reloads it when the file changes.</summary>
    public class ConfigLoader
    {
        public RootConfig Config { get; private set; } = new RootConfig();
        public string Path { get; }
        DateTime lastWrite;
        float nextCheck;

        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            NullValueHandling = NullValueHandling.Include,
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };

        public ConfigLoader()
        {
            Path = System.IO.Path.Combine(Application.streamingAssetsPath, "config.json");
            Load(initial: true);
        }

        public bool Load(bool initial = false)
        {
            try
            {
                if (!File.Exists(Path)) { Debug.LogWarning("config.json bulunamadı, varsayılanlar kullanılıyor: " + Path); return false; }
                string json = File.ReadAllText(Path);
                JsonConvert.PopulateObject(json, Config, Settings);
                lastWrite = File.GetLastWriteTimeUtc(Path);
                if (!initial) Debug.Log("config.json yeniden yüklendi");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("config.json okunamadı: " + e.Message);
                return false;
            }
        }

        /// <summary>Returns true when the file changed and was reloaded.</summary>
        public bool Poll()
        {
            if (Time.unscaledTime < nextCheck) return false;
            nextCheck = Time.unscaledTime + 1.5f;
            try
            {
                if (File.Exists(Path) && File.GetLastWriteTimeUtc(Path) != lastWrite) return Load();
            }
            catch { }
            return false;
        }

        public IEnumerable<string> BannedWords()
        {
            string p = System.IO.Path.Combine(Application.streamingAssetsPath, Config.moderation.bannedWordsFile ?? "banned_words.txt");
            if (!File.Exists(p)) return Array.Empty<string>();
            return File.ReadAllLines(p);
        }
    }

    /// <summary>Country cup table that survives restarts (DESIGN §9 "Kupa tablosu").</summary>
    public static class CupStore
    {
        static string FilePath => System.IO.Path.Combine(Application.persistentDataPath, "cups.json");

        public static Dictionary<string, int> Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonConvert.DeserializeObject<Dictionary<string, int>>(File.ReadAllText(FilePath)) ?? new Dictionary<string, int>();
            }
            catch (Exception e) { Debug.LogWarning("cups.json okunamadı: " + e.Message); }
            return new Dictionary<string, int>();
        }

        public static void Save(Dictionary<string, int> cups)
        {
            try { File.WriteAllText(FilePath, JsonConvert.SerializeObject(cups, Formatting.Indented)); }
            catch (Exception e) { Debug.LogWarning("cups.json yazılamadı: " + e.Message); }
        }
    }
}
