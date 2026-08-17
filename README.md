# SpotyDownloaderMP

Descargador de música que busca en Spotify (artistas, canciones o playlists) y descarga el audio desde YouTube, con metadatos (título, artista, álbum, carátula, año, número de pista) embebidos automáticamente.

Formatos disponibles: **MP3, FLAC, WAV, M4A y OPUS**.

## Requisitos

### yt-dlp

**Linux**
```bash
# Con pip
pip install yt-dlp

# Con el gestor de paquetes
sudo apt install yt-dlp        # Debian/Ubuntu
sudo pacman -S yt-dlp          # Arch
```

**Windows**
```powershell
winget install yt-dlp
```

### ffmpeg (necesario para convertir el audio)

yt-dlp descarga el audio y ffmpeg lo convierte al formato elegido, así que hace falta para todos los formatos, no solo MP3.

**Linux**
```bash
sudo apt install ffmpeg        # Debian/Ubuntu
sudo pacman -S ffmpeg          # Arch
```

**Windows**
```powershell
winget install ffmpeg
```

Ambos deben estar en el **PATH**. La barra superior de la app muestra su versión (`v1.0.0 · yt-dlp 2025.xx.xx`) o el aviso `yt-dlp no encontrado`.

---

## Configuración de Spotify

La app necesita credenciales de la API de Spotify para buscar.

> **El archivo `appsettings.json` no se incluye en el repositorio** (está en `.gitignore`, porque contiene credenciales privadas). Tienes que crearlo tú a partir de la plantilla `appsettings.example.json`.

1. Entra en [Spotify for Developers](https://developer.spotify.com/dashboard) e inicia sesión.
2. Crea una nueva aplicación (el nombre y la descripción son libres).
3. Copia el **Client ID** y el **Client Secret**.
4. Crea un archivo llamado `appsettings.json` con este contenido y pega tus credenciales:

```json
{
  "Spotify": {
    "ClientId": "TU_CLIENT_ID",
    "ClientSecret": "TU_CLIENT_SECRET"
  }
}
```

5. Guárdalo en el sitio correcto según cómo uses la app:

| Cómo usas la app | Dónde va `appsettings.json` |
|---|---|
| Release descargado / compilado | En la misma carpeta que el ejecutable |
| Desde el código fuente (`dotnet run`) | En `SpotyDownloaderMP/`, junto a `SpotyDownloaderMP.csproj` |

Atajo desde el código fuente:

```bash
cp SpotyDownloaderMP/appsettings.example.json SpotyDownloaderMP/appsettings.json
```

```powershell
copy SpotyDownloaderMP\appsettings.example.json SpotyDownloaderMP\appsettings.json
```

Después edita el archivo copiado con tus credenciales. El `appsettings.json` que crees queda ignorado por Git, así que no se subirá por accidente.

La app usa el flujo *Client Credentials*, que solo da acceso al catálogo público: no hace falta iniciar sesión con tu cuenta ni configurar URLs de redirección, pero tampoco puede acceder a tus playlists privadas.

---

## Uso

1. Elige el **modo de búsqueda** en el desplegable de la izquierda: **Artista**, **Canción** o **Playlist**.
2. Escribe el término de búsqueda y pulsa **Buscar** o **Enter**.
3. Elige el **formato de audio** en el desplegable de la derecha (MP3 por defecto).
4. Todos los resultados llegan seleccionados. Desmarca los que no quieras con su checkbox, o usa **Seleccionar todos** / **Deseleccionar todos**.
5. Pulsa **Descargar** y espera. La barra de progreso y el texto de estado muestran el avance canción a canción.

### Modos de búsqueda

| Modo | Qué busca | Qué se descarga |
|---|---|---|
| **Artista** | El artista y sus álbumes y singles (hasta 50, sin duplicados) | Todas las canciones de los álbumes seleccionados |
| **Canción** | Canciones sueltas (hasta 50 resultados) | Solo las canciones marcadas |
| **Playlist** | Playlists públicas (hasta 50 resultados) | Todas las canciones de las playlists seleccionadas |

Al cambiar de modo se limpian los resultados anteriores.

### Actualizar yt-dlp

El botón **Actualizar yt-dlp** de la barra superior ejecuta `yt-dlp --update` y refresca la versión mostrada. Útil cuando YouTube cambia algo y las descargas empiezan a fallar.

> Si instalaste yt-dlp con el gestor de paquetes (`apt`, `pacman`, `winget`), es probable que `--update` falle porque el binario lo gestiona el sistema. En ese caso actualízalo con el mismo gestor con el que lo instalaste.

### Dónde se guardan las canciones

Las canciones se guardan en la carpeta de música del sistema, organizadas por artista y álbum:

```
~/Music/                          (Linux)
C:\Users\{usuario}\Music\         (Windows)
  └── Artista\
        └── Nombre del álbum\
              ├── Cancion 1.mp3
              ├── Cancion 2.mp3
              └── ...
```

- La carpeta de artista usa el **primer artista** de la canción (en las colaboraciones, el principal).
- En modo Playlist, cada canción va a la carpeta de **su álbum**, no a una carpeta con el nombre de la playlist.
- Los caracteres no válidos para nombres de archivo se sustituyen por `_`.
- Si el archivo ya existe no se vuelve a descargar (`--no-overwrites`).

### Logs de descarga

Dentro de cada carpeta de artista se genera un archivo `download.log` con el resultado de cada canción:

```
[2026-07-08 21:33:20] OK    | Bohemian Rhapsody | A Night at the Opera | pista 1
[2026-07-08 21:34:05] ERROR | Some Song | Album | pista 3
              yt-dlp (código 1): ERROR: unable to download video data: HTTP Error 403
```

Si una canción falla, la descarga continúa con las demás. Al terminar, el estado muestra cuántas se descargaron correctamente y cuántas fallaron.

### Metadatos

Tras cada descarga se escriben las etiquetas con TagLib#: título, álbum, artistas, artista del álbum, número de pista, año y carátula. Si el formato elegido no admite alguna etiqueta (WAV y OPUS son más limitados que MP3 o FLAC), el archivo se guarda igual y solo se pierden esos datos.

---

## Compilar desde el código fuente

### Requisitos

- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)

