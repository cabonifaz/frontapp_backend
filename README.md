# AppFronton — Backend API (.NET Core 10)

Backend 100% bypass: recibe request → llama al Stored Procedure de MySQL → retorna el resultado.
**Ninguna lógica de negocio vive aquí.** Toda la lógica está en los SP de `AvoSportsDB`.

---

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- VS Code con extensión **C# Dev Kit**
- MySQL accesible en `84.46.245.240:6432` con la BD `AvoSportsDB`

---

## Setup (primera vez)

```bash
# 1. Abre esta carpeta en VS Code
code .

# 2. Restaura los paquetes NuGet
dotnet restore

# 3. Asegúrate que el archivo .env existe con los datos correctos
#    (ya viene creado, solo verifica que DB_NAME=AvoSportsDB)

# 4. Corre el proyecto
dotnet run
```

El API arranca en `http://localhost:5000`.  
Swagger disponible en `http://localhost:5000/swagger`.

---

## Estructura del proyecto

```
AppFronton/
├── Controllers/
│   ├── AuthController.cs          → sp_auth_*
│   ├── MaestroController.cs       → sp_maestro_*
│   ├── UsuarioController.cs       → sp_usuario_*
│   ├── RankingController.cs       → sp_ranking_*
│   ├── PartidoController.cs       → sp_partido_*, sp_gestion_*
│   ├── SolicitudController.cs     → sp_solicitud_*
│   ├── ResultadoController.cs     → sp_resultado_*
│   └── ClaseController.cs         → sp_clase_*
├── Data/
│   └── DbContext.cs               → Conexión MySQL
├── Helpers/
│   ├── SpHelper.cs                → Ejecutor de SP (QueryAsync / ExecuteAsync)
│   └── JwtHelper.cs               → Generación y lectura de JWT
├── .env                           → Variables de entorno (NO subir al repo)
├── .gitignore
├── Program.cs                     → Configuración de la app
└── AppFronton.csproj
```

---

## Cómo agregar un endpoint nuevo

1. Identifica el SP en DBeaver.
2. Ve al controller correspondiente.
3. Copia cualquier método existente como plantilla.
4. Cambia el nombre del SP y los parámetros. Listo.

---

## Variables de entorno (.env)

| Variable | Valor |
|---|---|
| DB_HOST | 84.46.245.240 |
| DB_PORT | 6432 |
| DB_NAME | AvoSportsDB |
| DB_USER | dev_admin |
| DB_PASSWORD | NuevaClaveAqui123! |
| JWT_SECRET | (clave para firmar tokens) |
| JWT_ISSUER | AppFronton |
| JWT_AUDIENCE | AppFrontonClient |
| JWT_EXPIRES_HOURS | 24 |

> ⚠️ **El archivo `.env` está en `.gitignore`. Nunca lo subas al repositorio.**  
> Cris debe configurar las variables de entorno en el servidor de producción por separado.
