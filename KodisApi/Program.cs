global using Microsoft.EntityFrameworkCore;
global using KodisApi.Data;
global using Sqids;
global using KodisApi.Services;
global using KodisApi.Dtos;
global using KodisApi.Extensions;

var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(
    "https://kod.is", "http://localhost:5173").AllowAnyMethod().AllowAnyHeader()));
builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(
    builder.Configuration.GetConnectionString("ApplicationDbContext")));
builder.Services.AddSingleton(new SqidsEncoder<int>(new()
{
    Alphabet = builder.Configuration["Squid:Alphabet"]!,
    MinLength = 6,
}));
builder.Services.AddScoped<NotebookService>();
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthorization();

app.MapControllers();

app.SeedDatabase();

app.Run();
