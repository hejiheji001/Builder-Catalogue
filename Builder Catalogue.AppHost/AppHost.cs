using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Add a Docker Compose environment & Enable dashboard with custom configuration
var compose = builder.AddDockerComposeEnvironment("compose").WithDashboard(dashboard => {
    dashboard.WithHostPort(8888).WithForwardedHeaders(enabled: true);
});

// Add your services to the Docker Compose environment
builder.AddProject<BuilderCatalogue_Api>("apiservice").PublishAsDockerComposeService((resource, service) =>
{
    service.Name = "api";
});


builder.Build().Run();
