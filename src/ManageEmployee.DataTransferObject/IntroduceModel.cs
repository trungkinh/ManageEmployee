using ManageEmployee.Entities.Enumerations;

namespace ManageEmployee.DataTransferObject;

public class IntroduceModel
{
    public int Id { get; set; }
    public LanguageEnum Type { get; set; }
    public string? Name { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? Summary { get; set; }

    public string? IframeYoutube { get; set; }
    public DateTime? CreateAt { get; set; }
    public int? IntroduceTypeId { get; set; }
}