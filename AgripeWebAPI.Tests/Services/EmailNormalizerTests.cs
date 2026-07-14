using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Services;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace AgripeWebAPI.Tests.Services
{
    /// <summary>
    /// Regressão de um bug que quebrava o produto inteiro: e-mail não diferencia maiúsculas de
    /// minúsculas, mas o Mongo diferencia. Quem se cadastrava como "Fulano@Fazenda.com" não
    /// conseguia logar digitando minúsculo, e o login com Google (que sempre devolve o e-mail
    /// minúsculo) criaria um usuário duplicado.
    /// </summary>
    public class EmailNormalizerTests
    {
        [Theory]
        [InlineData("Produtor@Fazenda.com", "produtor@fazenda.com")]
        [InlineData("  ESPACO@teste.com  ", "espaco@teste.com")]
        [InlineData("ja@minusculo.com", "ja@minusculo.com")]
        [InlineData(null, "")]
        [InlineData("", "")]
        public void Normalize_LowercasesAndTrims(string? input, string expected)
        {
            Assert.Equal(expected, EmailNormalizer.Normalize(input));
        }

        // O que o filtro precisa aceitar e recusar. Os casos de recusa são o motivo de o padrão
        // ser ancorado e escapado — sem isso, "ana@x.com" acharia "mariana@x.com".
        [Theory]
        [InlineData("Produtor@Fazenda.com", "produtor@fazenda.com", true)]   // gravado minúsculo
        [InlineData("produtor@fazenda.com", "Produtor@Fazenda.com", true)]   // gravado maiúsculo
        [InlineData("produtor@fazenda.com", "PRODUTOR@FAZENDA.COM", true)]
        [InlineData("  Produtor@Fazenda.com ", "produtor@fazenda.com", true)]
        [InlineData("ana@x.com", "mariana@x.com", false)]                    // sem âncora, casaria
        [InlineData("ana@x.com", "ana@x.com.br", false)]                     // sem âncora, casaria
        [InlineData("a.b@x.com", "axb@x.com", false)]                        // sem escape, casaria
        [InlineData("produtor@fazenda.com", "outro@fazenda.com", false)]
        public void ByEmail_MatchesIgnoringCaseAndNothingElse(string query, string stored, bool expected)
        {
            var regex = RegexOf(EmailNormalizer.ByEmail(query));

            Assert.Equal(expected, regex.IsMatch(stored));
        }

        [Fact]
        public void ByEmailExcluding_KeepsTheUserOutOfItsOwnConflictCheck()
        {
            // Salvar um usuário sem trocar o e-mail não pode acusar "e-mail já em uso" com ele mesmo.
            var filter = EmailNormalizer.ByEmailExcluding("Produtor@Fazenda.com", 7);
            var rendered = Render(filter).ToString();

            Assert.Matches(RegexOf(filter), "produtor@fazenda.com");
            Assert.Contains("$ne", rendered);
            Assert.Contains("7", rendered);
        }

        /// <summary>
        /// Extrai o regex que o filtro manda para o Mongo e o reconstrói como regex do .NET, para
        /// que o teste asserte o comportamento real de casamento — não o texto do BSON.
        /// </summary>
        private static System.Text.RegularExpressions.Regex RegexOf(FilterDefinition<User> filter)
        {
            var bson = Render(filter);
            var value = FindRegex(bson)
                ?? throw new InvalidOperationException($"Nenhum regex no filtro: {bson}");

            var options = System.Text.RegularExpressions.RegexOptions.None;
            if (value.Options.Contains('i'))
                options |= System.Text.RegularExpressions.RegexOptions.IgnoreCase;

            return new System.Text.RegularExpressions.Regex(value.Pattern, options);
        }

        private static BsonRegularExpression? FindRegex(BsonValue value) => value switch
        {
            BsonRegularExpression regex => regex,
            BsonDocument doc => doc.Values.Select(FindRegex).FirstOrDefault(x => x is not null),
            BsonArray array => array.Select(FindRegex).FirstOrDefault(x => x is not null),
            _ => null
        };

        private static BsonDocument Render(FilterDefinition<User> filter)
        {
            var serializer = BsonSerializer.SerializerRegistry.GetSerializer<User>();
            return filter.Render(new RenderArgs<User>(serializer, BsonSerializer.SerializerRegistry));
        }
    }
}
