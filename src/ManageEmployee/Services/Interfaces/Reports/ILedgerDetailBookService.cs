using ManageEmployee.DataTransferObject.PagingRequest;

namespace ManageEmployee.Services.Interfaces.Reports;

public interface ILedgerDetailBookService
{
    Task<string> GetDataReport_SoChiTiet_Four(LedgerReportParamDetail _param, int year, bool isNoiBo = false);
    Task<string> GetDataReport_SoChiTiet_Six(LedgerReportParamDetail _param, int year, string wareHouseCode = "");
    Task<string> GetDataReport_SoChiTiet_Full(LedgerReportParamDetail _param, int year, bool isNoiBo = false);
}
