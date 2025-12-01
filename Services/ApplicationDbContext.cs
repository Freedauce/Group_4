using Exam.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Exam.Services
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Car> Cars { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<SystemLog> SystemLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Seed roles
            builder.Entity<IdentityRole>().HasData(
                new IdentityRole
                {
                    Id = "1",
                    Name = "admin",
                    NormalizedName = "ADMIN"
                },
                new IdentityRole
                {
                    Id = "2",
                    Name = "manager",
                    NormalizedName = "MANAGER"
                },
                new IdentityRole
                {
                    Id = "3",
                    Name = "client",
                    NormalizedName = "CLIENT"
                }
            );

            // =========================================================================
            // FIXED: Proper configuration for navigation properties
            // =========================================================================

            // 1. Configure Car entity (no inverse navigation needed)
            builder.Entity<Car>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
            });

            // 2. Configure Booking entity with navigation properties
            builder.Entity<Booking>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                // Car relationship - ONE Booking has ONE Car
                entity.HasOne(b => b.Car)
                    .WithMany() // Car doesn't have a collection of Bookings
                    .HasForeignKey(b => b.CarId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Client relationship - ONE Booking has ONE Client (ApplicationUser)
                entity.HasOne(b => b.Client)
                    .WithMany() // ApplicationUser doesn't have a collection of Bookings
                    .HasForeignKey(b => b.ClientId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Payments relationship - ONE Booking has MANY Payments
                entity.HasMany(b => b.Payments)
                    .WithOne(p => p.Booking)
                    .HasForeignKey(p => p.BookingId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // 3. Configure Payment entity
            builder.Entity<Payment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                // The relationship with Booking is already configured above
                // No need to configure it here again
            });

            // 4. Configure SystemLog entity
            builder.Entity<SystemLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                // User relationship (optional)
                entity.HasOne<ApplicationUser>()
                    .WithMany()
                    .HasForeignKey(s => s.UserId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}