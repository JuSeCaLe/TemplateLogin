namespace Login.Infrastructure.Model
{
    using Login.Infrastructure.Data.Identity;
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
        public DbSet<Demandante> Demandante => Set<Demandante>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<AppRole>(b =>
            {
                b.Property(r => r.Description).HasMaxLength(250);
                b.Property(r => r.Active).HasDefaultValue(true);
                b.Property(r => r.CreatedAt).HasDefaultValueSql("NOW()");
            });

            builder.Entity<AppUser>(b =>
            {
                b.Property(u => u.Active).HasDefaultValue(true);
                b.Property(u => u.CreatedAt).HasDefaultValueSql("NOW()");
            });

            builder.Entity<TipoObligacion>(b =>
            {
                b.ToTable("TipoObligacion");
                b.Property(x => x.Name).HasMaxLength(150).IsRequired();
                b.Property(x => x.Description).HasMaxLength(500);
                b.Property(x => x.Active).HasDefaultValue(true);
                b.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
                b.HasIndex(x => x.Name).IsUnique();
            });

            builder.Entity<TipoProceso>(b =>
            {
                b.ToTable("TipoProceso");
                b.Property(x => x.Name).HasMaxLength(150).IsRequired();
                b.Property(x => x.Description).HasMaxLength(500);
                b.Property(x => x.Active).HasDefaultValue(true);
                b.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
                b.HasIndex(x => x.Name).IsUnique();
            });

            builder.Entity<Juzgado>(b =>
            {
                b.ToTable("Juzgado");
                b.Property(x => x.Name).HasMaxLength(150).IsRequired();
                b.Property(x => x.City).HasMaxLength(70);
                b.Property(x => x.Description).HasMaxLength(500);
                b.Property(x => x.Active).HasDefaultValue(true);
                b.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
                b.HasIndex(x => x.Name).IsUnique();
            });

            builder.Entity<Demandante>(b =>
            {
                b.ToTable("Demandante");
                b.Property(x => x.Name).HasMaxLength(150).IsRequired();
                b.Property(x => x.Description).HasMaxLength(500);
                b.Property(x => x.Active).HasDefaultValue(true);
                b.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
                b.HasIndex(x => x.Name).IsUnique();
            });
        }
    }
}