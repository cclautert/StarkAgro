using AgripeWebAPI.Configuration;
using AgripeWebAPI.Models.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;

namespace AgripeWebAPI.Models
{
    public class agpDBContext
    {
        private readonly IMongoCollection<CounterDocument>? _counters;

        protected agpDBContext() { }

        public agpDBContext(IOptions<MongoDbSettings> settings)
        {
            if (settings?.Value == null)
            {
                throw new InvalidOperationException("MongoDB settings are not configured.");
            }

            if (string.IsNullOrWhiteSpace(settings.Value.ConnectionString))
            {
                throw new InvalidOperationException("MongoDB connection string is not configured.");
            }

            if (string.IsNullOrWhiteSpace(settings.Value.DatabaseName))
            {
                throw new InvalidOperationException("MongoDB database name is not configured.");
            }

            MongoClient client;
            if (!string.IsNullOrEmpty(settings.Value.Username))
            {
                var clientSettings = MongoClientSettings.FromConnectionString(settings.Value.ConnectionString);
                clientSettings.Credential = MongoCredential.CreateCredential(
                    "admin",
                    settings.Value.Username,
                    settings.Value.Password);
                client = new MongoClient(clientSettings);
            }
            else
            {
                client = new MongoClient(settings.Value.ConnectionString);
            }
            var database = client.GetDatabase(settings.Value.DatabaseName);

            Users = database.GetCollection<User>("users");
            Pivots = database.GetCollection<Pivot>("pivots");
            Sensors = database.GetCollection<Sensor>("sensors");
            ReadSensors = database.GetCollection<ReadSensor>("read_sensors");
            SensorAnomalies = database.GetCollection<SensorAnomaly>("sensor_anomalies");
            IrrigationAlerts = database.GetCollection<IrrigationAlert>("irrigation_alerts");
            WaterSources = database.GetCollection<WaterSource>("water_sources");
            IrrigationProposals = database.GetCollection<IrrigationProposal>("irrigation_proposals");
            PlatformAiSettings = database.GetCollection<PlatformAiSettings>("platform_ai_settings");
            PlantDiagnoses = database.GetCollection<PlantDiagnosis>("plant_diagnoses");
            _counters = database.GetCollection<CounterDocument>("counters");

            // Fotos dos laudos ficam no GridFS: o driver já traz o suporte (nenhum pacote novo),
            // usa o mesmo backup do Mongo, e API e Worker compartilham este contexto.
            DiagnosisImages = new GridFSBucket(database, new GridFSBucketOptions
            {
                BucketName = "diagnosis_images"
            });

            _ = Task.Run(async () =>
            {
                try
                {
                    await SensorAnomalies.Indexes.CreateOneAsync(new CreateIndexModel<SensorAnomaly>(
                        Builders<SensorAnomaly>.IndexKeys
                            .Ascending(a => a.UserId)
                            .Ascending(a => a.SensorId)
                            .Descending(a => a.Date)));
                    await SensorAnomalies.Indexes.CreateOneAsync(new CreateIndexModel<SensorAnomaly>(
                        Builders<SensorAnomaly>.IndexKeys.Ascending(a => a.Acknowledged)));
                    await IrrigationAlerts.Indexes.CreateOneAsync(new CreateIndexModel<IrrigationAlert>(
                        Builders<IrrigationAlert>.IndexKeys
                            .Ascending(a => a.PivotId)
                            .Ascending(a => a.AlertType)
                            .Descending(a => a.Date)));
                    await WaterSources.Indexes.CreateOneAsync(new CreateIndexModel<WaterSource>(
                        Builders<WaterSource>.IndexKeys.Ascending(w => w.UserId)));
                    await IrrigationProposals.Indexes.CreateOneAsync(new CreateIndexModel<IrrigationProposal>(
                        Builders<IrrigationProposal>.IndexKeys
                            .Ascending(p => p.UserId)
                            .Descending(p => p.CreatedAt)));
                    await ReadSensors.Indexes.CreateOneAsync(new CreateIndexModel<ReadSensor>(
                        Builders<ReadSensor>.IndexKeys.Ascending(r => r.IdempotencyKey),
                        new CreateIndexOptions { Unique = true, Sparse = true }));

                    // Listagem do produtor
                    await PlantDiagnoses.Indexes.CreateOneAsync(new CreateIndexModel<PlantDiagnosis>(
                        Builders<PlantDiagnosis>.IndexKeys
                            .Ascending(d => d.UserId)
                            .Descending(d => d.CreatedAt)));
                    // Claim do worker
                    await PlantDiagnoses.Indexes.CreateOneAsync(new CreateIndexModel<PlantDiagnosis>(
                        Builders<PlantDiagnosis>.IndexKeys
                            .Ascending(d => d.Status)
                            .Ascending(d => d.NextAttemptAt)));
                    // Dedup de reenvio da mesma foto
                    await PlantDiagnoses.Indexes.CreateOneAsync(new CreateIndexModel<PlantDiagnosis>(
                        Builders<PlantDiagnosis>.IndexKeys
                            .Ascending(d => d.ImageSha256)
                            .Ascending(d => d.UserId)));
                }
                catch
                {
                    // Index creation is best-effort; app can start without it
                }
            });
        }

        public virtual IMongoCollection<User> Users { get; }
        public virtual IMongoCollection<Pivot> Pivots { get; }
        public virtual IMongoCollection<Sensor> Sensors { get; }
        public virtual IMongoCollection<ReadSensor> ReadSensors { get; }
        public virtual IMongoCollection<SensorAnomaly> SensorAnomalies { get; }
        public virtual IMongoCollection<IrrigationAlert> IrrigationAlerts { get; }
        public virtual IMongoCollection<WaterSource> WaterSources { get; }
        public virtual IMongoCollection<IrrigationProposal> IrrigationProposals { get; }
        public virtual IMongoCollection<PlatformAiSettings> PlatformAiSettings { get; }
        public virtual IMongoCollection<PlantDiagnosis> PlantDiagnoses { get; }
        public virtual IGridFSBucket DiagnosisImages { get; }

        public virtual async Task<int> GetNextIdAsync(string entityName, CancellationToken cancellationToken = default)
        {
            var filter = Builders<CounterDocument>.Filter.Eq(x => x.Id, entityName.ToLowerInvariant());
            var update = Builders<CounterDocument>.Update.Inc(x => x.Sequence, 1);

            var options = new FindOneAndUpdateOptions<CounterDocument>
            {
                IsUpsert = true,
                ReturnDocument = ReturnDocument.After
            };

            var counter = await _counters.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
            return counter.Sequence;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }

        public int SaveChanges()
        {
            return 0;
        }

        private sealed class CounterDocument
        {
            public string Id { get; set; } = string.Empty;
            public int Sequence { get; set; }
        }
    }
}
