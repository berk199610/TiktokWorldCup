// Simülatör: TikTok olmadan gerçekçi yayın trafiği üretir (DESIGN §16, Aşama 1).
// Ürettiği olaylar canlı TikTok ile AYNI şemadadır, bu yüzden test ettiğimiz şey canlıda çalışan şeyle aynıdır.

const NAMES = {
  TR: ['Ahmet', 'Mehmet', 'Ayşe', 'Elif', 'Can', 'Emre', 'Zeynep', 'Burak', 'Selin', 'Mert', 'Deniz', 'Kerem', 'Ece', 'Yusuf', 'Hakan', 'Büşra', 'Oğuz', 'İrem'],
  VN: ['Minh', 'Linh', 'Huy', 'Trang', 'Nam', 'Thu', 'Duc', 'Anh', 'Quang', 'Mai', 'Tuan', 'Hoa'],
  AZ: ['Elvin', 'Aysel', 'Rauf', 'Nigar', 'Orxan', 'Leyla', 'Kamran', 'Günay'],
  ID: ['Budi', 'Sari', 'Agus', 'Dewi', 'Rizky', 'Putri', 'Andi', 'Fajar'],
  BR: ['João', 'Lucas', 'Ana', 'Pedro', 'Gabriel', 'Larissa', 'Rafael', 'Bruna'],
  AR: ['Mateo', 'Valentina', 'Santiago', 'Lucía', 'Thiago', 'Martina'],
  MX: ['Diego', 'Sofía', 'Carlos', 'Ximena', 'Luis', 'Fernanda'],
  MA: ['Youssef', 'Amine', 'Salma', 'Hamza', 'Imane', 'Karim'],
  PK: ['Ali', 'Hamza', 'Ayesha', 'Usman', 'Fatima', 'Bilal'],
  PH: ['Juan', 'Mark', 'Angel', 'Paolo', 'Jasmine', 'Rico'],
};
const TR_PLATES = ['34', '06', '35', '16', '61', '07', '01', '42', '27', '55', '21', '33'];
const CHATTER = ['gooool', 'hadi', 'vamos', 'cố lên', 'yes!!', 'harika', 'bravo', 'ayy', 'come on', 'şut çek'];
const GIFT_NAMES = [
  [1, 'Rose'], [5, 'Finger Heart'], [10, 'Doughnut'], [99, 'Hand Hearts'], [100, 'Confetti'],
  [500, 'Money Gun'], [1500, 'Fireworks'], [3000, 'Sports Car'], [5000, 'Lion'], [29999, 'Universe'],
];

const rnd = (a, b) => a + Math.floor(Math.random() * (b - a));
const pick = (arr) => arr[Math.floor(Math.random() * arr.length)];
export const flagEmoji = (code) => String.fromCodePoint(...[...code.slice(0, 2).toUpperCase()].map((c) => 0x1f1e6 + c.charCodeAt(0) - 65));
const giftName = (unit) => { let n = 'Gift'; for (const [c, name] of GIFT_NAMES) if (unit >= c) n = name; return n; };

export class Simulator {
  constructor(emit) {
    this.emit = emit;
    this.fans = new Map();
    this.byCountry = {};
    this.nextId = 1;
    this.bot = { viewers: 0, weights: { TR: 40, VN: 25, AZ: 10, ID: 10, BR: 5, PH: 5, MX: 5 } };
    this.carry = { likes: 0, comments: 0, gifts: 0 };
    this.match = null; // last match state from the game, used for reactions
    this.timer = setInterval(() => this.tick(0.1), 100);
    this.spamTimers = [];
  }

  // ---------------------------------------------------------------- fans

  randomCountry() {
    const entries = Object.entries(this.bot.weights);
    const total = entries.reduce((s, [, w]) => s + w, 0);
    let r = Math.random() * total;
    for (const [c, w] of entries) { if ((r -= w) <= 0) return c; }
    return 'TR';
  }

  newFan(country, joinWithComment = true) {
    const id = 'sim_' + this.nextId++;
    const base = pick(NAMES[country] || NAMES.TR);
    const suffix = country === 'TR' ? pick(TR_PLATES) : (Math.random() < 0.5 ? String(rnd(1, 99)) : '_' + rnd(10, 999));
    const fan = { id, nickname: base + suffix, country };
    this.fans.set(id, fan);
    (this.byCountry[country] ||= []).push(fan);
    if (joinWithComment && country) this.comment(fan, flagEmoji(country) + (Math.random() < 0.5 ? '' : ' ' + pick(CHATTER)));
    return fan;
  }

  fanOf(country) {
    if (!country || country === 'none') return this.teamlessFan();
    const list = this.byCountry[country];
    if (list && list.length && Math.random() < 0.85) return pick(list);
    return this.newFan(country);
  }

