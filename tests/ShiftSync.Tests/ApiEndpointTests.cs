using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ShiftSync.Tests;

public class ApiEndpointTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _factory;

    public ApiEndpointTests(ApiFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_returns_a_token_and_me_uses_it()
    {
        await _factory.SeedAsync();
        using var client = _factory.CreateClient();

        var token = await client.LoginAsync("raj@test.demo");
        client.UseToken(token);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");

        Assert.Equal("raj@test.demo", me.GetProperty("email").GetString());
        Assert.Equal("Manager", me.GetProperty("role").GetString());
    }

    [Fact]
    public async Task A_bad_password_is_rejected()
    {
        await _factory.SeedAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "raj@test.demo", password = "nope-nope" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(401, body.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Missing_token_is_rejected()
    {
        await _factory.SeedAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/schedule");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Associate_cannot_create_a_shift()
    {
        var (_, associateId) = await _factory.SeedAsync();
        using var client = _factory.CreateClient();
        client.UseToken(await client.LoginAsync("ari@test.demo"));

        var response = await client.PostAsJsonAsync("/api/schedule/shifts", Shift(associateId, "09:00", "13:00"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Manager_can_create_a_shift_and_read_it_back()
    {
        var (_, associateId) = await _factory.SeedAsync();
        using var client = _factory.CreateClient();
        client.UseToken(await client.LoginAsync("raj@test.demo"));

        var created = await client.PostAsJsonAsync("/api/schedule/shifts", Shift(associateId, "09:00", "13:00"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var shift = await created.Content.ReadFromJsonAsync<JsonElement>();

        var week = await client.GetFromJsonAsync<JsonElement>("/api/schedule?weekStart=2026-11-02");
        var ids = week.GetProperty("shifts").EnumerateArray().Select(item => item.GetProperty("id").GetInt32());
        Assert.Contains(shift.GetProperty("id").GetInt32(), ids);
    }

    [Fact]
    public async Task Overlap_comes_back_as_a_conflict()
    {
        var (_, associateId) = await _factory.SeedAsync();
        using var client = _factory.CreateClient();
        client.UseToken(await client.LoginAsync("raj@test.demo"));
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/schedule/shifts", Shift(associateId, "09:00", "13:00"))).StatusCode);

        var response = await client.PostAsJsonAsync("/api/schedule/shifts", Shift(associateId, "12:00", "16:00"));

        await AssertConflict(response, "Overlap");
    }

    [Fact]
    public async Task Back_to_back_shifts_are_saved()
    {
        var (_, associateId) = await _factory.SeedAsync();
        using var client = _factory.CreateClient();
        client.UseToken(await client.LoginAsync("raj@test.demo"));
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/schedule/shifts", Shift(associateId, "09:00", "13:00"))).StatusCode);

        var response = await client.PostAsJsonAsync("/api/schedule/shifts", Shift(associateId, "13:00", "17:00"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Two_people_can_share_the_same_hour()
    {
        var (managerId, associateId) = await _factory.SeedAsync();
        using var client = _factory.CreateClient();
        client.UseToken(await client.LoginAsync("raj@test.demo"));

        var first = await client.PostAsJsonAsync("/api/schedule/shifts", Shift(associateId, "09:00", "13:00"));
        var second = await client.PostAsJsonAsync("/api/schedule/shifts", Shift(managerId, "09:00", "13:00"));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task A_shift_outside_availability_is_rejected()
    {
        var (_, associateId) = await _factory.SeedAsync();
        using var client = _factory.CreateClient();
        client.UseToken(await client.LoginAsync("raj@test.demo"));
        var availability = await client.PutAsJsonAsync($"/api/employees/{associateId}/availability", new[]
        {
            new { dayOfWeek = 1, start = "10:00", end = "14:00" }
        });
        Assert.Equal(HttpStatusCode.OK, availability.StatusCode);

        var response = await client.PostAsJsonAsync("/api/schedule/shifts", Shift(associateId, "15:00", "19:00"));

        await AssertConflict(response, "OutsideAvailability");
    }

    [Fact]
    public async Task Hours_over_the_weekly_cap_are_rejected()
    {
        var (_, associateId) = await _factory.SeedAsync();
        using var client = _factory.CreateClient();
        client.UseToken(await client.LoginAsync("raj@test.demo"));
        var updated = await client.PutAsJsonAsync($"/api/employees/{associateId}", new
        {
            firstName = "Ari",
            lastName = "Cole",
            phone = "",
            maxWeeklyHours = 6,
            isActive = true
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/schedule/shifts", Shift(associateId, "09:00", "13:00"))).StatusCode);

        var response = await client.PostAsJsonAsync("/api/schedule/shifts", Shift(associateId, "13:00", "17:00"));

        await AssertConflict(response, "MaxWeeklyHours");
    }

    [Fact]
    public async Task A_blank_position_is_a_validation_problem()
    {
        var (_, associateId) = await _factory.SeedAsync();
        using var client = _factory.CreateClient();
        client.UseToken(await client.LoginAsync("raj@test.demo"));

        var response = await client.PostAsJsonAsync("/api/schedule/shifts", new
        {
            employeeId = associateId,
            startsAt = "2026-11-02T09:00:00",
            endsAt = "2026-11-02T13:00:00",
            position = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, body.GetProperty("status").GetInt32());
        Assert.True(body.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Time_off_can_be_requested_and_then_blocks_a_shift()
    {
        var (_, associateId) = await _factory.SeedAsync();
        using var client = _factory.CreateClient();
        client.UseToken(await client.LoginAsync("ari@test.demo"));
        var requested = await client.PostAsJsonAsync("/api/requests/time-off", new
        {
            startDate = "2026-11-04",
            endDate = "2026-11-04",
            reason = "Appointment"
        });
        Assert.Equal(HttpStatusCode.Created, requested.StatusCode);
        var id = (await requested.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        client.UseToken(await client.LoginAsync("raj@test.demo"));
        var review = await client.PostAsJsonAsync($"/api/requests/time-off/{id}/review", new { approve = true, note = "Covered" });
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);

        var response = await client.PostAsJsonAsync("/api/schedule/shifts", new
        {
            employeeId = associateId,
            startsAt = "2026-11-04T10:00:00",
            endsAt = "2026-11-04T14:00:00",
            position = "Cash"
        });

        await AssertConflict(response, "ApprovedTimeOff");
    }

    [Fact]
    public async Task Approving_a_cover_moves_the_shift()
    {
        var (_, associateId) = await _factory.SeedAsync();
        using var client = _factory.CreateClient();
        client.UseToken(await client.LoginAsync("raj@test.demo"));
        var coworker = await client.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Ned",
            lastName = "Cole",
            email = "ned@test.demo",
            phone = "",
            role = "Associate",
            maxWeeklyHours = 40,
            password = "test-pass-1"
        });
        Assert.Equal(HttpStatusCode.Created, coworker.StatusCode);
        var coworkerId = (await coworker.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/employees/{coworkerId}/availability", AllWeek())).StatusCode);

        var created = await client.PostAsJsonAsync("/api/schedule/shifts", Shift(associateId, "09:00", "13:00"));
        var shiftId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        client.UseToken(await client.LoginAsync("ari@test.demo"));
        var swap = await client.PostAsJsonAsync("/api/requests/swaps", new
        {
            kind = "Cover",
            shiftId,
            targetEmployeeId = coworkerId,
            note = "Can you open?"
        });
        Assert.Equal(HttpStatusCode.Created, swap.StatusCode);
        var swapId = (await swap.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        client.UseToken(await client.LoginAsync("raj@test.demo"));
        var review = await client.PostAsJsonAsync($"/api/requests/swaps/{swapId}/review", new { approve = true, note = "Go for it" });
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);

        var week = await client.GetFromJsonAsync<JsonElement>("/api/schedule?weekStart=2026-11-02");
        var moved = week.GetProperty("shifts").EnumerateArray().Single(item => item.GetProperty("id").GetInt32() == shiftId);
        Assert.Equal(coworkerId, moved.GetProperty("employeeId").GetInt32());
    }

    [Fact]
    public async Task Approving_a_cover_that_breaks_the_cap_is_rejected()
    {
        var (_, associateId) = await _factory.SeedAsync();
        using var client = _factory.CreateClient();
        client.UseToken(await client.LoginAsync("raj@test.demo"));
        var coworker = await client.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Ned",
            lastName = "Cole",
            email = "ned2@test.demo",
            role = "Associate",
            maxWeeklyHours = 4,
            password = "test-pass-1"
        });
        var coworkerId = (await coworker.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/employees/{coworkerId}/availability", AllWeek())).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/schedule/shifts", new
        {
            employeeId = coworkerId,
            startsAt = "2026-11-03T09:00:00",
            endsAt = "2026-11-03T13:00:00",
            position = "Cash"
        })).StatusCode);

        var created = await client.PostAsJsonAsync("/api/schedule/shifts", Shift(associateId, "09:00", "13:00"));
        var shiftId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        client.UseToken(await client.LoginAsync("ari@test.demo"));
        var swap = await client.PostAsJsonAsync("/api/requests/swaps", new { kind = "Cover", shiftId, targetEmployeeId = coworkerId });
        var swapId = (await swap.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        client.UseToken(await client.LoginAsync("raj@test.demo"));
        var review = await client.PostAsJsonAsync($"/api/requests/swaps/{swapId}/review", new { approve = true });

        await AssertConflict(review, "MaxWeeklyHours");
        var week = await client.GetFromJsonAsync<JsonElement>("/api/schedule?weekStart=2026-11-02");
        var original = week.GetProperty("shifts").EnumerateArray().Single(item => item.GetProperty("id").GetInt32() == shiftId);
        Assert.Equal(associateId, original.GetProperty("employeeId").GetInt32());
    }

    [Fact]
    public async Task An_associate_cannot_approve_time_off()
    {
        await _factory.SeedAsync();
        using var client = _factory.CreateClient();
        client.UseToken(await client.LoginAsync("ari@test.demo"));
        var requested = await client.PostAsJsonAsync("/api/requests/time-off", new
        {
            startDate = "2026-11-05",
            endDate = "2026-11-05",
            reason = "Errand"
        });
        var id = (await requested.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var review = await client.PostAsJsonAsync($"/api/requests/time-off/{id}/review", new { approve = true });

        Assert.Equal(HttpStatusCode.Forbidden, review.StatusCode);
    }

    [Fact]
    public async Task Dashboard_reports_hours_and_gaps()
    {
        var (_, associateId) = await _factory.SeedAsync();
        using var client = _factory.CreateClient();
        client.UseToken(await client.LoginAsync("raj@test.demo"));
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/schedule/shifts", Shift(associateId, "10:00", "14:00"))).StatusCode);

        var dash = await client.GetFromJsonAsync<JsonElement>("/api/dashboard?weekStart=2026-11-02");

        Assert.Equal(4, dash.GetProperty("totalHours").GetDecimal());
        Assert.Equal(1, dash.GetProperty("scheduledPeople").GetInt32());
        Assert.True(dash.GetProperty("gapCount").GetInt32() > 0);
        Assert.Equal(7, dash.GetProperty("days").GetArrayLength());
    }

    private static object Shift(int employeeId, string start, string end) => new
    {
        employeeId,
        startsAt = $"2026-11-02T{start}:00",
        endsAt = $"2026-11-02T{end}:00",
        position = "Cash",
        notes = "Till"
    };

    private static object[] AllWeek()
        => Enumerable.Range(0, 7).Select(day => new { dayOfWeek = day, start = "08:00", end = "20:00" }).ToArray();

    private static async Task AssertConflict(HttpResponseMessage response, string kind)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(409, body.GetProperty("status").GetInt32());
        var kinds = body.GetProperty("conflicts").EnumerateArray().Select(item => item.GetProperty("kind").GetString());
        Assert.Contains(kind, kinds);
    }
}
