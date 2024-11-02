using ManageEmployee.DataTransferObject.GoodsModels;
using ManageEmployee.DataTransferObject.PagingResultModels;
using ManageEmployee.DataTransferObject.SearchModels;
using ManageEmployee.DataTransferObject.SelectModels;
using ManageEmployee.Entities;
using System.Linq.Expressions;
using GoodsEntity = ManageEmployee.Entities.GoodsEntities.Goods;

namespace ManageEmployee.Services.Interfaces.Goods;

public interface IGoodsService
{
    Task<IEnumerable<GoodsEntity>> GetAll(Expression<Func<GoodsEntity, bool>> where, int pageSize = 10);

    Task<GoodsPagingResult> GetPaging(SearchViewModel param, int year);

    Task<bool> CheckExistGoods(GoodsEntity requests);

    Task<string> Create(GoodsEntity param, int year);

    Task<GoodslDetailModel> GetById(int id, int year);

    Task<string> Update(GoodsUpdateModel param, int year);

    Task Delete(int id);

    Task<string> GetExcelReport(SearchViewModel param, bool isManager);

    Task SyncAccountGood(int year);

    Task<string> ImportFromExcel(List<GoodsExportlModel> lstGoods, bool isManager);

    Task<bool> CheckGoodNew(int year);

    Task<IEnumerable<SelectListModel>> GetAllGoodShowWeb();

    Task<PagingResult<GoodsReportPositionModel>> ReportForGoodsInWarehouse(SearchViewModel param, int year);

    Task<string> UpdateGoodsWebsite(GoodsEntity requests);

    Task UpdateMenuTypeForGood(UpdateMenuTypeForGoodModel request);

    Task UpdateStatusGoods(List<int> goodIds, int status);

    Task UpdateGoodIsService(List<int> goodIds);
}
