using System;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity.EntityFramework;

namespace EHS_PORTAL.Areas.ESTAFF.Models.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        static ApplicationDbContext()
        {
            // No automatic schema management, for anything that opens this
            // context - the web app, a console tool, a scheduled job.
            //
            // This database also hosts EHS_PORTAL's CLIP, CORD and FETS schemas.
            // ESTAFF's CLIP entities below map only the columns it reads (COF has
            // no Remarks/DocumentPath/HostInfo/ResidentInfo, PlantMonitoring has
            // no QuoteSubmitDate/EprSubmitDate/RenewDate), so letting EF reconcile
            // the model against the database would drop live columns EHS_PORTAL
            // depends on. Schema changes are applied deliberately: the scripts in
            // DATABASE/, or Update-Database from the Package Manager Console
            // (which drives the migrator directly and is unaffected by this).
            Database.SetInitializer<ApplicationDbContext>(null);
        }

        public ApplicationDbContext() : base("DefaultConnection", throwIfV1Schema: false)
        {
        }

        public virtual DbSet<TaskItem> TaskItems { get; set; }
        public virtual DbSet<TaskHistory> TaskHistories { get; set; }
        public virtual DbSet<Report> Reports { get; set; }
        public virtual DbSet<ReportApproval> ReportApprovals { get; set; }

        // Read-only projections of tables owned/created by EHS_PORTAL's CLIP module (same CLIP schema/DB).
        // Never write through these - see PreventClipReadOnlyWrites().
        public virtual DbSet<COF> COFs { get; set; }
        public virtual DbSet<Plant> Plants { get; set; }
        public virtual DbSet<UserPlant> UserPlants { get; set; }
        public virtual DbSet<PlantMonitoring> PlantMonitoring { get; set; }
        public virtual DbSet<Monitoring> Monitoring {get; set; }
        public virtual DbSet<TaskList> TaskLists { get; set; }
        public virtual DbSet<TaskClassification> TaskClassifications { get; set; }


        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApplicationUser>().ToTable("AspNetUsers", schemaName: "CLIP");
            modelBuilder.Entity<IdentityRole>().ToTable("AspNetRoles", schemaName: "CLIP");
            modelBuilder.Entity<IdentityUserRole>().ToTable("AspNetUserRoles", schemaName: "CLIP");
            modelBuilder.Entity<IdentityUserClaim>().ToTable("AspNetUserClaims", schemaName: "CLIP");
            modelBuilder.Entity<IdentityUserLogin>().ToTable("AspNetUserLogins", schemaName: "CLIP");

            modelBuilder.Entity<ApplicationUser>()
                .Property(u => u.IsAdmin)
                .HasColumnName("IsAdmin");

            modelBuilder.Entity<ApplicationUser>()
                .Property(u => u.HireDate)
                .HasColumnName("HireDate");

            modelBuilder.Entity<ApplicationUser>()
                .Property(u => u.CreatedDate)
                .HasColumnName("CreatedDate");

            modelBuilder.Entity<ApplicationUser>()
                .Property(u => u.LastModifiedDate)
                .HasColumnName("LastModifiedDate");

            // ESTAFF-owned tables, in ESTAFF schema, FK'ing to CLIP.AspNetUsers via ApplicationUser
            modelBuilder.Entity<Report>().ToTable("Reports", "ESTAFF");
            modelBuilder.Entity<ReportApproval>().ToTable("ReportApprovals", "ESTAFF");
            modelBuilder.Entity<Staff>().ToTable("Staffs", "ESTAFF");
            modelBuilder.Entity<TaskItem>().ToTable("TaskItems", "ESTAFF");
            modelBuilder.Entity<TaskHistory>().ToTable("TaskHistories", "ESTAFF");
            modelBuilder.Entity<TaskList>().ToTable("TaskLists", "ESTAFF");
            modelBuilder.Entity<TaskClassification>().ToTable("TaskClassifications", "ESTAFF");

            // ReportApproval relationships
            modelBuilder.Entity<ReportApproval>()
                .HasRequired(ra => ra.Reporter)
                .WithMany()
                .HasForeignKey(ra => ra.ReporterId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<ReportApproval>()
                .HasOptional(ra => ra.Approver)
                .WithMany()
                .HasForeignKey(ra => ra.ApproverId)
                .WillCascadeOnDelete(false);

            // TaskItem relationships
            modelBuilder.Entity<Staff>()
                .HasRequired(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Staff>()
                .HasRequired(s => s.Manager)
                .WithMany()
                .HasForeignKey(s => s.ManagerId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TaskItem>()
                .HasRequired(t => t.AssignedToUser)
                .WithMany()
                .HasForeignKey(t => t.AssignedToUserId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TaskItem>()
                .HasRequired(t => t.CreatedByUser)
                .WithMany()
                .HasForeignKey(t => t.CreatedByUserId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TaskItem>()
                .HasRequired(t => t.TaskClassification)
                .WithMany(tl => tl.TaskItems)
                .HasForeignKey(t => t.TaskClassificationId)
                .WillCascadeOnDelete(false);

            // Bound to the existing TaskList_TaskListId column, so exposing
            // TaskItem.TaskListId does not introduce a second relationship.
            modelBuilder.Entity<TaskItem>()
                .HasOptional(t => t.TaskList)
                .WithMany(tl => tl.TaskItems)
                .HasForeignKey(t => t.TaskListId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TaskHistory>()
                .HasRequired(h => h.Task)
                .WithMany(t => t.Histories)
                .HasForeignKey(h => h.TaskId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<TaskHistory>()
                .HasRequired(h => h.ChangedByUser)
                .WithMany()
                .HasForeignKey(h => h.ChangedByUserId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Report>()
                .HasRequired(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .WillCascadeOnDelete(false);

            // Optional, and with no database foreign key behind it - see
            // Report.PlantId. Mapped only so a report can read its plant's
            // name; ESTAFF never writes to CLIP.Plants.
            modelBuilder.Entity<Report>()
                .HasOptional(r => r.Plant)
                .WithMany()
                .HasForeignKey(r => r.PlantId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TaskList>()
                .HasRequired(t => t.TaskClassification)
                .WithMany(tc => tc.TaskLists)
                .HasForeignKey(t => t.TaskClassificationId)
                .WillCascadeOnDelete(false);

            // Read-only CLIP tables (owned by EHS_PORTAL - do not CreateTable/AddColumn for these in migrations).
            // Names mirror EHS_PORTAL exactly (Areas/CLIP/Models/IdentityModels.cs,
            // OnModelCreating). Note the SINGULAR "PlantMonitoring"/"Monitoring":
            // the plural spellings are a different, empty pair of tables that
            // ESTAFF's own automatic migrations created by mistake, and reading
            // them returns nothing.
            modelBuilder.Entity<COF>().ToTable("CertificateOfFitness", "CLIP");
            modelBuilder.Entity<Plant>().ToTable("Plants", "CLIP");
            modelBuilder.Entity<UserPlant>().ToTable("UserPlants", "CLIP");
            modelBuilder.Entity<PlantMonitoring>().ToTable("PlantMonitoring", "CLIP");
            modelBuilder.Entity<Monitoring>().ToTable("Monitoring", "CLIP");

            modelBuilder.Entity<COF>()
                .HasRequired(c => c.Plant)
                .WithMany()
                .HasForeignKey(c => c.PlantId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<UserPlant>()
                .HasRequired(up => up.User)
                .WithMany()
                .HasForeignKey(up => up.UserId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<UserPlant>()
                .HasRequired(up => up.Plant)
                .WithMany(p => p.UserPlants)
                .HasForeignKey(up => up.PlantId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<PlantMonitoring>()
                .HasRequired(up => up.Plant)
                .WithMany()
                .HasForeignKey(up => up.PlantID)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<PlantMonitoring>()
                .HasRequired(up => up.Monitoring)
                .WithMany(m => m.PlantMonitorings)
                .HasForeignKey(up => up.MonitoringID)
                .WillCascadeOnDelete(false);
        }

        public override int SaveChanges()
        {
            PreventClipReadOnlyWrites();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            PreventClipReadOnlyWrites();
            return base.SaveChangesAsync(cancellationToken);
        }

        // Mirror tables that EHS_PORTAL's CLIP module owns and writes to.
        // Blocking writes here at the SaveChanges level guards against silently corrupting
        // EHS_PORTAL's data if a future ESTAFF change accidentally Add/Update/Removes one.
        private void PreventClipReadOnlyWrites()
        {
            var hasClipWrites = ChangeTracker.Entries()
                .Any(e => e.State != EntityState.Unchanged &&
                          (e.Entity is COF
                        || e.Entity is Plant
                        || e.Entity is UserPlant
                        || e.Entity is PlantMonitoring
                        || e.Entity is Monitoring));

            if (hasClipWrites)
            {
                throw new InvalidOperationException(
                    "COF, Plant, UserPlant, PlantMonitoring, and Monitoring are read-only projections of EHS_PORTAL's CLIP schema and cannot be written from ESTAFF.");
            }
        }

        public static ApplicationDbContext Create()
        {
            return new ApplicationDbContext();
        }
    }
}
