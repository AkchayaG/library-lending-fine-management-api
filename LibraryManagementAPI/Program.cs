using LibraryManagementAPI.Data;

var builder = WebApplication.CreateBuilder(args);

// Register Controllers
builder.Services.AddControllers();

// Register SQL Server service
builder.Services.AddScoped<DatabaseConnection>();
builder.Services.AddScoped<LibraryManagementAPI.Services.ILibraryService,
                           LibraryManagementAPI.Services.LibraryService>();

// Build application
var app = builder.Build();

// Map API Controllers
app.MapControllers();

// Start API
app.Run();
