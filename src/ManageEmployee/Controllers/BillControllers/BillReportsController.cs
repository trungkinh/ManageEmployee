using ManageEmployee.DataTransferObject.BaseResponseModels;
using ManageEmployee.DataTransferObject.BillModels;
using ManageEmployee.Services.Interfaces.Bills;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManageEmployee.Controllers.BillControllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class BillReportsController : ControllerBase
{
    private readonly IBillReporter _billReporter;

    public BillReportsController(IBillReporter billReporter)
    {
        _billReporter = billReporter;
    }

    [HttpGet("report")]
    public async Task<IActionResult> GetLedgerFromBillId([FromQuery] BillPagingRequestModel param)
    {
        var result = await _billReporter.ReportAsync(param);
        return Ok(new BaseResponseModel
        {
            Data = result
        });
    }

    [HttpGet("report-home")]
    public async Task<IActionResult> ReportHome([FromHeader] int yearFilter)
    {
        var result = await _billReporter.ReportHomeAsync(yearFilter);
        return Ok(result);
    }
}