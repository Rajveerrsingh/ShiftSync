namespace ShiftSync.Domain.Entities;

public class StoreHour
{
    public int Id { get; set; }
    public int StoreId { get; set; }
    public Store Store { get; set; } = null!;
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly Open { get; set; }
    public TimeOnly Close { get; set; }
    public bool IsClosed { get; set; }
}
