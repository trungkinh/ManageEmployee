using System.ComponentModel.DataAnnotations;
using ManageEmployee.Entities.BaseEntities;
using ManageEmployee.Entities.Enumerations;

namespace ManageEmployee.Entities;

public class Slider : BaseEntity
{
    public int Id { get; set; }
    public LanguageEnum Type { get; set; }

    [StringLength(36)]
    public string? Name { get; set; }

    public string? Img { get; set; }
    public int AdsensePosition { get; set; }
}