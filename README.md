# CreditosApp – Examen Parcial 2026-1

Plataforma web interna para gestionar **solicitudes de crédito**: los clientes registran solicitudes y los analistas de riesgo las aprueban o rechazan.

**Stack:** ASP.NET Core MVC (.NET 10) + Identity · EF Core + SQLite · Sesión y cache en Redis · WebSocket (hub ASP.NET en `/hubs/solicitudes`) · RabbitMQ gestionado en CloudAMQP · Docker en Render.com

**URL en Render:** https://creditosapp-0ykw.onrender.com

---

## 1. Ramas y Pull Requests

| Pregunta | Rama | Contenido |
|---|---|---|
| P1 | `feature/bootstrap-dominio` | Proyecto `dotnet new mvc --auth Individual`, EF Core SQLite, modelos, restricciones, semilla |
| P2 | `feature/catalogo-solicitudes` | "Mis solicitudes" con filtros + detalle, validación server-side |
| P3 | `feature/solicitudes` | Registro de solicitud con reglas de negocio |
| P4 | `feature/sesion-redis` | Sesión en Redis (última solicitud) + cache 60 s con invalidación |
| P5 | `feature/panel-analista` | Panel `/Analista` con rol, aprobar/rechazar |
| P6 | `feature/websocket-notificaciones` | Hub WebSocket, evento `SolicitudEstadoActualizado` al propietario |
| P7 | `feature/cloudmq-notificaciones` | Productor/cola/consumidor en CloudAMQP, "Mis notificaciones" |
| P8 | `deploy/render` | Dockerfile, `render.yaml`, README |

Cada rama se crea desde la anterior ya integrada en `main` y se cierra con un PR hacia `main` (merge commit). No se trabaja directamente en `main`.

## 2. Ejecución local

Requisitos: .NET SDK 10, (opcional) herramienta EF: `dotnet tool install --global dotnet-ef`.

```bash
git clone <repo> && cd CreditosApp
dotnet restore
dotnet run                     # http://localhost:5000
```

Al iniciar, la app **aplica las migraciones automáticamente** (`Database.Migrate()`) y crea los datos semilla.

### Migraciones

| Migración | Contenido |
|---|---|
| `00000000000000_CreateIdentitySchema` | Tablas de Identity (plantilla) |
| `20260924150000_Dominio` | `Clientes`, `SolicitudesCredito`, check constraints, índice único filtrado |
| `20260924160000_Notificaciones` | `Notificaciones` (MessageId único) y columnas de seguimiento de publicación |

Comandos manuales:
```bash
dotnet ef database update                 # aplicar
dotnet ef migrations add <Nombre>         # nueva migración
dotnet ef migrations has-pending-model-changes
```

### Usuarios semilla (contraseña `Examen2026!`)

| Usuario | Rol / datos |
|---|---|
| `cliente1@creditos.pe` | Cliente, ingresos S/ 3,000, **1 solicitud Pendiente** (S/ 12,000) |
| `cliente2@creditos.pe` | Cliente, ingresos S/ 5,000, **1 solicitud Aprobada** (S/ 20,000) |
| `analista@creditos.pe` | Rol **Analista** |

Un usuario nuevo registrado en `/Identity/Account/Register` crea su perfil de cliente (ingresos) en **Mis solicitudes → Perfil**.

## 3. Variables de entorno

Nunca se suben credenciales al repositorio. En local usar *user-secrets* o variables de entorno:

```bash
dotnet user-secrets init
dotnet user-secrets set "Redis:ConnectionString" "redis-xxxxx.c1.us-east-1-2.ec2.redns.redis-cloud.com:12345,password=XXXX,abortConnect=false"
dotnet user-secrets set "RabbitMq:ConnectionString" "amqps://usuario:clave@jackal.rmq.cloudamqp.com/usuario"
```

