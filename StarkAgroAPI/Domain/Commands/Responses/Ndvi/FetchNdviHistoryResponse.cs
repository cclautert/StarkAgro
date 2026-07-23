namespace StarkAgroAPI.Domain.Commands.Responses.Ndvi
{
    /// <summary>
    /// Resultado da busca retroativa sob demanda. Enxuto de propósito: não reconstrói pontos de
    /// tendência — o front, no sucesso, recarrega <c>/trend</c> (a passagem agora está armazenada) e
    /// seleciona a data devolvida. Evita duplicar o mapeamento de <c>NdviTrendPoint</c>.
    /// </summary>
    public class FetchNdviHistoryResponse
    {
        /// <summary>Datas (yyyy-MM-dd) das passagens encontradas na janela — gravadas agora ou já existentes.</summary>
        public List<string> AcquisitionDates { get; set; } = [];

        /// <summary><c>true</c> quando foi preciso chamar a CDSE (gastou PU); <c>false</c> = já estava no banco.</summary>
        public bool FetchedFromCdse { get; set; }

        /// <summary>Data (yyyy-MM-dd) da passagem mais próxima da pedida — a que o front deve selecionar. Null se a janela veio vazia.</summary>
        public string? NearestDate { get; set; }
    }
}
