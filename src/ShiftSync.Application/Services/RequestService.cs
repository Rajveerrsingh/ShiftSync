using ShiftSync.Application.Abstractions;
using ShiftSync.Application.Dtos;
using ShiftSync.Application.Mapping;
using ShiftSync.Domain.Entities;
using ShiftSync.Domain.Enums;
using ShiftSync.Domain.Exceptions;
using ShiftSync.Domain.Scheduling;

namespace ShiftSync.Application.Services;

public class RequestService
{
    private readonly ICurrentUser _current;
    private readonly IEmployeeRepository _employees;
    private readonly IShiftRepository _shifts;
    private readonly IRequestRepository _requests;
    private readonly IUnitOfWork _uow;
    private readonly ScheduleService _schedule;

    public RequestService(
        ICurrentUser current,
        IEmployeeRepository employees,
        IShiftRepository shifts,
        IRequestRepository requests,
        IUnitOfWork uow,
        ScheduleService schedule)
    {
        _current = current;
        _employees = employees;
        _shifts = shifts;
        _requests = requests;
        _uow = uow;
        _schedule = schedule;
    }

    public async Task<List<TimeOffDto>> ListTimeOffAsync(CancellationToken ct)
    {
        var rows = await _requests.ListTimeOffForStoreAsync(_current.StoreId, ct);
        if (!_current.IsManager)
            rows = rows.Where(row => row.EmployeeId == _current.EmployeeId).ToList();

        var result = new List<TimeOffDto>();
        foreach (var row in rows)
            result.Add(StaffMap.TimeOff(row, await WarningsAsync(row, ct)));
        return result;
    }

    public async Task<TimeOffDto> CreateTimeOffAsync(CreateTimeOffRequest request, CancellationToken ct)
    {
        if (request.EndDate < request.StartDate)
            throw Errors.Bad("Invalid dates", "Time off has to end on or after the start date.");
        if (request.EndDate.DayNumber - request.StartDate.DayNumber > 31)
            throw Errors.Bad("Invalid dates", "Ask for 31 days or fewer at a time.");

        var employee = await _employees.GetByIdAsync(_current.EmployeeId, ct)
            ?? throw Errors.NotFound("Employee not found.");

        var row = new TimeOffRequest
        {
            EmployeeId = employee.Id,
            Employee = employee,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Reason = request.Reason.Trim(),
            Status = RequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        await _requests.AddTimeOffAsync(row, ct);
        await _uow.SaveChangesAsync(ct);
        return StaffMap.TimeOff(row, await _schedule.ShiftWarningsAsync(employee.Id, row.StartDate, row.EndDate, ct));
    }

    public async Task<TimeOffDto> ReviewTimeOffAsync(int id, ReviewRequest request, CancellationToken ct)
    {
        if (!_current.IsManager)
            throw Errors.Forbidden("Only a manager can review time off.");

        var row = await _requests.GetTimeOffAsync(id, ct) ?? throw Errors.NotFound("Time off request not found.");
        if (row.Employee.StoreId != _current.StoreId)
            throw Errors.NotFound("Time off request not found.");
        if (row.Status != RequestStatus.Pending)
            throw Errors.Bad("Already reviewed", "This request was already reviewed.");

        row.Status = request.Approve ? RequestStatus.Approved : RequestStatus.Denied;
        row.ReviewNote = Clean(request.Note);
        row.ReviewedById = _current.EmployeeId;
        row.ReviewedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);

        var saved = await _requests.ReadTimeOffAsync(id, ct) ?? row;
        return StaffMap.TimeOff(saved, await WarningsAsync(saved, ct));
    }

