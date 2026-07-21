using StarkAgroAPI.Models.Entities;

namespace StarkAgroAPI.Tests.Models.Entities
{
    public class UserRoleTests
    {
        [Fact]
        public void IsAdmin_TrueWhenRolesContainAdmin()
        {
            var user = new User { Roles = { UserRole.Admin } };

            Assert.True(user.IsAdmin);
            Assert.False(user.IsAgronomist);
            Assert.False(user.IsResellerManager);
        }

        [Fact]
        public void IsAgronomist_TrueWhenRolesContainAgronomist()
        {
            var user = new User { Roles = { UserRole.Agronomist } };

            Assert.True(user.IsAgronomist);
            Assert.False(user.IsAdmin);
        }

        [Fact]
        public void IsResellerManager_TrueWhenRolesContainResellerManager()
        {
            var user = new User { Roles = { UserRole.ResellerManager } };

            Assert.True(user.IsResellerManager);
        }

        [Fact]
        public void ComputedFlags_FalseWhenRolesEmpty()
        {
            var user = new User();

            Assert.False(user.IsAdmin);
            Assert.False(user.IsAgronomist);
            Assert.False(user.IsResellerManager);
        }

        [Fact]
        public void SetRole_EnabledTrue_AddsRoleWithoutDuplicating()
        {
            var user = new User();

            user.SetRole(UserRole.Admin, true);
            user.SetRole(UserRole.Admin, true);

            Assert.Single(user.Roles);
            Assert.Contains(UserRole.Admin, user.Roles);
            Assert.True(user.IsAdmin);
        }

        [Fact]
        public void SetRole_EnabledFalse_RemovesRole()
        {
            var user = new User { Roles = { UserRole.Admin, UserRole.Agronomist } };

            user.SetRole(UserRole.Admin, false);

            Assert.DoesNotContain(UserRole.Admin, user.Roles);
            Assert.Contains(UserRole.Agronomist, user.Roles);
            Assert.False(user.IsAdmin);
            Assert.True(user.IsAgronomist);
        }

        [Fact]
        public void SetRole_EnabledFalse_OnAbsentRole_IsNoOp()
        {
            var user = new User { Roles = { UserRole.Agronomist } };

            user.SetRole(UserRole.Admin, false);

            Assert.Single(user.Roles);
            Assert.Contains(UserRole.Agronomist, user.Roles);
        }
    }
}
