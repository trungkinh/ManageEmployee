using ManageEmployee.DataTransferObject.Reports;

namespace ManageEmployee.Services.Interfaces.Ledgers.V2;

public interface IAccountBalanceSheetV2Service
{
    Task<string> GenerateReport(AccountBalanceReportParam param, int year);
}
