using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShiftSync.Domain.Entities;
using ShiftSync.Domain.Enums;
using ShiftSync.Infrastructure.Persistence;

namespace ShiftSync.Tests;

public class ApiFixture : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"shiftsync-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            var descriptors = services.Where(service =>
                service.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                service.ServiceType == typeof(AppDbContext)).ToList();
            foreach (var descriptor in descriptors)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={_dbPath}"));
        });
    }

    public async Task<(int ManagerId, int AssociateId)> SeedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        SqliteConnection.ClearAllPools();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

        var hasher = new PasswordHasher<Employee>();
        var store = new Store
        {
            Name = "Test Outfitters",
            City = "Toronto",
            Address = "1 Test Street",
            MinimumStaff = 2
        };
        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
        {
            store.Hours.Add(new StoreHour
            {
                DayOfWeek = day,
                Open = new TimeOnly(8, 0),
                Close = new TimeOnly(20, 0)
            });
        }

        var manager = Make(store, hasher, "Raj", "Singh", "raj@test.demo", StaffRole.Manager, 40);
        var associate = Make(store, hasher, "Ari", "Cole", "ari@test.demo", StaffRole.Associate, 40);
        store.Employees.Add(manager);
        store.Employees.Add(associate);
        db.Add(store);
        await db.SaveChangesAsync();
        return (manager.Id, associate.Id);
    }

    private static Employee Make(
        Store store,
        PasswordHasher<Employee> hasher,
        string first,
        string last,
        string email,
        StaffRole role,
        decimal hours)
    {
        var employee = new Employee
        {
            Store = store,
            FirstName = first,
            LastName = last,
            Email = email,
            Role = role,
            MaxWeeklyHours = hours,
            Phone = "555",
            Color = "#333333",
            IsActive = true
        };
        employee.PasswordHash = hasher.HashPassword(employee, ApiClient.Password);
        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
        {
            employee.Availability.Add(new AvailabilityWindow
            {
                DayOfWeek = day,
                Start = new TimeOnly(8, 0),
                End = new TimeOnly(20, 0)
            });
        }
        return employee;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // The host can still be releasing the file.
            }
        }
    }
}

public static class ApiClient
{
    public const string Password = "test-pass-1";

    public static async Task<string> LoginAsync(this HttpClient client, string email, string? password = null)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = password ?? Password });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(body.ToString());
        return body.GetProperty("token").GetString()!;
    }

    public static void UseToken(this HttpClient client, string token)
        => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
}
