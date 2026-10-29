using System.Security.Claims;
using ShiftSync.Application.Abstractions;
using ShiftSync.Domain.Enums;
using ShiftSync.Domain.Exceptions;

namespace ShiftSync.Api.Auth;

public class HttpCurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal _user;

    public HttpCurrentUser(IHttpContextAccessor accessor)
    {
        _user = accessor.HttpContext?.User ?? new ClaimsPrincipal();
    }

    public int EmployeeId => ReadInt("sub");

    public int StoreId => ReadInt("store_id");

    public StaffRole Role
    {
        get
        {
            var value = _user.FindFirst("role")?.Value;
            if (!Enum.TryParse<StaffRole>(value, out var role))
                throw new AppException(401, "Unauthorized", "Sign in again.");
            return role;
        }
    }

    public bool IsManager => Role == StaffRole.Manager;

    private int ReadInt(string claim)
    {
        var value = _user.FindFirst(claim)?.Value;
        if (!int.TryParse(value, out var id))
            throw new AppException(401, "Unauthorized", "Sign in again.");
        return id;
    }
}
