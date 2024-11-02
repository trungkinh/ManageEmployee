using System.ComponentModel.DataAnnotations;
using ManageEmployee.Entities.BaseEntities;
using ManageEmployee.Entities.Enumerations;

namespace ManageEmployee.Entities.IntroduceEntities;

public class Introduce : BaseEntity
{
    public int Id { get; set; }
    public LanguageEnum Type { get; set; }
    [StringLength(36)]
    public string? Name { get; set; }
    public string? Title { get; set; }
    public string? Summary { get; set; }
    public string? Content { get; set; }
    public string? IframeYoutube { get; set; }
    public int? IntroduceTypeId { get; set; }
}
