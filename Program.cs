using Microsoft.EntityFrameworkCore;
using PatinetMo.Data;
using PatinetMo.Hubs;
using PatinetMo.Services;
using PatientMo.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. DB Connection
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Add API Controllers (We don't need "WithViews" anymore, but keeping it is fine)
builder.Services.AddControllers();

// 3. Add SignalR
builder.Services.AddSignalR()
    .AddMessagePackProtocol();


// 4. === CRITICAL: ENABLE CORS FOR REACT ===

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173") // <--- MUST BE EXACT URL (No trailing slash)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials(); // <--- This requires WithOrigins, not AllowAnyOrigin
        });
});

// 5. Register Services
builder.Services.AddSingleton<AlertService>();
builder.Services.AddHostedService<VitalSimulationService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment()) { app.UseHsts(); }

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// 6. === USE CORS ===
app.UseCors("AllowReactApp");

app.UseAuthorization();

app.MapControllers(); // Maps the API endpoints
app.MapHub<VitalsHub>("/VitalsHub"); // Maps the SignalR Hub

app.Run();

