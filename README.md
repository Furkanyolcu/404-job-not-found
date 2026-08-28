# 404JobNotFound

Kişisel iş başvuru yardımcısı. İş ilanı yapıştır → uygunluk skoru + gerekçe gör → kişiselleştirilmiş
başvuru e-postası taslağı üret → gözden geçir/düzenle → onayla → gönder. **Hiçbir e-posta senin
onayın olmadan gönderilmez** — sistemde otomatik gönderim yoktur, bilinçli bir tasarım kararıdır.

## Kapsam (v1 — hafta sonu MVP)

- ✅ İlan yapıştırma (manuel URL/metin) + LLM varsa otomatik alan çıkarımı
- ✅ Uygunluk skorlama: LLM key varsa AI destekli, yoksa anahtar kelime tabanlı deterministik fallback
- ✅ Grounding'e dayalı AI e-posta üretimi + halüsinasyon karşı fact-check geçişi
- ✅ Manuel onay akışı (Draft → Approved → Sent), otomatik gönderim yok
- ✅ Kara liste, tekrar-gönderim engeli, günlük gönderim limiti
- ❌ Otomatik scraping/scheduler yok (v2'ye bırakıldı)
- ❌ Docker/PostgreSQL yok — SQLite kullanıyor (tek dosya, kurulum yok)
- ❌ Auth yok — **sadece localhost'ta çalıştır, internete açma**

## Kurulum

1. `.env.example` dosyasını `.env` olarak kopyala ve doldur:
   ```
   cp .env.example .env
   ```
   - **ANTHROPIC_API_KEY**: https://console.anthropic.com/ — boş bırakırsan sistem yine çalışır,
     sadece AI yerine anahtar kelime tabanlı skorlama + şablon e-posta kullanır.
   - **GMAIL_ADDRESS / GMAIL_APP_PASSWORD**: Google hesabında 2FA aç, sonra
     https://myaccount.google.com/apppasswords adresinden bir uygulama şifresi oluştur. Normal Gmail
     şifreni **kullanma**.

2. Çalıştır:
   ```
   dotnet run
   ```
   İlk çalıştırmada veritabanı (`App_Data/hirepilot.db`) otomatik oluşturulur/migrate edilir.

3. Tarayıcıda aç: `http://localhost:5243`

4. **Profilim** sekmesinden önce temel bilgilerini, becerilerini (alias'larla, örn. "ASP.NET Core" /
   "AspNetCore") ve deneyim cümlelerini (bullet) gir — bunlar olmadan skorlama/e-posta üretimi çalışmaz.
   AI e-postaları **sadece** buradaki deneyim cümlelerinden alıntı yapar, başka bir şey uydurmaz.

5. Bir CV (PDF) yükle ve varsayılan olarak işaretle — gönderilen e-postalara otomatik eklenir.

## Mimari notları

- Tek ASP.NET Core Minimal API projesi + düz HTML/JS dashboard (`wwwroot/`), build aracı yok.
- SQLite + EF Core, migration'lar `Data/Migrations/`.
- `Services/Llm/ILlmClient` arkasında `AnthropicLlmClient` — provider değiştirmek istersen sadece bu
  arayüzü implemente eden yeni bir sınıf yazıp `Program.cs`'te DI kaydını değiştir.
- Gönderim `MailKit` ile Gmail SMTP üzerinden, `EmailSendService` içinde kara liste / tekrar-gönderim /
  günlük limit kontrolleri var.

## Güvenlik

Bu araç senin gerçek Gmail hesabınla e-posta gönderiyor ve dashboard'da hiç auth yok. Sadece kendi
makinende, `localhost`'ta çalıştır. Uzaktan erişmen gerekirse (ör. Tailscale/VPN üzerinden) formal bir
auth katmanı eklemeden internete açma.

## Sonraki adımlar (v2 fikirleri)

Resmi API'li iş kaynaklarından (Arbeitnow, Remotive, RemoteOK) otomatik çekme + Hangfire ile
zamanlama, bounce/complaint webhook takibi, Telegram/Discord üzerinden hızlı onay, çoklu profil/CV
versiyonlama.

## Lisans

Kişisel kullanım için geliştirildi, ayrı bir lisans dosyası yok.
