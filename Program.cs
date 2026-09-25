using System.Globalization;
using CreditosApp.Data;
using CreditosApp.Hubs;
using CreditosApp.Mensajeria;
using CreditosApp.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StackExchange.Redis;

// Cultura invariante: el punto es separador decimal en formularios y en el servidor (Render/Linux).
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

// ---------- Base de datos (SQLite) ----------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString)
           .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ---------- Identity + Roles ----------
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Hub y endpoints JSON: responder 401 (no redirigir al login) a peticiones anónimas.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/hubs") ||
            context.Request.Path.StartsWithSegments("/Solicitudes/Estados"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// ---------- Redis: cache distribuida + sesión + llaves de DataProtection ----------
var redisConnectionString = builder.Configuration["Redis:ConnectionString"];
if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
    redisOptions.AbortOnConnectFail = false; // la app arranca aunque Redis tarde en responder
    var redis = ConnectionMultiplexer.Connect(redisOptions);
    builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(redis);
        options.InstanceName = "creditos:";
    });
    // Las cookies (login/sesión) siguen siendo válidas tras reinicios/redeploys en Render.
    builder.Services.AddDataProtection()
        .SetApplicationName("CreditosApp")
        .PersistKeysToStackExchangeRedis(redis, "creditos:dataprotection-keys");
    Console.WriteLine("[Redis] Cache y sesión configuradas con Redis.");
}
else
{
    builder.Services.AddDistributedMemoryCache();
    Console.WriteLine("[Redis] Redis__ConnectionString vacío: se usa cache en memoria (solo desarrollo).");
}

// Sesión respaldada por IDistributedCache (Redis)
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".CreditosApp.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

// ---------- Servicios de dominio ----------
builder.Services.AddScoped<ICacheSolicitudes, CacheSolicitudes>();
builder.Services.AddScoped<ISolicitudService, SolicitudService>();
builder.Services.AddScoped<IEvaluacionService, EvaluacionService>();
builder.Services.AddSingleton<INotificadorSolicitudes, NotificadorSolicitudes>();

// ---------- Cloud MQ (RabbitMQ en CloudAMQP) ----------
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.Seccion));
builder.Services.AddSingleton<IPublicadorSolicitudes, PublicadorRabbitMq>();
builder.Services.AddScoped<ProcesadorNotificaciones>();
builder.Services.AddHostedService<ConsumidorNotificaciones>();

var app = builder.Build();

await DbSeeder.InicializarAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Hub protegido por Identity, solo transporte WebSocket
app.MapHub<SolicitudesHub>(SolicitudesHub.Ruta, options =>
{
    options.Transports = HttpTransportType.WebSockets;
});

app.MapRazorPages()
   .WithStaticAssets();

app.Run();
