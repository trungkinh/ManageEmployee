using ManageEmployee.DataTransferObject.PagingRequest;

namespace ManageEmployee.Services.Interfaces.Reports;

public interface ISavedMovedMoneyReportService
{
    Task<string> ExportDataReport(SavedMoneyReportParam request, int year, bool isNoiBo = false);
}
