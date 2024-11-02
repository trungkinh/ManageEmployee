namespace ManageEmployee.DataTransferObject.PagingRequest;

public class ChartOfAccountForCashierPagingRequestModel : PagingRequestModel
{
    public int? ChartOfAccountFilterId { get; set; }
    public string? Warehouse { get; set; }
}
