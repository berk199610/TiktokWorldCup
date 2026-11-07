using System;
using System.Collections.Generic;
using System.IO;
using DTK.Sim;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DTK.Game
{
    /// <summary>
    /// The only component in the scene. Builds everything in code, runs the simulation at a fixed 10 Hz,
    /// feeds it with bridge events and hands its output events to the picture (DESIGN §13 layers: Sim → Render → UI).
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        ConfigLoader config;
        RootConfig Cfg => config.Config;
        TournamentSim tour;
        MatchView view;
        CameraRig rig;
        Hud hud;
        BridgeClient bridge;
        float acc, stateTimer, lastConnectedAt;
        bool pausedByDisconnect, pausedByStreamer;
        readonly List<GameEvent> pendingEvents = new List<GameEvent>();
        int eventsTotal;
        string screenshotDir;

        void Awake()
        {
            Application.runInBackground = true; // TikTok LIVE Studio has the focus while we stream
            config = new ConfigLoader();
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Cfg.presentation.targetFrameRate;
            ApplyWindow();
            Flags.Init(Cfg);
            // the main camera stands far away (narrow portrait screen), so shadows must reach that far
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp)
                urp.shadowDistance = 260f;

            var filter = new NameFilter(config.BannedWords());
            tour = new TournamentSim(Cfg, filter, CupStore.Load());
            tour.ChampionCrowned += _ => CupStore.Save(tour.Cups);

            gameObject.AddComponent<AvatarCache>();
            view = MatchView.Build(transform, tour);
            rig = CameraRig.Build(transform, view, Cfg);
            hud = Hud.Build(transform, tour, view, rig, Cfg);
            bridge = new BridgeClient(Cfg.presentation.bridge.url, Cfg.presentation.bridge.reconnectSec);
            lastConnectedAt = Time.unscaledTime;
            screenshotDir = Path.Combine(Application.persistentDataPath, "screenshots");
            Debug.Log("Dünya Taraftar Kupası hazır. Köprü: " + Cfg.presentation.bridge.url);
        }

        void ApplyWindow()
        {
            if (Application.isEditor || !Cfg.presentation.window.applyOnStart) return;
            var w = Cfg.presentation.window;
            Screen.SetResolution(w.width, w.height, FullScreenMode.Windowed);
            try
            {
                var info = Screen.mainWindowDisplayInfo;
                int x = w.x >= 0 ? w.x : Mathf.Max(0, info.width - w.width);
                Screen.MoveMainWindowTo(info, new Vector2Int(x, w.y));
            }
            catch (Exception e) { Debug.LogWarning("Pencere taşınamadı: " + e.Message); }
        }

        void Update()
        {
            if (config.Poll())
            {
                Flags.Init(Cfg);
                Application.targetFrameRate = Cfg.presentation.targetFrameRate;
            }
            ReadBridge();
            UpdateConnectionState();

            float dt = 1f / Mathf.Max(1, Cfg.sim.tickHz);
            acc += Mathf.Min(Time.unscaledDeltaTime, 0.25f);
            while (acc >= dt)
            {
                acc -= dt;
                foreach (var e in pendingEvents) tour.HandleEvent(e);
                pendingEvents.Clear();
                tour.Tick(dt);
                Dispatch();
            }
            // events that arrive while paused still update the fan list
            if (tour.Paused && pendingEvents.Count > 0)
            {
                foreach (var e in pendingEvents) tour.HandleEvent(e);
                pendingEvents.Clear();
                Dispatch();
            }

            stateTimer -= Time.unscaledDeltaTime;
            if (stateTimer <= 0)
            {
                stateTimer = 0.5f;
                SendState();
            }
        }

        void Dispatch()
        {
            var outList = tour.Ctx.Out;
            if (outList.Count == 0) return;
            foreach (var e in outList)
            {
                view.OnEvent(e);
                rig.OnEvent(e);
                hud.OnEvent(e);
                if (e.kind == SimEventKind.Announcement && (e.style == "yildiz" || e.style == "efsane"))
                    bridge.Send(JsonConvert.SerializeObject(new { t = "thanks", nickname = e.nickname, tier = e.tier, team = e.teamCode }));
            }
            outList.Clear();
        }

        void UpdateConnectionState()
        {
            var b = Cfg.presentation.bridge;
            if (bridge.Connected) lastConnectedAt = Time.unscaledTime;
            bool gone = !bridge.Connected && Time.unscaledTime - lastConnectedAt > b.disconnectGraceSec;
            hud.Disconnected = gone && b.pauseWhenDisconnected;
            bool shouldPause = pausedByStreamer || (gone && b.pauseWhenDisconnected);
            pausedByDisconnect = gone;
            if (shouldPause != tour.Paused) tour.SetPaused(shouldPause);
        }

        void ReadBridge()
        {
            int guard = 0;
            while (guard++ < 500 && bridge.TryDequeue(out var msg))
            {
                try { HandleMessage(JObject.Parse(msg)); }
                catch (Exception e) { Debug.LogWarning("Köprü mesajı okunamadı: " + e.Message); }
            }
        }

        void HandleMessage(JObject o)
        {
            string t = (string)o["t"];
            switch (t)
            {
                case "ev":
                    AddEvent(o["e"]);
                    break;
                case "evs":
                    if (o["list"] is JArray arr) foreach (var item in arr) AddEvent(item);
                    break;
                case "cmd":
                    Command((string)o["cmd"], (string)o["arg"]);
                    break;
            }
        }

        void AddEvent(JToken tok)
        {
            if (tok == null) return;
            var e = tok.ToObject<GameEvent>();
            if (e == null) return;
            pendingEvents.Add(e);
            eventsTotal++;
        }

        void Command(string cmd, string arg)
        {
            switch (cmd)
            {
                case "pause": pausedByStreamer = true; break;
                case "resume": pausedByStreamer = false; break;
                case "skip": tour.SkipPhase(); break;
                case "kick": tour.KickUser(arg); break;
                case "reload": config.Load(); Flags.Init(Cfg); break;
                case "fps": Cfg.presentation.showFps = !Cfg.presentation.showFps; break;
                case "screenshot":
                    {
                        Directory.CreateDirectory(screenshotDir);
                        string file = string.IsNullOrEmpty(arg) ? Path.Combine(screenshotDir, DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png") : arg;
                        ScreenCapture.CaptureScreenshot(file);
                        bridge.Send(JsonConvert.SerializeObject(new { t = "screenshot", path = file }));
                        break;
                    }
                case "quit": Application.Quit(); break;
            }
        }

        void SendState()
        {
            var m = tour.Match;
            var top = tour.TopGifters(8);
            var st = new Dictionary<string, object>
            {
                ["t"] = "state",
                ["phase"] = tour.Phase.ToString(),
                ["phaseLeft"] = Math.Round(tour.PhaseLeft, 1),
                ["cup"] = tour.CupName,
                ["tournament"] = tour.TournamentNumber,
                ["paused"] = tour.Paused,
                ["fps"] = Mathf.RoundToInt(1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)),
                ["fans"] = tour.Ctx.Fans.Count,
                ["events"] = eventsTotal,
                ["avgGp"] = Math.Round(tour.Ctx.AvgGpPerSec, 1),
                ["sponsor"] = tour.Sponsor?.nickname,
                ["top"] = top.ConvertAll(f => new { n = f.nickname, c = f.tournamentCoins, team = f.team }),
                ["cups"] = tour.Cups,
            };
            if (m != null)
            {
                st["match"] = new
                {
                    a = m.Teams[0].code, b = m.Teams[1].code, sa = m.Teams[0].score, sb = m.Teams[1].score,
                    minute = m.Minute, phase = m.Phase.ToString(), golden = m.IsGoldenGoal, pens = m.IsPenalties,
                    ball = Math.Round(m.BallPos, 1), k = Math.Round(m.K, 4),
                    chance = m.Chance == null ? null : new { atk = m.Chance.attacker, shot = Math.Round(m.Chance.shot), save = Math.Round(m.Chance.save), left = Math.Round(m.Chance.timeLeft, 1) },
                    capA = m.Teams[0].captainName, capB = m.Teams[1].captainName,
                };
            }
            // LED boards: top gifters with their flag code (DESIGN §9)
            var lines = new List<string>();
            foreach (var f in top) lines.Add(f.nickname + "  " + Flags.Short(f.team));
            if (lines.Count == 0) lines.Add(Ui.UpperTr(Cfg.gameName));
            foreach (var led in view.Stadium.Leds) led.SetLines(lines);
            bridge.Send(JsonConvert.SerializeObject(st));
        }

        void OnDestroy()
        {
            bridge?.Dispose();
        }
    }
}
