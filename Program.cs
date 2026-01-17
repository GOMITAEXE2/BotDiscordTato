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
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();
        app.MapGet("/", () => "TatoBot v3.1: Fix Ambiguity 🟢");
        _ = app.RunAsync($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}");

        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent,
            AlwaysDownloadUsers = true,
            LogLevel = LogSeverity.Debug
        };

        _client = new DiscordSocketClient(config);

        var interactionConfig = new InteractionServiceConfig
        {
            LogLevel = LogSeverity.Debug,
            DefaultRunMode = Discord.Interactions.RunMode.Async
        };

        _interactions = new InteractionService(_client.Rest, interactionConfig);

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
        Console.WriteLine($"[CONECTADO] {_client.CurrentUser.Username}");
        try
        {
            await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
            await _interactions.RegisterCommandsToGuildAsync(MI_SERVIDOR_ID);
            Console.WriteLine($"[OK] Comandos registrados en servidor: {MI_SERVIDOR_ID}");
        }
        catch (Exception ex) { Console.WriteLine($"[ERROR REGISTRO] {ex.Message}"); }
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        try
        {
            var ctx = new SocketInteractionContext(_client, interaction);
            var result = await _interactions.ExecuteCommandAsync(ctx, _services);

            if (!result.IsSuccess)
            {
                Console.WriteLine($"[ERROR INTERACCIÓN] {result.ErrorReason}");
                if (interaction.Type == InteractionType.MessageComponent)
                {
                    await interaction.RespondAsync($"⚠ **Error interno:** {result.ErrorReason}", ephemeral: true);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CRASH] {ex}");
            if (interaction.HasResponded)
                await interaction.FollowupAsync($"🔥 Crash: {ex.Message}", ephemeral: true);
            else
                await interaction.RespondAsync($"🔥 Crash: {ex.Message}", ephemeral: true);
        }
    }

    private async Task HandleTextCommandAsync(SocketMessage arg)
    {
        if (arg is not SocketUserMessage message || message.Author.IsBot) return;
        if (message.Content == "t!jugar") await new GameModule().EnviarPanelJuego(message.Channel, message.Author.Id);
    }

    private Task LogAsync(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }
}

// --- MÓDULO DEL JUEGO ---
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
            .WithTitle("🎰 Casino TatoBot 🎰")
            .WithDescription("¡Haz clic abajo!")
            .WithColor(Color.Blue)
            .AddField("Tus Victorias", $"{wins} 🏆", true);

        // Botones con IDs 
        var builder = new ComponentBuilder()
            .WithButton("Tirar Dados", "btn_dados", ButtonStyle.Primary, new Emoji("🎲"))
            .WithButton("Ranking", "btn_ranking", ButtonStyle.Secondary, new Emoji("📜"));

        if (isSlash) await RespondAsync(embed: embed.Build(), components: builder.Build());
        else await channel.SendMessageAsync(embed: embed.Build(), components: builder.Build());
    }

    [ComponentInteraction("btn_dados")]
    public async Task BotonDados()
    {
        // 1. AVISO INMEDIATO
        await DeferAsync();

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

        // 2. RESPUESTA TARDÍA
        await FollowupAsync($"🎲 Tú: {u} | Bot: {b} -> **{resultado}**");
    }

    [ComponentInteraction("btn_ranking")]
    public async Task BotonRanking()
    {
        await DeferAsync(ephemeral: true);

        var top = RankingEnMemoria.GetTop();
        string lista = top.Count > 0 ? string.Join("\n", top) : "Nadie ha ganado aún.";

        await FollowupAsync($"🏆 **Top Ganadores:**\n{lista}", ephemeral: true);
    }
}

public static class RankingEnMemoria
{
    private static Dictionary<ulong, int> _victorias = new();

    public static void AddWin(ulong id)
    {
        if (!_victorias.ContainsKey(id)) _victorias[id] = 0;
        _victorias[id]++;
    }
    public static int GetWins(ulong id) => _victorias.ContainsKey(id) ? _victorias[id] : 0;
    public static List<string> GetTop() => _victorias.OrderByDescending(x => x.Value).Take(5).Select(x => $"<@{x.Key}>: {x.Value}").ToList();
}