using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Discord.Interactions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

public class Program
{
    const ulong MI_SERVIDOR_ID = 1008231468981555260;

    private DiscordSocketClient _client = null!;
    private InteractionService _interactions = null!;
    private IServiceProvider _services = null!;

    public static Task Main(string[] args) => new Program().MainAsync(args);

    public async Task MainAsync(string[] args)
    {
        // 1. Servidor Web Falso (Para que Render no duerma al bot)
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();
        app.MapGet("/", () => "TatoBot: Modo RAM y ID Correcta 🟢");
        _ = app.RunAsync($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}");

        // 2. Configuración
        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent,
            AlwaysDownloadUsers = true
        };

        _client = new DiscordSocketClient(config);
        _interactions = new InteractionService(_client.Rest);

        _services = new ServiceCollection()
            .AddSingleton(_client)
            .AddSingleton(_interactions)
            .BuildServiceProvider();

        _client.Log += LogAsync;
        _interactions.Log += LogAsync;
        _client.Ready += ReadyAsync;
        _client.MessageReceived += HandleTextCommandAsync;
        _client.InteractionCreated += HandleInteractionAsync;

        await _client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("DISCORD_TOKEN"));
        await _client.StartAsync();
        await Task.Delay(-1);
    }

    private async Task ReadyAsync()
    {
        Console.WriteLine($"[BOT] Conectado como {_client.CurrentUser.Username}");
        try
        {
            await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
            // Registramos los comandos DIRECTAMENTE en tu servidor
            await _interactions.RegisterCommandsToGuildAsync(MI_SERVIDOR_ID);
            Console.WriteLine($"[EXITO] Comandos registrados en el servidor {MI_SERVIDOR_ID}");
        }
        catch (Exception ex) { Console.WriteLine($"[ERROR FATAL] {ex.Message}"); }
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        try
        {
            var ctx = new SocketInteractionContext(_client, interaction);
            await _interactions.ExecuteCommandAsync(ctx, _services);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR INTERACCION] {ex}");
            if (interaction.Type == InteractionType.MessageComponent)
            {
                await interaction.RespondAsync($"🔥 **Error:** {ex.Message}", ephemeral: true);
            }
        }
    }

    private async Task HandleTextCommandAsync(SocketMessage arg)
    {
        if (arg is not SocketUserMessage message || message.Author.IsBot) return;

        // Atajo: t!jugar abre el panel igual que /jugar
        if (message.Content == "t!jugar")
        {
            await new GameModule().EnviarPanelJuego(message.Channel, message.Author.Id);
        }
    }

    private Task LogAsync(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }
}

// --- MÓDULO DE JUEGO ---
public class GameModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("jugar", "Panel de juegos")]
    public async Task JugarCommand()
    {
        await EnviarPanelJuego(Context.Channel, Context.User.Id, true);
    }

    public async Task EnviarPanelJuego(IMessageChannel channel, ulong userId, bool isSlash = false)
    {
        var wins = RankingEnMemoria.GetWins(userId);

        var embed = new EmbedBuilder()
            .WithTitle("🎰 Casino TatoBot")
            .WithDescription("¡Prueba tu suerte!")
            .WithColor(Color.Green)
            .AddField("Tus Victorias", $"{wins} 🏆", true);

        var builder = new ComponentBuilder()
            .WithButton("Tirar Dados", "juego_dados", ButtonStyle.Primary, new Emoji("🎲"))
            .WithButton("Ranking (RAM)", "ver_ranking", ButtonStyle.Secondary, new Emoji("📜"));

        if (isSlash) await RespondAsync(embed: embed.Build(), components: builder.Build());
        else await channel.SendMessageAsync(embed: embed.Build(), components: builder.Build());
    }

    [ComponentInteraction("juego_dados")]
    public async Task BotonDados()
    {
        var rnd = new Random();
        int u = rnd.Next(1, 7);
        int b = rnd.Next(1, 7);
        string resultado;

        if (u > b)
        {
            resultado = "¡Ganaste! 🎉";
            RankingEnMemoria.AddWin(Context.User.Id);
        }
        else if (u < b) resultado = "Perdiste... 💩";
        else resultado = "Empate 🤝";

        await RespondAsync($"🎲 Tú: {u} | Bot: {b} -> **{resultado}**");
    }

    [ComponentInteraction("ver_ranking")]
    public async Task BotonRanking()
    {
        var top = RankingEnMemoria.GetTop();
        string lista = top.Count > 0 ? string.Join("\n", top) : "Nadie ha ganado aún.";
        await RespondAsync($"🏆 **Top Ganadores (Sesión Actual):**\n{lista}", ephemeral: true);
    }
}

// --- SISTEMA DE RANKING EN MEMORIA (No falla por permisos) ---
public static class RankingEnMemoria
{
    private static Dictionary<ulong, int> _victorias = new();

    public static void AddWin(ulong id)
    {
        if (!_victorias.ContainsKey(id)) _victorias[id] = 0;
        _victorias[id]++;
    }

    public static int GetWins(ulong id) => _victorias.ContainsKey(id) ? _victorias[id] : 0;

    public static List<string> GetTop()
    {
        return _victorias.OrderByDescending(x => x.Value)
                         .Take(5)
                         .Select(x => $"<@{x.Key}>: {x.Value}")
                         .ToList();
    }
}