# Microsoft Store sayfası — Partner Center'a girilecek metinler

## Açıklama
Test Veri Üretici, yazılım testlerinde ihtiyaç duyulan geçerli biçimli TC Kimlik No, Vergi Kimlik No ve IBAN değerlerini tek tıkla üretir.

Saatin yanındaki ID ikonuna sağ tıklayın ve ihtiyacınız olanı seçin:
• TC üret: kontrol haneleri doğru, 11 haneli TC Kimlik No
• VKN üret: Gelir İdaresi algoritmasına uygun, 10 haneli Vergi Kimlik No
• IBAN üret: mod 97 kontrollü, 26 karakterlik TR IBAN

Değer anında panoya kopyalanır; istediğiniz alana Ctrl+V ile yapıştırın. Kısa bir bildirim neyin kopyalandığını gösterir ve çalıştığınız pencereden odağı almaz.

SAP, ERP, e-fatura, form ve API testleri yapan geliştiriciler, test uzmanları ve danışmanlar için.

Yalnızca test amaçlıdır: değerler rastgele üretilir, hiçbir gerçek kişinin verisinden türetilmez ve yalnızca biçim ile kontrol hanesi kurallarına uyar. Rastgele bir numara tesadüfen gerçek bir kişiye, şirkete veya hesaba ait olabileceği için test ortamları dışında kullanmayın.

Açık kaynaklıdır (MIT lisansı): https://github.com/alperennkls/TestVeriUretici

## Kısa açıklama
Tek tıkla geçerli TC Kimlik No, VKN ve IBAN test verisi üretip panoya kopyalar.

## Ürün özellikleri
- Tek tıkla TC Kimlik No, VKN ve TR IBAN üretimi
- Kontrol haneleri resmi algoritmalara uygun
- Değer otomatik olarak panoya kopyalanır
- Odağı çalmayan kısa bildirim
- İsteğe bağlı: Windows açılışında sessizce başlar
- Veri toplamaz, internete bağlanmaz

## Arama terimleri (en fazla 7)
TC kimlik, VKN, IBAN, test verisi, vergi kimlik numarası, test data, SAP

## Bu sürümdeki yenilikler
İlk Microsoft Store sürümü.

## Diğer alanlar
- Telif hakkı: © 2026 Alperen
- Kategori: Geliştirici araçları
- Web sitesi: https://github.com/alperennkls/TestVeriUretici
- Destek: https://github.com/alperennkls/TestVeriUretici/issues
- Gizlilik politikası: https://github.com/alperennkls/TestVeriUretici/blob/main/PRIVACY.md

## Yaş derecelendirmesi (IARC anketi)
Uygulama türü olarak "Yardımcı program / verimlilik / diğer" seçilir; şiddet, cinsellik, kumar, kullanıcılar arası iletişim, konum paylaşımı ve dijital satın alma sorularının hepsine "Hayır". Beklenen sonuç: 3+ / Herkes.

## İnceleme ekibine notlar (Notes for certification)
Test Veri Üretici is a system tray utility with no main window. After launch, a short notification appears near the clock and an "ID" icon is added to the notification area (Windows may place it under the ^ overflow arrow). Right-click the icon and choose "TC üret", "VKN üret" or "IBAN üret": a randomly generated, checksum-valid Turkish national ID number, tax ID number or TR IBAN is copied to the clipboard for use as software test data. "Windows ile başlat" toggles the app's startup task; "Çıkış" exits.
The values are random and only satisfy the public checksum rules (the same algorithms are used by common open-source test-data libraries); they are not derived from any real person's data, the app does not look anything up, and both the app description and its documentation state that the values are for test environments only. The app collects no data and makes no network connections.

## runFullTrust gerekçesi (Restricted capability justification)
The app is a Windows Forms (.NET Framework 4.8) desktop application packaged as MSIX; runFullTrust is required to run it as a desktop app. It shows a notification-area icon and writes generated test values to the clipboard. It does not modify system settings.
