using System.Diagnostics;
using FluentAssertions;
using LogiMarket.Application.Catalogo;
using LogiMarket.Application.Comprobantes;
using LogiMarket.Application.Consultas;
using LogiMarket.Application.Metricas;
using LogiMarket.Application.Pedidos;
using LogiMarket.Application.Puertos;
using LogiMarket.Application.Registros;
using LogiMarket.Application.Seguridad;
using LogiMarket.Domain.Entrega;
using LogiMarket.Domain.Seguridad;
using LogiMarket.Infrastructure.Documentos;
using LogiMarket.Infrastructure.Notificaciones;
using LogiMarket.Infrastructure.Pagos;
using LogiMarket.Infrastructure.Pagos.Legado;
using LogiMarket.Tests.Soporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace LogiMarket.Tests.Integracion;

/// <summary>Patrones creacionales y estructurales verificados con colaboradores reales.</summary>
public class PatronesIntegracionTests(ITestOutputHelper salida)
{
    [Fact]
    public async Task P11_factory_method_elige_email_o_sms_segun_el_cliente_y_registra_el_aviso()
    {
        await using var e = await EntornoIntegracion.CrearAsync();
        e.IniciarSesion(Rol.Ventas);
        var creadores = e.Servicios.GetServices<CreadorNotificador>().ToDictionary(c => c.Canal);

        creadores["EMAIL"].CrearNotificador().Should().BeOfType<NotificadorEmailSimulado>();
        creadores["SMS"].CrearNotificador().Should().BeOfType<NotificadorSmsSimulado>();

        await e.PedidoPagadoAsync("C001", TipoEntrega.Estandar, ("KIT_TELETRABAJO", 1)); // Ana → EMAIL
        await e.PedidoPagadoAsync("C002", TipoEntrega.Expres, ("P05", 1));               // Luis → SMS

        var avisos = await e.ConsultarBdAsync(db => db.Notificaciones.OrderBy(n => n.Id).ToListAsync());
        avisos.Select(a => (a.Canal, a.Destinatario)).Should().Equal(("EMAIL", "ana.lopez@demo.local"), ("SMS", "0990000002"));
        avisos[1].Mensaje.Length.Should().BeLessThanOrEqualTo(NotificadorSmsSimulado.LongitudMaxima);
    }

    [Fact]
    public async Task P15_singleton_metricas_es_la_misma_instancia_en_distintos_ambitos_y_es_thread_safe()
    {
        await using var e = await EntornoIntegracion.CrearAsync();
        e.IniciarSesion(Rol.Ventas);
        var vistaA = await e.EnAmbitoAsync(sp => Task.FromResult(sp.GetRequiredService<MetricasSesion>()));
        var vistaB = await e.EnAmbitoAsync(sp => Task.FromResult(sp.GetRequiredService<MetricasSesion>()));

        await e.PedidoPagadoAsync("C001", TipoEntrega.Estandar, ("KIT_TELETRABAJO", 1));
        Parallel.For(0, 1000, _ => vistaA.RegistrarCobroDenegado());

        vistaA.Should().BeSameAs(vistaB);
        vistaB.Instantanea.PedidosConfirmados.Should().Be(1);
        vistaB.Instantanea.VentasUsd.Should().Be(59.00m);
        vistaB.Instantanea.CobrosDenegados.Should().Be(1000);
    }

