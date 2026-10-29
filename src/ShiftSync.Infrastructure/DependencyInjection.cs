using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShiftSync.Application.Abstractions;
using ShiftSync.Infrastructure.Persistence;
using ShiftSync.Infrastructure.Repositories;

namespace ShiftSync.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var provider = DatabaseSetup.ResolveProvider(config);
        var connectionString = config.GetConnectionString("Default") ?? "Data Source=shiftsync.db";

        services.AddSingleton(new DatabaseOptions(provider));
        services.AddDbContext<AppDbContext>(options => DatabaseSetup.UseProvider(options, provider, connectionString));
        services.AddScoped<IStoreRepository, StoreRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IShiftRepository, ShiftRepository>();
        services.AddScoped<IRequestRepository, RequestRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}
