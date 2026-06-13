using MongoDB.Bson.Serialization.Attributes;

namespace AgripeWebAPI.Models.Entities
{
    [BsonIgnoreExtraElements]
    public abstract class Entity
    {
        protected Entity()
        {
        }

        [BsonId]
        public virtual int Id { get; set; }
    }
}
