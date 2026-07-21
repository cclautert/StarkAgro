using StarkAgroAPI.Domain.Commands.Requests.Diagnosis;
using StarkAgroAPI.Domain.Handlers.Diagnosis;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Notifications;
using StarkAgroAPI.Services.Diagnosis;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Services
{
    public class DiagnosisQuotaServiceTests
    {
        private const int UserId = 3;

        private static DiagnosisQuotaService Build(
            int? userQuota,
            int defaultQuota,
            int usedThisMonth,
            int? revendaQuota = null,
            bool memberOfRevenda = false)
        {
            var users = new Mock<IMongoCollection<User>>();
            MongoMockHelper.SetupFindList(users, [
                new User { Id = UserId, Name = "Produtor", DiagnosisQuotaPerMonth = userQuota }
            ]);

            var settings = new Mock<IMongoCollection<PlatformAiSettings>>();
            MongoMockHelper.SetupFindList(settings, [
                new PlatformAiSettings { Id = 1, DefaultDiagnosisQuotaPerMonth = defaultQuota }
            ]);

            var diagnoses = new Mock<IMongoCollection<PlantDiagnosis>>();
            MongoMockHelper.SetupFindList(diagnoses,
                Enumerable.Range(1, usedThisMonth)
                    .Select(i => new PlantDiagnosis { Id = i, UserId = UserId, CreatedAt = DateTime.UtcNow })
                    .ToList());

            // A revenda que paga pelo produtor também define cota — a busca passa pelo vínculo
            // Client Active, não pelo cache User.RevendaId.
            var memberships = new Mock<IMongoCollection<RevendaMembership>>();
            MongoMockHelper.SetupFindList(memberships, memberOfRevenda
                ? [new RevendaMembership
                {
                    Id = 1, RevendaId = 7, MemberUserId = UserId,
                    MemberRole = RevendaMemberRole.Client, Status = RevendaMembershipStatus.Active
                }]
                : []);

            var revendas = new Mock<IMongoCollection<StarkAgroAPI.Models.Entities.Revenda>>();
            MongoMockHelper.SetupFindList(revendas, [
                new StarkAgroAPI.Models.Entities.Revenda { Id = 7, Name = "AgroSul", DiagnosisQuotaPerMonth = revendaQuota }
            ]);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.Users).Returns(users.Object);
            db.Setup(d => d.PlatformAiSettings).Returns(settings.Object);
            db.Setup(d => d.PlantDiagnoses).Returns(diagnoses.Object);
            db.Setup(d => d.RevendaMemberships).Returns(memberships.Object);
            db.Setup(d => d.Revendas).Returns(revendas.Object);

            return new DiagnosisQuotaService(db.Object);
        }

        [Fact]
        public async Task Quota_HerdaDaRevendaQuandoUsuarioNaoTemPropria()
        {
            // Quem banca a conta limita o consumo: o produtor-membro cai na cota da revenda.
            var service = Build(userQuota: null, defaultQuota: 5, usedThisMonth: 0,
                revendaQuota: 30, memberOfRevenda: true);

            var quota = await service.GetAsync(UserId, CancellationToken.None);

            Assert.Equal(30, quota.Limit);
        }

        [Fact]
        public async Task Quota_DoUsuarioGanhaDaRevenda()
        {
            var service = Build(userQuota: 12, defaultQuota: 5, usedThisMonth: 0,
                revendaQuota: 30, memberOfRevenda: true);

            var quota = await service.GetAsync(UserId, CancellationToken.None);

            Assert.Equal(12, quota.Limit);
        }

        [Fact]
        public async Task Quota_SemVinculoDeRevenda_CaiNoPadraoDaPlataforma()
        {
            var service = Build(userQuota: null, defaultQuota: 5, usedThisMonth: 0,
                revendaQuota: 30, memberOfRevenda: false);

            var quota = await service.GetAsync(UserId, CancellationToken.None);

            Assert.Equal(5, quota.Limit);
        }

        [Fact]
        public async Task Quota_ZeroMeansUnlimited()
        {
            // Zero é o comportamento de antes de a cota existir: ninguém fica bloqueado por
            // acidente ao subir esta versão.
            var service = Build(userQuota: null, defaultQuota: 0, usedThisMonth: 500);

            var quota = await service.GetAsync(UserId, CancellationToken.None);

            Assert.True(quota.IsUnlimited);
            Assert.False(quota.IsExhausted);
        }

        [Fact]
        public async Task Quota_UserQuotaBeatsThePlatformDefault()
        {
            var service = Build(userQuota: 3, defaultQuota: 100, usedThisMonth: 3);

            var quota = await service.GetAsync(UserId, CancellationToken.None);

            Assert.Equal(3, quota.Limit);
            Assert.True(quota.IsExhausted);
        }

        [Fact]
        public async Task Quota_FallsBackToThePlatformDefault()
        {
            var service = Build(userQuota: null, defaultQuota: 10, usedThisMonth: 4);

            var quota = await service.GetAsync(UserId, CancellationToken.None);

            Assert.Equal(10, quota.Limit);
            Assert.Equal(4, quota.Used);
            Assert.Equal(6, quota.Remaining);
            Assert.False(quota.IsExhausted);
        }

        [Fact]
        public async Task Quota_ExhaustedWhenUsedReachesTheLimit()
        {
            var service = Build(userQuota: 5, defaultQuota: 0, usedThisMonth: 5);

            var quota = await service.GetAsync(UserId, CancellationToken.None);

            Assert.True(quota.IsExhausted);
            Assert.Equal(0, quota.Remaining);
        }

        [Fact]
        public async Task Quota_ResetsAtTheStartOfNextMonth()
        {
            var service = Build(userQuota: 5, defaultQuota: 0, usedThisMonth: 1);

            var quota = await service.GetAsync(UserId, CancellationToken.None);

            Assert.Equal(1, quota.ResetsAt.Day);
            Assert.True(quota.ResetsAt > DateTime.UtcNow);
        }
    }

    public class CreateDiagnosisQuotaEnforcementTests
    {
        private const int UserId = 3;

        private static (CreatePlantDiagnosisHandler handler,
                        Mock<IMongoCollection<PlantDiagnosis>> diagnoses,
                        Mock<IDiagnosisImageStore> store,
                        Notificator notifier) Build(DiagnosisQuota quota)
        {
            var diagnoses = new Mock<IMongoCollection<PlantDiagnosis>>();
            MongoMockHelper.SetupFindList(diagnoses, []);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.PlantDiagnoses).Returns(diagnoses.Object);
            db.Setup(d => d.Pivots).Returns(new Mock<IMongoCollection<Pivot>>().Object);
            db.Setup(d => d.GetNextIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var currentUser = new Mock<ICurrentUserContext>();
            currentUser.Setup(c => c.UserId).Returns(UserId);

            var store = new Mock<IDiagnosisImageStore>();
            store.Setup(s => s.UploadAsync(
                    It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(MongoDB.Bson.ObjectId.GenerateNewId());

            var access = new Mock<IDiagnosisAccessService>();
            access.Setup(a => a.GetActiveLinkForClientAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AgronomistClient?)null);

            var quotaService = new Mock<IDiagnosisQuotaService>();
            quotaService.Setup(q => q.GetAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(quota);

            var notifier = new Notificator();

            var handler = new CreatePlantDiagnosisHandler(
                db.Object, currentUser.Object, store.Object, access.Object, quotaService.Object, notifier);

            return (handler, diagnoses, store, notifier);
        }

        private static CreatePlantDiagnosisRequest Photo() => new()
        {
            ImageBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            FileName = "folha.jpg",
            ContentType = "image/jpeg"
        };

        [Fact]
        public async Task ExhaustedQuota_BlocksTheUploadBeforeSpendingAnything()
        {
            // Cada foto é uma chamada paga ao classificador. Estourou a cota, não sobe imagem
            // nem se cria laudo.
            var (handler, diagnoses, store, notifier) = Build(
                new DiagnosisQuota(Limit: 10, Used: 10, ResetsAt: DateTime.UtcNow.AddDays(5)));

            var result = await handler.Handle(Photo(), CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());

            store.Verify(s => s.UploadAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            diagnoses.Verify(c => c.InsertOneAsync(
                It.IsAny<PlantDiagnosis>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task QuotaWithRoom_LetsTheUploadThrough()
        {
            var (handler, diagnoses, _, notifier) = Build(
                new DiagnosisQuota(Limit: 10, Used: 9, ResetsAt: DateTime.UtcNow.AddDays(5)));

            var result = await handler.Handle(Photo(), CancellationToken.None);

            Assert.NotNull(result);
            Assert.False(notifier.HasNotification());

            diagnoses.Verify(c => c.InsertOneAsync(
                It.IsAny<PlantDiagnosis>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UnlimitedQuota_NeverBlocks()
        {
            var (handler, diagnoses, _, _) = Build(
                new DiagnosisQuota(Limit: 0, Used: 0, ResetsAt: DateTime.UtcNow.AddDays(5)));

            Assert.NotNull(await handler.Handle(Photo(), CancellationToken.None));

            diagnoses.Verify(c => c.InsertOneAsync(
                It.IsAny<PlantDiagnosis>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task QuotaHandler_ReportsUsage()
        {
            var currentUser = new Mock<ICurrentUserContext>();
            currentUser.Setup(c => c.UserId).Returns(UserId);

            var quotaService = new Mock<IDiagnosisQuotaService>();
            quotaService.Setup(q => q.GetAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DiagnosisQuota(10, 4, DateTime.UtcNow.AddDays(5)));

            var handler = new GetDiagnosisQuotaHandler(currentUser.Object, quotaService.Object);

            var result = await handler.Handle(new GetDiagnosisQuotaRequest(), CancellationToken.None);

            Assert.Equal(10, result.Limit);
            Assert.Equal(4, result.Used);
            Assert.Equal(6, result.Remaining);
            Assert.False(result.IsExhausted);
            Assert.False(result.IsUnlimited);
        }
    }
}
