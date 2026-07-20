using MongoDB.Bson.Serialization.Attributes;

namespace StarkAgroAPI.Models.Entities
{
    [BsonIgnoreExtraElements(Inherited = true)]
    public abstract class Entity
    {
        protected Entity()
        {
        }

        [BsonId]
        public virtual int Id { get; set; }
    }
}
