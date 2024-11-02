﻿namespace ManageEmployee.DataTransferObject.InOutModels;

public class ShiftUserFilterResponseModel
{
    public ShiftUserFilterResponseModel()
    {
        Details = new List<ShiftUserDetailResponseModel>();
    }

    public int Id { get; set; }
    public string Name { get; set; }
    public string FullName { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public string Note { get; set; }
    public List<ShiftUserDetailResponseModel> Details { get; set; }
}
