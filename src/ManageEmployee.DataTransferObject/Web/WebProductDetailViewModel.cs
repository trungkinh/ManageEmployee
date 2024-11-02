using ManageEmployee.DataTransferObject.GoodsModels;
using ManageEmployee.Entities.CategoryEntities;
using ManageEmployee.Entities.GoodsEntities;

namespace ManageEmployee.DataTransferObject.Web;

public class WebProductDetailViewModel
{
    public Goods? Good { get; set; }
    public Category? Category { get; set; }
    public List<string>? Images { get; set; }
    public IEnumerable<GoodsDetailModel>? Details { get; set; }
}
