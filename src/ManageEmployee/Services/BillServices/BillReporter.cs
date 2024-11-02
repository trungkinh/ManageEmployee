﻿using Common.Constants;
using ManageEmployee.Dal.DbContexts;
using ManageEmployee.DataTransferObject.BillModels;
using ManageEmployee.Entities.Constants;
using ManageEmployee.Helpers;
using ManageEmployee.Services.Interfaces.Bills;
using Microsoft.EntityFrameworkCore;

namespace ManageEmployee.Services.BillServices;


public class BillReporter : IBillReporter
{
    private readonly ApplicationDbContext _context;
    public BillReporter(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BillReporterModel> ReportAsync(BillPagingRequestModel param)
    {
        var fromAt = (param.FromAt ?? DateTime.Today).Date;
        var toAt = (param.ToAt ?? DateTime.Today).Date.AddDays(1);

        var user = await _context.Users.FindAsync(param.UserId);
        if (user is null)
        {
            throw new ErrorException(ErrorMessages.DataNotFound);
        }

        var bills = await _context.Bills.Where(x => x.CreatedDate >= fromAt && x.CreatedDate < toAt && x.UserCode == user.Username).ToListAsync();
        var customerIds = bills.Select(x => x.CustomerId);
        var customers = await _context.Customers.Where(x => customerIds.Contains(x.Id)).ToListAsync();
        var configDiscountCodes = typeof(ConfigDiscountConst).GetFields().Select(x => x.ToString());
        var configDiscounts = await _context.ConfigDiscounts.Where(x => configDiscountCodes.Contains(x.Code)).ToListAsync();
        var customerClassficationOld = await _context.CustomerClassifications.FirstOrDefaultAsync(x => x.Code == ConfigDiscountConst.CustomerOld);
        var customerClassficationNew = await _context.CustomerClassifications.FirstOrDefaultAsync(x => x.Code == ConfigDiscountConst.CustomerNew);

        var itemOut = new BillReporterModel
        {
            Items = new List<BillReporterDetailModel>()
        };
        double amountBonus = 0;
        foreach (var customer in customers)
        {
            var billCustomers = bills.Where(x => x.CustomerId == customer.Id).ToList();
            var billCustomerIds = billCustomers.Select(x => x.Id);
            var detailOut = new BillReporterDetailModel
            {
                CustomerName = customer.Name,
            };
            detailOut.QuantitySold = await _context.BillDetails.Where(x => billCustomerIds.Contains(x.BillId)).SumAsync(x => x.Quantity);
            detailOut.AmountSold = await _context.BillDetails.Where(x => billCustomerIds.Contains(x.BillId)).SumAsync(x => x.Quantity * x.UnitPrice);
            detailOut.QuantityRefund = await _context.BillDetailRefunds.Where(x => billCustomerIds.Contains(x.BillDetailId ?? 0)).SumAsync(x => x.Quantity);
            detailOut.AmountRefund = await _context.BillDetailRefunds.Where(x => billCustomerIds.Contains(x.BillDetailId ?? 0)).SumAsync(x => x.Quantity * x.UnitPrice);

            detailOut.QuantityRemaining = detailOut.QuantitySold - detailOut.QuantityRefund;
            detailOut.AmountRemaining = detailOut.AmountSold - detailOut.AmountRefund;
            var debitcode1 = "111";
            var debitcode2 = "112";
            var creditcode = "131";
            detailOut.TotalAmount = await _context.Ledgers.Where(x =>
                x.OrginalBookDate >= fromAt &&
                x.OrginalBookDate < toAt
                && (
                    x.DebitCode.StartsWith(debitcode1) ||
                    x.DebitCode.StartsWith(debitcode2)
                ) &&
                x.CreditCode.StartsWith(creditcode)
            ).SumAsync(x => x.Amount);

            itemOut.Items.Add(detailOut);
            if (customer.CustomerClassficationId == customerClassficationOld?.Id)
            {
                amountBonus += configDiscounts.FirstOrDefault(x => x.Code == customerClassficationOld.Code)?.DiscountReceivedYear ?? 0;
            }
            else if (customer.CustomerClassficationId == customerClassficationNew?.Id)
            {
                amountBonus += configDiscounts.FirstOrDefault(x => x.Code == customerClassficationNew.Code)?.DiscountReceivedYear ?? 0;
            }
        }
        itemOut.TotalItem = new BillReporterDetailModel
        {
            QuantityRefund = itemOut.Items.Sum(x => x.QuantityRefund),
            QuantityRemaining = itemOut.Items.Sum(x => x.QuantityRemaining),
            QuantitySold = itemOut.Items.Sum(x => x.QuantitySold),
            AmountRefund = itemOut.Items.Sum(x => x.AmountRefund),
            AmountRemaining = itemOut.Items.Sum(x => x.AmountRemaining),
            AmountSold = itemOut.Items.Sum(x => x.AmountSold),
            TotalAmount = itemOut.Items.Sum(x => x.AmountSold),
        };

        itemOut.TotalQuantity = itemOut.Items.Sum(x => x.QuantityRemaining);
        itemOut.AverageUnitPrice = itemOut.Items.Sum(x => x.AmountRemaining) / itemOut.TotalQuantity;
        itemOut.TonsCollected = itemOut.Items.Sum(x => x.TotalAmount) / itemOut.AverageUnitPrice;

        itemOut.AmountBonus = itemOut.TonsCollected * amountBonus;
        itemOut.ReceiveBonus = itemOut.AmountBonus / 2;
        itemOut.ToTalAmount = itemOut.AmountBonus - itemOut.ReceiveBonus;

        return itemOut;
    }

    public async Task<object> ReportHomeAsync(int year)
    {
        try
        {
            DateTime dtFrom = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

            var bills = await (from p in _context.Bills
                               where
                               p.Status.Contains(TranTypeConst.Paid)
                               && p.CreatedDate >= dtFrom && p.CreatedDate < dtFrom.AddMonths(1)
                               select new BillPaging
                               {
                                   Id = p.Id,
                                   CustomerId = p.CustomerId,
                                   CustomerName = p.CustomerName,
                                   UserCode = p.UserCode,
                                   DeskId = p.DeskId,
                                   FloorId = p.FloorId,
                                   QuantityCustomer = p.QuantityCustomer,
                                   TotalAmount = p.TotalAmount,
                                   AmountReceivedByCus = p.AmountReceivedByCus,
                                   AmountSendToCus = p.AmountSendToCus,
                                   DiscountPrice = p.DiscountPrice,
                                   DiscountType = p.DiscountType,
                                   Note = p.Note,
                                   CreatedDate = p.CreatedDate,
                                   TotalAmountCN = p.TypePay == "CN" ? (p.TotalAmount) : 0,
                                   DisplayOrder = $"Bill {p.DisplayOrder} - {p.BillNumber}",
                                   InvoiceNumber = p.InvoiceNumber,
                               }).ToListAsync();
            var billIds = bills.Select(x => x.Id).ToList();
            var TotalQuantity = await _context.BillDetails.Where(x => billIds.Contains(x.BillId)).SumAsync(x => x.Quantity);
            var account331 = await _context.GetChartOfAccount(year).FirstOrDefaultAsync(x => x.Code == "331");
            return new
            {
                TotalAmount = bills.Sum(x => x.TotalAmount),
                TotalQuantity = TotalQuantity,
                TotalCustomer = bills.Sum(x => x.QuantityCustomer),
                TotalAmountCN = bills.Sum(x => x.TotalAmountCN),
                TotalAmount331 = (account331?.OpeningDebit ?? 0) - (account331?.OpeningCredit ?? 0) + (account331?.ArisingDebit ?? 0) - (account331?.ArisingCredit ?? 0),
                TotalAmountWebsite = 0
            };
        }
        catch
        {
            return null;
        }
    }

}