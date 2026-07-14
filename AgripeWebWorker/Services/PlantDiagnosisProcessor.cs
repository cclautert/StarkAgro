using AgripeWebAPI.Models;
using AgripeWebAPI.Models.Entities;
using AgripeWebAPI.Services.Diagnosis;
using MongoDB.Driver;

namespace AgripeWebWorker.Services
{
    /// <summary>
    /// Consome os laudos pendentes e dispara a análise (classificador → contexto → LLM),
    /// delegada ao <see cref="IPlantDiagnosisProcessingService"/>.
    /// <para>
    /// Este BackgroundService cuida só da <b>fila</b>: claim atômico, retentativa e zumbis.
    /// O tenant vem <b>de dentro do documento</b> (<c>d.UserId</c>), nunca de um contexto de
    /// usuário: no worker o <c>WorkerUserContext</c> devolve <c>UserId = null</c>.
    /// </para>
    /// </summary>
    public sealed class PlantDiagnosisProcessor : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan ZombieTimeout = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Agrônomo que assumiu um laudo e sumiu não pode prendê-lo para sempre: depois de 24 h
        /// o laudo volta para a fila.
        /// </summary>
        private static readonly TimeSpan AbandonedReviewTimeout = TimeSpan.FromHours(24);

        private const int MaxRetries = 3;

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PlantDiagnosisProcessor> _logger;
        private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}";

        public PlantDiagnosisProcessor(
            IServiceProvider serviceProvider,
            ILogger<PlantDiagnosisProcessor> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await RunAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PlantDiagnosisProcessor tick failed");
                }
            }
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<agpDBContext>();
            var processingService = scope.ServiceProvider.GetRequiredService<IPlantDiagnosisProcessingService>();

            await ReleaseZombiesAsync(db, cancellationToken);
            await ReleaseAbandonedReviewsAsync(db, cancellationToken);

            // Drena a fila num único tick, um documento por vez — cada iteração faz seu
            // próprio claim atômico, então N workers em paralelo nunca pegam o mesmo laudo.
            while (!cancellationToken.IsCancellationRequested)
            {
                var diagnosis = await ClaimNextAsync(db, cancellationToken);
                if (diagnosis is null) break;

                try
                {
                    var result = await processingService.ProcessAsync(diagnosis, cancellationToken);

                    // Rejeição (foto ruim, não é planta) é um desfecho legítimo e terminal:
                    // o serviço já gravou o status. Só falha de verdade entra na retentativa.
                    if (result.Outcome == DiagnosisProcessingOutcome.Failed)
                    {
                        await FailAsync(db, diagnosis, result.Reason ?? "Falha ao processar o laudo.", cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PlantDiagnosisProcessor: failed to process diagnosis {Id}", diagnosis.Id);
                    await FailAsync(db, diagnosis, ex.Message, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Claim atômico: o <c>FindOneAndUpdate</c> é o lock. Quem conseguir virar o status
        /// para Processing ficou com o documento — sem Redis, sem fila externa.
        /// </summary>
        private async Task<PlantDiagnosis?> ClaimNextAsync(agpDBContext db, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;

            var filter = Builders<PlantDiagnosis>.Filter.And(
                Builders<PlantDiagnosis>.Filter.Eq(d => d.Status, PlantDiagnosisStatus.Uploaded),
                Builders<PlantDiagnosis>.Filter.Lte(d => d.NextAttemptAt, now));

            var update = Builders<PlantDiagnosis>.Update
                .Set(d => d.Status, PlantDiagnosisStatus.Processing)
                .Set(d => d.ProcessingStartedAt, now)
                .Set(d => d.UpdatedAt, now)
                .Set(d => d.WorkerId, _workerId)
                .Push(d => d.AuditTrail, new PlantDiagnosisAuditEntry
                {
                    At = now,
                    FromStatus = PlantDiagnosisStatus.Uploaded,
                    ToStatus = PlantDiagnosisStatus.Processing,
                    Action = "claimed"
                });

            return await db.PlantDiagnoses.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<PlantDiagnosis> { ReturnDocument = ReturnDocument.After },
                cancellationToken);
        }

        /// <summary>
        /// Um worker que morreu no meio deixa o laudo preso em Processing. Depois de 10 min,
        /// devolve para a fila (ou marca como falha, se já esgotou as tentativas).
        /// </summary>
        private async Task ReleaseZombiesAsync(agpDBContext db, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var cutoff = now - ZombieTimeout;

            var stuck = await db.PlantDiagnoses
                .Find(d => d.Status == PlantDiagnosisStatus.Processing && d.ProcessingStartedAt < cutoff)
                .ToListAsync(cancellationToken);

            foreach (var diagnosis in stuck)
            {
                _logger.LogWarning(
                    "PlantDiagnosisProcessor: releasing stuck diagnosis {Id} (started at {StartedAt:O})",
                    diagnosis.Id, diagnosis.ProcessingStartedAt);

                await FailAsync(db, diagnosis, "Processamento interrompido.", cancellationToken);
            }
        }

        /// <summary>
        /// Laudo que um agrônomo assumiu e abandonou volta para a fila depois de 24 h — senão
        /// o produtor fica esperando indefinidamente por alguém que não vai voltar.
        /// </summary>
        private async Task ReleaseAbandonedReviewsAsync(agpDBContext db, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var cutoff = now - AbandonedReviewTimeout;

            var abandoned = await db.PlantDiagnoses
                .Find(d => d.Status == PlantDiagnosisStatus.InReview && d.ReviewStartedAt < cutoff)
                .ToListAsync(cancellationToken);

            foreach (var diagnosis in abandoned)
            {
                await db.PlantDiagnoses.UpdateOneAsync(
                    d => d.Id == diagnosis.Id && d.Status == PlantDiagnosisStatus.InReview,
                    Builders<PlantDiagnosis>.Update
                        .Set(d => d.Status, PlantDiagnosisStatus.PendingReview)
                        .Set(d => d.ReviewerId, (int?)null)
                        .Set(d => d.ReviewStartedAt, (DateTime?)null)
                        .Set(d => d.UpdatedAt, now)
                        .Push(d => d.AuditTrail, new PlantDiagnosisAuditEntry
                        {
                            At = now,
                            FromStatus = PlantDiagnosisStatus.InReview,
                            ToStatus = PlantDiagnosisStatus.PendingReview,
                            Action = "review-abandoned"
                        }),
                    null,
                    cancellationToken);

                _logger.LogWarning(
                    "PlantDiagnosisProcessor: review of diagnosis {Id} abandoned, back to the queue", diagnosis.Id);
            }
        }

        private async Task FailAsync(
            agpDBContext db,
            PlantDiagnosis diagnosis,
            string reason,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var retryCount = diagnosis.RetryCount + 1;
            var giveUp = retryCount >= MaxRetries;

            var backoff = retryCount switch
            {
                1 => TimeSpan.FromMinutes(1),
                2 => TimeSpan.FromMinutes(5),
                _ => TimeSpan.FromMinutes(15)
            };

            var nextStatus = giveUp ? PlantDiagnosisStatus.Failed : PlantDiagnosisStatus.Uploaded;

            var update = Builders<PlantDiagnosis>.Update
                .Set(d => d.Status, nextStatus)
                .Set(d => d.RetryCount, retryCount)
                .Set(d => d.NextAttemptAt, now + backoff)
                .Set(d => d.FailureReason, reason)
                .Set(d => d.UpdatedAt, now)
                .Push(d => d.AuditTrail, new PlantDiagnosisAuditEntry
                {
                    At = now,
                    FromStatus = PlantDiagnosisStatus.Processing,
                    ToStatus = nextStatus,
                    Action = giveUp ? "failed" : "retry-scheduled"
                });

            await db.PlantDiagnoses.UpdateOneAsync(d => d.Id == diagnosis.Id, update, null, cancellationToken);
        }
    }
}
