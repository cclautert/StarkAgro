using StarkAgroAPI.Models.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text.RegularExpressions;

namespace StarkAgroAPI.Services.Diagnosis
{
    public interface IDiagnosisPdfService
    {
        byte[] Generate(PlantDiagnosis diagnosis, string? producerName, byte[]? image);
    }

    /// <summary>
    /// Gera o PDF do laudo — o documento que o produtor guarda, imprime e leva ao banco.
    /// <para>
    /// O rodapé traz o <b>SHA-256 do conteúdo assinado</b>: é o que permite provar, depois, que
    /// o texto não mudou desde a assinatura.
    /// </para>
    /// </summary>
    public class DiagnosisPdfService : IDiagnosisPdfService
    {
        private const string Green = "#1B5E20";
        private const string Muted = "#5F6B60";
        private const string Line = "#DCE3DC";
        private const string Amber = "#8A6410";

        public byte[] Generate(PlantDiagnosis diagnosis, string? producerName, byte[]? image)
        {
            // O laudo assinado é a fonte da verdade; se ainda não houver assinatura, sai a
            // pré-análise da IA — sempre marcada como tal.
            var body = diagnosis.AgronomistReportMarkdown ?? diagnosis.AiReportMarkdown ?? string.Empty;
            var isSigned = diagnosis.Signature is not null;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.6f, Unit.Centimetre);
                    page.DefaultTextStyle(t => t.FontSize(10).FontColor(Colors.Grey.Darken4));

