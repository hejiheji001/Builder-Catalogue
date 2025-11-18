var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<BuilderCatalogue_Api>("api");

builder.Build().Run();
