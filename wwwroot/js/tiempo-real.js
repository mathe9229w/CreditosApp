// Notificaciones en tiempo real de cambios de estado (P6)
(function () {
    "use strict";
    if (typeof signalR === "undefined") { console.error("No se cargó el cliente SignalR"); return; }

    const estadoEl = document.getElementById("ws-estado");
    const avisosEl = document.getElementById("avisos-tiempo-real");
    const clases = { Aprobado: "bg-success", Rechazado: "bg-danger", Pendiente: "bg-warning text-dark" };

    function mostrarEstado(texto, clase) {
        estadoEl.textContent = "WebSocket: " + texto;
        estadoEl.className = "badge " + clase;
    }

    function aviso(ev, extra) {
        const div = document.createElement("div");
        const tipo = ev.estado === "Aprobado" ? "success" : ev.estado === "Rechazado" ? "danger" : "info";
        div.className = "alert alert-" + tipo + " alert-dismissible fade show shadow";
        div.setAttribute("role", "alert");
        let texto = "La solicitud #" + ev.solicitudId + " fue " + ev.estado.toLowerCase() + ".";
        if (ev.motivoRechazo) texto += " Motivo: " + ev.motivoRechazo;
        if (extra) texto += " " + extra;
        div.textContent = texto;
        const cerrar = document.createElement("button");
        cerrar.type = "button"; cerrar.className = "btn-close"; cerrar.setAttribute("data-bs-dismiss", "alert");
        div.appendChild(cerrar);
        avisosEl.appendChild(div);
        setTimeout(() => div.remove(), 15000);
    }

    // Actualiza la UI. Devuelve true si el estado mostrado cambió.
    function aplicar(ev) {
        let cambio = false;
        document.querySelectorAll('[data-solicitud-id="' + ev.solicitudId + '"]').forEach(function (el) {
            const badge = el.querySelector("[data-estado]");
            if (badge && badge.textContent.trim() !== ev.estado) {
                badge.textContent = ev.estado;
                badge.className = "badge " + (clases[ev.estado] || "bg-secondary");
                cambio = true;
            }
            const motivo = el.querySelector("[data-motivo]");
            if (motivo) motivo.textContent = ev.motivoRechazo || "—";
        });
        return cambio;
    }

    // Al (re)conectar se consulta el estado vigente en el servidor para recuperar cambios perdidos.
    async function sincronizar(despuesDeReconexion) {
        try {
            const r = await fetch("/Solicitudes/Estados", { headers: { "Accept": "application/json" }, credentials: "same-origin" });
            if (!r.ok) return;
            const lista = await r.json();
            lista.forEach(function (s) {
                if (aplicar(s) && despuesDeReconexion) aviso(s, "(recuperado tras reconexión)");
            });
        } catch (e) { console.warn("No se pudo sincronizar estados", e); }
    }

    const conexion = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/solicitudes", {
            transport: signalR.HttpTransportType.WebSockets, // solo WebSocket
            skipNegotiation: true
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 20000, 30000])
        .configureLogging(signalR.LogLevel.Information)
        .build();

    conexion.on("SolicitudEstadoActualizado", function (ev) {
        console.log("SolicitudEstadoActualizado", ev);
        aplicar(ev);
        aviso(ev);
    });

    conexion.onreconnecting(function () { mostrarEstado("reconectando…", "bg-warning text-dark"); });
    conexion.onreconnected(async function () {
        mostrarEstado("conectado", "bg-success");
        await sincronizar(true);
    });
    conexion.onclose(function () {
        mostrarEstado("desconectado (reintentando)", "bg-danger");
        setTimeout(iniciar, 5000); // reintento manual cuando se agota la reconexión automática
    });

    let primeraVez = true;
    async function iniciar() {
        try {
            mostrarEstado("conectando…", "bg-secondary");
            await conexion.start();
            mostrarEstado("conectado", "bg-success");
            await sincronizar(!primeraVez);
            primeraVez = false;
        } catch (err) {
            console.error("Error de conexión al hub", err);
            mostrarEstado("desconectado (reintentando)", "bg-danger");
            setTimeout(iniciar, 5000);
        }
    }

    iniciar();
})();
