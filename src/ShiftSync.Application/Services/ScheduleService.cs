using ShiftSync.Application.Abstractions;
using ShiftSync.Application.Dtos;
using ShiftSync.Application.Mapping;
using ShiftSync.Application.Scheduling;
using ShiftSync.Domain.Entities;
using ShiftSync.Domain.Enums;
using ShiftSync.Domain.Exceptions;
using ShiftSync.Domain.Scheduling;

namespace ShiftSync.Application.Services;

public class ScheduleService
{
    private readonly ICurrentUser _current;
    private readonly IStoreRepository _stores;
    private readonly IEmployeeRepository _employees;
    private readonly IShiftRepository _shifts;
    private readonly IRequestRepository _requests;
    private readonly IUnitOfWork _uow;

    public ScheduleService(
        ICurrentUser current,
        IStoreRepository stores,
        IEmployeeRepository employees,
        IShiftRepository shifts,
        IRequestRepository requests,
        IUnitOfWork uow)
    {
        _current = current;
        _stores = stores;
        _employees = employees;
        _shifts = shifts;
        _requests = requests;
        _uow = uow;
    }

    public async Task<WeekScheduleDto> GetWeekAsync(DateOnly? weekStart, CancellationToken ct)
    {
        var start = weekStart.HasValue
            ? StoreClock.WeekContaining(weekStart.Value.ToDateTime(TimeOnly.MinValue))
            : StoreClock.WeekContaining(StoreClock.Today());
        var end = start.AddDays(7);
        var store = await _stores.GetWithHoursAsync(_current.StoreId, ct)
            ?? throw Errors.NotFound("Store not found.");
        var shifts = await _shifts.ListForStoreWeekAsync(_current.StoreId, start, end, ct);
        var people = await _employees.ListForStoreAsync(_current.StoreId, activeOnly: false, ct);
        var scheduled = shifts.Select(shift => shift.EmployeeId).ToHashSet();
        people = people.Where(person => person.IsActive || scheduled.Contains(person.Id)).ToList();

        var timeOff = await _requests.ListApprovedForStoreAsync(
            _current.StoreId,
            DateOnly.FromDateTime(start),
            DateOnly.FromDateTime(end.AddDays(-1)),
            ct);

        var spans = shifts.Select(shift => new ShiftSpan(shift.Id, shift.StartsAt, shift.EndsAt)).ToList();
        var gaps = CoveragePlanner.FindGaps(start, store.MinimumStaff, OpenWindows(store), spans);

        return new WeekScheduleDto
        {
            WeekStart = DateOnly.FromDateTime(start),
            StoreName = store.Name,
            City = store.City,
            MinimumStaff = store.MinimumStaff,
            Hours = store.Hours
                .OrderBy(hour => hour.DayOfWeek)
                .Select(hour => new StoreHourDto
                {
                    DayOfWeek = (int)hour.DayOfWeek,
                    Open = hour.IsClosed ? null : hour.Open.ToString("HH:mm"),
                    Close = hour.IsClosed ? null : hour.Close.ToString("HH:mm"),
                    IsClosed = hour.IsClosed
                })
                .ToList(),
            Employees = people.Select(StaffMap.Employee).ToList(),
            Shifts = shifts.Select(shift => StaffMap.Shift(shift, Flags(shift, timeOff))).ToList(),
            Gaps = gaps.Select(StaffMap.Gap).ToList()
        };
    }

    public async Task<ShiftDto> CreateAsync(UpsertShiftRequest request, CancellationToken ct)
    {
        EnsureManager();
        var employee = await RequireActiveEmployeeAsync(request.EmployeeId, ct);
        var position = ParsePosition(request.Position);
        var starts = StoreClock.AsStoreTime(request.StartsAt);
        var ends = StoreClock.AsStoreTime(request.EndsAt);
        var conflicts = await CheckAsync(employee.Id, new ShiftDraft(null, starts, ends), null, ct);
        if (conflicts.Count > 0)
            throw new ConflictException(conflicts);

        var shift = new Shift
        {
            StoreId = _current.StoreId,
            EmployeeId = employee.Id,
            Employee = employee,
            StartsAt = starts,
            EndsAt = ends,
            Position = position,
            Notes = request.Notes?.Trim() ?? ""
        };
        await _shifts.AddAsync(shift, ct);
        await _uow.SaveChangesAsync(ct);
        return StaffMap.Shift(shift);
    }

