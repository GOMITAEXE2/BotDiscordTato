# Usamos una imagen ligera de Python
FROM python:3.9-slim

# Establecemos la carpeta de trabajo
WORKDIR /app

# Copiamos la lista de compras y la instalamos
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copiamos el resto del código (main.py)
COPY . .

# Exponemos el puerto para Render
EXPOSE 8080

# Comando para arrancar el bot
CMD ["python", "main.py"]