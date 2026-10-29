using Microsoft.EntityFrameworkCore;
using ShiftSync.Domain.Entities;

namespace ShiftSync.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Store> Stores => Set<Store>();
    public DbSet<StoreHour> StoreHours => Set<StoreHour>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<AvailabilityWindow> AvailabilityWindows => Set<AvailabilityWindow>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<TimeOffRequest> TimeOffRequests => Set<TimeOffRequest>();
    public DbSet<ShiftSwapRequest> ShiftSwapRequests => Set<ShiftSwapRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Store>(entity =>
        {
            entity.Property(store => store.Name).HasMaxLength(80);
            entity.Property(store => store.City).HasMaxLength(80);
            entity.Property(store => store.Address).HasMaxLength(160);
        });

        modelBuilder.Entity<StoreHour>(entity =>
        {
            entity.HasOne(hour => hour.Store)
                .WithMany(store => store.Hours)
                .HasForeignKey(hour => hour.StoreId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(hour => new { hour.StoreId, hour.DayOfWeek }).IsUnique();
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.Property(employee => employee.FirstName).HasMaxLength(60);
            entity.Property(employee => employee.LastName).HasMaxLength(60);
            entity.Property(employee => employee.Email).HasMaxLength(120);
            entity.Property(employee => employee.PasswordHash).HasMaxLength(500);
            entity.Property(employee => employee.Phone).HasMaxLength(30);
            entity.Property(employee => employee.Color).HasMaxLength(16);
            entity.Property(employee => employee.MaxWeeklyHours).HasPrecision(4, 1);
            entity.HasIndex(employee => employee.Email).IsUnique();
            entity.HasOne(employee => employee.Store)
                .WithMany(store => store.Employees)
                .HasForeignKey(employee => employee.StoreId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AvailabilityWindow>(entity =>
        {
            entity.HasOne(window => window.Employee)
                .WithMany(employee => employee.Availability)
                .HasForeignKey(window => window.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Shift>(entity =>
        {
            entity.Property(shift => shift.Notes).HasMaxLength(280);
            entity.HasIndex(shift => new { shift.StoreId, shift.StartsAt });
            entity.HasIndex(shift => new { shift.EmployeeId, shift.StartsAt });
            entity.HasOne(shift => shift.Store)
                .WithMany()
                .HasForeignKey(shift => shift.StoreId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(shift => shift.Employee)
                .WithMany(employee => employee.Shifts)
                .HasForeignKey(shift => shift.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TimeOffRequest>(entity =>
        {
            entity.Property(request => request.Reason).HasMaxLength(400);
            entity.Property(request => request.ReviewNote).HasMaxLength(400);
            entity.HasOne(request => request.Employee)
                .WithMany()
                .HasForeignKey(request => request.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(request => request.ReviewedBy)
                .WithMany()
                .HasForeignKey(request => request.ReviewedById)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ShiftSwapRequest>(entity =>
        {
            entity.Property(request => request.Note).HasMaxLength(400);
            entity.Property(request => request.ReviewNote).HasMaxLength(400);
            entity.HasOne(request => request.Requester)
                .WithMany()
                .HasForeignKey(request => request.RequesterId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(request => request.TargetEmployee)
                .WithMany()
                .HasForeignKey(request => request.TargetEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(request => request.ReviewedBy)
                .WithMany()
                .HasForeignKey(request => request.ReviewedById)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(request => request.Shift)
                .WithMany()
                .HasForeignKey(request => request.ShiftId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(request => request.CounterShift)
                .WithMany()
                .HasForeignKey(request => request.CounterShiftId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