    [Fact]
    public async Task P16_adapter_legado_y_proveedor_v2_cumplen_el_mismo_contrato()
    {
        var bitacora = new AuditoriaEnMemoria();
        var legado = new PasarelaLegadaSimulada();
        IProveedorPago[] proveedores = [new PasarelaLegadaAdapter(legado, bitacora), new PasarelaV2Simulada(bitacora)];

        foreach (var p in proveedores)
        {
            var r1 = await p.CobrarAsync(59.00m, "CLAVE-1");
            var r2 = await p.CobrarAsync(59.00m, "CLAVE-1");
            r1.Aprobado.Should().BeTrue();
            r1.Referencia.Should().StartWith(p.PrefijoReferencia);
            r2.Should().Be(r1, $"{p.Nombre} es idempotente por clave");
            (await p.CobrarAsync(999_999m, "CLAVE-2")).Aprobado.Should().BeFalse("ambos rechazan importes sobre su límite");
            await p.Invoking(x => x.CobrarAsync(0m, "CLAVE-3")).Should().ThrowAsync<ArgumentOutOfRangeException>();
            await p.Invoking(x => x.CobrarAsync(10m, " ")).Should().ThrowAsync<ArgumentException>();
        }

        legado.CobrarCentavos(5900, "DIRECTO").Should().StartWith("00|LEG-", "el componente legado sigue funcionando sin cambios");
        typeof(IPasarelaPago).GetMethods().Select(m => m.Name).Should().NotContain("CobrarCentavos");
    }

    [Fact]
    public async Task P17_bridge_cuatro_combinaciones_con_txt_real_y_vista_previa_visible()
    {
        await using var e = await EntornoIntegracion.CrearAsync();
        e.IniciarSesion(Rol.Admin);
        var id = await e.PedidoPagadoAsync("C001", TipoEntrega.Estandar, ("KIT_TELETRABAJO", 1));
        await e.EnAmbitoAsync(sp => sp.GetRequiredService<ServicioLogistica>().DespacharAsync(id));
        await e.EnAmbitoAsync(sp => sp.GetRequiredService<ServicioLogistica>().EntregarAsync(id));
        await e.EnAmbitoAsync(sp => sp.GetRequiredService<Application.Devoluciones.ServicioDevoluciones>()
            .ProcesarAsync(new Application.Devoluciones.SolicitudDevolucion(id, Domain.Pedidos.MotivoDevolucion.Defecto, false, true)));
        var vista = e.Servicios.GetRequiredService<SalidaVistaPrevia>();

        var resultados = new List<string>();
        foreach (var tipo in new[] { TipoComprobante.Venta, TipoComprobante.Devolucion })
        {
            foreach (var medio in new[] { "VISTA PREVIA", "ARCHIVO TXT" })
            {
                resultados.Add(await e.EnAmbitoAsync(sp => sp.GetRequiredService<ServicioComprobantes>().EmitirAsync(id, tipo, medio)));
                if (medio == "VISTA PREVIA")
                {
                    vista.Ultimo!.Contenido.Should().Contain(tipo == TipoComprobante.Venta ? "TOTAL" : "TOTAL REEMBOLSADO");
                }
            }
        }

        var archivos = Directory.GetFiles(e.Opciones.CarpetaComprobantes, "*.txt");
        archivos.Should().HaveCount(2);
        File.ReadAllText(archivos.Single(a => a.Contains("venta"))).Should().Contain("USD 59.00");
        File.ReadAllText(archivos.Single(a => a.Contains("devoluci"))).Should().Contain("USD 54.00");
        resultados.Should().HaveCount(4);
    }

    [Fact]
    public async Task P19_decorator_registra_inicio_pasarela_y_fin_con_la_misma_correlacion()
    {
        await using var e = await EntornoIntegracion.CrearAsync();
        e.IniciarSesion(Rol.Ventas);
        await e.PedidoPagadoAsync("C001", TipoEntrega.Estandar, ("KIT_TELETRABAJO", 1));
        var rechazado = await e.ValidarPedidoAsync("C002", TipoEntrega.Estandar, ("P05", 1));
        e.Servicios.GetRequiredService<IModoDemostracion>().RechazarCobros = true;
        await e.ConfirmarAsync(rechazado.PedidoId!.Value);

        var trazas = await e.ConsultarBdAsync(db => db.Auditoria.Where(a => a.Categoria == "PAGO").OrderBy(a => a.Id).ToListAsync());
        var grupos = trazas.Where(t => t.Correlacion!.StartsWith("COR-")).GroupBy(t => t.Correlacion).ToList();
        foreach (var t in trazas)
        {
            salida.WriteLine($"{t.Correlacion} {t.Evento,-22} {t.Resultado,-16} {t.Detalle}");
        }

        grupos.Should().HaveCount(2);
        grupos[0].Select(t => t.Evento).Should().Equal("COBRO INICIO", "PASARELA LEGACY", "COBRO FIN");
        grupos[0].Last().Resultado.Should().Be("COBRO APROBADO");
        grupos[1].Select(t => t.Evento).Should().Equal("COBRO INICIO", "COBRO FIN");
        grupos[1].Last().Resultado.Should().Be("COBRO RECHAZADO");
        trazas.Should().ContainSingle(t => t.Evento == "COBRO RECHAZADO" && t.Correlacion!.StartsWith("PED-"), "la Facade deja además la traza de negocio por pedido");
    }

