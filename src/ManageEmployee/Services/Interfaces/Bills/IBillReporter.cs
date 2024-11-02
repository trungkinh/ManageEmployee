using ManageEmployee.DataTransferObject.BillModels;

namespace ManageEmployee.Services.Interfaces.Bills;

public interface IBillReporter
{
    Task<BillReporterModel> ReportAsync(BillPagingRequestModel param);
    Task<object> ReportHomeAsync(int year);
}