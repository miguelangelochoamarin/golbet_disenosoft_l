using GolBet.Entities;
using GolBet.Repositories.Data;
using GolBet.Repositories.Implementations;
using GolBet.Repositories.Interfaces;
using GolBet.Services.Implementations;
using GolBet.Services.Interfaces;
using GolBet.Services.Mapping;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Globalization;

var culture = new CultureInfo("es-CO");
//Dates in Spanish: numbers neutral so model binding reads "2.5" correctly
culture.NumberFormat = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
culture.NumberFormat.NumberGroupSeparator = ""; //"2,5"fails loudly, never binds as 25

CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));
// ...

// Add services to the container.
builder.Services.AddControllersWithViews();

//Este es el nuevo código  
builder.Services.AddDbContext<AppDbContext>(options =>
options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    // Academic-friendly password policy (production would be stricter) 
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireDigit = true;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";           // anonymous hitting [Authorize] 
    options.AccessDeniedPath = "/Account/AccessDenied";  // wrong role 
});


// Open generic registration: one line, a repository for every entity 

builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

// Specific repositories 

builder.Services.AddScoped<IMatchRepository, MatchRepository>();
builder.Services.AddScoped<IBetRepository, BetRepository>();

// AutoMapper: scans the assembly containing MappingProfile for all profiles 

builder.Services.AddAutoMapper(typeof(MappingProfile));



// Business services 

builder.Services.AddScoped<IMatchService, MatchService>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IBetService, BetService>();
var app = builder.Build();

// Unhandled exceptions (500): friendly page in production, 
// full yellow stack trace while developing 

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error/General");
}

// Status codes without a body (404 and friends): re-execute 
// the pipeline against our ErrorController 

app.UseStatusCodePagesWithReExecute("/Error/{0}");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSerilogRequestLogging();   // one clean line per request: method, path, status, duration
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Seed the database on startup AddDbContext 


using (var scope = app.Services.CreateScope())
{

    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

    await DbSeeder.SeedAsync(context, roleManager, userManager);

}

// --- in the pipeline, before app.UseAuthorization() --- 

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