    public async Task CancelTimeOffAsync(int id, CancellationToken ct)
    {
        var row = await _requests.GetTimeOffAsync(id, ct) ?? throw Errors.NotFound("Time off request not found.");
        if (row.Employee.StoreId != _current.StoreId)
            throw Errors.NotFound("Time off request not found.");
        if (row.EmployeeId != _current.EmployeeId && !_current.IsManager)
            throw Errors.Forbidden("You can cancel your own request.");
        if (row.Status != RequestStatus.Pending)
            throw Errors.Bad("Already reviewed", "Only a pending request can be cancelled.");

        row.Status = RequestStatus.Cancelled;
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<List<SwapDto>> ListSwapsAsync(CancellationToken ct)
    {
        var rows = await _requests.ListSwapsForStoreAsync(_current.StoreId, ct);
        if (!_current.IsManager)
        {
            rows = rows.Where(row =>
                row.RequesterId == _current.EmployeeId
                || row.TargetEmployeeId == _current.EmployeeId
                || row.CounterShift?.EmployeeId == _current.EmployeeId).ToList();
        }

        return rows.Select(StaffMap.Swap).ToList();
    }

    public async Task<SwapDto> CreateSwapAsync(CreateSwapRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<SwapKind>(request.Kind, true, out var kind) || !Enum.IsDefined(kind))
            throw Errors.Bad("Invalid kind", "Kind must be Cover or Exchange.");

        var shift = await _shifts.GetByIdAsync(request.ShiftId, ct) ?? throw Errors.NotFound("Shift not found.");
        if (shift.StoreId != _current.StoreId)
            throw Errors.NotFound("Shift not found.");
        if (shift.EmployeeId != _current.EmployeeId)
            throw Errors.Forbidden("You can only request a swap for your own shift.");
        if (await _requests.HasPendingForShiftAsync(shift.Id, ct))
            throw new AppException(409, "Already requested", "There's already a pending request on this shift.");

        Employee? target = null;
        Shift? counter = null;
        if (kind == SwapKind.Cover)
        {
            if (request.TargetEmployeeId is not int targetId)
                throw Errors.Bad("Missing coworker", "Pick who should cover the shift.");
            target = await RequireCoworkerAsync(targetId, ct);
        }
        else
        {
            if (request.CounterShiftId is not int counterId)
                throw Errors.Bad("Missing shift", "Pick the shift you want to exchange.");
            counter = await _shifts.GetByIdAsync(counterId, ct) ?? throw Errors.NotFound("The other shift was not found.");
            if (counter.StoreId != _current.StoreId)
                throw Errors.NotFound("The other shift was not found.");
            if (counter.Id == shift.Id || counter.EmployeeId == _current.EmployeeId)
                throw Errors.Bad("Pick someone else", "Exchange with a coworker's shift.");
            if (await _requests.HasPendingForShiftAsync(counter.Id, ct))
                throw new AppException(409, "Already requested", "The other shift already has a pending request.");
            target = counter.Employee;
        }

        var requester = await _employees.GetByIdAsync(_current.EmployeeId, ct)
            ?? throw Errors.NotFound("Employee not found.");

        var row = new ShiftSwapRequest
        {
            Kind = kind,
            RequesterId = requester.Id,
            Requester = requester,
            ShiftId = shift.Id,
            Shift = shift,
            CounterShiftId = counter?.Id,
            CounterShift = counter,
            TargetEmployeeId = target?.Id,
            TargetEmployee = target,
            Note = request.Note?.Trim() ?? "",
            Status = RequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        await _requests.AddSwapAsync(row, ct);
        await _uow.SaveChangesAsync(ct);
        return StaffMap.Swap(row);
    }

    public async Task<SwapDto> ReviewSwapAsync(int id, ReviewRequest request, CancellationToken ct)
    {
        if (!_current.IsManager)
            throw Errors.Forbidden("Only a manager can review a swap.");

        var swap = await _requests.GetSwapAsync(id, ct) ?? throw Errors.NotFound("Swap request not found.");
        if (swap.Requester.StoreId != _current.StoreId)
            throw Errors.NotFound("Swap request not found.");
        if (swap.Status != RequestStatus.Pending)
            throw Errors.Bad("Already reviewed", "This request was already reviewed.");

        if (!request.Approve)
        {
            MarkReviewed(swap, RequestStatus.Denied, request.Note);
            await _uow.SaveChangesAsync(ct);
            return StaffMap.Swap(await _requests.ReadSwapAsync(id, ct) ?? swap);
        }

        if (swap.Shift.EmployeeId != swap.RequesterId)
            throw Errors.Bad("Shift moved", "That shift is no longer on the requester's schedule.");

        if (swap.Kind == SwapKind.Cover)
        {
            var target = swap.TargetEmployee ?? throw Errors.Bad("Missing coworker", "A cover request needs a coworker.");
            var conflicts = await _schedule.CheckAsync(
                target.Id,
                new ShiftDraft(swap.Shift.Id, swap.Shift.StartsAt, swap.Shift.EndsAt),
                [swap.Shift.Id],
                ct);
            if (conflicts.Count > 0)
                throw new ConflictException(conflicts);
            swap.Shift.EmployeeId = target.Id;
        }
        else
        {
            var counter = swap.CounterShift ?? throw Errors.Bad("Missing shift", "An exchange needs the other shift.");
            if (counter.EmployeeId == swap.RequesterId)
                throw Errors.Bad("Shift moved", "Those two shifts are already on the same person.");

            var ignore = new[] { swap.Shift.Id, counter.Id };
            var requesterConflicts = await _schedule.CheckAsync(
                swap.RequesterId,
                new ShiftDraft(counter.Id, counter.StartsAt, counter.EndsAt),
                ignore,
                ct);
            var otherConflicts = await _schedule.CheckAsync(
                counter.EmployeeId,
                new ShiftDraft(swap.Shift.Id, swap.Shift.StartsAt, swap.Shift.EndsAt),
                ignore,
                ct);
            var conflicts = requesterConflicts.Concat(otherConflicts).ToList();
            if (conflicts.Count > 0)
                throw new ConflictException(conflicts);

            var otherId = counter.EmployeeId;
            counter.EmployeeId = swap.RequesterId;
            swap.Shift.EmployeeId = otherId;
        }

        MarkReviewed(swap, RequestStatus.Approved, request.Note);
        await _uow.SaveChangesAsync(ct);
        return StaffMap.Swap(await _requests.ReadSwapAsync(id, ct) ?? swap);
    }

    public async Task CancelSwapAsync(int id, CancellationToken ct)
    {
        var swap = await _requests.GetSwapAsync(id, ct) ?? throw Errors.NotFound("Swap request not found.");
        if (swap.Requester.StoreId != _current.StoreId)
            throw Errors.NotFound("Swap request not found.");
        if (swap.RequesterId != _current.EmployeeId && !_current.IsManager)
            throw Errors.Forbidden("You can cancel your own request.");
        if (swap.Status != RequestStatus.Pending)
            throw Errors.Bad("Already reviewed", "Only a pending request can be cancelled.");

        swap.Status = RequestStatus.Cancelled;
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<Employee> RequireCoworkerAsync(int id, CancellationToken ct)
    {
        if (id == _current.EmployeeId)
            throw Errors.Bad("Pick someone else", "You can't cover your own shift.");
        var person = await _employees.GetByIdAsync(id, ct) ?? throw Errors.NotFound("Coworker not found.");
        if (person.StoreId != _current.StoreId || !person.IsActive)
            throw Errors.NotFound("Coworker not found.");
        return person;
    }

    private async Task<List<string>> WarningsAsync(TimeOffRequest row, CancellationToken ct)
    {
        if (row.Status is RequestStatus.Denied or RequestStatus.Cancelled)
            return [];
        return await _schedule.ShiftWarningsAsync(row.EmployeeId, row.StartDate, row.EndDate, ct);
    }

    private void MarkReviewed(ShiftSwapRequest swap, RequestStatus status, string? note)
    {
        swap.Status = status;
        swap.ReviewNote = Clean(note);
        swap.ReviewedById = _current.EmployeeId;
        swap.ReviewedAt = DateTime.UtcNow;
    }

    private static string? Clean(string? note)
    {
        var trimmed = note?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
