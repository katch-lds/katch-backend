// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.BackgroundJobs;
using Katch.Backend.Infrastructure.Api;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Infrastructure.Cors;
using Katch.Backend.Infrastructure.Errors;
using Katch.Backend.Infrastructure.Hosting;
using Katch.Backend.Infrastructure.Persistence;
using Katch.Backend.Infrastructure.RateLimiting;
using Katch.Backend.Repositories;
using Katch.Backend.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// A cadeia de ligação vem da variável de ambiente ConnectionStrings__Katch e nunca do repositório.
string connectionString = builder.Configuration.GetConnectionString("Katch")
    ?? throw new InvalidOperationException(
        "Falta a cadeia de ligação ao PostgreSQL. Defina a variável de ambiente ConnectionStrings__Katch.");

builder.Services.AddDbContext<KatchDbContext>(options => options.UseKatchPostgres(connectionString));
builder.Services.AddKatchHosting();
builder.Services.AddKatchRateLimiting();
builder.Services.AddKatchCors();
builder.Services.AddKatchAuthentication();
builder.Services.AddKatchApplicationServices();
builder.Services.AddKatchBackgroundJobs();
builder.Services.AddControllers().AddKatchApiConventions();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    string xmlFile = Path.Combine(AppContext.BaseDirectory, $"{typeof(KatchDbContext).Assembly.GetName().Name}.xml");
    options.IncludeXmlComments(xmlFile);
    options.AddKatchBearerAuthentication();
    options.OperationFilter<FileUploadOperationFilter>();
});

WebApplication app = builder.Build();

app.UseKatchForwardedHeaders();
app.UseKatchSecurityHeaders();
app.UseKatchExceptionHandler();

if (app.Configuration.GetValue<bool>(HostingServiceCollectionExtensions.SwaggerEnabledKey))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseKatchCors();
app.UseKatchRequestBodyLimit();
app.UseKatchUnsupportedMediaType();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAccountStatusCheck();
app.UseAuthorization();
app.MapControllers();
app.MapKatchHealthChecks();

await app.RunAsync();
