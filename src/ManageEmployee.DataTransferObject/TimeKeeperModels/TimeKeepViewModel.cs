using Common.Helpers;
using ManageEmployee.DataTransferObject.PagingRequest;
using ManageEmployee.Entities.BaseEntities;

namespace ManageEmployee.DataTransferObject;

public class TimeKeep : BaseEntity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int TargetId { get; set; }
    public int TypeOfWork { get; set; }
    public int Type { get; set; }
    public int TimeKeepSymbolId { get; set; }
    public DateTime DateTimeKeep { get; set; }
    public int IsOverTime { get; set; } = 1;  // 1 - BT; 2-TC; 3-P; 4-KP

}

public class TimeKeepViewModel : PagingRequestModel
{
    public int? DepartmentId { get; set; }
    public DateTime DateTimeKeep { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int? TargetId { get; set; }
    public bool CheckCurrentUser { get; set; } = false;
}
public class TimeKeepMapping
{
    public class GetList : BaseEntity
    {
        public string? FullName { get; set; }
        public bool isDelete { get; set; }
        public string? Code { get; set; }
        public string? TargetName { get; set; }
        public int TargetId { get; set; }
        public int TypeOfWork { get; set; }
        public int UserId { get; set; }
        public string? DepartmentName { get; set; }
        public int Id { get; set; }
        public int Type { get; set; }
        public int TimeKeepSymbolId { get; set; }
        public int TimeKeepId { get; set; }
        public string? Timekeep { get; set; }
        public DateTime? DateTimeKeep { get; set; }
        public string? TargetCode { get; set; }
    }

    public class Report
    {
        public string? FullName { get; set; }
        public string? Code { get; set; }
        public int UserId { get; set; }
        public string? DepartmentName { get; set; }
        public List<TimeKeepHistoryMapping.GetForReport>? Histories { get; set; }
        public int SymbolId { get; set; }

        public int TotalWorkingDay
        {
            get
            {
                if (Histories == null || Histories.Count <= 0)
                    return 0;
                return Histories.GroupBy(x => x.DateTimeKeep.Day)
                    .Count();
            }
        }
        public int TotalPaidLeave
        {
            get
            {
                if (Histories == null || Histories.Count <= 0)
                    return 0;
                return Histories.Where(x => x.IsOverTime == 3).GroupBy(x => x.DateTimeKeep.Day)
                    .Count();
            }
        }
        public int TotalUnPaidLeave
        {
            get
            {
                if (Histories == null || Histories.Count <= 0)
                    return 0;
                return Histories.Where(x => x.IsOverTime == 4).GroupBy(x => x.DateTimeKeep.Day)
                    .Count();
            }
        }
        public double TotalPaid
        {
            get
            {
                return TotalPaidLeave + TotalWorkingDay;
            }
        }

        public double TotalWorkingHours
        {
            get
            {
                if (Histories == null || Histories.Count <= 0)
                    return 0;
                return Histories
                    .Where(x => x.IsOverTime == 1)
                    .Sum(x => x.TimeKeepSymbolTimeTotal);
            }
        }

        public double TotalOverTimeWorkingHours
        {
            get
            {
                if (Histories == null || Histories.Count <= 0)
                    return 0;
                return Histories
                    .Where(x => x.IsOverTime == 2) // 1 - BT; 2-TC; 3-P; 4-KP
                    .Sum(x => x.TimeKeepSymbolTimeTotal);
            }
        }

        public List<OverTimeHistoryModel>? OvertimesHistories { get; set; }

    }

    public class InOutSmallPeriodModel
    {
        private readonly DateTime? _timeFrameFrom;
        private readonly DateTime? _timeFrameTo;
        public int UserId { get; set; }
        public DateTime Date { get; set; }
        public DateTime? TimeIn { get; set; }
        public DateTime? TimeOut { get; set; }
        public string? SymbolCode { get; set; }

        public bool IsMissingInOut => TimeIn == null || TimeOut == null;

        private DateTime? AcceptedTimeIn => !IsMissingInOut
            ? CommonHelper.GetMaxValue(TimeIn, TimeFrameFrom)
            : null;

        private DateTime? AcceptedTimeOut => !IsMissingInOut
            ? CommonHelper.GetMinValue(TimeOut, TimeFrameTo)
            : null;

        public double TotalHours => AcceptedTimeOut != null && AcceptedTimeIn != null
            ? Math.Round((AcceptedTimeOut.Value - AcceptedTimeIn.Value).TotalHours, 2)
            : 0;

