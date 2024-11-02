﻿using System.Net;
using Common.Models;
using ManageEmployee.DataTransferObject;
using Microsoft.Extensions.Primitives;

namespace ManageEmployee.Services.Interfaces.TimeKeepings;

public interface ITimeKeepingService
{
    Task<List<InOutHistoryTimeline>> InOutValidateAsync(StringValues dbName, IdentityUser user,
        IPAddress remoteIpAddress, TimeKeepingValidationRequest request);
    Task<List<InOutHistoryTimeline>> GetInOutHistoryByDate(IdentityUser user, DateTime targetDate);
    string GetIpClient(IPAddress remoteIpAddress);

}
