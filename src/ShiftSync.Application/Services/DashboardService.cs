using ShiftSync.Application.Abstractions;
using ShiftSync.Application.Dtos;
using ShiftSync.Application.Mapping;
using ShiftSync.Application.Scheduling;
using ShiftSync.Domain.Enums;
using ShiftSync.Domain.Exceptions;
using ShiftSync.Domain.Scheduling;

namespace ShiftSync.Application.Services;

public class DashboardService
{
    private readonly ICurrentUser _current;
    private readonly IStoreRepository _stores;
    private readonly IShiftRepository _shifts;
    private readonly IRequestRepository _requests;

    public DashboardService(
        ICurrentUser current,
        IStoreRepository stores,
        IShiftRepository shifts,
        IRequestRepository requests)
    {
        _current = current;
        _stores = stores;
        _shifts = shifts;
        _requests = requests;
    }

    public async Task<DashboardDto> GetAsync(DateOnly? weekStart, CancellationToken ct)
    {
        var start = weekStart.HasValue
            ? StoreClock.WeekContaining(weekStart.Value.ToDateTime(TimeOnly.MinValue))
            : StoreClock.WeekContaining(StoreClock.Today());
        var end = start.AddDays(7);
        var store = await _stores.GetWithHoursAsync(_current.StoreId, ct)
            ?? throw Errors.NotFound("Store not found.");
        var shifts = await _shifts.ListForStoreWeekAsync(_current.StoreId, start, end, ct);
        var spans = shifts.Select(shift => new ShiftSpan(shift.Id, shift.StartsAt, shift.EndsAt)).ToList();
        var gaps = CoveragePlanner.FindGaps(
            start,
            store.MinimumStaff,
            store.Hours.Select(hour => new OpenWindow(hour.DayOfWeek, hour.Open, hour.Close, hour.IsClosed)).ToList(),
            spans);

        var today = StoreClock.Today();
        var includesToday = today >= start && today < end;

        var days = new List<DayLabourDto>();
        for (var offset = 0; offset < 7; offset++)
        {
            var date = start.AddDays(offset);
            var dayShifts = shifts.Where(shift => shift.StartsAt.Date == date.Date).ToList();
            days.Add(new DayLabourDto
            {
                Date = DateOnly.FromDateTime(date),
                Hours = dayShifts.Sum(shift => ConflictChecker.Hours(shift.StartsAt, shift.EndsAt)),
                ShiftCount = dayShifts.Count
            });
        }

        var positions = shifts
            .GroupBy(shift => shift.Position)
            .Select(group => new PositionHoursDto
            {
                Position = group.Key.ToString(),
                Hours = group.Sum(shift => ConflictChecker.Hours(shift.StartsAt, shift.EndsAt))
            })
            .OrderByDescending(item => item.Hours)
            .ToList();

        return new DashboardDto
        {
            WeekStart = DateOnly.FromDateTime(start),
            StoreName = store.Name,
            TotalHours = shifts.Sum(shift => ConflictChecker.Hours(shift.StartsAt, shift.EndsAt)),
            ScheduledPeople = shifts.Select(shift => shift.EmployeeId).Distinct().Count(),
            GapCount = gaps.Count,
            PendingTimeOff = await _requests.CountTimeOffAsync(_current.StoreId, RequestStatus.Pending, ct),
            PendingSwaps = await _requests.CountSwapsAsync(_current.StoreId, RequestStatus.Pending, ct),
            IncludesToday = includesToday,
            Days = days,
            Positions = positions,
            Gaps = gaps.Select(StaffMap.Gap).ToList(),
            Today = includesToday
                ? shifts.Where(shift => shift.StartsAt.Date == today.Date).Select(shift => StaffMap.Shift(shift)).ToList()
                : []
        };
    }
}
