using System.Text;
using Backend.Auth;
using Backend.Data;
using Backend.Hardware;
using Backend.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// La connection string vive en user-secrets (dotnet user-secrets set "ConnectionStrings:DefaultConnection" "...")
// o en appsettings.Development.json; nunca en appsettings.json.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Falta ConnectionStrings:DefaultConnection. Configurala con: " +
        "dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" " +
        "\"Server=.\\SQLEXPRESS;Database=BalanzaCILP;Trusted_Connection=True;TrustServerCertificate=True\"");

// Clave de firma del JWT: solo en user-secrets (nunca en appsettings.json ni en el código).
var claveJwt = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(claveJwt) || Encoding.UTF8.GetByteCount(claveJwt) < 32)
    throw new InvalidOperationException(
        "Falta Jwt:Key (al menos 32 caracteres). Configurala con: " +
        "dotnet user-secrets set \"Jwt:Key\" \"<clave aleatoria de 32 caracteres o más>\" --project Backend");

// Auditoría: cada conexión de EF registra el usuario logueado con dbo.pr_context_save.
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<AuditoriaInterceptor>();
builder.Services.AddDbContext<AppDbContext>((servicios, options) => options
    .UseSqlServer(connectionString)
    .AddInterceptors(servicios.GetRequiredService<AuditoriaInterceptor>()));

// Device Connector: simulado por defecto. Para hardware real, implementar IDeviceConnector
// y registrarlo cuando Hardware:Modo sea "Real".
var modoHardware = builder.Configuration.GetValue<string>("Hardware:Modo") ?? "Simulado";
if (modoHardware.Equals("Simulado", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IDeviceConnector, SimulatedDeviceConnector>();
else
    throw new InvalidOperationException("Hardware:Modo = \"" + modoHardware + "\" no tiene un conector registrado. Use \"Simulado\".");
builder.Services.AddSingleton<RegistroLecturas>();

// Autenticación con JWT (lo emite AuthController). Los claims se usan con su nombre corto
// (sub, name, role, punto): ver Backend/Auth/Permisos.cs.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(claveJwt)),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = ClaimsCilp.IdRed,
            RoleClaimType = ClaimsCilp.Rol
        };
    });

// Todos los endpoints exigen usuario logueado salvo los marcados con [AllowAnonymous] (el login).
// Los permisos por pantalla los controla [Permiso] en cada controller.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ForeignKeyConflictHandler>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

var app = builder.Build();

// El esquema y los datos de sistema los crea la publicación del proyecto Database (ver CLAUDE.md).
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
