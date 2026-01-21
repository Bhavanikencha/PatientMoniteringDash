using Microsoft.EntityFrameworkCore;
using PatinetMo.Data;
using PatinetMo.Hubs;
using PatinetMo.Services;
using PatientMo.Services; // Namespace for VitalSimulationService

var builder = WebApplication.CreateBuilder(args);

// --- 1. REGISTER DATABASE ---
// Ensure "DefaultConnection" exists in your appsettings.json
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- 2. REGISTER MVC (Controllers & Views) ---
builder.Services.AddControllersWithViews();

// --- 3. REGISTER SIGNALR (Real-Time) ---
builder.Services.AddSignalR();

// --- 4. REGISTER CUSTOM SERVICES ---
// AlertService is stateless, so Singleton is efficient
builder.Services.AddSingleton<AlertService>();

// The Simulation Service runs in the background (Hosted Service)
builder.Services.AddHostedService<VitalSimulationService>();

var app = builder.Build();

// --- PIPELINE CONFIGURATION ---

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// --- 5. MAP ENDPOINTS ---
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

// This URL "/VitalsHub" MUST match what is in your JavaScript connection line
app.MapHub<VitalsHub>("/VitalsHub");

app.Run();
