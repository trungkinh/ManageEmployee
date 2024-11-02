﻿using ManageEmployee.Entities.BaseEntities;

namespace ManageEmployee.Entities.DecideEntities;

public class DecisionType : BaseEntity
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool Status { get; set; }
}
