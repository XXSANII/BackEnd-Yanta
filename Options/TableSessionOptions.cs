namespace YantaBackEnd.Options;

public sealed class TableSessionOptions
{
    public const string SectionName = "TableSession";

    /// <summary>
    /// คีย์ลับสำหรับเซ็น token ของลูกค้าที่สแกน QR (HMAC-SHA256)
    /// อย่างน้อย 32 ตัวอักษร เก็บใน User Secrets / Environment Variable เท่านั้น ห้าม commit
    /// </summary>
    public string Secret { get; set; } = string.Empty;
}
