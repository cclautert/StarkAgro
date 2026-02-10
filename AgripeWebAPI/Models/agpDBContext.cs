using AgripeWebAPI.Configuration;
using AgripeWebAPI.Models.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AgripeWebAPI.Models
{
    public class agpDBContext
    {
        private readonly IMongoCollection<CounterDocument> _counters;

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

            var client = new MongoClient(settings.Value.ConnectionString);
            var database = client.GetDatabase(settings.Value.DatabaseName);

            Users = database.GetCollection<User>("users");
            Pivots = database.GetCollection<Pivot>("pivots");
            Sensors = database.GetCollection<Sensor>("sensors");
            ReadSensors = database.GetCollection<ReadSensor>("read_sensors");
            _counters = database.GetCollection<CounterDocument>("counters");
        }

        public IMongoCollection<User> Users { get; }
        public IMongoCollection<Pivot> Pivots { get; }
        public IMongoCollection<Sensor> Sensors { get; }
        public IMongoCollection<ReadSensor> ReadSensors { get; }

        public async Task<int> GetNextIdAsync(string entityName, CancellationToken cancellationToken = default)
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
