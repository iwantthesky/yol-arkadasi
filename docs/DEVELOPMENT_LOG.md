# Geliştirme ve doğrulama kaydı

Son güncelleme: 2026-09-16

## Uygulanan sürüm

- v0.5.1-demo.1 kapalı test paketi IL2CPP ile native Windows build olarak üretildi. Managed stripping `High`, engine stripping açık, development build kapalı ve çalışma zamanı stack trace'leri kapalıdır.
- Paket içine kaynak proje, C# kaynakları, `Assembly-CSharp.dll`, PDB/debug sembolleri veya Unity'nin `BackUpThisFolder_ButDontShipItWithYourGame` geliştirme arşivi alınmaz.
- Ekranda kalıcı `CLOSED TEST / DO NOT REDISTRIBUTE / ENGIN-CLOSED-ALPHA-20260916` işareti ve paket içinde kullanım notu bulunur. Bu bir DRM garantisi değildir; amacı kolay .NET decompilation yolunu kapatmak, sızıntıyı caydırmak ve build'i tanımlanabilir kılmaktır.

- v0.5.1'de kullanıcının bildirdiği aşırı savrulma ölçüldü. Kök neden denge torku değil, yüksek hızda fazla gidon açısıydı: 8 m/s'de tam dönüşü karşılamak için gereken denge girdisi 1,76 iken oyuncu sınırı 1,00'dı; 15 m/s'de gereksinim 3,30'a çıkıyordu.
- Gidon limiti hız ve izin verilen yanal ivmeye bağlandı; klavye direksiyonunun açıya ulaşma hızı da yumuşatıldı. Düşük hız manevrası korunurken yeni denge gereksinimi 8 m/s'de 0,52, 15 m/s'de 0,61 oldu.
- Otomatik rota ve QA sürücüsü yeni normalize direksiyon aralığına uyarlandı. Dönüşün dengeyi bozması korunur; tam direksiyon artık denge oyuncusunun fiziksel yetkisini aşmaz.

- v0.5.0'da Antigravity FBX modellerinin Unity'de beyaz kalan malzemeleri düzeltildi. Dağ vertex renklerini kullanan özel shader ve ağaç parçasına göre kabuk/yaprak/iğne/çiçek renk eşlemesi eklendi.
- On dağ örneğine statik MeshCollider, ağaçlara gövdeyi izleyen CapsuleCollider eklendi. Fizik sorgusu yalnız bu katmanlara yönelir; dekor yaprakları gereksiz pahalı çarpışma üretmez.
- Dağ yüksekliği artık sürüş zeminine katılır. Yaklaşık 34 dereceye kadar yönsel eğim ve 72 cm'den küçük yüzey adımı sürülebilir; daha dik yüz ve duvar motoru içeri sokmadan durdurur.
- Sıradağlar merkezi serbest dolaşım vadisini kapatmayacak, fakat 480 m genişliğindeki harita içinde erişilebilir kalacak şekilde dışa taşındı. Bitişten sonra da ağaç/dağ çarpışması çalışmaya devam eder.
- Değişiklikten önce `backups/20260916-035644-before-physical-scenery` altında 29 dosyanın SHA-256 manifestli yedeği alındı. Blender ve `d.blend` kullanılmadı veya değiştirilmedi.

- v0.4.0'da motosiklet rota rayından çıkarıldı. Dünya yönü, kalıcı gidon açısı ve X/Z hareketi eklendi; motor haritada 360 derece dönebilir, rotaya ters yönde gidebilir ve asfalt dışına serbestçe çıkabilir.
- Yol dışına çıkınca çalışan yapay kaza sınırı kaldırıldı. Yol ve arazi için ortak yükseklik örnekleme, çimde daha düşük çekiş/yuvarlanma direnci ve köprü kenarında gerçek düşüş davranışı eklendi.
- Roll fiziği yerçekimi, merkezcil dönüş ivmesi, yol bankı, rüzgâr, arazi sarsıntısı ve oyuncu ağırlık torkuyla yeniden kuruldu. Otomatik dik tutma yayı kaldırıldı; gidonu çevirmek dengeyi fiziksel olarak bozar.
- Bitiş çizgisi motoru kilitlemez. Başarı kaydedilir ve ekip haritada serbest sürüşe devam edebilir.

- v0.3.0 için dünya Antigravity'nin özgün 7 ağaç, 4 dağ ve 10 yol/engel modeliyle baştan kuruldu. Önceki v0.2.0 sürümü ve kaynakları korunur.
- Değişiklikten önce `backups/20260916-030646-before-antigravity-rebuild` altında 87 dosyalık yedek alındı; Blender kaynak kopyasının SHA-256 değeri kaynakla eşleşti.
- Dağ geçidi 10 büyük dağ örneği, performans ağırlıklı ağaç dağılımı, özgün köprü tahtaları, taşlar, paletler, variller, dubalar, lastikler ve hareketli barikatlarla yenilendi.
- Asfalt, fizik rotasının örnekleriyle üretilmeye devam eder; Antigravity yol tasarımından uyarlanan prosedürel yüzey dokusu ve genişletilmiş görüş mesafesi eklendi.
- İlk Player görüntü kontrolünde dağ FBX'lerinin Z-up ekseni Unity'de dik arazi plakaları oluşturuyordu. Dağlara özel -90 derece kök dönüşü uygulandı; ikinci build ve görsel kontrol doğru yatay araziyi doğruladı.

