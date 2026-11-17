# LocalRecorder

Windows için yerel ekran kaydedici. Ekranı, seçtiğiniz bir alanı veya açık bir pencereyi MP4 olarak kaydeder. Kayıtlar bilgisayarınızda kalır; bir hesaba veya buluta gönderilmez.

## Gereksinimler

- Windows 10 veya Windows 11, 64 bit
- Kayıt sırasında yeterli boş disk alanı
- Bilgisayar sesi için Windows’ta seçili bir çıkış cihazı
- Mikrofon kaydı için Windows’ta seçili bir giriş cihazı ve mikrofon izni

Hazır kurulum dosyası .NET çalışma zamanını birlikte getirir. Ayrı bir .NET kurulumu gerekmez. Kaynak koddan derleyecekseniz [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) gerekir.

## Kurulum

1. `LocalRecorder-Kurulum.exe` dosyasını çalıştırın.
2. Kurulum, uygulamayı şu klasöre kopyalar:

   `%LOCALAPPDATA%\Programs\LocalRecorder`

3. Masaüstüne ve Başlat menüsüne **LocalRecorder** kısayolu eklenir.
4. Kurulum bitince uygulama kendiliğinden açılır.

Uygulama kurulum sırasında açıksa kapatılır ve yeni sürümle değiştirilir. Yönetici izni istenmez; kurulum yalnızca sizin kullanıcı hesabınıza yapılır.

Kurulum takılırsa `%TEMP%\LocalRecorder-install.log` dosyasına bakın.

## Kullanım

Pencereyi başlıktan sürükleyebilirsiniz. Sağ üstten küçültülür veya kapatılır.

### Kayıt türü

Yeşil alanın sağ üstündeki listeden türü seçin:

- **Normal Ekran Kaydı** — masaüstü, pencere ve seçili alan için genel kayıt.
- **Oyun Kaydı** — tam ekran oyunların görüntüsünü almak için Windows Graphics Capture kullanır.

Kayıt başladıktan sonra tür değiştirilemez. Önce türü, sonra kaynağı seçin.

### Kayıt kaynağı

Soldaki monitör düğmesi kaynağı açar:

- **Ekran** — bağlı ekranlardan birinin tamamı. Birden fazla monitör varsa her biri ayrı satırdadır.
- **Belirli alan** — seçilen ekranın bir bölgesi.
- **Seçilen pencere** — açık pencerelerden biri. Listeyi **Yenile** ile güncelleyin. LocalRecorder kendi penceresini listede göstermez.

Alan kaydı için örtüşen kareler düğmesine basın, fareyle dikdörtgeni çizin, köşelerden boyutunu değiştirin veya içinden tutup taşıyın. **Alanı kaydet** ya da Enter ile onaylayın. **Vazgeç** veya Esc seçimi iptal eder. Alan en az birkaç piksel olmalı ve seçilen ekranın içinde kalmalıdır.

### Ses, hız ve kayıt

- **FPS** — 30 ve 60 arasında geçiş yapar. Varsayılan 30’dur.
- Hoparlör düğmesi bilgisayar sesini açar veya kapatır. Varsayılan açıktır.
- Mikrofon düğmesi varsayılan mikrofonu ekler. Varsayılan kapalıdır.
- Kırmızı düğme kaydı başlatır. Kayıt sırasında aynı düğme durdurur.
- Duraklat düğmesi kaydı bekletir; aynı düğme devam ettirir.

Kayıt sürerken kaynak, FPS, ses ve tür kilitlenir. Süre sayacı kaydın uzunluğunu gösterir. Altındaki boyut, o ana kadar yazılan dosyayı ve videoların bulunduğu diskte kalan boş alanı gösterir (`yazılan / boş`).

Durdurduktan sonra dosya tamamlanınca yolu bir pencerede gösterilir. Durum satırı kayıt sırasında dosya yolunu ipucu olarak da tutar.

## Kayıtlar nereye yazılır?

Videolar şu klasöre kaydedilir:

`%USERPROFILE%\Videos\LocalRecorder`

Dosya adı `Recording_yyyy-MM-dd_HH-mm-ss.mp4` biçimindedir. Örnek: `Recording_2026-11-17_21-30-00.mp4`.

Video H.264, ses stereo ve 192 kbps’tir. Klasör yoksa ilk kayıtta oluşturulur.

## Kaldırma

1. LocalRecorder açıksa kapatın.
2. `%LOCALAPPDATA%\Programs\LocalRecorder` klasörünü silin.
3. Masaüstündeki ve Başlat menüsündeki **LocalRecorder** kısayollarını silin.

Kayıtlar Videolar klasöründe kalır. Onları da silmek isterseniz `%USERPROFILE%\Videos\LocalRecorder` klasörünü ayrıca kaldırın.

## Kaynak koddan derleme

```powershell
dotnet publish LocalRecorder\LocalRecorder.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Çıktı `LocalRecorder\bin\x64\Release\net8.0-windows\win-x64\publish` altındadır. `LocalRecorder.exe` ile yanındaki `ScreenRecorderLib.dll` birlikte durmalıdır; DLL’yi exe’den ayırmayın.

Kurulum paketini yeniden üretmek için bu iki dosyayı `installer` klasörüne kopyalayıp şunu çalıştırın:

```powershell
iexpress /N installer\setup.sed
```

Paket `LocalRecorder-Kurulum.exe` olarak oluşur.

## Sık karşılaşılan durumlar

- **Bilgisayar sesi bulunamadı** — Windows ses ayarlarından bir çıkış cihazı seçin, hoparlör düğmesinin açık olduğundan emin olun.
- **Mikrofon bulunamadı** — Varsayılan giriş cihazını seçin ve Windows gizlilik ayarlarından mikrofon erişimine izin verin.
- **Pencere artık açık değil** — Pencere listesinde **Yenile** deyip kaydı yeniden seçin.
- **Ekran bağlı değil** — Monitör çıkarıldıysa kaynak listesinden hâlâ bağlı olan ekranı seçin.
- **Oyun görüntüsü boş** — Kayıt türünü **Oyun Kaydı** yapın. Oyun exclusive tam ekrandaysa oyunu kenarlıksız pencere moduna alın.
- **Disk doluyor** — Durum satırının altındaki boş alan azalırsa kaydı durdurun. Eski dosyalar Videolar\LocalRecorder içindedir.
