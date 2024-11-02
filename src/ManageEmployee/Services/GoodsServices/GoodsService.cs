using AutoMapper;
using Common.Errors;
using ManageEmployee.Helpers;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System.Linq.Expressions;
using ManageEmployee.Dal.DbContexts;
using ManageEmployee.Services.Interfaces.Goods;
using ManageEmployee.Services.Interfaces.Ledgers.V2;
using ManageEmployee.Entities;
using ManageEmployee.Entities.Enumerations;
using ManageEmployee.DataTransferObject.GoodsModels;
using ManageEmployee.Entities.GoodsEntities;
using ManageEmployee.Entities.CategoryEntities;
using ManageEmployee.Entities.ChartOfAccountEntities;
using ManageEmployee.DataTransferObject.SearchModels;
using ManageEmployee.DataTransferObject.SelectModels;
using ManageEmployee.DataTransferObject.PagingResultModels;
using ManageEmployee.Entities.HotelEntities.RoomEntities;

namespace ManageEmployee.Services.GoodsServices;

public class GoodsService : IGoodsService
{
    private readonly ApplicationDbContext _context;
    private readonly IChartOfAccountV2Service _chartOfAccountV2Service;
    private readonly IMapper _mapper;
    public GoodsService(ApplicationDbContext context,
        IChartOfAccountV2Service chartOfAccountV2Service,
        IMapper mapper)
    {
        _context = context;
        _chartOfAccountV2Service = chartOfAccountV2Service;
        _mapper = mapper;
    }

    public async Task<IEnumerable<Goods>> GetAll(Expression<Func<Goods, bool>> where, int pageSize = 10)
    {
        return await _context.Goods.Where(x => !x.IsDeleted).Where(where).OrderByDescending(x => x.DiscountPrice).Take(pageSize).ToListAsync();
    }

    public async Task<GoodsPagingResult> GetPaging(SearchViewModel param, int year)
    {
        try
        {
            if (param.PageSize <= 0)
                param.PageSize = 20;

            if (param.Page < 0)
                param.Page = 1;
            var results = GetAll_Common(param);

            if (!string.IsNullOrEmpty(param.Warehouse))
            {
                results = results.Where(x => x.Warehouse == param.Warehouse);
            }
            if (!string.IsNullOrEmpty(param.GoodCode))
            {
                results = results.Where(x => x.Detail1 == param.GoodCode || x.Detail2 == param.GoodCode);
            }
            if (param.MinStockType > 0)
            {
                results = results.Where(x => (x.Quantity ?? 0) - x.MinStockLevel < 0);
            }

            var searchTexts = new List<string>();
            if (!string.IsNullOrEmpty(param.SearchText))
            {
                searchTexts = param.SearchText.Split("&").Select(x => x.ToLower()).ToList();
            }

            List<GoodsExportlModel> goodDatas = await results.ToListAsync();
            if (searchTexts.Any())
            {
                goodDatas = goodDatas.Where(p => (!string.IsNullOrEmpty(p.Detail2) && searchTexts.All(s => p.Detail2.ToLower().Contains(s)))
                                              || (!string.IsNullOrEmpty(p.DetailName2) && searchTexts.All(s => p.DetailName2.ToLower().Contains(s)))
                                             || (!string.IsNullOrEmpty(p.Detail1) && searchTexts.All(s => p.Detail1.ToLower().Contains(s)))
                                             || (!string.IsNullOrEmpty(p.DetailName1) && searchTexts.All(s => p.DetailName1.ToLower().Contains(s)))
                                             ).ToList();
            }
            //storege
            if (param.isCashier || param.isManage)
            {
                var listStorege = await _context.GetChartOfAccount(year).Where(x => (x.Classification == 2 || x.Classification == 3) && !x.HasChild).ToListAsync();
                foreach (var item in goodDatas)
                {
                    ChartOfAccount storege;
                    if (!string.IsNullOrEmpty(item.Detail2))
                    {
                        string parentRef = item.Account + ":" + item.Detail1;
                        storege = listStorege.Find(x => x.Code == item.Detail2 && x.ParentRef == parentRef &&
                                (string.IsNullOrEmpty(item.Warehouse) || x.WarehouseCode == item.Warehouse));
                    }
                    else if (!string.IsNullOrEmpty(item.Detail1))
                        storege = listStorege.Find(x => x.Code == item.Detail1 && x.ParentRef == item.Account &&
                        (string.IsNullOrEmpty(item.Warehouse) || x.WarehouseCode == item.Warehouse));
                    else
                        storege = listStorege.Find(x => x.Code == item.Account);

                    if (storege != null)
                    {
                        item.Quantity = (storege.OpeningStockQuantityNB ?? 0) + (storege.ArisingStockQuantityNB ?? 0);
                        item.StockUnit = storege.StockUnit;
                        item.OpeningStockQuantityNB = storege.OpeningStockQuantityNB;
                    }
                }

                if (param.isQuantityStock)
                    goodDatas = goodDatas.Where(x => x.Quantity > 0).ToList();
            }

            var goods = param.Page == 0 ? goodDatas.OrderBy(x => x.Detail1).ToList() : goodDatas.OrderBy(x => x.Detail1).Skip((param.Page - 1) * param.PageSize).Take(param.PageSize).ToList();

            var company = await _context.Companies.FirstOrDefaultAsync();
            var goodsQuotaIds = goods.Select(x => x.GoodsQuotaId);
            var goodsQuotas = await _context.GoodsQuotas.Where(x => goodsQuotaIds.Contains(x.Id)).ToListAsync();

            foreach (var item in goods)
            {
                if (param.isCashier || param.isManage)
                {
                    item.QrCodes = await _context.GoodWarehouses.Where(x =>
                                        (string.IsNullOrEmpty(x.Account) || x.Account == item.Account) &&
                                        (string.IsNullOrEmpty(item.Detail1) || x.Detail1 == item.Detail1) &&
                                        (string.IsNullOrEmpty(item.Detail2) || x.Detail2 == item.Detail2) &&
                                        (string.IsNullOrEmpty(item.Warehouse) || x.Warehouse == item.Warehouse))
                                    .Select(x => (!string.IsNullOrEmpty(x.Detail2) ? x.Detail2 : x.Detail1 ?? x.Account) + " " + x.Order + "-" + x.Id)
                                    .ToListAsync();
                    if (string.IsNullOrEmpty(item.Image1))
                    {
                        item.Image1 = company?.FileLogo;
                    }
                }
                item.GoodsQuotaName = goodsQuotas.FirstOrDefault(X => X.Id == item.GoodsQuotaId)?.Code;

                if (!string.IsNullOrEmpty(param.GoodType))
                {
                    var goodDetails = await _context.GoodDetails.Where(x => !(x.IsDeleted ?? false) && x.GoodID == item.Id).ToListAsync();
                    item.ListDetailName = string.Join("; ", goodDetails.Select(x => !string.IsNullOrEmpty(x.Detail2) ? x.Detail2 : x.Detail1 ?? x.Account).ToArray());
                    item.TotalAmount = goodDetails.Sum(x => x.Amount);
                }
            }

            // Sort
            goods = goods.OrderBy(x => (x.Quantity ?? 0) - x.MinStockLevel).ToList();

            return new GoodsPagingResult()
            {
                pageIndex = param.Page,
                PageSize = param.PageSize,
                TotalItems = goodDatas.Count,
                Goods = goods
            };
        }
        catch
        {
            return new GoodsPagingResult()
            {
                pageIndex = param.Page,
                PageSize = param.PageSize,
                TotalItems = 0,
                Goods = new List<Goods>()
            };
        }
    }

