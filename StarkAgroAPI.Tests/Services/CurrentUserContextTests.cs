using StarkAgroAPI.Services;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Claims;

namespace StarkAgroAPI.Tests.Services
{
    public class CurrentUserContextTests
    {
        [Fact]
        public void UserId_Authenticated_ReturnsParsedId()
        {
            // Arrange
            var claims = new[] { new Claim("id", "42") };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext { User = principal };
            var accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(httpContext);

            var context = new CurrentUserContext(accessor.Object);

            // Act
            var result = context.UserId;

            // Assert
            Assert.Equal(42, result);
        }

        [Fact]
        public void UserId_NotAuthenticated_ReturnsNull()
        {
            // Arrange
            var identity = new ClaimsIdentity(); // no authenticationType => IsAuthenticated = false
            var principal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext { User = principal };
            var accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(httpContext);

            var context = new CurrentUserContext(accessor.Object);

            // Act
            var result = context.UserId;

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void UserId_NullHttpContext_ReturnsNull()
        {
            // Arrange
            var accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);

            var context = new CurrentUserContext(accessor.Object);

            // Act
            var result = context.UserId;

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void UserId_NameIdentifierClaim_FallsBack()
        {
            // Arrange
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "99") };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext { User = principal };
            var accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(httpContext);

            var context = new CurrentUserContext(accessor.Object);

            // Act
            var result = context.UserId;

            // Assert
            Assert.Equal(99, result);
        }

        [Fact]
        public void UserId_CachesResult()
        {
            // Arrange
            var claims = new[] { new Claim("id", "7") };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext { User = principal };
            var accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(httpContext);

            var context = new CurrentUserContext(accessor.Object);

            // Act
            var first = context.UserId;
            var second = context.UserId;

            // Assert
            Assert.Equal(first, second);
            Assert.Equal(7, first);
            // HttpContext is accessed during resolution, but _resolved flag prevents re-resolution
            // Both calls return the same cached value
        }

        [Fact]
        public void IsAuthenticated_WhenUserId_ReturnsTrue()
        {
            // Arrange
            var claims = new[] { new Claim("id", "1") };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext { User = principal };
            var accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(httpContext);

            var context = new CurrentUserContext(accessor.Object);

            // Act
            var result = context.IsAuthenticated;

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsAuthenticated_WhenNoUserId_ReturnsFalse()
        {
            // Arrange
            var accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);

            var context = new CurrentUserContext(accessor.Object);

            // Act
            var result = context.IsAuthenticated;

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Constructor_NullAccessor_Throws()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new CurrentUserContext(null!));
        }
    }
}
