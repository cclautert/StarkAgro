using StarkAgroAPI.Services.Fire;
using StarkAgroAPI.Services.Ndvi;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using System.Net;

namespace StarkAgroAPI.Tests.Services.Fire
{
    public class FirmsHotspotServiceTests
    {
        // Cabeçalho real do VIIRS (ordem oficial da NASA).
        private const string Header =
            "latitude,longitude,bright_ti4,scan,track,acq_date,acq_time,satellite,instrument,confidence,version,bright_ti5,frp,daynight";

        [Fact]
        public void ParseCsv_LinhaReal_LeOFoco()
        {
            var csv = Header + "\n" +
                "-21.9647,-46.9057,320.1,0.4,0.36,2026-07-22,0742,N,VIIRS,n,2.0NRT,295.3,12.5,D";

            var f = Assert.Single(FirmsHotspotService.ParseCsv(csv));

            Assert.Equal(-21.9647, f.Latitude, 4);
            Assert.Equal(-46.9057, f.Longitude, 4);
            Assert.Equal(new DateTime(2026, 7, 22, 7, 42, 0, DateTimeKind.Utc), f.AcquiredAt);
            Assert.Equal("N", f.Satellite);
            Assert.Equal("n", f.Confidence);
            Assert.Equal(12.5, f.Frp, 4);
        }

        [Fact]
        public void ParseCsv_DadoRealDoFirms_Parseia()
        {
            // Linhas VERBATIM da resposta real do FIRMS (VIIRS_SNPP_NRT, 2026-07-22), incluindo o
            // acq_time de 3 dígitos ("306" = 03:06) e confidence em letra minúscula ("n"/"h") — os
            // dois detalhes que só o dado real revela. Cabeçalho é a ordem oficial de 14 colunas.
            var csv =
                "latitude,longitude,bright_ti4,scan,track,acq_date,acq_time,satellite,instrument,confidence,version,bright_ti5,frp,daynight\n" +
                "-20.24594,-40.24869,301.32,0.69,0.74,2026-07-22,306,N,VIIRS,n,2.0NRT,287.19,2.47,N\n" +
                "-20.23912,-40.24675,315.51,0.69,0.74,2026-07-22,306,N,VIIRS,h,2.0NRT,287.6,2.47,N";

            var r = FirmsHotspotService.ParseCsv(csv);

            Assert.Equal(2, r.Count);
            Assert.Equal(new DateTime(2026, 7, 22, 3, 6, 0, DateTimeKind.Utc), r[0].AcquiredAt); // "306" → 03:06
            Assert.Equal(-20.24594, r[0].Latitude, 5);
            Assert.Equal(-40.24869, r[0].Longitude, 5);
            Assert.Equal("N", r[0].Satellite);
            Assert.Equal("n", r[0].Confidence);   // letra minúscula, guardada crua
            Assert.Equal("h", r[1].Confidence);
            Assert.Equal(2.47, r[0].Frp, 2);
        }

        [Fact]
        public void ParseCsv_ColunasReordenadas_AindaLeCerto()
        {
            // Parse é por NOME de coluna: reordenar não pode desalinhar o resultado.
            var csv = "acq_time,satellite,longitude,latitude,acq_date,frp,confidence\n" +
                      "0800,1,-47.0,-22.0,2026-07-20,3.1,high";

            var f = Assert.Single(FirmsHotspotService.ParseCsv(csv));

            Assert.Equal(-22.0, f.Latitude, 4);
            Assert.Equal(-47.0, f.Longitude, 4);
            Assert.Equal("1", f.Satellite);
            Assert.Equal("high", f.Confidence);
            Assert.Equal(new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc), f.AcquiredAt);
        }

        [Fact]
        public void ParseCsv_AcqTimeSemZeroAEsquerda_Normaliza()
        {
            // "742" = 07:42; "5" = 00:05 — o FIRMS omite zeros à esquerda.
            var csv = Header + "\n" +
                "-21.9,-46.9,320,0.4,0.36,2026-07-22,742,N,VIIRS,n,2,295,1,D\n" +
                "-21.8,-46.8,320,0.4,0.36,2026-07-22,5,N,VIIRS,n,2,295,1,N";

            var r = FirmsHotspotService.ParseCsv(csv);

            Assert.Equal(2, r.Count);
            Assert.Equal(new DateTime(2026, 7, 22, 7, 42, 0, DateTimeKind.Utc), r[0].AcquiredAt);
            Assert.Equal(new DateTime(2026, 7, 22, 0, 5, 0, DateTimeKind.Utc), r[1].AcquiredAt);
        }

