using CreditosApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreditosApp.Data.Migrations
{
    /// <summary>P7: tabla Notificaciones (MessageId único) y seguimiento de publicación en SolicitudesCredito.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260924160000_Notificaciones")]
    public partial class Notificaciones : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NotificacionEncolada",
                table: "SolicitudesCredito",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "NotificacionMessageId",
                table: "SolicitudesCredito",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Notificaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MessageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SolicitudId = table.Column<int>(type: "INTEGER", nullable: false),
                    UsuarioId = table.Column<string>(type: "TEXT", nullable: false),
                    Texto = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    FechaProcesamientoUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificaciones", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_MessageId",
                table: "Notificaciones",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_UsuarioId",
                table: "Notificaciones",
                column: "UsuarioId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Notificaciones");
            migrationBuilder.DropColumn(name: "NotificacionEncolada", table: "SolicitudesCredito");
            migrationBuilder.DropColumn(name: "NotificacionMessageId", table: "SolicitudesCredito");
        }
    }
}
