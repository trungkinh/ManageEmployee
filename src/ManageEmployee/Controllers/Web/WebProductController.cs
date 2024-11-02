using ManageEmployee.DataTransferObject;
using ManageEmployee.DataTransferObject.PagingRequest;
using ManageEmployee.DataTransferObject.Web;
using ManageEmployee.Services.Interfaces.Categories;
using ManageEmployee.Services.Interfaces.Goods;
using ManageEmployee.Services.Interfaces.Webs;
using Microsoft.AspNetCore.Mvc;

namespace ManageEmployee.Controllers.Web;

[Route("api/[controller]")]
[ApiController]
public class WebProductController : ControllerBase
{
    private readonly IGoodsService _goodsService;
    private readonly IWebProductService _webProductService;
    private readonly ICategoryStatusWebPeriodService _categoryStatusWebPeriodService;
    private readonly IGoodsDetailService _goodsDetailService;

    public WebProductController(
        IGoodsService goodsService,
        IGoodsDetailService goodsDetailService,
        IWebProductService webProductService,
        ICategoryStatusWebPeriodService categoryStatusWebPeriodService)
    {
        _goodsService = goodsService;
        _goodsDetailService = goodsDetailService;
        _webProductService = webProductService;
        _categoryStatusWebPeriodService = categoryStatusWebPeriodService;
    }

    /// <summary>
    /// Danh sách sản phẩm giảm giá hôm nay`
    /// </summary>
    /// <returns></returns>
    [HttpGet("getDealToday")]
    public async Task<IActionResult> GetDealToday()
    {
        var promotion = await _categoryStatusWebPeriodService.GetDealsOfDay();

        return Ok(new CommonWebResponse()
        {
            State = true,
            Code = 200,
            Message = "",
            Data = promotion
        });
    }

    [HttpGet("getAllProduct")]
    public async Task<IActionResult> GetAllProduct(int pageSize)
    {
        var results = await _goodsService.GetAll(x => x.PriceList == "BGC", pageSize);
        return Ok(new CommonWebResponse()
        {
            State = true,
            Code = 200,
            Message = "",
            Data = results
        });
    }

    [HttpPost("getProducts")]
    public async Task<IActionResult> GetProduct(ProductSearchModel search)
    {
        return Ok(await _webProductService.GetProduct(search));
    }

    /// <summary>
    /// Danh sách sản mới nhất
    /// </summary>
    /// <returns></returns>
    [HttpGet("getSampleCategory")]
    public async Task<IActionResult> GetSampleCategory(string? code)
    {
        var results = await _goodsService.GetAll(x => x.GoodsType == code);

        return Ok(new CommonWebResponse()
        {
            State = true,
            Code = 200,
            Message = "",
            Data = results
        });
    }

    /// <summary>
    /// Top 10 sản phẩm bán chạy
    /// </summary>
    /// <returns></returns>
    [HttpGet("get-top-sell")]
    public async Task<IActionResult> GetTopSell()
    {
        var results = await _webProductService.GetTopProductSell();
        return Ok(new CommonWebResponse()
        {
            State = true,
            Code = 200,
            Message = "",
            Data = results
        });
    }

    [HttpGet("get-products-by-menu")]
    public async Task<IActionResult> GetProductByMenu(string? menuType)
    {
        var results = await _webProductService.GetProductsByMenuTypeAsync(menuType);
        return Ok(new CommonWebResponse()
        {
            State = true,
            Code = 200,
            Message = "",
            Data = results
        });
    }

    [HttpGet("get-products-by-menu-paging")]
    public async Task<IActionResult> GetProductByMenu(string? menuType,[FromQuery] PagingRequestModel param, bool isService)
    {
        var results = await _webProductService.GetProductsByMenuTypeAsync(menuType, param, isService);
        return Ok(results);
    }

    /// <summary>
    /// Chi tiet san pham
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpGet("getById/{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var objResult = new WebProductDetailViewModel();
        var good = await _webProductService.GetByIdAsync(id);
        objResult.Good = good;
        objResult.Category = await _webProductService.GetCategoryByCodeAsync(good.MenuType);
        objResult.Images = new List<string>();
        if (!string.IsNullOrEmpty(objResult.Good.Image1))
        {
            objResult.Images.Add(objResult.Good.Image1);
        }

        if (!string.IsNullOrEmpty(objResult.Good.Image2))
        {
            objResult.Images.Add(objResult.Good.Image2);
        }

        if (!string.IsNullOrEmpty(objResult.Good.Image3))
        {
            objResult.Images.Add(objResult.Good.Image3);
        }

        if (!string.IsNullOrEmpty(objResult.Good.Image4))
        {
            objResult.Images.Add(objResult.Good.Image4);
        }

        if (!string.IsNullOrEmpty(objResult.Good.Image5))
        {
            objResult.Images.Add(objResult.Good.Image5);
        }

        objResult.Details = await _goodsDetailService.GetAllByGood(id);
        if (objResult.Good != null)
        {
            return Ok(new CommonWebResponse()
            {
                State = true,
                Code = 200,
                Message = "",
                Data = objResult
            });
        }
        else
        {
            return Ok(new CommonWebResponse()
            {
                State = false,
                Code = 200,
                Message = "",
                Data = objResult
            });
        }
    }

    /// <summary>
    /// Danh sách sản phẩm theo danh mục
    /// </summary>
    /// <returns></returns>
    [HttpGet("getProductCategory")]
    public async Task<IActionResult> GetProductCategory()
    {
        var results = await _webProductService.GetProductCategory();
        return Ok(new CommonWebResponse()
        {
            State = true,
            Code = 200,
            Message = "",
            Data = results
        });
    }

    /// <summary>
    ///  Get sản phẩm theo mã menuWeb
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    [HttpGet("get-good-show-web")]
    public async Task<IActionResult> GetGoodShowWeb(string? code)
    {
        var result = await _categoryStatusWebPeriodService.GetGoodShowWeb(code);
        return Ok(new ObjectReturn()
        {
            data = result,
        });
    }
}