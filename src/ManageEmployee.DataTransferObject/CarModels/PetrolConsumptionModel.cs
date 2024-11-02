﻿namespace ManageEmployee.DataTransferObject.CarModels;
public class PetrolConsumptionModel
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public int UserId { get; set; }
    public int CarId { get; set; }
    public double PetroPrice { get; set; }
    public double KmFrom { get; set; }
    public double KmTo { get; set; }
    public string? LocationFrom { get; set; }
    public string? LocationTo { get; set; }
    public double AdvanceAmount { get; set; }
    public string? Note { get; set; }
    public int? RoadRouteId { get; set; }
    public List<PetrolConsumptionPoliceCheckPointModel>? Points { get; set; }
}
