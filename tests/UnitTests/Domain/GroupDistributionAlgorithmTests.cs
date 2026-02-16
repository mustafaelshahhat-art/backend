using Domain.Services;
using FluentAssertions;
using Xunit;

namespace UnitTests.Domain;

public class GroupDistributionAlgorithmTests
{
    // ─── Even Distribution (no opening teams) ───

    [Fact]
    public void Distribute_16Teams_4Groups_ShouldBe_4_4_4_4()
    {
        var teams = CreateTeamIds(16);
        var result = GroupDistributionAlgorithm.Distribute(teams, 4);

        result.Should().HaveCount(4);
        result[1].Should().HaveCount(4);
        result[2].Should().HaveCount(4);
        result[3].Should().HaveCount(4);
        result[4].Should().HaveCount(4);
    }

    [Fact]
    public void Distribute_10Teams_4Groups_ShouldBe_3_3_2_2()
    {
        var teams = CreateTeamIds(10);
        var result = GroupDistributionAlgorithm.Distribute(teams, 4);

        result.Should().HaveCount(4);
        var sizes = result.Values.Select(g => g.Count).OrderByDescending(c => c).ToList();
        sizes.Should().BeEquivalentTo(new[] { 3, 3, 2, 2 });
    }

    [Fact]
    public void Distribute_18Teams_4Groups_ShouldBe_5_5_4_4()
    {
        var teams = CreateTeamIds(18);
        var result = GroupDistributionAlgorithm.Distribute(teams, 4);

        result.Should().HaveCount(4);
        var sizes = result.Values.Select(g => g.Count).OrderByDescending(c => c).ToList();
        sizes.Should().BeEquivalentTo(new[] { 5, 5, 4, 4 });
    }

    [Fact]
    public void Distribute_7Teams_4Groups_ShouldBe_2_2_2_1()
    {
        var teams = CreateTeamIds(7);
        var result = GroupDistributionAlgorithm.Distribute(teams, 4);

        result.Should().HaveCount(4);
        var sizes = result.Values.Select(g => g.Count).OrderByDescending(c => c).ToList();
        sizes.Should().BeEquivalentTo(new[] { 2, 2, 2, 1 });
    }

    [Fact]
    public void Distribute_32Teams_8Groups_ShouldBe_AllEqual_4()
    {
        var teams = CreateTeamIds(32);
        var result = GroupDistributionAlgorithm.Distribute(teams, 8);

        result.Should().HaveCount(8);
        foreach (var group in result.Values)
        {
            group.Should().HaveCount(4);
        }
    }

    [Fact]
    public void Distribute_64Teams_8Groups_ShouldBe_AllEqual_8()
    {
        var teams = CreateTeamIds(64);
        var result = GroupDistributionAlgorithm.Distribute(teams, 8);

        result.Should().HaveCount(8);
        foreach (var group in result.Values)
        {
            group.Should().HaveCount(8);
        }
    }

    // ─── Odd Numbers ───

    [Fact]
    public void Distribute_13Teams_4Groups_ShouldBe_4_3_3_3()
    {
        var teams = CreateTeamIds(13);
        var result = GroupDistributionAlgorithm.Distribute(teams, 4);

        result.Should().HaveCount(4);
        var sizes = result.Values.Select(g => g.Count).OrderByDescending(c => c).ToList();
        sizes.Should().BeEquivalentTo(new[] { 4, 3, 3, 3 });
    }

    [Fact]
    public void Distribute_5Teams_3Groups_ShouldBe_2_2_1()
    {
        var teams = CreateTeamIds(5);
        var result = GroupDistributionAlgorithm.Distribute(teams, 3);

        result.Should().HaveCount(3);
        var sizes = result.Values.Select(g => g.Count).OrderByDescending(c => c).ToList();
        sizes.Should().BeEquivalentTo(new[] { 2, 2, 1 });
    }

    // ─── Edge Case: Teams < Groups — No Empty Groups ───

    [Fact]
    public void Distribute_3Teams_4Groups_ShouldCreate_Only3Groups()
    {
        var teams = CreateTeamIds(3);
        var result = GroupDistributionAlgorithm.Distribute(teams, 4);

        // Should reduce to 3 groups, no empty groups
        result.Should().HaveCount(3);
        foreach (var group in result.Values)
        {
            group.Should().HaveCount(1);
        }
    }

