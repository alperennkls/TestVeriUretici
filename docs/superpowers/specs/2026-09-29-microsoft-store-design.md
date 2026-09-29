# Microsoft Store sürümü — tasarım

Tarih: 2026-09-29 · Durum: onay bekliyor

## Amaç

Uygulamayı Microsoft Store'da yayınlamak. Store paketleri Microsoft tarafından imzalandığı için indirenler
"Windows kişisel bilgisayarınızı korudu" uyarısını hiç görmez. GitHub'daki exe dağıtımı aynen sürer.

## Netleşen kararlar

| Konu | Karar |
|---|---|
| Dağıtım | Store (MSIX paketi) + GitHub exe birlikte; tek kod, tek exe |
| Windows ile başlat | İki sürümde de **menüden elle** açılır, kendiliğinden açılmaz |
| Store'da ikonun yeri | Otomatik yerleştirme **yok**; ilk açılış bildirimi "^ altından saatin yanına sürükle" der |
| Store'da özel izin | İstenmez (yalnızca masaüstü uygulamaları için standart olan `runFullTrust`) |

## Neden davranış değişiyor

Store uygulamaları korumalı alanda çalışır: uygulamanın `HKCU` altına yazdığı her şey ona özel bir kopyaya
gider, Windows'un kendisi görmez. Bu yüzden paketli çalışırken:

- `Run` kaydıyla "Windows ile başlat" etkisiz kalır → yerine Windows'un **StartupTask** sistemi kullanılır.
- `NotifyIconSettings\IsPromoted` yazısı Explorer'a ulaşmaz → paketli sürümde `TrayPin` hiç çalıştırılmaz.

## Mimari

Uygulama açılışta `GetCurrentPackageFullName` ile paketli olup olmadığını anlar ve davranışı ona göre seçer:

| Davranış | GitHub exe (paketsiz) | Store (paketli) |
|---|---|---|
| Windows ile başlat | `HKCU\...\Run` + `--autostart` (bugünkü gibi) | `StartupTask` "TestVeriUretici", manifestte `Enabled="false"`; menü `RequestEnableAsync` / `Disable` çağırır |
| Kullanıcı Ayarlar'dan kapattıysa | — | `DisabledByUser`: menü bunu ezmez, "Ayarlar → Uygulamalar → Başlangıç" sayfasını açar |
| Oturum açılışında sessiz başlama | `--autostart` argümanı | `AppInstance.GetActivatedEventArgs().Kind == StartupTask` |
| İkonun yeri | `TrayPin` otomatik yerleştirir | Yerleştirme yok, bildirimde ipucu |
| Açılış bildirimi | "Saatin yanındaki ID ikonuna sağ tıkla" | İkon zaten saatin yanındaysa aynı metin; değilse "İkon ^ altında, saatin yanına sürükle" |

- `AutoStart` sınıfı `TrayApp.cs`'den yeni `AutoStart.cs` dosyasına taşınır ve iki arka uç kazanır:
  paketsiz → Run kaydı (bugünkü kod), paketli → StartupTask. Arayüz asenkron olur
  (`IsEnabledAsync`, `SetEnabledAsync`); menü işareti açılışta bu sorguyla doldurulur.
- `TrayPin`'e salt okunur `IsPromoted()` eklenir. Paketli uygulama kayıt defterini **okurken** gerçek değerleri
  görür (yalnızca yazma yönlendirilir), bu yüzden ikonun saatin yanında olup olmadığını bilip doğru bildirimi seçer.
- WinRT çağrıları Windows'la gelen C# 5 derleyicisiyle `Windows.winmd` referansı üzerinden yapılır
  (denendi: derleniyor; paketsiz çalışırken paketsiz olduğunu doğru algılıyor).
- Tek örnek kilidi (mutex) iki sürüm arasında ortak: Store ve GitHub sürümü aynı anda açılmaz.

## Store paketi

- `store\AppxManifest.xml` şablonu: kimlik, `runFullTrust`, `windows.startupTask` uzantısı, dil `tr-TR`,
  en düşük Windows 10 2004 (`10.0.19041.0`). Kimliğin 3 değeri doğrudan şablonda durur (Store'da zaten herkese
  açık bilgiler); Partner Center değerleri gelene kadar geçici bir test kimliği kullanılır.
  Sürüm numarasını `build.ps1` `AssemblyInfo.cs`'den okuyup yazar, iki yer hiç ayrışmaz.
- İkonlar `AppIcon`'dan üretilir: `Square44x44Logo` (ölçek + `targetsize` 16–256, `altform-unplated`),
  `Square150x150Logo`, `StoreLogo`; `makepri` ile `resources.pri`.
- `build.ps1 -Msix`: exe'yi derler, paket klasörünü hazırlar, `makeappx pack` ile `TestVeriUretici.msix` üretir.
  Store'a imzasız yüklenir, Microsoft imzalar.
- Sürüm `1.2.0.0` (Store son haneyi 0 ister). GitHub'da da v1.2.0 yayınlanır.

## Store sayfası (hazırlanacaklar)

- Türkçe açıklama, kısa açıklama, özellik listesi, anahtar kelimeler, kategori önerisi (Geliştirici araçları).
- `PRIVACY.md`: uygulama veri toplamaz, internete bağlanmaz; Store'a bu dosyanın GitHub adresi verilir.
- Ekran görüntüleri: gerçek menü ve bildirim, kişisel masaüstü görünmeden. Ekranı 2–3 saniye kullanır,
  çalıştırmadan önce haber verilir.
- Store logoları (1080×1080, 300×300).
- İnceleme ekibine not: tepsi uygulaması olduğu, nasıl test edileceği, `runFullTrust` gerekçesi,
  değerlerin test amaçlı rastgele üretildiği.

## Test

1. Mevcut birim testleri + paketsiz çalışırken "paketli değil" algılandığının testi.
2. Yerel paket testi (Geliştirici Modu gerekir): paket klasörü `Add-AppxPackage -Register` ile kurulur;
   kontrol listesi: açılış bildirimi, menü, panoya kopyalama, Windows ile başlat aç/kapat
   (Ayarlar → Başlangıç'ta görünür), oturum açılışında sessiz başlama, kaldırınca temizlik.
3. Paketsiz exe'nin davranışı değişmedi mi (bugünkü üç test).

## Kullanıcının adımları

1. storedeveloper.microsoft.com'da bireysel hesap (ücretsiz, kimlik doğrulaması).
2. Partner Center'da ad ayırma, "Ürün kimliği" sayfasındaki 3 değeri paylaşma.
3. Yerel test için Geliştirici Modu'nu açma (Ayarlar → Sistem → Geliştiriciler için).
4. Paketi ve sayfa bilgilerini Partner Center'dan gönderme (adım adım rehber verilecek).

## Kapsam dışı

- Store'dan özel izin isteyerek otomatik ikon yerleştirme.
- İngilizce arayüz / Store sayfası.
- Kod imzalama sertifikası (GitHub exe'si imzasız kalır).

## Riskler

- **Ad alınmış olabilir** → alternatif ad (ör. "TC VKN IBAN Üretici").
- **İnceleme**: geçerli TC üreten bir araç yanlış anlaşılabilir → açıklamada ve inceleme notunda test amacı açıkça yazılır.
- **StartupTask** davranışı yalnızca paketli testte doğrulanabilir → yerel paket testi zorunlu.
