using MongoDB.Bson.Serialization.Attributes;

namespace AgripeWebAPI.Models.Entities
{
    public abstract class Entity
    {
        protected Entity() 
        {
        }

        [BsonId]
        public virtual int Id { get; set; }
    }
}