- Unity 6000.3.18f1 ile Windows 64-bit `Yol Arkadaşı` v0.2.0 prototipi hazırlandı.
- Kullanıcının `d.blend` motosikleti gövde, ön teker ve arka teker pivotlarıyla oyun modeline dönüştürüldü. Özgün dosyaya kayıt yapılmadı; önce hash eşleşmeli yedek alındı.
- 900 metrelik beş bölümlü parkur; slalom kayaları, yan rüzgâr, dar köprü, iki rampa, kütükler, hareketli kapılar, kontrol noktaları ve bitiş alanıyla oluşturuldu.
- Manuel kavrama, boş vites + beş ileri vites, devir, motor stop etme/marş, fren, direksiyon, ağırlık aktarımı, düşme, havalanma/iniş, kaza ve kontrol noktasından devam davranışları eklendi.
- 2–4 kişilik ev sahibi yetkili TCP/LAN co-op eklendi. Oyuncu sayısına göre gaz/fren, şanzıman, direksiyon ve denge rolleri otomatik paylaşılır.
- Türkçe ana menü, ekip garajı, sürüş HUD'ı, yardım, duraklatma, kamera ve ses denetimi eklendi.
- Kenney Nature Kit 2.1 içinden yedi CC0 FBX kullanıldı; kaynak ve arşiv lisansı `ASSETS.md` içinde kaydedildi.

## Geçen kontroller

- v0.5.1-demo.1 Unity davranış kontrolleri build öncesi geçti. IL2CPP Windows build: **başarılı**, 275.385.224 bayt, development build kapalı, `MOTOR_COOP_CLOSED_DEMO_PASS`.
- Kapalı demo menü Player testi: hata **0**, 219 Antigravity örneği, 10 dağ collider, 120 ağaç collider ve üç parçalı motor doğrulandı.
- Kapalı demo solo Player testi: **163,9 m**, **19,45 m/s**, yolda, kaza **0**, çalışma zamanı hatası **0** ve süreç çıkış kodu **0**. 1440x900 kamera çıktısı görsel olarak incelendi.
- Koruma denetiminde dağıtım kökünde `GameAssembly.dll` bulundu; `Assembly-CSharp.dll`, `.cs`, `.pdb`, `.mdb` ve IL2CPP C++ ara çıktıları dağıtım dışında tutuldu.

- v0.5.1 Unity davranış paketi: **16/16**. Yeni kontrol, hız duyarlı gidonun düşük hızda manevra bırakmasını, yüksek hızda daralmasını ve 8 m/s tam dönüşün denge yetkisiyle kontrol edilebilmesini doğrular.
- v0.5.1 Windows build: **başarılı**, 109.226.787 bayt; Unity batch çıkışı `0`, sürüm `0.5.1` ve `MOTOR_COOP_BUILD_PASS`.
- Yol Player testi: 207,2 m, 19,45 m/s, kontrol noktası 1, kaza **0**, hata **0**. Serbest arazi testi: yol merkezinden 40,2 m uzakta, kaza **0**, hata **0**.
- Eski/yeni direksiyon hesabı `qa/steering-diagnosis-v051.txt`; çalışma zamanı raporları `qa/steering-v051-road/` ve `qa/steering-v051-freeroam/` altındadır.

- v0.5.0 Unity davranış paketi: **15/15**. Yatık/dik dağ eğimi ayrımı ve gerçek Physics CapsuleCollider ile ağaç çarpışması yeni kontrollerdir; 900 m rota testi de geçmeye devam eder.
- v0.5.0 Windows build: **başarılı**, 109.226.787 bayt; Unity batch çıkışı `0`, sürüm `0.5.0` ve `MOTOR_COOP_BUILD_PASS`.
- Final Player raporu: **10 dağ MeshCollider**, **120 ağaç gövde CapsuleCollider**, 219 Antigravity örneği, üç parçalı motor ve çalışma zamanı hatası **0**.
- Serbest harita Player testi: `onRoad=false`, yol merkezinden **41,7 m** uzakta, kaza **0**, motor çalışır ve dengeli. İlk fizik denemesinde dik dağ yüzü gerçek Player içinde motoru durdurdu; dağlar dışa taşındıktan sonra merkezi vadi yeniden kesintisiz doğrulandı.
- 1440×900 görsel QA'da çöl platosunun terakota, yeşil tepelerin yeşil, alp kayalarının gri/koyu ve ağaç gövdelerinin kahverengi olduğu doğrulandı; beyaz malzeme sorunu görülmedi.

