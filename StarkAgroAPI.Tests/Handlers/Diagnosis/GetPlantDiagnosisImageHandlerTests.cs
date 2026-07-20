using StarkAgroAPI.Domain.Commands.Requests.Diagnosis;
using StarkAgroAPI.Domain.Handlers.Diagnosis;
using StarkAgroAPI.Models;
using StarkAgroAPI.Models.Entities;
using StarkAgroAPI.Models.Interfaces;
using StarkAgroAPI.Tests.Helpers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Moq;

namespace StarkAgroAPI.Tests.Handlers.Diagnosis
{
    /// <summary>
    /// A imagem é o endpoint que mais costuma ficar sem proteção — um id sequencial
    /// exposto num &lt;img&gt; é o convite perfeito para um IDOR. Estes testes provam que o
    /// handler filtra pelo usuário autenticado antes de abrir qualquer arquivo.
    /// </summary>
    public class GetPlantDiagnosisImageHandlerTests
    {
        private const int OwnerUserId = 7;

        [Fact]
        public async Task Handle_Owner_ReturnsImage()
        {
            var fileId = ObjectId.GenerateNewId();
            var diagnosis = new PlantDiagnosis
            {
                Id = 1,
                UserId = OwnerUserId,
                ImageFileId = fileId,
                ImageContentType = "image/jpeg"
            };

            var diagnoses = new Mock<IMongoCollection<PlantDiagnosis>>();
            MongoMockHelper.SetupFindList(diagnoses, [diagnosis]);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.PlantDiagnoses).Returns(diagnoses.Object);

            var currentUser = new Mock<ICurrentUserContext>();
            currentUser.Setup(c => c.UserId).Returns(OwnerUserId);

            var imageStore = new Mock<IDiagnosisImageStore>();
            imageStore.Setup(s => s.DownloadAsync(fileId, It.IsAny<CancellationToken>()))
                .ReturnsAsync([1, 2, 3]);

            var handler = new GetPlantDiagnosisImageHandler(db.Object, currentUser.Object, imageStore.Object);

            var result = await handler.Handle(
                new GetPlantDiagnosisImageRequest { Id = 1 }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("image/jpeg", result!.ContentType);
            Assert.Equal([1, 2, 3], result.Content);
        }

        [Fact]
        public async Task Handle_FiltersByAuthenticatedUser()
        {
            // O mock do Mongo ignora o filtro, então verificar "outro usuário recebe null"
            // seria tautológico. O que realmente importa é a query que o handler monta:
            // ela precisa conter a igualdade com o usuário autenticado.
            FilterDefinition<PlantDiagnosis>? capturedFilter = null;

            var diagnoses = new Mock<IMongoCollection<PlantDiagnosis>>();
            diagnoses.Setup(c => c.FindAsync(
                    It.IsAny<FilterDefinition<PlantDiagnosis>>(),
                    It.IsAny<FindOptions<PlantDiagnosis, PlantDiagnosis>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<FilterDefinition<PlantDiagnosis>, FindOptions<PlantDiagnosis, PlantDiagnosis>, CancellationToken>(
                    (filter, _, _) => capturedFilter = filter)
                .ReturnsAsync(() => MongoMockHelper.CreateMockCursor(new List<PlantDiagnosis>()).Object);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.PlantDiagnoses).Returns(diagnoses.Object);

            var currentUser = new Mock<ICurrentUserContext>();
            currentUser.Setup(c => c.UserId).Returns(OwnerUserId);

            var handler = new GetPlantDiagnosisImageHandler(
                db.Object, currentUser.Object, new Mock<IDiagnosisImageStore>().Object);

            var result = await handler.Handle(
                new GetPlantDiagnosisImageRequest { Id = 1 }, CancellationToken.None);

            Assert.Null(result);
            Assert.NotNull(capturedFilter);

            var rendered = Render(capturedFilter!);
            Assert.Equal(OwnerUserId, rendered["UserId"].AsInt32);
            Assert.Equal(1, rendered["_id"].AsInt32);
        }

        [Fact]
        public async Task Handle_DiagnosisNotFound_ReturnsNull()
        {
            var diagnoses = new Mock<IMongoCollection<PlantDiagnosis>>();
            MongoMockHelper.SetupFindList(diagnoses, []);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.PlantDiagnoses).Returns(diagnoses.Object);

            var currentUser = new Mock<ICurrentUserContext>();
            currentUser.Setup(c => c.UserId).Returns(OwnerUserId);

            var imageStore = new Mock<IDiagnosisImageStore>();

            var handler = new GetPlantDiagnosisImageHandler(db.Object, currentUser.Object, imageStore.Object);

            var result = await handler.Handle(
                new GetPlantDiagnosisImageRequest { Id = 999 }, CancellationToken.None);

            Assert.Null(result);
            imageStore.Verify(s => s.DownloadAsync(It.IsAny<ObjectId>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        private static BsonDocument Render(FilterDefinition<PlantDiagnosis> filter)
        {
            var serializer = BsonSerializer.SerializerRegistry.GetSerializer<PlantDiagnosis>();
            return filter.Render(new RenderArgs<PlantDiagnosis>(serializer, BsonSerializer.SerializerRegistry));
        }
    }
}
