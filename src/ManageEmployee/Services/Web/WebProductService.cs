using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ManageEmployee.Dal.DbContexts;
using ManageEmployee.Helpers;
using ManageEmployee.Services.Interfaces.Webs;
using ManageEmployee.DataTransferObject.Web;
using ManageEmployee.DataTransferObject.PagingRequest;
using ManageEmployee.Entities.Enumerations;
using ManageEmployee.Entities.GoodsEntities;
using ManageEmployee.Entities.CategoryEntities;
using ManageEmployee.DataTransferObject.Enums;
using ManageEmployee.DataTransferObject.UserModels;
using ManageEmployee.DataTransferObject.PagingResultModels;

namespace ManageEmployee.Services.Web;

public class WebProductService : IWebProductService
{
    private readonly ApplicationDbContext _context;

    public WebProductService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Goods> GetByIdAsync(int id)
    {
        var good = await _context.Goods.Where(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (good is null)
        {
            throw new ErrorException(ErrorMessages.DataNotFound);
        }
        return good;
    }

    public async Task<Category> GetCategoryByCodeAsync(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return default;
        }

        var category = await _context.Categories.Where(x => x.Code == code && !x.IsDeleted).FirstOrDefaultAsync();
        return category;
    }

    public async Task<CommonWebResponse> GetProduct(ProductSearchModel search)
    {
        if (search.Page < 1)
            search.Page = 1;

        var query = _context.Goods.Where(x => !x.IsDeleted && x.PriceList == "BGC")
            .Where(x => string.IsNullOrEmpty(search.SearchText) ||
                        (!string.IsNullOrEmpty(x.DetailName2) &&
                         x.DetailName2.ToLower().Contains(search.SearchText.ToLower())) ||
                        (!string.IsNullOrEmpty(x.DetailName1) &&
                         x.DetailName1.ToLower().Contains(search.SearchText.ToLower())))
            .Where(x => string.IsNullOrEmpty(search.CategoryCode) ||
                        x.MenuType.ToLower() == search.CategoryCode.ToLower())
            .Where(x => search.AmountFrom == null || x.SalePrice >= search.AmountFrom)
            .Where(x => search.AmountTo == null || x.SalePrice <= search.AmountTo);
        List<Goods> goods = new();
        switch (search.SortType)
        {
            case SortType.INCREASE_PRICE:
                goods = await query.OrderBy(x => x.Price).Skip((search.Page - 1) * search.PageSize).Take(search.PageSize)
            .ToListAsync();
                break;

            case SortType.REDUCE_PRICE:
                goods = await query.OrderByDescending(x => x.Price).Skip((search.Page - 1) * search.PageSize).Take(search.PageSize)
            .ToListAsync();
                break;

            default:
                break;
        }

        var result = new PagingResult<Goods>
        {
            CurrentPage = search.Page,
            PageSize = search.PageSize,
            TotalItems = await query.CountAsync(),
            Data = goods
        };
        return new CommonWebResponse()
        {
            State = true,
            Code = 200,
            Message = "",
            Data = result
        };
    }

    /// <summary>
    /// Danh sách sản phẩm bán chạy
    /// </summary>
    /// <returns></returns>
    /// <exception cref="System.NotImplementedException"></exception>
    public async Task<List<Goods>> GetTopProductSell()
    {
        return await _context.Goods.Take(10).ToListAsync();
    }

    /// <summary>
    /// Danh sách sản phẩm theo danh mục
    /// </summary>
    /// <returns></returns>
    public async Task<List<WebProductByCategory>> GetProductCategory()
    {
        var result = new List<WebProductByCategory>();
        var categories = await _context.Categories.Where(x =>
                x.IsDeleted != true && x.Type == (int)CategoryEnum.MenuWeb && string.IsNullOrEmpty(x.CodeParent))
            .Take(10).ToListAsync();
        if (categories?.Count > 0)
        {
            foreach (var category in categories)
            {
                var productByCategory = new WebProductByCategory();
                productByCategory.CategoryName = category.Name;
                productByCategory.CategoryCode = category.Code;
                productByCategory.CategoryImages = category.Image != null
                && category.Image != "" ? JsonSerializer.Deserialize<List<UserTaskFileModel>>(category.Image) : new List<UserTaskFileModel>();

                var categoryWeb = await _context.CategoryStatusWebPeriods.FirstOrDefaultAsync(x => x.CategoryId == category.Id);
                if (categoryWeb is null)
                    continue;
                var goodIds = await _context.CategoryStatusWebPeriodGoods.Where(x => x.CategoryStatusWebPeriodId == categoryWeb.Id).Select(x => x.GoodId).ToListAsync();
                if (goodIds.Any())
                    continue;
                productByCategory.Products = await _context.Goods.Where(g => goodIds.Contains(g.Id))
                    .Take(5).ToListAsync();
                result.Add(productByCategory);
            }
        }

        return result;
    }

    public async Task<List<Goods>> GetProductsByMenuTypeAsync(string menuType)
    {
        return await _context.Goods.Where(x => x.MenuType == menuType && x.Status == 1).Take(12)
        .ToListAsync();
    }

    public async Task<PagingResult<Goods>> GetProductsByMenuTypeAsync(string menuType, PagingRequestModel param, bool isService)
    {
        if (param.Page < 0)
            param.Page = 0;
        var query = _context.Goods.Where(x => (string.IsNullOrEmpty(menuType) || x.MenuType == menuType) && x.Status == 1 && x.IsService == isService);


        var data = new PagingResult<Goods>()
        {
            Data = await query.Skip(param.Page* param.PageSize).Take(param.PageSize).ToListAsync(),
            TotalItems = await query.CountAsync(),
            PageSize = param.PageSize,
            CurrentPage = param.Page
        };

        return data;
    }
}