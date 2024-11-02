using ManageEmployee.DataTransferObject.PagingRequest;
using ManageEmployee.DataTransferObject.PagingResultModels;
using ManageEmployee.Entities.IntroduceEntities;

namespace ManageEmployee.Services.Interfaces.Introduces;

public interface IIntroduceTypeService
{
    Task Create(IntroduceType param);
    Task Delete(int id);
    Task<IntroduceType> GetById(int id);
    Task<IEnumerable<IntroduceType>> GetList();
    Task<PagingResult<IntroduceType>> GetPaging(PagingRequestModel param);
    Task Update(IntroduceType param);
}
