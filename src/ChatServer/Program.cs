using FluentValidation;
using FluentValidation.AspNetCore;
using ChatServer.Storage;
using ChatServer.Services;
using ChatServer.Middleware;
using ChatServer.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add data store as singleton
builder.Services.AddSingleton<IDataStore, InMemoryDataStore>();

// Add application services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();

// Add services to the container
builder.Services.AddControllers();

// Add SignalR
builder.Services.AddSignalR();

// Add FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173") // React, Vite
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR
    });
});

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Chat Server API",
        Version = "v1",
        Description = "In-memory real-time chat server with REST API and WebSocket support"
    });
});

var app = builder.Build();

// Add global exception handling middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Chat Server API v1");
        options.RoutePrefix = "swagger";
    });
}
else
{
    // HTTPS redirection only in production (handled by reverse proxy in dev)
    app.UseHttpsRedirection();
}

app.UseCors();

app.UseAuthorization();

app.MapControllers();

// Map SignalR hub
app.MapHub<ChatHub>("/hubs/chat");

// Add health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithOpenApi();

app.Run();

// Make Program class accessible to tests
public partial class Program { }
