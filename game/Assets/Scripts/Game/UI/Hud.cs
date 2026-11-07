using System.Collections.Generic;
using System.Linq;
using DTK.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace DTK.Game
{
    /// <summary>
    /// Everything drawn on top of the 3D scene (DESIGN §3, §12). Built in code on a 1080x1920 reference canvas.
    /// Fixed parts never move; only the announcement slot, two name tags and the chance bar change.
    /// All important text sits above layout.tiktokCommentsTop because TikTok's own comments/gift banners cover the bottom.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        const float RefW = 1080f, RefH = 1920f;
        TournamentSim tour;
        MatchView view;
        CameraRig cam;
        RootConfig cfg;
        RectTransform root;
        LayoutConfig L => cfg.presentation.layout;

        // scoreboard
        RectTransform scorePanel;
        RawImage flagA, flagB;
        Text codeA, codeB, score, clock, guestA, guestB, lastGoal, penA, penB;
        Image clockPill, flameA, flameB, choreoA, choreoB, lastGoalBall;
        // sponsor
        Text sponsorText;
        RawImage sponsorFlag;
        // chance bar
        CanvasGroup chanceGroup;
        Image barAtk, barDef;
        RawImage chanceFlagAtk, chanceFlagDef;
        Text chanceTimer, chanceTitleAtk, chanceTitleDef, chanceNameAtk, chanceNameDef, chanceHeader;
        float chanceNameAtkUntil, chanceNameDefUntil;
        bool chanceVisible;
        // announcement
        CanvasGroup annGroup;
        RectTransform annPanel;
        Image annBg, annStripe;
        Text annTitle, annName, annInitials;
        RawImage annPhoto, annFlag;
        float annUntil, annStart;
        // name tags
        class Tag { public RectTransform rt; public Text text; public Image stripe; public CanvasGroup g; public float until; public int team; public bool keeper; public string style; }
        readonly List<Tag> tags = new List<Tag>();
        // phase card
        CanvasGroup cardGroup;
        RectTransform cardBody;
        string cardKey;
        float cardRefresh;
        // overlays
        CanvasGroup breakOverlay;
        Text fpsText;
        public bool Disconnected;

        public static Hud Build(Transform parent, TournamentSim t, MatchView v, CameraRig c, RootConfig cfg)
        {
            var go = new GameObject("HUD");
            go.transform.SetParent(parent, false);
            var hud = go.AddComponent<Hud>();
            hud.tour = t; hud.view = v; hud.cam = c; hud.cfg = cfg;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.matchWidthOrHeight = 0.5f;
            hud.root = (RectTransform)go.transform;
            hud.BuildAll();
            return hud;
        }

        float Y(float frac) => frac * RefH;

        void BuildAll()
        {
            BuildScoreboard();
            BuildGuide();
            BuildSponsor();
            BuildChanceBar();
            BuildNameTags();
            BuildCard();
            BuildAnnouncement();
            BuildOverlay();
        }

        // ------------------------------------------------------------------ scoreboard

        void BuildScoreboard()
        {
            float top = Y(L.scoreboardTop), h = Y(L.scoreboardHeight);
            var bg = Ui.Image(root, "Scoreboard", Ui.Rounded, new Color(0.05f, 0.07f, 0.13f, 0.86f));
            scorePanel = bg.rectTransform;
            Ui.PlaceTop(scorePanel, 0, top, 980, h);
            var p = scorePanel;
            flagA = Ui.Raw(p, "FlagA", null); Ui.Place(flagA.rectTransform, -420, 8, 100, 66);
            flagB = Ui.Raw(p, "FlagB", null); Ui.Place(flagB.rectTransform, 420, 8, 100, 66);
            codeA = Ui.Text(p, "CodeA", "", 58, Color.white, TextAnchor.MiddleLeft, Ui.FontBlack); Ui.Place(codeA.rectTransform, -250, 8, 160, 70);
            codeB = Ui.Text(p, "CodeB", "", 58, Color.white, TextAnchor.MiddleRight, Ui.FontBlack); Ui.Place(codeB.rectTransform, 250, 8, 160, 70);
            guestA = Ui.Text(p, "GuestA", "MİSAFİR", 22, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleLeft, Ui.FontSemi, false); Ui.Place(guestA.rectTransform, -250, -38, 160, 24);
            guestB = Ui.Text(p, "GuestB", "MİSAFİR", 22, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleRight, Ui.FontSemi, false); Ui.Place(guestB.rectTransform, 250, -38, 160, 24);
            score = Ui.Text(p, "Score", "0 - 0", 84, Color.white, TextAnchor.MiddleCenter, Ui.FontBlack); Ui.Place(score.rectTransform, 0, 12, 260, 90);
            penA = Ui.Text(p, "PenA", "", 30, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter, Ui.FontBlack); Ui.Place(penA.rectTransform, -150, -34, 60, 34);
            penB = Ui.Text(p, "PenB", "", 30, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter, Ui.FontBlack); Ui.Place(penB.rectTransform, 150, -34, 60, 34);
            clockPill = Ui.Image(p, "ClockPill", Ui.Rounded, new Color(1, 1, 1, 0.14f)); Ui.Place(clockPill.rectTransform, 0, -40, 150, 36);
            clock = Ui.Text(clockPill.transform, "Clock", "1'", 32, Color.white, TextAnchor.MiddleCenter, Ui.FontBold, false); Ui.Stretch(clock.rectTransform);
            flameA = Ui.Image(p, "FlameA", Ui.Flame, Color.white); Ui.Place(flameA.rectTransform, -150, 12, 40, 52);
            flameB = Ui.Image(p, "FlameB", Ui.Flame, Color.white); Ui.Place(flameB.rectTransform, 150, 12, 40, 52);
            // choreography progress (likes) as thin bars along the bottom edge
            var cbA = Ui.Image(p, "ChoreoBgA", Ui.Rounded, new Color(1, 1, 1, 0.12f)); Ui.Place(cbA.rectTransform, -300, -h / 2 + 8, 300, 8);
            var cbB = Ui.Image(p, "ChoreoBgB", Ui.Rounded, new Color(1, 1, 1, 0.12f)); Ui.Place(cbB.rectTransform, 300, -h / 2 + 8, 300, 8);
            choreoA = Ui.Image(cbA.transform, "Fill", Ui.Rounded, new Color(1f, 0.85f, 0.3f)); FillLeft(choreoA.rectTransform);
            choreoB = Ui.Image(cbB.transform, "Fill", Ui.Rounded, new Color(1f, 0.85f, 0.3f)); FillLeft(choreoB.rectTransform);
            // last goal line right under the scoreboard (DESIGN §12)
            lastGoalBall = Ui.Image(root, "LastGoalBall", Ui.BallIcon, Color.white);
            lastGoal = Ui.Text(root, "LastGoal", "", 30, Color.white, TextAnchor.MiddleLeft, Ui.FontBold);
            Ui.PlaceTop(lastGoal.rectTransform, 20, top + h + 2, 600, 34);
            Ui.PlaceTop(lastGoalBall.rectTransform, -10, top + h + 5, 28, 28);
        }

        static void FillLeft(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(0, 0);
        }

        // ------------------------------------------------------------------ gift guide strip

        void BuildGuide()
        {
            float top = Y(L.guideTop), h = Y(L.guideHeight);
            var strip = Ui.Rect(root, "GiftGuide");
            Ui.PlaceTop(strip, 0, top, 1020, h);
            Color[] tierColors =
            {
                new Color(0.22f, 0.62f, 0.38f), new Color(0.20f, 0.48f, 0.85f), new Color(0.95f, 0.50f, 0.12f),
                new Color(0.58f, 0.28f, 0.88f), new Color(0.85f, 0.66f, 0.12f)
            };
            float cellW = 158, gap = 8, joinW = 1020 - 5 * (cellW + gap);
            for (int i = 0; i < 5 && i < cfg.giftTiers.Count; i++)
            {
                var t = cfg.giftTiers[i];
                float x = -510 + i * (cellW + gap) + cellW / 2;
                var cell = Ui.Image(strip, "Tier" + (i + 1), Ui.Rounded, Ui.WithAlpha(tierColors[i] * 0.85f, 0.92f));
                Ui.Place(cell.rectTransform, x, 0, cellW, h);
                cell.rectTransform.anchorMin = cell.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                var coin = Ui.Image(cell.transform, "Coin", Ui.Coin, Color.white); Ui.Place(coin.rectTransform, -38, 18, 24, 24);
                string coins = t.minCoins >= 1000 ? (t.minCoins / 1000) + "B+" : t.minCoins + "+";
                var ct = Ui.Text(cell.transform, "Coins", coins, 26, Color.white, TextAnchor.MiddleLeft, Ui.FontBold, false); Ui.Place(ct.rectTransform, 22, 18, 100, 28);
                var word = Ui.Text(cell.transform, "Word", t.name, 34, Color.white, TextAnchor.MiddleCenter, Ui.FontBlack); Ui.Place(word.rectTransform, 0, -16, cellW, 38);
            }
            var join = Ui.Image(strip, "Join", Ui.Rounded, new Color(0.05f, 0.07f, 0.13f, 0.86f));
            Ui.Place(join.rectTransform, 510 - joinW / 2, 0, joinW, h);
            var jf = Ui.Raw(join.transform, "Flag", Flags.Get(cfg.homeTeam)); Ui.Place(jf.rectTransform, -joinW / 2 + 34, 0, 42, 28);
            var jt = Ui.Text(join.transform, "Text", "BAYRAK YAZ\nTAKIMA GİR", 25, Color.white, TextAnchor.MiddleLeft, Ui.FontBlack, false);
            jt.lineSpacing = 0.85f;
            Ui.Place(jt.rectTransform, 22, 0, joinW - 60, h);
        }

        // ------------------------------------------------------------------ sponsor line

        void BuildSponsor()
        {
            float top = Y(L.sponsorTop), h = Y(L.sponsorHeight);
            var bg = Ui.Image(root, "SponsorLine", Ui.Rounded, new Color(0.05f, 0.07f, 0.13f, 0.72f));
            Ui.PlaceTop(bg.rectTransform, 0, top, 1020, h + 6);
            sponsorText = Ui.Text(bg.transform, "Text", "", 30, Color.white, TextAnchor.MiddleCenter, Ui.FontBold, false);
            Ui.Stretch(sponsorText.rectTransform);
            sponsorFlag = Ui.Raw(bg.transform, "Flag", null);
            Ui.Place(sponsorFlag.rectTransform, 0, 0, 36, 24);
        }

        // ------------------------------------------------------------------ chance bar

        void BuildChanceBar()
        {
            var panel = Ui.Image(root, "ChanceBar", Ui.Rounded, new Color(0.04f, 0.06f, 0.12f, 0.78f));
            var rt = panel.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, -Y(L.chanceBarCenterTop));
            rt.sizeDelta = new Vector2(960, 178);
            chanceGroup = panel.gameObject.AddComponent<CanvasGroup>();
            chanceGroup.alpha = 0;
            chanceHeader = Ui.Text(rt, "Header", "POZİSYON", 30, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleCenter, Ui.FontBlack, false); Ui.Place(chanceHeader.rectTransform, 0, 66, 400, 34);
            chanceFlagAtk = Ui.Raw(rt, "FlagAtk", null); Ui.Place(chanceFlagAtk.rectTransform, -415, 30, 60, 40);
            chanceFlagDef = Ui.Raw(rt, "FlagDef", null); Ui.Place(chanceFlagDef.rectTransform, 415, 30, 60, 40);
            chanceTitleAtk = Ui.Text(rt, "Atk", "ŞUT", 44, Color.white, TextAnchor.MiddleLeft, Ui.FontBlack); Ui.Place(chanceTitleAtk.rectTransform, -235, 30, 260, 50);
            chanceTitleDef = Ui.Text(rt, "Def", "KURTARIŞ", 44, Color.white, TextAnchor.MiddleRight, Ui.FontBlack); Ui.Place(chanceTitleDef.rectTransform, 235, 30, 260, 50);
            var timerBg = Ui.Image(rt, "TimerBg", Ui.Circle, Color.white); Ui.Place(timerBg.rectTransform, 0, 22, 78, 78);
            chanceTimer = Ui.Text(timerBg.transform, "Timer", "6", 50, new Color(0.08f, 0.1f, 0.18f), TextAnchor.MiddleCenter, Ui.FontBlack, false); Ui.Stretch(chanceTimer.rectTransform);
            var track = Ui.Image(rt, "Track", Ui.Rounded, Color.white); Ui.Place(track.rectTransform, 0, -34, 900, 40);
            barDef = Ui.Image(track.transform, "Def", Ui.Rounded, Color.blue); Ui.Stretch(barDef.rectTransform);
            barAtk = Ui.Image(track.transform, "Atk", Ui.Rounded, Color.red); FillLeft(barAtk.rectTransform);
            chanceNameAtk = Ui.Text(rt, "NameAtk", "", 28, Color.white, TextAnchor.MiddleLeft, Ui.FontBold); Ui.Place(chanceNameAtk.rectTransform, -230, -72, 440, 32);
            chanceNameDef = Ui.Text(rt, "NameDef", "", 28, Color.white, TextAnchor.MiddleRight, Ui.FontBold); Ui.Place(chanceNameDef.rectTransform, 230, -72, 440, 32);
            var hintCoin = Ui.Image(rt, "HintCoin", Ui.Coin, Color.white); Ui.Place(hintCoin.rectTransform, -40, -72, 26, 26);
            var hint = Ui.Text(rt, "Hint", "= GÜÇ", 26, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleLeft, Ui.FontBold, false); Ui.Place(hint.rectTransform, 20, -72, 100, 30);
        }

        // ------------------------------------------------------------------ announcement slot

        void BuildAnnouncement()
        {
            annBg = Ui.Image(root, "Announcement", Ui.Rounded, new Color(0.05f, 0.07f, 0.13f, 0.92f));
            annPanel = annBg.rectTransform;
            annPanel.anchorMin = annPanel.anchorMax = new Vector2(0.5f, 1f);
            annPanel.pivot = new Vector2(0.5f, 0.5f);
            annPanel.anchoredPosition = new Vector2(0, -Y(L.announceCenter));
            annPanel.sizeDelta = new Vector2(900, 190);
            annGroup = annPanel.gameObject.AddComponent<CanvasGroup>();
            annGroup.alpha = 0;
            annStripe = Ui.Image(annPanel, "Stripe", Ui.Rounded, new Color(1f, 0.8f, 0.2f));
            Ui.Place(annStripe.rectTransform, 0, -88, 900, 12);
            annPhoto = Ui.Avatar(annPanel, "Avatar", 140, out annInitials);
            Ui.Place((RectTransform)annPhoto.transform.parent.parent, -350, 0, 140, 140);
            annTitle = Ui.Text(annPanel, "Title", "", 64, new Color(1f, 0.85f, 0.25f), TextAnchor.MiddleLeft, Ui.FontBlack); Ui.Place(annTitle.rectTransform, 90, 38, 640, 72);
            annName = Ui.Text(annPanel, "Name", "", 46, Color.white, TextAnchor.MiddleLeft, Ui.FontBold); Ui.Place(annName.rectTransform, 90, -34, 640, 56);
            annFlag = Ui.Raw(annPanel, "Flag", null); Ui.Place(annFlag.rectTransform, 380, 38, 66, 44);
        }

        // ------------------------------------------------------------------ name tags

        void BuildNameTags()
        {
            var layer = Ui.Rect(root, "NameTags");
            Ui.Stretch(layer);
            for (int i = 0; i < 3; i++)
            {
                var bg = Ui.Image(layer, "Tag" + i, Ui.Rounded, new Color(0.05f, 0.07f, 0.13f, 0.88f));
                var rt = bg.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.sizeDelta = new Vector2(220, 46);
                var stripe = Ui.Image(rt, "Stripe", Ui.Rounded, Color.red);
                stripe.rectTransform.anchorMin = new Vector2(0, 0); stripe.rectTransform.anchorMax = new Vector2(0, 1);
                stripe.rectTransform.pivot = new Vector2(0, 0.5f);
                stripe.rectTransform.offsetMin = new Vector2(0, 0); stripe.rectTransform.offsetMax = new Vector2(10, 0);
                var text = Ui.Text(rt, "Name", "", 32, Color.white, TextAnchor.MiddleCenter, Ui.FontBold, false);
                Ui.Stretch(text.rectTransform);
                text.rectTransform.offsetMin = new Vector2(16, 0);
                var g = bg.gameObject.AddComponent<CanvasGroup>();
                g.alpha = 0;
                tags.Add(new Tag { rt = rt, text = text, stripe = stripe, g = g });
            }
        }

        // ------------------------------------------------------------------ phase card + overlay

        void BuildCard()
        {
            var bg = Ui.Image(root, "PhaseCard", Ui.Rounded, new Color(0.04f, 0.06f, 0.12f, 0.9f));
            var rt = bg.rectTransform;
            float top = Y(L.cardTop), bottom = Y(L.cardBottom);
            Ui.PlaceTop(rt, 0, top, 960, bottom - top);
            cardGroup = bg.gameObject.AddComponent<CanvasGroup>();
            cardGroup.alpha = 0;
            cardBody = Ui.Rect(rt, "Body");
            Ui.Stretch(cardBody, 24);
        }

        void BuildOverlay()
        {
            var dim = Ui.Image(root, "TeknikMola", null, new Color(0, 0, 0, 0.7f));
            Ui.Stretch(dim.rectTransform);
            breakOverlay = dim.gameObject.AddComponent<CanvasGroup>();
            breakOverlay.alpha = 0;
            var t = Ui.Text(dim.transform, "Title", "TEKNİK MOLA", 110, Color.white, TextAnchor.MiddleCenter, Ui.FontBlack);
            Ui.Place(t.rectTransform, 0, 80, 1000, 140);
            var s = Ui.Text(dim.transform, "Sub", "Maç birazdan kaldığı yerden devam ediyor", 40, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleCenter, Ui.FontBold);
            Ui.Place(s.rectTransform, 0, -20, 1000, 60);
            fpsText = Ui.Text(root, "Fps", "", 24, new Color(1, 1, 1, 0.6f), TextAnchor.UpperLeft, Ui.FontSemi);
            Ui.PlaceTop(fpsText.rectTransform, -440, 6, 200, 30);
        }

        // ------------------------------------------------------------------ events

        public void OnEvent(SimEvent e)
        {
            switch (e.kind)
            {
                case SimEventKind.Announcement: ShowAnnouncement(e); break;
                case SimEventKind.NameTag: ShowTag(e); break;
                case SimEventKind.ChanceWindow:
                    chanceVisible = true;
                    chanceNameAtk.text = chanceNameDef.text = "";
                    break;
                case SimEventKind.ChanceResolved:
                case SimEventKind.MatchEnded:
                    chanceVisible = false;
                    break;
                case SimEventKind.Penalty:
                    break;
            }
        }

        void ShowAnnouncement(SimEvent e)
        {
            annStart = Time.time;
            annUntil = Time.time + Mathf.Max(1.5f, e.duration);
            Color accent = new Color(1f, 0.82f, 0.25f);
            string title = e.text ?? "";
            string name = e.nickname ?? "";
            Texture flag = Flags.Get(e.teamCode);
            switch (e.style)
            {
                case "gol":
                    title = "GOL!";
                    name = (e.value > 0 ? e.value + "'  " : "") + (string.IsNullOrEmpty(e.nickname) ? Flags.FullName(e.teamCode) : e.nickname);
                    accent = new Color(1f, 0.82f, 0.2f);
                    break;
                case "efsane": accent = new Color(1f, 0.75f, 0.1f); break;
                case "yildiz": accent = new Color(0.75f, 0.5f, 1f); break;
                case "kaptan": accent = Flags.TeamColors(e.teamCode)[0]; break;
                case "sponsor": accent = new Color(0.25f, 0.9f, 0.85f); break;
                case "sampiyon":
                    title = "ŞAMPİYON";
                    name = Flags.FullName(e.teamCode);
                    accent = new Color(1f, 0.82f, 0.2f);
                    break;
                case "mac":
                    title = "MAÇ SONU";
                    name = tour.Results.Count > 0 ? Pretty(tour.Results[tour.Results.Count - 1]) : "";
                    break;
                case "bilgi":
                    name = "";
                    break;
            }
            annTitle.text = title;
            annTitle.color = accent;
            annStripe.color = accent;
            annName.text = name;
            annFlag.texture = flag;
            annFlag.enabled = flag != null;
            bool hasPerson = !string.IsNullOrEmpty(e.nickname) && e.style != "sampiyon" && e.style != "mac" && e.style != "bilgi";
            annPhoto.transform.parent.parent.gameObject.SetActive(hasPerson);
            if (hasPerson) AvatarCache.Apply(annPhoto, annInitials, e.avatarUrl, e.nickname);
            float textX = hasPerson ? 90 : 0;
            var anchor = hasPerson ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            annTitle.alignment = annName.alignment = anchor;
            annTitle.rectTransform.anchoredPosition = new Vector2(textX, name.Length > 0 ? 38 : 0);
            annName.rectTransform.anchoredPosition = new Vector2(textX, -34);
            annFlag.rectTransform.anchoredPosition = new Vector2(hasPerson ? 30 + annTitle.preferredWidth + 50 : annTitle.preferredWidth / 2 + 60, annTitle.rectTransform.anchoredPosition.y);
        }

        static string Pretty(string result)
        {
            // "TR 2-1 VN" -> "TÜR 2 - 1 VIE"
            var parts = result.Split(' ');
            if (parts.Length < 3) return result;
            return Flags.Short(parts[0]) + "  " + parts[1].Replace("-", " - ") + "  " + Flags.Short(parts[2]);
        }

        void ShowTag(SimEvent e)
        {
            if (e.style == "bar")
            {
                var m = tour.Match;
                if (m?.Chance != null)
                {
                    bool atk = e.team == m.Chance.attacker;
                    if (atk) { chanceNameAtk.text = e.nickname; chanceNameAtkUntil = Time.time + 2f; }
                    else { chanceNameDef.text = e.nickname; chanceNameDefUntil = Time.time + 2f; }
                }
                return;
            }
            // reuse the oldest tag (the sim already limits to maxNameTags at once)
            Tag t = tags.OrderBy(x => x.until).First();
            t.until = Time.time + Mathf.Max(0.8f, e.duration);
            t.team = e.team;
            t.style = e.style;
            t.text.text = e.nickname;
            t.rt.sizeDelta = new Vector2(Mathf.Clamp(t.text.preferredWidth + 46, 120, 460), 46);
            var m2 = tour.Match;
            string code = m2 != null && e.team >= 0 && e.team <= 1 ? m2.Teams[e.team].code : null;
            t.stripe.color = code != null ? Flags.TeamColors(code)[0] : Color.white;
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            UpdateScoreboard();
            UpdateSponsor();
            UpdateChanceBar();
            UpdateAnnouncement();
            UpdateTags();
            UpdateCard();
            breakOverlay.alpha = Mathf.MoveTowards(breakOverlay.alpha, Disconnected ? 1 : 0, Time.deltaTime * 3f);
            if (cfg.presentation.showFps) fpsText.text = Mathf.RoundToInt(1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)) + " FPS";
            else fpsText.text = "";
        }

        void UpdateScoreboard()
        {
            var m = tour.Match;
            bool inMatch = m != null && (tour.Phase == TournamentPhase.Match || tour.Phase == TournamentPhase.Break || tour.Phase == TournamentPhase.Ceremony);
            scorePanel.gameObject.SetActive(inMatch);
            lastGoal.gameObject.SetActive(inMatch && m.LastGoalTeam >= 0);
            lastGoalBall.gameObject.SetActive(inMatch && m.LastGoalTeam >= 0);
            if (!inMatch) return;
            flagA.texture = Flags.Get(m.Teams[0].code);
            flagB.texture = Flags.Get(m.Teams[1].code);
            codeA.text = Flags.Short(m.Teams[0].code);
            codeB.text = Flags.Short(m.Teams[1].code);
            guestA.enabled = m.Teams[0].isGuest;
            guestB.enabled = m.Teams[1].isGuest;
            score.text = m.Teams[0].score + " - " + m.Teams[1].score;
            bool pens = m.IsPenalties || m.Teams[0].penaltyKicks.Count > 0;
            penA.text = pens ? "(" + m.Teams[0].PenaltyGoals + ")" : "";
            penB.text = pens ? "(" + m.Teams[1].PenaltyGoals + ")" : "";
            string ct;
            if (m.Phase == MatchPhase.Ended) ct = "MS";
            else if (m.IsPenalties) ct = "PENALTI";
            else if (m.IsGoldenGoal) ct = "ALTIN GOL " + Mathf.CeilToInt(m.GoldenLeft);
            else ct = m.Minute + "'";
            clock.text = ct;
            bool red = m.IsLastSeconds;
            clockPill.color = red ? new Color(0.9f, 0.15f, 0.15f, 0.95f) : new Color(1, 1, 1, 0.14f);
            clockPill.rectTransform.sizeDelta = new Vector2(Mathf.Max(110, clock.preferredWidth + 36), 36);
            flameA.enabled = m.Teams[0].flame;
            flameB.enabled = m.Teams[1].flame;
            float per = Mathf.Max(1, cfg.choreo.likesPerChoreo);
            choreoA.rectTransform.sizeDelta = new Vector2(300 * ((m.Teams[0].likes % per) / per), 0);
            choreoB.rectTransform.sizeDelta = new Vector2(300 * ((m.Teams[1].likes % per) / per), 0);
            if (m.LastGoalTeam >= 0)
            {
                string who = string.IsNullOrEmpty(m.LastGoalName) ? Flags.Short(m.Teams[m.LastGoalTeam].code) : m.LastGoalName;
                lastGoal.text = m.LastGoalMinute + "'  " + who;
                float w = lastGoal.preferredWidth;
                lastGoal.rectTransform.anchoredPosition = new Vector2(-w / 2 + 300 + 18, lastGoal.rectTransform.anchoredPosition.y);
                lastGoalBall.rectTransform.anchoredPosition = new Vector2(-w / 2 - 6, lastGoalBall.rectTransform.anchoredPosition.y);
            }
        }

        void UpdateSponsor()
        {
            var top = tour.TopGifters(2);
            if (top.Count == 0)
            {
                sponsorText.text = "ANA SPONSOR  ·  boş koltuk";
                sponsorFlag.enabled = false;
                return;
            }
            string s = "ANA SPONSOR  " + top[0].nickname + "   ";
            string rest = "";
            if (top.Count > 1) rest = " ·  2. " + top[1].nickname + " (" + Ui.Thousands(top[0].tournamentCoins - top[1].tournamentCoins) + " coin geride)";
            sponsorText.text = s + "     " + rest;
            var flag = Flags.Get(top[0].team);
            sponsorFlag.texture = flag;
            sponsorFlag.enabled = flag != null;
            // place the flag right after the sponsor's name
            float total = sponsorText.preferredWidth;
            sponsorText.text = s;
            float before = sponsorText.preferredWidth;
            sponsorText.text = s + "     " + rest;
            sponsorFlag.rectTransform.anchoredPosition = new Vector2(-total / 2 + before + 14, 0);
        }

        void UpdateChanceBar()
        {
            var m = tour.Match;
            bool show = chanceVisible && m != null && m.Chance != null && m.Chance.windowStarted && tour.Phase == TournamentPhase.Match;
            chanceGroup.alpha = Mathf.MoveTowards(chanceGroup.alpha, show ? 1 : 0, Time.deltaTime * 5f);
            if (!show) return;
            var c = m.Chance;
            string atk = m.Teams[c.attacker].code, def = m.Teams[c.Defender].code;
            float barY = c.attacker == 0 ? L.chanceBarCenterTop : L.chanceBarCenterBottom;
            var crt = (RectTransform)chanceGroup.transform;
            crt.anchoredPosition = new Vector2(0, -Y(barY));
            Flags.MatchKits(m.Teams[0].code, m.Teams[1].code, out var k0, out var k1);
            Color atkCol = (c.attacker == 0 ? k0 : k1)[0], defCol = (c.attacker == 0 ? k1 : k0)[0];
            barAtk.color = atkCol;
            barDef.color = defCol;
            float share = Mathf.Clamp01((float)c.ShotShare);
            float cur = barAtk.rectTransform.sizeDelta.x / 900f;
            barAtk.rectTransform.sizeDelta = new Vector2(900 * Mathf.Lerp(cur, share, 1 - Mathf.Exp(-Time.deltaTime * 10f)), 0);
            chanceFlagAtk.texture = Flags.Get(atk);
            chanceFlagDef.texture = Flags.Get(def);
            chanceTimer.text = Mathf.CeilToInt(Mathf.Max(0, c.timeLeft)).ToString();
            chanceHeader.text = c.source == "penalty" ? "PENALTI" + (c.opener != null ? " · " + c.opener.nickname : "") : "POZİSYON";
            if (Time.time > chanceNameAtkUntil) chanceNameAtk.text = "";
            if (Time.time > chanceNameDefUntil) chanceNameDef.text = "";
        }

        void UpdateAnnouncement()
        {
            bool on = Time.time < annUntil;
            // while the chance bar is up, the announcement sits right under it instead of the middle of the pitch
            var m = tour.Match;
            float center = L.announceCenter;
            if (chanceGroup.alpha > 0.5f && m?.Chance != null)
                center = (m.Chance.attacker == 0 ? L.chanceBarCenterTop : L.chanceBarCenterBottom) + 0.10f;
            annPanel.anchoredPosition = new Vector2(0, -Y(center));
            annGroup.alpha = Mathf.MoveTowards(annGroup.alpha, on ? 1 : 0, Time.deltaTime * 6f);
            float k = Mathf.Clamp01((Time.time - annStart) / 0.25f);
            float s = on ? Mathf.Lerp(0.82f, 1f, 1 - (1 - k) * (1 - k)) + Mathf.Sin(k * Mathf.PI) * 0.05f : 1f;
            annPanel.localScale = Vector3.one * s;
        }

        void UpdateTags()
        {
            var canvasScale = root.lossyScale.x;
            float minY = RefH * (1 - L.tiktokCommentsTop) + 10;          // never into the TikTok comments area
            float maxY = RefH * (1 - (L.sponsorTop + L.sponsorHeight)) - 60; // never over the fixed bars
            foreach (var t in tags)
            {
                bool on = Time.time < t.until && tour.Phase == TournamentPhase.Match;
                t.g.alpha = Mathf.MoveTowards(t.g.alpha, on ? 1 : 0, Time.deltaTime * 8f);
                if (t.g.alpha <= 0) continue;
                Vector3 world = view.TagAnchor(t.team);
                Vector3 sp = cam.Cam.WorldToScreenPoint(world);
                if (sp.z < 0) { t.g.alpha = 0; continue; }
                float x = sp.x / Screen.width * RefW;
                float y = sp.y / Screen.height * RefH;
                y = Mathf.Clamp(y, minY, maxY);
                x = Mathf.Clamp(x, t.rt.sizeDelta.x / 2 + 10, RefW - t.rt.sizeDelta.x / 2 - 10);
                t.rt.anchoredPosition = Vector2.Lerp(t.rt.anchoredPosition, new Vector2(x, y), t.g.alpha < 0.3f ? 1f : 1 - Mathf.Exp(-Time.deltaTime * 14f));
            }
        }

        // ------------------------------------------------------------------ phase cards

        void UpdateCard()
        {
            string key = null;
            switch (tour.Phase)
            {
                case TournamentPhase.Registration: key = "reg"; break;
                case TournamentPhase.Draw: key = "draw"; break;
                case TournamentPhase.Break: key = "break"; break;
                case TournamentPhase.Ceremony: key = tour.PhaseLeft < cfg.tournament.ceremonySec - 15f ? "ceremony" : null; break;
            }
            cardGroup.alpha = Mathf.MoveTowards(cardGroup.alpha, key != null ? 1 : 0, Time.deltaTime * 4f);
            if (key == null) { cardKey = null; return; }
            if (key != cardKey || Time.unscaledTime > cardRefresh)
            {
                cardKey = key;
                cardRefresh = Time.unscaledTime + 0.5f;
                RebuildCard(key);
            }
        }

        void Clear()
        {
            for (int i = cardBody.childCount - 1; i >= 0; i--) Destroy(cardBody.GetChild(i).gameObject);
        }

        Text CardText(string s, float y, int size, Color c, Font f = null, float x = 0, float w = 900, TextAnchor a = TextAnchor.MiddleCenter)
        {
            var t = Ui.Text(cardBody, "T", s, size, c, a, f ?? Ui.FontBlack);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            t.rectTransform.anchoredPosition = new Vector2(x, -y);
            t.rectTransform.sizeDelta = new Vector2(w, size + 10);
            return t;
        }

        RawImage CardFlag(string code, float x, float y, float w)
        {
            var r = Ui.Raw(cardBody, "F", Flags.Get(code));
            r.rectTransform.anchorMin = r.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            r.rectTransform.anchoredPosition = new Vector2(x, -y);
            r.rectTransform.sizeDelta = new Vector2(w, w * 0.66f);
            return r;
        }

        static string Clock(float sec)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(sec));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        void RebuildCard(string key)
        {
            Clear();
            var gold = new Color(1f, 0.82f, 0.25f);
            var dim = new Color(1, 1, 1, 0.7f);
            switch (key)
            {
                case "reg":
                    {
                        CardText(Ui.UpperTr(tour.CupName), 34, 52, gold);
                        CardText("BAŞLIYOR! BAYRAĞINI YAZ, TAKIMINA KATIL", 92, 36, Color.white, Ui.FontBold);
                        CardText(Clock(tour.PhaseLeft - cfg.tournament.drawAnimSec), 168, 86, Color.white);
                        var rank = tour.RegistrationRanking();
                        var counts = tour.Ctx.Fans.FanCounts();
                        float y = 250;
                        int shown = 0;
                        foreach (var kv in rank)
                        {
                            if (shown >= 5) break;
                            counts.TryGetValue(kv.Key, out int fans);
                            CardFlag(kv.Key, -330, y, 66);
                            CardText(Flags.FullName(kv.Key), y, 40, Color.white, Ui.FontBlack, -60, 400, TextAnchor.MiddleLeft);
                            CardText(fans + " taraftar", y, 34, dim, Ui.FontBold, 290, 300, TextAnchor.MiddleRight);
                            y += 58; shown++;
                        }
                        if (shown == 0) CardText("İlk bayrağı sen yaz!", 300, 40, dim, Ui.FontBold);
                        if (tour.Cups.Count > 0)
                        {
                            var cups = tour.Cups.OrderByDescending(k => k.Value).Take(4).ToList();
                            float cy = Mathf.Max(y + 20, 560);
                            CardText("KUPA TABLOSU", cy, 30, gold, Ui.FontBlack);
                            float x0 = -(cups.Count - 1) * 110;
                            for (int i = 0; i < cups.Count; i++)
                            {
                                CardFlag(cups[i].Key, x0 + i * 220 - 34, cy + 50, 50);
                                CardText("×" + cups[i].Value, cy + 50, 36, Color.white, Ui.FontBlack, x0 + i * 220 + 30, 80);
                            }
                        }
                        break;
                    }
                case "draw":
                    {
                        CardText("KURA", 40, 60, gold);
                        CardText(tour.CupName, 100, 34, dim, Ui.FontBold);
                        for (int i = 0; i < 2 && tour.Bracket.Count >= 4; i++)
                        {
                            float y = 210 + i * 160;
                            CardText(i == 0 ? "YARI FİNAL 1" : "YARI FİNAL 2", y - 50, 28, dim, Ui.FontBold);
                            CardFlag(tour.Bracket[i * 2], -250, y + 20, 110);
                            CardText(Flags.Short(tour.Bracket[i * 2]), y + 20, 54, Color.white, Ui.FontBlack, -110, 160);
                            CardText("-", y + 20, 54, dim);
                            CardText(Flags.Short(tour.Bracket[i * 2 + 1]), y + 20, 54, Color.white, Ui.FontBlack, 110, 160);
                            CardFlag(tour.Bracket[i * 2 + 1], 250, y + 20, 110);
                        }
                        break;
                    }
                case "break":
                    {
                        CardText("MAÇ SONU", 40, 54, gold);
                        if (tour.Results.Count > 0) CardText(Pretty(tour.Results[tour.Results.Count - 1]), 120, 64, Color.white);
                        string a = null, b = null;
                        if (tour.MatchIndex == 0 && tour.Bracket.Count >= 4) { a = tour.Bracket[2]; b = tour.Bracket[3]; }
                        else if (tour.MatchIndex == 1) { a = tour.SemiWinners[0]; b = tour.SemiWinners[1]; }
                        if (a != null && b != null)
                        {
                            CardText(tour.MatchIndex == 1 ? "SIRADAKİ: FİNAL" : "SIRADAKİ: YARI FİNAL 2", 240, 36, dim, Ui.FontBold);
                            CardFlag(a, -230, 340, 120);
                            CardText(Flags.Short(a), 340, 58, Color.white, Ui.FontBlack, -80, 160);
                            CardText(Flags.Short(b), 340, 58, Color.white, Ui.FontBlack, 80, 160);
                            CardFlag(b, 230, 340, 120);
                        }
                        CardText(Clock(tour.PhaseLeft), 470, 64, Color.white);
                        break;
                    }
                case "ceremony":
                    {
                        CardText("ŞAMPİYON", 40, 64, gold);
                        CardFlag(tour.Champion, 0, 150, 150);
                        CardText(Flags.FullName(tour.Champion), 250, 60, Color.white);
                        var sp = tour.Sponsor;
                        float y = 330;
                        if (sp != null) { CardText("ANA SPONSOR: " + sp.nickname, y, 38, Color.white, Ui.FontBold); y += 56; }
                        var ts = tour.TopScorer();
                        if (ts.Key != null && tour.Ctx.Fans.TryGet(ts.Key, out var scorer)) { CardText("GOL KRALI: " + scorer.nickname + " (" + ts.Value + ")", y, 38, Color.white, Ui.FontBold); y += 56; }
                        tour.Cups.TryGetValue(tour.Champion ?? "", out int cupCount);
                        if (cupCount > 0) { CardText("KUPA SAYISI: " + cupCount, y, 38, gold, Ui.FontBlack); y += 56; }
                        if (sp != null) CardText("SIRADAKİ: " + sp.nickname + " KUPASI", y + 10, 34, dim, Ui.FontBold); // names keep their own case
                        break;
                    }
            }
        }
    }
}
