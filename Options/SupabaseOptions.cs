namespace YantaBackEnd.Options;

public sealed class SupabaseOptions
{
    public const string SectionName = "Supabase";

    /// <summary>เช่น https://xxxx.supabase.co</summary>
    public string Url { get; set; } = string.Empty;

    public string Audience { get; set; } = "authenticated";

    /// <summary>
    /// ใส่เฉพาะกรณีโปรเจกต์ยังใช้ Legacy JWT Secret (HS256)
    /// ถ้าใช้ signing keys แบบใหม่ (ES256/RS256) ปล่อยว่างไว้
    /// เก็บใน User Secrets / Environment Variable เท่านั้น ห้าม commit
    /// </summary>
    public string? JwtSecret { get; set; }

    public string Issuer => $"{Url.TrimEnd('/')}/auth/v1";

    public string JwksUrl => $"{Issuer}/.well-known/jwks.json";
}