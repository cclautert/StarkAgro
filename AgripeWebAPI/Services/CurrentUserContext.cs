using AgripeWebAPI.Models.Interfaces;
using System.Security.Claims;

namespace AgripeWebAPI.Services
{
    /// <summary>
    /// Default implementation of ICurrentUserContext backed by IHttpContextAccessor.
    /// </summary>
    public sealed class CurrentUserContext : ICurrentUserContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private bool _resolved;
        private int? _userId;
        private bool _isAdminResolved;
        private bool _isAdmin;

        public CurrentUserContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public int? UserId
        {
            get
            {
                if (!_resolved)
                {
                    var httpContext = _httpContextAccessor.HttpContext;
                    var user = httpContext?.User;

                    if (user?.Identity?.IsAuthenticated == true)
                    {
                        // Primary claim used in this project is "id"
                        var claim = user.FindFirst("id") ?? user.FindFirst(ClaimTypes.NameIdentifier);
                        if (claim != null && int.TryParse(claim.Value, out var parsed))
                        {
                            _userId = parsed;
                        }
                    }

                    _resolved = true;
                }

                return _userId;
            }
        }

        public bool IsAuthenticated => UserId.HasValue;

        public bool IsAdmin
        {
            get
            {
                if (!_isAdminResolved)
                {
                    var httpContext = _httpContextAccessor.HttpContext;
                    var user = httpContext?.User;

                    if (user?.Identity?.IsAuthenticated == true)
                    {
                        var claim = user.FindFirst("isAdmin");
                        _isAdmin = claim?.Value == "true";
                    }

                    _isAdminResolved = true;
                }

                return _isAdmin;
            }
        }
    }
}

