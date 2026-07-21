using System.Security.Claims;

namespace StarkAgroAPI.Models.Interfaces
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

        /// <summary>
        /// Indicates whether the current user is an agronomist (reviews and signs reports
        /// for the producers linked to them).
        /// </summary>
        bool IsAgronomist { get; }

        /// <summary>
        /// Indicates whether the current user manages a revenda (reseller).
        /// </summary>
        bool IsResellerManager { get; }

        /// <summary>
        /// Returns true when the authenticated user carries the given role claim.
        /// </summary>
        bool HasRole(string role);
    }
}

