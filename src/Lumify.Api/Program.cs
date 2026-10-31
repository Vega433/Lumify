using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using dotenv.net;
using Lumify.Application.Interfaces;
using Lumify.Application.Services;
using Lumify.Infrastructure.BackgroundServices;
using Lumify.Infrastructure.Media;
using Lumify.Infrastructure.Persistence;
using Lumify.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

DotEnv.Load(options: new DotEnvOptions(
    ignoreExceptions: true,
    probeForEnv: true,
    probeLevelsToSearch: 6,
    overwriteExistingVars: false,
    trimValues: true, 
    supportExportSyntax: true,
    supportVariableExpansion: false));

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string? jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key is missing or shorter than 32 characters. Copy .env.example to .env and set Jwt__Key.");
}

if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DefaultConnection")))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not set. Copy .env.example to .env.");
}

// FFmpegSetup.ConfigureFrom(builder.Configuration);

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 512 * 1024 * 1024);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
        .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<ILumifyDbContext>(provider => provider.GetRequiredService<AppDbContext>());

string webRoot = builder.Environment.WebRootPath
    ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
string uploadsPath = Path.Combine(webRoot, "uploads");
Directory.CreateDirectory(uploadsPath);

// builder.Services.AddSingleton<IVideoProcessingQueue, VideoProcessingQueue>();
builder.Services.AddSingleton<IFileStorageService>(_ => new LocalFileStorageService(uploadsPath));

/* builder.Services.AddHostedService(sp => new VideoProcessingWorker(
    sp.GetRequiredService<IVideoProcessingQueue>(),
    sp.GetRequiredService<IServiceScopeFactory>(),
    sp.GetRequiredService<ILogger<VideoProcessingWorker>>(),
    uploadsPath)); */
builder.Services.AddScoped<VideoApplicationService>();
builder.Services.AddScoped<UserApplicationService>();
builder.Services.AddScoped<IdentityApplicationService>();
// builder.Services.AddScoped<IMediaService, FFMpegService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenService, TokenService>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        // Keep JWT short names (sub, email) — do not map to long ClaimTypes URIs.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero,
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .SetIsOriginAllowed(IsLocalOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Lumify API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description =
            "Paste the accessToken value only (without the word Bearer). " +
            "Do not paste refreshToken — it is not a JWT.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

WebApplication app = builder.Build();

// CORS до статики: плейлист HLS тоже должен открываться, если сайт запущен не с порта API.
app.UseCors("Frontend");

// uploads лежат в wwwroot/uploads. У .m3u8 в стандартной карте типов часто нет своего MIME.
FileExtensionContentTypeProvider contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings[".m3u8"] = "application/vnd.apple.mpegurl";
contentTypes.Mappings[".ts"] = "video/mp2t";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypes
});

UseFrontend(app);

app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Lumify API v1");
    options.RoutePrefix = string.Empty;
});
app.MapControllers();

app.Run();

static bool IsLocalOrigin(string origin)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri))
    {
        return false;
    }

    return uri.Host is "localhost" or "127.0.0.1";
}

// Сайт лежит в папке Lumify.Api/wwwroot/frontend/ и открывается как /app.
static void UseFrontend(WebApplication app)
{
    string frontendPath = Path.GetFullPath(Path.Combine(
        app.Environment.ContentRootPath,
        "wwwroot",
        "frontend"));

    if (!Directory.Exists(frontendPath))
    {
        return;
    }

    PhysicalFileProvider files = new PhysicalFileProvider(frontendPath);

    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = files,
        RequestPath = "/app"
    });

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = files,
        RequestPath = "/app"
    });
}
