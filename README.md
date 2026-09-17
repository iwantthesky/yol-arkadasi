# Yol Arkadaşı — Dört Cüce Bir Motor

Engin'in `d.blend` motosikletiyle, Antigravity'nin dağ, ağaç, yol ve engel assetlerinden kurulan 900 metrelik rota ve bütünüyle sürülebilir açık haritada 2–4 kişilik ortak sürüş. Unity 6000.3.18f1 / Windows.

Bu özel depo oyunun birlikte geliştirilen kaynak sürümüdür. Kararlı oynanabilir kaynak `main`, yeni çalışmalar `dev` dalında tutulur. Katkı akışı için [CONTRIBUTING.md](CONTRIBUTING.md) dosyasını okuyun.

## Başlatma

Yerel build varsa `Unity/Builds/Windows/Yol-Arkadasi.exe` dosyasını çalıştır. Build çıktıları GitHub deposuna eklenmez; kaynak koddan üretmek için aşağıdaki Unity bölümünü kullan.

Kapalı test için kaynak proje yerine `release/Yol-Arkadasi-Kapali-Test-0.5.1-demo.1.zip` gönderilir. Alıcı ZIP'i tamamen çıkartıp `Yol-Arkadasi-Kapali-Test.exe` dosyasını çalıştırmalıdır. Bu paket IL2CPP release build'dir; development seçenekleri, kaynak kod, IL2CPP C++ çıktısı ve debug sembolleri pakete alınmaz. Ekranın sağ altında `ENGIN-CLOSED-ALPHA-20260916` build kimliği görünür. Paket çalınmayı imkânsız kılmaz; kaynak kodun doğrudan verilmesini önler ve sıradan .NET decompilation işini belirgin biçimde zorlaştırır.

- **Ekibi kur:** adını ve 2–4 kişilik kapasiteyi seç. Garajda gösterilen IP adresini arkadaşlarına ver.
- **Katıl:** aynı yerel ağdan veya yapılandırılmış ortak VPN ağından ev sahibinin IP adresini yaz.
- En az iki kişi bağlanınca ev sahibi **Birlikte yola çık** düğmesine basar.
- **Tek başına antrenman:** tüm kontrolleri tek oyuncu yönetir.

Bağlantı TCP 47777 kullanır. Windows ağ erişimini sorarsa özel ağda oyuna izin verilmesi gerekir. Bu sürüm LAN/doğrudan IP prototipidir; Steam daveti, hesap sistemi, relay/NAT geçişi veya internet eşleştirmesi içermez. Genel internete port açmak gerekli değildir. Farklı evlerden oynamak için ortak VPN ağının erişilebilirliği ayrıca kurulmuş olmalıdır.

## Kontroller

| Görev | Tuşlar |
|---|---|
| Gaz / fren | W / S |
| Direksiyon | A / D |
| Debriyajı ayır | SPACE basılı tut; bırakınca kademeli kavrar |
| Vites küçült / büyüt | Q / E; debriyaj ayrılmış olmalı |
| Marş | I; boş viteste veya debriyaj ayrılmışken |
| Ağırlığı sola / sağa aktar | Sol / sağ ok |
| Son kontrol noktasından devam | R; yalnız ev sahibi |
| Rehber / kamera / ses | H / C / M |
| Duraklat / menü | ESC |

Kalkış: SPACE basılı → E ile birinci vites → W ile gaz → SPACE'i bırak. Dururken debriyajı ayır; aksi halde motor stop edebilir. Motor sağa yatıyorsa sol okla karşı ağırlık ver.

## Rol paylaşımı

| Oyuncu sayısı | 1. oyuncu | 2. oyuncu | 3. oyuncu | 4. oyuncu |
|---|---|---|---|---|
| 2 | Gaz, fren, direksiyon | Debriyaj, vites, denge | — | — |
| 3 | Gaz, fren, direksiyon | Debriyaj, vites | Denge | — |
| 4 | Gaz, fren | Debriyaj, vites | Direksiyon | Denge |

