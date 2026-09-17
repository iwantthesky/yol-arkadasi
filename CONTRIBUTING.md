# Yol Arkadaşı'na katkı

## Kurulum

1. Depoyu klonla.
2. Unity Hub ile `Unity` klasörünü aç.
3. Unity `6000.3.18f1` kullan.
4. Ana sahne olarak `Assets/Scenes/YolArkadasi.unity` dosyasını aç.

`Library`, `Logs`, `UserSettings` ve build klasörleri depoya eklenmez. Unity bunları ilk açılışta yeniden üretir.

## Dal düzeni

- `main`: oynanabilir ve doğrulanmış sürüm.
- `dev`: sıradaki sürümün birleşim dalı.
- Yeni işler: `feature/kisa-konu` veya `fix/kisa-konu`.

Değişiklikleri önce kendi dalına gönder, ardından `dev` dalına pull request aç. Sürüm hazır olduğunda `dev`, pull request ile `main` dalına alınır. Doğrudan `main` üzerine çalışma yapma.

## Değişiklik kontrolü

- Unity sahne ve `.meta` dosyalarını birlikte commit et.
- Başkasının sahne değişikliğiyle çakışabilecek büyük düzenlemeleri başlamadan önce ekibe yaz.
- Oynanışı değiştiren işlerde solo kalkış, düşme/toparlanma ve serbest dolaşımı kontrol et.
- Ağ kodunu değiştiren işlerde `tools/network-checks` kontrollerini çalıştır.
- Unity menüsündeki **Yol Arkadaşı → Test et ve Windows derle** komutunu sürüm pull request'inden önce çalıştır.
- Üçüncü taraf asset eklerken kaynak ve lisans bilgisini `docs/ASSETS.md` içine yaz.

## Büyük ve yerel dosyalar

Build, video, günlük, yedek ve Unity `Library` çıktıları GitHub'a gönderilmez. Ekip içi test build'leri ayrı dosya paylaşımı veya GitHub Release eki olarak paylaşılmalıdır.