    public async Task<GoodslDetailModel> GetById(int id, int year)
    {
        var goods = await _context.Goods.FindAsync(id);
        var item = _mapper.Map<GoodslDetailModel>(goods);
        if (!string.IsNullOrEmpty(goods.Account))
        {
            item.AccountObj = await _chartOfAccountV2Service.FindAccount(goods.Account, string.Empty, year);
            if (!string.IsNullOrEmpty(goods.Detail1))
            {
                item.DetailFirstObj = await _chartOfAccountV2Service.FindAccount(goods.Detail1, goods.Account, year);
                if (!string.IsNullOrEmpty(goods.Detail2))
                {
                    item.DetailSecondObj = await _chartOfAccountV2Service.FindAccount(goods.Detail2, goods.Account + ":" + goods.Detail1, year);
                }
            }
        }

        var accountCode = goods.Account;
        string parentRef = "";
        if (!string.IsNullOrEmpty(goods.Detail2))
        {
            accountCode = goods.Detail2;
            parentRef = goods.Account + ":" + goods.Detail1;
        }
        else if (!string.IsNullOrEmpty(goods.Detail1))
        {
            accountCode = goods.Detail1;
            parentRef = goods.Account;
        }
        var chartofAccount = await _context.GetChartOfAccount(year).FirstOrDefaultAsync(x => x.Code == accountCode && x.ParentRef == parentRef
                        && (string.IsNullOrEmpty(goods.Warehouse) || x.WarehouseCode == goods.Warehouse));
        if (chartofAccount != null)
        {
            item.StockUnitPriceNB = chartofAccount.StockUnitPriceNB;
            item.OpeningDebitNB = chartofAccount.OpeningDebitNB;
            item.OpeningStockQuantityNB = chartofAccount.OpeningStockQuantityNB;
            if (string.IsNullOrEmpty(item.WebGoodNameVietNam))
                item.WebGoodNameVietNam = chartofAccount.Name;
        }

        if (item.WebPriceVietNam == null || item.WebPriceVietNam == 0)
            item.WebPriceVietNam = item.SalePrice;

        return item;
    }

    public async Task<bool> CheckExistGoods(Goods requests)
    {
        var exist = await _context.Goods.SingleOrDefaultAsync(
                x => !x.IsDeleted && x.Detail1 == requests.Detail1 && x.Detail2 == requests.Detail2
                && x.DetailName1 == requests.DetailName1 && x.DetailName2 == requests.DetailName2
                && x.Warehouse == requests.Warehouse && x.WarehouseName == requests.WarehouseName
                && x.PriceList == requests.PriceList);
        return exist != null && exist.Id != requests.Id;
    }

    public async Task<string> Create(Goods param, int year)
    {
        ChartOfAccount account;

        await ValidateWhenSetting(param.Account, param.Detail1, param.Detail2, param.Warehouse, param.Id);

        if (!string.IsNullOrEmpty(param.Detail2))
            account = await _context.GetChartOfAccount(year)
                        .FirstOrDefaultAsync(x => x.Code == param.Detail2 && x.ParentRef == param.Account + ":" + param.Detail1);
        else if (!string.IsNullOrEmpty(param.Detail1))
            account = await _context.GetChartOfAccount(year)
                        .FirstOrDefaultAsync(x => x.Code == param.Detail1 && x.ParentRef == param.Account);
        else
            account = await _context.GetChartOfAccount(year)
                        .FirstOrDefaultAsync(x => x.ParentRef == param.Account);
        if (account is null)
        {
            throw new ErrorException("Not found account");
        }

        param.StockUnit = account.StockUnit;
        await _context.Goods.AddAsync(param);
        await _context.SaveChangesAsync();
        return string.Empty;
    }

    public async Task<string> Update(GoodsUpdateModel param, int year)
    {
        try
        {
            await _context.Database.BeginTransactionAsync();
            var goods = _context.Goods.SingleOrDefault(x => x.Id == param.Id && !x.IsDeleted);
            if (goods == null)
            {
                return ErrorMessages.DataNotFound;
            }
            await ValidateWhenSetting(param.Account, param.Detail1, param.Detail2, param.Warehouse, param.Id);

            goods.Account = param.Account;
            goods.AccountName = param.AccountName;
            goods.Delivery = param.Delivery;
            goods.PriceList = param.PriceList;
            goods.GoodsType = param.GoodsType;
            goods.Inventory = param.Inventory;
            goods.MaxStockLevel = param.MaxStockLevel;
            goods.MenuType = param.MenuType;
            goods.MinStockLevel = param.MinStockLevel;
            goods.Position = param.Position;
            goods.Price = param.Price;
            goods.SalePrice = param.SalePrice;
            goods.DiscountPrice = param.DiscountPrice;
            goods.Warehouse = param.Warehouse;
            goods.WarehouseName = param.WarehouseName;
            goods.Detail1 = param.Detail1;
            goods.Detail2 = param.Detail2;
            goods.DetailName1 = param.DetailName1;
            goods.DetailName2 = param.DetailName2;
            goods.Status = param.Status;
            goods.TaxRateId = param.TaxRateId;
            goods.Net = param.Net;
            goods.IsService = param.IsService;

            if (param.Image1 == "" && !File.Exists(goods.Image1) && !string.IsNullOrEmpty(goods.Image1))
            {
                goods.Image1 = "";
            }
            else
            {
                goods.Image1 = param.Image1;
            }

            if (param.Image2 == "" && !File.Exists(goods.Image2) && !string.IsNullOrEmpty(goods.Image2))
            {
                goods.Image2 = "";
            }
            else
            {
                goods.Image2 = param.Image2;
            }

            if (param.Image3 == "" && !File.Exists(goods.Image3) && !string.IsNullOrEmpty(goods.Image3))
            {
                goods.Image3 = "";
            }
            else
            {
                goods.Image3 = param.Image3;
            }

            if (param.Image4 == "" && !File.Exists(goods.Image4) && !string.IsNullOrEmpty(goods.Image4))
            {
                goods.Image4 = "";
            }
            else
            {
                goods.Image4 = param.Image4;
            }

            if (param.Image5 == "" && !File.Exists(goods.Image5) && !string.IsNullOrEmpty(goods.Image5))
            {
                goods.Image5 = "";
            }
            else
            {
                goods.Image5 = param.Image5;
            }

            _context.Goods.Update(goods);

            var accountCode = goods.Account;
            string parentRef = "";
            if (!string.IsNullOrEmpty(goods.Detail2))
            {
                accountCode = goods.Detail2;
                parentRef = goods.Account + ":" + goods.Detail1;
            }
            else if (!string.IsNullOrEmpty(goods.Detail1))
            {
                accountCode = goods.Detail1;
                parentRef = goods.Account;
            }
            var chartofAccount = await _context.GetChartOfAccount(year).FirstOrDefaultAsync(x => x.Code == accountCode && x.ParentRef == parentRef
                            && (string.IsNullOrEmpty(goods.Warehouse) || x.WarehouseCode == goods.Warehouse));
            if (chartofAccount != null)
            {
                chartofAccount.StockUnitPriceNB = param.StockUnitPriceNB;
                chartofAccount.OpeningDebitNB = param.OpeningDebitNB;
                chartofAccount.OpeningStockQuantityNB = param.OpeningStockQuantityNB;
                _context.ChartOfAccounts.Update(chartofAccount);
            }
            var roomTypes = await _context.GoodRoomTypes.FirstOrDefaultAsync(x => x.GoodId == param.Id);
            if (!param.IsService && roomTypes is not null)
            {
                _context.GoodRoomTypes.Remove(roomTypes);
            }
            if (param.IsService && roomTypes is null)
            {
                var roomTypeAdds = new GoodRoomType
                {
                    GoodId = param.Id,
                };
                await _context.GoodRoomTypes.AddAsync(roomTypeAdds);
            }

            await _context.SaveChangesAsync();
            _context.Database.CommitTransaction();
            return string.Empty;
        }
        catch
        {
            _context.Database.RollbackTransaction();
            throw;
        }
    }