Oyuncu girip çıktığında roller yeniden paylaşılır ve ev sahibi sürüşü devam ettirir. Fizik yalnız ev sahibinde hesaplanır; istemciler kendi rolünün girdisini gönderir. Ev sahibi ayrılırsa oda kapanır; otomatik ev sahibi aktarımı yoktur.

## Parkur ve fizik

Alp dağları, volkan, çöl platosu ve yeşil tepelerden geçen rota; özgün ağaç kümeleri, şantiye malzemeleri, slalom kayaları, yan rüzgâr, dar ahşap köprü, rampalar, hareketli barikatlar ve tırmanış içerir. Dağlar fiziksel yüzeydir: yatık eteklere çıkılabilir, çok dik yüzler motoru durdurur. Ağaçların gövdeleriyle çarpışılır ve içlerinden geçilemez. Kontrol noktaları 180, 360, 540 ve 720 metrededir. Engelden yavaş geç veya etrafından dolaş. Denge ve direksiyon ayrı girdilerdir.

Motor yol eksenine bağlı değildir: 360 derece dönebilir, başlangıca geri gidebilir ve asfalt dışındaki araziyi gezebilir. Gidon açısı gerçek bir dönüş yarıçapı ve yanal ivme üretir; dik motor dönüşün dışına düşmeye başlar. Denge oyuncusu dönüş içine ağırlık vermeli ve yerçekimi, rüzgâr ile arazi darbelerini sürekli düzeltmelidir. Otomatik dik tutma yoktur.

v0.5.1'de klavye direksiyonu hız duyarlı hale getirildi. Düşük hızda manevra açısı korunur; hız yükseldikçe gidon açısı ve tepki hızı kademeli azalır. Böylece direksiyon dönüşü hâlâ dengeyi bozar fakat gereken karşı ağırlık oyuncunun verebildiği aralıkta kalır.

Antigravity dağlarının özgün vertex renkleri Unity shader'ında okunur; renk verisi olmayan parçada dağ türüne uygun taş, terakota, bazalt veya yeşil yamaç rengi kullanılır. Ağaç kabuğu, gövde, yaprak, iğne yaprak ve sakura çiçekleri ayrı malzemelerle renklenir.

Sürüş beceri odaklı bir motosiklet modelidir: kavrama, motor devri, beş ileri vites, stop etme, gidon açısı, dünya yönü, ağırlık aktarımı, yerçekimi, merkezcil ivme ve rampa uçuşu modellenir. Lastik kayması ve ayrıntılı süspansiyon için WheelCollider tabanlı mühendislik simülasyonu değildir.

## Unity kaynakları

Projeyi `Unity` klasöründen aç. Sahne: `Assets/Scenes/YolArkadasi.unity`. Menü: **Yol Arkadaşı → Test et ve Windows derle**. Bu işlem davranış kontrollerini çalıştırır, sahneyi oluşturur ve Windows build üretir.

Kapalı demo için menü: **Yol Arkadaşı → Kapalı IL2CPP demo derle**. Windows Build Support (IL2CPP) modülü gereklidir. Build çıktısındaki `*_BackUpThisFolder_ButDontShipItWithYourGame` klasörü yalnız yerel hata ayıklama arşividir ve hiçbir zaman testçiye gönderilmez.

Temel proje `C:/Users/Engin/Documents/Dort-Cuce-Bir-Motor/Unity` içinden alınan yedekli çalışma kopyasıdır. Önceki repo ve `codex/unity-prototype` dalına bu çalışma sırasında dokunulmadı. Asıl kaynak `.blend` üzerine kaydedilmedi.

Kaynak/lisans ayrıntıları: [ASSETS.md](docs/ASSETS.md). Doğrulama çıktıları `qa` klasöründedir; son kabul durumu [DEVELOPMENT_LOG.md](docs/DEVELOPMENT_LOG.md) içindedir.
