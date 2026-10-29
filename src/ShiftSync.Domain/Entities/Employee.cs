using System.ComponentModel.DataAnnotations.Schema;
using ShiftSync.Domain.Enums;

namespace ShiftSync.Domain.Entities;

public class Employee
{
    public int Id { get; set; }
    public int StoreId { get; set; }
    public Store Store { get; set; } = null!;
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public StaffRole Role { get; set; }
    public string Phone { get; set; } = "";
    public decimal MaxWeeklyHours { get; set; } = 40;
    public bool IsActive { get; set; } = true;
    public string Color { get; set; } = "#b5522a";
    public List<AvailabilityWindow> Availability { get; set; } = new();
    public List<Shift> Shifts { get; set; } = new();

    [NotMapped]
    public string FullName => $"{FirstName} {LastName}".Trim();
}
