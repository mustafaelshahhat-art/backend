using System;

namespace Application.DTOs.Locations;

// ── Public DTOs (minimal for dropdowns) ──

public class GovernorateDto
{
    public Guid Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
}

public class CityDto
{
    public Guid Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public Guid GovernorateId { get; set; }
}

public class AreaDto
{
    public Guid Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public Guid CityId { get; set; }
}

// ── Admin DTOs (include status + audit) ──

public class GovernorateAdminDto
{
    public Guid Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public int CityCount { get; set; }
    public int UserCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CityAdminDto
{
    public Guid Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public Guid GovernorateId { get; set; }
    public string GovernorateNameAr { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public int AreaCount { get; set; }
    public int UserCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AreaAdminDto
{
    public Guid Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public Guid CityId { get; set; }
    public string CityNameAr { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public int UserCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── Request DTOs ──

public class CreateGovernorateRequest
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class CreateCityRequest
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public Guid GovernorateId { get; set; }
    public int SortOrder { get; set; }
}

public class CreateAreaRequest
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public Guid CityId { get; set; }
    public int SortOrder { get; set; }
}

public class UpdateLocationRequest
{
    public string? NameAr { get; set; }
    public string? NameEn { get; set; }
    public int? SortOrder { get; set; }
}

// ── User location display ──

public class UserLocationDto
{
    public Guid? GovernorateId { get; set; }
    public string? GovernorateNameAr { get; set; }
    public Guid? CityId { get; set; }
    public string? CityNameAr { get; set; }
    public Guid? AreaId { get; set; }
    public string? AreaNameAr { get; set; }
}
