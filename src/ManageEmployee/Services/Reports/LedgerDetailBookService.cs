using Common.Constants;
using DinkToPdf.Contracts;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System.Text;
using ManageEmployee.Dal.DbContexts;
using ManageEmployee.Helpers;
using ManageEmployee.Services.Interfaces.Ledgers;
using ManageEmployee.Services.Interfaces.Reports;
using ManageEmployee.Services.Interfaces.Accounts;
using ManageEmployee.Services.Interfaces.Companies;
using ManageEmployee.DataTransferObject.Reports;
using ManageEmployee.DataTransferObject.PagingRequest;
using ManageEmployee.Entities.Enumerations;
using ManageEmployee.Entities.ChartOfAccountEntities;

namespace ManageEmployee.Services.Reports;

public class LedgerDetailBookService : ILedgerDetailBookService
{
    private readonly ApplicationDbContext _context;
    private readonly ILedgerService _ledgerServices;
    private readonly ICompanyService _companyService;
    private readonly IAccountBalanceSheetService _accountBalanceSheet;
    private readonly IConverter _converterPDF;

    public LedgerDetailBookService(ApplicationDbContext context, 
        ILedgerService ledgerServices, ICompanyService companyService, 
        IAccountBalanceSheetService accountBalanceSheet, IConverter converterPDF)
    {
        _context = context;
        _ledgerServices = ledgerServices;
        _companyService = companyService;
        _accountBalanceSheet = accountBalanceSheet;
        _converterPDF = converterPDF;
    }

