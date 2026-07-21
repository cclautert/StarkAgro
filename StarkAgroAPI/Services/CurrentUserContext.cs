using StarkAgroAPI.Models.Interfaces;
using System.Linq;
using System.Security.Claims;

namespace StarkAgroAPI.Services
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
        private bool _isAgronomistResolved;
        private bool _isAgronomist;
        private bool _isResellerManagerResolved;
        private bool _isResellerManager;

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

        public bool IsAgronomist
        {
            get
            {
                if (!_isAgronomistResolved)
                {
                    var httpContext = _httpContextAccessor.HttpContext;
                    var user = httpContext?.User;

                    if (user?.Identity?.IsAuthenticated == true)
                    {
                        var claim = user.FindFirst("isAgronomist");
                        _isAgronomist = claim?.Value == "true";
                    }

                    _isAgronomistResolved = true;
                }

                return _isAgronomist;
            }
        }

        public bool IsResellerManager
        {
            get
            {
                if (!_isResellerManagerResolved)
                {
                    var httpContext = _httpContextAccessor.HttpContext;
                    var user = httpContext?.User;

                    if (user?.Identity?.IsAuthenticated == true)
                    {
                        var claim = user.FindFirst("isResellerManager");
                        _isResellerManager = claim?.Value == "true";
                    }

                    _isResellerManagerResolved = true;
                }

                return _isResellerManager;
            }
        }

        public bool HasRole(string role)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var user = httpContext?.User;

            if (user?.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            return user.FindAll("role").Any(c => c.Value == role);
        }
    }
}

