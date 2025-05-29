using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgripeWebAPI.Models.Entities
{
    public abstract class Entity
    {
        protected Entity() 
        {
        }

        [Key]
        [Column("id")]
        public virtual int Id { get; private set; }
    }
}