                    page.Header().Element(e => Header(e, diagnosis, producerName, isSigned));
                    page.Content().PaddingVertical(12).Element(e => Content(e, diagnosis, body, image));
                    page.Footer().Element(e => Footer(e, diagnosis));
                });
            });

            return document.GeneratePdf();
        }

        private static void Header(IContainer container, PlantDiagnosis d, string? producerName, bool isSigned)
        {
            container.Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Text("StarkAgro").FontSize(9).LetterSpacing(0.2f)
                            .FontColor(Muted).SemiBold();
                        left.Item().Text($"Laudo Fitossanitário #{d.Id}")
                            .FontSize(17).Bold().FontColor(Green);
                    });

                    row.ConstantItem(110).AlignRight().AlignMiddle().Text(text =>
                    {
                        text.Span(StatusLabel(isSigned))
                            .FontSize(8).Bold().LetterSpacing(0.1f)
                            .FontColor(isSigned ? Green : Amber);
                    });
                });

                column.Item().PaddingTop(6).LineHorizontal(1).LineColor(Line);

                column.Item().PaddingTop(6).Row(row =>
                {
                    Field(row, "Produtor", producerName ?? "—");
                    Field(row, "Pivô", d.ContextSnapshot?.PivotName ?? "—");
                    Field(row, "Cultura", d.CropName ?? "—");
                    Field(row, "Data da foto", d.CapturedAt.ToString("dd/MM/yyyy HH:mm"));
                });
            });
        }

        private static void Field(RowDescriptor row, string label, string value)
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(label.ToUpperInvariant()).FontSize(6.5f).FontColor(Muted).SemiBold();
                col.Item().Text(value).FontSize(9);
            });
        }

        private static void Content(IContainer container, PlantDiagnosis d, string body, byte[]? image)
        {
            container.Column(column =>
            {
                column.Spacing(10);

                if (image is not null)
                {
                    column.Item().Row(row =>
                    {
                        row.ConstantItem(150).Height(110).Image(image).FitArea();

                        row.RelativeItem().PaddingLeft(12).Column(col =>
                        {
                            col.Item().Text("Diagnóstico provável").FontSize(11).Bold().FontColor(Green);
                            col.Item().PaddingTop(4).Element(e => Diseases(e, d));
                        });
                    });
                }
                else
                {
                    column.Item().Text("Diagnóstico provável").FontSize(11).Bold().FontColor(Green);
                    column.Item().Element(e => Diseases(e, d));
                }

                if (d.ContextSnapshot is not null)
                {
                    column.Item().Element(e => Context(e, d.ContextSnapshot));
                }

                column.Item().PaddingTop(4).Element(e => Markdown(e, body));

                if (!string.IsNullOrWhiteSpace(d.Prescription))
                {
                    column.Item().Element(e => Prescription(e, d.Prescription!));
                }

                if (d.Signature is not null)
                {
                    column.Item().PaddingTop(8).Element(e => Signature(e, d.Signature));
                }
            });
        }

        private static void Diseases(IContainer container, PlantDiagnosis d)
        {
            if (d.Diseases.Count == 0)
            {
                container.Text("Nenhuma doença identificada com confiança.").FontColor(Muted).FontSize(9);
                return;
            }

            container.Column(column =>
            {
                column.Spacing(3);

                foreach (var disease in d.Diseases.Take(3))
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem(2).Text(text =>
                        {
                            text.Span(disease.Name).FontSize(9).SemiBold();
                            if (!string.IsNullOrWhiteSpace(disease.ScientificName))
                                text.Span($"  {disease.ScientificName}").FontSize(7.5f).Italic().FontColor(Muted);
                        });

                        row.ConstantItem(40).AlignRight()
                            .Text(ProbabilityFormatter.ToPercent(disease.Probability))
                            .FontSize(9).Bold().FontColor(Green);
                    });
                }

                if (d.ConfirmedDisease is not null)
                {
                    column.Item().PaddingTop(4).Text(text =>
                    {
                        text.Span("Confirmado pelo agrônomo: ").FontSize(8.5f).FontColor(Muted);
                        text.Span(d.ConfirmedDisease).FontSize(8.5f).SemiBold();
                    });
                }
            });
        }

        private static void Context(IContainer container, PlantDiagnosisContextSnapshot ctx)
        {
            container
                .Background("#F2F7F2").Border(1).BorderColor("#D5E5D6")
                .Padding(8)
                .Column(column =>
                {
                    column.Item().Text("Contexto da lavoura no momento do laudo")
                        .FontSize(8).Bold().FontColor(Green);

                    column.Item().PaddingTop(3).Row(row =>
                    {
                        if (ctx.MoistureAvg7d.HasValue)
                            Field(row, "Umidade média 7d", $"{ctx.MoistureAvg7d:0.0}%");

                        if (ctx.LimiteSuperior.HasValue)
                            Field(row, "Faixa ideal", $"{ctx.LimiteInferior:0}–{ctx.LimiteSuperior:0}%");

                        if (ctx.DaysAboveUpperLimit > 0)
                            Field(row, "Dias acima do limite", ctx.DaysAboveUpperLimit.ToString());

                        if (ctx.OpenAnomalies > 0)
                            Field(row, "Anomalias abertas", ctx.OpenAnomalies.ToString());
                    });

                    if (!string.IsNullOrWhiteSpace(ctx.ForecastSummary))
                    {
                        column.Item().PaddingTop(4)
                            .Text(ctx.ForecastSummary).FontSize(8).FontColor(Muted);
                    }
                });
        }

        private static void Prescription(IContainer container, string prescription)
        {
            container
                .Background("#FDF3DF").Border(1).BorderColor("#F0DFB0")
                .Padding(8)
                .Column(column =>
                {
                    column.Item().Text("Prescrição do agrônomo").FontSize(8).Bold().FontColor(Amber);
                    column.Item().PaddingTop(2).Text(prescription).FontSize(9);
                });
        }

        private static void Signature(IContainer container, PlantDiagnosisSignature signature)
        {
            container
                .BorderTop(1).BorderColor(Line)
                .PaddingTop(8)
                .Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("Assinado por ").FontSize(9).FontColor(Muted);
                            text.Span(signature.AgronomistName).FontSize(9).Bold();
                        });

                        if (!string.IsNullOrWhiteSpace(signature.Crea))
                            col.Item().Text(signature.Crea).FontSize(8.5f).FontColor(Muted);

                        col.Item().Text($"Em {signature.SignedAt:dd/MM/yyyy 'às' HH:mm} (UTC)")
                            .FontSize(8).FontColor(Muted);
                    });
                });
        }

        /// <summary>
        /// Renderiza o subconjunto de markdown que o laudo usa: títulos, listas, negrito e itálico.
        /// Não é um parser completo — é o que o prompt gera.
        /// </summary>
        private static void Markdown(IContainer container, string markdown)
        {
            container.Column(column =>
            {
                column.Spacing(3);

                foreach (var raw in markdown.Split('\n'))
                {
                    var line = raw.TrimEnd();

                    if (string.IsNullOrWhiteSpace(line) || line.Trim() == "---") continue;

                    // O disclaimer vem no fim do markdown (o prompt pede, e o EnsureDisclaimer
                    // garante). No PDF ele é o rodapé de toda página — repeti-lo no corpo é ruído.
                    if (IsDisclaimer(line)) continue;

                    if (line.StartsWith("### "))
                    {
                        column.Item().PaddingTop(4).Text(Inline(line[4..]))
                            .FontSize(9.5f).Bold().FontColor(Green);
                    }
                    else if (line.StartsWith("## "))
                    {
                        column.Item().PaddingTop(6).Text(Inline(line[3..]))
                            .FontSize(11).Bold().FontColor(Green);
                    }
                    else if (line.StartsWith("# "))
                    {
                        column.Item().PaddingTop(6).Text(Inline(line[2..]))
                            .FontSize(12).Bold().FontColor(Green);
                    }
                    else if (Regex.IsMatch(line, @"^\s*[-*]\s+"))
                    {
                        var text = Regex.Replace(line, @"^\s*[-*]\s+", string.Empty);
                        column.Item().PaddingLeft(10).Text($"• {Inline(text)}").FontSize(9.5f).LineHeight(1.35f);
                    }
                    else if (Regex.IsMatch(line, @"^\s*\d+\.\s+"))
                    {
                        column.Item().PaddingLeft(6).Text(Inline(line.Trim())).FontSize(9.5f).LineHeight(1.35f);
                    }
                    else
                    {
                        column.Item().Text(Inline(line)).FontSize(9.5f).LineHeight(1.35f);
                    }
                }
            });
        }

        /// <summary>
        /// Reconhece a linha de aviso legal no corpo do laudo. Tolerante a acento e a variações
        /// do modelo — o que importa é não imprimir o mesmo aviso duas vezes na mesma página.
        /// </summary>
        public static bool IsDisclaimer(string line)
        {
            var clean = Inline(line).ToLowerInvariant();

            return (clean.Contains("receituário") || clean.Contains("receituario"))
                   && clean.Contains("art")
                   && (clean.Contains("não constitui") || clean.Contains("nao constitui"));
        }

        /// <summary>Tira a marcação inline: o QuestPDF não interpreta markdown dentro do texto.</summary>
        private static string Inline(string text)
        {
            text = Regex.Replace(text, @"\*\*(.+?)\*\*", "$1");
            text = Regex.Replace(text, @"\*(.+?)\*", "$1");
            text = Regex.Replace(text, @"`(.+?)`", "$1");
            text = Regex.Replace(text, @"_(.+?)_", "$1");
            return text.Trim();
        }

        public const string Disclaimer =
            "Laudo técnico informativo. Não constitui receituário agronômico nem ART.";

        /// <summary>
        /// Selo do topo. Um documento sem assinatura não pode se passar por laudo assinado.
        /// </summary>
        public static string StatusLabel(bool isSigned) => isSigned ? "ASSINADO" : "PRÉ-ANÁLISE";

        /// <summary>
        /// Linhas do rodapé.
        /// <para>
        /// Público porque o texto renderizado no PDF <b>não é verificável nos bytes</b>: o QuestPDF
        /// embute a fonte como subset e escreve os glifos por id, não em ASCII. Testar o conteúdo
        /// aqui é a forma honesta de garantir que o disclaimer e o hash de integridade saem no
        /// documento — o <see cref="Footer"/> consome exatamente estas linhas.
        /// </para>
        /// </summary>
        public static IReadOnlyList<string> FooterLines(PlantDiagnosis d)
        {
            var lines = new List<string> { Disclaimer };

            if (d.Signature is not null)
            {
                // Prova de integridade: confere o hash e você sabe que o texto é o mesmo que
                // foi assinado.
                lines.Add($"SHA-256 do conteúdo assinado: {d.Signature.ContentSha256}");
            }

            return lines;
        }

        private static void Footer(IContainer container, PlantDiagnosis d)
        {
            var lines = FooterLines(d);

            container.Column(column =>
            {
                column.Item().LineHorizontal(1).LineColor(Line);

                column.Item().PaddingTop(4).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(lines[0]).FontSize(7).Italic().FontColor(Muted);

                        foreach (var extra in lines.Skip(1))
                            col.Item().Text(extra).FontSize(6).FontColor(Muted);
                    });

                    row.ConstantItem(60).AlignRight().Text(text =>
                    {
                        text.CurrentPageNumber().FontSize(7).FontColor(Muted);
                        text.Span(" / ").FontSize(7).FontColor(Muted);
                        text.TotalPages().FontSize(7).FontColor(Muted);
                    });
                });
            });
        }
    }
}