        public DateTime? TimeFrameFrom
        {
            get => _timeFrameFrom;
            init =>
                _timeFrameFrom = value.HasValue
                    ? new DateTime(value.Value.Year, value.Value.Month, value.Value.Day, value.Value.Hour,
                        value.Value.Minute, 0)
                    : null;
        }

        public DateTime? TimeFrameTo
        {
            get => _timeFrameTo;
            init =>
                _timeFrameTo = value.HasValue
                    ? new DateTime(value.Value.Year, value.Value.Month, value.Value.Day, value.Value.Hour,
                        value.Value.Minute, 0)
                    : null;
        }

        public bool Missing { get; set; }
        public int WorkType { get; set; }
        public double SymbolHours { get; set; }
    }

    public class TimeKeepingHistoryByDateModel
    {
        public TimeKeepingHistoryByDateModel(
            DateTime date,
            bool isMissingInOut = true,
            double workingHours = 0d,
            double overtimeHours = 0d,
            string symbolCode = "",
            double symbolHours = 0d)
        {
            Date = date;
            IsMissingInOut = isMissingInOut;
            WorkingHours = Math.Round(workingHours, 2);
            OvertimeHours = Math.Round(overtimeHours, 2);
            SymbolCode = symbolCode;
            SymbolHours = symbolHours;
        }

        public DateTime Date { get; set; }
        public double WorkingHours { get; set; }
        public double OvertimeHours { get; set; }
        public string SymbolCode { get; set; }
        public double SymbolHours { get; set; }
        private bool IsMissingInOut { get; set; }
        public bool IsDisplaySymbol => !IsMissingInOut && WorkingHours >= SymbolHours;
    }

    public class TimeKeepingReportV2Model
    {
        public string? FullName { get; set; }
        public string? Code { get; set; }
        public int UserId { get; set; }
        public string? DepartmentName { get; set; }

        public int TotalWorkingDay { get; set; }

        public int TotalPaidLeave { get; set; }

        public int TotalUnPaidLeave { get; set; }
        public double TotalPaid => TotalPaidLeave + TotalWorkingDay;
        public double TotalWorkingHours { get; set; }
        public double TotalOverTimeWorkingHours { get; set; }

        public IEnumerable<TimeKeepingHistoryByDateModel>? Histories { get; set; }
    }

    public class OverTimeHistoryModel
    {
        public double TotalHours { get; set; }
        public DateTime Date { get; set; }
    }
    public class GetListReport : BaseEntity
    {
        public string? FullName { get; set; }
        public string? Code { get; set; }
        public string? DepartmentName { get; set; }
        public int UserId { get; set; }
        public List<TimeKeep>? TypeKeepList { get; set; }
    }

    public class GetSelectList
    {
        public string? FullName { get; set; }
        public string? Code { get; set; }
        public int Id { get; set; }
        public string? UserId { get; set; }
        public string? Type { get; set; }
        public string? Timekeep { get; set; }
        public DateTime DateTimeKeep { get; set; }

    }
}


public class TimeKeepHistoryMapping
{
    public class GetForReport
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        //public int TimekeepId { get; set; }
        public DateTime? TimeIn { get; set; }
        public DateTime? TimeOut { get; set; }
        public int CheckInMethod { get; set; } // 0 -> Manual, 1 -> Automatic
        public int TimeKeepSymbolId { get; set; }
        public string? TimeKeepSymbolCode { get; set; }
        public double TimeKeepSymbolTimeTotal { get; set; }
        public string? TimeKeepSymbolName { get; set; }
        public DateTime DateTimeKeep { get; set; }
        public int IsOverTime { get; set; }  // 1 - BT; 2-TC; 3-P; 4-KP
        public int TargetId { get; set; }
        public string? TargetCode { get; set; }
        public string? TargetName { get; set; }
    }
    public class GetHistory
    {
        public int Id { get; set; }
        public int TimekeepId { get; set; }
        public DateTime TimeIn { get; set; }
        public DateTime TimeOut { get; set; }
        public int Type { get; set; }
        public int TimeKeepSymbolId { get; set; }
        public string? TargetName { get; set; }
        public DateTime DateTimeKeep { get; set; }
        public string? FullName { get; set; }
        public string? Code { get; set; }
        public int TypeOfWork { get; set; }
    }
}