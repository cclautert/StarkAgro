using StarkAgroAPI.Models.Entities;

namespace StarkAgroAPI.Tests.Models.Entities
{
    public class RevendaMembershipTests
    {
        [Fact]
        public void Defaults_AreClientAndPending()
        {
            var m = new RevendaMembership();

            Assert.Equal(RevendaMemberRole.Client, m.MemberRole);
            Assert.Equal(RevendaMembershipStatus.Pending, m.Status);
            Assert.Equal(string.Empty, m.MemberEmail);
            Assert.Equal(string.Empty, m.InviteToken);
        }

        [Fact]
        public void AllFields_RoundTrip()
        {
            var now = DateTime.UtcNow;
            var m = new RevendaMembership
            {
                Id = 1,
                RevendaId = 2,
                MemberRole = RevendaMemberRole.Agronomist,
                MemberUserId = 3,
                MemberEmail = "a@b.com",
                Status = RevendaMembershipStatus.Revoked,
                InviteToken = "tok",
                InvitedAt = now,
                InviteExpiresAt = now.AddDays(7),
                AcceptedAt = now,
                RevokedAt = now,
                RevokedByUserId = 9,
                CreatedAt = now
            };

            Assert.Equal(3, m.MemberUserId);
            Assert.Equal(now, m.RevokedAt);
            Assert.Equal(9, m.RevokedByUserId);
            Assert.Equal(RevendaMembershipStatus.Revoked, m.Status);
        }
    }
}
