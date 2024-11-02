﻿using ManageEmployee.DataTransferObject;
using ManageEmployee.DataTransferObject.BaseResponseModels;
using ManageEmployee.DataTransferObject.InOut;
using ManageEmployee.DataTransferObject.PagingResultModels;
using ManageEmployee.DataTransferObject.Timekeep;
using ManageEmployee.Entities.InOutEntities;

namespace ManageEmployee.Services.Interfaces.InOuts;

public interface IInOutService
{
    Task<PagingResult<InOutHistoryViewModel>> GetPaging(InOutHistoryFilterDateParams param, int userId, string listRole);

    Task<PagingResult<InOutHistoryViewModel>> GetAllByUserId(string keyword, int? departmentId, int targetId, DateTime fromDate, DateTime toDate, int page, int pageSize, int userId);

    bool HasPreviousCheckInWithAnotherSymbolId(int userId, DateTime? timeIn, int symbolId);

    Task Update(InOutHistory param);

    Task UpdateCheckedAsync(int Id);

    Task<InOutHistory> Create(InOutHistory param);

    Task<IEnumerable<InOutHistory>> GetAll();

    Task Delete(int id);

    Task<PagingResult<ManualCheckInViewModel>> GetManualCheckInUsers(ManualCheckInUserRequest request, int userId, string roles);

    IEnumerable<TimeKeepMapping.Report> ExportReport(DateTime fromDate, DateTime toDate, int departmentId = 0, int targetId = 0, string keyword = "", int currentUser = 0);

    Task<IEnumerable<TimeKeepMapping.Report>> GetReport(TimeKeepViewModel param, int userId, string roles);

    Task<BaseResponseModel> TimeKeepingReportV2(TimeKeepViewModel param, int userId, string roles);
}