  teamlessFan() {
    const id = 'sim_anon_' + this.nextId++;
    const fan = { id, nickname: 'İzleyici' + rnd(100, 999), country: null };
    this.fans.set(id, fan);
    return fan;
  }

  anyFan() {
    if (this.fans.size < Math.max(1, this.bot.viewers) && (this.fans.size === 0 || Math.random() < 0.3))
      return this.newFan(this.randomCountry());
    const all = [...this.fans.values()];
    return all[Math.floor(Math.random() * all.length)];
  }

  // ---------------------------------------------------------------- event makers (same schema as TikTok)

  base(fan, type) {
    return { type, userId: fan.id, nickname: fan.nickname, avatarUrl: '', ts: Date.now() };
  }
  comment(fan, text) { this.emit({ ...this.base(fan, 'comment'), text }); }
  like(fan, count) { this.emit({ ...this.base(fan, 'like'), count }); }
  gift(fan, unit, count = 1) {
    this.emit({ ...this.base(fan, 'gift'), coins: unit * count, unitCoins: unit, count, giftName: giftName(unit) });
  }

  // ---------------------------------------------------------------- panel actions

  handle(action, a = {}) {
    switch (action) {
      case 'join': return this.newFan(a.country || 'TR');
      case 'like': return this.like(this.fanOf(a.country || 'TR'), a.count || 100);
      case 'comment': return this.comment(this.fanOf(a.country || 'TR'), a.text || pick(CHATTER));
      case 'gift': return this.gift(this.fanOf(a.country), a.unit || 1, a.count || 1);
      case 'whale': {
        const f = this.fanOf(a.country || 'TR');
        return this.gift(f, 30000, 1);
      }
      case 'teamless': return this.gift(this.teamlessFan(), 1, 1);
      case 'spam': {
        const f = this.fanOf(a.country || 'TR');
        let n = 0;
        const t = setInterval(() => { this.comment(f, 'spam ' + n); if (++n >= 100) clearInterval(t); }, 100);
        this.spamTimers.push(t);
        return;
      }
      case 'storm': {
        for (let i = 0; i < 10; i++) this.gift(this.newFan(a.country || 'TR', true), 500, 1);
        return;
      }
      case 'bot':
        this.bot.viewers = Math.max(0, Math.min(5000, Number(a.viewers) || 0));
        return;
      case 'weights':
        if (a.weights) this.bot.weights = a.weights;
        return;
      case 'reset':
        this.fans.clear(); this.byCountry = {};
        return;
    }
  }

  onGameState(state) {
    this.match = state && state.match ? state.match : null;
  }

  // ---------------------------------------------------------------- crowd bot (same model as the C# test bot)

  tick(dt) {
    const v = this.bot.viewers;
    if (v <= 0) return;
    this.carry.likes += v * 0.3 * dt;
    this.carry.comments += v * 0.01 * dt;
    this.carry.gifts += v * 0.0015 * dt;
    while (this.carry.comments >= 1 || (this.carry.comments > 0 && Math.random() < this.carry.comments)) {
      this.carry.comments = Math.max(0, this.carry.comments - 1);
      this.comment(this.anyFan(), pick(CHATTER));
    }
    if (this.carry.likes >= 1) {
      const n = Math.floor(this.carry.likes);
      this.carry.likes -= n;
      // likes come in bursts from a few people
      const parts = Math.min(n, 1 + Math.floor(v / 200));
      for (let i = 0; i < parts; i++) this.like(this.anyFan(), Math.max(1, Math.floor(n / parts)));
    }
    while (this.carry.gifts >= 1 || (this.carry.gifts > 0 && Math.random() < this.carry.gifts)) {
      this.carry.gifts = Math.max(0, this.carry.gifts - 1);
      const r = Math.random();
      let unit, count = 1;
      if (r < 0.80) { unit = 1; count = rnd(1, 6); }
      else if (r < 0.88) unit = 5;
      else if (r < 0.975) unit = rnd(10, 100);
      else if (r < 0.995) unit = rnd(100, 500);
      else if (r < 0.9995) unit = rnd(500, 5000);
      else unit = rnd(5000, 30000);
      this.gift(this.anyFan(), unit, count);
    }
    // fans react to the shot/save bar ("10 gül = kurtarış")
    const m = this.match;
    if (m && m.chance && m.pens === false) {
      const p = Math.min(0.9, v * 0.002) * dt;
      const atk = m.chance.atk === 0 ? m.a : m.b;
      const def = m.chance.atk === 0 ? m.b : m.a;
      if (Math.random() < p) this.gift(this.fanOf(def), 1, rnd(3, 16));
      if (Math.random() < p * 0.6) this.gift(this.fanOf(atk), 1, rnd(3, 12));
    }
  }

  info() {
    return { viewers: this.bot.viewers, fans: this.fans.size, weights: this.bot.weights };
  }
}
