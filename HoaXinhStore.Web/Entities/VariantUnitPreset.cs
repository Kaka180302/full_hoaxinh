using System.ComponentModel.DataAnnotations;

namespace HoaXinhStore.Web.Entities;

public class VariantUnitPreset
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(40)]
    public string UnitTemplate { get; set; } = "single";

    [MaxLength(80)]
    public string Unit2Name { get; set; } = "Hộp";
    public int Unit2Factor { get; set; } = 10;

    [MaxLength(80)]
    public string Unit3Name { get; set; } = "Thùng";
    public int Unit3Factor { get; set; } = 20;

    public bool IsActive { get; set; } = true;
}
