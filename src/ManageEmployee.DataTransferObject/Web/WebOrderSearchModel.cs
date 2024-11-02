using ManageEmployee.DataTransferObject.PagingRequest;

namespace ManageEmployee.DataTransferObject.Web;

public class WebOrderSearchModel : PagingRequestModel
{
    public int CustomerId { get; set; }
}
