using Discord;
using Discord.WebSocket;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

// --- SERVIDOR WEB FALSO (Para Render) ---
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.MapGet("/", () => "El bot está funcionando 🤖");
_ = app.RunAsync($"http://0.0.0.0:{port}");

// --- CONFIGURACIÓN DEL BOT ---
var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");

// AQUÍ ESTÁ EL SOSPECHOSO: Asegúrate que este número sea el correcto
// Pero con este nuevo código, veremos en los logs cuál es el número real que ve el bot.
const ulong MI_SERVIDOR_ID = 1008231468981555260; // <--- TU ID ACTUAL (Probablemente incorrecta)

var config = new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
};

using var client = new DiscordSocketClient(config);

client.Log += LogAsync;
client.MessageReceived += HandleCommandAsync;

await client.LoginAsync(TokenType.Bot, token);
await client.StartAsync();
await Task.Delay(-1);

// --- LÓGICA ---

async Task HandleCommandAsync(SocketMessage messageParam)
{
    if (messageParam is not SocketUserMessage message) return;
    if (message.Author.IsBot) return;
    if (message.Channel is not SocketGuildChannel channel) return;

    // --- ZONA DE DIAGNÓSTICO ---
    // Esto imprimirá en la consola de Render cada vez que escribas algo
    Console.WriteLine($"[DEBUG] Mensaje recibido: '{message.Content}'");
    Console.WriteLine($"[DEBUG] ID del Servidor actual: {channel.Guild.Id}");
    Console.WriteLine($"[DEBUG] ID Configurada en código: {MI_SERVIDOR_ID}");

    // Verificación de seguridad
    if (channel.Guild.Id != MI_SERVIDOR_ID)
    {
        Console.WriteLine($"[BLOQUEO] El mensaje fue ignorado porque la ID no coincide.");
        // TRUCO TEMPORAL: Descomenta la siguiente línea si quieres que funcione IGUAL aunque la ID esté mal
        // return; 

        // Por ahora, le diremos al bot que nos avise del error en Discord para que lo veas
        await message.Channel.SendMessageAsync($"⚠ **Error de Configuración:**\nMi ID programada es `{MI_SERVIDOR_ID}`\nPero estoy en el servidor `{channel.Guild.Id}`\n¡Cambia la ID en el código!");
        return;
    }

    // Comandos
    if (message.Content == "!hola")
    {
        await message.Channel.SendMessageAsync($"¡Hola {message.Author.Username}! ID Verificada ✅");
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