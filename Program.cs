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
    // --- CONFIGURACIÓN ---
    // ¡IMPORTANTE! Cambia esto por la ID real de tu servidor
    const ulong MI_SERVIDOR_ID = 1008231468981555260; // <--- PON TU ID AQUÍ

    // Corrección de Advertencias (CS8618): Inicializamos con null! para que C# no se queje
    private DiscordSocketClient _client = null!;
    private CommandService _commands = null!;
    private InteractionService _interactions = null!;
    private IServiceProvider _services = null!;

    // Corrección del Error (CS0103): Pasamos 'args' del Main al MainAsync
    public static Task Main(string[] args) => new Program().MainAsync(args);

    public async Task MainAsync(string[] args)
    {
        // 1. Servidor Web Falso para Render
        // Ahora sí 'args' existe aquí porque lo pasamos en el paréntesis de arriba
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();
        app.MapGet("/", () => "El TatoBot está ONLINE 🤖");
        _ = app.RunAsync($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}");

        // 2. Configuración del Cliente Discord
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

        // 3. Eventos
        _client.Log += LogAsync;
        _client.Ready += ReadyAsync;
        _client.MessageReceived += HandleTextCommandAsync;
        _client.InteractionCreated += HandleInteractionAsync;

        // 4. Login
        var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        await Task.Delay(-1);
    }

    private async Task ReadyAsync()
    {
        Console.WriteLine($"[BOT] Conectado como {_client.CurrentUser.Username}");

        try
        {
            // Registramos los comandos en tu servidor específico
            await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
            await _interactions.RegisterCommandsToGuildAsync(MI_SERVIDOR_ID);
            Console.WriteLine("[BOT] Comandos '/' registrados exitosamente.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Fallo al registrar comandos: {ex.Message}");
        }
    }

    private async Task HandleTextCommandAsync(SocketMessage arg)
    {
        if (arg is not SocketUserMessage message || message.Author.IsBot) return;

        int argPos = 0;
        // Prefijo "t!" configurado
        if (message.HasStringPrefix("t!", ref argPos))
        {
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

// ==========================================
// MÓDULO DE JUEGOS Y COMANDOS
// ==========================================
public class GameModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("jugar", "Abre el panel de juegos")]
    public async Task JugarCommand()
    {
        var wins = RankingSystem.GetWins(Context.User.Id);

        var embed = new EmbedBuilder()
            .WithTitle("🎮 Centro de Juegos TatoBot")
            .WithDescription("Selecciona una opción:")
            .WithColor(Color.Purple)
            .AddField("🏆 Tu Ranking", $"Victorias: {wins}", true);

        var builder = new ComponentBuilder()
            .WithButton("Dados (Solo)", "juego_dados", ButtonStyle.Primary, new Emoji("🎲"))
            .WithButton("Duelo (Multi)", "juego_duelo", ButtonStyle.Danger, new Emoji("⚔"))
            .WithButton("Ver Ranking", "ver_ranking", ButtonStyle.Secondary, new Emoji("📜"));

        await RespondAsync(embed: embed.Build(), components: builder.Build());
    }

    [ComponentInteraction("juego_dados")]
    public async Task BotonDados()
    {
        var rnd = new Random();
        int usuario = rnd.Next(1, 7);
        int bot = rnd.Next(1, 7);
        string resultado;

        if (usuario > bot)
        {
            resultado = "¡Ganaste! 🎉";
            RankingSystem.AddWin(Context.User.Id, Context.User.Username);
        }
        else if (usuario < bot) resultado = "Perdiste... 💩";
        else resultado = "Empate 🤝";

        await RespondAsync($"🎲 **Dados:** Tú [{usuario}] vs Bot [{bot}] -> **{resultado}**");
    }

    [ComponentInteraction("ver_ranking")]
    public async Task BotonRanking()
    {
        var top = RankingSystem.GetTopPlayers();
        var lista = top.Count > 0
            ? string.Join("\n", top.Select((p, i) => $"**#{i + 1}** {p.Name}: {p.Wins} victorias"))
            : "Nadie ha ganado aún...";

        var embed = new EmbedBuilder()
            .WithTitle("🏆 Salón de la Fama")
            .WithDescription(lista)
            .WithColor(Color.Gold);

        await RespondAsync(embed: embed.Build(), ephemeral: true);
    }

    [ComponentInteraction("juego_duelo")]
    public async Task BotonDuelo()
    {
        var builder = new ComponentBuilder()
            .WithButton("¡Aceptar Reto!", $"aceptar_duelo:{Context.User.Id}", ButtonStyle.Success);

        await RespondAsync($"⚔ **{Context.User.Username}** busca rival. ¿Quién acepta?", components: builder.Build());
    }

    [ComponentInteraction("aceptar_duelo:*")]
    public async Task AceptarDuelo(string idRival)
    {
        ulong rivalId = ulong.Parse(idRival);

        if (Context.User.Id == rivalId)
        {
            await RespondAsync("¡No puedes pelear contra ti mismo!", ephemeral: true);
            return;
        }

        var rnd = new Random();
        int rollRival = rnd.Next(1, 100);
        int rollAceptante = rnd.Next(1, 100);

        string ganador;
        if (rollRival > rollAceptante)
        {
            ganador = $"Gana el retador <@{rivalId}>";
            RankingSystem.AddWin(rivalId, "Retador");
        }
        else
        {
            ganador = $"Gana <@{Context.User.Id}>";
            RankingSystem.AddWin(Context.User.Id, Context.User.Username);
        }

        await RespondAsync($"⚔ **DUELO:**\nRetador: {rollRival}\nAceptante: {rollAceptante}\n🏆 **{ganador}**");
    }
}

// ==========================================
// SISTEMA DE RANKING
// ==========================================
public static class RankingSystem
{
    private static Dictionary<ulong, PlayerData> _ranking = new();
    private const string FILE_PATH = "ranking.json";

    static RankingSystem()
    {
        if (File.Exists(FILE_PATH))
        {
            try
            {
                var json = File.ReadAllText(FILE_PATH);
                _ranking = JsonConvert.DeserializeObject<Dictionary<ulong, PlayerData>>(json) ?? new();
            }
            catch { _ranking = new(); }
        }
    }

    public static void AddWin(ulong userId, string username)
    {
        if (!_ranking.ContainsKey(userId))
            _ranking[userId] = new PlayerData { Name = username, Wins = 0 };

        _ranking[userId].Wins++;
        _ranking[userId].Name = username;
        Save();
    }

    public static int GetWins(ulong userId) => _ranking.ContainsKey(userId) ? _ranking[userId].Wins : 0;

    public static List<PlayerData> GetTopPlayers()
    {
        return _ranking.Values.OrderByDescending(p => p.Wins).Take(10).ToList();
    }

    private static void Save()
    {
        try { File.WriteAllText(FILE_PATH, JsonConvert.SerializeObject(_ranking)); } catch { }
    }

    public class PlayerData
    {
        public string Name { get; set; } = "Jugador";
        public int Wins { get; set; }
    }
}