    [Fact]
    public async Task P20_facade_con_falla_en_un_colaborador_no_deja_persistencia_parcial_y_compensa_el_cobro()
    {
        await using var e = await EntornoIntegracion.CrearAsync(s => s.AddScoped<IRepositorioOutbox, OutboxQueFalla>());
        e.IniciarSesion(Rol.Ventas);
        var validado = await e.ValidarPedidoAsync("C001", TipoEntrega.Estandar, ("KIT_TELETRABAJO", 1));

        var r = await e.EnAmbitoAsync(sp => sp.GetRequiredService<ConfirmacionPedidoFacade>().ConfirmarAsync(validado.PedidoId!.Value));

        r.Exito.Should().BeFalse();
        r.Codigo.Should().Be("ERROR_PERSISTENCIA");
        (await e.ConsultarBdAsync(db => db.Pagos.CountAsync())).Should().Be(0);
        (await e.ConsultarBdAsync(db => db.Movimientos.CountAsync(m => m.Referencia.StartsWith("PED-")))).Should().Be(0);
        (await e.StockAsync("P02")).Should().Be(5);
        var estado = await e.ConsultarBdAsync(db => db.Pedidos.Select(p => p.CodigoEstado).SingleAsync());
        estado.Should().Be(Domain.Pedidos.Estados.CodigoEstadoPedido.Validado);
        (await e.ConsultarBdAsync(db => db.Auditoria.CountAsync(a => a.Evento == "COBRO ANULADO"))).Should().Be(1);
        (await e.ConsultarBdAsync(db => db.Auditoria.CountAsync(a => a.Evento == "PASARELA LEGACY REVERSO" && a.Resultado == "OK"))).Should().Be(1);
    }

    [Fact]
    public void P21_flyweight_comparte_metadatos_entre_mil_filas_y_reduce_memoria_en_escenario_equivalente()
    {
        var productos = Enumerable.Range(1, 5).Select(i => new ProductoDto(i, $"P0{i}", $"Producto {i} con nombre descriptivo", "Categoría de prueba", new string('d', 120), 10m * i, 0.1m, 10, true)).ToList();
        const int filas = 10_000;

        var fabrica = new CatalogoVisualFlyweightFactory();
        var antesFly = GC.GetAllocatedBytesForCurrentThread();
        var conFlyweight = fabrica.ConstruirVista(productos, filas);
        var bytesFly = GC.GetAllocatedBytesForCurrentThread() - antesFly;

        var antesSin = GC.GetAllocatedBytesForCurrentThread();
        var sinFlyweight = Enumerable.Range(0, filas).Select(i =>
        {
            var p = productos[i % productos.Count];
            // Escenario equivalente sin compartir: cada fila copia sus metadatos.
            return (Sku: new string(p.Sku), Nombre: new string(p.Nombre), Categoria: new string(p.Categoria), Descripcion: new string(p.Descripcion), p.Stock, Cantidad: (i % 7) + 1, p.Precio);
        }).ToList();
        var bytesSin = GC.GetAllocatedBytesForCurrentThread() - antesSin;

        salida.WriteLine($"Filas: {filas}; instancias de metadatos con Flyweight: {fabrica.InstanciasCreadas}; sin Flyweight: {filas}");
        salida.WriteLine($"Bytes asignados con Flyweight: {bytesFly:N0}; sin Flyweight: {bytesSin:N0}; reducción: {100.0 * (bytesSin - bytesFly) / bytesSin:0.0}%");
        conFlyweight.Should().HaveCount(filas);
        sinFlyweight.Should().HaveCount(filas);
        fabrica.InstanciasCreadas.Should().Be(5);
        ReferenceEquals(conFlyweight[0].Metadatos, conFlyweight[5].Metadatos).Should().BeTrue();
        conFlyweight.Take(1000).Select(f => f.Metadatos).Distinct().Should().HaveCount(5, "1.000 filas comparten 5 objetos");
        bytesFly.Should().BeLessThan(bytesSin);
    }

