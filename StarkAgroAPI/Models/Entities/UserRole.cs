namespace StarkAgroAPI.Models.Entities
{
    /// <summary>
    /// Papéis de um <see cref="User"/>. São strings numa lista (<see cref="User.Roles"/>) em vez de
    /// booleans paralelos — o 3º papel (gestor de revenda) tornou a dívida registrada no antigo
    /// <c>IsAgronomist</c> concreta, então colapsamos tudo aqui de uma vez.
    /// </summary>
    public static class UserRole
    {
        public const string Admin = "Admin";
        public const string Agronomist = "Agronomist";
        public const string ResellerManager = "ResellerManager";
    }
}