    [Fact]
    public void Distribute_1Team_4Groups_ShouldCreate_Only1Group()
    {
        var teams = CreateTeamIds(1);
        var result = GroupDistributionAlgorithm.Distribute(teams, 4);

        result.Should().HaveCount(1);
        result[1].Should().HaveCount(1);
    }

    [Fact]
    public void Distribute_2Teams_4Groups_ShouldCreate_Only2Groups()
    {
        var teams = CreateTeamIds(2);
        var result = GroupDistributionAlgorithm.Distribute(teams, 4);

        result.Should().HaveCount(2);
        result[1].Should().HaveCount(1);
        result[2].Should().HaveCount(1);
    }

    // ─── All Teams Assigned Exactly Once ───

    [Theory]
    [InlineData(10, 4)]
    [InlineData(16, 4)]
    [InlineData(18, 4)]
    [InlineData(7, 4)]
    [InlineData(32, 8)]
    [InlineData(64, 8)]
    [InlineData(3, 4)]
    [InlineData(100, 10)]
    [InlineData(1, 1)]
    public void Distribute_AllTeamsAssignedExactlyOnce(int teamCount, int groupCount)
    {
        var teams = CreateTeamIds(teamCount);
        var result = GroupDistributionAlgorithm.Distribute(teams, groupCount);

        var allAssigned = result.Values.SelectMany(g => g).ToList();
        allAssigned.Should().HaveCount(teamCount);
        allAssigned.Distinct().Should().HaveCount(teamCount);
        allAssigned.Should().BeEquivalentTo(teams);
    }

    // ─── Max Difference Between Groups Is At Most 1 ───

    [Theory]
    [InlineData(10, 4)]
    [InlineData(11, 4)]
    [InlineData(13, 4)]
    [InlineData(15, 4)]
    [InlineData(17, 6)]
    [InlineData(33, 8)]
    [InlineData(100, 7)]
    [InlineData(99, 10)]
    public void Distribute_MaxDifferenceIsAtMost1(int teamCount, int groupCount)
    {
        var teams = CreateTeamIds(teamCount);
        var result = GroupDistributionAlgorithm.Distribute(teams, groupCount);

        var sizes = result.Values.Select(g => g.Count).ToList();
        var diff = sizes.Max() - sizes.Min();
        diff.Should().BeLessThanOrEqualTo(1, 
            $"N={teamCount}, G={groupCount}, sizes=[{string.Join(",", sizes)}]");
    }

    // ─── No Empty Groups ───

    [Theory]
    [InlineData(1, 4)]
    [InlineData(2, 4)]
    [InlineData(3, 4)]
    [InlineData(4, 4)]
    [InlineData(5, 4)]
    [InlineData(10, 4)]
    [InlineData(100, 10)]
    public void Distribute_NoEmptyGroups(int teamCount, int groupCount)
    {
        var teams = CreateTeamIds(teamCount);
        var result = GroupDistributionAlgorithm.Distribute(teams, groupCount);

        foreach (var group in result.Values)
        {
            group.Should().NotBeEmpty();
        }
    }

    // ─── Opening Teams ───

    [Fact]
    public void Distribute_WithOpeningTeams_BothInGroup1()
    {
        var teams = CreateTeamIds(16);
        var openA = teams[5];
        var openB = teams[12];

        var result = GroupDistributionAlgorithm.Distribute(teams, 4, openA, openB);

        result[1].Should().Contain(openA);
        result[1].Should().Contain(openB);
    }

    [Fact]
    public void Distribute_WithOpeningTeams_StillBalanced()
    {
        var teams = CreateTeamIds(16);
        var openA = teams[0];
        var openB = teams[1];

        var result = GroupDistributionAlgorithm.Distribute(teams, 4, openA, openB);

        var sizes = result.Values.Select(g => g.Count).ToList();
        var diff = sizes.Max() - sizes.Min();
        diff.Should().BeLessThanOrEqualTo(1);
        result.Values.SelectMany(g => g).Should().HaveCount(16);
    }

    [Fact]
    public void Distribute_WithOpeningTeams_18Teams_4Groups_Balanced()
    {
        var teams = CreateTeamIds(18);
        var openA = teams[3];
        var openB = teams[7];

        var result = GroupDistributionAlgorithm.Distribute(teams, 4, openA, openB);

        result[1].Should().Contain(openA);
        result[1].Should().Contain(openB);

        var sizes = result.Values.Select(g => g.Count).OrderByDescending(c => c).ToList();
        sizes.Should().BeEquivalentTo(new[] { 5, 5, 4, 4 });
    }

