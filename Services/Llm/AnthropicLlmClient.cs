using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace _404JobNotFound.Services.Llm;

/// <summary>
/// Talks to the Anthropic Messages API directly over HTTP (no SDK dependency needed for this scope).
/// Every prompt asks the model to answer with strict JSON so responses are cheap to parse and validate.
/// </summary>
public class AnthropicLlmClient : ILlmClient
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly string _model;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public AnthropicLlmClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = config["ANTHROPIC_API_KEY"];
        _model = config["ANTHROPIC_MODEL"] ?? "claude-sonnet-5";

        _http.BaseAddress = new Uri("https://api.anthropic.com/");
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _http.DefaultRequestHeaders.Remove("x-api-key");
            _http.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            _http.DefaultRequestHeaders.Remove("anthropic-version");
            _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        }
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<JobExtractResult> ExtractJobAsync(string rawText, CancellationToken ct = default)
    {
        const string system = """
            Bir iş ilanı metnini yapılandırılmış veriye dönüştüren bir ayrıştırıcısın.
            Sadece verilen metinde GEÇEN bilgileri çıkar. Emin olmadığın alanları boş bırak, uydurma.
            SADECE aşağıdaki şemaya uyan JSON döndür, başka hiçbir metin ekleme:
            {"title": string, "companyName": string, "location": string|null, "workMode": "Remote"|"Hybrid"|"OnSite"|"Unknown",
             "description": string, "applyUrl": string|null, "contactEmail": string|null}
            "description" alanına ilanın gereksinim/nitelik kısımlarını olabildiğince eksiksiz aktar.
            """;

        var raw = await SendAsync(system, rawText, 2000, ct);
        var json = ExtractJson(raw);
        var dto = JsonSerializer.Deserialize<JobExtractDto>(json, JsonOpts) ?? new JobExtractDto();
        return new JobExtractResult
        {
            Title = dto.Title ?? "",
            CompanyName = dto.CompanyName ?? "",
            Location = dto.Location,
            WorkMode = dto.WorkMode,
            Description = dto.Description ?? rawText,
            ApplyUrl = dto.ApplyUrl,
            ContactEmail = dto.ContactEmail
        };
    }

    public async Task<JobScoreResult> ScoreJobAsync(string jobTitle, string jobDescription, ProfileContext profile, CancellationToken ct = default)
    {
        const string system = """
            Kıdemli bir teknik işe alım danışmanısın. Sana bir aday profili ve bir iş ilanı verilecek.
            Görevin: adayın bu ilana ne kadar uygun olduğunu 0-100 arası bir puanla değerlendirmek ve
            gerekçelerini maddeler halinde açıklamak.
            Kurallar:
            - Sadece adayın profilinde listelenen becerileri "mevcut" say. Profilde olmayan hiçbir beceriyi varsayma.
            - İlan kıdemli/senior/lead pozisyon istiyorsa veya adayın deneyim yılının çok üzerinde bir deneyim
              istiyorsa bunu ciddi bir eksi olarak işaretle.
            - Breakdown listesinde ilanın somut gereksinimlerini madde madde ver (örn. "C# gerekli", "5 yıl deneyim gerekli").
            SADECE şu şemaya uyan JSON döndür, başka metin ekleme:
            {"score": number (0-100), "breakdown": [{"requirement": string, "met": boolean, "note": string}]}
            """;

        var userPrompt = $$"""
            ADAY PROFİLİ:
            İsim: {{profile.FullName}}
            Deneyim: {{profile.YearsOfExperience}} yıl
            Özet: {{profile.Summary}}
            Beceriler: {{string.Join(", ", profile.Skills)}}

            İLAN:
            Pozisyon: {{jobTitle}}
            Açıklama:
            {{jobDescription}}
            """;

        var raw = await SendAsync(system, userPrompt, 1500, ct);
        var json = ExtractJson(raw);
        var dto = JsonSerializer.Deserialize<JobScoreDto>(json, JsonOpts) ?? new JobScoreDto();
        return new JobScoreResult
        {
            Score = Math.Clamp(dto.Score, 0, 100),
            Breakdown = (dto.Breakdown ?? new()).Select(b => new ScoreBreakdownItem
            {
                Requirement = b.Requirement ?? "",
                Met = b.Met,
                Note = b.Note ?? ""
            }).ToList()
        };
    }

    public async Task<EmailDraftResult> GenerateEmailAsync(EmailGenerationContext context, CancellationToken ct = default)
    {
        var system = """
            Bir iş başvuru e-postası taslağı yazan bir asistansın. Kısa, profesyonel ve doğal görünmelidir
            (kopyala-yapıştır gibi hissettirmemeli). E-postayı ilan hangi dildeyse o dilde yaz (Türkçe ilan -> Türkçe e-posta,
            İngilizce ilan -> İngilizce e-posta).

            EN ÖNEMLİ KURAL: Adayla ilgili yazacağın HER somut iddia (proje, teknoloji, başarı, deneyim) SADECE
            aşağıda "GROUNDING_BULLETS" olarak verilen listeden gelmelidir. Bu listede olmayan hiçbir deneyim,
            proje, şirket adı veya rakam UYDURMA. Listede ilana uyan bir şey yoksa genel ama dürüst bir ifade kullan,
            asla yalan yazma.

            SADECE şu şemaya uyan JSON döndür, başka metin ekleme:
            {"subject": string, "body": string}
            "body" düz metin olsun (HTML değil), CV'nin ekli olduğuna dair kısa bir not içersin.
            """;

        var userPrompt = $$"""
            ADAY: {{context.CandidateName}}
            ADAY ÖZETİ: {{context.ProfileSummary}}

            GROUNDING_BULLETS (sadece bunları kullanabilirsin):
            {{string.Join("\n", context.GroundingBullets.Select(b => "- " + b))}}

            ŞİRKET: {{context.CompanyName}}
            POZİSYON: {{context.JobTitle}}
            İLAN AÇIKLAMASI:
            {{context.JobDescription}}
            """;

        var raw = await SendAsync(system, userPrompt, 1200, ct);
        var json = ExtractJson(raw);
        var dto = JsonSerializer.Deserialize<EmailDraftDto>(json, JsonOpts) ?? new EmailDraftDto();
        return new EmailDraftResult { Subject = dto.Subject ?? "", Body = dto.Body ?? "" };
    }

    public async Task<FactCheckResult> FactCheckEmailAsync(string emailBody, IReadOnlyList<string> groundingBullets, CancellationToken ct = default)
    {
        const string system = """
            Bir gerçeklik denetleyicisisin. Sana bir başvuru e-postası ve adayla ilgili izin verilen "gerçek" listesi
            (grounding bullets) verilecek. E-postada geçen HER somut iddiayı (proje, teknoloji, şirket, rakam, başarı)
            kontrol et: bu iddia grounding listesindeki bir maddeyle destekleniyor mu?
            Desteklenmeyen bir iddia bulursan bunu bir sorun olarak işaretle.
            Genel/nazik ifadeler (örn. "işinizle ilgileniyorum") sorun değildir, sadece SOMUT iddiaları kontrol et.
            SADECE şu şemaya uyan JSON döndür:
            {"passed": boolean, "issues": [string]}
            """;

        var userPrompt = $$"""
            GROUNDING_BULLETS:
            {{string.Join("\n", groundingBullets.Select(b => "- " + b))}}

            E-POSTA METNİ:
            {{emailBody}}
            """;

        var raw = await SendAsync(system, userPrompt, 800, ct);
        var json = ExtractJson(raw);
        var dto = JsonSerializer.Deserialize<FactCheckDto>(json, JsonOpts) ?? new FactCheckDto { Passed = true };
        return new FactCheckResult { Passed = dto.Passed, Issues = dto.Issues ?? new() };
    }

    private async Task<string> SendAsync(string system, string userMessage, int maxTokens, CancellationToken ct)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("ANTHROPIC_API_KEY yapılandırılmamış.");

        var payload = new
        {
            model = _model,
            max_tokens = maxTokens,
            system,
            messages = new[] { new { role = "user", content = userMessage } }
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync("v1/messages", content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Anthropic API hatası ({(int)response.StatusCode}): {body}");

        using var doc = JsonDocument.Parse(body);
        var text = doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();

        return text ?? "";
    }

    /// <summary>Strips ```json / ``` markdown fences the model sometimes wraps its answer in.</summary>
    private static string ExtractJson(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0) trimmed = trimmed[(firstNewline + 1)..];
            var closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (closingFence >= 0) trimmed = trimmed[..closingFence];
        }
        return trimmed.Trim();
    }

    private class JobExtractDto
    {
        public string? Title { get; set; }
        public string? CompanyName { get; set; }
        public string? Location { get; set; }
        public string? WorkMode { get; set; }
        public string? Description { get; set; }
        public string? ApplyUrl { get; set; }
        public string? ContactEmail { get; set; }
    }

    private class JobScoreDto
    {
        public int Score { get; set; }
        public List<ScoreBreakdownDto>? Breakdown { get; set; }
    }

    private class ScoreBreakdownDto
    {
        public string? Requirement { get; set; }
        public bool Met { get; set; }
        public string? Note { get; set; }
    }

    private class EmailDraftDto
    {
        public string? Subject { get; set; }
        public string? Body { get; set; }
    }

    private class FactCheckDto
    {
        public bool Passed { get; set; }
        public List<string>? Issues { get; set; }
    }
}
