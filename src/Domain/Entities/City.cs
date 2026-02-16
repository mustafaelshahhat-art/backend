using System;
using System.Collections.Generic;

namespace Domain.Entities;

public class City : BaseEntity
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public Guid GovernorateId { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    // Navigation
    public Governorate Governorate { get; set; } = null!;
    public ICollection<Area> Areas { get; set; } = new List<Area>();
    public ICollection<User> Users { get; set; } = new List<User>();
}
