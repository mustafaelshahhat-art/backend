using System;
using System.Collections.Generic;

namespace Domain.Entities;

public class Governorate : BaseEntity
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    // Navigation
    public ICollection<City> Cities { get; set; } = new List<City>();
    public ICollection<User> Users { get; set; } = new List<User>();
}
