using System;
using System.Data.Entity;
using System.Threading;
using System.Threading.Tasks;

namespace EHS_PORTAL.Areas.ESTAFF.Models.Data
{
    // Read-only window onto the CLIP schema, which belongs to EHS_PORTAL.
    //
    // These tables live in the same database as ESTAFF's own, but they are not
    // ours: EHS_PORTAL creates, migrates and writes them. Keeping them in a
    // separate context with its initializer set to null means ESTAFF's
    // migrations can never emit DDL against them — ESTAFF's model of these
    // tables is intentionally partial (it maps only the columns it reads), so
    // letting a migration reconcile it would drop live EHS_PORTAL columns.
    //
    // Table names mirror EHS_PORTAL exactly
    // (Areas/CLIP/Models/IdentityModels.cs, OnModelCreating). Note the singular
    // "Monitoring"/"PlantMonitoring" — the plural spellings are a different,
    // empty pair of tables and must not be used.
    public class ClipDbContext : DbContext
    {
        static ClipDbContext()
        {
            // Never create, migrate or drop anything in the CLIP schema.
            Database.SetInitializer<ClipDbContext>(null);
        }

        public ClipDbContext() : base("DefaultConnection")
        {
            Configuration.LazyLoadingEnabled = false;
            Configuration.ProxyCreationEnabled = false;
        }

        public virtual DbSet<COF> COFs { get; set; }
        public virtual DbSet<Plant> Plants { get; set; }
        public virtual DbSet<UserPlant> UserPlants { get; set; }
        public virtual DbSet<PlantMonitoring> PlantMonitoring { get; set; }
        public virtual DbSet<Monitoring> Monitoring { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<COF>().ToTable("CertificateOfFitness", "CLIP");
            modelBuilder.Entity<Plant>().ToTable("Plants", "CLIP");
            modelBuilder.Entity<UserPlant>().ToTable("UserPlants", "CLIP");

            // UserPlant.User points at ApplicationUser, which ApplicationDbContext
            // owns. Following it here would pull ESTAFF's whole Identity model
            // into this context; the UserId string is all the CLIP reads need.
            modelBuilder.Entity<UserPlant>().Ignore(up => up.User);
            modelBuilder.Entity<PlantMonitoring>().ToTable("PlantMonitoring", "CLIP");
            modelBuilder.Entity<Monitoring>().ToTable("Monitoring", "CLIP");

            modelBuilder.Entity<COF>()
                .HasRequired(c => c.Plant)
                .WithMany()
                .HasForeignKey(c => c.PlantId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<UserPlant>()
                .HasRequired(up => up.Plant)
                .WithMany(p => p.UserPlants)
                .HasForeignKey(up => up.PlantId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<PlantMonitoring>()
                .HasRequired(pm => pm.Plant)
                .WithMany()
                .HasForeignKey(pm => pm.PlantID)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<PlantMonitoring>()
                .HasRequired(pm => pm.Monitoring)
                .WithMany(m => m.PlantMonitorings)
                .HasForeignKey(pm => pm.MonitoringID)
                .WillCascadeOnDelete(false);

            base.OnModelCreating(modelBuilder);
        }

        public override int SaveChanges()
        {
            throw new InvalidOperationException(ReadOnlyMessage);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(ReadOnlyMessage);
        }

        private const string ReadOnlyMessage =
            "The CLIP schema belongs to EHS_PORTAL and is read-only from ESTAFF. " +
            "Change it through EHS_PORTAL instead.";
    }
}
