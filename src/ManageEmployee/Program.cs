using Hangfire;
using ManageEmployee.Extends;
using ManageEmployee.Filters;
using ManageEmployee.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using ManageEmployee.Dal.DbContexts;
using ManageEmployee.Helpers;
using ManageEmployee.Services.Configurations;
using ManageEmployee.Services.Interfaces;
using ManageEmployee.Constants;
using Newtonsoft.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Custom application.json by env
IWebHostEnvironment env = builder.Environment;

Console.WriteLine($"Get configuration from application.{env.EnvironmentName}.json");

builder.Configuration.SetBasePath(env.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

ConfigurationManager configuration = builder.Configuration;

builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddTransient<IConnectionStringProvider, ConnectionStringProvider>();
builder.Services.AddDbContext<ApplicationDbContext>(
    (serviceProvider, dbContextBuilder) =>
    {
        var connectionStringProvider = serviceProvider.GetRequiredService<IConnectionStringProvider>();

        var connectionString = connectionStringProvider.GetConnectionString();

        connectionStringProvider.SetupDbContextOptionsBuilder(dbContextBuilder, connectionString);
    });

builder.Services.AddHangfire((serviceProvider, hangfireConfiguration) =>
{
    var connectionStringProvider = serviceProvider.GetRequiredService<IConnectionStringProvider>();
    var connectionString = connectionStringProvider.GetConnectionString();
    hangfireConfiguration.UseSqlServerStorage(connectionString);
});

builder.Services.AddCors(x =>
    x.AddPolicy("AllowAll", builders => builders.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// DI Services
builder.Services.RegisterServiceInjection();

var appSettingsSection = configuration.GetSection("AppSettings");
builder.Services.Configure<AppSettings>(appSettingsSection);
var appSettingsInvoice = configuration.GetSection("Invoice");
builder.Services.Configure<AppSettingInvoice>(appSettingsInvoice);
var appSettingsDatabase = configuration.GetSection("ConnectionStrings");
builder.Services.Configure<SettingDatabase>(appSettingsDatabase);
var appSettingOtherCompany = configuration.GetSection("OtherCompany");
builder.Services.Configure<AppSettingOtherCompany>(appSettingOtherCompany);

// Adding Authentication
builder.Services.AddAuthorization();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;

        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.SaveToken = true;
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidAudience = configuration["JWT:ValidAudience"],
            ValidIssuer = configuration["JWT:ValidIssuer"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JWT:Secret"]))
        };
    });

// Hangfire

//builder.Services.AddHangfire((serviceProvider, dbContextBuilder) =>
//{
//    var connectionString = connectionStringPlaceHolder.Replace("{dbName}", dbName);
//    dbContextBuilder.UseSqlServerStorage(connectionString);
//});
builder.Services.AddHangfireServer();


//RecurringJob.AddOrUpdate(() => SendMailBirthdayJob.SendMail(ApplicationDbContext dbContext), Cron.Daily(9,0));

//builder.Services.AddControllers();
builder.Services.AddControllers(
                        option =>
                        {
                            //option.Filters.Add(typeof(OnExceptionFilter));
                            option.EnableEndpointRouting = false;
                        }
).AddNewtonsoftJson();
builder.Services.AddHttpClient();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddDirectoryBrowser();

// add signalR
builder.Services.AddSignalR(o => { o.EnableDetailedErrors = true; });
builder.WebHost.UseKestrel(options =>
{
    options.Limits.MaxConcurrentConnections = 1000;
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
});
builder.Services.AddSwaggerGen(swagger =>
{
    //This is to generate the Default UI of Swagger Documentation
    swagger.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "Isoft API",
        Description = "Assian API"
    });
    // using System.Reflection;
    //var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    //swagger.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
    // To Enable authorization using Swagger (JWT)
    swagger.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description =
            "JWT Authorization header using the Bearer scheme. \r\n\r\n Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer 12345abcdef\"",
    });
    swagger.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            new string[] { }
        }
    });
});

// Filter
builder.Services.AddControllersWithViews(options => { options.Filters.Add<ExceptionFilter>(); });

var app = builder.Build();

var loggerFactory = app.Services.GetService<ILoggerFactory>();
loggerFactory.AddFile(builder.Configuration["Logging:LogFilePath"].ToString());

app.DatabaseMigration(configuration);

app.UseFileServer();
app.UseRouting();
app.UseCors(x => x
    .AllowAnyMethod()
    .AllowAnyHeader()
    .SetIsOriginAllowed(origin => true) // allow any origin
    .AllowCredentials());

app.UseHangfireDashboard();
app.UseAuthentication();
app.UseAuthorization();
app.UseHttpsRedirection();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "Uploads")),
    RequestPath = "/Uploads"
});

app.Use((context, next) =>
{
    context.Request.Headers.TryGetValue(Constants.YearFilterHeaderName, out var yearFilter);
    if(string.IsNullOrWhiteSpace(yearFilter))
    {
        context.Request.Headers[Constants.YearFilterHeaderName] = DateTime.UtcNow.Year.ToString();
    }
    return next(context);
});

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
};

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/ws")
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            using WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
        }
        else
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        }
    }
    else
    {
        //context.Request.EnableBuffering(bufferThreshold: 1024 * 45, bufferLimit: 1024 * 100);
        await next();
    }
});

var configUrl = configuration["AppSettings:UrlWebSocket"];
List<string> domains = new();
domains.Add("http://localhost:4200");

if (!string.IsNullOrEmpty(configUrl))
{
    domains.Add(configUrl);
}

domains.ForEach(domain => { webSocketOptions.AllowedOrigins.Add(domain); });

app.UseWebSockets(webSocketOptions);

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
if (!app.Environment.IsDevelopment())
{
    app.MapHub<BroadcastHub>("/notify",
        options => { options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets; });
}

app.MapControllers();
app.Run();