    [Fact]
    public void Distribute_WithOpeningTeams_OddCount()
    {
        var teams = CreateTeamIds(11);
        var openA = teams[0];
        var openB = teams[10];

        var result = GroupDistributionAlgorithm.Distribute(teams, 4, openA, openB);

        result[1].Should().Contain(openA);
        result[1].Should().Contain(openB);

        var sizes = result.Values.Select(g => g.Count).ToList();
        var diff = sizes.Max() - sizes.Min();
        diff.Should().BeLessThanOrEqualTo(1);
    }

    // ─── Large Scale ───

    [Fact]
    public void Distribute_500Teams_16Groups_ShouldBeBalanced()
    {
        var teams = CreateTeamIds(500);
        var result = GroupDistributionAlgorithm.Distribute(teams, 16);

        result.Should().HaveCount(16);
        var sizes = result.Values.Select(g => g.Count).ToList();
        sizes.Max().Should().Be(32); // 500/16 = 31 remainder 4 → first 4 get 32
        sizes.Min().Should().Be(31);
        sizes.Sum().Should().Be(500);
    }

    // ─── Validation ───

    [Fact]
    public void Validate_CorrectDistribution_ShouldPass()
    {
        var teams = CreateTeamIds(16);
        var result = GroupDistributionAlgorithm.Distribute(teams, 4);
        var validation = GroupDistributionAlgorithm.Validate(result, teams, 4);

        validation.IsValid.Should().BeTrue();
        validation.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_MissingTeam_ShouldFail()
    {
        var teams = CreateTeamIds(16);
        var result = GroupDistributionAlgorithm.Distribute(teams, 4);
        
        // Tamper: remove a team
        result[1].RemoveAt(0);

        var validation = GroupDistributionAlgorithm.Validate(result, teams, 4);
        validation.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_DuplicateTeam_ShouldFail()
    {
        var teams = CreateTeamIds(8);
        var result = GroupDistributionAlgorithm.Distribute(teams, 2);
        
        // Tamper: add duplicate
        result[1].Add(result[2][0]);

        var validation = GroupDistributionAlgorithm.Validate(result, teams, 2);
        validation.IsValid.Should().BeFalse();
    }

    // ─── Error Cases ───

    [Fact]
    public void Distribute_EmptyTeamList_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            GroupDistributionAlgorithm.Distribute(Array.Empty<Guid>(), 4));
    }

    [Fact]
    public void Distribute_ZeroGroups_ShouldThrow()
    {
        var teams = CreateTeamIds(10);
        Assert.Throws<ArgumentException>(() =>
            GroupDistributionAlgorithm.Distribute(teams, 0));
    }

    [Fact]
    public void Distribute_DuplicateTeams_ShouldThrow()
    {
        var id = Guid.NewGuid();
        var teams = new[] { id, id, Guid.NewGuid() };
        Assert.Throws<ArgumentException>(() =>
            GroupDistributionAlgorithm.Distribute(teams, 2));
    }

    [Fact]
    public void Distribute_OpeningTeamsSame_ShouldThrow()
    {
        var teams = CreateTeamIds(8);
        Assert.Throws<ArgumentException>(() =>
            GroupDistributionAlgorithm.Distribute(teams, 2, teams[0], teams[0]));
    }

    [Fact]
    public void Distribute_OpeningTeamNotInList_ShouldThrow()
    {
        var teams = CreateTeamIds(8);
        Assert.Throws<ArgumentException>(() =>
            GroupDistributionAlgorithm.Distribute(teams, 2, teams[0], Guid.NewGuid()));
    }

    // ─── Determinism ───

    [Fact]
    public void Distribute_SameInput_SameOutput()
    {
        var teams = CreateTeamIds(16);
        var result1 = GroupDistributionAlgorithm.Distribute(teams, 4);
        var result2 = GroupDistributionAlgorithm.Distribute(teams, 4);

        // Same input order → same distribution
        for (int g = 1; g <= 4; g++)
        {
            result1[g].Should().BeEquivalentTo(result2[g]);
        }
    }

    // ─── Helpers ───

    private static Guid[] CreateTeamIds(int count)
    {
        // Use deterministic GUIDs for reproducible tests
        return Enumerable.Range(1, count)
            .Select(i => new Guid($"{i:D8}-0000-0000-0000-000000000000"))
            .ToArray();
    }
}
