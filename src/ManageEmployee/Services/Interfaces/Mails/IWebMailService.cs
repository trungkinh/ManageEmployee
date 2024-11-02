﻿using ManageEmployee.DataTransferObject.PagingRequest;
using ManageEmployee.DataTransferObject.PagingResultModels;
using ManageEmployee.Entities.WebEntities;

namespace ManageEmployee.Services.Interfaces.Mails;

public interface IWebMailService
{
    Task CreateMail(string email, int? userId);
    Task<PagingResult<WebMail>> GetPaging(PagingRequestModel param);
}
