global using Microsoft.EntityFrameworkCore;
global using KodisApi.Data;
global using Sqids;
global using KodisApi.Services;
global using KodisApi.Dtos;
global using KodisApi.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using KodisApi.Settings;

var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("JwtSettings"));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ClockSkew = TimeSpan.Zero,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Secret"]!))
        };
    });
builder.Services.AddScoped<JwtService>();
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

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.SeedDatabase();

app.Run();