    public async Task<string> GetDataReport_SoChiTiet_Four(LedgerReportParamDetail _param, int year, bool isNoiBo = false)
    {
        try
        {
            DateTime dtFrom, dtTo;

            if (string.IsNullOrEmpty(_param.AccountCodeDetail1)) _param.AccountCodeDetail1 = string.Empty;
            _param.AccountCodeDetail1 = _param.AccountCodeDetail1.Trim();

            
            var _accountFind = await _context.GetChartOfAccount(year).FirstOrDefaultAsync(x => x.Code == _param.AccountCode);
            ChartOfAccount accountChild = null;
            if (!string.IsNullOrEmpty(_param.AccountCodeDetail1))
            {
                accountChild = await _context.GetChartOfAccount(year).FirstOrDefaultAsync(x => x.Code == _param.AccountCodeDetail1
                                        && x.ParentRef == _param.AccountCode);
            }
            if (!string.IsNullOrEmpty(_param.AccountCodeDetail2))
            {
                var parentRefCode = _param.AccountCode + ":" + _param.AccountCodeDetail1;
                accountChild = await _context.GetChartOfAccount(year).FirstOrDefaultAsync(x => x.Code == _param.AccountCodeDetail2
                                        && x.ParentRef == parentRefCode);
            }

            var _company = await _companyService.GetCompany();
            if (_param.FilterType == 1)
            {
                dtFrom = new DateTime(year, _param.FromMonth.Value, 1);
                dtTo = new DateTime(year, _param.ToMonth.Value, 1);
                dtTo = dtTo.AddMonths(1);
                _param.FromDate = dtFrom;
                _param.ToDate = dtTo;
            }
            else
            {
                dtFrom = new DateTime(_param.FromDate.Value.Year, _param.FromDate.Value.Month, _param.FromDate.Value.Day);
                dtTo = new DateTime(_param.ToDate.Value.Year, _param.ToDate.Value.Month, _param.ToDate.Value.Day).AddDays(1);
            }
            var dtFromBefore = new DateTime(_param.FromDate.Value.Year, 1, 1);

            if (string.IsNullOrEmpty(_param.AccountCode))
            {
                return string.Empty;
            }

            List<SoChiTietViewModel> relations = await _context.GetLedgerNotForYear(isNoiBo ? 3 : 2)
                .Where(x => x.IsInternal != LedgerInternalConst.LedgerTemporary && x.OrginalBookDate >= dtFrom && x.OrginalBookDate < dtTo)
                .Where(x =>
                (string.IsNullOrEmpty(_param.AccountCode) || (x.DebitCode == _param.AccountCode || x.CreditCode == _param.AccountCode))
                && (string.IsNullOrEmpty(_param.AccountCodeDetail1) || ((x.DebitDetailCodeFirst == _param.AccountCodeDetail1 && x.DebitCode == _param.AccountCode) || (x.CreditDetailCodeFirst == _param.AccountCodeDetail1 && x.CreditCode == _param.AccountCode)))
                && (string.IsNullOrEmpty(_param.AccountCodeDetail2) || ((x.DebitDetailCodeSecond == _param.AccountCodeDetail2  && x.DebitDetailCodeFirst == _param.AccountCodeDetail1 && x.DebitCode == _param.AccountCode) || x.CreditDetailCodeSecond == _param.AccountCodeDetail2 && x.CreditDetailCodeFirst == _param.AccountCodeDetail1 && x.CreditCode == _param.AccountCode))

                && (string.IsNullOrEmpty(_param.AccountCodeReciprocal) || (x.DebitCode == _param.AccountCodeReciprocal || x.CreditCode == _param.AccountCodeReciprocal))
                && (string.IsNullOrEmpty(_param.AccountCodeDetail1Reciprocal) || (x.DebitDetailCodeFirst == _param.AccountCodeDetail1Reciprocal || x.CreditDetailCodeFirst == _param.AccountCodeDetail1Reciprocal))
                && (string.IsNullOrEmpty(_param.AccountCodeDetail2Reciprocal) || (x.DebitDetailCodeSecond == _param.AccountCodeDetail2Reciprocal || x.CreditDetailCodeSecond == _param.AccountCodeDetail2Reciprocal))
                )
            .Select(k => new SoChiTietViewModel
            {
                BookDate = k.BookDate,
                OrginalBookDate = k.OrginalBookDate,
                DebitCode = k.DebitCode,
                CreditCode = k.CreditCode,
                Description = k.OrginalDescription,
                TakeNote = string.Empty,
                VoucherNumber = k.VoucherNumber,
                OrginalVoucherNumber = k.OrginalVoucherNumber,
                IsDebit = k.DebitCode.Equals(_param.AccountCode),
                DebitAmount = k.DebitCode.Equals(_param.AccountCode) ? k.Amount : 0,
                CreditAmount = k.CreditCode.Equals(_param.AccountCode) ? k.Amount : 0,
                DetailCode = k.DebitCode.Equals(_param.AccountCode) ? k.CreditDetailCodeFirst : k.DebitDetailCodeFirst,
                ArisingDebit = k.DebitCode.Equals(_param.AccountCode) ? k.Amount : 0,
                ArisingCredit = k.CreditCode.Equals(_param.AccountCode) ? k.Amount : 0,
                Month = k.Month,
                Year = k.OrginalBookDate.Value.Year,
                ExchangeRate = k.ExchangeRate,
                OrginalCurrency = k.OrginalCurrency,

                NameOfPerson = k.OrginalCompanyName,
                UnitPrice = k.UnitPrice,
                Amount = k.Amount,
                Quantity = k.Quantity,

                DebitDetailCodeFirst = k.DebitDetailCodeFirst,
                DebitDetailCodeSecond = k.DebitDetailCodeSecond,

                CreditDetailCodeFirst = k.CreditDetailCodeFirst,
                CreditDetailCodeSecond = k.CreditDetailCodeSecond,
                InvoiceNumber = k.InvoiceNumber
            })
                .OrderBy(x => x.OrginalBookDate)
            .ToListAsync();

            List<SumSoChiTietViewModel> listLedgerBefore = await _context.GetLedgerNotForYear(isNoiBo ? 3 : 2).Where(y => y.IsInternal != LedgerInternalConst.LedgerTemporary &&  y.OrginalBookDate.Value >= dtFromBefore
                           && y.OrginalBookDate.Value < dtFrom && (y.DebitCode == _param.AccountCode || y.CreditCode == _param.AccountCode)
                && (string.IsNullOrEmpty(_param.AccountCodeDetail1) || (y.DebitDetailCodeFirst == _param.AccountCodeDetail1 || y.CreditDetailCodeFirst == _param.AccountCodeDetail1))
                && (string.IsNullOrEmpty(_param.AccountCodeDetail2) || (y.DebitDetailCodeSecond == _param.AccountCodeDetail2 || y.CreditDetailCodeSecond == _param.AccountCodeDetail2))
                )
                .Select(k => new SumSoChiTietViewModel
                {
                    DebitCode = k.DebitCode,
                    CreditCode = k.CreditCode,
                    CreditDetailCodeFirst = k.CreditDetailCodeFirst,
                    DebitDetailCodeSecond = k.DebitDetailCodeSecond,
                    CreditDetailCodeSecond = k.CreditDetailCodeSecond,
                    DebitDetailCodeFirst = k.DebitDetailCodeFirst,
                    Amount = k.Amount,
                    OrginalCurrency = k.OrginalCurrency,
                    Quantity = k.Quantity,
                    CreditWarehouse = k.CreditWarehouse,
                    DebitWarehouse = k.DebitWarehouse,
                })
                .ToListAsync();
            
            IDictionary<string, List<SoChiTietThuChiViewModel>> v_dicTotal = new Dictionary<string, List<SoChiTietThuChiViewModel>>();

            var listAccount = await _context.GetChartOfAccount(year).Where(x => x.ParentRef.Contains(_param.AccountCode) && !x.HasChild && !x.HasDetails).ToListAsync();

            if (!string.IsNullOrEmpty(_param.AccountCodeDetail1))
                listAccount = listAccount.Where(x => x.Code == _param.AccountCodeDetail1)
                    .ToList();

            var listLedgerAll = await _context.GetLedgerNotForYear(_param.IsNoiBo ? 3 : 2).Where(x => x.OrginalBookDate >= dtFromBefore && x.OrginalBookDate < dtTo)
                            .Where(y => y.DebitCode == _param.AccountCode || y.CreditCode == _param.AccountCode).ToListAsync();
            foreach (var account in listAccount)
            {
                string detail1 = "";
                string detail2 = "";
                string wareHouse = account.WarehouseCode;
                var relations_new = new List<SoChiTietViewModel>();
                if (account.Type == 5)
                {
                    relations_new = relations.Where(x => (x.DebitDetailCodeFirst == account.Code || x.CreditDetailCodeFirst == account.Code)
                    && (string.IsNullOrEmpty(wareHouse) || x.DebitWarehouseCode == wareHouse || x.CreditWarehouseCode == wareHouse)).ToList();
                    if (relations_new.Count == 0)
                        continue;
                    detail1 = account.Code;
                }
                else if (account.Type == 6)
                {
                    relations_new = relations.Where(x => x.DebitDetailCodeSecond == account.Code || x.CreditDetailCodeSecond == account.Code
                    && (string.IsNullOrEmpty(wareHouse) || x.DebitWarehouseCode == wareHouse || x.CreditWarehouseCode == wareHouse)).ToList();
                    if (relations_new.Count == 0)
                        continue;
                    detail1 = account.ParentRef.Split(':')[1];
                    detail2 = account.Code;
                }
                if (_param.FromDate.Value.Year != year)
                {
                    var accountOld = await _context.ChartOfAccounts.FirstOrDefaultAsync(x => x.Code == account.Code && x.ParentRef == account.ParentRef
                                    && (string.IsNullOrEmpty(account.WarehouseCode) || x.WarehouseCode == account.WarehouseCode) && x.Year == _param.FromDate.Value.Year);
                    account.OpeningDebit = accountOld?.OpeningDebit;
                    account.OpeningCredit = accountOld?.OpeningCredit;
                    account.OpeningStockQuantity = accountOld?.OpeningStockQuantity;

                    account.OpeningDebitNB = accountOld?.OpeningDebitNB;
                    account.OpeningCreditNB = accountOld?.OpeningCreditNB;
                    account.OpeningStockQuantityNB = accountOld?.OpeningStockQuantityNB;
                }

                double _dauKy_Thu = listLedgerBefore.Where(y => y.DebitCode == _param.AccountCode
                && (string.IsNullOrEmpty(_param.AccountCodeDetail1) || y.DebitDetailCodeFirst == _param.AccountCodeDetail1)
                && (string.IsNullOrEmpty(_param.AccountCodeDetail2) || y.DebitDetailCodeSecond == _param.AccountCodeDetail2)
                && (string.IsNullOrEmpty(wareHouse) || y.CreditWarehouse == wareHouse)
                )
                .Sum(q => q.Amount);
                double _dauKy_Chi = listLedgerBefore.Where(y => y.CreditCode == _param.AccountCode
                    && (string.IsNullOrEmpty(_param.AccountCodeDetail1) || y.CreditDetailCodeFirst == _param.AccountCodeDetail1)
                    && (string.IsNullOrEmpty(_param.AccountCodeDetail2) || y.CreditDetailCodeSecond == _param.AccountCodeDetail2)
                    && (string.IsNullOrEmpty(wareHouse) || y.DebitWarehouse == wareHouse)
                    )
                    .Sum(q => q.Amount);

                double _dauKyNo_SoLuong = listLedgerBefore.Where(y => y.DebitCode == _param.AccountCode
                && (string.IsNullOrEmpty(detail1) || y.DebitDetailCodeFirst == detail1)
                    && (string.IsNullOrEmpty(detail2) || y.DebitDetailCodeSecond == detail2)).Sum(q => q.Quantity);

                double _dauKyCo_SoLuong = listLedgerBefore.Where(y => y.CreditCode == _param.AccountCode
                && (string.IsNullOrEmpty(detail1) || y.CreditDetailCodeFirst == detail1)
                    && (string.IsNullOrEmpty(detail2) || y.CreditDetailCodeSecond == detail2)).Sum(q => q.Quantity);

                relations_new.ForEach(x =>
                {
                    x.Thu_Amount = x.IsDebit ? x.Amount : 0;// ArisingDebit_OrginalCur
                    x.Chi_Amount = !x.IsDebit ? x.Amount : 0;//ArisingCredit_OrginalCur
                    x.Input_Quantity = x.IsDebit ? x.Quantity : 0;
                    x.Output_Quantity = !x.IsDebit ? x.Quantity : 0;
                    x.Temp = long.Parse(x.Month + "" + x.Year);

                    if (!isNoiBo)
                    {
                        x.Residual_Amount = (account.OpeningDebit ?? 0) - (account.OpeningCredit ?? 0) + _dauKy_Thu - _dauKy_Chi + x.Thu_Amount - x.Chi_Amount;
                        x.Residual_Quantity = ((account.OpeningStockQuantity ?? 0) + _dauKyNo_SoLuong - _dauKyCo_SoLuong) + x.Input_Quantity - x.Output_Quantity;
                    }
                    else
                    {
                        x.Residual_Amount = (account.OpeningDebitNB ?? 0) - (account.OpeningCreditNB ?? 0) + _dauKy_Thu - _dauKy_Chi + x.Thu_Amount - x.Chi_Amount;
                        x.Residual_Quantity = (account.OpeningStockQuantityNB ?? 0) + _dauKyNo_SoLuong - _dauKyCo_SoLuong + x.Input_Quantity - x.Output_Quantity;
                    }
                    _dauKy_Thu += x.Thu_Amount;
                    _dauKy_Chi += x.Chi_Amount;
                    _dauKyNo_SoLuong += x.Input_Quantity;
                    _dauKyCo_SoLuong += x.Output_Quantity;
                });

                double _luyKe_PS_Thu = 0, _luyKe_PS_Chi = 0, _luyKe_PS_Ton = 0;
                double _luyKe_PS_No_SoLuong = 0, _luyKe_PS_Co_SoLuong = 0;
                double _quantityDK = 0;
                double _donGiaDK = 0;
                double _tienDK = 0;
                double _quantityLK = 0;
                double _tienLK = 0;

                List<long> _months1 = relations_new.Select(x => x.Temp).Distinct().ToList();
                if (!isNoiBo)
                    _luyKe_PS_Ton = (account.OpeningDebit ?? 0) - (account.OpeningCredit ?? 0) + _dauKy_Thu - _dauKy_Chi;
                else
                    _luyKe_PS_Ton = (account.OpeningDebitNB ?? 0) - (account.OpeningCreditNB ?? 0) + _dauKy_Thu - _dauKy_Chi;

                _quantityDK = account.OpeningStockQuantity ?? 0;
                _donGiaDK = account.StockUnitPrice ?? 0;
                _tienDK = (account.OpeningDebit ?? 0) - (account.OpeningCredit ?? 0);

                _quantityLK = account.OpeningStockQuantity ?? 0;
                _tienLK = (account.OpeningDebit ?? 0) - (account.OpeningCredit ?? 0);

                if (dtFrom.Day > 1 || dtFrom.Month > 1)
                {
                    var dauKyCo = listLedgerAll.Where(y => y.CreditCode == _param.AccountCode && y.CreditDetailCodeFirst == detail1
                                                && (string.IsNullOrEmpty(detail2) || y.CreditDetailCodeSecond == detail2)
                                                && (string.IsNullOrEmpty(account.WarehouseCode) || y.CreditWarehouse == account.WarehouseCode)).ToList();
                    double _dauKyCo_SoLuongLK = dauKyCo.Sum(q => q.Quantity);
                    double _dauKyCo_Amount = dauKyCo.Sum(q => q.Amount);
                    var dauKyNo = listLedgerAll.Where(y => y.DebitCode == _param.AccountCode && y.DebitDetailCodeFirst == detail1
                                                && (string.IsNullOrEmpty(detail2) || y.DebitDetailCodeSecond == detail2)
                                                && (string.IsNullOrEmpty(account.WarehouseCode) || y.DebitWarehouse == account.WarehouseCode)).ToList();
                    double _dauKyNo_SoLuongLK = dauKyNo.Sum(q => q.Quantity);
                    double _dauKyNo_Amount = dauKyNo.Sum(q => q.Amount);
                    _quantityLK = (account.OpeningStockQuantity ?? 0) + _dauKyNo_SoLuongLK - _dauKyCo_SoLuongLK;
                    _tienLK = (account.OpeningDebit ?? 0) - (account.OpeningCredit ?? 0) + _dauKyNo_Amount - _dauKyCo_Amount;
                }

                var listThuChi = new List<SoChiTietThuChiViewModel>();
                _months1.ForEach(x =>
                {
                    int _ms = (int)x / 10000;
                    int _year = (int)x % 10000;

                    SoChiTietThuChiViewModel _congPS = new SoChiTietThuChiViewModel();
                    _congPS.Thu_Amount = relations_new.Where(y => y.Month == _ms && y.Year == _year).Sum(q => q.Thu_Amount);
                    _congPS.Chi_Amount = relations_new.Where(y => y.Month == _ms && y.Year == _year).Sum(q => q.Chi_Amount);
                    _congPS.Residual_Amount = _luyKe_PS_Ton + _congPS.Thu_Amount - _congPS.Chi_Amount;
                    _congPS.Type = 1;
                    _congPS.Month = _ms;//cộng phát sinh

                    _congPS.Input_Quantity = relations_new.Where(y => y.Month == _ms && y.Year == _year).Sum(q => q.Input_Quantity);
                    _congPS.Output_Quantity = relations_new.Where(y => y.Month == _ms && y.Year == _year).Sum(q => q.Output_Quantity);
                    _congPS.Residual_Quantity = relations_new.Where(y => y.Month == _ms && y.Year == _year).Sum(q => q.Residual_Quantity);

                    SoChiTietThuChiViewModel _LuyKeNam = new SoChiTietThuChiViewModel();
                    _LuyKeNam.Thu_Amount = relations_new.Where(y => y.Month == _ms && y.Year == _year).Sum(q => q.Thu_Amount);
                    _LuyKeNam.Chi_Amount = relations_new.Where(y => y.Month == _ms && y.Year == _year).Sum(q => q.Chi_Amount);
                    _LuyKeNam.Residual_Amount = _congPS.Residual_Amount;
                    _LuyKeNam.Type = 2;//cộng lũy kế
                    _LuyKeNam.Month = _ms;//cộng lũy kế

                    _LuyKeNam.Input_Quantity = relations_new.Where(y => y.Month == _ms && y.Year == _year).Sum(q => q.Input_Quantity);
                    _LuyKeNam.Output_Quantity = relations_new.Where(y => y.Month == _ms && y.Year == _year).Sum(q => q.Output_Quantity);
                    _LuyKeNam.Residual_Quantity = relations_new.Where(y => y.Month == _ms && y.Year == _year).Sum(q => q.Residual_Quantity);

                    if (_ms > 1)
                    {
                        double _thuDK = 0, _chiDK = 0;
                        if (!isNoiBo)
                        {
                            _thuDK = account.OpeningDebit ?? 0;
                            _chiDK = account.OpeningCredit ?? 0;
                        }
                        else
                        {
                            _thuDK = account.OpeningDebitNB ?? 0;
                            _chiDK = account.OpeningCreditNB ?? 0;
                        }
                        _luyKe_PS_Thu = listLedgerAll.Where(y => y.Month < _ms && y.Year == dtFrom.Year)
                        .Where(y => y.DebitCode == _param.AccountCode
                        && (string.IsNullOrEmpty(detail1) || y.DebitDetailCodeFirst == detail1)
                        && (string.IsNullOrEmpty(detail2) || y.DebitDetailCodeSecond == detail2)
                        && (string.IsNullOrEmpty(account.WarehouseCode) || y.DebitWarehouse == account.WarehouseCode)
                        ).Sum(q => q.Amount);

                        _luyKe_PS_Chi = listLedgerAll.Where(y => y.Month < _ms && y.Year == dtFrom.Year)
                        .Where(y => y.CreditCode == _param.AccountCode
                        && (string.IsNullOrEmpty(detail1) || y.CreditDetailCodeFirst == detail1)
                        && (string.IsNullOrEmpty(detail2) || y.CreditDetailCodeSecond == detail2)
                        && (string.IsNullOrEmpty(account.WarehouseCode) || y.CreditWarehouse == account.WarehouseCode)
                        )
                        .Sum(q => q.Amount);

                        _LuyKeNam.Thu_Amount += _luyKe_PS_Thu;
                        _LuyKeNam.Chi_Amount += _luyKe_PS_Chi;

                        _thuDK += _LuyKeNam.Thu_Amount;
                        _chiDK += _LuyKeNam.Chi_Amount;
                        _LuyKeNam.Residual_Amount = _thuDK - _chiDK;
                        var listLuyKeNo = listLedgerAll.Where(y => y.Month < _ms && y.Year == dtFrom.Year).Where(y => y.DebitCode == _param.AccountCode
                        && y.DebitDetailCodeFirst == detail1
                        && (string.IsNullOrEmpty(detail2) || y.DebitDetailCodeSecond == detail2)
                        && (string.IsNullOrEmpty(wareHouse) || y.DebitWarehouseName == wareHouse)).ToList();

                        var listLuyKeCo = listLedgerAll.Where(y => y.Month < _ms && y.Year == dtFrom.Year)
                        .Where(y => y.CreditCode == _param.AccountCode
                        && y.CreditDetailCodeFirst == _param.AccountCodeDetail1
                        && (string.IsNullOrEmpty(detail2) || y.CreditDetailCodeSecond == detail2)
                        && (string.IsNullOrEmpty(wareHouse) || y.CreditWarehouse == wareHouse)).ToList();

                        _luyKe_PS_No_SoLuong = listLuyKeNo.Sum(q => q.Quantity);
                        _luyKe_PS_Co_SoLuong = listLuyKeCo.Sum(q => q.Quantity);
                        _LuyKeNam.Input_Quantity += _luyKe_PS_No_SoLuong;
                        _LuyKeNam.Output_Quantity += _luyKe_PS_Co_SoLuong;

                        if (_luyKe_PS_No_SoLuong > 0)
                        {
                            _luyKe_PS_No_SoLuong += _quantityDK;
                        }
                        else if (_luyKe_PS_Co_SoLuong > 0)
                        {
                            _luyKe_PS_Co_SoLuong -= _quantityDK;
                        }

                        _LuyKeNam.Residual_Quantity = relations_new.Where(y => y.Month == _ms && y.Year == _year).Sum(q => q.Residual_Quantity);
                        _LuyKeNam.Residual_Amount = relations_new.Where(y => y.Month == _ms && y.Year == _year).Sum(q => q.Residual_Amount);
                    }
                    _luyKe_PS_Ton = _congPS.Residual_Amount;
                    SoChiTietThuChiViewModel _du = new SoChiTietThuChiViewModel();
                    _du.Month = _ms;//dư
                    _du.Type = 3;//dư
                    _du.ArisingDebit_Foreign = 0;
                    _du.ArisingCredit_Foreign = 0;
                    _du.Input_Quantity = 0;
                    _du.Output_Quantity = 0;

                    if (!isNoiBo)
                    {
                        _du.Thu_Amount = (account.OpeningDebit ?? 0) + _LuyKeNam.Thu_Amount;
                        _du.Chi_Amount = (account.OpeningCredit ?? 0) + _LuyKeNam.Chi_Amount;
                        _du.Residual_Amount = (account.OpeningDebit ?? 0) - (account.OpeningCredit ?? 0) + _LuyKeNam.Thu_Amount - _LuyKeNam.Chi_Amount;
                        _du.Residual_Quantity = (account.OpeningStockQuantity ?? 0) + _LuyKeNam.Input_Quantity - _LuyKeNam.Output_Quantity;
                    }
                    else
                    {
                        _du.Thu_Amount = (account.OpeningDebitNB ?? 0) + _LuyKeNam.Thu_Amount;
                        _du.Chi_Amount = (account.OpeningCreditNB ?? 0) + _LuyKeNam.Chi_Amount;
                        _du.Residual_Amount = (account.OpeningDebitNB ?? 0) - (account.OpeningCreditNB ?? 0) + _LuyKeNam.Thu_Amount - _LuyKeNam.Chi_Amount;
                        _du.Residual_Quantity = (account.OpeningStockQuantityNB ?? 0) + _LuyKeNam.Input_Quantity - _LuyKeNam.Output_Quantity;
                    }

                    listThuChi.Add(_congPS);
                    listThuChi.Add(_LuyKeNam);
                    listThuChi.Add(_du);
                });

                v_dicTotal.Add(detail1 + "-" + detail2 + "-" + account.WarehouseCode + "-" + String.Format("{0:N0}", _quantityLK) + "-" + String.Format("{0:N0}", _tienLK), listThuChi);
            }

            LedgerReportModel _model = new()
            {
                InfoSum = null,
                Items = null,
                BookDetails = relations,
                LedgerCalculator = null,
                Address = _company.Address,
                Company = _company.Name,
                MethodCalcExportPrice = _company.MethodCalcExportPrice,
                TaxId = _company.MST,
                CEOName = _company.NameOfCEO,
                ChiefAccountantName = _company.NameOfChiefAccountant,
                AccountCode = _accountFind?.Code,
                AccountName = _accountFind?.Name + (accountChild != null ? (" - " + accountChild.Name) : string.Empty),
                listAccoutCodeThuChi = v_dicTotal,
                CEONote = _company.NoteOfCEO,
                ChiefAccountantNote = _company.NoteOfChiefAccountant,
            };

            return await ExportDataReport_SoChiTiet(_model, _param, year);
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task<string> GetDataReport_SoChiTiet_Six(LedgerReportParamDetail _param, int year, string wareHouseCode = "")
    {
        try
        {
            var _accountFind = await _context.GetChartOfAccount(year).FirstOrDefaultAsync(x => x.Code == _param.AccountCode);
            ChartOfAccount accountChild = null;
            if (!string.IsNullOrEmpty(_param.AccountCodeDetail1))
            {
                accountChild = await _context.GetChartOfAccount(year).FirstOrDefaultAsync(x => x.Code == _param.AccountCodeDetail1
                                        && x.ParentRef == _param.AccountCode);
            }
            if (!string.IsNullOrEmpty(_param.AccountCodeDetail2))
            {
                var parentRefCode = _param.AccountCode + ":" + _param.AccountCodeDetail1;
                accountChild = await _context.GetChartOfAccount(year).FirstOrDefaultAsync(x => x.Code == _param.AccountCodeDetail2
                                        && x.ParentRef == parentRefCode);
            }

            var _company = await _companyService.GetCompany();

            List<LedgerReportTonSLViewModel> relationReturn = await _ledgerServices.GetDataReport_SoChiTiet_Six_data(_param, year, wareHouseCode);

            LedgerReportModel _model = new LedgerReportModel
            {
                InfoSum = null,
                Items = null,
                BookDetails = null,
                LedgerCalculator = null,
                ItemSLTons = relationReturn,
                Address = _company.Address,
                Company = _company.Name,
                MethodCalcExportPrice = _company.MethodCalcExportPrice,
                TaxId = _company.MST,
                CEOName = _company.NameOfCEO,
                ChiefAccountantName = _company.NameOfChiefAccountant,
                AccountCode = _accountFind?.Code,
                AccountName = _accountFind?.Name + (accountChild != null ? (" - " + accountChild.Name) : string.Empty),
                CEONote = _company.NoteOfCEO,
                ChiefAccountantNote = _company.NoteOfChiefAccountant,
            };

            return await ExportDataReport_SoChiTiet(_model, _param, year);
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task<string> GetDataReport_SoChiTiet_Full(LedgerReportParamDetail _param, int year, bool isNoiBo = false)
    {
        try
        {
            DateTime dtFrom, dtTo;

            if (string.IsNullOrEmpty(_param.AccountCodeDetail1)) _param.AccountCodeDetail1 = string.Empty;
            _param.AccountCodeDetail1 = _param.AccountCodeDetail1.Trim();

            var _accountFind = await _context.GetChartOfAccount(year).FirstOrDefaultAsync(x => x.Code == _param.AccountCode);
            ChartOfAccount accountChild = null;
            if (!string.IsNullOrEmpty(_param.AccountCodeDetail1))
            {
                accountChild = await _context.GetChartOfAccount(year).FirstOrDefaultAsync(x => x.Code == _param.AccountCodeDetail1
                                        && x.ParentRef == _param.AccountCode);
            }
            if (!string.IsNullOrEmpty(_param.AccountCodeDetail2))
            {
                var parentRefCode = _param.AccountCode + ":" + _param.AccountCodeDetail1;
                accountChild = await _context.GetChartOfAccount(year).FirstOrDefaultAsync(x => x.Code == _param.AccountCodeDetail2
                                        && x.ParentRef == parentRefCode);
            }
            var _company = await _companyService.GetCompany();

            if (_param.FilterType == 1)
            {
                dtFrom = new DateTime(year, _param.FromMonth.Value, 1);
                dtTo = new DateTime(year, _param.ToMonth.Value, 1);
                dtTo = dtTo.AddMonths(1);
                _param.FromDate = dtFrom;
                _param.ToDate = dtTo;
            }
            else
            {
                dtFrom = new DateTime(_param.FromDate.Value.Year, _param.FromDate.Value.Month, _param.FromDate.Value.Day);
                dtTo = new DateTime(_param.ToDate.Value.Year, _param.ToDate.Value.Month, _param.ToDate.Value.Day).AddDays(1);
            }
            var dtFromBefore = new DateTime(_param.FromDate.Value.Year, 1, 1);

            if (string.IsNullOrEmpty(_param.AccountCode))
            {
                return string.Empty;
            }

            List<SoChiTietViewModel> relations = await _context.GetLedgerNotForYear(isNoiBo ? 3 : 2)
                .Where(x => x.OrginalBookDate >= dtFrom && x.OrginalBookDate < dtTo)
                .Where(x =>
                (string.IsNullOrEmpty(_param.AccountCode) || (x.DebitCode == _param.AccountCode || x.CreditCode == _param.AccountCode))
                && (string.IsNullOrEmpty(_param.AccountCodeDetail1) || (x.DebitDetailCodeFirst == _param.AccountCodeDetail1 || x.CreditDetailCodeFirst == _param.AccountCodeDetail1))
                && (string.IsNullOrEmpty(_param.AccountCodeDetail2) || (x.DebitDetailCodeSecond == _param.AccountCodeDetail2 || x.CreditDetailCodeSecond == _param.AccountCodeDetail2))

                && (string.IsNullOrEmpty(_param.AccountCodeReciprocal) || (x.DebitCode == _param.AccountCodeReciprocal || x.CreditCode == _param.AccountCodeReciprocal))
                && (string.IsNullOrEmpty(_param.AccountCodeDetail1Reciprocal) || (x.DebitDetailCodeFirst == _param.AccountCodeDetail1Reciprocal || x.CreditDetailCodeFirst == _param.AccountCodeDetail1Reciprocal))
                && (string.IsNullOrEmpty(_param.AccountCodeDetail2Reciprocal) || (x.DebitDetailCodeSecond == _param.AccountCodeDetail2Reciprocal || x.CreditDetailCodeSecond == _param.AccountCodeDetail2Reciprocal))
                )
            .Select(k => new SoChiTietViewModel
            {
                BookDate = k.BookDate,
                OrginalBookDate = k.OrginalBookDate,
                DebitCode = k.DebitCode,
                CreditCode = k.CreditCode,
                Description = k.OrginalDescription,
                TakeNote = string.Empty,
                VoucherNumber = k.VoucherNumber,
                OrginalVoucherNumber = k.OrginalVoucherNumber,
                IsDebit = k.DebitCode.Equals(_param.AccountCode),
                DebitAmount = k.DebitCode.Equals(_param.AccountCode) ? k.Amount : 0,
                CreditAmount = k.CreditCode.Equals(_param.AccountCode) ? k.Amount : 0,
                DetailCode = k.DebitCode.Equals(_param.AccountCode) ? k.CreditDetailCodeFirst : k.DebitDetailCodeFirst,
                ArisingDebit = k.DebitCode.Equals(_param.AccountCode) ? k.Amount : 0,
                ArisingCredit = k.CreditCode.Equals(_param.AccountCode) ? k.Amount : 0,
                Month = k.Month,
                Year = k.Year ?? 0,
                ExchangeRate = k.ExchangeRate,
                OrginalCurrency = k.OrginalCurrency,

                NameOfPerson = k.OrginalCompanyName,
                UnitPrice = k.UnitPrice,
                Amount = k.Amount,
                Quantity = k.Quantity,
                DebitDetailCodeFirst = k.DebitDetailCodeFirst,
                DebitDetailCodeSecond = k.DebitDetailCodeSecond,

                CreditDetailCodeFirst = k.CreditDetailCodeFirst,
                CreditDetailCodeSecond = k.CreditDetailCodeSecond,

                InvoiceNumber = k.InvoiceNumber,
            })
            .OrderBy(x => x.OrginalBookDate)
            .ToListAsync();

            List<SumSoChiTietViewModel> listLedgerBefore = await _context.GetLedgerNotForYear(isNoiBo ? 3 : 2).Where(x => x.OrginalBookDate >= dtFromBefore)
                    .Where(y => y.OrginalBookDate.Value < dtFrom && (y.DebitCode == _param.AccountCode || y.CreditCode == _param.AccountCode)
                && (string.IsNullOrEmpty(_param.AccountCodeDetail1) || (y.DebitDetailCodeFirst == _param.AccountCodeDetail1 || y.CreditDetailCodeFirst == _param.AccountCodeDetail1))
                && (string.IsNullOrEmpty(_param.AccountCodeDetail2) || (y.DebitDetailCodeSecond == _param.AccountCodeDetail2 || y.CreditDetailCodeSecond == _param.AccountCodeDetail2))
                )
                .Select(k => new SumSoChiTietViewModel
                {
                    DebitCode = k.DebitCode,
                    CreditCode = k.CreditCode,
                    CreditDetailCodeFirst = k.CreditDetailCodeFirst,
                    DebitDetailCodeSecond = k.DebitDetailCodeSecond,
                    CreditDetailCodeSecond = k.CreditDetailCodeSecond,
                    DebitDetailCodeFirst = k.DebitDetailCodeFirst,
                    Amount = k.Amount,
                    OrginalCurrency = k.OrginalCurrency,
                    Quantity = k.Quantity,
                })
                .ToListAsync();
            
            LedgerReportCalculatorIO _OpeningBackLog = await CalculatorFollowMonth_ThuChi(_param, year);

            double _dauKy_Thu = listLedgerBefore.Where(y => y.DebitCode == _param.AccountCode
                && (string.IsNullOrEmpty(_param.AccountCodeDetail1) || y.DebitDetailCodeFirst == _param.AccountCodeDetail1)
                && (string.IsNullOrEmpty(_param.AccountCodeDetail2) || y.DebitDetailCodeSecond == _param.AccountCodeDetail2)
                )
                .Sum(q => q.Amount);
            double _dauKy_Chi = listLedgerBefore.Where(y => y.CreditCode == _param.AccountCode
                && (string.IsNullOrEmpty(_param.AccountCodeDetail1) || y.CreditDetailCodeFirst == _param.AccountCodeDetail1)
                && (string.IsNullOrEmpty(_param.AccountCodeDetail2) || y.CreditDetailCodeSecond == _param.AccountCodeDetail2)
                )
                .Sum(q => q.Amount);
            double _luyKe_PS_No_NT = 0, _luyKe_PS_No_VND = 0, _luyKe_PS_Co_NT = 0, _luyKe_PS_Co_VND = 0, _samplOpeningBackLog = 0;

            if (!isNoiBo)
            {
                _OpeningBackLog.OpeningAmountLeft = _OpeningBackLog.OpeningAmountLeft + _dauKy_Thu - _dauKy_Chi;
                _samplOpeningBackLog = _OpeningBackLog.OpeningDebit - _OpeningBackLog.OpeningCredit;
            }
            else
            {
                _OpeningBackLog.OpeningAmountLeftNB = _OpeningBackLog.OpeningAmountLeftNB + _dauKy_Thu - _dauKy_Chi;
                _samplOpeningBackLog = _OpeningBackLog.OpeningDebitNB - _OpeningBackLog.OpeningCreditNB;
            }
            double _dauKyNo_NgoaiTe = listLedgerBefore.Where(y => y.DebitCode == _param.AccountCode
            && (string.IsNullOrEmpty(_param.AccountCodeDetail1) || y.DebitDetailCodeFirst == _param.AccountCodeDetail1)
                && (string.IsNullOrEmpty(_param.AccountCodeDetail2) || y.DebitDetailCodeSecond == _param.AccountCodeDetail2)).Sum(q => q.OrginalCurrency);
            double _dauKyNo_VND = listLedgerBefore.Where(y => y.DebitCode == _param.AccountCode
            && (string.IsNullOrEmpty(_param.AccountCodeDetail1) || y.DebitDetailCodeFirst == _param.AccountCodeDetail1)
                && (string.IsNullOrEmpty(_param.AccountCodeDetail2) || y.DebitDetailCodeSecond == _param.AccountCodeDetail2)).Sum(q => q.Amount);
            double _dauKyNo_SoLuong = listLedgerBefore.Where(y => y.DebitCode == _param.AccountCode
            && (string.IsNullOrEmpty(_param.AccountCodeDetail1) || y.DebitDetailCodeFirst == _param.AccountCodeDetail1)
                && (string.IsNullOrEmpty(_param.AccountCodeDetail2) || y.DebitDetailCodeSecond == _param.AccountCodeDetail2)).Sum(q => q.Quantity);

            double _dauKyCo_NgoaiTe = listLedgerBefore.Where(y => y.CreditCode == _param.AccountCode
            && (string.IsNullOrEmpty(_param.AccountCodeDetail1) || y.CreditDetailCodeFirst == _param.AccountCodeDetail1)
                && (string.IsNullOrEmpty(_param.AccountCodeDetail2) || y.CreditDetailCodeSecond == _param.AccountCodeDetail2)).Sum(q => q.OrginalCurrency);
            double _dauKyCo_VND = listLedgerBefore.Where(y => y.CreditCode == _param.AccountCode
            && (string.IsNullOrEmpty(_param.AccountCodeDetail1) || y.CreditDetailCodeFirst == _param.AccountCodeDetail1)
                && (string.IsNullOrEmpty(_param.AccountCodeDetail2) || y.CreditDetailCodeSecond == _param.AccountCodeDetail2)).Sum(q => q.Amount);
            double _dauKyCo_SoLuong = listLedgerBefore.Where(y => y.CreditCode == _param.AccountCode
            && (string.IsNullOrEmpty(_param.AccountCodeDetail1) || y.CreditDetailCodeFirst == _param.AccountCodeDetail1)
                && (string.IsNullOrEmpty(_param.AccountCodeDetail2) || y.CreditDetailCodeSecond == _param.AccountCodeDetail2)).Sum(q => q.Quantity);

            relations.ForEach(x =>
            {
                double _sampl = _samplOpeningBackLog;
                GetResidualAmount(ref _sampl, x.DebitCode.Equals(_param.AccountCode) ? x.DebitAmount : x.CreditAmount * -1);
                x.ResidualDebit = _sampl > 0 ? _sampl : 0;
                x.ResidualCredit = _sampl < 0 ? Math.Abs(_sampl) : 0;
                _samplOpeningBackLog = _sampl;
                x.Thu_Amount = x.IsDebit ? x.Amount : 0;// ArisingDebit_OrginalCur
                x.Chi_Amount = !x.IsDebit ? x.Amount : 0;//ArisingCredit_OrginalCur
                if (!isNoiBo)
                {
                    x.Residual_Amount = (_OpeningBackLog.OpeningAmountLeft) + x.Thu_Amount - x.Chi_Amount;
                    x.Temp = long.Parse(x.Month + "" + x.OrginalBookDate.Value.Year);
                    _OpeningBackLog.OpeningAmountLeft = x.Residual_Amount;
                    x.ArisingDebit_Foreign = x.IsDebit ? x.OrginalCurrency : 0;
                    x.ArisingCredit_Foreign = !x.IsDebit ? x.OrginalCurrency : 0;
                    x.ResidualAmount_Foreign = (_OpeningBackLog.OriginalCurrency + _dauKyNo_NgoaiTe - _dauKyCo_NgoaiTe) + x.ArisingDebit_Foreign - x.ArisingCredit_Foreign;
                    x.ResidualAmount_OrginalCur = (_OpeningBackLog.ExchangeRate + _dauKyNo_VND - _dauKyCo_VND) + x.Thu_Amount - x.Chi_Amount;
                    _OpeningBackLog.OriginalCurrency = x.ResidualAmount_Foreign;
                    _OpeningBackLog.ExchangeRate = x.ResidualAmount_OrginalCur;
                    //4
                    x.Input_Quantity = x.IsDebit ? x.Quantity : 0;
                    x.Output_Quantity = !x.IsDebit ? x.Quantity : 0;
                    x.Residual_Quantity = (_OpeningBackLog.OpeningStockQuantity + _dauKyNo_SoLuong - _dauKyCo_SoLuong) + x.Input_Quantity - x.Output_Quantity;
                    _OpeningBackLog.OpeningStockQuantity = x.Residual_Quantity;
                }
                else
                {
                    x.Residual_Amount = (_OpeningBackLog.OpeningAmountLeftNB) + x.Thu_Amount - x.Chi_Amount;
                    x.Temp = long.Parse(x.Month + "" + x.OrginalBookDate.Value.Year);
                    _OpeningBackLog.OpeningAmountLeftNB = x.Residual_Amount;
                    x.ArisingDebit_Foreign = x.IsDebit ? x.OrginalCurrency : 0;
                    x.ArisingCredit_Foreign = !x.IsDebit ? x.OrginalCurrency : 0;
                    x.ResidualAmount_Foreign = (_OpeningBackLog.OriginalCurrencyNB + _dauKyNo_NgoaiTe - _dauKyCo_NgoaiTe) + x.ArisingDebit_Foreign - x.ArisingCredit_Foreign;
                    x.ResidualAmount_OrginalCur = (_OpeningBackLog.ExchangeRateNB + _dauKyNo_VND - _dauKyCo_VND) + x.Thu_Amount - x.Chi_Amount;
                    _OpeningBackLog.OriginalCurrencyNB = x.ResidualAmount_Foreign;
                    _OpeningBackLog.ExchangeRateNB = x.ResidualAmount_OrginalCur;
                    //4
                    x.Input_Quantity = x.IsDebit ? x.Quantity : 0;
                    x.Output_Quantity = !x.IsDebit ? x.Quantity : 0;
                    x.Residual_Quantity = (_OpeningBackLog.OpeningStockQuantityNB + _dauKyNo_SoLuong - _dauKyCo_SoLuong) + x.Input_Quantity - x.Output_Quantity;
                    _OpeningBackLog.OpeningStockQuantityNB = x.Residual_Quantity;
                }
            });

            LedgerReportCalculatorIO _OpeningBackLog_2 = await CalculatorFollowMonth_ThuChi(_param, year);
            IDictionary<long, List<SoChiTietThuChiViewModel>> v_dicTotal = new Dictionary<long, List<SoChiTietThuChiViewModel>>();
            List<long> _months1 = relations.Select(x => x.Temp).Distinct().ToList();
            int _ind = 0;
            double _luyKe_PS_Thu = 0, _luyKe_PS_Chi = 0, _luyKe_PS_Ton = 0;
            double _luyKe_PS_No_SoLuong = 0, _luyKe_PS_Co_SoLuong = 0;
            double _quantityDK = 0;
            double _donGiaDK = 0;
            double _tienDK = 0;
            double _exchangeRateDK = 0;
            double _OrginalCurrencyDK = 0;
            if (!isNoiBo)
                _luyKe_PS_Ton = _OpeningBackLog_2.OpeningAmountLeft + _dauKy_Thu - _dauKy_Chi;
            else
                _luyKe_PS_Ton = _OpeningBackLog_2.OpeningAmountLeftNB + _dauKy_Thu - _dauKy_Chi;

            var chartAcc = await _context.GetChartOfAccount(year).FirstOrDefaultAsync(y => y.Code == _param.AccountCodeDetail1 && y.ParentRef == _param.AccountCode);
            if (chartAcc != null)
            {
                if (_param.FromDate.Value.Year != year)
                {
                    var accountOld = await _context.ChartOfAccounts.FirstOrDefaultAsync(x => x.Code == chartAcc.Code && x.ParentRef == chartAcc.ParentRef
                                    && (string.IsNullOrEmpty(chartAcc.WarehouseCode) || x.WarehouseCode == chartAcc.WarehouseCode) && x.Year == _param.FromDate.Value.Year);
                    chartAcc.OpeningDebit = accountOld?.OpeningDebit;
                    chartAcc.OpeningDebitNB = accountOld?.OpeningDebitNB;
                    chartAcc.OpeningCredit = accountOld?.OpeningCredit;
                    chartAcc.OpeningCreditNB = accountOld?.OpeningCreditNB;
                    chartAcc.StockUnitPrice = accountOld?.StockUnitPrice;
                    chartAcc.StockUnitPriceNB = accountOld?.StockUnitPriceNB;
                    chartAcc.OpeningForeignDebit = accountOld?.OpeningForeignDebit;
                    chartAcc.OpeningForeignDebitNB = accountOld?.OpeningForeignDebitNB;
                    chartAcc.OpeningStockQuantity = accountOld?.OpeningStockQuantity;
                    chartAcc.OpeningStockQuantityNB = accountOld?.OpeningStockQuantityNB;
                }
                //4
                _quantityDK = (isNoiBo ? chartAcc.OpeningStockQuantityNB : chartAcc.OpeningStockQuantity) ?? 0;
                _donGiaDK = (isNoiBo ? chartAcc.StockUnitPriceNB : chartAcc.StockUnitPrice) ?? 0;
                _tienDK = ((isNoiBo ? chartAcc.OpeningDebitNB : chartAcc.OpeningDebit) ?? 0) - ((isNoiBo ? chartAcc.OpeningCreditNB : chartAcc.OpeningCredit) ?? 0);
                //3
                _exchangeRateDK = (isNoiBo ? chartAcc.OpeningForeignDebitNB : chartAcc.OpeningForeignDebit) ?? 0;
                _OrginalCurrencyDK = (isNoiBo ? chartAcc.OpeningDebitNB : chartAcc.OpeningDebit) ?? 0;
            }
            foreach (var x in _months1)
            {
                if (!v_dicTotal.ContainsKey(x))
                {
                    SoChiTietThuChiViewModel _congPS = new SoChiTietThuChiViewModel();
                    _congPS.Thu_Amount = relations.Where(y => y.Temp == x).Sum(q => q.Thu_Amount);
                    _congPS.Chi_Amount = relations.Where(y => y.Temp == x).Sum(q => q.Chi_Amount);
                    _congPS.Residual_Amount = _luyKe_PS_Ton + _congPS.Thu_Amount - _congPS.Chi_Amount;
                    _congPS.Month = -1;//cộng phát sinh
                    _congPS.ArisingDebit_Foreign = relations.Where(y => y.Temp == x).Sum(q => q.ArisingDebit_Foreign);
                    _congPS.ArisingCredit_Foreign = relations.Where(y => y.Temp == x).Sum(q => q.ArisingCredit_Foreign);
                    _congPS.ResidualAmount_Foreign = relations.Where(y => y.Temp == x).Sum(q => q.ResidualAmount_Foreign);
                    _congPS.ResidualAmount_OrginalCur = relations.Where(y => y.Temp == x).Sum(q => q.ResidualAmount_OrginalCur);
                    //4
                    _congPS.Input_Quantity = relations.Where(y => y.Temp == x).Sum(q => q.Input_Quantity);
                    _congPS.Output_Quantity = relations.Where(y => y.Temp == x).Sum(q => q.Output_Quantity);
                    _congPS.Residual_Quantity = relations.Where(y => y.Temp == x).Sum(q => q.Residual_Quantity);

                    SoChiTietThuChiViewModel _LuyKeNam = new SoChiTietThuChiViewModel();
                    _LuyKeNam.Thu_Amount = relations.Where(y => y.Temp == x).Sum(q => q.Thu_Amount);
                    _LuyKeNam.Chi_Amount = relations.Where(y => y.Temp == x).Sum(q => q.Chi_Amount);
                    _LuyKeNam.Residual_Amount = _congPS.Residual_Amount;
                    _LuyKeNam.Month = -2;//cộng lũy kế
                    _LuyKeNam.ArisingDebit_Foreign = relations.Where(y => y.Temp == x).Sum(q => q.ArisingDebit_Foreign);
                    _LuyKeNam.ArisingCredit_Foreign = relations.Where(y => y.Temp == x).Sum(q => q.ArisingCredit_Foreign);
                    _LuyKeNam.ResidualAmount_Foreign = relations.Where(y => y.Temp == x).Sum(q => q.ResidualAmount_Foreign);
                    _LuyKeNam.ResidualAmount_OrginalCur = relations.Where(y => y.Temp == x).Sum(q => q.ResidualAmount_OrginalCur);
                    //4
                    _LuyKeNam.Input_Quantity = relations.Where(y => y.Temp == x).Sum(q => q.Input_Quantity);
                    _LuyKeNam.Output_Quantity = relations.Where(y => y.Temp == x).Sum(q => q.Output_Quantity);
                    _LuyKeNam.Residual_Quantity = relations.Where(y => y.Temp == x).Sum(q => q.Residual_Quantity);

                    int _ms = (int)x / 10000;
                    int _year = (int)x % 10000;
                    if (x > long.Parse("1" + _year))
                    {
                        double _thuDK = 0, _chiDK = 0;
                        if (!isNoiBo)
                        {
                            _thuDK = _OpeningBackLog_2.OpeningDebit;
                            _chiDK = _OpeningBackLog_2.OpeningCredit;
                        }
                        else
                        {
                            _thuDK = _OpeningBackLog_2.OpeningDebitNB;
                            _chiDK = _OpeningBackLog_2.OpeningCreditNB;
                        }

                        _luyKe_PS_Thu = await _context.GetLedgerNotForYear(isNoiBo ? 3 : 2).Where(x => x.IsInternal != LedgerInternalConst.LedgerTemporary &&  x.OrginalBookDate >= dtFromBefore)
                        .Where(y => y.Month < _ms && y.Year <= _year)
                        .Where(y => y.DebitCode == _param.AccountCode
            && (string.IsNullOrEmpty(_param.AccountCodeDetail1) || y.DebitDetailCodeFirst == _param.AccountCodeDetail1)
            && (string.IsNullOrEmpty(_param.AccountCodeDetail2) || y.DebitDetailCodeSecond == _param.AccountCodeDetail2)
            ).SumAsync(q => q.Amount);

                        _luyKe_PS_Chi = await _context.GetLedgerNotForYear(isNoiBo ? 3 : 2).Where(x => x.IsInternal != LedgerInternalConst.LedgerTemporary &&  x.OrginalBookDate >= dtFromBefore)
                        .Where(y => y.Month < _ms && y.Year <= _year)
                        .Where(y => y.CreditCode == _param.AccountCode
            && (string.IsNullOrEmpty(_param.AccountCodeDetail1) || y.CreditDetailCodeFirst == _param.AccountCodeDetail1)
            && (string.IsNullOrEmpty(_param.AccountCodeDetail2) || y.CreditDetailCodeSecond == _param.AccountCodeDetail2)
            )
                        .SumAsync(q => q.Amount);

                        _LuyKeNam.Thu_Amount += _luyKe_PS_Thu;
                        _LuyKeNam.Chi_Amount += _luyKe_PS_Chi;

                        _thuDK += _LuyKeNam.Thu_Amount;
                        _chiDK += _LuyKeNam.Chi_Amount;
                        _LuyKeNam.Residual_Amount = _thuDK - _chiDK;

                        var listLuyKeNo = relations.Where(y => y.Month < _ms && y.Year <= _year)
                            .Where(y => y.DebitCode == _param.AccountCode && y.DebitDetailCodeFirst == _param.AccountCodeDetail1).ToList();
                        var listLuyKeCo = relations.Where(y => y.Month < _ms && y.Year <= _year)
                        .Where(y => y.CreditCode == _param.AccountCode && y.CreditDetailCodeFirst == _param.AccountCodeDetail1).ToList();
                        if (_param.BookDetailType == 3)
                        {
                            _luyKe_PS_No_NT = listLuyKeNo.Sum(q => q.OrginalCurrency);
                            _luyKe_PS_No_VND = listLuyKeNo.Sum(q => q.Amount);
                            _luyKe_PS_Co_NT = listLuyKeCo.Sum(q => q.OrginalCurrency);
                            _luyKe_PS_Co_VND = listLuyKeCo.Sum(q => q.Amount);

                            _LuyKeNam.ArisingDebit_Foreign += _luyKe_PS_No_NT;
                            _LuyKeNam.ArisingCredit_Foreign += _luyKe_PS_Co_NT;

                            if (_luyKe_PS_No_NT > 0)
                            {
                                _luyKe_PS_No_NT += _exchangeRateDK;
                                _luyKe_PS_No_VND += _OrginalCurrencyDK;
                            }
                            else
                            {
                                if (_luyKe_PS_Co_NT > 0)
                                {
                                    _luyKe_PS_Co_NT -= _exchangeRateDK;
                                    _luyKe_PS_Co_VND -= _OrginalCurrencyDK;
                                }
                            }
                            _LuyKeNam.ResidualAmount_Foreign = relations.Where(y => y.Temp == x).Sum(q => q.ResidualAmount_Foreign);
                            _LuyKeNam.ResidualAmount_OrginalCur = relations.Where(y => y.Temp == x).Sum(q => q.ResidualAmount_OrginalCur);
                        }
                        else if (_param.BookDetailType == 4)
                        {
                            _luyKe_PS_No_SoLuong = listLuyKeNo.Sum(q => q.Quantity);
                            _luyKe_PS_Co_SoLuong = listLuyKeCo.Sum(q => q.Quantity);
                            _LuyKeNam.Input_Quantity += _luyKe_PS_No_SoLuong;
                            _LuyKeNam.Output_Quantity += _luyKe_PS_Co_SoLuong;

                            if (_luyKe_PS_No_SoLuong > 0)
                            {
                                _luyKe_PS_No_SoLuong += _quantityDK;
                            }
                            else if (_luyKe_PS_Co_SoLuong > 0)
                            {
                                _luyKe_PS_Co_SoLuong -= _quantityDK;
                            }
                            _LuyKeNam.Residual_Quantity = relations.Where(y => y.Temp == x).Sum(q => q.Residual_Quantity);
                            _LuyKeNam.Residual_Amount = relations.Where(y => y.Temp == x).Sum(q => q.Residual_Amount);
                        }
                    }
                    _luyKe_PS_Ton = _congPS.Residual_Amount;
                    SoChiTietThuChiViewModel _du = new SoChiTietThuChiViewModel();
                    _du.Month = -3;//dư
                    _du.ArisingDebit_Foreign = 0;
                    _du.ArisingCredit_Foreign = 0;
                    _du.Input_Quantity = 0;
                    _du.Output_Quantity = 0;

                    if (!isNoiBo)
                    {
                        _du.Thu_Amount = _OpeningBackLog_2.OpeningDebit + _LuyKeNam.Thu_Amount;
                        _du.Chi_Amount = _OpeningBackLog_2.OpeningCredit + _LuyKeNam.Chi_Amount;
                        _du.Residual_Amount = _OpeningBackLog_2.OpeningAmountLeft + _LuyKeNam.Thu_Amount - _LuyKeNam.Chi_Amount;

                        _du.ResidualAmount_Foreign = _OpeningBackLog_2.OriginalCurrency + _LuyKeNam.ArisingDebit_Foreign - _LuyKeNam.ArisingCredit_Foreign;
                        _du.ResidualAmount_OrginalCur = _OpeningBackLog_2.ExchangeRate + _LuyKeNam.Thu_Amount - _LuyKeNam.Chi_Amount;
                        _du.Residual_Quantity = _OpeningBackLog_2.OpeningStockQuantity + _LuyKeNam.Input_Quantity - _LuyKeNam.Output_Quantity;
                    }
                    else
                    {
                        _du.Thu_Amount = _OpeningBackLog_2.OpeningDebitNB + _LuyKeNam.Thu_Amount;
                        _du.Chi_Amount = _OpeningBackLog_2.OpeningCreditNB + _LuyKeNam.Chi_Amount;
                        _du.Residual_Amount = _OpeningBackLog_2.OpeningAmountLeftNB + _LuyKeNam.Thu_Amount - _LuyKeNam.Chi_Amount;

                        _du.ResidualAmount_Foreign = _OpeningBackLog_2.OriginalCurrencyNB + _LuyKeNam.ArisingDebit_Foreign - _LuyKeNam.ArisingCredit_Foreign;
                        _du.ResidualAmount_OrginalCur = _OpeningBackLog_2.ExchangeRateNB + _LuyKeNam.Thu_Amount - _LuyKeNam.Chi_Amount;
                        _du.Residual_Quantity = _OpeningBackLog_2.OpeningStockQuantityNB + _LuyKeNam.Input_Quantity - _LuyKeNam.Output_Quantity;
                    }

                    v_dicTotal.Add(x, new List<SoChiTietThuChiViewModel>
                    {
                        _congPS,
                        _LuyKeNam,
                        _du
                    });

                    _ind++;
                }
            }
            LedgerReportModel _model = new LedgerReportModel
            {
                InfoSum = null,
                Items = null,
                BookDetails = relations,
                LedgerCalculator = null,
                Address = _company.Address,
                Company = _company.Name,
                MethodCalcExportPrice = _company.MethodCalcExportPrice,
                TaxId = _company.MST,
                CEOName = _company.NameOfCEO,
                ChiefAccountantName = _company.NameOfChiefAccountant,
                AccountCode = _accountFind?.Code,
                AccountName = _accountFind?.Name + (accountChild != null ? (" - " + accountChild.Name) : string.Empty),
                SumItem_SCT_ThuChi = v_dicTotal,
                CEONote = _company.NoteOfCEO,
                ChiefAccountantNote = _company.NoteOfChiefAccountant,
            };

            return await ExportDataReport_SoChiTiet(_model, _param, year);
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task<string> ExportDataReport_SoChiTiet(LedgerReportModel ledgers, LedgerReportParamDetail param, int year)
    {
        try
        {
            var _accountGet = await _accountBalanceSheet.
                GenerateAccrualAccounting("date", param.FromDate, param.ToDate, param.AccountCode, param.AccountCodeDetail1, param.AccountCodeDetail2, param.IsNoiBo);

            string _path = string.Empty;
            switch (param.FileType)
            {
                case "html":
                    _path = ConvertToHTML_SoChiTiet_PhanMau(ledgers, param, _accountGet.OpeningStock, year);
                    break;

                case "excel":
                    _path = ExportExcel_Report_SoChiTiet_PhanMau(ledgers, param, _accountGet.OpeningStock);
                    break;

                case "pdf":
                    _path = ConvertToPDFFile_SoChiTiet(ledgers, param, _accountGet.OpeningStock, year);
                    break;
            }
            return _path;
        }
        catch
        {
            return string.Empty;
        }
    }

    private string ConvertToHTML_SoChiTiet_PhanMau(LedgerReportModel p, LedgerReportParamDetail param, double openingStock, int year)
    {
        try
        {
            string _html = string.Empty;
            switch (param.BookDetailType)
            {
                case (int)ReportBookDetailTypeEnum.soCoDu_1_ben:
                    _html = ConvertToHTML_SoChiTiet_Loai_1(p, param, openingStock, year);
                    break;

                case (int)ReportBookDetailTypeEnum.soCoDu_2_ben:
                    _html = ConvertToHTML_SoChiTiet_And_2(p, param, openingStock, year);
                    break;

                case (int)ReportBookDetailTypeEnum.soCoNgoaiTe:
                    _html = ConvertToHTML_SoChiTiet_Loai_3(p, param, openingStock, year);
                    break;

                case (int)ReportBookDetailTypeEnum.soCoHangTonKho:
                    _html = ConvertToHTML_SoChiTiet_Loai_4(p, param, year);
                    break;

                case (int)ReportBookDetailTypeEnum.soQuy:
                    _html = ConvertToHTML_SoChiTiet_Loai_5(p, param, openingStock, year);
                    break;

                case (int)ReportBookDetailTypeEnum.soSoLuongTonKho:
                    _html = ConvertToHTML_SoChiTiet_Loai_6(p, param, year);
                    break;

                default:
                    break;
            }
            return _html;
        }
        catch
        {
            return string.Empty;
        }
    }

    private string ExportExcel_Report_SoChiTiet_PhanMau(LedgerReportModel p, LedgerReportParam param, double openingStock)
    {
        try
        {
            if (param.BookDetailType == (int)ReportBookDetailTypeEnum.soSoLuongTonKho)
            {
                string _html6 = ExportExcel_Report_SoChiTiet_Loai_6(p, param);
                return _html6;
            }
            else if (param.BookDetailType == (int)ReportBookDetailTypeEnum.soCoHangTonKho)
            {
                string _html6 = ExportExcel_Report_SoChiTiet_Loai_4(p, param);
                return _html6;
            }
            string _html = ExportExcel_Report_SoChiTiet_Loai_1_And_2(p, param, openingStock);

            return _html;
        }
        catch
        {
            return string.Empty;
        }
    }

    private string ConvertToPDFFile_SoChiTiet(LedgerReportModel p, LedgerReportParamDetail param, double openingStock, int year)
    {
        try
        {
            string _allText = ConvertToHTML_SoChiTiet_PhanMau(p, param, openingStock, year);
            return ExcelHelpers.ConvertUseDinkLandscape(_allText, _converterPDF, Directory.GetCurrentDirectory(), "SoChiTiet");
        }
        catch
        {
            return string.Empty;
        }
    }

    private string ConvertToHTML_SoChiTiet_And_2(LedgerReportModel p, LedgerReportParamDetail param, double openingStock, int year)
    {
        try
        {
            if (p == null) return string.Empty;

            string _template = "SoChiTiet_TwoTemplate.html",
                _folderPath = @"Uploads\Html",
                path = Path.Combine(Directory.GetCurrentDirectory(), _folderPath, _template),
                _allText = System.IO.File.ReadAllText(path), resultHTML = string.Empty;
            IDictionary<string, string> v_dicFixed = new Dictionary<string, string>
            {
                { "TIEU_DE","SỔ CHI TIẾT "+ p.AccountName.ToUpper() },
                { "TenCongTy", p.Company },
                { "DiaChi", p.Address },
                { "MST", p.TaxId },
                { "NgayChungTu", string.Empty },
                { "TaiKhoanAccount", p.AccountCode +" : "+p.AccountName },
                { "TuThang", param.FromDate.Value.Month.ToString("D2")},
                { "DenThang", param.ToDate.Value.Month.ToString("D2") },
                { "Nam",  year.ToString("D4") },
                { "NguoiLap", !string.IsNullOrEmpty(param.LedgerReportMaker) ? param.LedgerReportMaker : string.Empty },
                { "Ngay", (param.ToDate.Value.Day).ToString("D2") },
                { "Thang",  (param.ToDate.Value.Month).ToString("D2") },
                { "NamSign",   year.ToString() },
                { "KeToanTruong", param.isCheckName ? p.ChiefAccountantName : string.Empty },
                { "GiamDoc", param.isCheckName ? p.CEOName : string.Empty },
                { "SO_DU_BEN", "(BÊN NỢ)" },
                { "LUY_KE_DAU_NAM", openingStock > 0 ? String.Format("{0:N0}", openingStock) : string.Empty},
                { "KeToanTruong_CV", p.ChiefAccountantNote},
                { "GiamDoc_CV", p.CEONote},
            };
            string soThapPhan = "N" + p.MethodCalcExportPrice;
            v_dicFixed.Keys.ToList().ForEach(x => _allText = _allText.Replace("{{{" + x + "}}}", v_dicFixed[x]));
            int _ind = -1;

            resultHTML = @"<tr class='font-b'>
                                                                <td colspan='6'>Lũy kế đầu năm</td>
                                                                <td></td>
                                                                <td></td>
                                                                <td class='txt-right'>{{LUY_KE_DAU_NAM_NO}}</td>
                                                                <td class='txt-right'>{{LUY_KE_DAU_NAM_CO}}</td>
                                                            </tr>";
            resultHTML = resultHTML.Replace("{{LUY_KE_DAU_NAM_NO}}", openingStock > 0 ? String.Format("{0:N0}", Math.Abs(openingStock)) : string.Empty)
                                    .Replace("{{LUY_KE_DAU_NAM_CO}}", openingStock < 0 ? String.Format("{0:N0}", openingStock) : string.Empty);

            if (p.BookDetails.Count > 0)
            {
                p.BookDetails.ForEach(x =>
                {
                    _ind++;
                    string _txt = @"<tr>
                                            <td>{{{NGAY_GHI_SO}}}</td>
                                    <td><a href='{{{URL_LINK}}}#{{{FILTER_TYPE}}}#{{{FILTER_TEXT}}}#{{{FILTER_MONTH}}}#{{{FILTER_ISINTERNAL}}}' target='_blank'>{{{CHUNG_TU_SO}}}</a></td>

                                            <td>{{{CHUNG_TU_NGAY}}}</td>
                                            <td class='tbl-td-diengiai'>{{{DIEN_GIAI}}}</td>
                                            <td>{{{TK_DOI_UNG}}}</td>
                                            <td>{{{CHI_TIET}}}</td>
                                            <td class='txt-right'>{{{SO_TIEN_PS_NO}}}</td>
                                            <td class='txt-right'>{{{SO_TIEN_PS_CO}}}</td>
                                            <td class='txt-right'>{{{SO_TIEN_DU_NO}}}</td>
                                            <td class='txt-right'>{{{SO_TIEN_DU_CO}}}</td>
                                        </tr>";

                    _txt = _txt.Replace("{{{NGAY_GHI_SO}}}", x.OrginalBookDate.HasValue ? x.OrginalBookDate.Value.ToString("dd/MM/yyyy") : string.Empty)
                    .Replace("{{{URL_LINK}}}", UrlLinkConst.UrlLinkArise)
                                        .Replace("{{{CHUNG_TU_SO}}}", x.OrginalVoucherNumber)
                                        .Replace("{{{CHUNG_TU_NGAY}}}", x.OrginalBookDate.HasValue ? x.OrginalBookDate.Value.ToString("dd/MM/yyyy") : string.Empty)
                                        .Replace("{{{DIEN_GIAI}}}", x.Description)
                                        .Replace("{{{TK_DOI_UNG}}}", x.IsDebit ? x.CreditCode : x.DebitCode)
                                        .Replace("{{{CHI_TIET}}}", x.DetailCode)
                                        .Replace("{{{SO_TIEN_PS_NO}}}", x.ArisingDebit > 0 ? String.Format("{0:" + soThapPhan + "}", x.ArisingDebit) : string.Empty)
                                        .Replace("{{{SO_TIEN_PS_CO}}}", x.ArisingCredit > 0 ? String.Format("{0:" + soThapPhan + "}", x.ArisingCredit) : string.Empty)
                                        .Replace("{{{SO_TIEN_DU_NO}}}", x.ResidualDebit > 0 ? String.Format("{0:" + soThapPhan + "}", x.ResidualDebit) : string.Empty)
                                        .Replace("{{{SO_TIEN_DU_CO}}}", x.ResidualCredit > 0 ? String.Format("{0:" + soThapPhan + "}", x.ResidualCredit) : string.Empty)

                                        .Replace("{{{FILTER_TYPE}}}", x.VoucherNumber.Split('/')[1])
                               .Replace("{{{FILTER_MONTH}}}", x.Month.ToString())
                               .Replace("{{{FILTER_ISINTERNAL}}}", (param.IsNoiBo ? "3" : "1"))
                               .Replace("{{{FILTER_TEXT}}}", x.OrginalVoucherNumber)
;

                    resultHTML += _txt;

                    SoChiTietViewModel _sct = p.BookDetails[_ind + 1 < p.BookDetails.Count ? _ind + 1 : p.BookDetails.Count - 1];
                    if (!param.IsNewReport && ((_sct.Month + "" + _sct.Year) != (x.Month + "" + x.Year) && _ind < p.BookDetails.Count) || (_ind == p.BookDetails.Count - 1) && p.SumItem_SCT_ThuChi.ContainsKey(x.Temp))
                    {
                        List<SoChiTietThuChiViewModel> _ledgerSum = p.SumItem_SCT_ThuChi[x.Temp];

                        SoChiTietThuChiViewModel _CongPhatSinh = _ledgerSum.FirstOrDefault(x => x.Month == -1) ?? new SoChiTietThuChiViewModel();
                        SoChiTietThuChiViewModel _LuyKe = _ledgerSum.FirstOrDefault(x => x.Month == -2) ?? new SoChiTietThuChiViewModel();

                        string _sumRowMonthHTML = @"<tr class='font-b'>
                                                                <td colspan='4'>Cộng phát sinh tháng {{THANG_ROW}}</td>
                                                                <td></td>
                                                                <td></td>
                                                                <td class='txt-right'>{{SR_TONG_SO_TIEN_PS_NO_CONG}}</td>
                                                                <td class='txt-right'>{{SR_TONG_SO_TIEN_PS_CO_CONG}}</td>
                                                                <td class='txt-right'>{{SR_TONG_SO_TIEN_DU_NO_CONG}}</td>
                                                                <td class='txt-right'>{{SR_TONG_SO_TIEN_DU_CO_CONG}}</td>
                                                            </tr>
			                                                <tr class='font-b'>
                                                                <td colspan='4'>Lũy kế phát sinh đến cuối kỳ</td>
                                                                <td></td>
                                                                <td></td>
                                                                <td class='txt-right'>{{SR_TONG_SO_TIEN_PS_NO_LK}}</td>
                                                                <td class='txt-right'>{{SR_TONG_SO_TIEN_PS_CO_LK}}</td>
                                                                <td class='txt-right'>{{SR_TONG_SO_TIEN_DU_NO_LK}}</td>
                                                                <td class='txt-right'>{{SR_TONG_SO_TIEN_DU_CO_LK}}</td>
                                                            </tr>    ";

                        _sumRowMonthHTML = _sumRowMonthHTML.Replace("{{THANG_ROW}}", x.Month.ToString("00") + "/" + x.Year.ToString())

                         .Replace("{{SR_TONG_SO_TIEN_PS_NO_CONG}}", _CongPhatSinh.Thu_Amount > 0 ? String.Format("{0:" + soThapPhan + "}", _CongPhatSinh.Thu_Amount) : string.Empty)
                         .Replace("{{SR_TONG_SO_TIEN_PS_CO_CONG}}", _CongPhatSinh.Chi_Amount > 0 ? String.Format("{0:" + soThapPhan + "}", _CongPhatSinh.Chi_Amount) : string.Empty)
                         .Replace("{{SR_TONG_SO_TIEN_DU_NO_CONG}}", _CongPhatSinh.Residual_Amount > 0 ? String.Format("{0:" + soThapPhan + "}", _CongPhatSinh.Residual_Amount) : string.Empty)
                         .Replace("{{SR_TONG_SO_TIEN_DU_CO_CONG}}", _CongPhatSinh.Residual_Amount < 0 ? String.Format("{0:" + soThapPhan + "}", Math.Abs(_CongPhatSinh.Residual_Amount)) : string.Empty)

                         .Replace("{{SR_TONG_SO_TIEN_PS_NO_LK}}", _LuyKe.Thu_Amount > 0 ? String.Format("{0:" + soThapPhan + "}", _LuyKe.Thu_Amount) : string.Empty)
                         .Replace("{{SR_TONG_SO_TIEN_PS_CO_LK}}", _LuyKe.Chi_Amount > 0 ? String.Format("{0:" + soThapPhan + "}", _LuyKe.Chi_Amount) : string.Empty)
                         .Replace("{{SR_TONG_SO_TIEN_DU_NO_LK}}", _LuyKe.Residual_Amount > 0 ? String.Format("{0:" + soThapPhan + "}", _LuyKe.Residual_Amount) : string.Empty)
                         .Replace("{{SR_TONG_SO_TIEN_DU_CO_LK}}", _LuyKe.Residual_Amount < 0 ? String.Format("{0:" + soThapPhan + "}", Math.Abs(_LuyKe.Residual_Amount)) : string.Empty)
                        ;

                        resultHTML += _sumRowMonthHTML;
                    }
                });
            }

            _allText = _allText.Replace("##REPLACE_PLACE##", resultHTML);

            return _allText;
        }
        catch
        {
            return string.Empty;
        }
    }

    private string ConvertToHTML_SoChiTiet_Loai_1(LedgerReportModel p, LedgerReportParamDetail param, double openingStock, int year)
    {
        try
        {
            if (p == null) return string.Empty;

            string _template = "SoChiTiet_OneTemplate.html",
                _folderPath = @"Uploads\Html",
                path = Path.Combine(Directory.GetCurrentDirectory(), _folderPath, _template),
                _allText = System.IO.File.ReadAllText(path), resultHTML = string.Empty;
            IDictionary<string, string> v_dicFixed = new Dictionary<string, string>
            {
                { "TIEU_DE","SỔ CHI TIẾT "+ p.AccountName.ToUpper() },
                { "TenCongTy", p.Company },
                { "DiaChi", p.Address },
                { "MST", p.TaxId },
                { "NgayChungTu", string.Empty },
                { "TaiKhoanAccount", p.AccountCode +" : "+p.AccountName },
                { "TuThang", ((param.FromMonth > 0 && param.ToMonth > 0) ? ((int)param.FromMonth) : ((DateTime)param.FromDate).Month ).ToString("D2")   },
                { "DenThang", ( (param.FromMonth > 0 && param.ToMonth > 0) ? ((int)param.ToMonth) : ((DateTime)param.ToDate).Month ).ToString("D2") },
                { "Nam", ((param.FromMonth > 0 && param.ToMonth > 0) ? year : ((DateTime)param.FromDate).Year ).ToString("D4") },
                { "NguoiLap", !string.IsNullOrEmpty(param.LedgerReportMaker) ? param.LedgerReportMaker : string.Empty },
                { "Ngay", (param.ToDate.Value.Day).ToString("D2") },
                { "Thang",  (param.ToDate.Value.Month).ToString("D2") },
                { "NamSign",   year.ToString() },
                { "KeToanTruong", param.isCheckName ? p.ChiefAccountantName : string.Empty },
                { "GiamDoc", param.isCheckName ? p.CEOName : string.Empty },
                { "SO_DU_BEN", "(BÊN NỢ)" },
                { "LUY_KE_DAU_NAM", openingStock > 0 ? String.Format("{0:N0}", openingStock) : string.Empty},
                { "KeToanTruong_CV", p.ChiefAccountantNote},
                { "GiamDoc_CV", p.CEONote},
            };
            string soThapPhan = "N" + p.MethodCalcExportPrice;
            v_dicFixed.Keys.ToList().ForEach(x => _allText = _allText.Replace("{{{" + x + "}}}", v_dicFixed[x]));
            int _ind = -1;

            resultHTML = @"<tr class='font-b'>
                                                                <td colspan='5'>Lũy kế đầu năm</td>
                                                                <td></td>
                                                                <td></td>
                                                                <td  colspan='3' class='txt-right'>{{LUY_KE_DAU_NAM}}</td>
                                                            </tr>";
            resultHTML = resultHTML.Replace("{{LUY_KE_DAU_NAM}}", openingStock > 0 ? String.Format("{0:N0}", openingStock) : string.Empty)
                                                     .Replace("{{LUY_KE_DAU_NAM}}", openingStock > 0 ? String.Format("{0:N0}", openingStock) : string.Empty);

            if (p.BookDetails.Count > 0)
            {
                p.BookDetails.ForEach(x =>
                {
                    _ind++;
                    string _txt = @"<tr>
                                            <td>{{{NGAY_GHI_SO}}}</td>
                                            <td><a href='{{{URL_LINK}}}#{{{FILTER_TYPE}}}#{{{FILTER_TEXT}}}#{{{FILTER_MONTH}}}#{{{FILTER_ISINTERNAL}}}' target='_blank'>{{{CHUNG_TU_SO}}}</a></td>

                                            <td>{{{CHUNG_TU_NGAY}}}</td>
                                            <td>{{{SO_HOA_DON}}}</td>
                                            <td class='tbl-td-diengiai'>{{{DIEN_GIAI}}}</td>
                                            <td>{{{TK_DOI_UNG}}}</td>
                                            <td>{{{CHI_TIET}}}</td>
                                            <td class='txt-right'>{{{SO_TIEN_PS_NO}}}</td>
                                            <td class='txt-right'>{{{SO_TIEN_PS_CO}}}</td>
                                            <td class='txt-right' colspan='2'>{{{SO_TIEN_DU}}}</td>
                                        </tr>";

                    _txt = _txt
                    .Replace("{{{NGAY_GHI_SO}}}", x.OrginalBookDate.HasValue ? x.OrginalBookDate.Value.ToString("dd/MM/yyyy") : string.Empty)
                    .Replace("{{{URL_LINK}}}", UrlLinkConst.UrlLinkArise)
                                        .Replace("{{{CHUNG_TU_SO}}}", x.OrginalVoucherNumber)
                                        .Replace("{{{CHUNG_TU_NGAY}}}", x.OrginalBookDate.HasValue ? x.OrginalBookDate.Value.ToString("dd/MM/yyyy") : string.Empty)
                                        .Replace("{{{SO_HOA_DON}}}", x.InvoiceNumber)
                                        .Replace("{{{DIEN_GIAI}}}", x.Description)
                                        .Replace("{{{TK_DOI_UNG}}}", x.IsDebit ? x.CreditCode : x.DebitCode)
                                        .Replace("{{{CHI_TIET}}}", x.DetailCode)
                                        .Replace("{{{SO_TIEN_PS_NO}}}", x.ArisingDebit > 0 ? String.Format("{0:" + soThapPhan + "}", x.ArisingDebit) : string.Empty)
                                        .Replace("{{{SO_TIEN_PS_CO}}}", x.ArisingCredit > 0 ? String.Format("{0:" + soThapPhan + "}", x.ArisingCredit) : string.Empty)
                                        .Replace("{{{SO_TIEN_DU}}}", x.ResidualDebit > 0 ? String.Format("{0:" + soThapPhan + "}", x.ResidualDebit) : String.Format("{0:N0}", x.ResidualCredit))

                               .Replace("{{{FILTER_TEXT}}}", x.OrginalVoucherNumber)

                                        .Replace("{{{FILTER_TYPE}}}", x.VoucherNumber.Split('/')[1])
                               .Replace("{{{FILTER_MONTH}}}", x.Month.ToString())
                                                                      .Replace("{{{FILTER_ISINTERNAL}}}", (param.IsNoiBo ? "3" : "1"))

;

                    resultHTML += _txt;

                    SoChiTietViewModel _sct = p.BookDetails[_ind + 1 < p.BookDetails.Count ? _ind + 1 : p.BookDetails.Count - 1];
                    if (!param.IsNewReport && ((_sct.Month + "" + _sct.Year) != (x.Month + "" + x.Year) && _ind < p.BookDetails.Count) || (_ind == p.BookDetails.Count - 1) && p.SumItem_SCT_ThuChi.ContainsKey(x.Temp))
                    {
                        List<SoChiTietThuChiViewModel> _ledgerSum = p.SumItem_SCT_ThuChi[x.Temp];

                        SoChiTietThuChiViewModel _CongPhatSinh = _ledgerSum.FirstOrDefault(x => x.Month == -1);
                        SoChiTietThuChiViewModel _LuyKe = _ledgerSum.FirstOrDefault(x => x.Month == -2);

                        string _sumRowMonthHTML = @"                                                        <tr class='font-b'>
                                                                <td colspan='5'>Cộng phát sinh tháng {{THANG_ROW}}</td>
                                                                <td></td>
                                                                <td></td>
                                                                <td class='txt-right'>{{SR_THU}}</td>
                                                                <td class='txt-right'>{{SR_CHI}}</td>
                                                                <td class='txt-right' colspan='2'>{{SR_TON}}</td>
                                                            </tr>
			                                                <tr class='font-b'>
                                                                <td colspan='5'>Lũy kế phát sinh đến cuối kỳ</td>
                                                                <td></td>
                                                                <td></td>
                                                                <td class='txt-right'>{{SR_LK_THU}}</td>
                                                                <td class='txt-right'>{{SR_LK_CHI}}</td>
                                                                <td class='txt-right' colspan='2'>{{SR_LK_TON}}</td>
                                                            </tr>";

                        _sumRowMonthHTML = _sumRowMonthHTML.Replace("{{THANG_ROW}}", x.Month.ToString("00") + "/" + x.Year.ToString())
                         .Replace("{{SR_THU}}", _CongPhatSinh?.Thu_Amount > 0 ? String.Format("{0:" + soThapPhan + "}", _CongPhatSinh?.Thu_Amount) : string.Empty)
                         .Replace("{{SR_CHI}}", _CongPhatSinh?.Chi_Amount > 0 ? String.Format("{0:" + soThapPhan + "}", _CongPhatSinh?.Chi_Amount) : string.Empty)
                         .Replace("{{SR_TON}}", _CongPhatSinh?.Residual_Amount > 0 ? String.Format("{0:" + soThapPhan + "}", _CongPhatSinh?.Residual_Amount) : string.Empty)

                         .Replace("{{SR_LK_THU}}", _LuyKe?.Thu_Amount > 0 ? String.Format("{0:" + soThapPhan + "}", _LuyKe?.Thu_Amount) : string.Empty)
                         .Replace("{{SR_LK_CHI}}", _LuyKe?.Chi_Amount > 0 ? String.Format("{0:" + soThapPhan + "}", _LuyKe?.Chi_Amount) : string.Empty)
                         .Replace("{{SR_LK_TON}}", _LuyKe?.Residual_Amount > 0 ? String.Format("{0:" + soThapPhan + "}", _LuyKe?.Residual_Amount) : string.Empty)
                        ;

                        resultHTML += _sumRowMonthHTML;
                    }
                });
            }

            _allText = _allText.Replace("##REPLACE_PLACE##", resultHTML);

            return _allText;
        }
        catch
        {
            return string.Empty;
        }
    }

    private string ConvertToHTML_SoChiTiet_Loai_3(LedgerReportModel p, LedgerReportParamDetail param, double openingStock, int year)
    {
        try
        {
            if (p == null) return string.Empty;

            string _template = param.BookDetailType == 3 ? "SoChiTiet_ThreeTemplate.html" : "SoChiTiet_FourTemplate.html",
                _folderPath = @"Uploads\Html",
                path = Path.Combine(Directory.GetCurrentDirectory(), _folderPath, _template),
                _allText = System.IO.File.ReadAllText(path), resultHTML = string.Empty;
            IDictionary<string, string> v_dicFixed = new Dictionary<string, string>
            {
                { "TIEU_DE","SỔ CHI TIẾT "+ p.AccountName.ToUpper() },
                { "TenCongTy", p.Company },
                { "DiaChi", p.Address },
                { "MST", p.TaxId },
                { "NgayChungTu", string.Empty },
                { "TaiKhoanAccount", p.AccountCode+": "+p.AccountName },
                { "TuThang", param.FromDate.Value.Month.ToString("D2")   },
                { "DenThang", param.ToDate.Value.Month.ToString("D2") },
                { "Nam",  year.ToString("D4") },
                { "NguoiLap", !string.IsNullOrEmpty(param.LedgerReportMaker) ? param.LedgerReportMaker : string.Empty },
                { "Ngay", (param.ToDate.Value.Day).ToString("D2") },
                { "Thang",  (param.ToDate.Value.Month).ToString("D2") },
                { "NamSign",   year.ToString() },
                { "KeToanTruong", param.isCheckName ? p.ChiefAccountantName : string.Empty },
                { "GiamDoc", param.isCheckName ? p.CEOName : string.Empty },
                {"LUY_KE_DAU_NAM", openingStock > 0 ? String.Format("{0:N0}", openingStock) : string.Empty },

                { "KeToanTruong_CV", p.ChiefAccountantNote},
                { "GiamDoc_CV", p.CEONote},
            };
            string soThapPhan = "N" + p.MethodCalcExportPrice;
            v_dicFixed.Keys.ToList().ForEach(x => _allText = _allText.Replace("{{{" + x + "}}}", v_dicFixed[x]));
            int _ind = -1;

            resultHTML = @"<tr class='font-b'>
                                                                <td colspan='5'>Lũy kế đầu năm</td>
                                                                <td></td>
                                                                <td></td>
                                                                <td></td>
                                                                <td  colspan='6' class='txt-right'>{{LUY_KE_DAU_NAM}}</td>
                                                            </tr>";
            resultHTML = resultHTML.Replace("{{LUY_KE_DAU_NAM}}", openingStock > 0 ? String.Format("{0:N0}", openingStock) : string.Empty)
                                                     .Replace("{{LUY_KE_DAU_NAM}}", openingStock > 0 ? String.Format("{0:N0}", openingStock) : string.Empty);

            if (p.BookDetails.Count > 0 && param.BookDetailType == 3)
            {
                p.BookDetails.ForEach(x =>
                {
                    _ind++;
                    string _txt = @"<tr>
                                            <td>{{{NGAY_GHI_SO}}}</td>
                                    <td><a href='{{{URL_LINK}}}#{{{FILTER_TYPE}}}#{{{FILTER_TEXT}}}#{{{FILTER_MONTH}}}#{{{FILTER_ISINTERNAL}}}' target='_blank'>{{{CHUNG_TU_SO}}}</a></td>

                                            <td>{{{CHUNG_TU_NGAY}}}</td>
                                            <td class='tbl-td-diengiai' colspan='2'>{{{DIEN_GIAI}}}</td>
                                            <td>{{{TK_DOI_UNG}}}</td>
                                            <td>{{{CHI_TIET}}}</td>
                                            <td class='txt-right'>{{{TY_GIA}}}</td>
                                            <td class='txt-right'>{{{PSN_NGOAI_TE}}}</td>
                                            <td class='txt-right'>{{{PSN_VND}}}</td>
                                            <td class='txt-right'>{{{PSC_NGOAI_TE}}}</td>
                                            <td class='txt-right'>{{{PSC_VND}}}</td>
                                            <td class='txt-right'>{{{SD_NGOAI_TE}}}</td>
                                            <td class='txt-right'>{{{SD_VND}}}</td>
                                        </tr>";

                    _txt = _txt.Replace("{{{NGAY_GHI_SO}}}", x.OrginalBookDate.HasValue ? x.OrginalBookDate.Value.ToString("dd/MM/yyyy") : string.Empty)
                                        .Replace("{{{URL_LINK}}}", UrlLinkConst.UrlLinkArise)
                                        .Replace("{{{CHUNG_TU_SO}}}", x.OrginalVoucherNumber)
                                        .Replace("{{{CHUNG_TU_NGAY}}}", x.OrginalBookDate.HasValue ? x.OrginalBookDate.Value.ToString("dd/MM/yyyy") : string.Empty)
                                        .Replace("{{{DIEN_GIAI}}}", x.Description)
                                        .Replace("{{{TK_DOI_UNG}}}", x.IsDebit ? x.CreditCode : x.DebitCode)
                                        .Replace("{{{CHI_TIET}}}", x.DetailCode)

                                        .Replace("{{{TY_GIA}}}", (x.ExchangeRate > 0) ? String.Format("{0:" + soThapPhan + "}", x.ExchangeRate) : string.Empty)
                                        .Replace("{{{PSN_NGOAI_TE}}}", x.IsDebit ? String.Format("{0:" + soThapPhan + "}", x.OrginalCurrency) : string.Empty)
                                        .Replace("{{{PSN_VND}}}", x.IsDebit ? String.Format("{0:" + soThapPhan + "}", x.Amount) : string.Empty)
                                        .Replace("{{{PSC_NGOAI_TE}}}", !x.IsDebit ? String.Format("{0:" + soThapPhan + "}", x.OrginalCurrency) : string.Empty)
                                        .Replace("{{{PSC_VND}}}", !x.IsDebit ? String.Format("{0:" + soThapPhan + "}", x.Amount) : string.Empty)
                                        .Replace("{{{SD_NGOAI_TE}}}", x.ResidualAmount_Foreign < 0 ? "(" + String.Format("{0:" + soThapPhan + "}", Math.Abs(x.ResidualAmount_Foreign)) + ")" : String.Format("{0:" + soThapPhan + "}", x.ResidualAmount_Foreign))
                                        .Replace("{{{SD_VND}}}", x.ResidualAmount_OrginalCur < 0 ? "(" + String.Format("{0:" + soThapPhan + "}", Math.Abs(x.ResidualAmount_OrginalCur)) + ")" : String.Format("{0:" + soThapPhan + "}", x.ResidualAmount_OrginalCur))

                                                                               .Replace("{{{FILTER_TEXT}}}", x.OrginalVoucherNumber)

                                        .Replace("{{{FILTER_TYPE}}}", x.VoucherNumber.Split('/')[1])
                               .Replace("{{{FILTER_MONTH}}}", x.Month.ToString())
                                                                      .Replace("{{{FILTER_ISINTERNAL}}}", (param.IsNoiBo ? "3" : "1"))

;

                    resultHTML += _txt;

                    //asdasdasd
                    if (!param.IsNewReport)
                    {
                        SoChiTietViewModel _sct = p.BookDetails[_ind + 1 < p.BookDetails.Count ? _ind + 1 : p.BookDetails.Count - 1];
                        if (((_sct.Month + "" + _sct.Year) != (x.Month + "" + x.Year) && _ind < p.BookDetails.Count) || (_ind == p.BookDetails.Count - 1) && p.SumItem_SCT_ThuChi.ContainsKey(x.Temp))
                        {
                            List<SoChiTietThuChiViewModel> _ledgerSum = p.SumItem_SCT_ThuChi[x.Temp];

                            SoChiTietThuChiViewModel _CongPhatSinh = _ledgerSum.FirstOrDefault(x => x.Month == -1) ?? new SoChiTietThuChiViewModel();
                            SoChiTietThuChiViewModel _LuyKe = _ledgerSum.FirstOrDefault(x => x.Month == -2) ?? new SoChiTietThuChiViewModel();
                            SoChiTietThuChiViewModel _Du = _ledgerSum.FirstOrDefault(x => x.Month == -3) ?? new SoChiTietThuChiViewModel();

                            string _sumRowMonthHTML = @"
                                                            <tr class='font-b'>
                                                                <td colspan='3'></td>
                                                                <td colspan='2'>Cộng phát sinh tháng {{THANG_ROW}}</td>
                                                                <td></td>
                                                                <td></td>
                                                                <td></td>
                                                                <td class='txt-right'>{{SR_PSN_NGOAI_TE}}</td>
                                                                <td class='txt-right'>{{SR_PSN_VND}}</td>
                                                                <td class='txt-right'>{{SR_PSC_NGOAI_TE}}</td>
                                                                <td class='txt-right'>{{SR_PSC_VND}}</td>
                                                                <td class='txt-right'>{{SR_SD_NGOAI_TE}}</td>
                                                                <td class='txt-right'>{{SR_SD_VND}}</td>
                                                            </tr>
			                                                <tr class='font-b'>
                                                                <td colspan='3'></td>
                                                                <td colspan='2'>Lũy kế phát sinh đến cuối kỳ</td>
                                                                <td></td>
                                                                <td></td>
                                                                <td></td>
                                                                <td class='txt-right'>{{SR_LK_PSN_NGOAI_TE}}</td>
                                                                <td class='txt-right'>{{SR_LK_PSN_VND}}</td>
                                                                <td class='txt-right'>{{SR_LK_PSC_NGOAI_TE}}</td>
                                                                <td class='txt-right'>{{SR_LK_PSC_VND}}</td>
                                                                <td class='txt-right'>{{SR_LK_SD_NGOAI_TE}}</td>
                                                                <td class='txt-right'>{{SR_LK_SD_VND}}</td>
                                                            </tr>
                                                            <tr class='font-b'>
                                                                <td colspan='3'></td>
                                                                <td colspan='2'>Dư cuối {{THANG_ROW}}</td>
                                                                <td></td>
                                                                <td></td>
                                                                <td></td>
                                                                <td class='txt-right'>{{SR_DC_PSN_NGOAI_TE}}</td>
                                                                <td class='txt-right'>{{SR_DC_PSN_VND}}</td>
                                                                <td class='txt-right'>{{SR_DC_PSC_NGOAI_TE}}</td>
                                                                <td class='txt-right'>{{SR_DC_PSC_VND}}</td>
                                                                <td class='txt-right'>{{SR_DC_SD_NGOAI_TE}}</td>
                                                                <td class='txt-right'>{{SR_DC_SD_VND}}</td>
                                                            </tr>
";

                            _sumRowMonthHTML = _sumRowMonthHTML.Replace("{{THANG_ROW}}", x.Month.ToString("00") + "/" + x.Year.ToString())

                             .Replace("{{SR_PSN_NGOAI_TE}}", _CongPhatSinh.ArisingDebit_Foreign > 0 ? String.Format("{0:" + soThapPhan + "}", _CongPhatSinh.ArisingDebit_Foreign) : string.Empty)
                             .Replace("{{SR_PSN_VND}}", _CongPhatSinh.Thu_Amount > 0 ? String.Format("{0:" + soThapPhan + "}", _CongPhatSinh.Thu_Amount) : string.Empty)
                             .Replace("{{SR_PSC_NGOAI_TE}}", _CongPhatSinh.ArisingCredit_Foreign > 0 ? String.Format("{0:" + soThapPhan + "}", _CongPhatSinh.ArisingCredit_Foreign) : string.Empty)
                             .Replace("{{SR_PSC_VND}}", _CongPhatSinh.Chi_Amount > 0 ? String.Format("{0:" + soThapPhan + "}", _CongPhatSinh.Chi_Amount) : string.Empty)
                             .Replace("{{SR_SD_NGOAI_TE}}", String.Format("{0:" + soThapPhan + "}", string.Empty))
                             .Replace("{{SR_SD_VND}}", String.Format("{0:" + soThapPhan + "}", string.Empty))

                             .Replace("{{SR_LK_PSN_NGOAI_TE}}", _LuyKe.ArisingDebit_Foreign > 0 ? String.Format("{0:" + soThapPhan + "}", _LuyKe.ArisingDebit_Foreign) : string.Empty)
                             .Replace("{{SR_LK_PSN_VND}}", _LuyKe.Thu_Amount > 0 ? String.Format("{0:" + soThapPhan + "}", _LuyKe.Thu_Amount) : string.Empty)
                             .Replace("{{SR_LK_PSC_NGOAI_TE}}", _LuyKe.ArisingCredit_Foreign > 0 ? String.Format("{0:" + soThapPhan + "}", _LuyKe.ArisingCredit_Foreign) : string.Empty)
                             .Replace("{{SR_LK_PSC_VND}}", _LuyKe.Chi_Amount > 0 ? String.Format("{0:" + soThapPhan + "}", _LuyKe.Chi_Amount) : string.Empty)
                             .Replace("{{SR_LK_SD_NGOAI_TE}}", String.Format("{0:" + soThapPhan + "}", string.Empty))
                             .Replace("{{SR_LK_SD_VND}}", String.Format("{0:" + soThapPhan + "}", string.Empty))

                             .Replace("{{SR_DC_PSN_NGOAI_TE}}", String.Format("{0:" + soThapPhan + "}", string.Empty))
                             .Replace("{{SR_DC_PSN_VND}}", String.Format("{0:" + soThapPhan + "}", string.Empty))
                             .Replace("{{SR_DC_PSC_NGOAI_TE}}", String.Format("{0:" + soThapPhan + "}", string.Empty))
                             .Replace("{{SR_DC_PSC_VND}}", String.Format("{0:" + soThapPhan + "}", string.Empty))
                             .Replace("{{SR_DC_SD_NGOAI_TE}}", _Du.ResidualAmount_Foreign < 0 ? "(" + String.Format("{0:" + soThapPhan + "}", Math.Abs(_Du.ResidualAmount_Foreign)) + ")" : String.Format("{0:" + soThapPhan + "}", _Du.ResidualAmount_Foreign))
                             .Replace("{{SR_DC_SD_VND}}", _Du.ResidualAmount_OrginalCur < 0 ? "(" + String.Format("{0:" + soThapPhan + "}", Math.Abs(_Du.ResidualAmount_OrginalCur)) + ")" : String.Format("{0:" + soThapPhan + "}", _Du.ResidualAmount_OrginalCur))

                            ;

                            resultHTML += _sumRowMonthHTML;
                        }
                    }
                });
            }

            _allText = _allText.Replace("##REPLACE_PLACE##", resultHTML);

            return _allText;
        }
        catch
        {
            return string.Empty;
        }
    }

    private string ConvertToHTML_SoChiTiet_Loai_5(LedgerReportModel p, LedgerReportParamDetail param, double openingStock, int year)
    {
        try
        {
            if (p == null) return string.Empty;
            if (param.FromMonth == null) param.FromMonth = 0;
            if (param.ToMonth == null) param.ToMonth = 0;
            if (param.FromDate == null) param.FromDate = DateTime.Now;
            if (param.ToDate == null) param.ToDate = DateTime.Now;

            string _template = "SoChiTiet_FiveTemplate.html",
                _folderPath = @"Uploads\Html",
                path = Path.Combine(Directory.GetCurrentDirectory(), _folderPath, _template),
                _allText = System.IO.File.ReadAllText(path), resultHTML = string.Empty;
            IDictionary<string, string> v_dicFixed = new Dictionary<string, string>
            {
                { "TIEU_DE","SỔ CHI TIẾT "+ p.AccountName.ToUpper() },
                { "TenCongTy", p.Company },
                { "DiaChi", p.Address },
                { "MST", p.TaxId },
                { "NgayChungTu", string.Empty },
                { "TaiKhoanAccount", p.AccountCode+": "+p.AccountName },
                { "TuThang", param.FromDate.Value.Month.ToString("D2")   },
                { "DenThang", param.ToDate.Value.Month.ToString("D2") },
                { "Nam",  year.ToString("D4") },
                { "NguoiLap", !string.IsNullOrEmpty(param.LedgerReportMaker) ? param.LedgerReportMaker : string.Empty },
                { "Ngay", (param.ToDate.Value.Day).ToString("D2") },
                { "Thang",  (param.ToDate.Value.Month).ToString("D2") },
                { "NamSign",   year.ToString() },
                { "KeToanTruong", param.isCheckName ? p.ChiefAccountantName : string.Empty },
                { "GiamDoc", param.isCheckName ? p.CEOName : string.Empty },
                {"LUY_KE_DAU_NAM", openingStock > 0 ? String.Format("{0:N0}", openingStock) : string.Empty },
                { "KeToanTruong_CV", p.ChiefAccountantNote},
                            { "GiamDoc_CV",  p.CEONote},
            };
            string soThapPhan = "N" + p.MethodCalcExportPrice;
            v_dicFixed.Keys.ToList().ForEach(x => _allText = _allText.Replace("{{{" + x + "}}}", v_dicFixed[x]));
            int _ind = -1;

            resultHTML = @"<tr class='font-b'>
                                                                <td colspan='6'>Lũy kế đầu năm</td>
                                                                <td></td>
                                                                <td  colspan='3' class='txt-right'>{{LUY_KE_DAU_NAM}}</td>
                                                            </tr>";
            resultHTML = resultHTML.Replace("{{LUY_KE_DAU_NAM}}", openingStock > 0 ? String.Format("{0:N0}", openingStock) : string.Empty)
                                                     .Replace("{{LUY_KE_DAU_NAM}}", openingStock > 0 ? String.Format("{0:N0}", openingStock) : string.Empty);

            if (p.BookDetails.Count > 0 && param.BookDetailType == 5)
            {
                p.BookDetails.ForEach(x =>
                {
                    _ind++;
                    string _txt = @"<tr>
                                            <td>{{{NGAY_GHI_SO}}}</td>
                                    <td><a href='{{{URL_LINK}}}#{{{FILTER_TYPE}}}#{{{FILTER_TEXT}}}#{{{FILTER_MONTH}}}#{{{FILTER_ISINTERNAL}}}' target='_blank'>{{{CHUNG_TU_SO}}}</a></td>

                                            <td>{{{CHUNG_TU_NGAY}}}</td>
                                            <td>{{{NGUOI_NOP}}}</td>
                                            <td class='tbl-td-diengiai' colspan='2'>{{{DIEN_GIAI}}}</td>
                                            <td>{{{TK_DOI_UNG}}}</td>
                                            <td class='txt-right'>{{{THU}}}</td>
                                            <td class='txt-right'>{{{CHI}}}</td>
                                            <td class='txt-right'>{{{TON}}}</td>
                                        </tr>";

                    _txt = _txt.Replace("{{{NGAY_GHI_SO}}}", x.OrginalBookDate.HasValue ? x.OrginalBookDate.Value.ToString("dd/MM/yyyy") : string.Empty)
                    .Replace("{{{URL_LINK}}}", UrlLinkConst.UrlLinkArise)
                                        .Replace("{{{CHUNG_TU_SO}}}", x.OrginalVoucherNumber)
                                        .Replace("{{{CHUNG_TU_NGAY}}}", x.OrginalBookDate.HasValue ? x.OrginalBookDate.Value.ToString("dd/MM/yyyy") : string.Empty)
                                        .Replace("{{{NGUOI_NOP}}}", x.NameOfPerson)
                                        .Replace("{{{DIEN_GIAI}}}", x.Description)
                                        .Replace("{{{TK_DOI_UNG}}}", x.IsDebit ? x.CreditCode : x.DebitCode)

                                        .Replace("{{{THU}}}", x.IsDebit ? String.Format("{0:" + soThapPhan + "}", x.Thu_Amount) : string.Empty)
                                        .Replace("{{{CHI}}}", !x.IsDebit ? String.Format("{0:" + soThapPhan + "}", x.Chi_Amount) : string.Empty)
                                        .Replace("{{{TON}}}", x.Residual_Amount < 0 ? "(" + String.Format("{0:" + soThapPhan + "}", Math.Abs(x.Residual_Amount)) + ")" : String.Format("{0:" + soThapPhan + "}", Math.Abs(x.Residual_Amount)))

                                         .Replace("{{{FILTER_TEXT}}}", x.OrginalVoucherNumber)
                                        .Replace("{{{FILTER_TYPE}}}", x.VoucherNumber.Split('/')[1])
                               .Replace("{{{FILTER_MONTH}}}", x.Month.ToString())
                                                                      .Replace("{{{FILTER_ISINTERNAL}}}", (param.IsNoiBo ? "3" : "1"));

                    resultHTML += _txt;

                    if (!param.IsNewReport)
                    {
                        SoChiTietViewModel _sct = p.BookDetails[_ind + 1 < p.BookDetails.Count ? _ind + 1 : p.BookDetails.Count - 1];
                        if (((_sct.Month + "" + _sct.Year) != (x.Month + "" + x.Year) && _ind < p.BookDetails.Count) || (_ind == p.BookDetails.Count - 1) && p.SumItem_SCT_ThuChi.ContainsKey(x.Temp))
                        {
                            List<SoChiTietThuChiViewModel> _ledgerSum = p.SumItem_SCT_ThuChi[x.Temp];

                            SoChiTietThuChiViewModel _CongPhatSinh = _ledgerSum.FirstOrDefault(x => x.Month == -1) ?? new SoChiTietThuChiViewModel();
                            SoChiTietThuChiViewModel _LuyKe = _ledgerSum.FirstOrDefault(x => x.Month == -2) ?? new SoChiTietThuChiViewModel();
                            SoChiTietThuChiViewModel _Du = _ledgerSum.FirstOrDefault(x => x.Month == -3) ?? new SoChiTietThuChiViewModel();

                            string _sumRowMonthHTML = @"
<tr class='font-b'>
                                                                <td colspan='6'>Cộng phát sinh tháng {{THANG_ROW}}</td>
                                                                <td></td>
                                                                <td class='txt-right'>{{SR_THU}}</td>
                                                                <td class='txt-right'>{{SR_CHI}}</td>
                                                                <td class='txt-right'>{{SR_TON}}</td>
                                                            </tr>
			                                                <tr class='font-b'>
                                                                <td colspan='6'>Lũy kế phát sinh đến cuối kỳ</td>
                                                                <td></td>
                                                                <td class='txt-right'>{{SR_LK_THU}}</td>
                                                                <td class='txt-right'>{{SR_LK_CHI}}</td>
                                                                <td class='txt-right'>{{SR_LK_TON}}</td>
                                                            </tr>
                                                            <tr class='font-b' style='display:none;'>
                                                                <td colspan='6'>Dư cuối {{THANG_ROW}}</td>
                                                                <td></td>
                                                                <td class='txt-right'>{{SR_DC_THU}}</td>
                                                                <td class='txt-right'>{{SR_DC_CHI}}</td>
                                                                <td class='txt-right'>{{SR_DC_TON}}</td>
                                                            </tr>";

                            _sumRowMonthHTML = _sumRowMonthHTML.Replace("{{THANG_ROW}}", x.Month.ToString("00") + "/" + x.Year.ToString())

                             .Replace("{{SR_THU}}", _CongPhatSinh.Thu_Amount > 0 ? String.Format("{0:" + soThapPhan + "}", _CongPhatSinh.Thu_Amount) : string.Empty)
                             .Replace("{{SR_CHI}}", _CongPhatSinh.Chi_Amount > 0 ? String.Format("{0:" + soThapPhan + "}", _CongPhatSinh.Chi_Amount) : string.Empty)
                             .Replace("{{SR_TON}}", _CongPhatSinh.Residual_Amount > 0 ? String.Format("{0:" + soThapPhan + "}", _CongPhatSinh.Residual_Amount) : string.Empty)

                             .Replace("{{SR_LK_THU}}", _LuyKe.Thu_Amount > 0 ? String.Format("{0:" + soThapPhan + "}", _LuyKe.Thu_Amount) : string.Empty)
                             .Replace("{{SR_LK_CHI}}", _LuyKe.Chi_Amount > 0 ? String.Format("{0:" + soThapPhan + "}", _LuyKe.Chi_Amount) : string.Empty)
                             .Replace("{{SR_LK_TON}}", _LuyKe.Residual_Amount > 0 ? String.Format("{0:" + soThapPhan + "}", _LuyKe.Residual_Amount) : string.Empty)

                             .Replace("{{SR_DC_THU}}", _Du.Thu_Amount < 0 ? "(" + String.Format("{0:" + soThapPhan + "}", Math.Abs(_Du.Thu_Amount)) + ")" : String.Format("{0:" + soThapPhan + "}", _Du.Thu_Amount))
                             .Replace("{{SR_DC_CHI}}", _Du.Chi_Amount < 0 ? "(" + String.Format("{0:" + soThapPhan + "}", Math.Abs(_Du.Chi_Amount)) + ")" : String.Format("{0:" + soThapPhan + "}", _Du.Chi_Amount))
                             .Replace("{{SR_DC_TON}}", _Du.Residual_Amount < 0 ? "(" + String.Format("{0:" + soThapPhan + "}", Math.Abs(_Du.Residual_Amount)) + ")" : String.Format("{0:" + soThapPhan + "}", _Du.Residual_Amount))

                            ;

                            resultHTML += _sumRowMonthHTML;
                        }
                    }
                });
            }

            _allText = _allText.Replace("##REPLACE_PLACE##", resultHTML);

            return _allText;
        }
        catch
        {
            return string.Empty;
        }
    }

    private string ConvertToHTML_SoChiTiet_Loai_6(LedgerReportModel p, LedgerReportParamDetail param, int year)
    {
        try
        {
            if (p == null) return string.Empty;

            string _template = "SoChiTiet_SixTemplate.html",
                _folderPath = @"Uploads\Html",
                path = Path.Combine(Directory.GetCurrentDirectory(), _folderPath, _template),
                _allText = System.IO.File.ReadAllText(path), resultHTML = string.Empty;
            IDictionary<string, string> v_dicFixed = new Dictionary<string, string>
            {
                { "TIEU_DE","SỔ CHI TIẾT "+ p.AccountName.ToUpper() },
                { "TenCongTy", p.Company },
                { "DiaChi", p.Address },
                { "MST", p.TaxId },
                { "NgayChungTu", string.Empty },
                { "TaiKhoanAccount", (p.AccountCode ?? "" ) +": "+ (p.AccountName  ?? "" )},
                { "TuThang", param.FromDate.Value.Month.ToString("D2")   },
                { "DenThang", param.ToDate.Value.Month.ToString("D2") },
                { "Nam", year.ToString("D4") },
                { "NguoiLap", !string.IsNullOrEmpty(param.LedgerReportMaker) ? param.LedgerReportMaker : string.Empty },
                { "Ngay", (param.ToDate.Value.Day).ToString("D2") },
                { "Thang",  (param.ToDate.Value.Month).ToString("D2") },
                { "NamSign",   year.ToString() },
                { "KeToanTruong", param.isCheckName ? p.ChiefAccountantName : string.Empty },
                { "GiamDoc", param.isCheckName ? p.CEOName : string.Empty },
                { "KeToanTruong_CV", p.ChiefAccountantNote},
                            { "GiamDoc_CV",  p.CEONote},
            };

            string soThapPhan = "N" + p.MethodCalcExportPrice;
            v_dicFixed.Keys.ToList().ForEach(x => _allText = _allText.Replace("{{{" + x + "}}}", v_dicFixed[x]));
            int _ind = 0;
            if (p.ItemSLTons.Count > 0)
            {
                p.ItemSLTons.ForEach(x =>
                {
                    _ind++;
                    string _txt = @"<tr>
                                            <td>{{{STT}}}</td>
                                            <td>{{{KHO}}}</td>
                                            <td>{{{MA_HANG}}}</td>
                                            <td>{{{TEN_HANG}}}</td>
                                            <td class='txt-right'>{{{OPEN_SL}}}</td>
                                            <td class='txt-right'>{{{IP_SL}}}</td>
                                            <td class='txt-right'>{{{OP_SL}}}</td>
                                            <td class='txt-right'>{{{LEFT_SL}}}</td>
                                        </tr>";
                    x.CloseQuantity = x.OpenQuantity + x.InputQuantity - x.OutputQuantity;
                    _txt = _txt.Replace("{{{STT}}}", _ind.ToString())
                                        .Replace("{{{KHO}}}", x.Warehouse)
                                        .Replace("{{{MA_HANG}}}", string.IsNullOrEmpty(x.Detail2) ? x.Detail1 : x.Detail2)
                                        .Replace("{{{TEN_HANG}}}", x.NameGood)
                                        .Replace("{{{OPEN_SL}}}", x.OpenQuantity > 0 ? String.Format("{0:" + soThapPhan + "}", x.OpenQuantity) : String.Empty)
                                        .Replace("{{{IP_SL}}}", x.InputQuantity > 0 ? String.Format("{0:" + soThapPhan + "}", x.InputQuantity) : String.Empty)
                                        .Replace("{{{OP_SL}}}", x.OutputQuantity > 0 ? String.Format("{0:" + soThapPhan + "}", x.OutputQuantity) : String.Empty)
                                        .Replace("{{{LEFT_SL}}}", x.CloseQuantity < 0 ? "(" + String.Format("{0:" + soThapPhan + "}", Math.Abs(x.CloseQuantity)) + ")" : String.Format("{0:" + soThapPhan + "}", x.CloseQuantity))
                                        ;

                    resultHTML += _txt;
                });
            }
            _allText = _allText.Replace("##REPLACE_PLACE##", resultHTML);

            return _allText;
        }
        catch
        {
            return string.Empty;
        }
    }

    private string ConvertToHTML_SoChiTiet_Loai_4(LedgerReportModel p, LedgerReportParamDetail param, int year)
    {
        try
        {
            if (p == null) return string.Empty;

            StringBuilder _allTextFull = new StringBuilder();

            if (p.BookDetails.Count > 0 && param.BookDetailType == 4)
            {
                foreach (var item in p.listAccoutCodeThuChi)
                {
                    string[] listKey = item.Key.Split('-');
                    string detail1 = listKey[0];
                    string detail2 = listKey[1];
                    string warehouseCode = listKey[2];
                    int _ind = -1;
                    string _template = "SoChiTiet_FourTemplate.html",
                    _folderPath = @"Uploads\Html",
                    path = Path.Combine(Directory.GetCurrentDirectory(), _folderPath, _template);
                    StringBuilder resultHTML = new StringBuilder();
                    string _allText = System.IO.File.ReadAllText(path);
                    IDictionary<string, string> v_dicFixed = new Dictionary<string, string>
                        {
                            { "TIEU_DE","SỔ CHI TIẾT "+ p.AccountName.ToUpper() },
                            { "TenCongTy", p.Company },
                            { "DiaChi", p.Address },
                            { "MST", p.TaxId },
                            { "NgayChungTu", string.Empty },
                            { "TaiKhoanAccount", param.AccountCodeDetail1 + " - " + (string.IsNullOrEmpty(detail2) ? detail1 : detail2) + (string.IsNullOrEmpty(warehouseCode) ? "" : (" - " + warehouseCode))},
                            { "TuThang", param.FromDate.Value.Month.ToString("D2")   },
                            { "DenThang", param.ToDate.Value.Month.ToString("D2") },
                            { "Nam", year.ToString("D4") },
                            { "NguoiLap", !string.IsNullOrEmpty(param.LedgerReportMaker) ? param.LedgerReportMaker : string.Empty },
                            { "Ngay", (param.ToDate.Value.Day).ToString("D2") },
                            { "Thang",  (param.ToDate.Value.Month).ToString("D2") },
                            { "NamSign",   year.ToString() },
                            { "KeToanTruong", param.isCheckName ? p.ChiefAccountantName : string.Empty },
                            { "GiamDoc", param.isCheckName ? p.CEOName : string.Empty },
                            {"LUY_KE_DAU_NAM", listKey[3] + " - " +  listKey[4]},
                            { "KeToanTruong_CV", p.ChiefAccountantNote},
                            { "GiamDoc_CV",  p.CEONote},
                        };
                    v_dicFixed.Keys.ToList().ForEach(x => _allText = _allText.Replace("{{{" + x + "}}}", v_dicFixed[x]));
                    string soThapPhan = "N" + p.MethodCalcExportPrice;
                    var listBookDetails = p.BookDetails.Where(x => string.IsNullOrEmpty(detail1) || (x.DebitDetailCodeFirst == detail1 || x.CreditDetailCodeFirst == detail1)
                                    && string.IsNullOrEmpty(detail2) || (x.DebitDetailCodeSecond == detail2 || x.CreditDetailCodeSecond == detail2)
                                    && string.IsNullOrEmpty(warehouseCode) || (x.CreditWarehouseCode == warehouseCode || x.DebitWarehouseCode == warehouseCode)
                                    ).ToList();
                    string luyKeDauNam = @"<tr class='font-b'>
<td colspan='3'></td>
                                                                <td colspan='2'>Lũy kế đầu năm</td>
                                                                <td></td>
                                                                <td></td>
<td></td>
                                                                <td  colspan='6' class='txt-right'>{{LUY_KE_DAU_NAM}}</td>
                                                            </tr>";
                    luyKeDauNam = luyKeDauNam.Replace("{{LUY_KE_DAU_NAM}}", listKey[3]);
                    resultHTML.Append(luyKeDauNam);

                    listBookDetails.ForEach(x =>
                    {
                        _ind++;
                        string _txt = @"<tr>
                                            <td>{{{NGAY_GHI_SO}}}</td>
                                    <td><a href='{{{URL_LINK}}}#{{{FILTER_TYPE}}}#{{{FILTER_TEXT}}}#{{{FILTER_MONTH}}}#{{{FILTER_ISINTERNAL}}}' target='_blank'>{{{CHUNG_TU_SO}}}</a></td>

                                            <td>{{{CHUNG_TU_NGAY}}}</td>
                                            <td class='tbl-td-diengiai' colspan='2'>{{{DIEN_GIAI}}}</td>
                                            <td>{{{TK_DOI_UNG}}}</td>
                                            <td>{{{CHI_TIET}}}</td>
                                            <td class='txt-right'>{{{DON_GIA}}}</td>
                                            <td class='txt-right'>{{{IP_SL}}}</td>
                                            <td class='txt-right'>{{{IP_MONEY}}}</td>
                                            <td class='txt-right'>{{{OP_SL}}}</td>
                                            <td class='txt-right'>{{{OP_MONEY}}}</td>
                                            <td class='txt-right'>{{{LEFT_SL}}}</td>
                                            <td class='txt-right'>{{{LEFT_MONEY}}}</td>
                                        </tr>";

                        _txt = _txt.Replace("{{{NGAY_GHI_SO}}}", x.OrginalBookDate.HasValue ? x.OrginalBookDate.Value.ToString("dd/MM/yyyy") : string.Empty)
                        .Replace("{{{URL_LINK}}}", UrlLinkConst.UrlLinkArise)
                                            .Replace("{{{CHUNG_TU_SO}}}", x.OrginalVoucherNumber)
                                            .Replace("{{{CHUNG_TU_NGAY}}}", x.OrginalBookDate.HasValue ? x.OrginalBookDate.Value.ToString("dd/MM/yyyy") : string.Empty)
                                            .Replace("{{{DIEN_GIAI}}}", string.IsNullOrEmpty(detail2) ? detail1 : detail2)
                                            .Replace("{{{TK_DOI_UNG}}}", x.IsDebit ? x.CreditCode : x.DebitCode)
                                            .Replace("{{{CHI_TIET}}}", x.DetailCode)

                                            .Replace("{{{DON_GIA}}}", String.Format("{0:" + soThapPhan + "}", x.UnitPrice))
                                            .Replace("{{{IP_SL}}}", x.IsDebit ? String.Format("{0:" + soThapPhan + "}", x.Quantity) : string.Empty)
                                            .Replace("{{{IP_MONEY}}}", x.IsDebit ? String.Format("{0:" + soThapPhan + "}", x.Amount) : string.Empty)
                                            .Replace("{{{OP_SL}}}", !x.IsDebit ? String.Format("{0:" + soThapPhan + "}", x.Quantity) : string.Empty)
                                            .Replace("{{{OP_MONEY}}}", !x.IsDebit ? String.Format("{0:" + soThapPhan + "}", x.Amount) : string.Empty)
                                            .Replace("{{{LEFT_SL}}}", x.Residual_Quantity < 0 ? "(" + String.Format("{0:" + soThapPhan + "}", Math.Abs(x.Residual_Quantity)) + ")" : String.Format("{0:" + soThapPhan + "}", x.Residual_Quantity))
                                            .Replace("{{{LEFT_MONEY}}}", x.Residual_Amount < 0 ? "(" + String.Format("{0:" + soThapPhan + "}", Math.Abs(x.Residual_Amount)) + ")" : String.Format("{0:" + soThapPhan + "}", x.Residual_Amount))

                                                                                   .Replace("{{{FILTER_ISINTERNAL}}}", (param.IsNoiBo ? "3" : "1"))

                                        .Replace("{{{FILTER_TYPE}}}", x.VoucherNumber.Split('/')[1])
                               .Replace("{{{FILTER_MONTH}}}", x.Month.ToString())
                               .Replace("{{{FILTER_ISINTERNAL}}}", (param.IsNoiBo ? "3" : "1"))
                               .Replace("{{{FILTER_TEXT}}}", x.OrginalVoucherNumber);

                        resultHTML.Append(_txt);

                        SoChiTietViewModel _sct = listBookDetails[_ind + 1 < listBookDetails.Count ? _ind + 1 : listBookDetails.Count - 1];
                        if (!param.IsNewReport && (_sct.Month != x.Month && _ind < listBookDetails.Count) || (_ind == listBookDetails.Count - 1))
                        {
                            List<SoChiTietThuChiViewModel> _ledgerSum = item.Value.Where(y => y.Month == x.Month).ToList();

                            SoChiTietThuChiViewModel _CongPhatSinh = _ledgerSum.FirstOrDefault(y => y.Type == 1);
                            if (_CongPhatSinh == null)
                                _CongPhatSinh = new SoChiTietThuChiViewModel();
                            SoChiTietThuChiViewModel _LuyKe = _ledgerSum.FirstOrDefault(y => y.Type == 2);
                            if (_LuyKe == null)
                                _LuyKe = new SoChiTietThuChiViewModel();
                            SoChiTietThuChiViewModel _Du = _ledgerSum.FirstOrDefault(y => y.Type == 3);
                            if (_Du == null)
                                _Du = new SoChiTietThuChiViewModel();
                            string _sumRowMonthHTML = @"
<tr class='font-b'>
                                                                <td colspan='3'></td>
                                                                <td colspan='2'>Cộng phát sinh tháng {{THANG_ROW}}</td>
                                                                <td></td>
                                                                <td></td>
                                                                <td></td>
                                                                <td class='txt-right'>{{SR_IP_SL}}</td>
                                                                <td class='txt-right'>{{SR_IP_TIEN}}</td>
                                                                <td class='txt-right'>{{SR_OP_SL}}</td>
                                                                <td class='txt-right'>{{SR_OP_TIEN}}</td>
                                                                <td class='txt-right'>{{SR_LEFT_SL}}</td>
                                                                <td class='txt-right'>{{SR_LEFT_TIEN}}</td>
                                                            </tr>
			                                                <tr class='font-b'>
                                                                <td colspan='3'></td>
                                                                <td colspan='2'>Lũy kế phát sinh đến cuối kỳ</td>
                                                                <td></td>
                                                                <td></td>
                                                                <td></td>
                                                                <td class='txt-right'>{{SR_LK_IP_SL}}</td>
                                                                <td class='txt-right'>{{SR_LK_IP_TIEN}}</td>
                                                                <td class='txt-right'>{{SR_LK_OP_SL}}</td>
                                                                <td class='txt-right'>{{SR_LK_OP_TIEN}}</td>
                                                                <td class='txt-right'>{{SR_LK_LEFT_SL}}</td>
                                                                <td class='txt-right'>{{SR_LK_LEFT_TIEN}}</td>
                                                            </tr>
                                                            <tr class='font-b'>
                                                                <td colspan='3'></td>
                                                                <td colspan='2'>Dư cuối {{THANG_ROW}}</td>
                                                                <td></td>
                                                                <td></td>
                                                                <td class='txt-right'>{{SR_DC_IP_DG}}</td>
                                                                <td class='txt-right'>{{SR_DC_IP_SL}}</td>
                                                                <td class='txt-right'>{{SR_DC_IP_TIEN}}</td>
                                                                <td class='txt-right'>{{SR_DC_OP_SL}}</td>
                                                                <td class='txt-right'>{{SR_DC_OP_TIEN}}</td>
                                                                <td class='txt-right'>{{SR_DC_LEFT_SL}}</td>
                                                                <td class='txt-right'>{{SR_DC_LEFT_TIEN}}</td>
                                                            </tr>";

                            double SR_DC_IP_DG = 0;
                            if (_Du.Residual_Quantity > 0)
                                SR_DC_IP_DG = Math.Abs(_Du.Residual_Amount) / Math.Abs(_Du.Residual_Quantity);
                            _sumRowMonthHTML = _sumRowMonthHTML.Replace("{{THANG_ROW}}", x.Month.ToString("00") + "/" + x.Year.ToString())

                             .Replace("{{SR_IP_SL}}", _CongPhatSinh.Input_Quantity > 0 ? String.Format("{0:" + soThapPhan + "}", _CongPhatSinh.Input_Quantity) : string.Empty)
                             .Replace("{{SR_IP_TIEN}}", _CongPhatSinh.Thu_Amount > 0 ? String.Format("{0:" + soThapPhan + "}", _CongPhatSinh.Thu_Amount) : string.Empty)
                             .Replace("{{SR_OP_SL}}", _CongPhatSinh.Output_Quantity > 0 ? String.Format("{0:" + soThapPhan + "}", _CongPhatSinh.Output_Quantity) : string.Empty)
                             .Replace("{{SR_OP_TIEN}}", _CongPhatSinh.Chi_Amount > 0 ? String.Format("{0:" + soThapPhan + "}", _CongPhatSinh.Chi_Amount) : string.Empty)
                             .Replace("{{SR_LEFT_SL}}", String.Format("{0:" + soThapPhan + "}", string.Empty))
                             .Replace("{{SR_LEFT_TIEN}}", String.Format("{0:" + soThapPhan + "}", string.Empty))

                             .Replace("{{SR_LK_IP_SL}}", _LuyKe.Input_Quantity > 0 ? String.Format("{0:" + soThapPhan + "}", _LuyKe.Input_Quantity) : string.Empty)
                             .Replace("{{SR_LK_IP_TIEN}}", _LuyKe.Thu_Amount > 0 ? String.Format("{0:" + soThapPhan + "}", _LuyKe.Thu_Amount) : string.Empty)
                             .Replace("{{SR_LK_OP_SL}}", _LuyKe.Output_Quantity > 0 ? String.Format("{0:" + soThapPhan + "}", _LuyKe.Output_Quantity) : string.Empty)
                             .Replace("{{SR_LK_OP_TIEN}}", _LuyKe.Chi_Amount > 0 ? String.Format("{0:" + soThapPhan + "}", _LuyKe.Chi_Amount) : string.Empty)
                             .Replace("{{SR_LK_LEFT_SL}}", String.Format("{0:" + soThapPhan + "}", string.Empty))
                             .Replace("{{SR_LK_LEFT_TIEN}}", String.Format("{0:" + soThapPhan + "}", string.Empty))

                             .Replace("{{SR_DC_IP_DG}}", SR_DC_IP_DG > 0 ? String.Format("{0:" + soThapPhan + "}", SR_DC_IP_DG) : string.Empty)
                             .Replace("{{SR_DC_IP_SL}}", String.Format("{0:" + soThapPhan + "}", string.Empty))
                             .Replace("{{SR_DC_IP_TIEN}}", String.Format("{0:" + soThapPhan + "}", string.Empty))
                             .Replace("{{SR_DC_OP_SL}}", String.Format("{0:" + soThapPhan + "}", string.Empty))
                             .Replace("{{SR_DC_OP_TIEN}}", String.Format("{0:" + soThapPhan + "}", string.Empty))
                             .Replace("{{SR_DC_LEFT_SL}}", _Du.Residual_Quantity < 0 ? "(" + String.Format("{0:" + soThapPhan + "}", Math.Abs(_Du.Residual_Quantity)) + ")" : String.Format("{0:" + soThapPhan + "}", _Du.Residual_Quantity))
                             .Replace("{{SR_DC_LEFT_TIEN}}", _Du.Residual_Amount < 0 ? "(" + String.Format("{0:" + soThapPhan + "}", Math.Abs(_Du.Residual_Amount)) + ")" : String.Format("{0:" + soThapPhan + "}", _Du.Residual_Amount))
                            ;
                            resultHTML.Append(_sumRowMonthHTML);
                        }
                    });
                    _allTextFull.Append(_allText.Replace("##REPLACE_PLACE##", resultHTML.ToString()));
                }
            }

            string _template1 = "SoChiTiet_FourTotalTemplate.html",
               _folderPath1 = @"Uploads\Html",
               path1 = Path.Combine(Directory.GetCurrentDirectory(), _folderPath1, _template1),
               _allText1 = System.IO.File.ReadAllText(path1);
            return _allText1.Replace("##REPLACE_PLACE_BODY##", _allTextFull.ToString());
        }
        catch
        {
            return string.Empty;
        }
    }

    private string ExportExcel_Report_SoChiTiet_Loai_1_And_2(LedgerReportModel p, LedgerReportParam param, double openingStock)
    {
        try
        {
            if (p == null) return string.Empty;
            if (param.FromMonth == null) param.FromMonth = 0;
            if (param.ToMonth == null) param.ToMonth = 0;
            if (param.FromDate == null) param.FromDate = DateTime.Now;
            if (param.ToDate == null) param.ToDate = DateTime.Now;
            string sTenFile = "BaoCaoChiTiet1.xlsx";
            int nCol = 9;
            if (param.BookDetailType == (int)ReportBookDetailTypeEnum.soCoDu_2_ben)
            {
                nCol = 10;
                sTenFile = "BaoCaoChiTiet2.xlsx";
            }
            else if (param.BookDetailType == (int)ReportBookDetailTypeEnum.soCoNgoaiTe)
            {
                nCol = 13;
                sTenFile = "BaoCaoChiTiet3.xlsx";
            }
            else if (param.BookDetailType == (int)ReportBookDetailTypeEnum.soCoHangTonKho)
            {
                nCol = 13;
                sTenFile = "BaoCaoChiTiet4.xlsx";
            }
            else if (param.BookDetailType == (int)ReportBookDetailTypeEnum.soQuy)
            {
                nCol = 9;
                sTenFile = "BaoCaoChiTiet5.xlsx";
            }
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Uploads/Excel/" + sTenFile);
            using (FileStream templateDocumentStream = System.IO.File.OpenRead(path))
            {
                using (ExcelPackage package = new ExcelPackage(templateDocumentStream))
                {
                    ExcelWorksheet worksheet = package.Workbook.Worksheets[0];
                    worksheet.Cells["A1"].Value = p.Company;
                    worksheet.Cells["A2"].Value = p.Address;
                    worksheet.Cells["A3"].Value = p.TaxId;

                    worksheet.Cells["A5"].Value = "SỔ CHI TIẾT";
                    worksheet.Cells["A6"].Value = "Từ tháng ... đến tháng ... năm ... ";

                    worksheet.Cells["A7"].Value = "Tài khoản: " + param.AccountCode + ": " + p.AccountCode + " - " + p.AccountName;

                    worksheet.Cells["A8"].Value = "Đơn vị tính: Đồng";

                    worksheet.Cells["F8"].Value = "Lũy kế đầu năm: " + openingStock.ToString("#,##0;");
                    int currentRowNo = 10, flagRowNo = 0, nRowBegin = 10;

                    if (param.BookDetailType == (int)ReportBookDetailTypeEnum.soCoNgoaiTe)
                        ImportDataToExcel_Loai3(worksheet, p, ref currentRowNo);
                    else if (param.BookDetailType == (int)ReportBookDetailTypeEnum.soQuy)
                        ImportDataToExcel_Loai5(worksheet, p, ref currentRowNo);
                    else
                        ImportDataToExcel(worksheet, p, param.BookDetailType, ref currentRowNo);

                    flagRowNo = currentRowNo;

                    IDictionary<string, string> v_dicFixed = new Dictionary<string, string>
                    {
                        { "TIEU_DE","SỔ CHI TIẾT "+ p.AccountName.ToUpper() },
                        { "TenCongTy", p.Company },
                        { "DiaChi", p.Address },
                        { "MST", p.TaxId },
                        { "NgayChungTu", string.Empty },
                        { "TaiKhoanAccount", p.AccountCode+" - "+p.AccountName },
                        { "TuThang", ((param.FromMonth > 0 && param.ToMonth > 0) ? ((int)param.FromMonth) : ((DateTime)param.FromDate).Month ).ToString("D2")   },
                        { "DenThang", ( (param.FromMonth > 0 && param.ToMonth > 0) ? ((int)param.ToMonth) : ((DateTime)param.ToDate).Month ).ToString("D2") },
                        { "Nam", ((param.FromMonth > 0 && param.ToMonth > 0) ? DateTime.Now.Year : ((DateTime)param.FromDate).Year ).ToString("D4") },
                        { "NguoiLap", !string.IsNullOrEmpty(param.LedgerReportMaker) ? param.LedgerReportMaker : string.Empty },
                        { "Ngay", " .... " },
                        { "Thang",  " .... " },
                        { "NamSign",  " .... " },
                        { "KeToanTruong", param.isCheckName ? p.ChiefAccountantName : string.Empty },
                        { "GiamDoc", param.isCheckName ? p.CEOName : string.Empty },
                        { "KeToanTruong_CV", p.ChiefAccountantNote},
                            { "GiamDoc_CV",  p.CEONote},
                    };

                    currentRowNo++;
                    worksheet.Cells[currentRowNo, 6, currentRowNo, 8].Merge = true;
                    worksheet.Cells[currentRowNo, 6, currentRowNo, 8].Value = "Ngày ... tháng ... năm";

                    currentRowNo++;
                    worksheet.Cells[currentRowNo, 1, currentRowNo, 3].Merge = true;
                    worksheet.Cells[currentRowNo, 4, currentRowNo, 5].Merge = true;
                    worksheet.Cells[currentRowNo, 6, currentRowNo, 8].Merge = true;
                    worksheet.Cells[currentRowNo, 1, currentRowNo, 3].Value = "Người ghi sổ";
                    worksheet.Cells[currentRowNo, 4, currentRowNo, 5].Value = v_dicFixed["KeToanTruong_CV"];
                    worksheet.Cells[currentRowNo, 6, currentRowNo, 8].Value = v_dicFixed["GiamDoc_CV"];

                    currentRowNo += 4;
                    worksheet.Cells[currentRowNo, 1, currentRowNo, 3].Merge = true;
                    worksheet.Cells[currentRowNo, 4, currentRowNo, 5].Merge = true;
                    worksheet.Cells[currentRowNo, 6, currentRowNo, 8].Merge = true;
                    worksheet.Cells[currentRowNo, 1, currentRowNo, 3].Value = v_dicFixed["NguoiLap"];
                    worksheet.Cells[currentRowNo, 4, currentRowNo, 5].Value = v_dicFixed["KeToanTruong"];
                    worksheet.Cells[currentRowNo, 6, currentRowNo, 8].Value = v_dicFixed["GiamDoc"];

                    currentRowNo--;
                    if (currentRowNo > 10)
                    {
                        worksheet.Cells[nRowBegin, 7, flagRowNo, nCol].Style.Numberformat.Format = "_(* #,##0_);_(* (#,##0);_(* \"-\"??_);_(@_)";

                        worksheet.Cells[nRowBegin, 1, flagRowNo, nCol].Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                        worksheet.Cells[nRowBegin, 1, flagRowNo, nCol].Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                        worksheet.Cells[nRowBegin, 1, flagRowNo, nCol].Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                        worksheet.Cells[nRowBegin, 1, flagRowNo, nCol].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;

                        worksheet.Cells[nRowBegin, 1, flagRowNo, nCol].Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        worksheet.Cells[nRowBegin, 1, flagRowNo, nCol].Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        worksheet.Cells[nRowBegin, 1, flagRowNo, nCol].Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        worksheet.Cells[nRowBegin, 1, flagRowNo, nCol].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    }

                    return ExcelHelpers.SaveFileExcel(package, Directory.GetCurrentDirectory(), "SoChiTietLoai");
                }
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    private string ExportExcel_Report_SoChiTiet_Loai_6(LedgerReportModel p, LedgerReportParam param)
    {
        try
        {
            if (p == null) return string.Empty;
            if (param.FromMonth == null) param.FromMonth = 0;
            if (param.ToMonth == null) param.ToMonth = 0;
            if (param.FromDate == null) param.FromDate = DateTime.Now;
            if (param.ToDate == null) param.ToDate = DateTime.Now;
            string sTenFile = "BaoCaoChiTiet6.xlsx";
            int nCol = 8;

            string path = Path.Combine(Directory.GetCurrentDirectory(), "Uploads/Excel/" + sTenFile);
            using (FileStream templateDocumentStream = System.IO.File.OpenRead(path))
            {
                using (ExcelPackage package = new ExcelPackage(templateDocumentStream))
                {
                    ExcelWorksheet worksheet = package.Workbook.Worksheets[0];
                    worksheet.Cells["A1"].Value = p.Company;
                    worksheet.Cells["A2"].Value = p.Address;
                    worksheet.Cells["A3"].Value = p.TaxId;

                    worksheet.Cells["A5"].Value = "SỔ CHI TIẾT";
                    worksheet.Cells["A6"].Value = "Từ tháng ... đến tháng ... năm ... ";

                    worksheet.Cells["A7"].Value = "Tài khoản: " + param.AccountCode + ": " + p.AccountCode + " - " + p.AccountName;

                    worksheet.Cells["A8"].Value = "Đơn vị tính: Đồng";

                    int currentRowNo = 10, flagRowNo = 0, nRowBegin = 10;

                    ImportDataToExcel_Loai6(worksheet, p, ref currentRowNo);

                    flagRowNo = currentRowNo;

                    IDictionary<string, string> v_dicFixed = new Dictionary<string, string>
                    {
                        { "TIEU_DE","SỔ CHI TIẾT "+ p.AccountName.ToUpper() },
                        { "TenCongTy", p.Company },
                        { "DiaChi", p.Address },
                        { "MST", p.TaxId },
                        { "NgayChungTu", string.Empty },
                        { "TaiKhoanAccount", p.AccountCode+" - "+p.AccountName },
                        { "TuThang", ((param.FromMonth > 0 && param.ToMonth > 0) ? ((int)param.FromMonth) : ((DateTime)param.FromDate).Month ).ToString("D2")   },
                        { "DenThang", ( (param.FromMonth > 0 && param.ToMonth > 0) ? ((int)param.ToMonth) : ((DateTime)param.ToDate).Month ).ToString("D2") },
                        { "Nam", ((param.FromMonth > 0 && param.ToMonth > 0) ? DateTime.Now.Year : ((DateTime)param.FromDate).Year ).ToString("D4") },
                        { "NguoiLap", !string.IsNullOrEmpty(param.LedgerReportMaker) ? param.LedgerReportMaker : string.Empty },
                        { "Ngay", " .... " },
                        { "Thang",  " .... " },
                        { "NamSign",  " .... " },
                        { "KeToanTruong", param.isCheckName ? p.ChiefAccountantName : string.Empty },
                        { "GiamDoc", param.isCheckName ? p.CEOName : string.Empty },
                        { "KeToanTruong_CV", p.ChiefAccountantNote},
                        { "GiamDoc_CV", p.CEONote},
                    };

                    currentRowNo++;
                    worksheet.Cells[currentRowNo, 4, currentRowNo, nCol].Merge = true;
                    worksheet.Cells[currentRowNo, 4, currentRowNo, nCol].Value = "Ngày ... tháng ... năm";

                    currentRowNo++;
                    worksheet.Cells[currentRowNo, 1, currentRowNo, 2].Merge = true;
                    worksheet.Cells[currentRowNo, 3, currentRowNo, 4].Merge = true;
                    worksheet.Cells[currentRowNo, 5, currentRowNo, 6].Merge = true;
                    worksheet.Cells[currentRowNo, 1].Value = "Người ghi sổ";
                    worksheet.Cells[currentRowNo, 3].Value = v_dicFixed["KeToanTruong_CV"];
                    worksheet.Cells[currentRowNo, 5].Value = v_dicFixed["GiamDoc_CV"];

                    currentRowNo += 4;
                    worksheet.Cells[currentRowNo, 1, currentRowNo, 2].Merge = true;
                    worksheet.Cells[currentRowNo, 3, currentRowNo, 4].Merge = true;
                    worksheet.Cells[currentRowNo, 5, currentRowNo, 6].Merge = true;
                    worksheet.Cells[currentRowNo, 1, currentRowNo, 2].Value = v_dicFixed["NguoiLap"];
                    worksheet.Cells[currentRowNo, 3, currentRowNo, 4].Value = v_dicFixed["KeToanTruong"];
                    worksheet.Cells[currentRowNo, 5, currentRowNo, 6].Value = v_dicFixed["GiamDoc"];

                    currentRowNo--;
                    if (currentRowNo > 10)
                    {
                        worksheet.Cells[nRowBegin, 5, flagRowNo, nCol].Style.Numberformat.Format = "_(* #,##0_);_(* (#,##0);_(* \"-\"??_);_(@_)";

                        worksheet.Cells[nRowBegin, 1, flagRowNo, nCol].Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                        worksheet.Cells[nRowBegin, 1, flagRowNo, nCol].Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                        worksheet.Cells[nRowBegin, 1, flagRowNo, nCol].Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                        worksheet.Cells[nRowBegin, 1, flagRowNo, nCol].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;

                        worksheet.Cells[nRowBegin, 1, flagRowNo, nCol].Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        worksheet.Cells[nRowBegin, 1, flagRowNo, nCol].Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        worksheet.Cells[nRowBegin, 1, flagRowNo, nCol].Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        worksheet.Cells[nRowBegin, 1, flagRowNo, nCol].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    }

                    return ExcelHelpers.SaveFileExcel(package, Directory.GetCurrentDirectory(), "SoChiTietLoai");
                }
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    private string ExportExcel_Report_SoChiTiet_Loai_4(LedgerReportModel p, LedgerReportParam param)
    {
        try
        {
            if (p == null) return string.Empty;
            if (param.FromMonth == null) param.FromMonth = 0;
            if (param.ToMonth == null) param.ToMonth = 0;
            if (param.FromDate == null) param.FromDate = DateTime.Now;
            if (param.ToDate == null) param.ToDate = DateTime.Now;
            string sTenFile = "BaoCaoChiTiet4.xlsx";
            int nCol = 13;

            string path = Path.Combine(Directory.GetCurrentDirectory(), "Uploads/Excel/" + sTenFile);
            using (FileStream templateDocumentStream = System.IO.File.OpenRead(path))
            {
                using (ExcelPackage package = new ExcelPackage(templateDocumentStream))
                {
                    ExcelWorksheet worksheet = package.Workbook.Worksheets[0];
                    worksheet.Cells["A1"].Value = p.Company;
                    worksheet.Cells["A2"].Value = p.Address;
                    worksheet.Cells["A3"].Value = p.TaxId;

                    worksheet.Cells["A5"].Value = "SỔ CHI TIẾT";
                    worksheet.Cells["A6"].Value = "Từ tháng ... đến tháng ... năm ... ";

                    worksheet.Cells["A7"].Value = "Tài khoản: " + param.AccountCode + ": " + p.AccountCode + " - " + p.AccountName;

                    worksheet.Cells["A8"].Value = "Đơn vị tính: Đồng";

                    int currentRowNo = 10, flagRowNo = 0, nRowBegin = 10;

                    ImportDataToExcel_Loai4(worksheet, p, ref currentRowNo);

                    flagRowNo = currentRowNo;

                    IDictionary<string, string> v_dicFixed = new Dictionary<string, string>
                    {
                        { "TIEU_DE","SỔ CHI TIẾT "+ p.AccountName.ToUpper() },
                        { "TenCongTy", p.Company },
                        { "DiaChi", p.Address },
                        { "MST", p.TaxId },
                        { "NgayChungTu", string.Empty },
                        { "TaiKhoanAccount", p.AccountCode+" - "+p.AccountName },
                        { "TuThang", ((param.FromMonth > 0 && param.ToMonth > 0) ? ((int)param.FromMonth) : ((DateTime)param.FromDate).Month ).ToString("D2")   },
                        { "DenThang", ( (param.FromMonth > 0 && param.ToMonth > 0) ? ((int)param.ToMonth) : ((DateTime)param.ToDate).Month ).ToString("D2") },
                        { "Nam", ((param.FromMonth > 0 && param.ToMonth > 0) ? DateTime.Now.Year : ((DateTime)param.FromDate).Year ).ToString("D4") },
                        { "NguoiLap", !string.IsNullOrEmpty(param.LedgerReportMaker) ? param.LedgerReportMaker : string.Empty },
                        { "Ngay", " .... " },
                        { "Thang",  " .... " },
                        { "NamSign",  " .... " },
                        { "KeToanTruong", param.isCheckName ? p.ChiefAccountantName : string.Empty },
                        { "GiamDoc", param.isCheckName ? p.CEOName : string.Empty },
                        { "KeToanTruong_CV", p.ChiefAccountantNote},
                        { "GiamDoc_CV", p.CEONote},
                    };

                    currentRowNo++;
                    worksheet.Cells[currentRowNo, 6, currentRowNo, 8].Merge = true;
                    worksheet.Cells[currentRowNo, 6, currentRowNo, 8].Value = "Ngày ... tháng ... năm";

                    currentRowNo++;
                    worksheet.Cells[currentRowNo, 1, currentRowNo, 3].Merge = true;
                    worksheet.Cells[currentRowNo, 4, currentRowNo, 5].Merge = true;
                    worksheet.Cells[currentRowNo, 6, currentRowNo, 8].Merge = true;
                    worksheet.Cells[currentRowNo, 1, currentRowNo, 3].Value = "Người ghi sổ";
                    worksheet.Cells[currentRowNo, 4, currentRowNo, 5].Value = v_dicFixed["KeToanTruong_CV"];
                    worksheet.Cells[currentRowNo, 6, currentRowNo, 8].Value = v_dicFixed["GiamDoc_CV"];

                    currentRowNo += 4;
                    worksheet.Cells[currentRowNo, 1, currentRowNo, 3].Merge = true;
                    worksheet.Cells[currentRowNo, 4, currentRowNo, 5].Merge = true;
                    worksheet.Cells[currentRowNo, 6, currentRowNo, 8].Merge = true;
                    worksheet.Cells[currentRowNo, 1, currentRowNo, 3].Value = v_dicFixed["NguoiLap"];
                    worksheet.Cells[currentRowNo, 4, currentRowNo, 5].Value = v_dicFixed["KeToanTruong"];
                    worksheet.Cells[currentRowNo, 6, currentRowNo, 8].Value = v_dicFixed["GiamDoc"];

                    currentRowNo--;
                    if (currentRowNo > 10)
                    {
                        worksheet.Cells[nRowBegin, 7, flagRowNo, nCol].Style.Numberformat.Format = "_(* #,##0_);_(* (#,##0);_(* \"-\"??_);_(@_)";

                        worksheet.Cells[nRowBegin, 1, flagRowNo, nCol].Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                        worksheet.Cells[nRowBegin, 1, flagRowNo, nCol].Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                        worksheet.Cells[nRowBegin, 1, flagRowNo, nCol].Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                        worksheet.Cells[nRowBegin, 1, flagRowNo, nCol].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;

                        worksheet.Cells[nRowBegin, 1, flagRowNo, nCol].Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        worksheet.Cells[nRowBegin, 1, flagRowNo, nCol].Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        worksheet.Cells[nRowBegin, 1, flagRowNo, nCol].Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        worksheet.Cells[nRowBegin, 1, flagRowNo, nCol].Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    }

                    return ExcelHelpers.SaveFileExcel(package, Directory.GetCurrentDirectory(), "SoChiTietLoai");
                }
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    private void ImportDataToExcel_Loai6(ExcelWorksheet _excel, LedgerReportModel p, ref int _currentRow)
    {
        int _ind = 0;
        var pItems = p.ItemSLTons;
        foreach (LedgerReportTonSLViewModel _k in pItems)
        {
            _currentRow++;
            _ind++;

            _excel.Cells[_currentRow, 1].Value = _ind;
            _excel.Cells[_currentRow, 2].Value = _k.Warehouse;
            _excel.Cells[_currentRow, 3].Value = string.IsNullOrEmpty(_k.Detail2) ? _k.Detail1 : _k.Detail2;
            _excel.Cells[_currentRow, 4].Value = _k.NameGood;
            _excel.Cells[_currentRow, 5].Value = _k.OpenQuantity;
            _excel.Cells[_currentRow, 6].Value = _k.InputQuantity;
            _excel.Cells[_currentRow, 7].Value = _k.OutputQuantity;
            _excel.Cells[_currentRow, 8].Value = _k.OpenQuantity + _k.InputQuantity - _k.OutputQuantity;
        }
    }

    private void ImportDataToExcel(ExcelWorksheet _excel, LedgerReportModel p, int bookDetailType, ref int _currentRow)
    {
        int _ind = -1;

        var pItems = p.BookDetails;
        foreach (SoChiTietViewModel _k in pItems)
        {
            _ind++;
            _currentRow++;
            _excel.Cells[_currentRow, 1].Value = _k.OrginalBookDate.HasValue ? _k.OrginalBookDate.Value.ToString("dd/MM") : string.Empty;
            _excel.Cells[_currentRow, 2].Value = _k.OrginalVoucherNumber;
            _excel.Cells[_currentRow, 3].Value = _k.OrginalBookDate.HasValue ? _k.OrginalBookDate.Value.ToString("dd/MM/yyyy") : string.Empty;
            _excel.Cells[_currentRow, 4].Value = _k.Description;
            _excel.Cells[_currentRow, 5].Value = _k.IsDebit ? _k.CreditCode : _k.DebitCode;
            _excel.Cells[_currentRow, 6].Value = _k.DetailCode;
            _excel.Cells[_currentRow, 7].Value = _k.ArisingDebit > 0 ? _k.ArisingDebit : 0;
            _excel.Cells[_currentRow, 8].Value = _k.ArisingCredit > 0 ? _k.ArisingCredit : 0;
            _excel.Cells[_currentRow, 9].Value = _k.ResidualDebit > 0 ? _k.ResidualDebit : 0;
            if (bookDetailType == (int)ReportBookDetailTypeEnum.soCoDu_2_ben)
                _excel.Cells[_currentRow, 10].Value = _k.ResidualCredit > 0 ? _k.ResidualCredit : 0;

            SoChiTietViewModel _sct = pItems[_ind + 1 < pItems.Count ? _ind + 1 : pItems.Count - 1];
            if (((_sct.Month + "" + _sct.Year) != (_k.Month + "" + _k.Year) && _ind < pItems.Count) || (_ind == pItems.Count - 1) && p.SumItem_SCT_ThuChi.ContainsKey(_k.Temp))
            {
                List<SoChiTietThuChiViewModel> _ledgerSum = p.SumItem_SCT_ThuChi[_k.Temp];

                SoChiTietThuChiViewModel _CongPhatSinh = _ledgerSum.FirstOrDefault(x => x.Month == -1) ?? new SoChiTietThuChiViewModel();
                SoChiTietThuChiViewModel _LuyKe = _ledgerSum.FirstOrDefault(x => x.Month == -2) ?? new SoChiTietThuChiViewModel();

                string _month = _k.Month.ToString("00") + "/" + _k.Year.ToString();

                _currentRow++;
                _excel.Cells[_currentRow, 1, _currentRow, 4].Merge = true;
                _excel.Cells[_currentRow, 1, _currentRow, 4].Value = "Cộng phát sinh tháng " + _month;
                _excel.Cells[_currentRow, 5].Value = string.Empty;
                _excel.Cells[_currentRow, 6].Value = string.Empty;
                _excel.Cells[_currentRow, 7].Value = _CongPhatSinh.Thu_Amount;
                _excel.Cells[_currentRow, 8].Value = _CongPhatSinh.Chi_Amount;
                _excel.Cells[_currentRow, 9].Value = _CongPhatSinh.Residual_Amount > 0 ? _CongPhatSinh.Residual_Amount : 0;
                if (bookDetailType == (int)ReportBookDetailTypeEnum.soCoDu_2_ben)
                    _excel.Cells[_currentRow, 10].Value = _CongPhatSinh.Residual_Amount < 0 ? _CongPhatSinh.Residual_Amount : 0;

                _currentRow++;
                _excel.Cells[_currentRow, 1, _currentRow, 4].Merge = true;
                _excel.Cells[_currentRow, 1, _currentRow, 4].Value = "Lũy kế phát sinh đến cuối kỳ";
                _excel.Cells[_currentRow, 5].Value = string.Empty;
                _excel.Cells[_currentRow, 6].Value = string.Empty;
                _excel.Cells[_currentRow, 7].Value = _LuyKe.Thu_Amount;
                _excel.Cells[_currentRow, 8].Value = _LuyKe.Chi_Amount;
                _excel.Cells[_currentRow, 9].Value = _LuyKe.Residual_Amount > 0 ? _LuyKe.Residual_Amount : 0;
                if (bookDetailType == (int)ReportBookDetailTypeEnum.soCoDu_2_ben)
                    _excel.Cells[_currentRow, 10].Value = _LuyKe.Residual_Amount < 0 ? _LuyKe.Residual_Amount : 0;
            }
        }
    }

    private void ImportDataToExcel_Loai3(ExcelWorksheet _excel, LedgerReportModel p, ref int _currentRow)
    {
        int _ind = -1;
        var pItems = p.BookDetails;
        foreach (SoChiTietViewModel _k in pItems)
        {
            _currentRow++;
            _ind++;

            _excel.Cells[_currentRow, 1].Value = _k.OrginalBookDate.HasValue ? _k.OrginalBookDate.Value.ToString("dd/MM") : string.Empty;
            _excel.Cells[_currentRow, 2].Value = _k.OrginalVoucherNumber;
            _excel.Cells[_currentRow, 3].Value = _k.OrginalBookDate.HasValue ? _k.OrginalBookDate.Value.ToString("dd/MM/yyyy") : string.Empty;
            _excel.Cells[_currentRow, 4].Value = _k.Description;
            _excel.Cells[_currentRow, 5].Value = _k.IsDebit ? _k.CreditCode : _k.DebitCode;
            _excel.Cells[_currentRow, 6].Value = _k.DetailCode;
            _excel.Cells[_currentRow, 7].Value = _k.ExchangeRate;
            _excel.Cells[_currentRow, 8].Value = _k.IsDebit ? _k.OrginalCurrency : 0;
            _excel.Cells[_currentRow, 9].Value = _k.IsDebit ? _k.Amount : 0;
            _excel.Cells[_currentRow, 10].Value = !_k.IsDebit ? _k.OrginalCurrency : 0;
            _excel.Cells[_currentRow, 11].Value = !_k.IsDebit ? _k.Amount : 0;
            _excel.Cells[_currentRow, 12].Value = _k.ResidualAmount_Foreign;
            _excel.Cells[_currentRow, 13].Value = _k.ResidualAmount_OrginalCur;
            SoChiTietViewModel _sct = p.BookDetails[_ind + 1 < p.BookDetails.Count ? _ind + 1 : p.BookDetails.Count - 1];
            if (((_sct.Month + "" + _sct.Year) != (_k.Month + "" + _k.Year) && _ind < p.BookDetails.Count) || (_ind == p.BookDetails.Count - 1) && p.SumItem_SCT_ThuChi.ContainsKey(_k.Temp))
            {
                List<SoChiTietThuChiViewModel> _ledgerSum = p.SumItem_SCT_ThuChi[_k.Temp];

                SoChiTietThuChiViewModel _CongPhatSinh = _ledgerSum.FirstOrDefault(x => x.Month == -1) ?? new SoChiTietThuChiViewModel();
                SoChiTietThuChiViewModel _LuyKe = _ledgerSum.FirstOrDefault(x => x.Month == -2) ?? new SoChiTietThuChiViewModel();
                SoChiTietThuChiViewModel _Du = _ledgerSum.FirstOrDefault(x => x.Month == -3) ?? new SoChiTietThuChiViewModel();

                string _monthb = _k.Month.ToString("00") + "/" + _k.Year.ToString();

                _currentRow++;
                _excel.Cells[_currentRow, 1, _currentRow, 4].Merge = true;
                _excel.Cells[_currentRow, 1, _currentRow, 4].Value = "Cộng phát sinh tháng " + _monthb;
                _excel.Cells[_currentRow, 5].Value = string.Empty;
                _excel.Cells[_currentRow, 6].Value = string.Empty;
                _excel.Cells[_currentRow, 7].Value = string.Empty;
                _excel.Cells[_currentRow, 8].Value = _CongPhatSinh.ArisingDebit_Foreign;
                _excel.Cells[_currentRow, 9].Value = _CongPhatSinh.Thu_Amount;
                _excel.Cells[_currentRow, 10].Value = _CongPhatSinh.ArisingCredit_Foreign;
                _excel.Cells[_currentRow, 11].Value = _CongPhatSinh.Chi_Amount;
                _excel.Cells[_currentRow, 12].Value = 0;
                _excel.Cells[_currentRow, 13].Value = 0;

                _currentRow++;
                _excel.Cells[_currentRow, 1, _currentRow, 4].Merge = true;
                _excel.Cells[_currentRow, 1, _currentRow, 4].Value = "Lũy kế phát sinh đến cuối kỳ";
                _excel.Cells[_currentRow, 5].Value = string.Empty;
                _excel.Cells[_currentRow, 6].Value = string.Empty;
                _excel.Cells[_currentRow, 7].Value = string.Empty;
                _excel.Cells[_currentRow, 8].Value = _LuyKe.ArisingDebit_Foreign;
                _excel.Cells[_currentRow, 9].Value = _LuyKe.Thu_Amount;
                _excel.Cells[_currentRow, 10].Value = _LuyKe.ArisingCredit_Foreign;
                _excel.Cells[_currentRow, 11].Value = _LuyKe.Chi_Amount;
                _excel.Cells[_currentRow, 12].Value = 0;
                _excel.Cells[_currentRow, 13].Value = 0;

                _currentRow++;
                _excel.Cells[_currentRow, 1, _currentRow, 4].Merge = true;
                _excel.Cells[_currentRow, 1, _currentRow, 4].Value = "Dư cuối " + _monthb;
                _excel.Cells[_currentRow, 5].Value = string.Empty;
                _excel.Cells[_currentRow, 6].Value = string.Empty;
                _excel.Cells[_currentRow, 7].Value = string.Empty;
                _excel.Cells[_currentRow, 8].Value = 0;
                _excel.Cells[_currentRow, 9].Value = 0;
                _excel.Cells[_currentRow, 10].Value = 0;
                _excel.Cells[_currentRow, 11].Value = 0;
                _excel.Cells[_currentRow, 12].Value = _Du.ResidualAmount_Foreign;
                _excel.Cells[_currentRow, 13].Value = _Du.ResidualAmount_OrginalCur;
            }
        }
    }

    private void ImportDataToExcel_Loai5(ExcelWorksheet _excel, LedgerReportModel p, ref int _currentRow)
    {
        int _ind = -1;
        var pItems = p.BookDetails;
        foreach (SoChiTietViewModel _k in pItems)
        {
            _currentRow++;
            _ind++;
            _excel.Cells[_currentRow, 1].Value = _k.OrginalBookDate.HasValue ? _k.OrginalBookDate.Value.ToString("dd/MM") : string.Empty;
            _excel.Cells[_currentRow, 2].Value = _k.OrginalVoucherNumber;
            _excel.Cells[_currentRow, 3].Value = _k.OrginalBookDate.HasValue ? _k.OrginalBookDate.Value.ToString("dd/MM/yyyy") : string.Empty;
            _excel.Cells[_currentRow, 4].Value = _k.NameOfPerson;
            _excel.Cells[_currentRow, 5].Value = _k.Description;
            _excel.Cells[_currentRow, 6].Value = _k.IsDebit ? _k.CreditCode : _k.DebitCode;
            _excel.Cells[_currentRow, 7].Value = _k.IsDebit ? _k.Thu_Amount : 0;
            _excel.Cells[_currentRow, 8].Value = !_k.IsDebit ? _k.Chi_Amount : 0;
            _excel.Cells[_currentRow, 9].Value = _k.Residual_Amount;
            SoChiTietViewModel _sct = p.BookDetails[_ind + 1 < p.BookDetails.Count ? _ind + 1 : p.BookDetails.Count - 1];
            if (((_sct.Month + "" + _sct.Year) != (_k.Month + "" + _k.Year) && _ind < p.BookDetails.Count) || (_ind == p.BookDetails.Count - 1) && p.SumItem_SCT_ThuChi.ContainsKey(_k.Temp))
            {
                List<SoChiTietThuChiViewModel> _ledgerSum = p.SumItem_SCT_ThuChi[_k.Temp];

                SoChiTietThuChiViewModel _CongPhatSinh = _ledgerSum.FirstOrDefault(x => x.Month == -1) ?? new SoChiTietThuChiViewModel();
                SoChiTietThuChiViewModel _LuyKe = _ledgerSum.FirstOrDefault(x => x.Month == -2) ?? new SoChiTietThuChiViewModel();

                string _monthb = _k.Month.ToString("00") + "/" + _k.Year.ToString();

                _currentRow++;
                _excel.Cells[_currentRow, 1, _currentRow, 4].Merge = true;
                _excel.Cells[_currentRow, 1, _currentRow, 4].Value = "Cộng phát sinh tháng " + _monthb;
                _excel.Cells[_currentRow, 5].Value = string.Empty;
                _excel.Cells[_currentRow, 6].Value = string.Empty;
                _excel.Cells[_currentRow, 7].Value = _CongPhatSinh.Thu_Amount;
                _excel.Cells[_currentRow, 8].Value = _CongPhatSinh.Chi_Amount;
                _excel.Cells[_currentRow, 9].Value = _CongPhatSinh.Residual_Amount;

                _currentRow++;
                _excel.Cells[_currentRow, 1, _currentRow, 4].Merge = true;
                _excel.Cells[_currentRow, 1, _currentRow, 4].Value = "Lũy kế phát sinh đến cuối kỳ";
                _excel.Cells[_currentRow, 5].Value = string.Empty;
                _excel.Cells[_currentRow, 6].Value = string.Empty;
                _excel.Cells[_currentRow, 7].Value = _LuyKe.Thu_Amount;
                _excel.Cells[_currentRow, 8].Value = _LuyKe.Chi_Amount;
                _excel.Cells[_currentRow, 9].Value = _LuyKe.Residual_Amount;
            }
        }
    }

    private async Task<LedgerReportCalculatorIO> CalculatorFollowMonth_ThuChi(LedgerReportParam _param, int year)
    {
        LedgerReportCalculatorIO p = new LedgerReportCalculatorIO();
        try
        {
            DateTime _fromDt, _toDt;
            _fromDt = _param.FromMonth > 0 ? new DateTime(DateTime.Now.Year, (int)_param.FromMonth, 1) : (DateTime)_param.FromDate;
            _toDt = _param.ToMonth > 0 ? new DateTime(DateTime.Now.Year, (int)_param.ToMonth, 1) : (DateTime)_param.ToDate;

            var _chart = _context.GetChartOfAccount(year).Where(x =>
                string.IsNullOrEmpty(_param.AccountCodeDetail1)
                ? x.Code == _param.AccountCode
                : x.Code == _param.AccountCodeDetail1 && x.ParentRef == _param.AccountCode
                ).FirstOrDefault();

            if (_param.FromDate.Value.Year != year)
            {
                var accountOld = await _context.ChartOfAccounts.FirstOrDefaultAsync(x => x.Code == _chart.Code && x.ParentRef == _chart.ParentRef
                                && (string.IsNullOrEmpty(_chart.WarehouseCode) || x.WarehouseCode == _chart.WarehouseCode) && x.Year == _param.FromDate.Value.Year);
                _chart.OpeningDebit = accountOld?.OpeningDebit;
                _chart.OpeningCredit = accountOld?.OpeningCredit;
                _chart.OpeningStockQuantity = accountOld?.OpeningStockQuantity;

                _chart.OpeningDebitNB = accountOld?.OpeningDebitNB;
                _chart.OpeningCreditNB = accountOld?.OpeningCreditNB;
                _chart.OpeningStockQuantityNB = accountOld?.OpeningStockQuantityNB;
            }

            if (_chart != null)
            {
                p.OpeningDebit = _chart.OpeningDebit ?? 0;
                p.OpeningCredit = _chart.OpeningCredit ?? 0;
                p.OpeningAmountLeft = p.OpeningDebit - p.OpeningCredit;

                p.OriginalCurrency = _chart.OpeningForeignDebit ?? 0;
                p.ExchangeRate = (_chart?.OpeningDebit ?? 0) - (_chart?.OpeningCredit ?? 0);
                //4
                p.StockUnitPrice = (_chart.StockUnitPrice ?? 0);
                p.OpeningStockQuantity = (_chart.OpeningStockQuantity ?? 0);

                p.OpeningDebitNB = _chart.OpeningDebitNB ?? 0;
                p.OpeningCreditNB = _chart.OpeningCreditNB ?? 0;
                p.OpeningAmountLeftNB = p.OpeningDebitNB - p.OpeningCreditNB;
                p.OriginalCurrencyNB = _chart.OpeningForeignDebitNB ?? 0;
                p.ExchangeRateNB = (_chart?.OpeningDebitNB ?? 0) - (_chart?.OpeningCreditNB ?? 0);
                p.StockUnitPriceNB = _chart.StockUnitPriceNB ?? 0;
                p.OpeningStockQuantityNB = _chart.OpeningStockQuantityNB ?? 0;
            }

            return p;
        }
        catch
        {
            return null;
        }
    }

    private void GetResidualAmount(ref double OpeningBackLog, double Amount)
    {
        OpeningBackLog += Amount;
    }

    private void ImportDataToExcel_Loai4(ExcelWorksheet _excel, LedgerReportModel p, ref int _currentRow)
    {
        foreach (var item in p.listAccoutCodeThuChi)
        {
            string[] listKey = item.Key.Split('-');
            string detail1 = listKey[0];
            string detail2 = listKey[1];
            string warehouseCode = listKey[2];
            int _ind = -1;
            var listBookDetails = p.BookDetails.Where(x => string.IsNullOrEmpty(detail1) || (x.DebitDetailCodeFirst == detail1 || x.CreditDetailCodeFirst == detail1)
                                    && string.IsNullOrEmpty(detail2) || (x.DebitDetailCodeSecond == detail2 || x.CreditDetailCodeSecond == detail2)
                                    && string.IsNullOrEmpty(warehouseCode) || (x.CreditWarehouseCode == warehouseCode || x.DebitWarehouseCode == warehouseCode)
                                    ).ToList();

            foreach (SoChiTietViewModel _k in listBookDetails)
            {
                _currentRow++;
                _ind++;

                _excel.Cells[_currentRow, 1].Value = _k.OrginalBookDate.HasValue ? _k.OrginalBookDate.Value.ToString("dd/MM") : string.Empty;
                _excel.Cells[_currentRow, 2].Value = _k.OrginalVoucherNumber;
                _excel.Cells[_currentRow, 3].Value = _k.OrginalBookDate.HasValue ? _k.OrginalBookDate.Value.ToString("dd/MM/yyyy") : string.Empty;
                _excel.Cells[_currentRow, 4].Value = string.IsNullOrEmpty(detail2) ? detail1 : detail2;
                _excel.Cells[_currentRow, 5].Value = _k.IsDebit ? _k.CreditCode : _k.DebitCode;
                _excel.Cells[_currentRow, 6].Value = _k.DetailCode;
                _excel.Cells[_currentRow, 7].Value = _k.UnitPrice;
                _excel.Cells[_currentRow, 8].Value = _k.IsDebit ? _k.Quantity : 0;
                _excel.Cells[_currentRow, 9].Value = _k.IsDebit ? _k.Amount : 0;
                _excel.Cells[_currentRow, 10].Value = !_k.IsDebit ? _k.Quantity : 0;
                _excel.Cells[_currentRow, 11].Value = !_k.IsDebit ? _k.Amount : 0;
                _excel.Cells[_currentRow, 12].Value = _k.Residual_Quantity;
                _excel.Cells[_currentRow, 13].Value = _k.Residual_Amount;
                SoChiTietViewModel _sct = listBookDetails[_ind + 1 < listBookDetails.Count ? _ind + 1 : listBookDetails.Count - 1];
                if ((_sct.Month != _k.Month && _ind < listBookDetails.Count) || (_ind == listBookDetails.Count - 1))
                {
                    List<SoChiTietThuChiViewModel> _ledgerSum = item.Value.Where(y => y.Month == _k.Month).ToList();

                    SoChiTietThuChiViewModel _CongPhatSinh = _ledgerSum.FirstOrDefault(y => y.Type == 1);
                    if (_CongPhatSinh == null)
                        _CongPhatSinh = new SoChiTietThuChiViewModel();
                    SoChiTietThuChiViewModel _LuyKe = _ledgerSum.FirstOrDefault(y => y.Type == 2);
                    if (_LuyKe == null)
                        _LuyKe = new SoChiTietThuChiViewModel();
                    SoChiTietThuChiViewModel _Du = _ledgerSum.FirstOrDefault(y => y.Type == 3);
                    if (_Du == null)
                        _Du = new SoChiTietThuChiViewModel();

                    string _monthb = _k.Month.ToString("00") + "/" + _k.Year.ToString();

                    _currentRow++;
                    _excel.Cells[_currentRow, 1, _currentRow, 4].Merge = true;
                    _excel.Cells[_currentRow, 1, _currentRow, 4].Value = "Cộng phát sinh tháng " + _monthb;
                    _excel.Cells[_currentRow, 5].Value = string.Empty;
                    _excel.Cells[_currentRow, 6].Value = string.Empty;
                    _excel.Cells[_currentRow, 7].Value = string.Empty;
                    _excel.Cells[_currentRow, 8].Value = _CongPhatSinh.Input_Quantity;
                    _excel.Cells[_currentRow, 9].Value = _CongPhatSinh.Thu_Amount;
                    _excel.Cells[_currentRow, 10].Value = _CongPhatSinh.Output_Quantity;
                    _excel.Cells[_currentRow, 11].Value = _CongPhatSinh.Chi_Amount;
                    _excel.Cells[_currentRow, 12].Value = 0;
                    _excel.Cells[_currentRow, 13].Value = 0;

                    _currentRow++;
                    _excel.Cells[_currentRow, 1, _currentRow, 4].Merge = true;
                    _excel.Cells[_currentRow, 1, _currentRow, 4].Value = "Lũy kế phát sinh đến cuối kỳ";
                    _excel.Cells[_currentRow, 5].Value = string.Empty;
                    _excel.Cells[_currentRow, 6].Value = string.Empty;
                    _excel.Cells[_currentRow, 7].Value = string.Empty;
                    _excel.Cells[_currentRow, 8].Value = _LuyKe.Input_Quantity;
                    _excel.Cells[_currentRow, 9].Value = _LuyKe.Thu_Amount;
                    _excel.Cells[_currentRow, 10].Value = _LuyKe.Output_Quantity;
                    _excel.Cells[_currentRow, 11].Value = _LuyKe.Chi_Amount;
                    _excel.Cells[_currentRow, 12].Value = 0;
                    _excel.Cells[_currentRow, 13].Value = 0;

                    _currentRow++;
                    double SR_DC_IP_DG = 0;
                    if (_Du.Residual_Quantity > 0)
                        SR_DC_IP_DG = Math.Abs(_Du.Residual_Amount) / Math.Abs(_Du.Residual_Quantity);
                    _excel.Cells[_currentRow, 1, _currentRow, 4].Merge = true;
                    _excel.Cells[_currentRow, 1, _currentRow, 4].Value = "Dư cuối " + _monthb;
                    _excel.Cells[_currentRow, 5].Value = string.Empty;
                    _excel.Cells[_currentRow, 6].Value = string.Empty;
                    _excel.Cells[_currentRow, 7].Value = SR_DC_IP_DG;
                    _excel.Cells[_currentRow, 8].Value = 0;
                    _excel.Cells[_currentRow, 9].Value = 0;
                    _excel.Cells[_currentRow, 10].Value = 0;
                    _excel.Cells[_currentRow, 11].Value = 0;
                    _excel.Cells[_currentRow, 12].Value = _Du.Residual_Quantity;
                    _excel.Cells[_currentRow, 13].Value = _Du.Residual_Amount;
                    _currentRow++;
                }
            }
        }
    }
}