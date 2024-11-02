using ManageEmployee.Dal.DbContexts;
using ManageEmployee.Helpers;
using Microsoft.EntityFrameworkCore;
using ManageEmployee.Services.Interfaces.Introduces;
using ManageEmployee.DataTransferObject.PagingRequest;
using ManageEmployee.Entities.IntroduceEntities;
using ManageEmployee.DataTransferObject.PagingResultModels;

namespace ManageEmployee.Services;
public class IntroduceTypeService: IIntroduceTypeService
{
    private readonly ApplicationDbContext _context;

    public IntroduceTypeService(ApplicationDbContext context)
    {
        _context = context;
    }
   
    public async Task<IEnumerable<IntroduceType>> GetList()
    {
        return await _context.IntroduceTypes.ToListAsync();
    }

    public async Task<PagingResult<IntroduceType>> GetPaging(PagingRequestModel param)
    {
        var query = _context.IntroduceTypes
                    .Where(x => string.IsNullOrEmpty(param.SearchText) || x.Name.Contains(param.SearchText) || x.Note.Contains(param.SearchText));

        return new PagingResult<IntroduceType>
        {
            CurrentPage = param.Page,
            PageSize = param.PageSize,
            Data = await query.Skip((param.Page) * param.PageSize).Take(param.PageSize).ToListAsync(),
            TotalItems = await query.CountAsync()
        };
    }

    public async Task Create(IntroduceType param)
    {
        
        await _context.IntroduceTypes.AddAsync(param);
        await _context.SaveChangesAsync();
    }

    public async Task Update(IntroduceType param)
    {
        var stationery = await _context.IntroduceTypes.FindAsync(param.Id);
        if (stationery is null)
            throw new ErrorException(ErrorMessages.DataNotFound);
        stationery.Code = param.Code;
        stationery.Name = param.Name;
        stationery.Note = param.Note;
        

        _context.IntroduceTypes.Update(stationery);
        await _context.SaveChangesAsync();
    }

    public async Task Delete(int id)
    {
        var stationery = await _context.IntroduceTypes.FindAsync(id);
        if (stationery is null)
            throw new ErrorException(ErrorMessages.DataNotFound);

        _context.IntroduceTypes.Remove(stationery);
        await _context.SaveChangesAsync();
    }

    public async Task<IntroduceType> GetById(int id)
    {
        var itemOut = await _context.IntroduceTypes.FindAsync(id);
        if (itemOut is null)
            throw new ErrorException(ErrorMessages.DataNotFound);
        
        return itemOut;
    }

}
