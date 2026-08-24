using InfraAis.Options;
using InfraAis.Services;
using InfraAis.Repositories;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOptions<AisStreamOptions>().Bind(builder.Configuration.GetSection(AisStreamOptions.SectionName));
builder.Services
    .AddOptions<IngestionOptions>()
    .Bind(builder.Configuration.GetSection(IngestionOptions.SectionName));
builder.Services.AddHostedService<AisIngestionService>();
builder.Services
    .AddOptions<AisDbOptions>()
    .Bind(builder.Configuration.GetSection(AisDbOptions.SectionName));
builder.Services.AddScoped<IAisRepository, AisRepository>();
builder.Services.AddScoped<IvesselIdentifierResolver,VesselIdentifierResolver>();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
