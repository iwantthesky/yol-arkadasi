# Asset kaynakları

## Engin motosikleti

- Kullanıcının sağladığı kaynak: `C:/Users/Engin/Desktop/3D Modeller/d.blend`.
- Özgün SHA-256: `9097A5F390BE8534A0BAAEE7E26AB0BB48F62CB78F37D3BF92F5F56AB2673E52`.
- Yedek: `backups/20260913-153255/d.blend`; kopyanın hash'i özgün dosyayla aynı.
- Blender 5.2 MCP üzerinden değerlendirilen geometri ayrı FBX'e çıkarıldı. Stüdyo, paddock standı ve açık yan ayak oyun kopyasına alınmadı. Gövde/ön teker/arka teker ayrı; teker açıklığı 1.9 metreye normalize edildi.
- Oyun dosyası: `Unity/Assets/Resources/Motorcycle/EnginMotor.fbx`.
- `materials.json` kaynak Principled base color, roughness ve metallic değerlerini taşır. Blender prosedürel mikro dokuları bake edilmedi; Unity görünümü kaynak renderının tam eşlemesi değildir.
- Kullanıcının sağladığı asset; CC0 olduğu iddia edilmez. Üçüncü taraf marka/işaret bilgileri ayrıca doğrulanmadı.

## Antigravity özgün çevre seti

- Ağaç kaynakları: `C:/Users/Engin/.gemini/antigravity/scratch/blender_trees`.
- Dağ kaynakları: `C:/Users/Engin/.gemini/antigravity/scratch/blender_mountains`.
- Yol ve engel kaynakları: `C:/Users/Engin/.gemini/antigravity/scratch/DangerousMountainRoad`.
- Oyun kopyaları: `Unity/Assets/Resources/Antigravity`.
- İçerik: 7 ağaç, 4 dağ ve 10 yol/şantiye modeli. Dağların sahne sunum konumları temizlenip oyun rotasına yerleştirilebilen FBX'lere dönüştürüldü.
- Yol geometrisi mevcut sürüş fiziğiyle aynı `MotorCourse` örneklerini kullanır. Antigravity'nin `DangerousRoadGenerator.cs` tasarımı asfalt görünümü, dağ geçidi yerleşimi ve yol kenarı işaretlerinde kaynak olarak kullanıldı.
- Bunlar üçüncü taraf CC0 paketleri değildir; kullanıcının yerel Antigravity/Blender üretimleridir. Ayrıntılı model listesi `Unity/Assets/Resources/Antigravity/PROVENANCE.md` dosyasındadır.

## Önceki Kenney yedeği

Kenney Nature Kit 2.1 dosyaları, lisans metniyle birlikte proje içinde geri dönüş yedeği olarak tutulur. Antigravity dünya kurulumunda sahneye örneklenmez.

## Bu prototipte üretilenler

Sürüşle birebir eşleşen yol/zemin geometrisi, kamp yapıları, tabelalar, arayüz, basit ekip karakterleri ve motor sesi bu çalışma için kodla üretildi. Asfalt dokusu çalışma zamanında prosedürel üretilir. Harici ses kaydı veya müzik kullanılmadı. Arayüz Unity'nin `LegacyRuntime.ttf` fontunu kullanır.
