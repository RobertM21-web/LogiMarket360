using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogiMarket.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Auditoria",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FechaUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Categoria = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Evento = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Resultado = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Detalle = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Usuario = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Correlacion = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cliente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Codigo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Identificacion = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Categoria = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Telefono = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CanalPreferido = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cliente", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventoOutbox",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tipo = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Carga = table.Column<string>(type: "TEXT", nullable: false),
                    Referencia = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CreadoUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Estado = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    Intentos = table.Column<int>(type: "INTEGER", nullable: false),
                    UltimoError = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ProcesadoUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ObservadoresCompletados = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventoOutbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Kit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Codigo = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notificacion",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Canal = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Destinatario = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Asunto = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Mensaje = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Referencia = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificacion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Producto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Sku = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Categoria = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Precio = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PesoKg = table.Column<decimal>(type: "TEXT", precision: 10, scale: 3, nullable: false),
                    Stock = table.Column<int>(type: "INTEGER", nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Producto", x => x.Id);
                    table.CheckConstraint("CK_Producto_Peso", "CAST(PesoKg AS REAL) >= 0");
                    table.CheckConstraint("CK_Producto_Precio", "CAST(Precio AS REAL) > 0");
                    table.CheckConstraint("CK_Producto_Stock", "Stock >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Usuario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NombreUsuario = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    NombreCompleto = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Rol = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    HashContrasena = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Sal = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuario", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pedido",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Codigo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ClienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    NombreCliente = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    CategoriaCliente = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    TipoEntrega = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Subtotal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PoliticaDescuento = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TasaDescuento = table.Column<decimal>(type: "TEXT", precision: 5, scale: 4, nullable: false),
                    Descuento = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TarifaEntrega = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    UnidadesFisicas = table.Column<int>(type: "INTEGER", nullable: false),
                    Estado = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    ClaveIdempotencia = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CreadoPor = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    FechaCreacionUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaValidacionUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FechaPagoUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FechaDespachoUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FechaEntregaUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FechaCancelacionUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pedido", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pedido_Cliente_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Cliente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KitComponente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KitId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductoId = table.Column<int>(type: "INTEGER", nullable: true),
                    KitHijoId = table.Column<int>(type: "INTEGER", nullable: true),
                    Cantidad = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitComponente", x => x.Id);
                    table.CheckConstraint("CK_KitComponente_Cantidad", "Cantidad > 0");
                    table.CheckConstraint("CK_KitComponente_Exclusivo", "(ProductoId IS NULL) <> (KitHijoId IS NULL)");
                    table.CheckConstraint("CK_KitComponente_NoAutoReferencia", "KitHijoId IS NULL OR KitHijoId <> KitId");
                    table.ForeignKey(
                        name: "FK_KitComponente_Kit_KitHijoId",
                        column: x => x.KitHijoId,
                        principalTable: "Kit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KitComponente_Kit_KitId",
                        column: x => x.KitId,
                        principalTable: "Kit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KitComponente_Producto_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Producto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimientoInventario",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Sku = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Cantidad = table.Column<int>(type: "INTEGER", nullable: false),
                    Motivo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Referencia = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Usuario = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StockResultante = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientoInventario", x => x.Id);
                    table.CheckConstraint("CK_Movimiento_NoCero", "Cantidad <> 0");
                    table.ForeignKey(
                        name: "FK_MovimientoInventario_Producto_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Producto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Despacho",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PedidoId = table.Column<int>(type: "INTEGER", nullable: false),
                    NumeroGuia = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    TipoEntrega = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Etiqueta = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    PlazoDias = table.Column<int>(type: "INTEGER", nullable: false),
                    FechaDespachoUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaEstimadaUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaEntregaUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UsuarioDespacho = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    UsuarioEntrega = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Despacho", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Despacho_Pedido_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Devolucion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Codigo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PedidoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Motivo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DiasDesdeEntrega = table.Column<int>(type: "INTEGER", nullable: false),
                    MontoReembolso = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ReintegraStock = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReferenciaReembolso = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Observacion = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Usuario = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devolucion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devolucion_Pedido_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistorialEstadoPedido",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PedidoId = table.Column<int>(type: "INTEGER", nullable: false),
                    EstadoAnterior = table.Column<string>(type: "TEXT", maxLength: 12, nullable: true),
                    EstadoNuevo = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    Usuario = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialEstadoPedido", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialEstadoPedido_Pedido_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pago",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PedidoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Monto = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Proveedor = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Referencia = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    ClaveIdempotencia = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Usuario = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pago", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pago_Pedido_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PedidoDetalle",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PedidoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Codigo = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    EsKit = table.Column<bool>(type: "INTEGER", nullable: false),
                    Cantidad = table.Column<int>(type: "INTEGER", nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Importe = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PedidoDetalle", x => x.Id);
                    table.CheckConstraint("CK_PedidoDetalle_Cantidad", "Cantidad > 0");
                    table.ForeignKey(
                        name: "FK_PedidoDetalle_Pedido_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PedidoDetalleComponente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PedidoDetalleId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Sku = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    CantidadPorUnidad = table.Column<int>(type: "INTEGER", nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PedidoDetalleComponente", x => x.Id);
                    table.CheckConstraint("CK_PDC_Cantidad", "CantidadPorUnidad > 0");
                    table.ForeignKey(
                        name: "FK_PedidoDetalleComponente_PedidoDetalle_PedidoDetalleId",
                        column: x => x.PedidoDetalleId,
                        principalTable: "PedidoDetalle",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PedidoDetalleComponente_Producto_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Producto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Categoria",
                table: "Auditoria",
                column: "Categoria");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_FechaUtc",
                table: "Auditoria",
                column: "FechaUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Cliente_Codigo",
                table: "Cliente",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cliente_Identificacion",
                table: "Cliente",
                column: "Identificacion",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cliente_Nombre",
                table: "Cliente",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "IX_Despacho_NumeroGuia",
                table: "Despacho",
                column: "NumeroGuia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Despacho_PedidoId",
                table: "Despacho",
                column: "PedidoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devolucion_Codigo",
                table: "Devolucion",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devolucion_PedidoId",
                table: "Devolucion",
                column: "PedidoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devolucion_ReferenciaReembolso",
                table: "Devolucion",
                column: "ReferenciaReembolso",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventoOutbox_Estado",
                table: "EventoOutbox",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstadoPedido_PedidoId",
                table: "HistorialEstadoPedido",
                column: "PedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_Kit_Codigo",
                table: "Kit",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KitComponente_KitHijoId",
                table: "KitComponente",
                column: "KitHijoId");

            migrationBuilder.CreateIndex(
                name: "IX_KitComponente_KitId",
                table: "KitComponente",
                column: "KitId");

            migrationBuilder.CreateIndex(
                name: "IX_KitComponente_ProductoId",
                table: "KitComponente",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientoInventario_FechaUtc",
                table: "MovimientoInventario",
                column: "FechaUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientoInventario_ProductoId",
                table: "MovimientoInventario",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientoInventario_Referencia",
                table: "MovimientoInventario",
                column: "Referencia");

            migrationBuilder.CreateIndex(
                name: "IX_Pago_ClaveIdempotencia",
                table: "Pago",
                column: "ClaveIdempotencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pago_PedidoId",
                table: "Pago",
                column: "PedidoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pago_Referencia",
                table: "Pago",
                column: "Referencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pedido_ClaveIdempotencia",
                table: "Pedido",
                column: "ClaveIdempotencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pedido_ClienteId",
                table: "Pedido",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedido_Codigo",
                table: "Pedido",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pedido_Estado",
                table: "Pedido",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Pedido_FechaCreacionUtc",
                table: "Pedido",
                column: "FechaCreacionUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PedidoDetalle_PedidoId",
                table: "PedidoDetalle",
                column: "PedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidoDetalleComponente_PedidoDetalleId",
                table: "PedidoDetalleComponente",
                column: "PedidoDetalleId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidoDetalleComponente_ProductoId",
                table: "PedidoDetalleComponente",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Producto_Categoria",
                table: "Producto",
                column: "Categoria");

            migrationBuilder.CreateIndex(
                name: "IX_Producto_Nombre",
                table: "Producto",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "IX_Producto_Sku",
                table: "Producto",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuario_NombreUsuario",
                table: "Usuario",
                column: "NombreUsuario",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Auditoria");

            migrationBuilder.DropTable(
                name: "Despacho");

            migrationBuilder.DropTable(
                name: "Devolucion");

            migrationBuilder.DropTable(
                name: "EventoOutbox");

            migrationBuilder.DropTable(
                name: "HistorialEstadoPedido");

            migrationBuilder.DropTable(
                name: "KitComponente");

            migrationBuilder.DropTable(
                name: "MovimientoInventario");

            migrationBuilder.DropTable(
                name: "Notificacion");

            migrationBuilder.DropTable(
                name: "Pago");

            migrationBuilder.DropTable(
                name: "PedidoDetalleComponente");

            migrationBuilder.DropTable(
                name: "Usuario");

            migrationBuilder.DropTable(
                name: "Kit");

            migrationBuilder.DropTable(
                name: "PedidoDetalle");

            migrationBuilder.DropTable(
                name: "Producto");

            migrationBuilder.DropTable(
                name: "Pedido");

            migrationBuilder.DropTable(
                name: "Cliente");
        }
    }
}
