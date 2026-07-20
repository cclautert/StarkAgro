using StarkAgroAPI.Models.Entities;
using MongoDB.Bson;

namespace StarkAgroAPI.Services
{
    /// <summary>
    /// Converte os campos booleanos legados (<c>IsAdmin</c>/<c>IsAgronomist</c>) de um documento de
    /// usuário gravado no formato antigo para a lista <see cref="User.Roles"/>. Isolado do plumbing
    /// Mongo para ser testável — um erro no nome do campo apagaria os papéis de todo mundo.
    /// </summary>
    public static class UserRoleMigration
    {
        public static BsonArray DeriveRoles(BsonDocument legacyUser)
        {
            var roles = new BsonArray();
            if (legacyUser.TryGetValue("IsAdmin", out var isAdmin) && isAdmin.ToBoolean())
                roles.Add(UserRole.Admin);
            if (legacyUser.TryGetValue("IsAgronomist", out var isAgronomist) && isAgronomist.ToBoolean())
                roles.Add(UserRole.Agronomist);
            return roles;
        }
    }
}
