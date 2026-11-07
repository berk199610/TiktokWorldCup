# Ücretsiz asset listesi ve lisanslar

Bu depo herkese açık. Bu yüzden içine sadece **serbestçe dağıtılabilen** dosyalar konur
(CC0, kamu malı, SIL OFL). Lisansı "kullan ama dağıtma" olan dosyalar
`game/Assets/_Private/` klasörüne konur. Bu klasör git'e gönderilmez.

## Projede şu an kullanılanlar

| Ne | Kaynak | Lisans | Nerede |
|---|---|---|---|
| Ülke bayrakları (252 adet, PNG 320 px) | [flagcdn.com](https://flagcdn.com) | Bayraklar kamu malı | `game/Assets/StreamingAssets/flags/` |
| Yazı tipi Barlow Condensed (SemiBold/Bold/ExtraBold) | [Google Fonts](https://fonts.google.com/specimen/Barlow+Condensed) | SIL Open Font License 1.1 | `game/Assets/Resources/Fonts/` |
| Stadyum, saha, kale, file, top, kupa, tribün, LED pano | Kodla üretiliyor | Bizim | `game/Assets/Scripts/Game/Visual/` |
| Oyuncular (Aşama 1: basit gövdeler) | Kodla üretiliyor | Bizim | `Actors.cs` |

## İndirildi, Aşama 3'te oyuna takılacak

| Ne | Kaynak | Lisans | İçinde ne var |
|---|---|---|---|
| Universal Animation Library 1 (Standard) | [Quaternius](https://quaternius.com/packs/universalanimationlibrary.html) · [OpenGameArt](https://opengameart.org/content/universal-animation-library) | CC0 | 45 animasyon + insan mankeni: Idle, Walk, Jog, Sprint, Jump, Roll, Dance, Hit… |
| Universal Animation Library 2 (Standard) | [Quaternius](https://quaternius.com/packs/universalanimationlibrary2.html) · [OpenGameArt](https://opengameart.org/content/universal-animation-library-2) | CC0 | 43 animasyon + kadın manken: Slide, NinjaJump, Idle_FoldArms, Yes… |

İndirilen ham dosyalar `_downloads/` klasöründe durur (git'e gitmez).

## Önerilen, henüz indirilmedi

| Ne | Kaynak | Lisans | Neden | Kim indirir |
|---|---|---|---|---|
| Universal Base Characters (6 karakter, saç, ten rengi) | [quaternius.itch.io/universal-base-characters](https://quaternius.itch.io/universal-base-characters) | CC0 | En kaliteli ücretsiz insan modeli; animasyon kütüphaneleriyle aynı iskelet | itch.io "No thanks, just take me to the downloads" — tarayıcıdan bir tık |
| Futbol mocap paketi (şut, top sürme, kaleci kurtarışı) | Anderson Rohr — Gumroad ([haber](https://www.cgchannel.com/2026/09/download-130-free-fbx-mocap-moves-from-anderson-rohr/)) | Ticari kullanım serbest, isim gerekmez; ham dosya dağıtılamaz | CC0 kütüphanelerde şut ve kaleci uçuşu yok | Gumroad e-posta istiyor → sen indirirsin, `_Private/` klasörüne konur |
| Mixamo futbol animasyonları (yedek) | mixamo.com | Ücretsiz, ticari serbest; ham dosya dağıtılamaz | Gerekirse ek şut/sevinç | Adobe hesabı → sen indirirsin, `_Private/` |
| Tribün uğultusu, düdük, gol sesi | Freesound (CC0 filtresi), OpenGameArt | CC0 olanlar | Aşama 3 sesleri | Lisansı tek tek kontrol edilecek |

## Kullanmayacaklarımız (DESIGN §2)

Gerçek futbolcu adı/yüzü, federasyon logoları, resmi formalar, "FIFA / Dünya Kupası" adları
ve kupa şekilleri, milli marşlar, popüler şarkılar, başka oyunlardan alınmış modeller.
