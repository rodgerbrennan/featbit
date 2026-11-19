using Application.Services;
using Domain.Organizations;
using Domain.Projects;
using Infrastructure.IntegrationTests.Fixtures;
using Infrastructure.Persistence.MongoDb;
using Microsoft.Extensions.Options;
using Environment = Domain.Environments.Environment;
using EfResourceService = Infrastructure.Services.EntityFrameworkCore.ResourceService;
using MongoResourceService = Infrastructure.Services.MongoDb.ResourceService;

namespace Infrastructure.IntegrationTests.Services;

/// <summary>Change 8: resolve the organization that owns an environment or a project.</summary>
public abstract class ResourceOrganizationLookupTestsBase : IntegrationTestBase
{
    protected abstract IResourceService NewService();

    protected abstract Task SeedAsync(Organization org, Project project, Environment env);

    private async Task<(Organization Org, Project Project, Environment Env)> SeedHierarchyAsync()
    {
        // identical project and env keys in every organization: only the IDs tell them apart
        var org = new Organization(Guid.NewGuid(), "org", $"org-{Guid.NewGuid():N}") { Id = Guid.NewGuid() };
        var project = new Project(org.Id, "web", "web") { Id = Guid.NewGuid() };
        var env = new Environment(project.Id, "prod", "prod");
        await SeedAsync(org, project, env);
        return (org, project, env);
    }

    [DockerFact]
    public async Task GetEnvOrganizationIdAsync_KnownEnv_ReturnsOwningOrganization()
    {
        var a = await SeedHierarchyAsync();
        var b = await SeedHierarchyAsync();

        Assert.Equal(a.Org.Id, await NewService().GetEnvOrganizationIdAsync(a.Env.Id));
        Assert.Equal(b.Org.Id, await NewService().GetEnvOrganizationIdAsync(b.Env.Id));
    }

    [DockerFact]
    public async Task GetEnvOrganizationIdAsync_UnknownEnv_ReturnsNull()
    {
        Assert.Null(await NewService().GetEnvOrganizationIdAsync(Guid.NewGuid()));
    }

    [DockerFact]
    public async Task GetProjectOrganizationIdAsync_KnownProject_ReturnsOwningOrganization()
    {
        var a = await SeedHierarchyAsync();
        var b = await SeedHierarchyAsync();

        Assert.Equal(a.Org.Id, await NewService().GetProjectOrganizationIdAsync(a.Project.Id));
        Assert.Equal(b.Org.Id, await NewService().GetProjectOrganizationIdAsync(b.Project.Id));
    }

    [DockerFact]
    public async Task GetProjectOrganizationIdAsync_UnknownProject_ReturnsNull()
    {
        Assert.Null(await NewService().GetProjectOrganizationIdAsync(Guid.NewGuid()));
    }
}

[Collection(FeatBitPostgresCollection.Name)]
public sealed class PostgresResourceOrganizationLookupTests : ResourceOrganizationLookupTestsBase
{
    private readonly FeatBitPostgresFixture _postgres;

    public PostgresResourceOrganizationLookupTests(FeatBitPostgresFixture postgres)
    {
        _postgres = postgres;
    }

    protected override IResourceService NewService() =>
        new EfResourceService(AppDbContextFactory.Create(_postgres.ConnectionString));

    protected override async Task SeedAsync(Organization org, Project project, Environment env)
    {
        await using var db = AppDbContextFactory.Create(_postgres.ConnectionString);
        db.Set<Organization>().Add(org);
        db.Set<Project>().Add(project);
        db.Set<Environment>().Add(env);
        await db.SaveChangesAsync();
    }
}

[Collection(MongoCollection.Name)]
public sealed class MongoResourceOrganizationLookupTests : ResourceOrganizationLookupTestsBase
{
    private readonly MongoDbFixture _mongo;
    private readonly string _database = $"be-res-org-{Guid.NewGuid():N}";

    public MongoResourceOrganizationLookupTests(MongoDbFixture mongo)
    {
        _mongo = mongo;
    }

    private MongoDbClient NewClient() => new(Options.Create(new MongoDbOptions
    {
        ConnectionString = _mongo.ConnectionString,
        Database = _database
    }));

    protected override IResourceService NewService() => new MongoResourceService(NewClient());

    protected override async Task SeedAsync(Organization org, Project project, Environment env)
    {
        var client = NewClient();
        await client.CollectionOf<Organization>().InsertOneAsync(org);
        await client.CollectionOf<Project>().InsertOneAsync(project);
        await client.CollectionOf<Environment>().InsertOneAsync(env);
    }
}
