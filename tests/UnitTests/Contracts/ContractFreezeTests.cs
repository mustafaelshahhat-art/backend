using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Common.Models;
using Application.Contracts.Admin.Responses;
using Application.Contracts.Common;
using Application.Contracts.Notifications.Responses;
using Application.Contracts.Settings.Responses;
using Application.Contracts.Teams.Responses;
using Application.DTOs.Auth;
using Application.DTOs.Matches;
using Application.DTOs.Teams;
using Application.DTOs.Tournaments;
using Application.DTOs.Users;
using FluentAssertions;
using Xunit;

namespace UnitTests.Contracts;

/// <summary>
/// CONTRACT FREEZE TESTS — Phase 2 Lock
/// These tests verify the exact JSON shape of all API response contracts.
/// If any test breaks, a contract-breaking change has been introduced.
/// DO NOT modify these tests without frontend team approval.
/// </summary>
public class ContractFreezeTests
{
    /// <summary>
    /// Matches the JsonSerializerOptions configured in Program.cs:
    /// - camelCase property naming (ASP.NET default)
    /// - JsonStringEnumConverter (string enum values)
    /// - WhenWritingNull (skip null properties)
    /// </summary>
    private static readonly JsonSerializerOptions ApiJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    #region Paging Contract

    [Fact]
    public void PagedResult_Serializes_To_Correct_Shape()
    {
        // Arrange — the LIVE paging type used by 24 endpoints
        var paged = new PagedResult<string>(
            items: new List<string> { "a", "b" },
            count: 50,
            pageNumber: 2,
            pageSize: 10
        );

        // Act
        var json = JsonSerializer.Serialize(paged, ApiJsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Assert — locked shape: { items, pageNumber, pageSize, totalCount, totalPages }
        root.GetProperty("items").GetArrayLength().Should().Be(2);
        root.GetProperty("pageNumber").GetInt32().Should().Be(2);
        root.GetProperty("pageSize").GetInt32().Should().Be(10);
        root.GetProperty("totalCount").GetInt32().Should().Be(50);
        root.GetProperty("totalPages").GetInt32().Should().Be(5);

        // Ensure NO extra properties (exactly 5)
        root.EnumerateObject().Count().Should().Be(5,
            "PagedResult must have exactly 5 properties: items, pageNumber, pageSize, totalCount, totalPages");
    }

    [Fact]
    public void PagedResult_PropertyNames_Are_CamelCase()
    {
        var paged = new PagedResult<int>(new List<int> { 1 }, 1, 1, 10);
        var json = JsonSerializer.Serialize(paged, ApiJsonOptions);

        json.Should().Contain("\"items\"");
        json.Should().Contain("\"pageNumber\"");
        json.Should().Contain("\"pageSize\"");
        json.Should().Contain("\"totalCount\"");
        json.Should().Contain("\"totalPages\"");

        // Must NOT contain PascalCase
        json.Should().NotContain("\"Items\"");
        json.Should().NotContain("\"PageNumber\"");
        json.Should().NotContain("\"TotalCount\"");
    }

    [Fact]
    public void PagedResponse_Matches_PagedResult_Shape()
    {
        // PagedResponse<T> should produce identical JSON to PagedResult<T>
        var result = new PagedResult<string>(new List<string> { "x" }, 100, 3, 25);
        var response = PagedResponse<string>.FromPagedResult(result);

        var resultJson = JsonSerializer.Serialize(result, ApiJsonOptions);
        var responseJson = JsonSerializer.Serialize(response, ApiJsonOptions);

        var resultDoc = JsonDocument.Parse(resultJson);
        var responseDoc = JsonDocument.Parse(responseJson);

        // Same properties, same values
        responseDoc.RootElement.GetProperty("pageNumber").GetInt32()
            .Should().Be(resultDoc.RootElement.GetProperty("pageNumber").GetInt32());
        responseDoc.RootElement.GetProperty("pageSize").GetInt32()
            .Should().Be(resultDoc.RootElement.GetProperty("pageSize").GetInt32());
        responseDoc.RootElement.GetProperty("totalCount").GetInt32()
            .Should().Be(resultDoc.RootElement.GetProperty("totalCount").GetInt32());
        responseDoc.RootElement.GetProperty("totalPages").GetInt32()
            .Should().Be(resultDoc.RootElement.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public void PagedResult_TotalPages_Computes_Correctly()
    {
        new PagedResult<int>(new(), 0, 1, 10).TotalPages.Should().Be(0);
        new PagedResult<int>(new(), 1, 1, 10).TotalPages.Should().Be(1);
        new PagedResult<int>(new(), 10, 1, 10).TotalPages.Should().Be(1);
        new PagedResult<int>(new(), 11, 1, 10).TotalPages.Should().Be(2);
        new PagedResult<int>(new(), 100, 1, 10).TotalPages.Should().Be(10);
    }

    #endregion

    #region MessageResponse Contract

    [Fact]
    public void MessageResponse_Serializes_To_Correct_Shape()
    {
        var msg = new MessageResponse("تم بنجاح");
        var json = JsonSerializer.Serialize(msg, ApiJsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("message").GetString().Should().Be("تم بنجاح");
        root.EnumerateObject().Count().Should().Be(1,
            "MessageResponse must have exactly 1 property: message");
    }

    #endregion

    #region ErrorResponse Contract

    [Fact]
    public void ErrorResponse_Serializes_To_Correct_Shape()
    {
        var error = new ErrorResponse("NOT_FOUND", "العنصر غير موجود");
        var json = JsonSerializer.Serialize(error, ApiJsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("code").GetString().Should().Be("NOT_FOUND");
        root.GetProperty("message").GetString().Should().Be("العنصر غير موجود");

        // details should be omitted when null (WhenWritingNull)
        root.TryGetProperty("details", out _).Should().BeFalse(
            "null details should be omitted per WhenWritingNull policy");

        root.EnumerateObject().Count().Should().Be(2);
    }

    [Fact]
    public void ErrorResponse_With_Details_Includes_Details()
    {
        var error = new ErrorResponse("VALIDATION_ERROR", "Check input", new { field = "email" });
        var json = JsonSerializer.Serialize(error, ApiJsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
        root.GetProperty("message").GetString().Should().Be("Check input");
        root.GetProperty("details").GetProperty("field").GetString().Should().Be("email");
        root.EnumerateObject().Count().Should().Be(3);
    }

    [Fact]
    public void ValidationErrorResponse_Has_Dictionary_Details()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["email"] = new[] { "Email is required", "Email format invalid" },
            ["password"] = new[] { "Password too short" }
        };
        var response = new ValidationErrorResponse("Validation failed", errors);
        var json = JsonSerializer.Serialize(response, ApiJsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
        root.GetProperty("details").GetProperty("email").GetArrayLength().Should().Be(2);
        root.GetProperty("details").GetProperty("password").GetArrayLength().Should().Be(1);
    }

    #endregion

    #region AuthResponse Contract (Live — DTOs.Auth)

    [Fact]
    public void AuthResponse_Serializes_To_Correct_Shape()
    {
        // The LIVE AuthResponse from Application.DTOs.Auth — used by AuthService
        var auth = new AuthResponse
        {
            Token = "jwt-token",
            RefreshToken = "refresh-token",
            User = new UserDto
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                DisplayId = "USR-001",
                Name = "أحمد",
                Email = "ahmed@test.com",
                Role = "Player",
                Status = "Active",
                IsEmailVerified = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        var json = JsonSerializer.Serialize(auth, ApiJsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Top-level shape
        root.GetProperty("token").GetString().Should().Be("jwt-token");
        root.GetProperty("refreshToken").GetString().Should().Be("refresh-token");

        // User sub-object must be typed (not object?)
        var user = root.GetProperty("user");
        user.GetProperty("id").GetString().Should().Be("11111111-1111-1111-1111-111111111111");
        user.GetProperty("displayId").GetString().Should().Be("USR-001");
        user.GetProperty("name").GetString().Should().Be("أحمد");
        user.GetProperty("email").GetString().Should().Be("ahmed@test.com");
        user.GetProperty("role").GetString().Should().Be("Player");
        user.GetProperty("status").GetString().Should().Be("Active");
        user.GetProperty("isEmailVerified").GetBoolean().Should().BeTrue();
        user.GetProperty("createdAt").ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public void AuthResponse_User_Is_UserDto_Not_Object()
    {
        // Verify User property is typed UserDto — ensures "object? User" regression is caught
        var userProperty = typeof(AuthResponse).GetProperty("User");
        userProperty.Should().NotBeNull();
        userProperty!.PropertyType.Should().Be(typeof(UserDto),
            "AuthResponse.User must be typed as UserDto, not object");
    }

    #endregion

    #region NotificationResponse Contracts

    [Fact]
    public void UnreadCountResponse_Serializes_To_Correct_Shape()
    {
        var response = new UnreadCountResponse(42);
        var json = JsonSerializer.Serialize(response, ApiJsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("count").GetInt32().Should().Be(42);
        root.EnumerateObject().Count().Should().Be(1,
            "UnreadCountResponse must have exactly 1 property: count");
    }

    #endregion

    #region Teams Contracts

    [Fact]
    public void RemovePlayerResponse_Serializes_To_Correct_Shape()
    {
        var response = new RemovePlayerResponse
        {
            TeamRemoved = true,
            PlayerId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            TeamId = Guid.Parse("33333333-3333-3333-3333-333333333333")
        };

        var json = JsonSerializer.Serialize(response, ApiJsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("teamRemoved").GetBoolean().Should().BeTrue();
        root.GetProperty("playerId").GetString().Should().Be("22222222-2222-2222-2222-222222222222");
        root.GetProperty("teamId").GetString().Should().Be("33333333-3333-3333-3333-333333333333");
        root.EnumerateObject().Count().Should().Be(3);
    }

    #endregion

    #region Admin Contracts

    [Fact]
    public void DeadLetterListResponse_Serializes_To_Correct_Shape()
    {
        var messages = new List<DeadLetterMessageDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                EventType = "UserCreated",
                Payload = "{}",
                Status = "Failed",
                RetryCount = 3,
                Error = "Timeout",
                CreatedAt = DateTime.UtcNow
            }
        };
        var response = new global::Application.Common.Models.PagedResult<DeadLetterMessageDto>(messages, 1, 1, 20);

        var json = JsonSerializer.Serialize(response, ApiJsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("items").GetArrayLength().Should().Be(1);
        root.GetProperty("totalCount").GetInt32().Should().Be(1);
        root.GetProperty("pageNumber").GetInt32().Should().Be(1);
        root.GetProperty("pageSize").GetInt32().Should().Be(20);

        var msg = root.GetProperty("items")[0];
        msg.GetProperty("id").ValueKind.Should().Be(JsonValueKind.String);
        msg.GetProperty("eventType").GetString().Should().Be("UserCreated");
        msg.GetProperty("payload").GetString().Should().Be("{}");
        msg.GetProperty("status").GetString().Should().Be("Failed");
        msg.GetProperty("retryCount").GetInt32().Should().Be(3);
        msg.GetProperty("error").GetString().Should().Be("Timeout");
        msg.GetProperty("createdAt").ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public void ClearDeadLettersResponse_Serializes_To_Correct_Shape()
    {
        var response = new ClearDeadLettersResponse { Count = 5, Message = "Cleared" };
        var json = JsonSerializer.Serialize(response, ApiJsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("count").GetInt32().Should().Be(5);
        root.GetProperty("message").GetString().Should().Be("Cleared");
        root.EnumerateObject().Count().Should().Be(2);
    }

    #endregion

    #region Settings Contracts

    [Fact]
    public void MaintenanceStatusResponse_Serializes_To_Correct_Shape()
    {
        var response = new MaintenanceStatusResponse
        {
            MaintenanceMode = true,
            UpdatedAt = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc)
        };

        var json = JsonSerializer.Serialize(response, ApiJsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("maintenanceMode").GetBoolean().Should().BeTrue();
        root.GetProperty("updatedAt").ValueKind.Should().Be(JsonValueKind.String);
        root.EnumerateObject().Count().Should().Be(2);
    }

    [Fact]
    public void MaintenanceStatusResponse_Without_UpdatedAt_Omits_Null()
    {
        var response = new MaintenanceStatusResponse { MaintenanceMode = false };
        var json = JsonSerializer.Serialize(response, ApiJsonOptions);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("maintenanceMode").GetBoolean().Should().BeFalse();
        root.TryGetProperty("updatedAt", out _).Should().BeFalse(
            "null updatedAt should be omitted per WhenWritingNull policy");
        root.EnumerateObject().Count().Should().Be(1);
    }

    #endregion

    #region UserDto Contract

    [Fact]
    public void UserDto_Has_All_Required_Properties()
    {
        // Lock the UserDto property set — adding/removing properties is a contract break
        var expectedProperties = new[]
        {
            "Id", "DisplayId", "Name", "Email", "Role", "Status",
            "Phone", "Age", "NationalId",
            "GovernorateId", "GovernorateNameAr",
            "CityId", "CityNameAr",
            "AreaId", "AreaNameAr",
            "IdFrontUrl", "IdBackUrl",
            "TeamId", "TeamName", "TeamRole",
            "JoinedTeamIds", "IsEmailVerified", "CreatedAt", "Activities"
        };

        var actualProperties = typeof(UserDto).GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToArray();

        actualProperties.Should().BeEquivalentTo(expectedProperties,
            "UserDto property set is locked — changes require frontend alignment");
    }

    [Fact]
    public void UserPublicDto_Has_All_Required_Properties()
    {
        var expectedProperties = new[]
        {
            "Id", "DisplayId", "Name", "Role",
            "GovernorateId", "GovernorateNameAr",
            "CityId", "CityNameAr",
            "TeamId", "TeamName", "TeamRole",
            "IsEmailVerified", "Status"
        };

        var actualProperties = typeof(UserPublicDto).GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToArray();

        actualProperties.Should().BeEquivalentTo(expectedProperties,
            "UserPublicDto property set is locked — changes require frontend alignment");
    }

    #endregion

    #region JSON Serialization Standards

    [Fact]
    public void Enums_Serialize_As_Strings()
    {
        // Verify JsonStringEnumConverter works
        var obj = new { status = global::Domain.Enums.UserStatus.Active };
        var json = JsonSerializer.Serialize(obj, ApiJsonOptions);
        json.Should().Contain("\"Active\"");
        json.Should().NotContain("\"1\"");
    }

    [Fact]
    public void Null_Properties_Are_Omitted()
    {
        var dto = new UserDto
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Email = "test@test.com",
            DisplayId = "T-1",
            Role = "Player",
            Status = "Active",
            // Phone, NationalId, etc. are null
        };

        var json = JsonSerializer.Serialize(dto, ApiJsonOptions);
        json.Should().NotContain("\"phone\"");
        json.Should().NotContain("\"nationalId\"");
        json.Should().NotContain("\"idFrontUrl\"");
        json.Should().NotContain("\"idBackUrl\"");
    }

    [Fact]
    public void CamelCase_Naming_Applied()
    {
        var dto = new MessageResponse("test");
        var json = JsonSerializer.Serialize(dto, ApiJsonOptions);

        json.Should().Contain("\"message\"");
        json.Should().NotContain("\"Message\"");
    }

    #endregion

    #region TournamentDto Contract

    [Fact]
    public void TournamentDto_Has_All_Required_Properties()
    {
        var expectedProperties = new[]
        {
            "Id", "Name", "NameAr", "NameEn", "CreatorUserId", "ImageUrl",
            "Status", "Mode", "StartDate", "EndDate", "RegistrationDeadline",
            "EntryFee", "MaxTeams", "MinTeams", "CurrentTeams",
            "Location", "Description", "Rules", "Prizes", "Format", "MatchType",
            "NumberOfGroups", "WalletNumber", "InstaPayNumber", "IsHomeAwayEnabled",
            "PaymentMethodsJson", "Registrations",
            "WinnerTeamId", "WinnerTeamName", "RequiresAdminIntervention",
            "AllowLateRegistration", "LateRegistrationMode", "SchedulingMode",
            "OpeningMatchHomeTeamId", "OpeningMatchAwayTeamId", "OpeningMatchId",
            "AdminId", "CreatedAt", "UpdatedAt"
        };

        var actualProperties = typeof(TournamentDto).GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToArray();

        actualProperties.Should().BeEquivalentTo(expectedProperties,
            "TournamentDto property set is locked — changes require frontend alignment");
    }

    [Fact]
    public void TournamentDto_Key_Properties_Have_Correct_Types()
    {
        var t = typeof(TournamentDto);

        t.GetProperty("Id")!.PropertyType.Should().Be(typeof(Guid));
        t.GetProperty("Name")!.PropertyType.Should().Be(typeof(string));
        t.GetProperty("Status")!.PropertyType.Should().Be(typeof(string));
        t.GetProperty("EntryFee")!.PropertyType.Should().Be(typeof(decimal));
        t.GetProperty("MaxTeams")!.PropertyType.Should().Be(typeof(int));
        t.GetProperty("CurrentTeams")!.PropertyType.Should().Be(typeof(int));
        t.GetProperty("StartDate")!.PropertyType.Should().Be(typeof(DateTime));
        t.GetProperty("EndDate")!.PropertyType.Should().Be(typeof(DateTime));
        t.GetProperty("IsHomeAwayEnabled")!.PropertyType.Should().Be(typeof(bool));
        t.GetProperty("Registrations")!.PropertyType.Should().Be(typeof(List<TeamRegistrationDto>));
        t.GetProperty("WinnerTeamId")!.PropertyType.Should().Be(typeof(Guid?));
        t.GetProperty("CreatedAt")!.PropertyType.Should().Be(typeof(DateTime));
    }

    [Fact]
    public void TournamentDto_Serializes_CamelCase_Properties()
    {
        var dto = new TournamentDto
        {
            Id = Guid.NewGuid(),
            Name = "رمضان كأس",
            Status = "Active",
            EntryFee = 100m,
            MaxTeams = 16,
            CurrentTeams = 4,
            Location = "Cairo",
            Description = "Test",
            Rules = "Standard",
            Prizes = "Trophy",
            Format = "Groups",
            MatchType = "SingleLeg",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30),
            RegistrationDeadline = DateTime.UtcNow.AddDays(7)
        };

        var json = JsonSerializer.Serialize(dto, ApiJsonOptions);

        json.Should().Contain("\"id\"");
        json.Should().Contain("\"name\"");
        json.Should().Contain("\"status\"");
        json.Should().Contain("\"entryFee\"");
        json.Should().Contain("\"maxTeams\"");
        json.Should().Contain("\"currentTeams\"");
        json.Should().Contain("\"startDate\"");
        json.Should().Contain("\"isHomeAwayEnabled\"");
        json.Should().Contain("\"registrations\"");

        // Must NOT contain PascalCase
        json.Should().NotContain("\"Id\"");
        json.Should().NotContain("\"Name\"");
        json.Should().NotContain("\"MaxTeams\"");
        json.Should().NotContain("\"StartDate\"");
    }

    #endregion

    #region TeamDto Contract

    [Fact]
    public void TeamDto_Has_All_Required_Properties()
    {
        var expectedProperties = new[]
        {
            "Id", "Name", "CaptainName", "Founded", "City",
            "IsActive", "PlayerCount", "MaxPlayers", "Stats", "Players"
        };

        var actualProperties = typeof(TeamDto).GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToArray();

        actualProperties.Should().BeEquivalentTo(expectedProperties,
            "TeamDto property set is locked — changes require frontend alignment");
    }

    [Fact]
    public void TeamDto_Key_Properties_Have_Correct_Types()
    {
        var t = typeof(TeamDto);

        t.GetProperty("Id")!.PropertyType.Should().Be(typeof(Guid));
        t.GetProperty("Name")!.PropertyType.Should().Be(typeof(string));
        t.GetProperty("CaptainName")!.PropertyType.Should().Be(typeof(string));
        t.GetProperty("Founded")!.PropertyType.Should().Be(typeof(string));
        t.GetProperty("City")!.PropertyType.Should().Be(typeof(string));
        t.GetProperty("IsActive")!.PropertyType.Should().Be(typeof(bool));
        t.GetProperty("PlayerCount")!.PropertyType.Should().Be(typeof(int));
        t.GetProperty("MaxPlayers")!.PropertyType.Should().Be(typeof(int));
        t.GetProperty("Stats")!.PropertyType.Should().Be(typeof(TeamStatsDto));
        t.GetProperty("Players")!.PropertyType.Should().Be(typeof(List<PlayerDto>));
    }

    [Fact]
    public void TeamDto_Serializes_CamelCase_Properties()
    {
        var dto = new TeamDto
        {
            Id = Guid.NewGuid(),
            Name = "الأهلي",
            CaptainName = "أحمد",
            Founded = "2024",
            IsActive = true,
            PlayerCount = 11,
            MaxPlayers = 25
        };

        var json = JsonSerializer.Serialize(dto, ApiJsonOptions);

        json.Should().Contain("\"id\"");
        json.Should().Contain("\"name\"");
        json.Should().Contain("\"captainName\"");
        json.Should().Contain("\"founded\"");
        json.Should().Contain("\"isActive\"");
        json.Should().Contain("\"playerCount\"");
        json.Should().Contain("\"maxPlayers\"");
        json.Should().Contain("\"players\"");

        // Must NOT contain PascalCase
        json.Should().NotContain("\"CaptainName\"");
        json.Should().NotContain("\"IsActive\"");
        json.Should().NotContain("\"PlayerCount\"");
    }

    #endregion

    #region MatchDto Contract

    [Fact]
    public void MatchDto_Has_All_Required_Properties()
    {
        var expectedProperties = new[]
        {
            "Id", "TournamentId", "HomeTeamId", "HomeTeamName",
            "AwayTeamId", "AwayTeamName", "HomeScore", "AwayScore",
            "GroupId", "RoundNumber", "StageName", "Status", "Date",
            "TournamentName", "TournamentCreatorId", "Events"
        };

        var actualProperties = typeof(MatchDto).GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToArray();

        actualProperties.Should().BeEquivalentTo(expectedProperties,
            "MatchDto property set is locked — changes require frontend alignment");
    }

    [Fact]
    public void MatchDto_Key_Properties_Have_Correct_Types()
    {
        var t = typeof(MatchDto);

        t.GetProperty("Id")!.PropertyType.Should().Be(typeof(Guid));
        t.GetProperty("TournamentId")!.PropertyType.Should().Be(typeof(Guid));
        t.GetProperty("HomeTeamId")!.PropertyType.Should().Be(typeof(Guid));
        t.GetProperty("HomeTeamName")!.PropertyType.Should().Be(typeof(string));
        t.GetProperty("AwayTeamId")!.PropertyType.Should().Be(typeof(Guid));
        t.GetProperty("AwayTeamName")!.PropertyType.Should().Be(typeof(string));
        t.GetProperty("HomeScore")!.PropertyType.Should().Be(typeof(int));
        t.GetProperty("AwayScore")!.PropertyType.Should().Be(typeof(int));
        t.GetProperty("GroupId")!.PropertyType.Should().Be(typeof(int?));
        t.GetProperty("RoundNumber")!.PropertyType.Should().Be(typeof(int?));
        t.GetProperty("Status")!.PropertyType.Should().Be(typeof(string));
        t.GetProperty("Date")!.PropertyType.Should().Be(typeof(DateTime?));
        t.GetProperty("Events")!.PropertyType.Should().Be(typeof(List<MatchEventDto>));
    }

    [Fact]
    public void MatchDto_Serializes_CamelCase_Properties()
    {
        var dto = new MatchDto
        {
            Id = Guid.NewGuid(),
            TournamentId = Guid.NewGuid(),
            HomeTeamId = Guid.NewGuid(),
            HomeTeamName = "Team A",
            AwayTeamId = Guid.NewGuid(),
            AwayTeamName = "Team B",
            HomeScore = 2,
            AwayScore = 1,
            Status = "Completed"
        };

        var json = JsonSerializer.Serialize(dto, ApiJsonOptions);

        json.Should().Contain("\"id\"");
        json.Should().Contain("\"tournamentId\"");
        json.Should().Contain("\"homeTeamId\"");
        json.Should().Contain("\"homeTeamName\"");
        json.Should().Contain("\"awayTeamId\"");
        json.Should().Contain("\"awayTeamName\"");
        json.Should().Contain("\"homeScore\"");
        json.Should().Contain("\"awayScore\"");
        json.Should().Contain("\"status\"");
        json.Should().Contain("\"events\"");

        // Must NOT contain PascalCase
        json.Should().NotContain("\"HomeTeamId\"");
        json.Should().NotContain("\"AwayTeamName\"");
        json.Should().NotContain("\"HomeScore\"");
    }

    #endregion

    #region PlayerDto Contract

    [Fact]
    public void PlayerDto_Has_All_Required_Properties()
    {
        var expectedProperties = new[]
        {
            "Id", "Name", "DisplayId", "Number", "Position", "Status",
            "Goals", "Assists", "YellowCards", "RedCards",
            "TeamId", "UserId", "TeamRole"
        };

        var actualProperties = typeof(PlayerDto).GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToArray();

        actualProperties.Should().BeEquivalentTo(expectedProperties,
            "PlayerDto property set is locked — changes require frontend alignment");
    }

    [Fact]
    public void PlayerDto_Key_Properties_Have_Correct_Types()
    {
        var t = typeof(PlayerDto);

        t.GetProperty("Id")!.PropertyType.Should().Be(typeof(Guid));
        t.GetProperty("Name")!.PropertyType.Should().Be(typeof(string));
        t.GetProperty("DisplayId")!.PropertyType.Should().Be(typeof(string));
        t.GetProperty("Number")!.PropertyType.Should().Be(typeof(int));
        t.GetProperty("Position")!.PropertyType.Should().Be(typeof(string));
        t.GetProperty("Status")!.PropertyType.Should().Be(typeof(string));
        t.GetProperty("Goals")!.PropertyType.Should().Be(typeof(int));
        t.GetProperty("Assists")!.PropertyType.Should().Be(typeof(int));
        t.GetProperty("YellowCards")!.PropertyType.Should().Be(typeof(int));
        t.GetProperty("RedCards")!.PropertyType.Should().Be(typeof(int));
        t.GetProperty("TeamId")!.PropertyType.Should().Be(typeof(Guid));
        t.GetProperty("UserId")!.PropertyType.Should().Be(typeof(Guid?));
        t.GetProperty("TeamRole")!.PropertyType.Should().Be(typeof(global::Domain.Enums.TeamRole));
    }

    [Fact]
    public void PlayerDto_Serializes_CamelCase_Properties()
    {
        var dto = new PlayerDto
        {
            Id = Guid.NewGuid(),
            Name = "محمد",
            DisplayId = "PLR-001",
            Number = 10,
            Position = "Forward",
            Status = "Active",
            Goals = 5,
            Assists = 3,
            YellowCards = 1,
            RedCards = 0,
            TeamId = Guid.NewGuid(),
            TeamRole = global::Domain.Enums.TeamRole.Member
        };

        var json = JsonSerializer.Serialize(dto, ApiJsonOptions);

        json.Should().Contain("\"id\"");
        json.Should().Contain("\"name\"");
        json.Should().Contain("\"displayId\"");
        json.Should().Contain("\"number\"");
        json.Should().Contain("\"position\"");
        json.Should().Contain("\"goals\"");
        json.Should().Contain("\"assists\"");
        json.Should().Contain("\"yellowCards\"");
        json.Should().Contain("\"redCards\"");
        json.Should().Contain("\"teamId\"");
        json.Should().Contain("\"teamRole\"");

        // Must NOT contain PascalCase
        json.Should().NotContain("\"DisplayId\"");
        json.Should().NotContain("\"YellowCards\"");
        json.Should().NotContain("\"TeamRole\"");
    }

    [Fact]
    public void PlayerDto_TeamRole_Serializes_As_String()
    {
        var dto = new PlayerDto
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            DisplayId = "PLR-001",
            Position = "GK",
            Status = "Active",
            TeamId = Guid.NewGuid(),
            TeamRole = global::Domain.Enums.TeamRole.Captain
        };

        var json = JsonSerializer.Serialize(dto, ApiJsonOptions);
        json.Should().Contain("\"Captain\"");
    }

    #endregion
}
