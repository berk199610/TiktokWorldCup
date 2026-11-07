# CLAUDE.md — Dünya Taraftar Kupası (TikTok LIVE football)

The user writes Turkish and does not code: answer in Turkish, plain language, and do the technical work yourself.

## What this is
A self-playing, gift-driven 3D football tournament for the user's TikTok LIVE. Viewers join a country by typing a flag;
gifts push the ball / open chances; names appear on the pitch. Source of truth for rules:
`C:\Users\admin\OneDrive\Desktop\TiktokLive\Bayrak Kupası — TikTok LIVE Futbol Oyunu Tasarım Dokümanı.pdf` (§ numbers below refer to it).
Do not change a game rule without asking; numbers live in `game/Assets/StreamingAssets/config.json`.

## Layout
- `game/` Unity 6.3 LTS (6000.3.28f1), URP. Everything is built from code; the only scene object is `Game` with `GameBootstrap`.
  - `Assets/Scripts/Sim/` pure C# rules (asmdef `DTK.Sim`, noEngineReferences). TournamentSim → MatchSim. Emits `SimEvent`s.
  - `Assets/Scripts/Game/` presentation (`DTK.Game`): Core (config, bridge client, bootstrap), Visual (stadium, actors, camera), UI (uGUI HUD).
  - `Assets/Scripts/Editor/` `DTK > Kurulum` and `DTK > Windows için derle` (also callable in batch mode).
  - `Assets/Tests/EditMode/` NUnit tests = DESIGN §16 scenarios. Keep them green.
  - `Assets/Shaders/` DTK/Crowd (one-mesh crowd, jump + tifo flag), DTK/VertexColorLit (ball).
- `bridge/` Node (ESM, only dep `ws`). `src/server.js` (ws `/game`, ws `/panel`, http panel + `/api/sim`, `/api/cmd`),
  `src/simulator.js` (crowd bot, same model as C# TrafficBot), `src/tiktok.js` (Aşama 4 stub — read the current
  tiktok-live-connector docs before wiring; do not guess field names).
- `tools/node/` portable Node 24 (gitignored). `_downloads/` raw asset packs (gitignored). `docs/` notes.
- `BASLAT.bat` starts bridge + panel + game.

## Commands (Git Bash)
```bash
UNITY="/c/Program Files/Unity/Hub/Editor/6000.3.28f1/Editor/Unity.exe"
# tests (editor must be closed on this project)
"$UNITY" -batchmode -projectPath "C:/Projeler/TiktokWorldCup/game" -runTests -testPlatform EditMode -testResults "C:/Projeler/TiktokWorldCup/_test-output/editmode.xml" -logFile "C:/Projeler/TiktokWorldCup/_test-output/tests.log"
# setup + Windows build -> Builds/Windows/DunyaTaraftarKupasi.exe
"$UNITY" -batchmode -quit -projectPath "C:/Projeler/TiktokWorldCup/game" -executeMethod DTK.EditorTools.BuildScript.SetupAndBuild -logFile "C:/Projeler/TiktokWorldCup/_test-output/build.log"
# bridge
tools/node/node.exe bridge/src/server.js
# drive the running game without a browser
curl -s -X POST localhost:8787/api/sim -d '{"action":"bot","viewers":200}'
curl -s -X POST localhost:8787/api/cmd -d '{"cmd":"screenshot","arg":"C:/Projeler/TiktokWorldCup/_test-output/shot.png"}'
```
If the user has the project open in the Unity editor, batch mode fails (project lock): read
`%LOCALAPPDATA%\Unity\Editor\Editor.log` for compile errors instead, or ask them to close Unity.

## Conventions
- C# 9 max (no file-scoped namespaces, no records). Sim code must not reference UnityEngine.
- Runtime materials: copy the base materials in `Resources/Materials` (created by setup); never toggle shader keywords at runtime.
- All tunables in config.json (hot-reloaded). Screen layout fractions in `presentation.layout`.
- Public repo: only CC0 / public-domain / OFL assets. Restricted-license files go to `game/Assets/_Private/` (gitignored). See docs/ASSETS.md.
