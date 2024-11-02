using ManageEmployee.Entities;

namespace ManageEmployee.JobSchedules;

public interface IWarningNotificationService
{
    Task<List<WarningNotification>> GetWarningNotification(int userId);
    Task WarningNotificationTrigger();
}
