namespace Login.Infrastructure.Model
{
    using Login.Infrastructure.Data.Identity;
    using Login.Infrastructure.Model.Cases;
    using Login.Infrastructure.Model.Catalog;
    using Login.Infrastructure.Model.Parametros;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore;

    public class DataContext : IdentityDbContext<AppUser, AppRole, string>
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {
        }

        public DbSet<TipoObligacion> TiposObligacion => Set<TipoObligacion>();
        public DbSet<TipoProceso> TiposProceso => Set<TipoProceso>();
        public DbSet<Juzgado> Juzgado => Set<Juzgado>();

        public DbSet<Case> Cases => Set<Case>();
        public DbSet<CaseParty> CaseParties => Set<CaseParty>();
        public DbSet<CaseProcessStage> CaseProcessStages => Set<CaseProcessStage>();
        public DbSet<CaseProceduralNote> CaseProceduralNotes => Set<CaseProceduralNote>();

        public DbSet<CatalogProcessType> CatalogProcessTypes => Set<CatalogProcessType>();
        public DbSet<CatalogStage> CatalogStages => Set<CatalogStage>();
        public DbSet<CatalogSubStage> CatalogSubStages => Set<CatalogSubStage>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<AppRole>(b =>
            {
                b.Property(r => r.Description).HasMaxLength(250);
                b.Property(r => r.Active).HasDefaultValue(true);
                b.Property(r => r.CreatedAt).HasDefaultValueSql("now()");
                b.Property(r => r.IsDemandante).HasDefaultValue(false);
            });

            builder.Entity<AppUser>(b =>
            {
                b.Property(u => u.Active).HasDefaultValue(true);
                b.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
            });

            builder.Entity<TipoObligacion>(b =>
            {
                b.ToTable("TipoObligacion");
                b.Property(x => x.Name).HasMaxLength(150).IsRequired();
                b.Property(x => x.Description).HasMaxLength(500);
                b.Property(x => x.Active).HasDefaultValue(true);
                b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
                b.HasIndex(x => x.Name).IsUnique();
            });

            builder.Entity<TipoProceso>(b =>
            {
                b.ToTable("TipoProceso");
                b.Property(x => x.Name).HasMaxLength(150).IsRequired();
                b.Property(x => x.Description).HasMaxLength(500);
                b.Property(x => x.Active).HasDefaultValue(true);
                b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
                b.HasIndex(x => x.Name).IsUnique();
            });

            builder.Entity<Juzgado>(b =>
            {
                b.ToTable("Juzgado");
                b.Property(x => x.Name).HasMaxLength(150).IsRequired();
                b.Property(x => x.City).HasMaxLength(70);
                b.Property(x => x.Description).HasMaxLength(500);
                b.Property(x => x.Active).HasDefaultValue(true);
                b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
                b.HasIndex(x => x.Name).IsUnique();
            });

            builder.Entity<CatalogProcessType>(b =>
            {
                b.ToTable("CatalogProcessType");
                b.Property(x => x.Name).HasMaxLength(150).IsRequired();
                b.HasMany(x => x.Stages).WithOne(x => x.ProcessType).HasForeignKey(x => x.CatalogProcessTypeId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<CatalogStage>(b =>
            {
                b.ToTable("CatalogStage");
                b.Property(x => x.Name).HasMaxLength(300).IsRequired();
                b.HasMany(x => x.SubStages).WithOne(x => x.Stage).HasForeignKey(x => x.CatalogStageId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<CatalogSubStage>(b =>
            {
                b.ToTable("CatalogSubStage");
                b.Property(x => x.Name).HasMaxLength(300).IsRequired();
            });

            builder.Entity<Case>(b =>
            {
                b.ToTable("Case");
                b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

                b.OwnsOne(x => x.Process, p =>
                {
                    p.Property(x => x.Radicado).HasMaxLength(50).IsRequired();
                    p.Property(x => x.ProcessType).HasMaxLength(100).IsRequired();
                    p.Property(x => x.Court).HasMaxLength(150).IsRequired();
                    p.Property(x => x.City).HasMaxLength(100).IsRequired();
                });

                b.OwnsOne(x => x.FinancialInfo, f =>
                {
                    f.Property(x => x.Capital).HasColumnType("decimal(18,2)");
                    f.Property(x => x.Obligations).HasMaxLength(500);
                });

                b.OwnsOne(x => x.Measures, m =>
                {
                    m.Property(x => x.EmbargoDate).HasMaxLength(10);
                    m.Property(x => x.RemanentEntity).HasMaxLength(200);
                });

                b.OwnsOne(x => x.Stages, s =>
                {
                    s.Property(x => x.PersonalNotificationDate).HasMaxLength(10);
                    s.Property(x => x.FirstInstanceDate).HasMaxLength(10);
                    s.Property(x => x.SecondInstanceDate).HasMaxLength(10);
                });

                b.OwnsOne(x => x.Auction, a =>
                {
                    a.Property(x => x.AppraisalStatus).HasMaxLength(50);
                    a.Property(x => x.AppraisalDate).HasMaxLength(10);
                    a.Property(x => x.AppraisalValue).HasColumnType("decimal(18,2)");
                    a.Property(x => x.AuctionStatus).HasMaxLength(50);
                    a.Property(x => x.AuctionDate).HasMaxLength(10);
                    a.Property(x => x.AwardDate).HasMaxLength(10);
                });

                b.OwnsOne(x => x.Closure, c =>
                {
                    c.Property(x => x.TerminationDate).HasMaxLength(10);
                    c.Property(x => x.TerminationReason).HasMaxLength(500);
                    c.Property(x => x.TitlesStatus).HasMaxLength(100);
                    c.Property(x => x.DeliveryDate).HasMaxLength(10);
                    c.Property(x => x.FileReturnStatus).HasMaxLength(100);
                    c.Property(x => x.FileReturnDate).HasMaxLength(10);
                });

                b.HasMany(x => x.Parties).WithOne(x => x.Case).HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Cascade);
                b.HasMany(x => x.ProcessStages).WithOne(x => x.Case).HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Cascade);
                b.HasMany(x => x.ProceduralNotes).WithOne(x => x.Case).HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Cascade);

                b.HasOne(x => x.DemandanteRole).WithMany().HasForeignKey(x => x.DemandanteRoleId).OnDelete(DeleteBehavior.Restrict);
                b.HasIndex(x => x.DemandanteRoleId);
            });

            builder.Entity<CaseParty>(b =>
            {
                b.ToTable("CaseParty");
                b.Property(x => x.Person).HasMaxLength(300).IsRequired();
                b.Property(x => x.ProcessRole).HasMaxLength(100).IsRequired();
            });

            builder.Entity<CaseProcessStage>(b =>
            {
                b.ToTable("CaseProcessStage");
                b.Property(x => x.CreatedAt).HasMaxLength(10).IsRequired();
                b.Property(x => x.StageName).HasMaxLength(200).IsRequired();
                b.Property(x => x.SubStageName).HasMaxLength(200).IsRequired();
                b.Property(x => x.Observation).HasMaxLength(1000);
            });

            builder.Entity<CaseProceduralNote>(b =>
            {
                b.ToTable("CaseProceduralNote");
                b.Property(x => x.CreatedAt).HasMaxLength(10).IsRequired();
                b.Property(x => x.Text).HasMaxLength(2000).IsRequired();
            });
        }
    }
}