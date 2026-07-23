using Lumify.Application.Interfaces;
using Lumify.Application.Services;
using Lumify.Infrastructure.BackgroundServices;
using Lumify.Infrastructure.Media;
using Lumify.Infrastructure.Persistence;
using Lumify.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

FFmpegSetup.ConfigureFrom(builder.Configuration);

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 512 * 1024 * 1024);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
        .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<ILumifyDbContext>(provider => provider.GetRequiredService<AppDbContext>());

string webRoot = builder.Environment.WebRootPath
    ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
string uploadsPath = Path.Combine(webRoot, "uploads");
Directory.CreateDirectory(uploadsPath);

builder.Services.AddSingleton<IVideoProcessingQueue, VideoProcessingQueue>();
builder.Services.AddSingleton<IFileStorageService>(_ => new LocalFileStorageService(uploadsPath));

builder.Services.AddHostedService(sp => new VideoProcessingWorker(
    sp.GetRequiredService<IVideoProcessingQueue>(),
    sp.GetRequiredService<IServiceScopeFactory>(),
    sp.GetRequiredService<ILogger<VideoProcessingWorker>>(),
    uploadsPath));
builder.Services.AddScoped<VideoApplicationService>();
builder.Services.AddScoped<UserApplicationService>();
builder.Services.AddScoped<IdentityApplicationService>();
builder.Services.AddScoped<IMediaService, FFMpegService>();
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ClockSkew = TimeSpan.Zero,
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = ClaimTypes.Role
        };
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

// uploads лежат в wwwroot/uploads
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Lumify API v1");
    options.RoutePrefix = string.Empty;
});
app.MapControllers();

using (IServiceScope scope = app.Services.CreateScope())
{
    AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
