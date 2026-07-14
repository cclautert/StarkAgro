using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Services.Diagnosis;
using System.Text;

namespace AgripeWebAPI.Tests.Services
{
    public class DiagnosisPdfServiceTests
    {
        public DiagnosisPdfServiceTests()
        {
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        }

        private static PlantDiagnosis Signed() => new()
        {
            Id = 42,
            UserId = 3,
            CropName = "tomate",
            CapturedAt = new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc),
            Status = PlantDiagnosisStatus.Signed,
            TopProbability = 0.78,
            Diseases =
            [
                new PlantDiseaseSuggestion
                {
                    Name = "Pinta-preta",
                    ScientificName = "Alternaria solani",
                    Probability = 0.78
                }
            ],
            ContextSnapshot = new PlantDiagnosisContextSnapshot
            {
                PivotName = "Pivô Sede",
                MoistureAvg7d = 88.2m,
                LimiteInferior = 40m,
                LimiteSuperior = 75m,
                DaysAboveUpperLimit = 7,
                ForecastSummary = "Precipitação total prevista: 59,7 mm"
            },
            AiReportMarkdown = "## Identificação\n\nTexto **da IA**.",
            AgronomistReportMarkdown = "## Identificação\n\nTexto assinado pelo agrônomo.\n\n- item de manejo",
            ConfirmedDisease = "Alternaria solani",
            Prescription = "Receituário emitido em separado.",
            Signature = new PlantDiagnosisSignature
            {
                AgronomistId = 4,
                AgronomistName = "Eng. Agr. Fulano",
                Crea = "CREA-RS 12345",
                SignedAt = new DateTime(2026, 7, 13, 15, 42, 0, DateTimeKind.Utc),
                ContentSha256 = "85d8d14b0267337b1f2c3d4e5f60718293a4b5c6d7e8f90123456789abcdef01"
            }
        };

        /// <summary>JPEG mínimo válido, para o QuestPDF conseguir decodificar a imagem.</summary>
        private static byte[] TinyJpeg() => Convert.FromBase64String(
            "/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0a" +
            "HBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/wAALCAABAAEBAREA/8QAHwAAAQUBAQEB" +
            "AQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1Fh" +
            "ByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZ" +
            "WmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXG" +
            "x8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/9oACAEBAAA/APn+iiiiiiiiiiv/2Q==");

        [Fact]
        public void Generate_ProducesAValidPdf()
        {
            var service = new DiagnosisPdfService();

            var pdf = service.Generate(Signed(), "Produtor João", TinyJpeg());

            Assert.NotEmpty(pdf);
            Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        }

        [Fact]
        public void Generate_WithoutImage_StillProducesPdf()
        {
            // A foto pode ter sumido do GridFS — o laudo continua sendo um documento válido.
            var service = new DiagnosisPdfService();

            var pdf = service.Generate(Signed(), "Produtor João", image: null);

            Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        }

        [Fact]
        public void Generate_UnsignedDiagnosis_DoesNotCrash()
        {
            var service = new DiagnosisPdfService();
            var diagnosis = Signed();
            diagnosis.Signature = null;
            diagnosis.AgronomistReportMarkdown = null;
            diagnosis.Status = PlantDiagnosisStatus.AiCompleted;

            var pdf = service.Generate(diagnosis, "Produtor João", TinyJpeg());

            Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        }

        // Os testes abaixo checam o CONTEÚDO do rodapé, e não os bytes do PDF: o QuestPDF embute
        // a fonte como subset e escreve os glifos por id, então procurar texto no arquivo gerado
        // não prova nada. O Footer() consome exatamente estas mesmas funções.

        [Fact]
        public void FooterLines_CarryTheContentHash()
        {
            // O hash é a prova de integridade: confere e você sabe que o texto é o mesmo que
            // foi assinado.
            var diagnosis = Signed();

            var lines = DiagnosisPdfService.FooterLines(diagnosis);

            Assert.Contains(lines, l => l.Contains(diagnosis.Signature!.ContentSha256));
        }

        [Fact]
        public void FooterLines_AlwaysCarryTheLegalDisclaimer()
        {
            // O aviso legal não é decorativo: é o que separa este documento de um receituário.
            var unsigned = Signed();
            unsigned.Signature = null;

            Assert.Contains(DiagnosisPdfService.Disclaimer, DiagnosisPdfService.FooterLines(Signed()));
            Assert.Contains(DiagnosisPdfService.Disclaimer, DiagnosisPdfService.FooterLines(unsigned));

            Assert.Contains("receituário agronômico", DiagnosisPdfService.Disclaimer);
            Assert.Contains("ART", DiagnosisPdfService.Disclaimer);
        }

        [Fact]
        public void FooterLines_WithoutSignature_HaveNoHash()
        {
            var unsigned = Signed();
            unsigned.Signature = null;

            var lines = DiagnosisPdfService.FooterLines(unsigned);

            Assert.Single(lines);
            Assert.DoesNotContain(lines, l => l.Contains("SHA-256"));
        }

        [Fact]
        public void StatusLabel_UnsignedIsNeverPresentedAsSigned()
        {
            Assert.Equal("ASSINADO", DiagnosisPdfService.StatusLabel(isSigned: true));
            Assert.Equal("PRÉ-ANÁLISE", DiagnosisPdfService.StatusLabel(isSigned: false));
        }

        [Theory]
        [InlineData("_Laudo técnico informativo. Não constitui receituário agronômico nem ART._")]
        [InlineData("Laudo tecnico informativo. Nao constitui receituario agronomico nem ART.")]
        [InlineData("*Não constitui receituário agronômico nem ART.*")]
        public void IsDisclaimer_RecognisesTheLegalLineInTheBody(string line)
        {
            // O aviso já é o rodapé de toda página; repeti-lo no corpo é ruído no documento.
            Assert.True(DiagnosisPdfService.IsDisclaimer(line));
        }

        [Theory]
        [InlineData("## Recomendações de manejo")]
        [InlineData("- Suspender a irrigação até a umidade voltar à faixa ideal")]
        [InlineData("A prescrição depende de avaliação do agrônomo responsável.")]
        public void IsDisclaimer_DoesNotSwallowRealContent(string line)
        {
            Assert.False(DiagnosisPdfService.IsDisclaimer(line));
        }
    }
}
