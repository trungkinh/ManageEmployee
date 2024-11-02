using ManageEmployee.DataTransferObject.PagingRequest;
using ManageEmployee.DataTransferObject.PagingResultModels;
using ManageEmployee.DataTransferObject.Web;

namespace ManageEmployee.Services.Interfaces.Webs;

public interface IWebNewsService
{
    Task<IEnumerable<NewsViewModel>> GetAll();

    Task<PagingResult<NewsViewModel>> SearchNews(WebNewPagingRequestModel searchRequest);

    Task CreateOrUpdate(NewsViewSetupModel request);

    Task<NewsViewDetailModel> GetById(int id);

    Task Delete(int id);
}
