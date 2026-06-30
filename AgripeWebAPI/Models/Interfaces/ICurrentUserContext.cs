using System.Security.Claims;

namespace AgripeWebAPI.Models.Interfaces
{
    /// <summary>
    /// Provides information about the current authenticated user (multi-tenant context).
    /// Backed by the HTTP context / JWT claims.
    /// </summary>
    public interface ICurrentUserContext
    {
        /// <summary>
        /// Returns the current user id (tenant id) when authenticated, or null otherwise.
        /// </summary>
        int? UserId { get; }

        /// <summary>
        /// Indicates whether there is an authenticated user associated with the current request.
        /// </summary>
        bool IsAuthenticated { get; }

        /// <summary>
        /// Indicates whether the current user has administrator privileges.
        /// </summary>
        bool IsAdmin { get; }
    }
}

