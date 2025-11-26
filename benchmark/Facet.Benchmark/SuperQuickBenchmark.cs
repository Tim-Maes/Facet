using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Facet.Benchmark.Models;
using Facet.Benchmark.DTOs;
using Facet.Benchmark.Mappers;
using Facet.Extensions;
using Mapster;
using System.Collections.Generic;
using System.Linq;

namespace Facet.Benchmark.Benchmarks;

/// <summary>
/// Super fast minimal benchmark for development testing
/// Uses absolute minimum iterations for quickest possible feedback
/// </summary>
[SimpleJob(RunStrategy.Monitoring, warmupCount: 1, iterationCount: 2)]
[MemoryDiagnoser(false)] // Disable memory diagnostics for speed
[MarkdownExporter]
public class SuperQuickBenchmark
{
    private User _user = null!;
    private List<User> _users = null!;
    private UserMapper _userMapper = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Minimal test data
        var testData = TestDataGenerator.CreateTestDataSet(1, 1, 1);
        _user = testData.Users[0];
        _users = TestDataGenerator.GenerateUsers(10); // Very small collection
        
        _userMapper = new UserMapper();
        
        // Configure Mapster
        TypeAdapterConfig<User, UserBasicManualDto>.NewConfig().Compile();
    }

    [Benchmark(Baseline = true, Description = "Facet - Single Mapping")]
    public UserBasicDto FacetSingle()
    {
        return _user.ToFacet<UserBasicDto>();
    }

    [Benchmark(Description = "Mapster - Single Mapping")]
    public UserBasicManualDto MapsterSingle()
    {
        return _user.Adapt<UserBasicManualDto>();
    }

    [Benchmark(Description = "Mapperly - Single Mapping")]
    public UserBasicManualDto MapperlySingle()
    {
        return _userMapper.ToBasicDto(_user);
    }

    [Benchmark(Description = "Facet - Collection (10 items)")]
    public List<UserBasicDto> FacetCollection()
    {
        // Use optimized SelectFacets which pre-allocates and caches the mapper
        return (List<UserBasicDto>)_users.SelectFacets<User, UserBasicDto>();
    }

    [Benchmark(Description = "Mapster - Collection (10 items)")]
    public List<UserBasicManualDto> MapsterCollection()
    {
        return _users.Adapt<List<UserBasicManualDto>>();
    }

    [Benchmark(Description = "Mapperly - Collection (10 items)")]
    public List<UserBasicManualDto> MapperlyCollection()
    {
        return _users.Select(_userMapper.ToBasicDto).ToList();
    }
}