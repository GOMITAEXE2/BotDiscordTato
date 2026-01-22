import discord
from discord.ext import commands
import os
from flask import Flask
from threading import Thread

# --- 1. SERVIDOR WEB (Para mantenerlo vivo en Render) ---
app = Flask('')

@app.route('/')
def home():
    return "¡Hola! El bot de Python está vivo 🐍"

def run():
    app.run(host='0.0.0.0', port=8080)

def keep_alive():
    t = Thread(target=run)
    t.start()

# --- 2. CONFIGURACIÓN DEL BOT ---
# Permisos necesarios (Intents)
intents = discord.Intents.default()
intents.message_content = True

bot = commands.Bot(command_prefix='t!', intents=intents)

@bot.event
async def on_ready():
    print(f'✅ Conectado como {bot.user} (ID: {bot.user.id})')
    # Sincronizamos los comandos de barra (/)
    try:
        synced = await bot.tree.sync()
        print(f'✅ Se han sincronizado {len(synced)} comandos /')
    except Exception as e:
        print(f'❌ Error al sincronizar: {e}')

# --- 3. COMANDOS ---

# Comando simple: t!hola
@bot.command()
async def hola(ctx):
    await ctx.send(f'¡Hola {ctx.author.mention}! Soy TatoBot en Python 🐍')

# Comando de Barra: /jugar
@bot.tree.command(name="jugar", description="Tira un dado")
async def jugar(interaction: discord.Interaction):
    import random
    resultado = random.randint(1, 6)
    await interaction.response.send_message(f"🎲 Dado: **{resultado}**")

# --- 4. ARRANCAR ---
if __name__ == '__main__':
    keep_alive() # Encendemos el servidor web
    token = os.environ.get("DISCORD_TOKEN") # Tomamos el token de Render
    bot.run(token)