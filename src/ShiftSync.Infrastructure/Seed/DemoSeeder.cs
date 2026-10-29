using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShiftSync.Application.Scheduling;
using ShiftSync.Domain.Entities;
using ShiftSync.Domain.Enums;
using ShiftSync.Infrastructure.Persistence;

namespace ShiftSync.Infrastructure.Seed;

public static class DemoSeeder
{
    public const string Password = "Northline#1";

    // Wednesday and Friday evenings are left under the two-person minimum on purpose.
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Employees.AnyAsync(ct))
            return;

        var week = StoreClock.WeekContaining(StoreClock.Today());
        var hasher = new PasswordHasher<Employee>();

        var store = new Store
        {
            Name = "Northline Outfitters",
            City = "Toronto",
            Address = "418 Queen Street West",
            MinimumStaff = 2
        };

        AddHours(store, DayOfWeek.Monday, "10:00", "20:00");
        AddHours(store, DayOfWeek.Tuesday, "10:00", "20:00");
        AddHours(store, DayOfWeek.Wednesday, "10:00", "20:00");
        AddHours(store, DayOfWeek.Thursday, "10:00", "20:00");
        AddHours(store, DayOfWeek.Friday, "10:00", "20:00");
        AddHours(store, DayOfWeek.Saturday, "10:00", "18:00");
        AddHours(store, DayOfWeek.Sunday, "12:00", "17:00");

        var rajveer = Person(store, hasher, "Rajveer", "Singh", "rajveer.singh@northline.demo", StaffRole.Manager, 44, "(416) 555-0142", "#b5522a");
        var amira = Person(store, hasher, "Amira", "Hassan", "amira.hassan@northline.demo", StaffRole.Associate, 36, "(416) 555-0194", "#1f6f6a");
        var liam = Person(store, hasher, "Liam", "Chen", "liam.chen@northline.demo", StaffRole.Associate, 24, "(647) 555-0133", "#3d5a80");
        var priya = Person(store, hasher, "Priya", "Nair", "priya.nair@northline.demo", StaffRole.Associate, 40, "(416) 555-0177", "#8a5a44");
        var owen = Person(store, hasher, "Owen", "Blake", "owen.blake@northline.demo", StaffRole.Associate, 32, "(437) 555-0118", "#5c6b3c");
        var sofia = Person(store, hasher, "Sofia", "Martins", "sofia.martins@northline.demo", StaffRole.Associate, 20, "(416) 555-0160", "#7a4e7a");
        var jonah = Person(store, hasher, "Jonah", "Adeyemi", "jonah.adeyemi@northline.demo", StaffRole.Associate, 30, "(647) 555-0185", "#a15c12");

        Window(rajveer, DayOfWeek.Monday, DayOfWeek.Saturday, "09:00", "21:00");
        Window(amira, DayOfWeek.Monday, DayOfWeek.Friday, "11:00", "20:00");
        Window(liam, DayOfWeek.Tuesday, "16:00", "21:00");
        Window(liam, DayOfWeek.Thursday, "16:00", "21:00");
        Window(liam, DayOfWeek.Saturday, DayOfWeek.Sunday, "10:00", "18:00");
        Window(priya, DayOfWeek.Wednesday, DayOfWeek.Sunday, "09:00", "18:00");
        Window(owen, DayOfWeek.Monday, DayOfWeek.Friday, "08:00", "16:00");
        Window(sofia, DayOfWeek.Friday, DayOfWeek.Sunday, "10:00", "18:00");
        Window(jonah, DayOfWeek.Monday, DayOfWeek.Tuesday, "10:00", "20:00");
        Window(jonah, DayOfWeek.Friday, DayOfWeek.Saturday, "10:00", "20:00");

        foreach (var day in new[] { 0, 1, 2, 3, 4 })
            Shift(rajveer, store, week, day, 9, 0, 17, 0, ShiftPosition.SalesFloor);

        foreach (var day in new[] { 0, 1, 2, 3 })
            Shift(amira, store, week, day, 12, 0, 20, 0, ShiftPosition.SalesFloor);

        Shift(liam, store, week, 1, 16, 0, 20, 0, ShiftPosition.Cash);
        Shift(liam, store, week, 3, 16, 0, 20, 0, ShiftPosition.Cash);
        var liamSaturday = Shift(liam, store, week, 5, 10, 0, 16, 0, ShiftPosition.Cash);
        Shift(liam, store, week, 6, 12, 0, 17, 0, ShiftPosition.Cash);

        foreach (var day in new[] { 2, 3, 4 })
            Shift(priya, store, week, day, 10, 0, 18, 0, ShiftPosition.SalesFloor);
        var priyaSaturday = Shift(priya, store, week, 5, 10, 0, 18, 0, ShiftPosition.SalesFloor);

        foreach (var day in new[] { 0, 1, 2, 3 })
            Shift(owen, store, week, day, 8, 0, 16, 0, ShiftPosition.Stockroom);

        var sofiaFriday = Shift(sofia, store, week, 4, 12, 0, 18, 0, ShiftPosition.FittingRoom);
        Shift(sofia, store, week, 5, 12, 0, 18, 0, ShiftPosition.FittingRoom);
        Shift(sofia, store, week, 6, 12, 0, 17, 0, ShiftPosition.FittingRoom);

        Shift(jonah, store, week, 0, 12, 0, 20, 0, ShiftPosition.Cash);
        Shift(jonah, store, week, 1, 12, 0, 20, 0, ShiftPosition.Cash);
        Shift(jonah, store, week, 4, 12, 0, 20, 0, ShiftPosition.Cash);

        db.Stores.Add(store);
        await db.SaveChangesAsync(ct);

        db.TimeOffRequests.Add(new TimeOffRequest
        {
            EmployeeId = sofia.Id,
            StartDate = DateOnly.FromDateTime(week.AddDays(7)),
            EndDate = DateOnly.FromDateTime(week.AddDays(8)),
            Reason = "Sister's wedding in Hamilton.",
            Status = RequestStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });

        db.TimeOffRequests.Add(new TimeOffRequest
        {
            EmployeeId = owen.Id,
            StartDate = DateOnly.FromDateTime(week.AddDays(11)),
            EndDate = DateOnly.FromDateTime(week.AddDays(11)),
            Reason = "Dentist, late morning.",
            Status = RequestStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddHours(-5)
        });

        db.TimeOffRequests.Add(new TimeOffRequest
        {
            EmployeeId = amira.Id,
            StartDate = DateOnly.FromDateTime(week.AddDays(-7)),
            EndDate = DateOnly.FromDateTime(week.AddDays(-7)),
            Reason = "Family visiting from Ottawa.",
            Status = RequestStatus.Denied,
            ReviewNote = "Monday open is already thin.",
            ReviewedById = rajveer.Id,
            ReviewedAt = DateTime.UtcNow.AddDays(-6),
            CreatedAt = DateTime.UtcNow.AddDays(-8)
        });

        db.ShiftSwapRequests.Add(new ShiftSwapRequest
        {
            Kind = SwapKind.Cover,
            RequesterId = liam.Id,
            ShiftId = liamSaturday.Id,
            TargetEmployeeId = jonah.Id,
            Note = "Midterm Saturday morning. Jonah said he can open.",
            Status = RequestStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddHours(-8)
        });

        db.ShiftSwapRequests.Add(new ShiftSwapRequest
        {
            Kind = SwapKind.Exchange,
            RequesterId = priya.Id,
            ShiftId = priyaSaturday.Id,
            CounterShiftId = sofiaFriday.Id,
            TargetEmployeeId = sofia.Id,
            Note = "Priya can take Friday if Sofia wants the Saturday.",
            Status = RequestStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddHours(-3)
        });

        await db.SaveChangesAsync(ct);
    }

    private static Employee Person(
        Store store,
        PasswordHasher<Employee> hasher,
        string first,
        string last,
        string email,
        StaffRole role,
        decimal maxHours,
        string phone,
        string color)
    {
        var employee = new Employee
        {
            Store = store,
            FirstName = first,
            LastName = last,
            Email = email.ToLowerInvariant(),
            Role = role,
            MaxWeeklyHours = maxHours,
            Phone = phone,
            Color = color,
            IsActive = true
        };
        employee.PasswordHash = hasher.HashPassword(employee, Password);
        store.Employees.Add(employee);
        return employee;
    }

    private static void Window(Employee employee, DayOfWeek from, DayOfWeek to, string start, string end)
    {
        var day = from;
        while (true)
        {
            Window(employee, day, start, end);
            if (day == to)
                break;
            day = (DayOfWeek)(((int)day + 1) % 7);
        }
    }

    private static void Window(Employee employee, DayOfWeek day, string start, string end)
    {
        employee.Availability.Add(new AvailabilityWindow
        {
            DayOfWeek = day,
            Start = TimeOnly.Parse(start),
            End = TimeOnly.Parse(end)
        });
    }

    private static void AddHours(Store store, DayOfWeek day, string open, string close)
    {
        store.Hours.Add(new StoreHour
        {
            DayOfWeek = day,
            Open = TimeOnly.Parse(open),
            Close = TimeOnly.Parse(close)
        });
    }

    private static Shift Shift(
        Employee employee,
        Store store,
        DateTime week,
        int dayOffset,
        int startHour,
        int startMinute,
        int endHour,
        int endMinute,
        ShiftPosition position)
    {
        var day = week.AddDays(dayOffset);
        var shift = new Shift
        {
            Store = store,
            Employee = employee,
            StartsAt = day.AddHours(startHour).AddMinutes(startMinute),
            EndsAt = day.AddHours(endHour).AddMinutes(endMinute),
            Position = position
        };
        employee.Shifts.Add(shift);
        return shift;
    }
}
