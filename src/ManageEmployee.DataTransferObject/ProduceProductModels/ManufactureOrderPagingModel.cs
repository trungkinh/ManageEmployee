﻿namespace ManageEmployee.DataTransferObject.ProduceProductModels;

public class ManufactureOrderPagingModel
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string? Note { get; set; }
    public int UserId { get; set; }
    public bool IsFinished { get; set; }
    public string? ProcedureNumber { get; set; }
    public int? ProcedureStatusId { get; set; }
    public string? ProcedureStatusName { get; set; }
    public bool ShoulDelete { get; set; }
    public bool ShoulNotAccept { get; set; }
    public string? Code { get; set; }
    public string? CanceledNote { get; set; }

}
