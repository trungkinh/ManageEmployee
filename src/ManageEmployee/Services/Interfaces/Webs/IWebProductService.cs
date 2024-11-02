using GoodsEntity = ManageEmployee.Entities.GoodsEntities.Goods;
using ManageEmployee.DataTransferObject.Web;
using ManageEmployee.DataTransferObject.PagingRequest;
using ManageEmployee.Entities.CategoryEntities;
using ManageEmployee.DataTransferObject.PagingResultModels;

namespace ManageEmployee.Services.Interfaces.Webs;

public interface IWebProductService
{
    Task<List<GoodsEntity>> GetTopProductSell();

    Task<CommonWebResponse> GetProduct(ProductSearchModel search);

    Task<GoodsEntity> GetByIdAsync(int id);

    Task<List<WebProductByCategory>> GetProductCategory();

    Task<List<GoodsEntity>> GetProductsByMenuTypeAsync(string menuType);
    Task<Category> GetCategoryByCodeAsync(string code);
    Task<PagingResult<GoodsEntity>> GetProductsByMenuTypeAsync(string menuType, PagingRequestModel param, bool isService);
}
