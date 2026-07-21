using StarkAgroAPI.Services.Revenda;

namespace StarkAgroAPI.Domain.Commands.Responses.Revenda
{
    /// <summary>
    /// Ocupação de assentos da revenda. Serve para a tela de membros mostrar "X de Y" e não deixar
    /// o gestor bater na parede — a autoridade do bloqueio continua sendo o handler do convite.
    /// </summary>
    public class RevendaSeatsResponse
    {
        public int Used { get; set; }
        public int Included { get; set; }
        /// <summary>0 = ilimitado.</summary>
        public int Max { get; set; }
        public int Overage { get; set; }
        public bool IsUnlimited { get; set; }
        public bool IsFull { get; set; }

        public static RevendaSeatsResponse From(RevendaSeats s) => new()
        {
            Used = s.Used,
            Included = s.Included,
            Max = s.Max,
            Overage = s.Overage,
            IsUnlimited = s.IsUnlimited,
            IsFull = s.IsFull
        };
    }
}
