namespace ITBees.Products.DbModels;

public class SimCard
{
    public int Id { get; set; }
    public string Serial { get; set; }
    public string Phone { get; set; }
    public DateTime? ActiveTo { get; set; }
    public DateTime? ActiveFrom { get; set; }
    public SimCardOperator SimCardOperator { get; set; }
    public int SimCardOperatorId { get; set; }
    public int MonthlyLimitMb { get; set; }
    public Guid? AssignedGpsDeviceGuid { get; set; }
}