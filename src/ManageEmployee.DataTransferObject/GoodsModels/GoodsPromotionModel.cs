﻿namespace ManageEmployee.DataTransferObject.GoodsModels;

public class GoodsPromotionModel
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public double Value { get; set; }
    public DateTime FromAt { get; set; }
    public DateTime ToAt { get; set; }
    public string? FileLink { get; set; }
    public string? Address { get; set; }
    public string? CustomerNote { get; set; }
    public string? Note { get; set; }
}