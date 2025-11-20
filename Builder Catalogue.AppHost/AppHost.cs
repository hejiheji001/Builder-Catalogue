using Projects;

var builder = DistributedApplication.CreateBuilder(args);

// Always define the API project along with an external HTTP endpoint so cloud deployments expose ingress.
var apiService = builder
    .AddProject<BuilderCatalogue_Api>("apiservice")
    .WithExternalHttpEndpoints();

if (builder.Environment.EnvironmentName == "Development")
{
    // Add a Docker Compose environment & Enable dashboard with custom configuration for local dev only.
    var dockerEnv = builder.AddDockerComposeEnvironment("docker").WithDashboard(dashboard =>
    {
        dashboard.WithHostPort(8888).WithForwardedHeaders(enabled: true);
    });

    // Bind the API project into the compose stack.
    apiService.PublishAsDockerComposeService((resource, service) =>
    {
        service.Name = "api";
    });
}

// Add Azure Container Apps environment for cloud deployments.
var azureEnv = builder.AddAzureContainerAppEnvironment("aspire-env").WithDashboard().WithAzdResourceNaming();

builder.Build().Run();
