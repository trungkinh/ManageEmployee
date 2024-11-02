using ManageEmployee.DataTransferObject.PagingRequest;

namespace ManageEmployee.DataTransferObject;

public class PayerPagingationRequestModel : PagingRequestModel
{
    public int PayerType { get; set; } = 1;
}
