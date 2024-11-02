using ManageEmployee.Dal.DbContexts;
using Microsoft.EntityFrameworkCore;
using ManageEmployee.Helpers;
using ManageEmployee.Services.Interfaces.Sliders;
using ManageEmployee.DataTransferObject.PagingRequest;
using ManageEmployee.DataTransferObject;
using ManageEmployee.Entities;
using ManageEmployee.DataTransferObject.PagingResultModels;

namespace ManageEmployee.Services;
public class SliderService: ISliderService
{
    private readonly ApplicationDbContext _context;

    public SliderService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> Create(SliderModel request)
    {
        var isExisted = _context.Sliders.Any(x => x.Name.ToLower() == request.Name.ToLower() &&
                                                  x.Type == request.Type &&
                                                  !x.IsDelete);
        if (isExisted)
        {
            return ErrorMessages.CategoryNameAlreadyExist;
        }

        var entity = new Slider();
        MappingModelToEntity(entity, request);
        _context.Sliders.Add(entity);
        await _context.SaveChangesAsync();
        return string.Empty;
    }

    private void MappingModelToEntity(Slider entity, SliderModel model)
    {
        entity.Name = model.Name;
        entity.Type = model.Type;
        entity.Img = model.Img;
        entity.AdsensePosition = model.AdsensePosition;
        if (entity.Id <= 0)
        {
            entity.CreatedAt = DateTime.Now;
        }
        entity.UpdatedAt = DateTime.Now;
    }

    public async Task Delete(int id)
    {
        var itemDelete = await _context.Sliders.FindAsync(id);
        if (itemDelete != null)
        {
            itemDelete.IsDelete = true;
            itemDelete.DeleteAt = DateTime.Now;
            _context.Sliders.Update(itemDelete);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<SliderModel>> GetAll()
    {
        return await _context.Sliders.Where(x => !x.IsDelete).Select(x => new SliderModel
        {
            Id = x.Id,
            Name = x.Name,
            Type = x.Type,
            Img = x.Img,
            CreateAt = x.CreatedAt,
            AdsensePosition = x.AdsensePosition,
        }).ToListAsync();
    }

    public async Task<PagingResult<SliderModel>> GetAll(SlideRequestModel param)
    {
        try
        {
            if (param.PageSize <= 0)
                param.PageSize = 20;

            if (param.Page < 1)
                param.Page = 1;

            var query = _context.Sliders.Where(x => !x.IsDelete && x.Id != 0)
                                         .Where(x => string.IsNullOrEmpty(param.SearchText) || (!string.IsNullOrEmpty(x.Name) && x.Name.ToLower().Contains(param.SearchText.ToLower())))
                                         .Where(x => param.Type == null ||  (int)x.Type == param.Type)
                                         .Where(x => param.AdsensePosition == null || x.AdsensePosition == param.AdsensePosition)

                                         .Select(x => new SliderModel
                                         {
                                             Id = x.Id,
                                             Name = x.Name,
                                             Type = x.Type,
                                             Img = x.Img,
                                             CreateAt = x.CreatedAt,
                                             AdsensePosition = x.AdsensePosition
                                         });

            return new PagingResult<SliderModel>()
            {
                CurrentPage = param.Page,
                PageSize = param.PageSize,
                TotalItems = await query.CountAsync(),
                Data = await query.Skip((param.Page - 1) * param.PageSize).Take(param.PageSize).ToListAsync()
            };
        }
        catch
        {
            return new PagingResult<SliderModel>()
            {
                CurrentPage = param.Page,
                PageSize = param.PageSize,
                TotalItems = 0,
                Data = new List<SliderModel>()
            };
        }
    }

    public async Task<SliderModel> GetById(int id)
    {
        var itemData = await _context.Sliders.Where(x => x.Id == id && !x.IsDelete).FirstOrDefaultAsync();
        if (itemData != null)
        {
            return new SliderModel
            {
                Id = itemData.Id,
                Name = itemData.Name,
                Type = itemData.Type,
                CreateAt = itemData.CreatedAt,
                Img = itemData.Img,
                AdsensePosition = itemData.AdsensePosition,
            };
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    public async Task<string> Update(SliderModel request)
    {
        var itemUpdate = await _context.Sliders.FirstOrDefaultAsync(x => x.Id == request.Id && !x.IsDelete);
        if (itemUpdate == null)
        {
            return ErrorMessages.DataNotFound;
        }

        var isExistedName = await _context.Sliders.AnyAsync(x =>
            x.Name.ToLower() == request.Name.ToLower() &&
            x.Type == request.Type &&
            !x.IsDelete &&
            x.Id != itemUpdate.Id);

        if (isExistedName)
        {
            return ErrorMessages.NameAlreadyExist;
        }

        MappingModelToEntity(itemUpdate, request);
        
        _context.Sliders.Update(itemUpdate);
        await _context.SaveChangesAsync();
        return string.Empty;
    }
}
