using LayoutManagementAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LayoutManagementAPI.Data;

public class RealEstateDbContext : DbContext
{
    public RealEstateDbContext(DbContextOptions<RealEstateDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<PropertyListing> Properties => Set<PropertyListing>();
    public DbSet<Inquiry> Inquiries => Set<Inquiry>();
    public DbSet<LayoutPlan> LayoutPlans => Set<LayoutPlan>();
    public DbSet<Plot> Plots => Set<Plot>();
    public DbSet<LayoutImage> LayoutImages => Set<LayoutImage>();
    public DbSet<PlotSurvey> PlotSurveys => Set<PlotSurvey>();
    public DbSet<OwnerPayment> OwnerPayments => Set<OwnerPayment>();
    public DbSet<RegistrationReminder> RegistrationReminders => Set<RegistrationReminder>();
    public DbSet<LayoutAccess> LayoutAccesses => Set<LayoutAccess>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(user =>
        {
            user.Property(u => u.Id).HasMaxLength(450);
            user.Property(u => u.UserName).HasMaxLength(60);
            user.Property(u => u.NormalizedUserName).HasMaxLength(60);
            user.Property(u => u.Email).HasMaxLength(256);
            user.Property(u => u.FullName).HasMaxLength(256);
            user.Property(u => u.Role).HasMaxLength(40);
            user.HasIndex(u => u.NormalizedUserName).IsUnique();
        });

        modelBuilder.Entity<PropertyListing>()
            .Property(p => p.Price)
            .HasPrecision(18, 2);

        modelBuilder.Entity<PropertyListing>()
            .Property(p => p.Bathrooms)
            .HasPrecision(4, 1);

        modelBuilder.Entity<PropertyListing>()
            .HasOne(p => p.Agent)
            .WithMany(a => a.Properties)
            .HasForeignKey(p => p.AgentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Inquiry>()
            .HasOne(i => i.Property)
            .WithMany(p => p.Inquiries)
            .HasForeignKey(i => i.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Inquiry>()
            .HasOne(i => i.Client)
            .WithMany(c => c.Inquiries)
            .HasForeignKey(i => i.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Plot>()
            .Property(p => p.Area)
            .HasPrecision(18, 2);
        modelBuilder.Entity<Plot>()
            .Property(p => p.Price)
            .HasPrecision(18, 2);
        modelBuilder.Entity<Plot>()
            .Property(p => p.AdvanceReceived)
            .HasPrecision(18, 2);
        foreach (var dimension in new[] { "LengthFeet", "BreadthFeet", "TopFeet", "RightFeet", "BottomFeet", "LeftFeet" })
        {
            modelBuilder.Entity<Plot>().Property<decimal>(dimension).HasPrecision(18, 2);
        }
        modelBuilder.Entity<Plot>()
            .HasIndex(p => new { p.LayoutPlanId, p.PlotNumber })
            .IsUnique();
        modelBuilder.Entity<Plot>()
            .HasOne(p => p.LayoutPlan)
            .WithMany(l => l.Plots)
            .HasForeignKey(p => p.LayoutPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<LayoutImage>()
            .HasOne(i => i.LayoutPlan)
            .WithOne(l => l.Image)
            .HasForeignKey<LayoutImage>(i => i.LayoutPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlotSurvey>()
            .HasKey(s => s.PlotId);
        modelBuilder.Entity<PlotSurvey>()
            .HasOne(s => s.Plot)
            .WithOne(p => p.Survey)
            .HasForeignKey<PlotSurvey>(s => s.PlotId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<LayoutPlan>().Property(l => l.OwnerPayable).HasPrecision(18, 2);
        modelBuilder.Entity<LayoutPlan>().Property(l => l.SurveyorAmount).HasPrecision(18, 2);
        modelBuilder.Entity<LayoutPlan>().Property(l => l.TipperCost).HasPrecision(18, 2);
        modelBuilder.Entity<LayoutPlan>().Property(l => l.MiscellaneousAmount).HasPrecision(18, 2);

        modelBuilder.Entity<OwnerPayment>().Property(p => p.Amount).HasPrecision(18, 2);
        modelBuilder.Entity<OwnerPayment>()
            .HasOne(p => p.LayoutPlan)
            .WithMany(l => l.OwnerPayments)
            .HasForeignKey(p => p.LayoutPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RegistrationReminder>().Property(r => r.AdvanceReceived).HasPrecision(18, 2);
        modelBuilder.Entity<RegistrationReminder>()
            .HasIndex(r => new { r.PlotId, r.ReminderDate })
            .IsUnique();

        modelBuilder.Entity<LayoutAccess>()
            .HasIndex(access => new { access.LayoutPlanId, access.UserId })
            .IsUnique();
        modelBuilder.Entity<LayoutAccess>()
            .Property(access => access.UserId)
            .HasMaxLength(450);
        modelBuilder.Entity<LayoutAccess>()
            .HasOne(access => access.LayoutPlan)
            .WithMany(layout => layout.Accesses)
            .HasForeignKey(access => access.LayoutPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
