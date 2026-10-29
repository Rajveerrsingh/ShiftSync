namespace ShiftSync.Domain.Entities;

public class Store
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string City { get; set; } = "";
    public string Address { get; set; } = "";
    public int MinimumStaff { get; set; } = 2;
    public List<StoreHour> Hours { get; set; } = new();
    public List<Employee> Employees { get; set; } = new();
}
