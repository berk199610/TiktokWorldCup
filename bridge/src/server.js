// Dünya Taraftar Kupası — köprü sunucusu.
//   ws://127.0.0.1:8787/game   -> Unity oyunu bağlanır (olaylar buradan gider, durum buradan gelir)
//   ws://127.0.0.1:8787/panel  -> yayıncı / simülatör paneli
//   http://127.0.0.1:8787/     -> paneli tarayıcıda aç (telefondan da açılır: http://<bilgisayar-ip>:8787)
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { fileURLToPath } from 'node:url';
import { WebSocketServer } from 'ws';
import { Simulator } from './simulator.js';
import { TikTokProvider } from './tiktok.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '..');
const cfg = JSON.parse(fs.readFileSync(path.join(root, 'config.json'), 'utf8'));
const port = Number(process.env.PORT || cfg.port || 8787);

const games = new Set();
const panels = new Set();
let lastState = null;
const thanks = [];        // recent K4/K5 senders for the streamer to thank by voice (DESIGN §12)
const recentShots = [];
let eventsThisSecond = 0, eventsPerSecond = 0, eventsTotal = 0;
const log = (...a) => console.log(new Date().toLocaleTimeString('tr-TR'), ...a);

function send(set, obj) {
  const s = JSON.stringify(obj);
  for (const ws of set) if (ws.readyState === 1) ws.send(s);
}

// Every viewer event goes through here, whatever the source (simulator or TikTok).
const queue = [];
function emitEvent(e) {
  queue.push(e);
  eventsThisSecond++;
  eventsTotal++;
  if (cfg.logEvents) log('olay', e.type, e.nickname, e.coins || e.count || e.text || '');
  if (e.type === 'gift' && (e.unitCoins || e.coins) >= 100) send(panels, { t: 'gift', e });
}
// batch events every 50 ms so a big crowd does not flood the socket
setInterval(() => {
  if (queue.length === 0) return;
  const list = queue.splice(0, queue.length);
  send(games, { t: 'evs', list });
}, 50);
setInterval(() => { eventsPerSecond = eventsThisSecond; eventsThisSecond = 0; broadcastStatus(); }, 1000);

const sim = new Simulator(emitEvent);
const tiktok = new TikTokProvider(emitEvent, log);
if (cfg.provider === 'tiktok') tiktok.start(cfg.tiktokUsername);

function broadcastStatus() {
  send(panels, {
    t: 'status',
    gameConnected: games.size > 0,
    provider: cfg.provider,
    tiktokConnected: tiktok.connected,
    eventsPerSecond,
    eventsTotal,
    bot: sim.info(),
    state: lastState,
    thanks: thanks.slice(-12).reverse(),
    shots: recentShots.slice(-3),
  });
}

// ------------------------------------------------------------------ HTTP (panel)
const server = http.createServer((req, res) => {
  const url = new URL(req.url, 'http://localhost');
  if (url.pathname === '/' || url.pathname === '/panel') {
    res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store' });
    fs.createReadStream(path.join(root, 'public', 'panel.html')).pipe(res);
    return;
  }
  if (url.pathname === '/api/status') {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ gameConnected: games.size > 0, eventsTotal, state: lastState }));
    return;
  }
  // tiny local API so scripts (and Claude) can drive the simulator without a browser
  if (url.pathname === '/api/sim' && req.method === 'POST') return readJson(req, (body) => { sim.handle(body.action, body); res.end('ok'); });
  if (url.pathname === '/api/cmd' && req.method === 'POST') return readJson(req, (body) => { send(games, { t: 'cmd', cmd: body.cmd, arg: body.arg || '' }); res.end('ok'); });
  res.writeHead(404); res.end('not found');
});

function readJson(req, cb) {
  let data = '';
  req.on('data', (c) => (data += c));
  req.on('end', () => { try { cb(JSON.parse(data || '{}')); } catch (e) { log('API hatası', e.message); } });
}

// ------------------------------------------------------------------ WebSockets
const wssGame = new WebSocketServer({ noServer: true });
const wssPanel = new WebSocketServer({ noServer: true });

server.on('upgrade', (req, socket, head) => {
  const p = new URL(req.url, 'http://localhost').pathname;
  if (p === '/game') wssGame.handleUpgrade(req, socket, head, (ws) => wssGame.emit('connection', ws, req));
  else if (p === '/panel') wssPanel.handleUpgrade(req, socket, head, (ws) => wssPanel.emit('connection', ws, req));
  else socket.destroy();
});

wssGame.on('connection', (ws) => {
  games.add(ws);
  log('Oyun bağlandı');
  // a (re)started game knows nobody yet: let the simulator start with fresh viewers too
  if (cfg.provider !== 'tiktok') sim.handle('reset');
  ws.on('message', (buf) => {
    let m; try { m = JSON.parse(buf.toString()); } catch { return; }
    if (m.t === 'state') { lastState = m; sim.onGameState(m); }
    else if (m.t === 'thanks') { thanks.push({ ...m, at: Date.now() }); if (thanks.length > 50) thanks.shift(); }
    else if (m.t === 'screenshot') { recentShots.push(m.path); log('Ekran görüntüsü:', m.path); }
  });
  ws.on('close', () => { games.delete(ws); log('Oyun bağlantısı kapandı'); });
});

wssPanel.on('connection', (ws) => {
  panels.add(ws);
  ws.on('message', (buf) => {
    let m; try { m = JSON.parse(buf.toString()); } catch { return; }
    if (m.t === 'sim') sim.handle(m.action, m);
    else if (m.t === 'cmd') send(games, { t: 'cmd', cmd: m.cmd, arg: m.arg || '' });
  });
  ws.on('close', () => panels.delete(ws));
  broadcastStatus();
});

// Only this computer by default. Set "lan": true in config.json to open the panel from a phone on the same Wi-Fi
// (Windows will then ask once for firewall permission).
const host = cfg.lan ? '0.0.0.0' : '127.0.0.1';
server.listen(port, host, () => {
  const ips = Object.values(os.networkInterfaces()).flat().filter((i) => i && i.family === 'IPv4' && !i.internal).map((i) => i.address);
  log(`Köprü hazır. Panel: http://localhost:${port}` + (cfg.lan && ips.length ? `  (telefondan: http://${ips[0]}:${port})` : ''));
  log(`Kaynak: ${cfg.provider === 'tiktok' ? 'TikTok LIVE (@' + cfg.tiktokUsername + ')' : 'Simülatör'}`);
});