### Release para Linux y Windows

```bash
./build.sh          # Linux / macOS (necesita 'zip' y 'tar')
```

```powershell
.\build.bat         # Windows
```

Ambos scripts generan en la carpeta `dist/`:

```
dist/
  SpotyDownloaderMP-linux-x64.tar.gz
  SpotyDownloaderMP-windows-x64.zip
```

Los binarios son autocontenidos y de archivo único (no requieren tener .NET instalado en la máquina de destino).

> **Ojo con las credenciales:** el `appsettings.json` que tengas en `SpotyDownloaderMP/` se copia a la salida de compilación, así que acaba dentro de los paquetes de `dist/`. Si vas a repartir un release, borra o sustituye ese archivo dentro del paquete antes de publicarlo.

### Debug local

```bash
dotnet run --project SpotyDownloaderMP/SpotyDownloaderMP.csproj
```

---

## Errores comunes

| Error | Causa | Solución |
|---|---|---|
| `No se encontró 'yt-dlp'` | yt-dlp no está instalado o no está en PATH | Instalar yt-dlp y reiniciar la app |
| `ERROR: ffmpeg not found` (en `download.log`) | ffmpeg no está instalado o no está en PATH | Instalar ffmpeg y reiniciar la app |
| `HTTP Error 403: Forbidden` | YouTube bloquea ese vídeo concreto | Probar **Actualizar yt-dlp**; si sigue, la canción se marca como error y la descarga continúa |
| `No se encontró ningún artista` | El artista no existe en Spotify o hay un error tipográfico | Revisar el nombre |
| `Faltan ClientId o ClientSecret` | `appsettings.json` no tiene las credenciales | Ver sección de configuración de Spotify |
| `The configuration file 'appsettings.json' was not found` | No has creado el `appsettings.json` (no viene en el repositorio) | Crearlo a partir de `appsettings.example.json`, ver sección de configuración de Spotify |
| `Error: Invalid client` al buscar | Las credenciales existen pero son incorrectas o están revocadas | Regenerar el Client Secret en el dashboard de Spotify |