    [Fact]
    public async Task P22_rol_consulta_no_alcanza_decorator_ni_adapter_y_queda_auditoria_de_seguridad()
    {
        await using var e = await EntornoIntegracion.CrearAsync();
        e.IniciarSesion(Rol.Ventas);
        var validado = await e.ValidarPedidoAsync("C001", TipoEntrega.Estandar, ("KIT_TELETRABAJO", 1));
        e.IniciarSesion(Rol.Consulta);

        var pasarela = () => e.EnAmbitoAsync(sp => sp.GetRequiredService<IPasarelaPago>().CobrarAsync(59m, "clave-consulta"));
        await pasarela.Should().ThrowAsync<AccesoDenegadoException>();
        var r = await e.ConfirmarAsync(validado.PedidoId!.Value);

        r.Codigo.Should().Be("ACCESO_DENEGADO");
        (await e.ConsultarBdAsync(db => db.Auditoria.CountAsync(a => a.Categoria == "PAGO"))).Should().Be(0, "no hay LOG de inicio/fin de cobro");
        (await e.ConsultarBdAsync(db => db.Auditoria.CountAsync(a => a.Categoria == "SEGURIDAD" && a.Resultado == "DENEGADO"))).Should().Be(2);
        e.Servicios.GetRequiredService<PasarelaLegadaSimulada>().OperacionesProcesadas.Should().Be(0);
        e.Servicios.GetRequiredService<MetricasSesion>().Instantanea.CobrosDenegados.Should().Be(2, "se cuentan la llamada directa (Proxy) y el comando de confirmación");
    }

    [Fact]
    public async Task P38_dip_la_facade_depende_solo_de_abstracciones_de_aplicacion()
    {
        var parametros = typeof(ConfirmacionPedidoFacade).GetConstructors().Single().GetParameters().Select(p => p.ParameterType).ToList();

        parametros.Should().OnlyContain(t => t.Assembly == typeof(IPasarelaPago).Assembly || t.Assembly == typeof(Domain.Comun.IReloj).Assembly);
        parametros.Where(t => !t.IsInterface).Should().OnlyContain(t => t == typeof(Application.Pedidos.Validacion.CadenaValidacionPedido));

        await using var e = await EntornoIntegracion.CrearAsync();
        var stopwatch = Stopwatch.StartNew();
        e.IniciarSesion(Rol.Ventas);
        await e.PedidoPagadoAsync("C001", TipoEntrega.Estandar, ("KIT_TELETRABAJO", 1));
        salida.WriteLine($"Validación + confirmación + Outbox: {stopwatch.ElapsedMilliseconds} ms");
    }

    private sealed class OutboxQueFalla : IRepositorioOutbox
    {
        public void Agregar(EventoOutbox evento) => throw new InvalidOperationException("Outbox no disponible (falla inducida en prueba).");

        public Task<IReadOnlyList<EventoOutbox>> ListarPendientesAsync(int maximo, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EventoOutbox>>(Array.Empty<EventoOutbox>());
    }
}
