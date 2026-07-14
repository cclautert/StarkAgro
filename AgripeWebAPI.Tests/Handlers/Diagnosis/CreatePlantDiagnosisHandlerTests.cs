using AgripeWebAPI.Domain.Commands.Requests.Diagnosis;
using AgripeWebAPI.Domain.Handlers.Diagnosis;
using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Models.Interfaces;
using AgripeWebAPI.Notifications;
using AgripeWebAPI.Tests.Helpers;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;

namespace AgripeWebAPI.Tests.Handlers.Diagnosis
{
    public class CreatePlantDiagnosisHandlerTests
    {
        private const int OwnerUserId = 7;

        /// <summary>JPEG mínimo: magic bytes FF D8 FF + preenchimento.</summary>
        private static byte[] ValidJpeg()
        {
            var bytes = new byte[64];
            bytes[0] = 0xFF; bytes[1] = 0xD8; bytes[2] = 0xFF; bytes[3] = 0xE0;
            return bytes;
        }

        private static (CreatePlantDiagnosisHandler handler,
                        Mock<IMongoCollection<PlantDiagnosis>> diagnoses,
                        Mock<IDiagnosisImageStore> imageStore,
                        Notificator notifier) BuildHandler(
            List<PlantDiagnosis>? existingDiagnoses = null,
            List<Pivot>? pivots = null,
            int? currentUserId = OwnerUserId)
        {
            var diagnoses = new Mock<IMongoCollection<PlantDiagnosis>>();
            MongoMockHelper.SetupFindList(diagnoses, existingDiagnoses ?? []);

            var pivotCollection = new Mock<IMongoCollection<Pivot>>();
            MongoMockHelper.SetupFindList(pivotCollection, pivots ?? []);

            var db = new Mock<agpDBContext>();
            db.Setup(d => d.PlantDiagnoses).Returns(diagnoses.Object);
            db.Setup(d => d.Pivots).Returns(pivotCollection.Object);
            db.Setup(d => d.GetNextIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(42);

            var currentUser = new Mock<ICurrentUserContext>();
            currentUser.Setup(c => c.UserId).Returns(currentUserId);

            var imageStore = new Mock<IDiagnosisImageStore>();
            imageStore.Setup(s => s.UploadAsync(
                    It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ObjectId.GenerateNewId());

            var notifier = new Notificator();

            var handler = new CreatePlantDiagnosisHandler(
                db.Object, currentUser.Object, imageStore.Object, notifier);

            return (handler, diagnoses, imageStore, notifier);
        }

        [Fact]
        public async Task Handle_ValidJpeg_CreatesDiagnosisAndUploadsImage()
        {
            var (handler, diagnoses, imageStore, notifier) = BuildHandler();

            var result = await handler.Handle(new CreatePlantDiagnosisRequest
            {
                ImageBytes = ValidJpeg(),
                FileName = "folha.jpg",
                ContentType = "image/jpeg"
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(42, result!.Id);
            Assert.Equal(PlantDiagnosisStatus.Uploaded, result.Status);
            Assert.False(notifier.HasNotification());

            imageStore.Verify(s => s.UploadAsync(
                It.IsAny<byte[]>(), "folha.jpg", "image/jpeg", It.IsAny<CancellationToken>()), Times.Once);

            diagnoses.Verify(c => c.InsertOneAsync(
                It.Is<PlantDiagnosis>(d => d.UserId == OwnerUserId && d.Status == PlantDiagnosisStatus.Uploaded),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_PersistsTenantFromCurrentUser_NotFromRequest()
        {
            // O request não expõe UserId de propósito. Este teste trava a regra:
            // o tenant sai de ICurrentUserContext, e um payload forjado não muda isso.
            var (handler, diagnoses, _, _) = BuildHandler(currentUserId: OwnerUserId);

            await handler.Handle(new CreatePlantDiagnosisRequest
            {
                ImageBytes = ValidJpeg(),
                FileName = "folha.jpg",
                ContentType = "image/jpeg"
            }, CancellationToken.None);

            diagnoses.Verify(c => c.InsertOneAsync(
                It.Is<PlantDiagnosis>(d => d.UserId == OwnerUserId),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_ContentTypeSaysImageButBytesAreNot_Rejects()
        {
            // Content-Type declarado pelo cliente não é confiável: sem magic bytes de imagem,
            // o upload não passa.
            var (handler, diagnoses, imageStore, notifier) = BuildHandler();

            var result = await handler.Handle(new CreatePlantDiagnosisRequest
            {
                ImageBytes = "<?php system($_GET[0]); ?>"u8.ToArray(),
                FileName = "shell.jpg",
                ContentType = "image/jpeg"
            }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());

            imageStore.Verify(s => s.UploadAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            diagnoses.Verify(c => c.InsertOneAsync(
                It.IsAny<PlantDiagnosis>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_DisallowedContentType_Rejects()
        {
            var (handler, _, imageStore, notifier) = BuildHandler();

            var result = await handler.Handle(new CreatePlantDiagnosisRequest
            {
                ImageBytes = ValidJpeg(),
                FileName = "laudo.pdf",
                ContentType = "application/pdf"
            }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
            imageStore.Verify(s => s.UploadAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_PivotOfAnotherUser_Rejects()
        {
            // Nenhum pivô é devolvido pelo filtro (que já inclui UserId), então o pivô
            // informado não pertence a quem está enviando.
            var (handler, diagnoses, _, notifier) = BuildHandler(pivots: []);

            var result = await handler.Handle(new CreatePlantDiagnosisRequest
            {
                ImageBytes = ValidJpeg(),
                FileName = "folha.jpg",
                ContentType = "image/jpeg",
                PivotId = 99
            }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
            diagnoses.Verify(c => c.InsertOneAsync(
                It.IsAny<PlantDiagnosis>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_SameImageSentTwice_ReturnsExistingWithoutReuploading()
        {
            var existing = new PlantDiagnosis
            {
                Id = 5,
                UserId = OwnerUserId,
                Status = PlantDiagnosisStatus.AiCompleted,
                ImageSha256 = "irrelevante-o-mock-devolve-este-doc"
            };

            var (handler, diagnoses, imageStore, _) = BuildHandler(existingDiagnoses: [existing]);

            var result = await handler.Handle(new CreatePlantDiagnosisRequest
            {
                ImageBytes = ValidJpeg(),
                FileName = "folha.jpg",
                ContentType = "image/jpeg"
            }, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(5, result!.Id);

            imageStore.Verify(s => s.UploadAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            diagnoses.Verify(c => c.InsertOneAsync(
                It.IsAny<PlantDiagnosis>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_EmptyImage_Rejects()
        {
            var (handler, _, _, notifier) = BuildHandler();

            var result = await handler.Handle(new CreatePlantDiagnosisRequest
            {
                ImageBytes = [],
                FileName = "vazio.jpg",
                ContentType = "image/jpeg"
            }, CancellationToken.None);

            Assert.Null(result);
            Assert.True(notifier.HasNotification());
        }
    }
}