- v0.4.0 Unity davranış paketi: **13/13**. Yeni kontrol; asfalt dışına yapay kaza olmadan çıkmayı, dünya yönüyle başlangıca geri sürmeyi ve gidonun dengeyi dışa bozmasını kapsar. Tam 900 m rota yeni fizik denklemiyle de tamamlandı.
- v0.4.0 Windows build: **başarılı**, 107.667.059 bayt; Unity batch çıkışı `0` ve `MOTOR_COOP_BUILD_PASS`.
- Serbest harita Player testi: yol merkezinden **41,7 m** uzakta, **28 km/sa**, 1. vites, motor çalışır, dengeli, kaza **0**, çalışma zamanı hatası **0** ve `onRoad=false`.
- Harita zemini 480×1260 m alana genişletildi; final sahnede **219 Antigravity örneği** ve üç parçalı motosiklet doğrulandı.
- Ağ protokolü yeni yön/gidon durumu için v2 oldu. Güncel kaynakla 2/3/4 oyunculu gerçek TCP harness yeniden **25/25** geçti.

- v0.3.0 Windows build: **başarılı**, 107.664.499 bayt; Unity batch çıkışı `0` ve `MOTOR_COOP_BUILD_PASS`.
- Antigravity asset açılış kontrolü: **21/21 model** bulundu. Final Player raporunda sahnede **173 Antigravity örneği**, motosiklette **3 hareketli mesh** ve çalışma zamanı hatası **0**.
- Final solo Player sürüşü: **139 metre**, yaklaşık **73 km/sa**, 1. vites, motor çalışır, dengeli, kaza **0** ve hata **0**.
- v0.3.0 sonrası gerçek TCP harness: **25/25**; 2/3/4 oyunculu katılım, rol paylaşımı, kopma/yeniden katılma ve kötü niyetli paket izolasyonu geçti.

- Unity Editor davranış paketi: **12/12**. Debriyaj zorunluluğu, kalkış/stop/marş, vites sınırları, fren, yönlü denge, checkpoint/reset, kaya/kütük, rampa inişi, bitiş, 30/60 FPS tutarlılığı ve yasal girdilerle 900 m tam rota kapsandı.
- Unity rol kontrolü: 1–4 oyuncuda her girdinin tek sahibi, yalnız ev sahibinin reset yetkisi ve bozuk girdilerin sınırlandırılması geçti.
- Ayrı gerçek TCP harness: **25/25**. 2/3/4 oyuncu, rol değişimi, bağlantı kopması/yeniden katılma, pulse tekrarını engelleme, sahte rol/reset, 8 KiB sınırı ve mesaj taşması kapsandı. Harness Unity zaman/JSON API'leri için stublar kullanır; transport kaynak dosyasını doğrudan derler.
- Dört gerçek Unity Player süreci aynı odaya bağlandı. İstemciler 1, 2 ve 3 numaralı farklı rolleri aldı; ortak motor 300 metreden fazla ilerledi; dört süreçte de çalışma zamanı hatası `0` kaldı.
- Son menü ve solo Player çalıştırması: motor FBX'i üç hareketli mesh olarak yüklendi, solo sürüş 40 metre ilerledi, motor çalışır/1. vites durumunda kaldı ve hata sayısı `0` oldu.
- Windows build: `MOTOR_COOP_BUILD_PASS`, 94.341.027 bayt; Unity batch çıkışı `0`.
- Görsel QA sonrası ters ve taşan dünya yazıları gerçek dünya yüksekliğine ölçeklendi. Menü, motor, dört karakter, parkur ve HUD son ekran görüntülerinde incelendi.

Kanıt dosyaları `qa/unity-v050-final-build.log`, `qa/physical-world-v05-final/`, `qa/physical-world-v05-menu/`, `qa/physical-world-v05-player.log`, `qa/build-result.txt`, `qa/network-harness.txt` ve `qa/lan4-verification.txt` altındadır.

## Sınırlar

- IL2CPP ve stripping tersine mühendisliği zorlaştırır fakat imkânsız yapmaz. Modeller, dokular ve diğer istemci varlıkları yeterli uğraşla çıkarılabilir; ciddi ticari dağıtım öncesinde kod imzalama, çevrimiçi yetkilendirme ve kişiye özel build kimlikleri ayrıca değerlendirilebilir.
- Çevrimiçi hizmet, eşleştirme, Steam daveti, relay/NAT geçişi ve otomatik host migration yoktur. Doğrulanan kapsam aynı bilgisayar ve yerel TCP'dir; farklı evler için ortak VPN veya erişilebilir ağ kurulumu gerekir.
- Fizik beceri odaklıdır; dünya yönü, gidon geometrisi, dönüş ivmesi ve oyuncu dengesi modellenir. Ayrıntılı lastik kayması ve WheelCollider süspansiyonu içermez.
- Kullanıcı motosikletindeki prosedürel Blender mikro dokuları bake edilmedi. Ana materyal renkleri/metaliklik/parlaklık Unity'ye taşındı.
- Son oyuncu kontrolü klavye ile yapılır; oyun kolu ve yeniden eşlenebilir tuşlar bu sürümde yoktur.
