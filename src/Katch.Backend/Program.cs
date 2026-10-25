// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.BackgroundJobs;
using Katch.Backend.Infrastructure.Api;
using Katch.Backend.Infrastructure.Authentication;
using Katch.Backend.Infrastructure.Persistence;
using Katch.Backend.Repositories;
using Katch.Backend.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// A cadeia de ligação vem da variável de ambiente ConnectionStrings__Katch e nunca do repositório.
string connectionString = builder.Configuration.GetConnectionString("Katch")
    ?? throw new InvalidOperationException(
        "Falta a cadeia de ligação ao PostgreSQL. Defina a variável de ambiente ConnectionStrings__Katch.");

builder.Services.AddDbContext<KatchDbContext>(options => options.UseKatchPostgres(connectionString));
builder.Services.AddKatchAuthentication();
builder.Services.AddKatchApplicationServices();
builder.Services.AddKatchBackgroundJobs();
builder.Services.AddControllers().AddKatchApiConventions();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    string xmlFile = Path.Combine(AppContext.BaseDirectory, $"{typeof(KatchDbContext).Assembly.GetName().Name}.xml");
    options.IncludeXmlComments(xmlFile);
});

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAccountStatusCheck();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
