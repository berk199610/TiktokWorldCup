// TikTok LIVE sağlayıcısı — Aşama 4'te bağlanacak.
//
// Tasarım (DESIGN §13, §15):
//  * Bağlantı tek bir dosyada durur; kütüphane bozulursa sadece burası değişir, oyun etkilenmez.
//  * Her TikTok olayı simülatörle AYNI şemaya çevrilir:
//      { type: 'gift'|'like'|'comment'|'follow'|'share'|'join', userId, nickname, avatarUrl,
//        coins, unitCoins, count, giftName, text, ts }
//  * Hediye serileri (combo): seri bitince toplam coin bir kez gönderilir (çift sayım olmaz).
//  * Bağlantı koparsa yeniden bağlanır; oyun bu sürede "TEKNİK MOLA" gösterir.
//
// Aşama 4'te yapılacak: `tiktok-live-connector` paketinin GÜNCEL belgesini okuyup olay ve alan adlarını
// oradan almak (tahmin etmek yok), EulerStream imza anahtarını config.json'daki eulerApiKey alanından okumak.

export class TikTokProvider {
  constructor(emit, log) {
    this.emit = emit;
    this.log = log;
    this.connected = false;
  }

  async start(username) {
    this.log(`TikTok sağlayıcısı henüz bağlı değil (Aşama 4). Kullanıcı: ${username || '(boş)'}`);
    this.connected = false;
    return false;
  }

  stop() {
    this.connected = false;
  }
}
