namespace Login.Infrastructure.Model
{
    using Login.Infrastructure.Data.Identity;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore;

    public class DataContext : IdentityDbContext<AppUser, AppRole, string>
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {            
        }

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
        }
    }
}