    public async Task<ShiftDto> UpdateAsync(int id, UpsertShiftRequest request, CancellationToken ct)
    {
        EnsureManager();
        var shift = await _shifts.GetByIdAsync(id, ct) ?? throw Errors.NotFound("Shift not found.");
        if (shift.StoreId != _current.StoreId)
            throw Errors.NotFound("Shift not found.");

        var employee = await RequireActiveEmployeeAsync(request.EmployeeId, ct);
        var position = ParsePosition(request.Position);
        var starts = StoreClock.AsStoreTime(request.StartsAt);
        var ends = StoreClock.AsStoreTime(request.EndsAt);
        var conflicts = await CheckAsync(employee.Id, new ShiftDraft(shift.Id, starts, ends), null, ct);
        if (conflicts.Count > 0)
            throw new ConflictException(conflicts);

        shift.EmployeeId = employee.Id;
        shift.Employee = employee;
        shift.StartsAt = starts;
        shift.EndsAt = ends;
        shift.Position = position;
        shift.Notes = request.Notes?.Trim() ?? "";
        await _uow.SaveChangesAsync(ct);
        return StaffMap.Shift(shift);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        EnsureManager();
        var shift = await _shifts.GetByIdAsync(id, ct) ?? throw Errors.NotFound("Shift not found.");
        if (shift.StoreId != _current.StoreId)
            throw Errors.NotFound("Shift not found.");

        await _requests.RemoveSwapsForShiftAsync(shift.Id, ct);
        _shifts.Remove(shift);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<List<ConflictDto>> PreviewAsync(PreviewShiftRequest request, CancellationToken ct)
    {
        EnsureManager();
        var starts = StoreClock.AsStoreTime(request.StartsAt);
        var ends = StoreClock.AsStoreTime(request.EndsAt);
        var conflicts = await CheckAsync(
            request.EmployeeId,
            new ShiftDraft(request.ShiftId, starts, ends),
            null,
            ct);
        return conflicts.Select(StaffMap.Conflict).ToList();
    }

    public async Task<IReadOnlyList<ScheduleConflict>> CheckAsync(
        int employeeId,
        ShiftDraft draft,
        IReadOnlyCollection<int>? ignoreShiftIds,
        CancellationToken ct)
    {
        var employee = await RequireActiveEmployeeAsync(employeeId, ct);
        var weekStart = StoreClock.WeekContaining(draft.StartsAt);
        var weekEnd = weekStart.AddDays(7);
        var existing = await _shifts.ListForEmployeeAsync(employee.Id, weekStart, weekEnd, ct);
        var timeOff = await _requests.ListApprovedAsync(
            employee.Id,
            DateOnly.FromDateTime(weekStart),
            DateOnly.FromDateTime(weekEnd.AddDays(-1)),
            ct);

        var spans = existing
            .Where(shift => ignoreShiftIds is null || !ignoreShiftIds.Contains(shift.Id))
            .Select(shift => new ShiftSpan(shift.Id, shift.StartsAt, shift.EndsAt))
            .ToList();

        return ConflictChecker.Evaluate(
            draft,
            employee.MaxWeeklyHours,
            spans,
            employee.Availability
                .Select(window => new AvailabilitySpan(window.DayOfWeek, window.Start, window.End))
                .ToList(),
            timeOff.Select(item => new DateRange(item.StartDate, item.EndDate)).ToList());
    }

    public async Task<List<string>> ShiftWarningsAsync(int employeeId, DateOnly start, DateOnly end, CancellationToken ct)
    {
        var from = start.ToDateTime(TimeOnly.MinValue);
        var to = end.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var shifts = await _shifts.ListForEmployeeAsync(employeeId, from, to, ct);
        return shifts
            .Select(shift => $"{shift.StartsAt:ddd MMM d} still has a {shift.StartsAt:HH:mm}–{shift.EndsAt:HH:mm} shift.")
            .ToList();
    }

    private async Task<Employee> RequireActiveEmployeeAsync(int id, CancellationToken ct)
    {
        var employee = await _employees.GetByIdAsync(id, ct) ?? throw Errors.NotFound("Employee not found.");
        if (employee.StoreId != _current.StoreId)
            throw Errors.NotFound("Employee not found.");
        if (!employee.IsActive)
            throw Errors.Bad("Inactive employee", "That person is marked inactive.");
        return employee;
    }

    private void EnsureManager()
    {
        if (!_current.IsManager)
            throw Errors.Forbidden("Only a manager can change the schedule.");
    }

    private static ShiftPosition ParsePosition(string value)
    {
        if (!Enum.TryParse<ShiftPosition>(value, true, out var position) || !Enum.IsDefined(position))
            throw Errors.Bad("Invalid position", "Position must be SalesFloor, Cash, Stockroom, or FittingRoom.");
        return position;
    }

    private static List<string> Flags(Shift shift, IReadOnlyList<TimeOffRequest> timeOff)
    {
        var day = DateOnly.FromDateTime(shift.StartsAt);
        var onLeave = timeOff.Any(item =>
            item.EmployeeId == shift.EmployeeId && item.StartDate <= day && day <= item.EndDate);
        return onLeave ? ["timeoff"] : [];
    }

    private static List<OpenWindow> OpenWindows(Store store)
        => store.Hours
            .Select(hour => new OpenWindow(hour.DayOfWeek, hour.Open, hour.Close, hour.IsClosed))
            .ToList();
}
