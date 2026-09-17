using Server.Application.Abstractions;
using Server.Application.Services;
using Server.Hubs;
using Server.Infrastructure;
using Server.Options;
using Shared;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
// builder.Services.AddControllers();

builder.Services.Configure<GameOptions>(builder.Configuration.GetSection("Game"));

builder.Services.AddSingleton<AppMemory>();
builder.Services.AddSingleton<IGameRepository, InMemoryGameRepository>();
builder.Services.AddSingleton<IPlayerRepository, InMemoryPlayerRepository>();
builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<PlayerService>();
builder.Services.AddScoped<ILobbyFlowService, LobbyFlowService>();
builder.Services.AddScoped<TenderCatalogService>();

WebApplication app = builder.Build();

// Configuration du pipeline de requêtes HTTP.
if (app.Environment.IsDevelopment())
{
    
}

app.UseHttpsRedirection();

app.MapHub<LobbyHub>(HubRoutes.Lobby);
app.MapHub<GameHub>(HubRoutes.Game);

app.Run();
