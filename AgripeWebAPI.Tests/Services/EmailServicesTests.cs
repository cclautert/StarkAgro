using AgripeWebAPI.Configuration;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Services.Diagnosis;
using AgripeWebAPI.Services.Email;
using AgripeWebAPI.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Services
{
    public class SmtpSettingsTests
    {
        [Theory]
        [InlineData(null, null, false)]
        [InlineData("CHANGE_ME", "a@b.com", false)]   // placeholder do repositório não é config
        [InlineData("smtp.teste.com", null, false)]
        [InlineData("smtp.teste.com", "", false)]
        [InlineData("smtp.teste.com", "a@b.com", true)]
        public void IsConfigured_OnlyWhenHostAndSenderAreReal(string? host, string? from, bool expected)
        {
            var settings = new SmtpSettings { Host = host, FromEmail = from };

            Assert.Equal(expected, settings.IsConfigured);
        }
    }

    public class SmtpEmailSenderTests
    {
        [Fact]
        public async Task Send_WithoutConfiguration_ReturnsFalseInsteadOfThrowing()
        {
            // Um alerta ou um laudo não pode falhar porque o e-mail não está configurado.
            var sender = new SmtpEmailSender(
                Options.Create(new SmtpSettings()),
                NullLogger<SmtpEmailSender>.Instance);

            Assert.False(await sender.SendAsync("p@teste.com", "assunto", "<p>corpo</p>"));
        }

        [Fact]
        public async Task Send_WithoutRecipient_ReturnsFalse()
        {
            var sender = new SmtpEmailSender(
                Options.Create(new SmtpSettings { Host = "smtp.teste.com", FromEmail = "no-reply@agripeweb.com" }),
                NullLogger<SmtpEmailSender>.Instance);

            Assert.False(await sender.SendAsync("", "assunto", "<p>corpo</p>"));
        }
    }

    public class AlertEmailServiceTests
    {
        private static (AlertEmailService service, Mock<IEmailSender> sender) Build(User? user)
        {
            var users = new Mock<IMongoCollection<User>>();
            MongoMockHelper.SetupFindList(users, user is null ? [] : [user]);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.Users).Returns(users.Object);

            var sender = new Mock<IEmailSender>();

            return (new AlertEmailService(db.Object, sender.Object, NullLogger<AlertEmailService>.Instance), sender);
        }

        [Fact]
        public async Task Send_UsesTheProducerEmail()
        {
            var (service, sender) = Build(new User { Id = 3, Name = "Produtor", Email = "produtor@teste.com" });

            await service.SendIrrigationAlertAsync(1, 3, "Pivô Sede", 30m, 22m, 25m);

            sender.Verify(s => s.SendAsync(
                "produtor@teste.com",
                It.Is<string>(subject => subject.Contains("Pivô Sede")),
                It.Is<string>(body => body.Contains("22") && body.Contains("25")),
                It.IsAny<IEnumerable<EmailAttachment>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Send_UserWithoutEmail_DoesNotSend()
        {
            var (service, sender) = Build(new User { Id = 3, Name = "Produtor", Email = "" });

            await service.SendIrrigationAlertAsync(1, 3, "Pivô Sede", 30m, 22m, 25m);

            sender.Verify(s => s.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<EmailAttachment>>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    public class DiagnosisEmailServiceTests
    {
        private static PlantDiagnosis Signed() => new()
        {
            Id = 42,
            UserId = 3,
            Status = PlantDiagnosisStatus.Signed,
            ImageFileId = ObjectId.GenerateNewId(),
            ConfirmedDisease = "Alternaria solani",
            Prescription = "Receituário emitido em separado.",
            AgronomistReportMarkdown = "## Laudo",
            Signature = new PlantDiagnosisSignature
            {
                AgronomistId = 4,
                AgronomistName = "Eng. Agr. Fulano",
                Crea = "CREA-RS 12345",
                SignedAt = DateTime.UtcNow,
                ContentSha256 = "abc"
            }
        };

        private static (DiagnosisEmailService service, Mock<IEmailSender> sender) Build(User? producer)
        {
            var users = new Mock<IMongoCollection<User>>();
            MongoMockHelper.SetupFindList(users, producer is null ? [] : [producer]);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.Users).Returns(users.Object);

            var sender = new Mock<IEmailSender>();

            var pdf = new Mock<IDiagnosisPdfService>();
            pdf.Setup(p => p.Generate(It.IsAny<PlantDiagnosis>(), It.IsAny<string>(), It.IsAny<byte[]>()))
                .Returns([0x25, 0x50, 0x44, 0x46]);

            var store = new Mock<IDiagnosisImageStore>();
            store.Setup(s => s.DownloadAsync(It.IsAny<ObjectId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([1, 2, 3]);

            var service = new DiagnosisEmailService(
                db.Object, sender.Object, pdf.Object, store.Object,
                NullLogger<DiagnosisEmailService>.Instance);

            return (service, sender);
        }

        [Fact]
        public async Task Send_AttachesTheSignedPdf()
        {
            // O PDF é o entregável: é o que o produtor guarda e imprime.
            var (service, sender) = Build(new User { Id = 3, Name = "Produtor", Email = "produtor@teste.com" });

            await service.SendSignedReportAsync(Signed(), CancellationToken.None);

            sender.Verify(s => s.SendAsync(
                "produtor@teste.com",
                It.Is<string>(subject => subject.Contains("42") && subject.Contains("Alternaria")),
                It.IsAny<string>(),
                It.Is<IEnumerable<EmailAttachment>>(a =>
                    a.Single().FileName == "laudo-42.pdf" && a.Single().ContentType == "application/pdf"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Send_BodyCarriesTheSignatureAndTheDisclaimer()
        {
            string? body = null;

            var (service, sender) = Build(new User { Id = 3, Name = "Produtor", Email = "produtor@teste.com" });
            sender.Setup(s => s.SendAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<IEnumerable<EmailAttachment>>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, string, IEnumerable<EmailAttachment>?, CancellationToken>(
                    (_, _, html, _, _) => body = html)
                .ReturnsAsync(true);

            await service.SendSignedReportAsync(Signed(), CancellationToken.None);

            Assert.NotNull(body);
            Assert.Contains("Eng. Agr. Fulano", body);
            Assert.Contains("CREA-RS 12345", body);
            Assert.Contains("receituário agronômico", body);
        }

        [Fact]
        public async Task Send_ProducerWithoutEmail_DoesNotSend()
        {
            var (service, sender) = Build(new User { Id = 3, Name = "Produtor", Email = "" });

            await service.SendSignedReportAsync(Signed(), CancellationToken.None);

            sender.Verify(s => s.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<EmailAttachment>>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    public class EmailTemplatesTests
    {
        [Fact]
        public void Escape_NeutralisesHtmlFromUserContent()
        {
            // O nome do pivô e a prescrição vêm de texto livre e vão para dentro de um HTML.
            var html = EmailTemplates.Escape("<script>alert(1)</script>");

            Assert.DoesNotContain("<script>", html);
            Assert.Contains("&lt;script&gt;", html);
        }

        [Fact]
        public void Wrap_EscapesTheTitle()
        {
            var html = EmailTemplates.Wrap("<b>x</b>", "<p>conteúdo</p>");

            Assert.DoesNotContain("<b>x</b>", html);
            Assert.Contains("<p>conteúdo</p>", html);   // o conteúdo já vem montado
        }
    }
}
