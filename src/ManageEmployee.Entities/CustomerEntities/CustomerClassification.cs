using ManageEmployee.Entities.BaseEntities;

namespace ManageEmployee.Entities.CustomerEntities;

public class CustomerClassification : BaseEntity
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public decimal Purchase { get; set; }
    public bool Status { get; set; }
    public string? Color { get; set; }
    public string? Note { get; set; }
}
