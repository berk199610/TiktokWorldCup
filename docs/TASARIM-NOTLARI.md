# Tasarım notları — dokümandan farklar ve verilen kararlar

Ana kaynak tasarım dokümanıdır (PDF). Bu dosya, uygularken dokümanın açık bıraktığı yerlerde ne
yaptığımızı ve bilinçli farkları listeler. Bir kararı değiştirmek istersen buradan bul, söyle, değiştirelim.

## Bilinçli farklar

| Konu | Doküman | Bizde | Neden |
|---|---|---|---|
| Oyun motoru | Three.js (tarayıcı) | Unity 6.3 LTS | Senin tercihin. Katmanlar aynı kaldı: Köprü → Sim → Görüntü → Arayüz |
| Bayraklar ekranda | Bayrak emojisi | Bayrak resmi (PNG) | Windows bayrak emojisi çizemiyor; yorumdaki bayrak emojisi yine okunuyor |
| Duyuru yuvası | Ekranın %64–68'i | Ekranın ~%50'si, sahanın ortası | TikTok'un hediye bildirimleri ve yorumları altta sol tarafı kaplıyor |
| Hız ayarı (k) penceresi | "son 60 sn" | Yayının son 60 sn'si (maçtan bağımsız) | Maç başında top çok hızlı gidiyordu (test yakaladı) |
| Köprü dili | Node + TypeScript | Node + düz JavaScript | Derleme adımı yok; çift tıkla çalışır |
| Kamera kütüphanesi | — | Kendi küçük kamera yönetmenimiz | Kod içinde tam kontrol; Cinemachine gerekirse Aşama 3'te eklenebilir |

## Dokümanın açık bıraktığı yerler — verdiğimiz karar

1. **Takımı sahada olmayan taraftarın hediyesi** (ör. AZ taraftarı, TR–VN maçında gül atar):
   takımsız hediye kuralı uygulanır → ev sahibi sahadaysa ona, değilse geride olan takıma. Para boşa gitmez.
2. **Hediyenin kademesi**: tek hediyenin coin değerine göre (10 gül = 10 × K1 "PAS", K2 değil).
   Güç ise toplam coin üzerinden. Böylece "rövaşata için ne atmalıyım" sorusunun cevabı tek bir hediye olur.
3. **Pozisyon açıkken K4/K5**: sinematik oynarken gelirse kuyruğa girer (oynayan dahil en fazla 3),
   fazlası anında çubuğa eklenir. Pozisyon penceresi açıkken gelirse gücü çubuğa eklenir, adı duyuruda görünür.
4. **Balina testi**: tek bir 30.000 coin hediye en fazla 1 gol getirir. Alev ikonu 2 gol geride olunca çıkar;
   test bu yüzden önce 1-0 öne geçip sonra balinayı gönderiyor.
5. **Sessiz yayında penaltılar**: kimse bir şey yapmazsa her atış kaçar; ani ölüm sonsuza gitmesin diye
   5 turdan sonra maçta daha çok güç veren taraf kazanır (`maxSuddenDeathRounds`).
6. **Misafir takım şampiyon olursa** kupa sayılmaz (kupa tablosu gerçek ülkeler için).
7. **İlk kaptan** sessizce belirlenir; duyuru sadece biri onu geçince çıkar ("Biri onu geçerse").

## Ayar konusunda izlenecekler (Aşama 5'te gerçek veriyle)

- 2.000 izleyicide top ortalama ~8,8 birim/sn gidiyor (hedef ~4). Sebep: tek taraflı hediye akışı,
  sürekli koreografi bonusu. İzlenebilir hızda; gerçek yayın verisiyle `ball.kMax` / `targetUnitsPerSec` ayarlanacak.
- Kalabalık yayında beğeniler çok hızlı birikiyor; 1.000 beğenide bir koreografi büyük yayında sürekli tetiklenebilir.
  Öneri: `likesPerChoreo` değerini izleyici sayısına göre büyütmek (karar senin).
- 100 izleyicili simülasyonda maç başına ortalama 4,0 gol (hedef 2–5 ✓).
