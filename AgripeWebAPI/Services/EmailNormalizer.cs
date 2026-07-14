using AgripeWebAPI.Models.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace AgripeWebAPI.Services
{
    /// <summary>
    /// E-mail não diferencia maiúsculas de minúsculas — mas o Mongo, sim.
    /// <para>
    /// Comparar por igualdade exata quebrava o produto de três formas: quem se cadastrou com
    /// <c>Fulano@Fazenda.com</c> <b>não conseguia logar</b> digitando minúsculo; o login com
    /// Google (que sempre devolve o e-mail minúsculo) <b>criaria um usuário duplicado</b>; e a
    /// checagem de e-mail repetido deixava passar dois cadastros que diferem só na caixa.
    /// </para>
    /// <para>
    /// Escreva sempre normalizado (<see cref="Normalize"/>); leia sempre sem diferenciar caixa
    /// (<see cref="ByEmail"/>), para que os cadastros antigos, gravados com maiúsculas,
    /// continuem funcionando sem migração de dados.
    /// </para>
    /// </summary>
    public static class EmailNormalizer
    {
        public static string Normalize(string? email)
            => (email ?? string.Empty).Trim().ToLowerInvariant();

        /// <summary>Filtro por e-mail que ignora a caixa das letras.</summary>
        public static FilterDefinition<User> ByEmail(string? email)
        {
            var pattern = $"^{Regex.Escape(Normalize(email))}$";

            return Builders<User>.Filter.Regex(
                u => u.Email,
                new BsonRegularExpression(pattern, "i"));
        }

        /// <summary>Filtro por e-mail que ignora a caixa e exclui um id (checagem de duplicidade ao editar).</summary>
        public static FilterDefinition<User> ByEmailExcluding(string? email, int userId)
            => ByEmail(email) & Builders<User>.Filter.Ne(u => u.Id, userId);
    }
}
