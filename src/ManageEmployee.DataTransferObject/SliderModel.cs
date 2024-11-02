using ManageEmployee.Entities.Enumerations;

namespace ManageEmployee.DataTransferObject;

public class SliderModel
{
    public int Id { get; set; }
    public LanguageEnum Type { get; set; }
    public string? Name { get; set; }
    public string? Img { get; set; }
    public DateTime? CreateAt { get; set; }
    public int AdsensePosition { get; set; }
}
