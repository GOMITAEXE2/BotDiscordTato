using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Discord.Interactions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Reflection;

public class Program
{
    // PON TU ID REAL AQUI ABAJO O NO FUNCIONARAN LOS COMANDOS "/"
    const ulong MI_SERVIDOR_ID = 1008231468981555260;

    private DiscordSocketClient _client = null!;
    private CommandService _commands = null!;
    private InteractionService _interactions = null!;
    private IServiceProvider _services = null!;

    public static Task Main(string[] args) => new Program().MainAsync(args);

    public async Task MainAsync(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();
        app.MapGet("/", () => "Bot Online");
        _ = app.RunAsync($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}");

        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent,
            AlwaysDownloadUsers = true
        };

        _client = new DiscordSocketClient(config);
        _commands = new CommandService();
        _interactions = new InteractionService(_client);

        _services = new ServiceCollection()
            .AddSingleton(_client)
            .AddSingleton(_commands)
            .AddSingleton(_interactions)
            .BuildServiceProvider();

        _client.Log += LogAsync;
        _client.Ready += ReadyAsync;
        _client.MessageReceived += HandleTextCommandAsync;
        _client.InteractionCreated += HandleInteractionAsync;

        await _client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("DISCORD_TOKEN"));
        await _client.StartAsync();
        await Task.Delay(-1);
    }

    private async Task ReadyAsync()
    {
        Console.WriteLine($"Conectado: {_client.CurrentUser.Username}");
        try
        {
            await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
            await _interactions.RegisterCommandsToGuildAsync(MI_SERVIDOR_ID);
        }
        catch (Exception ex) { Console.WriteLine($"Error registrando comandos: {ex.Message}"); }
    }

    private async Task HandleTextCommandAsync(SocketMessage arg)
    {
        if (arg is not SocketUserMessage message || message.Author.IsBot) return;

        int argPos = 0;
        if (message.HasStringPrefix("t!", ref argPos))
        {
            // Truco para que t! funcione igual que /
            if (message.Content == "t!jugar")
            {
                await new GameModule().EnviarPanelJuego(message.Channel, message.Author.Id);
                return;
            }
            if (message.Content == "t!ping")
            {
                await message.Channel.SendMessageAsync("¡Pong! Estoy vivo 🤖");
                return;
            }

            var context = new SocketCommandContext(_client, message);
            await _commands.ExecuteAsync(context, argPos, _services);
        }
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        var ctx = new SocketInteractionContext(_client, interaction);
        await _interactions.ExecuteCommandAsync(ctx, _services);
    }

    private Task LogAsync(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }
}

public class GameModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("jugar", "Abre el panel")]
    public async Task JugarCommand()
    {
        await EnviarPanelJuego(Context.Channel, Context.User.Id, true);
    }

    // Metodo compartido para t!jugar y /jugar
    public async Task EnviarPanelJuego(IMessageChannel channel, ulong userId, bool isSlash = false)
    {
        var wins = RankingSystem.GetWins(userId);
        var embed = new EmbedBuilder()
            .WithTitle("🎮 Centro de Juegos")
            .WithDescription("Elige:")
            .WithColor(Color.Purple)
            .AddField("🏆 Tus Victorias", $"{wins}", true);

        var builder = new ComponentBuilder()
            .WithButton("Dados", "juego_dados", ButtonStyle.Primary, new Emoji("🎲"))
            .WithButton("Duelo", "juego_duelo", ButtonStyle.Danger, new Emoji("⚔"))
            .WithButton("Ranking", "ver_ranking", ButtonStyle.Secondary, new Emoji("📜"));

        if (isSlash) await RespondAsync(embed: embed.Build(), components: builder.Build());
        else await channel.SendMessageAsync(embed: embed.Build(), components: builder.Build());
    }

    [ComponentInteraction("juego_dados")]
    public async Task BotonDados()
    {
        var rnd = new Random();
        int u = rnd.Next(1, 7), b = rnd.Next(1, 7);
        string r = u > b ? "Ganaste 🎉" : (u < b ? "Perdiste 💩" : "Empate");

        if (u > b) RankingSystem.AddWin(Context.User.Id, Context.User.Username);
        await RespondAsync($"🎲 Tú: {u} | Bot: {b} -> **{r}**");
    }

    [ComponentInteraction("ver_ranking")]
    public async Task BotonRanking()
    {
        var top = RankingSystem.GetTopPlayers();
        var lista = top.Count > 0 ? string.Join("\n", top.Select((p, i) => $"#{i + 1} {p.Name}: {p.Wins}")) : "Vacio";
        await RespondAsync(embed: new EmbedBuilder().WithTitle("🏆 Top 10").WithDescription(lista).WithColor(Color.Gold).Build(), ephemeral: true);
    }

    [ComponentInteraction("juego_duelo")]
    public async Task BotonDuelo()
    {
        await RespondAsync($"⚔ **{Context.User.Username}** busca rival.", components: new ComponentBuilder().WithButton("Aceptar", $"aceptar_duelo:{Context.User.Id}", ButtonStyle.Success).Build());
    }

    [ComponentInteraction("aceptar_duelo:*")]
    public async Task AceptarDuelo(string idRival)
    {
        ulong rId = ulong.Parse(idRival);
        if (Context.User.Id == rId) { await RespondAsync("No puedes pelear solo.", ephemeral: true); return; }

        var rnd = new Random();
        int rollR = rnd.Next(1, 100), rollA = rnd.Next(1, 100);
        ulong winnerId = rollR > rollA ? rId : Context.User.Id;
        string wName = rollR > rollA ? "Retador" : Context.User.Username;

        RankingSystem.AddWin(winnerId, wName);
        await RespondAsync($"⚔ Retador: {rollR} vs Aceptante: {rollA}\n🏆 Gana: <@{winnerId}>");
    }
}

public static class RankingSystem
{
    private static Dictionary<ulong, PlayerData> _ranking = new();
    private const string FILE = "ranking.json";

    static RankingSystem()
    {
        if (File.Exists(FILE)) try { _ranking = JsonConvert.DeserializeObject<Dictionary<ulong, PlayerData>>(File.ReadAllText(FILE)) ?? new(); } catch { }
    }

    public static void AddWin(ulong id, string name)
    {
        if (!_ranking.ContainsKey(id)) _ranking[id] = new PlayerData();
        _ranking[id].Wins++; _ranking[id].Name = name;
        try { File.WriteAllText(FILE, JsonConvert.SerializeObject(_ranking)); } catch { }
    }

    public static int GetWins(ulong id) => _ranking.ContainsKey(id) ? _ranking[id].Wins : 0;
    public static List<PlayerData> GetTopPlayers() => _ranking.Values.OrderByDescending(p => p.Wins).Take(10).ToList();

    public class PlayerData { public string Name { get; set; } = "Jugador"; public int Wins { get; set; } }
}