    public async Task Delete(int id)
    {
        var goods = await _context.Goods.FindAsync(id);
        if (goods != null)
        {
            // check
            goods.IsDeleted = true;
            _context.Goods.Update(goods);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<string> GetExcelReport(SearchViewModel param, bool isManager)
    {
        var data = await GetAll_Common(param).ToListAsync();

        if (data.Count > 0)
        {
            if (isManager)
                return await ExportExcelManager(data);

            return await ExportExcel(data);
        }
        return string.Empty;
    }

    private async Task<string> ExportExcel(List<GoodsExportlModel> goods)
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), "Uploads/Excel/BangKeHangHoa.xlsx");
        MemoryStream stream = new MemoryStream();
        List<Category> lstGroupType = await _context.Categories.Where(x => !x.IsDeleted).ToListAsync();

        using (FileStream templateDocumentStream = File.OpenRead(path))
        {
            using ExcelPackage package = new ExcelPackage(templateDocumentStream);
            ExcelWorksheet worksheet = package.Workbook.Worksheets["Sheet1"];
            int nRowBegin = 7;
            int iRow = nRowBegin;
            var listMenuType = lstGroupType.Where(x => x.Type == (int)CategoryEnum.GoodGroup).Take(10).ToList();
            var listPriceType = lstGroupType.Where(x => x.Type == (int)CategoryEnum.PriceList).Take(4).ToList();
            listPriceType.Add(lstGroupType.Find(t => t.Code == goods[0].PriceList && t.Type == (int)CategoryEnum.PriceList));
            var listGoodsType = lstGroupType.Where(x => x.Type == (int)CategoryEnum.GoodsType2).Take(10).ToList();
            var listPositionType = lstGroupType.Where(x => x.Type == (int)CategoryEnum.Position).Take(10).ToList();
            var listMenuWeb = lstGroupType.Where(x => x.Type == (int)CategoryEnum.MenuWeb).Take(10).ToList();
            var taxRates = await _context.TaxRates.Where(x => x.Code.Contains("R")).ToListAsync();
            for (int i = 0; i < goods.Count; i++)
            {
                Goods item = goods[i];

                string strMenuTypeName = listMenuType.Find(t => t.Code == item.MenuType)?.Name;
                string strPriceListName = lstGroupType.Find(t => t.Code == item.PriceList && t.Type == (int)CategoryEnum.PriceList)?.Name;
                string strGoodsTypeName = listGoodsType.Find(t => t.Code == item.GoodsType && t.Type == (int)CategoryEnum.GoodsType2)?.Name;
                string strPositionName = listPositionType.Find(t => t.Code == item.Position)?.Name;

                worksheet.Cells[iRow, 1].Value = string.IsNullOrEmpty(item.Detail2) ? item.Detail1 : item.Detail2;
                worksheet.Cells[iRow, 2].Value = string.IsNullOrEmpty(item.DetailName2) ? item.DetailName1 : item.DetailName2;
                worksheet.Cells[iRow, 3].Value = string.IsNullOrEmpty(item.Account) ? null : item.Account;
                worksheet.Cells[iRow, 4].Value = string.IsNullOrEmpty(item.AccountName) ? null : item.AccountName;
                worksheet.Cells[iRow, 5].Value = string.IsNullOrEmpty(item.Detail1) ? null : item.Detail1;
                worksheet.Cells[iRow, 6].Value = string.IsNullOrEmpty(item.DetailName1) ? null : item.DetailName1;

                worksheet.Cells[iRow, 7].Value = string.IsNullOrEmpty(item.Detail2) ? null : item.Detail2;
                worksheet.Cells[iRow, 8].Value = string.IsNullOrEmpty(item.DetailName2) ? null : item.DetailName2;

                worksheet.Cells[iRow, 9].Value = item.Warehouse;
                worksheet.Cells[iRow, 10].Value = item.WarehouseName;

                worksheet.Cells[iRow, 11].Value = item.Price;
                worksheet.Cells[iRow, 12].Value = item.SalePrice;

                var taxRate = taxRates.Find(x => x.Id == item.TaxRateId);
                worksheet.Cells[iRow, 13].Value = $"{taxRate?.Name}-{taxRate?.Percent.ToString()}%";
                worksheet.Cells[iRow, 14].Value = item.DiscountPrice;

                worksheet.Cells[iRow, 15].Value = strMenuTypeName;

                worksheet.Cells[iRow, 16].Value = strPriceListName;

                worksheet.Cells[iRow, 17].Value = strGoodsTypeName;

                worksheet.Cells[iRow, 18].Value = strPositionName;

                worksheet.Cells[iRow, 20].Value = item.Image1;
                worksheet.Cells[iRow, 21].Value = item.Image2;
                worksheet.Cells[iRow, 22].Value = item.Image3;
                worksheet.Cells[iRow, 23].Value = item.Image4;
                worksheet.Cells[iRow, 24].Value = item.Image5;
                iRow++;
            }
            iRow--;
            if (listMenuType.Count > 0)
            {
                var menu = worksheet.Cells[nRowBegin, 15, iRow, 15].DataValidation.AddListDataValidation();
                foreach (var itemFor in listMenuType)
                {
                    menu.Formula.Values.Add(itemFor.Name);
                }
            }
            if (listPriceType.Count > 0)
            {
                var price = worksheet.Cells[nRowBegin, 16, iRow, 16].DataValidation.AddListDataValidation();
                foreach (var itemFor in listPriceType)
                {
                    price.Formula.Values.Add(itemFor.Name);
                }
            }
            if (listGoodsType.Count > 0)
            {
                var goodstype = worksheet.Cells[nRowBegin, 17, iRow, 17].DataValidation.AddListDataValidation();
                foreach (var itemFor in listGoodsType)
                {
                    goodstype.Formula.Values.Add(itemFor.Name);
                }
            }
            if (listPositionType.Count > 0)
            {
                var position = worksheet.Cells[nRowBegin, 18, iRow, 18].DataValidation.AddListDataValidation();
                foreach (var itemFor in listPositionType)
                {
                    position.Formula.Values.Add(itemFor.Name);
                }
            }
            if (listMenuWeb.Count > 0)
            {
                var menuWeb = worksheet.Cells[nRowBegin, 19, iRow, 19].DataValidation.AddListDataValidation();
                foreach (var itemFor in listMenuWeb)
                {
                    menuWeb.Formula.Values.Add(itemFor.Name);
                }
            }
            if (taxRates.Count > 0)
            {
                var taxRate = worksheet.Cells[nRowBegin, 13, iRow, 13].DataValidation.AddListDataValidation();
                foreach (var itemFor in taxRates)
                {
                    taxRate.Formula.Values.Add($"{itemFor.Name}-{itemFor.Percent}%");
                }
            }
            if (iRow >= nRowBegin)
            {
                worksheet.Cells[nRowBegin, 11, iRow, 14].Style.Numberformat.Format = "_(* #,##0_);_(* (#,##0);_(* \"-\"??_);_(@_)";

                worksheet.Cells[nRowBegin, 1, iRow, 24].Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                worksheet.Cells[nRowBegin, 1, iRow, 24].Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                worksheet.Cells[nRowBegin, 1, iRow, 24].Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                worksheet.Cells[nRowBegin, 1, iRow, 24].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;

                worksheet.Cells[nRowBegin, 1, iRow, 24].Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                worksheet.Cells[nRowBegin, 1, iRow, 24].Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                worksheet.Cells[nRowBegin, 1, iRow, 24].Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                worksheet.Cells[nRowBegin, 1, iRow, 24].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
            }
            package.SaveAs(stream);
            return ExcelHelpers.SaveFileExcel(package, Directory.GetCurrentDirectory(), "BangKeHangHoa");
        }
    }

    private async Task<string> ExportExcelManager(List<GoodsExportlModel> goods)
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), "Uploads/Excel/DanhSachHangHoa.xlsx");
        MemoryStream stream = new MemoryStream();
        using (FileStream templateDocumentStream = File.OpenRead(path))
        {
            using ExcelPackage package = new ExcelPackage(templateDocumentStream);
            ExcelWorksheet worksheet = package.Workbook.Worksheets["Sheet1"];
            int nRowBegin = 7;
            int iRow = nRowBegin;
            var listGoodsType = await _context.Categories.Where(x => !x.IsDeleted && x.Type == (int)CategoryEnum.GoodsType2).ToListAsync();
            var taxRates = await _context.TaxRates.Where(x => x.Code.Contains("R")).ToListAsync();

            for (int i = 0; i < goods.Count; i++)
            {
                var item = goods[i];

                worksheet.Cells[iRow, 1].Value = item.Image1;
                worksheet.Cells[iRow, 2].Value = string.IsNullOrEmpty(item.Account) ? null : item.Account;
                worksheet.Cells[iRow, 3].Value = string.IsNullOrEmpty(item.AccountName) ? null : item.AccountName;
                worksheet.Cells[iRow, 4].Value = string.IsNullOrEmpty(item.Detail1) ? null : item.Detail1;
                worksheet.Cells[iRow, 5].Value = string.IsNullOrEmpty(item.DetailName1) ? null : item.DetailName1;
                worksheet.Cells[iRow, 6].Value = string.IsNullOrEmpty(item.Detail2) ? null : item.Detail2;
                worksheet.Cells[iRow, 7].Value = string.IsNullOrEmpty(item.DetailName2) ? null : item.DetailName2;
                worksheet.Cells[iRow, 8].Value = string.IsNullOrEmpty(item.Warehouse) ? null : item.Warehouse;
                worksheet.Cells[iRow, 9].Value = string.IsNullOrEmpty(item.WarehouseName) ? null : item.WarehouseName;
                worksheet.Cells[iRow, 10].Value = item.GoodsType;
                worksheet.Cells[iRow, 11].Value = item.MinStockLevel;
                worksheet.Cells[iRow, 12].Value = item.MaxStockLevel;
                worksheet.Cells[iRow, 13].Value = item.Net;

                var taxRate = taxRates.Find(x => x.Id == item.TaxRateId);
                worksheet.Cells[iRow, 14].Value = $"{taxRate?.Name}-{taxRate?.Percent.ToString()}%";

                worksheet.Cells[iRow, 15].Value = item.Status == 1 ? "Đang kinh doanh" : "Ngừng kinh doanh";

                iRow++;
            }
            iRow--;
            if (listGoodsType.Count > 0)
            {
                var goodstype = worksheet.Cells[nRowBegin, 8, iRow, 8].DataValidation.AddListDataValidation();
                foreach (var itemFor in listGoodsType)
                {
                    goodstype.Formula.Values.Add(itemFor.Name);
                }
            }
            if (taxRates.Count > 0)
            {
                var taxRate = worksheet.Cells[nRowBegin, 14, iRow, 14].DataValidation.AddListDataValidation();
                foreach (var itemFor in taxRates)
                {
                    taxRate.Formula.Values.Add($"{itemFor.Name}-{itemFor.Percent.ToString()}%");
                }
            }
            var status = worksheet.Cells[nRowBegin, 9, iRow, 9].DataValidation.AddListDataValidation();
            status.Formula.Values.Add("Đang kinh doanh");
            status.Formula.Values.Add("Ngừng kinh doanh");

            int nCol = 15;
            if (iRow >= nRowBegin)
            {
                worksheet.Cells[nRowBegin, 11, iRow, 14].Style.Numberformat.Format = "_(* #,##0_);_(* (#,##0);_(* \"-\"??_);_(@_)";

                worksheet.Cells[nRowBegin, 1, iRow, nCol].Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                worksheet.Cells[nRowBegin, 1, iRow, nCol].Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                worksheet.Cells[nRowBegin, 1, iRow, nCol].Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                worksheet.Cells[nRowBegin, 1, iRow, nCol].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;

                worksheet.Cells[nRowBegin, 1, iRow, nCol].Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                worksheet.Cells[nRowBegin, 1, iRow, nCol].Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                worksheet.Cells[nRowBegin, 1, iRow, nCol].Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                worksheet.Cells[nRowBegin, 1, iRow, nCol].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
            }
            package.SaveAs(stream);
            return ExcelHelpers.SaveFileExcel(package, Directory.GetCurrentDirectory(), "DanhSachHangHoa");
        }
    }

    public async Task<string> ImportFromExcel(List<GoodsExportlModel> lstGoods, bool isManager)
    {
        if (!isManager)
        {
            string message = await ImportFromExcelForPriceList(lstGoods);
            return message;
        }

        if (lstGoods.Count == 0)
            return ErrorMessages.DataNotFound;

        using (var trans = _context.Database.BeginTransaction())
        {
            try
            {
                List<Goods> goodChecks = await _context.Goods.Where(x => !x.IsDeleted && x.PriceList == "BGC").ToListAsync();
                var company = await _context.Companies.OrderByDescending(x => x.SignDate).FirstOrDefaultAsync();
                var taxRates = await _context.TaxRates.Where(x => x.Code.Contains("R")).ToListAsync();
                taxRates = taxRates.Where(x => x.Code.StartsWith("R")).ToList();

                foreach (var goods in lstGoods)
                {
                    Goods item = goodChecks.Find(x => x.Account == goods.Account && x.Detail1 == goods.Detail1
                                                        && x.Detail2 == goods.Detail2
                                                        && (x.Warehouse == goods.Warehouse || string.IsNullOrEmpty(goods.Warehouse))
                                                        && x.Status == goods.Status);
                    if (item == null)
                        item = new Goods();
                    item.Account = goods.Account;
                    item.AccountName = goods.AccountName;
                    item.Detail1 = goods.Detail1;
                    item.DetailName1 = goods.DetailName1;
                    item.Detail2 = goods.Detail2;
                    item.DetailName2 = goods.DetailName2;
                    item.Warehouse = goods.Warehouse;
                    item.WarehouseName = goods.WarehouseName;
                    item.GoodsType = goods.GoodsType;
                    item.MinStockLevel = goods.MinStockLevel;
                    item.MaxStockLevel = goods.MaxStockLevel;
                    item.Net = goods.Net;

                    var taxRateName = goods.TaxRateName.Split("-");
                    if (taxRateName.Length > 0)
                    {
                        var taxRate = taxRates.Find(x => x.Name == taxRateName[0]);
                        item.TaxRateId = taxRate?.Id;
                    }

                    item.Status = goods.Status;
                    item.PriceList = "BGC";
                    if (item.Id > 0)
                        _context.Goods.Update(item);
                    else
                    {
                        if (string.IsNullOrEmpty(item.Image1))
                            item.Image1 = company.FileLogo;
                        _context.Goods.Add(item);
                    }
                }

                await _context.SaveChangesAsync();
                _context.Database.CommitTransaction();
            }
            catch (Exception ex)
            {
                _context.Database.RollbackTransaction();
                return ex.Message;
            }
        }

        return string.Empty;
    }

    private async Task<string> ImportFromExcelForPriceList(List<GoodsExportlModel> lstGoods)
    {
        if (lstGoods.Count == 0)
            return ErrorMessages.DataNotFound;

        using (var trans = _context.Database.BeginTransaction())
        {
            try
            {
                List<Category> lstGroupType = await _context.Categories.Where(x => !x.IsDeleted).ToListAsync();
                var taxRates = await _context.TaxRates.Where(x => x.Code.Contains("R")).ToListAsync();
                taxRates = taxRates.Where(x => x.Code.StartsWith("R")).ToList();

                var listMenuType = lstGroupType.Where(x => x.Type == (int)CategoryEnum.GoodGroup).ToList();
                var listGoodsType = lstGroupType.Where(x => x.Type == (int)CategoryEnum.GoodsType2).ToList();
                var listPositionType = lstGroupType.Where(x => x.Type == (int)CategoryEnum.Position).ToList();

                var company = await _context.Companies.OrderByDescending(x => x.SignDate).FirstOrDefaultAsync();
                List<Goods> goodUpdates = new List<Goods>();
                List<Goods> goodAdds = new List<Goods>();
                foreach (var goods in lstGoods)
                {
                    Goods item = await _context.Goods.FirstOrDefaultAsync(x => x.Account == goods.Account
                                                        && x.Detail1 == goods.Detail1
                                                        && (x.Detail2 == goods.Detail2 || string.IsNullOrEmpty(goods.Detail2))
                                                        && (x.Warehouse == goods.Warehouse || string.IsNullOrEmpty(goods.Warehouse))
                                                        && x.Status == goods.Status
                                                        && x.PriceList == goods.PriceList
                                                        && !x.IsDeleted);
                    if (item == null)
                        item = new Goods();
                    item.Account = goods.Account;
                    item.AccountName = goods.AccountName;
                    item.Detail1 = goods.Detail1;
                    item.DetailName1 = goods.DetailName1;
                    item.Detail2 = goods.Detail2;
                    item.DetailName2 = goods.DetailName2;
                    item.Warehouse = goods.Warehouse;
                    item.WarehouseName = goods.WarehouseName;
                    item.DiscountPrice = goods.DiscountPrice;
                    item.Image1 = goods.Image1;
                    item.Image2 = goods.Image2;
                    item.Image3 = goods.Image3;
                    item.Image4 = goods.Image4;
                    item.Image5 = goods.Image5;
                    item.Price = goods.Price;
                    item.PriceList = goods.PriceList;
                    item.SalePrice = goods.SalePrice;
                    if (goods.TaxRateName != null)
                    {
                        var taxRateName = goods.TaxRateName.Split("-");
                        if (taxRateName.Length > 0)
                        {
                            var taxRate = taxRates.Find(x => x.Name == taxRateName[0]);
                            item.TaxRateId = taxRate?.Id;
                        }
                    }
                    item.MenuType = listMenuType.Find(t => t.Name == goods.MenuType)?.Code;
                    item.GoodsType = listGoodsType.Find(t => t.Name == item.GoodsType)?.Code;
                    item.Position = listPositionType.Find(t => t.Name == item.Position)?.Code;

                    if (item.Id > 0)
                    {
                        if (!goodUpdates.Exists(x => x.Id == item.Id))
                            goodUpdates.Add(item);
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(item.Image1))
                            item.Image1 = company.FileLogo;
                        goodAdds.Add(item);
                    }
                }
                _context.Goods.UpdateRange(goodUpdates);
                _context.Goods.AddRange(goodAdds);

                await _context.SaveChangesAsync();
                _context.Database.CommitTransaction();
            }
            catch (Exception ex)
            {
                _context.Database.RollbackTransaction();
                return ex.Message;
            }
        }

        return string.Empty;
    }

    public async Task SyncAccountGood(int year)
    {
        try
        {
            var isNewGood = await CheckGoodNew(year);
            if (!isNewGood)
                return;

            await _context.Database.BeginTransactionAsync();
            List<Goods> listGood_new = new List<Goods>();
            var listGood = _context.Goods.Where(x => !x.IsDeleted && x.PriceList == "BGC").ToList();
            var listAccount = await _context.GetChartOfAccount(year)
                .Where(x => x.Classification == 2 || x.Classification == 3).ToListAsync();

            var company = _context.Companies.OrderByDescending(x => x.SignDate).FirstOrDefault();

            foreach (var account in listAccount)
            {
                if (string.IsNullOrEmpty(account.ParentRef) || account.Type < 5)//account.HasDetails || 
                    continue;
                string detail1 = account.Code;
                string detail2 = "";
                string code = account.ParentRef;
                if (account.ParentRef.Contains(":"))
                {
                    detail2 = account.Code;
                    string[] codes = account.ParentRef.Split(':');
                    code = codes[0];
                    detail1 = codes[1];
                }
                var goods = listGood.Find(x => x.Account == code 
                            && x.Detail1 == detail1
                            && (string.IsNullOrEmpty(x.Detail2) || x.Detail2 == detail2)
                            && (string.IsNullOrEmpty(x.Warehouse) || x.Warehouse == account.WarehouseCode));

                if (goods == null)
                {
                    goods = new Goods();
                    goods.PriceList = "BGC";
                    goods.Account = code;
                    string codeName = listAccount.Find(x => x.Code == code)?.Name;
                    if (string.IsNullOrEmpty(codeName))
                        continue;
                    goods.AccountName = codeName;
                    goods.Detail1 = detail1;
                    string detail1Name = listAccount.Find(x => x.Code == detail1 && x.ParentRef == code)?.Name;
                    goods.DetailName1 = detail1Name;

                    goods.Detail2 = detail2;
                    string detail2Name = "";
                    if (!string.IsNullOrEmpty(detail2))
                    {
                        detail2Name = listAccount.Find(x => x.Code == detail2 && x.ParentRef == code + ":" + detail1)?.Name;
                    }
                    goods.DetailName2 = detail2Name;

                    goods.Warehouse = account.WarehouseCode ?? "";
                    goods.WarehouseName = account.WarehouseName;
                    goods.Status = 1;
                    goods.Price = 0;
                    goods.Image1 = company?.FileLogo;
                    goods.StockUnit = account.StockUnit;
                    goods.OpeningStockQuantityNB = account.OpeningStockQuantityNB;
                    listGood_new.Add(goods);
                }
            }
            await _context.Goods.AddRangeAsync(listGood_new);

            await _context.SaveChangesAsync();
            var roomTypes = listGood_new.Where(x => x.IsService).Select(x => new GoodRoomType
            {
                GoodId = x.Id,
            });

            await _context.GoodRoomTypes.AddRangeAsync(roomTypes);
            await _context.SaveChangesAsync();
            _context.Database.CommitTransaction();
        }
        catch (Exception ex)
        {
            _context.Database.RollbackTransaction();
            throw new ErrorException(ex.Message.ToString());
        }
    }

    private IQueryable<GoodsExportlModel> GetAll_Common(SearchViewModel param)
    {
        var results = from p in _context.Goods
                      join t in _context.TaxRates on p.TaxRateId equals t.Id into _t
                      from t in _t.DefaultIfEmpty()

                      where !p.IsDeleted
                      && (string.IsNullOrEmpty(param.GoodType) || p.GoodsType == param.GoodType)
                      && (string.IsNullOrEmpty(param.Account) || p.Account == param.Account)
                      && (string.IsNullOrEmpty(param.Detail1) || p.Detail1 == param.Detail1)
                      && (string.IsNullOrEmpty(param.PriceCode) || p.PriceList == param.PriceCode)
                      && (string.IsNullOrEmpty(param.MenuType) || p.MenuType == param.MenuType)
                      && (string.IsNullOrEmpty(param.Position) || p.Position == param.Position)

                      //&& (string.IsNullOrEmpty(param.SearchText)
                      //         || p.Detail2.Contains(param.SearchText)
                      //         || p.DetailName2.Contains(param.SearchText)
                      //         || p.Detail1.Contains(param.SearchText)
                      //         || p.DetailName1.Contains(param.SearchText))

                      && p.Status == param.Status
                      select new GoodsExportlModel()
                      {
                          Id = p.Id,
                          MenuType = p.MenuType,
                          Account = p.Account,
                          AccountName = p.AccountName,
                          Delivery = p.Delivery,
                          Warehouse = p.Warehouse,
                          WarehouseName = p.WarehouseName,
                          Detail1 = p.Detail1,
                          Detail2 = p.Detail2,
                          DetailName1 = p.DetailName1,
                          DetailName2 = p.DetailName2,
                          GoodsType = p.GoodsType,
                          Image1 = p.Image1,
                          Image2 = p.Image2,
                          Image3 = p.Image3,
                          Image4 = p.Image4,
                          Image5 = p.Image5,
                          Inventory = p.Inventory,
                          IsDeleted = p.IsDeleted,
                          MaxStockLevel = p.MaxStockLevel,
                          MinStockLevel = p.MinStockLevel,
                          Position = p.Position,
                          Price = p.Price,
                          SalePrice = p.SalePrice,
                          DiscountPrice = p.DiscountPrice,
                          Status = p.Status,
                          PriceList = p.PriceList,

                          ContentEnglish = !param.isCashier && !param.isManage ? p.ContentEnglish : "",
                          ContentKorea = !param.isCashier && !param.isManage ? p.ContentKorea : "",
                          ContentVietNam = !param.isCashier && !param.isManage ? p.ContentVietNam : "",
                          TitleEnglish = !param.isCashier && !param.isManage ? p.TitleEnglish : "",
                          TitleKorea = !param.isCashier && !param.isManage ? p.TitleKorea : "",
                          TitleVietNam = !param.isCashier && !param.isManage ? p.TitleVietNam : "",

                          //WebDiscountEnglish = p.WebDiscountEnglish,
                          //WebDiscountKorea = p.WebDiscountKorea,
                          //WebDiscountVietNam = p.WebDiscountVietNam,
                          //WebGoodNameEnglish = p.WebGoodNameEnglish,
                          //WebGoodNameKorea = p.WebGoodNameKorea,
                          //WebGoodNameVietNam = p.WebGoodNameVietNam,
                          //WebPriceEnglish = p.WebPriceEnglish,
                          //WebPriceKorea = p.WebPriceKorea,
                          //WebPriceVietNam = p.WebPriceVietNam,
                          TaxVAT = p.SalePrice * t.Percent / 100,
                          isPromotion = p.isPromotion,
                          TaxRateId = p.TaxRateId,
                          Net = p.Net,
                          OpeningStockQuantityNB = p.OpeningStockQuantityNB,
                          GoodsQuotaId = p.GoodsQuotaId
                      };

        // Filter category type
        if (!string.IsNullOrEmpty(param.CategoryTypesSearch))
        {
            var categoryArr = new string[4];
            categoryArr = param.CategoryTypesSearch.Split(',');
            if (categoryArr.Length > 0)
            {
                for (int i = 0; i < categoryArr.Length; i++)
                {
                    switch (categoryArr[i].Substring(0, 1))
                    {
                        case "1":
                            results = results.Where(x => x.MenuType == categoryArr[i].Substring(1, categoryArr[i].Length - 1)); break;
                        case "2":
                            results = results.Where(x => x.GoodsType == categoryArr[i].Substring(1, categoryArr[i].Length - 1)); break;
                        case "3":
                            results = results.Where(x => x.Position == categoryArr[i].Substring(1, categoryArr[i].Length - 1)); break;
                        case "4":
                            results = results.Where(x => x.PriceList == categoryArr[i].Substring(1, categoryArr[i].Length - 1)); break;
                    }
                }
            }
        }
        return results;
    }

    public async Task<bool> CheckGoodNew(int year)
    {
        var listGood = await _context.Goods.Where(x => !x.IsDeleted && x.PriceList == "BGC" && !x.IsDeleted).ToListAsync();
        if (listGood.Count == 0)
            return true;

        var accountCodes = listGood.Select(x => x.Account).Distinct().ToList();

        var listAccount = await _context.GetChartOfAccount(year)
            .Where(x => (x.Classification == 2 || x.Classification == 3)
            && !string.IsNullOrEmpty(x.ParentRef) && x.Type > 4).ToListAsync();// && !x.HasDetails
        foreach (var account in listAccount)
        {
            string detail1 = account.Code;
            string detail2 = "";
            string code = account.ParentRef;

            if (account.ParentRef.Contains(":"))
            {
                detail2 = account.Code;
                string[] codes = account.ParentRef.Split(':');
                code = codes[0];
                detail1 = codes[1];
            }
            var goods = listGood.Find(x => x.Account == code && x.Detail1 == detail1
            && (string.IsNullOrEmpty(x.Detail2) || x.Detail2 == detail2)
            && (string.IsNullOrEmpty(x.Warehouse) || x.Warehouse == account.WarehouseCode)
            );
            if (goods == null)
            {
                return true;
            }
        }
        return false;
    }

    public async Task<IEnumerable<SelectListModel>> GetAllGoodShowWeb()
    {
        var priceList = await _context.Categories.FirstOrDefaultAsync(x => x.IsShowWeb
                            && x.Type == (int)CategoryEnum.PriceList);
        string priceListCode = "BGC";
        if (priceList != null)
        {
            priceListCode = priceList.Code;
        }
        return await _context.Goods.Where(x => !x.IsDeleted && x.PriceList == priceListCode)
            .Select(x => new SelectListModel()
            {
                Id = x.Id,
                Code = !string.IsNullOrEmpty(x.Detail2) ? x.Detail2 : x.Detail1,
                Name = !string.IsNullOrEmpty(x.DetailName2) ? x.DetailName2 : x.DetailName1
            }).ToListAsync();
    }

    public async Task<PagingResult<GoodsReportPositionModel>> ReportForGoodsInWarehouse(SearchViewModel param, int year)
    {
        try
        {
            if (param.PageSize <= 0)
                param.PageSize = 20;

            if (param.Page < 0)
                param.Page = 1;
            var results = ReportForGoodsInWarehouseQuery(param);

            if (!string.IsNullOrEmpty(param.Warehouse))
            {
                results = results.Where(x => x.Warehouse == param.Warehouse);
            }
            if (!string.IsNullOrEmpty(param.GoodCode))
            {
                results = results.Where(x => x.Detail1 == param.GoodCode || x.Detail2 == param.GoodCode);
            }
            if (param.MinStockType > 0)
            {
                results = results.Where(x => (x.Quantity ?? 0 - x.MinStockLevel) < 0);
            }

            var goods = await results.OrderBy(x => x.Detail1).Skip((param.Page - 1) * param.PageSize).Take(param.PageSize)
                .Select(X => new GoodsReportPositionModel()
                {
                    Account = X.Account,
                    AccountName = X.AccountName,
                    Detail1 = X.Detail1,
                    DetailName1 = X.DetailName1,
                    Detail2 = X.Detail2,
                    DetailName2 = X.DetailName2,
                    Quantity = X.Quantity
                })
                .ToListAsync();

            var warehouses = await _context.Warehouses.Where(x => !x.IsDelete).ToListAsync();
            var shevels = await _context.WareHouseShelves.ToListAsync();
            var floors = await _context.WareHouseFloors.ToListAsync();
            var positions = await _context.WareHousePositions.ToListAsync();

            var listStorege = await _context.GetChartOfAccount(year).Where(x => (x.Classification == 2 || x.Classification == 3) && !x.HasChild).ToListAsync();

            foreach (var good in goods)
            {
                var goodWarehouses = await _context.GoodWarehouses.Where(x => x.Account == good.Account
                            && (string.IsNullOrEmpty(good.Detail1) || x.Detail1 == good.Detail1)
                            && (string.IsNullOrEmpty(good.Detail2) || x.Detail2 == good.Detail2)
                            ).ToListAsync();
                if (!goodWarehouses.Any())
                    continue;
                var goodWarehouseIds = goodWarehouses.Select(x => x.Id).ToList();
                var goodWarehouseDetails = await _context.GoodWarehousesPositions.Where(x => goodWarehouseIds.Contains(x.GoodWarehousesId)).ToListAsync();
                good.Positions = new List<string>();
                foreach (var goodWarehouseDetail in goodWarehouseDetails)
                {
                    var warehouse = warehouses.Find(X => X.Code == goodWarehouseDetail.Warehouse);
                    var shevel = shevels.Find(X => X.Id == goodWarehouseDetail.WareHouseShelvesId);
                    var floor = floors.Find(X => X.Id == goodWarehouseDetail.WareHouseFloorId);
                    var position = positions.Find(X => X.Id == goodWarehouseDetail.WareHousePositionId);
                    good.Positions.Add("Số lượng " + goodWarehouseDetail.Quantity.ToString() + " " + warehouse?.Name + ", " + shevel?.Name + ", " + floor?.Name + ", " + position?.Name);
                }

                ChartOfAccount storege;
                if (!string.IsNullOrEmpty(good.Detail2))
                {
                    string parentRef = good.Account + ":" + good.Detail1;
                    storege = listStorege.Find(x => x.Code == good.Detail2 && x.ParentRef == parentRef &&
                            (string.IsNullOrEmpty(good.Warehouse) || x.WarehouseCode == good.Warehouse));
                }
                else if (!string.IsNullOrEmpty(good.Detail1))
                    storege = listStorege.Find(x => x.Code == good.Detail1 && x.ParentRef == good.Account &&
                    (string.IsNullOrEmpty(good.Warehouse) || x.WarehouseCode == good.Warehouse));
                else
                    storege = listStorege.Find(x => x.Code == good.Account);

                if (storege != null)
                {
                    good.Quantity = (storege.OpeningStockQuantity ?? 0) + (storege.ArisingStockQuantity ?? 0);
                    good.StockUnit = storege.StockUnit;
                }
                if (good.Quantity == null || good.Quantity < 0)
                    good.Quantity = 0;
            }
            return new PagingResult<GoodsReportPositionModel>()
            {
                CurrentPage = param.Page,
                PageSize = param.PageSize,
                TotalItems = results.Count(),
                Data = goods
            };
        }
        catch
        {
            return new PagingResult<GoodsReportPositionModel>()
            {
                CurrentPage = param.Page,
                PageSize = param.PageSize,
                TotalItems = 0,
                Data = new List<GoodsReportPositionModel>()
            };
        }
    }

    private IQueryable<GoodsExportlModel> ReportForGoodsInWarehouseQuery(SearchViewModel param)
    {
        var results = from p in _context.Goods
                      where !p.IsDeleted
                      && (string.IsNullOrEmpty(param.GoodType) || p.GoodsType == param.GoodType)
                      && (string.IsNullOrEmpty(param.Account) || p.Account == param.Account)
                      && (string.IsNullOrEmpty(param.Detail1) || p.Detail1 == param.Detail1)
                      && (string.IsNullOrEmpty(param.PriceCode) || p.PriceList == param.PriceCode)
                      && (string.IsNullOrEmpty(param.MenuType) || p.MenuType == param.MenuType)
                      && (string.IsNullOrEmpty(param.Position) || p.Position == param.Position)
                      && (string.IsNullOrEmpty(param.SearchText) || (!string.IsNullOrEmpty(p.Detail2) ? p.DetailName2 : p.DetailName1 ?? p.AccountName).Contains(param.SearchText)
                      || p.SalePrice.ToString().Contains(param.SearchText) || p.Detail2.Contains(param.SearchText))

                      && p.Status == param.Status
                      select new GoodsExportlModel()
                      {
                          Id = p.Id,
                          MenuType = p.MenuType,
                          Account = p.Account,
                          AccountName = p.AccountName,
                          Delivery = p.Delivery,
                          Warehouse = p.Warehouse,
                          WarehouseName = p.WarehouseName,
                          Detail1 = p.Detail1,
                          Detail2 = p.Detail2,
                          DetailName1 = p.DetailName1,
                          DetailName2 = p.DetailName2,
                          GoodsType = p.GoodsType,
                          Inventory = p.Inventory,
                          IsDeleted = p.IsDeleted,
                          MaxStockLevel = p.MaxStockLevel,
                          MinStockLevel = p.MinStockLevel,
                          Position = p.Position,
                          Price = p.Price,
                          SalePrice = p.SalePrice,
                          DiscountPrice = p.DiscountPrice,
                          PriceList = p.PriceList,
                          isPromotion = p.isPromotion,
                      };

        return results;
    }

    public async Task<string> UpdateGoodsWebsite(Goods requests)
    {
        try
        {
            var goods = _context.Goods.SingleOrDefault(x => x.Id == requests.Id && !x.IsDeleted);
            if (goods == null)
            {
                return ErrorMessages.DataNotFound;
            }
            await ValidateWhenSetting(requests.Account, requests.Detail1, requests.Detail2, requests.Warehouse, requests.Id);

            goods.TitleVietNam = requests.TitleVietNam;
            goods.WebDiscountVietNam = requests.WebDiscountVietNam;
            goods.WebGoodNameVietNam = requests.WebGoodNameVietNam;
            goods.WebPriceVietNam = requests.WebPriceVietNam;
            goods.ContentVietNam = requests.ContentVietNam;

            goods.TitleKorea = requests.TitleKorea;
            goods.WebDiscountKorea = requests.WebDiscountKorea;
            goods.WebGoodNameKorea = requests.WebGoodNameKorea;
            goods.WebPriceKorea = requests.WebPriceKorea;
            goods.ContentKorea = requests.ContentKorea;

            goods.TitleEnglish = requests.TitleEnglish;
            goods.WebDiscountEnglish = requests.WebDiscountEnglish;
            goods.WebGoodNameEnglish = requests.WebGoodNameEnglish;
            goods.WebPriceEnglish = requests.WebPriceEnglish;
            goods.ContentEnglish = requests.ContentEnglish;
            goods.MenuType = requests.MenuType;
            if (requests.Image1 == "" && !File.Exists(goods.Image1) && !string.IsNullOrEmpty(goods.Image1))
            {
                goods.Image1 = "";
            }
            else
            {
                goods.Image1 = requests.Image1;
            }

            if (requests.Image2 == "" && !File.Exists(goods.Image2) && !string.IsNullOrEmpty(goods.Image2))
            {
                goods.Image2 = "";
            }
            else
            {
                goods.Image2 = requests.Image2;
            }

            if (requests.Image3 == "" && !File.Exists(goods.Image3) && !string.IsNullOrEmpty(goods.Image3))
            {
                goods.Image3 = "";
            }
            else
            {
                goods.Image3 = requests.Image3;
            }

            if (requests.Image4 == "" && !File.Exists(goods.Image4) && !string.IsNullOrEmpty(goods.Image4))
            {
                goods.Image4 = "";
            }
            else
            {
                goods.Image4 = requests.Image4;
            }

            if (requests.Image5 == "" && !File.Exists(goods.Image5) && !string.IsNullOrEmpty(goods.Image5))
            {
                goods.Image5 = "";
            }
            else
            {
                goods.Image5 = requests.Image5;
            }

            _context.Goods.Update(goods);
            await _context.SaveChangesAsync();
            return string.Empty;
        }
        catch
        {
            _context.Database.RollbackTransaction();
            throw;
        }
    }

    public async Task UpdateMenuTypeForGood(UpdateMenuTypeForGoodModel request)
    {
        if (request is null)
        {
            throw new ErrorException(ErrorMessage.DATA_IS_EMPTY);
        }

        if (request.MenuType == null)
        {
            throw new ErrorException(ErrorMessage.USERID_IS_EMPTY);
        }

        if (request.GoodIds == null)
        {
            throw new ErrorException(ErrorMessage.USERID_IS_EMPTY);
        }

        var isExistMenuType = await _context.Categories.AnyAsync(x => x.Code == request.MenuType && (x.Type == (int)CategoryEnum.MenuWeb || x.Type == (int)CategoryEnum.MenuWebOnePage));
        if (!isExistMenuType)
        {
            throw new ErrorException(ErrorMessage.DATA_IS_NOT_EXIST);
        }

        var goods = await _context.Goods.Where(x => request.GoodIds.Contains(x.Id)).ToListAsync();
        goods = goods.ConvertAll(x => { x.MenuType = request.MenuType; return x; });
        _context.Goods.UpdateRange(goods);
        await _context.SaveChangesAsync();
    }

    private async Task ValidateWhenSetting(string account, string detail1, string detail2, string wareHouse, int goodId)
    {
        var shoudIgnorSetting = await _context.Goods.AnyAsync(x => x.Id != goodId
                                                        && x.Account == account
                                                        && !string.IsNullOrEmpty(x.Detail1) && !string.IsNullOrEmpty(detail1) && x.Detail1 == detail1
                                                        && !string.IsNullOrEmpty(x.Detail2) && !string.IsNullOrEmpty(detail2) && x.Detail2 == detail2
                                                        && !string.IsNullOrEmpty(x.Warehouse) && !string.IsNullOrEmpty(wareHouse) && x.Warehouse == wareHouse
                                                        );
        if (shoudIgnorSetting)
        {
            throw new ErrorException(ErrorMessages.GoodsCodeAlreadyExist);
        }
    }

    public async Task UpdateStatusGoods(List<int> goodIds, int status)
    {
        if (status != 0 && status != 1)
        {
            throw new ErrorException("Not exist status");
        }

        var goods = await _context.Goods.Where(x => goodIds.Contains(x.Id)).ToListAsync();
        if (!goods.Any())
        {
            throw new ErrorException("Not exist goods");
        }
        goods = goods.ConvertAll(x =>
        {
            x.Status = status;
            return x;
        });
        _context.Goods.UpdateRange(goods);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateGoodIsService(List<int> goodIds)
    {
        var goods = await _context.Goods.Where(x => goodIds.Contains(x.Id)).ToListAsync();
        if (!goods.Any())
        {
            throw new ErrorException("Not exist goods");
        }
        goods = goods.ConvertAll(x =>
        {
            x.IsService = true;
            return x;
        });
        _context.Goods.UpdateRange(goods);

        var roomTypeGoodIds = await _context.GoodRoomTypes.Where(x => goodIds.Contains(x.GoodId)).Select(x => x.GoodId).ToListAsync();

        var roomTypeAdds = goodIds.Where(x => !roomTypeGoodIds.Contains(x)).Select(x => new GoodRoomType
        {
            GoodId = x,
        });
        await _context.GoodRoomTypes.AddRangeAsync(roomTypeAdds);
        await _context.SaveChangesAsync();
    }
}