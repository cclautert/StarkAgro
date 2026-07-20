namespace StarkAgroAPI.Services.Diagnosis
{
    /// <summary>
    /// Valida a foto enviada pelo produtor. O <c>Content-Type</c> declarado pelo cliente
    /// não é confiável, então além da allowlist conferimos os <i>magic bytes</i> do arquivo.
    /// </summary>
    public static class ImageContentValidator
    {
        public const long MaxSizeBytes = 12L * 1024 * 1024;

        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

        public static bool IsAllowedContentType(string? contentType)
            => !string.IsNullOrWhiteSpace(contentType)
               && AllowedContentTypes.Contains(contentType.Split(';')[0].Trim());

        /// <summary>
        /// Confere a assinatura binária do arquivo. Retorna o content-type real, ou null
        /// se o conteúdo não for uma imagem JPEG, PNG ou WebP.
        /// </summary>
        public static string? SniffContentType(byte[] content)
        {
            if (content is null || content.Length < 12) return null;

            // JPEG: FF D8 FF
            if (content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
                return "image/jpeg";

            // PNG: 89 50 4E 47 0D 0A 1A 0A
            if (content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47
                && content[4] == 0x0D && content[5] == 0x0A && content[6] == 0x1A && content[7] == 0x0A)
                return "image/png";

            // WebP: "RIFF" .... "WEBP"
            if (content[0] == 0x52 && content[1] == 0x49 && content[2] == 0x46 && content[3] == 0x46
                && content[8] == 0x57 && content[9] == 0x45 && content[10] == 0x42 && content[11] == 0x50)
                return "image/webp";

            return null;
        }

        public static string ComputeSha256(byte[] content)
            => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant();
    }
}
