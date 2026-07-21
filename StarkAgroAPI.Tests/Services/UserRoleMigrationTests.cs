using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Services;
using MongoDB.Bson;

namespace StarkAgroAPI.Tests.Services
{
    public class UserRoleMigrationTests
    {
        [Fact]
        public void DeriveRoles_LegacyAdmin_YieldsAdminRole()
        {
            var doc = new BsonDocument { { "IsAdmin", true }, { "IsAgronomist", false } };

            var roles = UserRoleMigration.DeriveRoles(doc);

            Assert.Contains(UserRole.Admin, roles.Select(v => v.AsString));
            Assert.DoesNotContain(UserRole.Agronomist, roles.Select(v => v.AsString));
        }

        [Fact]
        public void DeriveRoles_LegacyAgronomist_YieldsAgronomistRole()
        {
            var doc = new BsonDocument { { "IsAdmin", false }, { "IsAgronomist", true } };

            var roles = UserRoleMigration.DeriveRoles(doc);

            Assert.Contains(UserRole.Agronomist, roles.Select(v => v.AsString));
            Assert.DoesNotContain(UserRole.Admin, roles.Select(v => v.AsString));
        }

        [Fact]
        public void DeriveRoles_LegacyBothTrue_YieldsBothRoles()
        {
            var doc = new BsonDocument { { "IsAdmin", true }, { "IsAgronomist", true } };

            var roles = UserRoleMigration.DeriveRoles(doc).Select(v => v.AsString).ToList();

            Assert.Contains(UserRole.Admin, roles);
            Assert.Contains(UserRole.Agronomist, roles);
            Assert.Equal(2, roles.Count);
        }

        [Fact]
        public void DeriveRoles_NoLegacyFlags_YieldsEmpty()
        {
            var doc = new BsonDocument { { "IsAdmin", false }, { "IsAgronomist", false } };

            Assert.Empty(UserRoleMigration.DeriveRoles(doc));
        }

        [Fact]
        public void DeriveRoles_MissingLegacyFields_YieldsEmpty()
        {
            var doc = new BsonDocument { { "Name", "Someone" } };

            Assert.Empty(UserRoleMigration.DeriveRoles(doc));
        }
    }
}
