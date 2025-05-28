using AgripeWebAPI.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgripeWebAPI.Models
{
    public class agpDBContext : DbContext
    {
        public agpDBContext(DbContextOptions<agpDBContext> options)
            : base(options)
        {
        }

        // DbSets para suas entidades
        public DbSet<ReadSensor> ReadSensors { get; set; }
        public DbSet<Sensor> Sensores { get; set; }
        public DbSet<Pivo> Pivos { get; set; }        
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configurações com Fluent API (opcional)
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd(); // Makes it an identity column
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Password).IsRequired().HasMaxLength(100);
            });

            modelBuilder.Entity<Sensor>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd(); // Makes it an identity column
                entity.Property(e => e.Code).IsRequired().HasMaxLength(17);
            });

            modelBuilder.Entity<ReadSensor>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd(); // Makes it an identity column
                entity.Property(e => e.Value).IsRequired();
                entity.Property(e => e.Date).IsRequired();
            });

            modelBuilder.Entity<Pivo>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd(); // Makes it an identity column
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            });
        }
    }
}
