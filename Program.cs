using Discord;
using Discord.WebSocket;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

// --- CONFIGURACIÓN DEL SERVIDOR WEB (TRUCO PARA RENDER) ---
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Render nos asignará un puerto dinámico en la variable "PORT".
// Si no existe (en tu PC), usará el 8080.
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";

// Creamos una ruta simple para que el "ping" funcione
app.MapGet("/", () => "El bot está funcionando 🤖");

// Iniciamos el servidor web en segundo plano para no bloquear el bot
// Escuchamos en 0.0.0.0 para que sea accesible desde fuera
_ = app.RunAsync($"http://0.0.0.0:{port}");

// --- AQUI EMPIEZA TU BOT DE SIEMPRE ---

// Variables de configuración
var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN") ?? "TOKEN_DE_COMPILADO";
const ulong MI_SERVIDOR_ID = 123456789012345678; // <--- REEMPLAZA CON TU ID

var config = new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
};

using var client = new DiscordSocketClient(config);

client.Log += LogAsync;
client.MessageReceived += HandleCommandAsync;

// Iniciar el Bot
await client.LoginAsync(TokenType.Bot, token);
await client.StartAsync();

// Mantenemos el proceso vivo (ahora dependemos del ciclo de vida del bot)
await Task.Delay(-1);


// --- LÓGICA DEL BOT ---

async Task HandleCommandAsync(SocketMessage messageParam)
{
    if (messageParam is not SocketUserMessage message) return;
    if (message.Author.IsBot) return;
    if (message.Channel is not SocketGuildChannel channel) return;

    // Restricción de servidor
    if (channel.Guild.Id != MI_SERVIDOR_ID) return;

    if (message.Content == "!hola")
    {
        await message.Channel.SendMessageAsync($"¡Hola! Soy un bot web híbrido en Linux.");
    }

    if (message.Content == "!jugar")
    {
        var rnd = new Random();
        int numeroGanador = rnd.Next(1, 11);
        await message.Channel.SendMessageAsync("🎲 **Juego de Dados** 🎲\nSi sacas más de 5, ganas.");

        int numeroUsuario = rnd.Next(1, 11);
        string resultado = numeroUsuario > 5 ? "¡Ganaste! 🎉" : "Perdiste... 😢";

        await message.Channel.SendMessageAsync($"Tu número: {numeroUsuario}. Resultado: {resultado}");
    }
}

Task LogAsync(LogMessage msg)
{
    Console.WriteLine(msg.ToString());
    return Task.CompletedTask;
}