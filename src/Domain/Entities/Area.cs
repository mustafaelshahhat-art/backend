using System;
using System.Collections.Generic;

namespace Domain.Entities;

public class Area : BaseEntity
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public Guid CityId { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    // Navigation
    public City City { get; set; } = null!;
    public ICollection<User> Users { get; set; } = new List<User>();
}