        [Fact]
        public void ParseCsv_CabecalhoAusente_DevolveVazio()
        {
            // Sem as colunas obrigatórias (linha de dados no lugar do header) → vazio, não lixo.
            var csv = "-21.9,-46.9,320,0.4,0.36,2026-07-22,0742,N,VIIRS,n,2,295,1,D";

            Assert.Empty(FirmsHotspotService.ParseCsv(csv));
        }

        [Fact]
        public void ParseCsv_ColunaObrigatoriaFaltando_DevolveVazio()
        {
            // Falta "longitude" — parsear por posição leria o campo errado; por nome, aborta.
            var csv = "latitude,acq_date,acq_time,satellite\n-21.9,2026-07-22,0742,N";

            Assert.Empty(FirmsHotspotService.ParseCsv(csv));
        }

        [Fact]
        public void ParseCsv_LinhaDesalinhada_EhPulada()
        {
            var csv = Header + "\n" +
                "-21.9,-46.9,320,0.4,0.36,2026-07-22,0742,N,VIIRS,n,2,295,1,D\n" +
                "faltam,campos,aqui";   // menos campos que o header → pulada

            Assert.Single(FirmsHotspotService.ParseCsv(csv));
        }

        [Fact]
        public void ParseCsv_LatOuDataInvalida_PulaSoAquelaLinha()
        {
            var csv = Header + "\n" +
                "NAO_E_NUMERO,-46.9,320,0.4,0.36,2026-07-22,0742,N,VIIRS,n,2,295,1,D\n" +
                "-21.9,-46.9,320,0.4,0.36,2026-13-99,0742,N,VIIRS,n,2,295,1,D\n" +
                "-21.9,-46.9,320,0.4,0.36,2026-07-22,0742,N,VIIRS,n,2,295,1,D";

            Assert.Single(FirmsHotspotService.ParseCsv(csv)); // só a 3ª é válida
        }

        [Fact]
        public void ParseCsv_Vazio_DevolveVazio()
        {
            Assert.Empty(FirmsHotspotService.ParseCsv(""));
            Assert.Empty(FirmsHotspotService.ParseCsv("   "));
            Assert.Empty(FirmsHotspotService.ParseCsv(Header)); // só cabeçalho, sem dados
        }

        [Fact]
        public async Task GetHotspots_Http200ComCsv_Parseia()
        {
            var svc = Service(HttpStatusCode.OK, Header + "\n-21.9,-46.9,320,0.4,0.36,2026-07-22,0742,N,VIIRS,n,2,295,1,D");

            var r = await svc.GetHotspotsAsync("key", "VIIRS_SNPP_NRT", Bbox(), 1, CancellationToken.None);

            Assert.NotNull(r);
            Assert.Single(r!);
        }

        [Fact]
        public async Task GetHotspots_HttpError_ReturnsNull()
        {
            var svc = Service(HttpStatusCode.BadRequest, "erro");

            Assert.Null(await svc.GetHotspotsAsync("key", "VIIRS_SNPP_NRT", Bbox(), 1, CancellationToken.None));
        }

        [Fact]
        public async Task GetHotspots_MapKeyInvalida_200ComTextoDeErro_ReturnsNull()
        {
            // O FIRMS devolve 200 com corpo de erro em texto quando a chave é inválida — sem
            // cabeçalho CSV. Tem que virar null, não uma lista vazia silenciosa.
            var svc = Service(HttpStatusCode.OK, "Invalid MAP_KEY. Please check your MAP_KEY.");

            Assert.Null(await svc.GetHotspotsAsync("bad", "VIIRS_SNPP_NRT", Bbox(), 1, CancellationToken.None));
        }

        [Fact]
        public async Task GetHotspots_TransportThrows_ReturnsNull()
        {
            var svc = Service(HttpStatusCode.OK, "", throws: new HttpRequestException("down"));

            Assert.Null(await svc.GetHotspotsAsync("key", "VIIRS_SNPP_NRT", Bbox(), 1, CancellationToken.None));
        }

        private static NdviBbox Bbox() => new(-47.0, -22.0, -46.9, -21.9);

        private static FirmsHotspotService Service(HttpStatusCode code, string body, Exception? throws = null)
        {
            var handler = new Mock<HttpMessageHandler>();
            var setup = handler.Protected().Setup<Task<HttpResponseMessage>>(
                "SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
            if (throws is not null) setup.ThrowsAsync(throws);
            else setup.ReturnsAsync(new HttpResponseMessage(code) { Content = new StringContent(body) });
            var client = new HttpClient(handler.Object) { BaseAddress = new Uri("https://firms.modaps.eosdis.nasa.gov/") };
            return new FirmsHotspotService(client, NullLogger<FirmsHotspotService>.Instance);
        }
    }
}
