namespace ITBees.Products.DbModels;

public class SimCardOperator
{
    public int Id { get; set; }
    public string OperatorName { get; set; }
    public string OperatorCode { get; set; }
    public bool IsActive { get; set; }
}