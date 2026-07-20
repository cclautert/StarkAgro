using StarkAgroAPI.Services.Diagnosis;

namespace StarkAgroAPI.Tests.Services
{
    public class ImageContentValidatorTests
    {
        [Theory]
        [InlineData("image/jpeg", true)]
        [InlineData("image/png", true)]
        [InlineData("image/webp", true)]
        [InlineData("image/jpeg; charset=utf-8", true)]
        [InlineData("IMAGE/JPEG", true)]
        [InlineData("image/gif", false)]
        [InlineData("application/pdf", false)]
        [InlineData("text/html", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsAllowedContentType_EnforcesAllowlist(string? contentType, bool expected)
        {
            Assert.Equal(expected, ImageContentValidator.IsAllowedContentType(contentType));
        }

        [Fact]
        public void SniffContentType_Jpeg_IsDetected()
        {
            var bytes = new byte[16];
            bytes[0] = 0xFF; bytes[1] = 0xD8; bytes[2] = 0xFF;

            Assert.Equal("image/jpeg", ImageContentValidator.SniffContentType(bytes));
        }

        [Fact]
        public void SniffContentType_Png_IsDetected()
        {
            var bytes = new byte[16];
            byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
            signature.CopyTo(bytes, 0);

            Assert.Equal("image/png", ImageContentValidator.SniffContentType(bytes));
        }

        [Fact]
        public void SniffContentType_Webp_IsDetected()
        {
            var bytes = new byte[16];
            "RIFF"u8.ToArray().CopyTo(bytes, 0);
            "WEBP"u8.ToArray().CopyTo(bytes, 8);

            Assert.Equal("image/webp", ImageContentValidator.SniffContentType(bytes));
        }

        [Fact]
        public void SniffContentType_PayloadDisguisedAsImage_ReturnsNull()
        {
            // Um arquivo pode se declarar image/jpeg no header e ser outra coisa —
            // é o conteúdo que decide.
            var bytes = "<?php system($_GET[0]); ?>"u8.ToArray();

            Assert.Null(ImageContentValidator.SniffContentType(bytes));
        }

        [Fact]
        public void SniffContentType_TooShort_ReturnsNull()
        {
            Assert.Null(ImageContentValidator.SniffContentType([0xFF, 0xD8]));
        }

        [Fact]
        public void ComputeSha256_IsStableForSameContent()
        {
            var a = ImageContentValidator.ComputeSha256("folha"u8.ToArray());
            var b = ImageContentValidator.ComputeSha256("folha"u8.ToArray());
            var c = ImageContentValidator.ComputeSha256("outra"u8.ToArray());

            Assert.Equal(a, b);
            Assert.NotEqual(a, c);
            Assert.Equal(64, a.Length);
        }
    }
}
