using Blazored.Toast;
using DotNetNote.Models;
using DotNetSaleCore.Models;
using Hawaso.Areas.Identity;
using Hawaso.Areas.Identity.Services;
using Hawaso.Data;
using Hawaso.Extensions.BriefingLogs;
using Hawaso.Extensions.Memos;
using Hawaso.Infrastructures;
using Hawaso.Models.CommonValues;
using Hawaso.Models.Notes;
using Hawaso.Rules;
using Hawaso.Settings;
using MachineTypeApp.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NoticeApp.Models;
using ReplyApp.Managers;
using System;
using System.Linq;
using System.Threading.Tasks;
using VisualAcademy;
using VisualAcademy.Models.Departments;
using VisualAcademy.Models.Replys;
using Zero.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

var services = builder.Services;
var Configuration = builder.Configuration;

services.AddHttpContextAccessor();

services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        Configuration.GetConnectionString("DefaultConnection"),
        options => options.EnableRetryOnFailure()));

// ASP.NET Core Identity ����
services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

services.Configure<IdentityOptions>(options =>
{
    options.Password.RequiredLength = 4;
});

services.ConfigureApplicationCookie(options =>
{
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(15);
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.ReturnUrlParameter = CookieAuthenticationDefaults.ReturnUrlParameter;
    options.SlidingExpiration = true;
});

services.AddTransient<IEmailSender, EmailSender>();

services.AddControllersWithViews();
services.AddRazorPages();
services.AddServerSideBlazor()
    .AddCircuitOptions(options => { options.DetailedErrors = true; });

// CORS ����
services.AddCors(options =>
{
    options.AddPolicy("AllowAnyOrigin",
        builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

services.AddHttpClient();

services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<ApplicationUser>>();
services.AddDatabaseDeveloperPageExceptionFilter();
services.AddSingleton<WeatherForecastService>();

services.Configure<DotNetNoteSettings>(Configuration.GetSection("DotNetNoteSettings"));

services.AddDbContext<DotNetNoteContext>(options =>
    options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));
services.AddDbContext<NoteDbContext>(options =>
    options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));

// ������ ���� �����̳� ����
DependencyInjectionContainer(services);

services.AddTransient<IFileStorageManager, ReplyAppFileStorageManager>();

services.AddDependencyInjectionContainerForPurgeApp(Configuration.GetConnectionString("DefaultConnection"));

// ������ ���� �޼��� ����
void DependencyInjectionContainer(IServiceCollection services)
{
    services.AddSingleton<IConfiguration>(Configuration);

    services.AddTransient<INoteRepository, NoteRepository>();
    services.AddTransient<INoteCommentRepository, NoteCommentRepository>();

    services.AddDbContext<HawasoDbContext>(options =>
        options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")), ServiceLifetime.Transient);
    services.AddDbContext<CommonValueDbContext>(options =>
        options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));
    services.AddTransient<ICommonValueRepository, CommonValueRepository>();

    // ManufacturerApp ���� ������ ����
    services.AddDependencyInjectionContainerForManufacturer(Configuration.GetConnectionString("DefaultConnection"));

    // Blazored.Toast
    services.AddBlazoredToast();

    // MemoApp ���� ������ ����
    services.AddDependencyInjectionContainerForMemoApp(Configuration.GetConnectionString("DefaultConnection"));

    // Upload Feature
    services.AddDiForLibries(Configuration.GetConnectionString("DefaultConnection"));
    services.AddDiForBriefingLogs(Configuration.GetConnectionString("DefaultConnection"));

    // ArchiveApp ���� ������ ����
    services.AddDependencyInjectionContainerForArchiveApp(Configuration.GetConnectionString("DefaultConnection"));

    // DepartmentApp ���� ������ ����
    services.AddDependencyInjectionContainerForDepartmentApp(Configuration.GetConnectionString("DefaultConnection"));
}

// DotNetSaleCore ���� ������ ����
AddDependencyInjectionContainerForDotNetSaleCore(services, Configuration);

// �����ͺ��̽� �ʱ�ȭ �� ���̱׷��̼�
try
{
    await DatabaseHelper.AddOrUpdateRegistrationDate(connectionString);
}
catch (Exception)
{

}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseCors("AllowAnyOrigin");

// Configure the HTTP request pipeline.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    var scopedServices = scope.ServiceProvider;
    await UserAndRoleInitializer.CreateBuiltInUsersAndRoles(scopedServices);
}

app.Run();

/// <summary>
/// ���θ�(DotNetSaleCore) ���� ������(���Ӽ�) ���� ���� �ڵ常 ���� ��Ƽ� ����
/// </summary>
void AddDependencyInjectionContainerForDotNetSaleCore(IServiceCollection services, IConfiguration configuration)
{
    services.AddDbContext<DotNetSaleCoreDbContext>(options =>
        options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")), ServiceLifetime.Transient);

    services.AddTransient<ICustomerRepository, CustomerRepository>();
    services.AddTransient<ICategoryRepository, CategoryRepository>();
    services.AddTransient<IProductRepositoryAsync, ProductRepositoryAsync>();

    services.AddSingleton<ILoginRepository>(
        new LoginRepository(configuration.GetConnectionString("DefaultConnection")));
    services.AddDbContext<LoginDbContext>(options =>
        options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")), ServiceLifetime.Transient);
    services.AddTransient<ILoginRepositoryAsync, LoginRepositoryAsync>();
}