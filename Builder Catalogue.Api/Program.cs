using BuilderCatalogue.Api.Clients;
using BuilderCatalogue.Api.Options;
using BuilderCatalogue.Api.Services;
using BuilderCatalogue.Api.Services.Caching;
using BuilderCatalogue.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

builder.Services.Configure<CatalogueApiOptions>(builder.Configuration.GetSection(CatalogueApiOptions.SectionName));

builder.Services.AddSingleton<ICacheService, SimpleMemoryCache>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<LEGOSetService>();

builder.Services.AddHttpClient<ICatalogueApiClient, CatalogueApiClient>((serviceProvider, client) =>
{
    var apiOptions = serviceProvider.GetRequiredService<IOptions<CatalogueApiOptions>>().Value;
    if (string.IsNullOrWhiteSpace(apiOptions.BaseUrl))
    {
        throw new InvalidOperationException("Catalogue API base URL must be configured.");
    }

    client.BaseAddress = new Uri(apiOptions.BaseUrl);
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
