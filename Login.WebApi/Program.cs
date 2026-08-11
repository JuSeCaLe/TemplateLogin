using Login.Infrastructure.Data.Identity;
using Login.Infrastructure.Model;
using Login.Infrastructure.Model.Parametros;
using Login.Infrastructure.Services;
using Login.WebApi.Seeding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var corsAllowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Value?.
    Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? Array.Empty<string>();
var connectionString = builder.Configuration.GetConnectionString("DefaultConection");
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Login API", Version = "v1" });

    //  Define Bearer token auth
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingresa: Bearer {tu_token}"
    });

    // Aplica Bearer a las operaciones
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<DataContext>(options =>
    options.UseNpgsql(
        connectionString,
        npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly("Login.Infrastructure");
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null);
        }));
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(
        policy => policy
            .WithOrigins(corsAllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).
    AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(
                    builder.Configuration["Jwt:Key"] ?? string.Empty
                )
            ),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();
builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<AppRole>()
    .AddEntityFrameworkStores<DataContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<DemandanteAuthService>();

builder.Services.Configure<GoogleDriveOptions>(builder.Configuration.GetSection("GoogleDrive"));
builder.Services.AddSingleton<GoogleDriveService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI();
//}
app.UseRouting();
app.UseCors();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        await db.Database.MigrateAsync();

        string[] roles = ["r-admin", "r-user"];

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new AppRole
                {
                    Name = role,
                    Description = role == "r-admin" ? "Administrador del sistema" : "Usuario estándar",
                    Active = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        // Demandantes = roles marcados IsDemandante. Un usuario con uno de estos
        // roles solo ve/crea casos de ese demandante (ver CasesController).
        string[] demandanteRoles = ["Bancolombia", "BBVA", "Davivienda"];

        foreach (var name in demandanteRoles)
        {
            if (!await roleManager.RoleExistsAsync(name))
            {
                await roleManager.CreateAsync(new AppRole
                {
                    Name = name,
                    Description = $"Demandante: {name}",
                    Active = true,
                    IsDemandante = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        var adminEmail = "admin@abogapp.com";
        var adminPassword = "Admin123!";
        var adminFirstName = "admin";
        var adminLastName = "abogapp";

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new AppUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = adminFirstName,
                LastName = adminLastName,
                EmailConfirmed = true,
                Active = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "r-admin");
            }
        }

        var userEmail = "user@abogapp.com";
        var userPassword = "User123!";
        var userFirstName = "user";
        var userLastName = "abogapp";

        var normalUser = await userManager.FindByEmailAsync(userEmail);
        if (normalUser == null)
        {
            normalUser = new AppUser
            {
                UserName = userEmail,
                Email = userEmail,
                FirstName = userFirstName,
                LastName = userLastName,
                EmailConfirmed = true,
                Active = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(normalUser, userPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(normalUser, "r-user");
            }
        }

        string[] obligationTypes = ["PAGARE", "CONTRATO", "LETRA"];
        foreach (var name in obligationTypes)
        {
            var exists = await db.TiposObligacion.AnyAsync(x => x.Name.ToLower() == name.ToLower());
            if (!exists)
            {
                db.TiposObligacion.Add(new TipoObligacion
                {
                    Name = name,
                    Description = name,
                    Active = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        string[] processTypes =
        [
            "EJECUTIVO SINGULAR",
            "EJECUTIVO HIPOTECARIO",
            "EJECUTIVO MIXTO",
            "EJECUTIVO PRENDARIO",
            "RESTITUCIÓN",
            "LEASING"
        ];

        foreach (var name in processTypes)
        {
            var exists = await db.TiposProceso.AnyAsync(x => x.Name.ToLower() == name.ToLower());
            if (!exists)
            {
                db.TiposProceso.Add(new TipoProceso
                {
                    Name = name,
                    Description = name,
                    Active = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        string[] courts =
        [
            "Juzgado 01 Civil Municipal",
            "Juzgado 10 Civil del Circuito"
        ];

        foreach (var name in courts)
        {
            var exists = await db.Juzgado.AnyAsync(x => x.Name.ToLower() == name.ToLower());
            if (!exists)
            {
                db.Juzgado.Add(new Juzgado
                {
                    Name = name,
                    Description = name,
                    City = name.Contains("Municipal") ? "Bogotá" : "Medellín",
                    Active = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await db.SaveChangesAsync();

        // Un usuario demo por cada rol-demandante, para poder probar el filtrado
        // por rol (ver CasesController.AuthorizedQuery). "demandante@abogapp.com"
        // se conserva tal cual para Bancolombia por compatibilidad con logins ya
        // existentes; los demás siguen el patrón demandante.{rol}@abogapp.com.
        var demandanteUserPassword = "Demandante123!";

        foreach (var name in demandanteRoles)
        {
            var demandanteUserEmail = name == "Bancolombia"
                ? "demandante@abogapp.com"
                : $"demandante.{name.ToLowerInvariant()}@abogapp.com";

            if (await userManager.FindByEmailAsync(demandanteUserEmail) is null)
            {
                var demandanteUser = new AppUser
                {
                    UserName = demandanteUserEmail,
                    Email = demandanteUserEmail,
                    FirstName = "Usuario",
                    LastName = name,
                    EmailConfirmed = true,
                    Active = true,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(demandanteUser, demandanteUserPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(demandanteUser, name);
                }
            }
        }

        await CatalogSeeder.SeedAsync(db);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Startup/seed error: {ex}");
    }
}

app.Run();