| Variable | Ejemplo / descripción |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_URLS` | `http://0.0.0.0:${PORT}` (en Render la expande el `CMD` del Dockerfile) |
| `ConnectionStrings__DefaultConnection` | `Data Source=/var/data/creditos.db` |
| `Redis__ConnectionString` | `host:puerto,password=...,abortConnect=false` (Redis Cloud). Agregar `ssl=true` si la BD tiene TLS |
| `RabbitMq__ConnectionString` | URI **amqps** de CloudAMQP (pestaña *Details → AMQP URL*, cambiar `amqp://` por `amqps://`) |
| `RabbitMq__QueueName` | `solicitudes.notificaciones` |
| `RabbitMq__ConsumerEnabled` | `true` / `false` |

Si `Redis__ConnectionString` está vacío se usa cache en memoria (solo desarrollo).

## 4. Reglas de negocio (validadas en servidor)

- `IngresosMensuales > 0` y `MontoSolicitado > 0`: DataAnnotations + **CHECK constraints** en SQLite.
- Una sola solicitud **Pendiente** por cliente: validación en servicio + **índice único filtrado** `IX_SolicitudesCredito_ClienteId_Pendiente (WHERE Estado = 0)`.
- Registro: usuario autenticado, cliente activo, monto ≤ **10×** ingresos.
- Aprobación: monto ≤ **5×** ingresos; no se procesan solicitudes ya aprobadas/rechazadas; motivo obligatorio al rechazar.
- Filtros: montos no negativos, `MontoMin ≤ MontoMax`, `FechaInicio ≤ FechaFin` (`IValidatableObject`).

## 5. Sesión y cache Redis (P4)

- **Sesión** (`AddSession` sobre `IDistributedCache` Redis): al abrir un detalle se guarda la última solicitud y el layout muestra **"Ver última solicitud S/ X"**.
- **Cache**: clave `creditos:solicitudes:usuario:{userId}`, TTL **60 s**. La vista muestra el origen (badge *cache Redis* / *base de datos*) y los logs muestran `Cache HIT/MISS`.
- **Invalidación**: al registrar una solicitud y al aprobar/rechazar (se borra la clave del propietario).
- Las llaves de DataProtection también se guardan en Redis, así las cookies siguen válidas tras redeploys.

## 6. WebSocket (P6)

- Hub `SolicitudesHub` en `/hubs/solicitudes` con `[Authorize]` (Identity) y **solo transporte WebSocket**.
- Conexión anónima → **401** (el cookie handler responde 401 en `/hubs` en lugar de redirigir).
- Al aprobar/rechazar: 1) se guarda en BD, 2) se invalida la cache Redis, 3) se emite `SolicitudEstadoActualizado { solicitudId, estado, motivoRechazo }` con `Clients.User(usuarioId)`; el `usuarioId` se obtiene **en el servidor** desde la BD (el hub no expone métodos para que el navegador elija destinatario).
- "Mis solicitudes" y "Detalle" muestran el estado de la conexión (badge abajo a la derecha), actualizan el badge del estado y muestran un aviso sin recargar.
- Reconexión automática (+ reintento manual cada 5 s). Al reconectar se consulta `GET /Solicitudes/Estados` para recuperar cambios ocurridos durante la desconexión.

### Prueba
1. Navegador A (normal): `cliente1@creditos.pe` → Mis solicitudes. Navegador B (incógnito): `analista@creditos.pe` → Panel Analista. Navegador C (otro perfil): `cliente2@creditos.pe` → Mis solicitudes.
2. En B aprobar/rechazar la solicitud de cliente1 → en A cambia el estado y aparece el aviso al instante; en C no llega nada.
3. DevTools → **Network → filtro WS** → `hubs/solicitudes` con estado **101 Switching Protocols** (en Render `wss://`). En *Messages* se ve el evento.
4. Conexión anónima: en una ventana sin sesión abrir la consola y ejecutar `new WebSocket("wss://<host>/hubs/solicitudes")` → falla con **401**; o `curl -i https://<host>/hubs/solicitudes/negotiate?negotiateVersion=1 -X POST` → `401 Unauthorized`.

## 7. Cloud MQ con CloudAMQP (P7)

