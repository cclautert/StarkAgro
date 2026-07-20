using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Services.Diagnosis;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Services
{
    /// <summary>
    /// A matriz de autorização cruzada. É o entregável de segurança da Fase 2: o papel
    /// "agrônomo" é o vetor natural de um IDOR neste projeto, porque é o único caso em que
    /// alguém lê um documento cujo <c>UserId</c> não é o dele.
    /// </summary>
    public class DiagnosisAccessServiceTests
    {
        private const int ProducerId = 10;
        private const int AgronomistId = 20;
        private const int OtherProducerId = 30;
        private const int OtherAgronomistId = 40;
        private const int AdminId = 50;

        private static PlantDiagnosis Diagnosis() => new()
        {
            Id = 1,
            UserId = ProducerId,
            AgronomistId = AgronomistId
        };

        private static DiagnosisAccessService Build(AgronomistClient? activeLink)
        {
            var links = new Mock<IMongoCollection<AgronomistClient>>();
            MongoMockHelper.SetupFindList(links, activeLink is null ? [] : [activeLink]);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.AgronomistClients).Returns(links.Object);

            return new DiagnosisAccessService(db.Object);
        }

        private static AgronomistClient ActiveLink() => new()
        {
            Id = 1,
            AgronomistId = AgronomistId,
            ClientUserId = ProducerId,
            Status = AgronomistClientStatus.Active
        };

        [Fact]
        public async Task Owner_CanAccess()
        {
            var service = Build(activeLink: null);

            Assert.True(await service.CanAccessAsync(ProducerId, Diagnosis(), CancellationToken.None));
        }

        [Fact]
        public async Task OtherProducer_IsDenied()
        {
            var service = Build(ActiveLink());

            Assert.False(await service.CanAccessAsync(OtherProducerId, Diagnosis(), CancellationToken.None));
        }

        [Fact]
        public async Task LinkedAgronomist_CanAccess()
        {
            var service = Build(ActiveLink());

            Assert.True(await service.CanAccessAsync(AgronomistId, Diagnosis(), CancellationToken.None));
        }

        [Fact]
        public async Task AgronomistOfAnotherProducer_IsDenied()
        {
            // O laudo não aponta para ele: o AgronomistId denormalizado já barra.
            var service = Build(ActiveLink());

            Assert.False(await service.CanAccessAsync(OtherAgronomistId, Diagnosis(), CancellationToken.None));
        }

        [Fact]
        public async Task AgronomistWithRevokedLink_IsDenied()
        {
            // ESTE é o bug que todo mundo escreve: o laudo ainda tem o AgronomistId dele
            // (foi capturado na criação), mas o vínculo não está mais ativo. Sem a segunda
            // condição, a revogação não teria efeito nenhum.
            var service = Build(activeLink: null); // nenhum vínculo Active para este par

            var diagnosis = Diagnosis(); // continua com AgronomistId == AgronomistId

            Assert.False(await service.CanAccessAsync(AgronomistId, diagnosis, CancellationToken.None));
        }

        [Fact]
        public async Task AgronomistWithPendingInvite_IsDenied()
        {
            // Convite pendente não é vínculo: o filtro do serviço exige Status == Active,
            // então o mock não devolve nada e o acesso é negado.
            var service = Build(activeLink: null);

            Assert.False(await service.CanAccessAsync(AgronomistId, Diagnosis(), CancellationToken.None));
        }

        [Fact]
        public async Task Admin_IsDenied()
        {
            // Laudo é ato profissional. Admin não lê nem assina — não há furo aqui.
            var service = Build(ActiveLink());

            Assert.False(await service.CanAccessAsync(AdminId, Diagnosis(), CancellationToken.None));
        }

        [Fact]
        public async Task DiagnosisWithoutAgronomist_OnlyOwnerCanAccess()
        {
            var service = Build(ActiveLink());
            var diagnosis = new PlantDiagnosis { Id = 2, UserId = ProducerId, AgronomistId = null };

            Assert.True(await service.CanAccessAsync(ProducerId, diagnosis, CancellationToken.None));
            Assert.False(await service.CanAccessAsync(AgronomistId, diagnosis, CancellationToken.None));
        }

        [Fact]
        public async Task LinkQuery_RequiresActiveStatus()
        {
            // O mock do Mongo ignora o filtro, então os testes acima simulam "sem vínculo ativo"
            // devolvendo lista vazia. O que prova a regra de verdade é a query que o serviço
            // monta: ela precisa exigir Status == Active, senão um vínculo revogado passaria.
            FilterDefinition<AgronomistClient>? captured = null;

            var links = new Mock<IMongoCollection<AgronomistClient>>();
            links.Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<AgronomistClient>>(),
                    It.IsAny<FindOptions<AgronomistClient, AgronomistClient>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<FilterDefinition<AgronomistClient>, FindOptions<AgronomistClient, AgronomistClient>, CancellationToken>(
                    (filter, _, _) => captured = filter)
                .ReturnsAsync(() => MongoMockHelper.CreateMockCursor(new List<AgronomistClient>()).Object);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.AgronomistClients).Returns(links.Object);

            var service = new DiagnosisAccessService(db.Object);
            await service.CanAccessAsync(AgronomistId, Diagnosis(), CancellationToken.None);

            Assert.NotNull(captured);

            var serializer = MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry
                .GetSerializer<AgronomistClient>();
            var rendered = captured!
                .Render(new RenderArgs<AgronomistClient>(
                    serializer, MongoDB.Bson.Serialization.BsonSerializer.SerializerRegistry))
                .ToString();

            Assert.Contains(AgronomistClientStatus.Active, rendered);
            Assert.Contains(AgronomistId.ToString(), rendered);
            Assert.Contains(ProducerId.ToString(), rendered);
        }

        [Fact]
        public async Task GetActiveClientIds_OnlyReturnsActiveLinks()
        {
            var links = new Mock<IMongoCollection<AgronomistClient>>();
            MongoMockHelper.SetupFindList(links, [
                new AgronomistClient { AgronomistId = AgronomistId, ClientUserId = ProducerId, Status = AgronomistClientStatus.Active },
                new AgronomistClient { AgronomistId = AgronomistId, ClientUserId = null, Status = AgronomistClientStatus.Active }
            ]);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.AgronomistClients).Returns(links.Object);

            var service = new DiagnosisAccessService(db.Object);
            var ids = await service.GetActiveClientIdsAsync(AgronomistId, CancellationToken.None);

            // O convidado que ainda não tem conta (ClientUserId null) não entra na fila.
            Assert.Equal([ProducerId], ids);
        }
    }
}
