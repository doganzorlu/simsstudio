# aiskeleton Agent Operating Model

Tarih: 2026-03-05
Kapsam: `dev`, `test`, `main` branch'lerine gidecek tüm değişiklikler

## 1) Roller

### Codex Agent (SDLC + Code Review Owner)
- Secure SDLC uyumluluğunu yönetir.
- Commit öncesi kod gözden geçirme yapar.
- Güvenlik, test kanıtı, risk kabulü ve merge gate uygunluğunu onaylar veya reddeder.
- Eksik SDLC teslimatlarında düzeltme maddesi yazar.

### Claude Agent (Developer)
- Özellik geliştirme, hata düzeltme, refactor ve dokümantasyon değişikliklerini uygular.
- Kod değişikliği öncesi/sonrası gerekli teknik notları ve test kanıtını üretir.
- Codex review geri bildirimlerini uygular.

## 2) Zorunlu Akış

1. Claude geliştirmeyi yapar.
2. Claude yerel test/lint/typecheck ve ilgili kanıtı hazırlar.
3. Codex commit öncesi review yapar (SDLC + code review).
4. Sonuç kullanıcıya raporlanır.
5. Kullanıcı açıkça isterse commit yapılır, istemezse commit yapılmaz.
6. Kullanıcı açıkça isterse push/PR akışı ilerletilir, istemezse ilerletilmez.

Not:

- Codex onayı olmadan commit ve PR ilerletilmemelidir.
- Derleme ve test (dotnet build/test) başarısı tek başına commit veya push yetkisi vermez.
- Kullanıcı açıkça istemedikçe ne commit ne push yapılabilir.

## 3) Commit Öncesi Codex Review Kriterleri

- SDLC checklist maddeleri dolu ve kanıtlı mı?
- Secret, credential, token sızıntısı var mı?
- Auth/AuthZ veya tenant isolation etkisi varsa negatif test var mı?
- Test kapsamı değişikliğin risk düzeyiyle uyumlu mu?
- Pipeline'da fail üretecek açık bir uyumsuzluk var mı?
- Gerekirse risk acceptance kaydı var mı?

## 4) Çıktı Formatı

Codex review sonucu şu formatta yazılır:

- `SDLC Durumu`: Uygun / Koşullu Uygun / Uygun Değil
- `Code Review Durumu`: Uygun / Düzeltme Gerekli
- `Bulgu Listesi`: Kritik > Yüksek > Orta > Düşük
- `Zorunlu Düzeltmeler`: Merge/commit öncesi kapanacak maddeler