Flujo: **registrar solicitud → guardar en SQLite → publicar `SolicitudRegistrada` → cola `solicitudes.notificaciones` → consumidor `BackgroundService` → tabla `Notificaciones` → vista "Mis notificaciones"**.

- Conexión **AMQPS** (`amqps://`, TLS 5671) con credenciales solo por variables de entorno.
- Cola **durable** `solicitudes.notificaciones` (+ DLQ durable `solicitudes.notificaciones.dlq`). **No crear la cola a mano** en CloudAMQP: la app la declara con los argumentos de dead-letter (si existe con otros argumentos, borrarla).
- Mensaje JSON **persistente** (`delivery_mode=2`):
  ```json
  { "Tipo": "SolicitudRegistrada", "MessageId": "3f2b…-uuid", "SolicitudId": 7, "UsuarioId": "…", "FechaEventoUtc": "2026-09-24T20:15:00Z" }
  ```
- Se publica **solo después** de validar y guardar la solicitud. **Publisher confirms**: se espera el ACK del broker (`mandatory=true`); si falla, la solicitud se conserva, se registra el error y se muestra *"la notificación no pudo encolarse"*.
- Consumidor (`RabbitMQ.Client` 7, `autoAck=false`):
  - **ACK manual** solo después de guardar la `Notificacion`.
  - **Idempotencia**: índice único en `MessageId`; si ya existe → ACK sin insertar.
  - **Mensaje inválido** (JSON mal formado, campos vacíos, solicitud inexistente) → `BasicReject(requeue:false)` → DLQ + log `Mensaje INVÁLIDO`.
  - **Error de procesamiento** → log + `BasicNack`; se reencola **una sola vez** (si no era redelivery) y luego va a la DLQ. Sin reintentos infinitos.
  - El consumidor no aprueba ni rechaza créditos.

### Reenvío manual (mismo MessageId)
El `MessageId` se guarda en `SolicitudesCredito.NotificacionMessageId`.
- **Opción A (app):** Panel Analista → *Cloud MQ – Reenvío manual* → ingresar Id de solicitud → **Reenviar mismo MessageId**. Las solicitudes cuya publicación falló aparecen listadas con botón *Reenviar*.
- **Opción B (CloudAMQP):** RabbitMQ Manager → *Queues* → `solicitudes.notificaciones.dlq` → *Get messages* (copiar el payload) → cola `solicitudes.notificaciones` → *Publish message* con *Delivery mode 2 – Persistent* y el mismo JSON (mismo `MessageId`).

### Prueba
1. Poner `RabbitMq__ConsumerEnabled=false` y reiniciar. Log: `Consumidor RabbitMQ DESHABILITADO`.
2. Con `cliente2@creditos.pe` registrar una solicitud (ej. S/ 10,000).
3. CloudAMQP → RabbitMQ Manager → *Queues*: `solicitudes.notificaciones` muestra **Ready = 1** (captura).
4. Poner `RabbitMq__ConsumerEnabled=true` y reiniciar → la cola queda en **0** y en "Mis notificaciones" aparece **una** notificación (captura).
5. Panel Analista → reenviar la misma solicitud → log `MessageId … ya procesado: se confirma sin duplicar`; sigue habiendo **una** notificación.
6. Mensaje inválido: en el Manager publicar `{"hola":1}` en la cola → log `Mensaje INVÁLIDO rechazado sin reencolar`, el mensaje queda en la DLQ.

## 8. Despliegue en Render (P8)

1. Subir el repo a GitHub (`main` con todos los PRs integrados).
2. Render → **New → Web Service** → conectar el repo → **Runtime: Docker** (usa el `Dockerfile`). También se puede usar *New → Blueprint* con `render.yaml`.
3. **Instancias: 1** (SQLite y el consumidor `BackgroundService` viven en esa instancia).
4. Variables de entorno (sección 3): `ASPNETCORE_ENVIRONMENT`, `ConnectionStrings__DefaultConnection=Data Source=/var/data/creditos.db`, `Redis__ConnectionString`, `RabbitMq__ConnectionString`, `RabbitMq__QueueName`, `RabbitMq__ConsumerEnabled=true`.
5. **Comando de inicio:** el `Dockerfile` ejecuta
   `sh -c "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet CreditosApp.dll"`
   → `PORT` se expande en el shell en tiempo de ejecución (no se asume que `${PORT}` se expanda dentro de otra variable).
