using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgripeWebAPI.Models.Entities
{
    public class Pivo
    {
        [Key]
        public int Id { get; set; }        
        public string? Name { get; set; }
        [ForeignKey("User")]
        public int UserId { get; set; }
    }
}
