using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ShiftSync.Application.Services;
using ShiftSync.Domain.Entities;

namespace ShiftSync.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IPasswordHasher<Employee>, PasswordHasher<Employee>>();
        services.AddScoped<AuthService>();
        services.AddScoped<EmployeeService>();
        services.AddScoped<ScheduleService>();
        services.AddScoped<RequestService>();
        services.AddScoped<DashboardService>();
        return services;
    }
}