6. Verificar online: login, registro de solicitud, validaciones, panel analista, badge de cache, enlace de sesión, WebSocket **wss** (DevTools), publicación/consumo en CloudAMQP.

### Persistencia de SQLite
- El filesystem de Render es efímero. Se agrega un **Persistent Disk** montado en `/var/data` (plan *Starter* o superior) y la BD se guarda en `/var/data/creditos.db`, que sobrevive a deploys y reinicios. La app crea la carpeta y aplica migraciones al iniciar.
- En el plan *Free* (sin disco) la BD se recrea en cada deploy/reinicio: la semilla vuelve a crear los usuarios y datos de prueba.
- Un disco persistente impide escalar a más de una instancia, coherente con el requisito de una sola instancia.

## 9. Evidencias

### Git - Ramas publicadas en GitHub

![Git - Ramas publicadas en GitHub](evidencias/9ramas.png)

### analista.

![analista.](evidencias/analista..png)

### analista

![analista](evidencias/analista.png)

### cliente1

![cliente1](evidencias/cliente1.png)

### cliente2

![cliente2](evidencias/cliente2.png)

### P6 - Evento SolicitudEstadoActualizado recibido por el WebSocket (sin recargar)

![P6 - Evento SolicitudEstadoActualizado recibido por el WebSocket (sin recargar)](evidencias/estado%20solicitud.png)

### P4 - Listado servido desde cache Redis (60 s) y WebSocket conectado

![P4 - Listado servido desde cache Redis (60 s) y WebSocket conectado](evidencias/p4-cache-redis.png)

### P6 - Conexion anonima al hub rechazada (401)

![P6 - Conexion anonima al hub rechazada (401)](evidencias/p6-anonimo-401.png)

### P6 - Un segundo cliente NO recibe el evento

![P6 - Un segundo cliente NO recibe el evento](evidencias/p6-segundo-cliente.png)

### P6 - Conexion wss://.../hubs/solicitudes con 101 Switching Protocols

![P6 - Conexion wss://.../hubs/solicitudes con 101 Switching Protocols](evidencias/p6-ws-101.png)

### P7 - Consumidor deshabilitado: mensaje pendiente en solicitudes.notificaciones

![P7 - Consumidor deshabilitado: mensaje pendiente en solicitudes.notificaciones](evidencias/p7-cola-pendiente.png)

### P7 - Consumidor habilitado: la cola se vacia

![P7 - Consumidor habilitado: la cola se vacia](evidencias/p7-cola-vacia.png)

### P7 - Mensaje invalido rechazado sin reencolar (log)

![P7 - Mensaje invalido rechazado sin reencolar (log)](evidencias/p7-mensaje-invalido-log.png)

### P7 - Tras el reenvio sigue habiendo una sola notificacion

![P7 - Tras el reenvio sigue habiendo una sola notificacion](evidencias/p7-reenvio-sin-duplicado.png)

### P7 - Reenvio manual con el mismo MessageId (Panel Analista)

![P7 - Reenvio manual con el mismo MessageId (Panel Analista)](evidencias/p7-reenvio-sin-duplicado-analista.png)

### P7 - Una sola notificacion en Mis notificaciones

![P7 - Una sola notificacion en Mis notificaciones](evidencias/p7-una-notificacion.png)

### P8 - Servicio Live en Render

![P8 - Servicio Live en Render](evidencias/p8-render-online.png)

### README con la URL de Render

![README con la URL de Render](evidencias/readme%20url.png)

### P6 - Conexion WebSocket en DevTools

![P6 - Conexion WebSocket en DevTools](evidencias/WS.png)

