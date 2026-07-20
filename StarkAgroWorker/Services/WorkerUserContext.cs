using StarkAgroAPI.Models.Interfaces;

namespace StarkAgroWorker.Services
{
    /// <summary>
    /// ICurrentUserContext implementation for the worker process.
    /// Returns null UserId since there is no HTTP/JWT context.
    /// </summary>
    public sealed class WorkerUserContext : ICurrentUserContext
    {
        public int? UserId => null;
        public bool IsAuthenticated => false;
        public bool IsAdmin => false;
        public bool IsAgronomist => false;
    }
}
