using FluentAssertions;
using LogiMarket.WinForms.Comun;
using LogiMarket.WinForms.Componentes;

namespace LogiMarket.Tests.Calidad;

/// <summary>Regresiones de los defectos de interfaz detectados con las capturas reales (R01, R03).</summary>
public class RegresionInterfazTests
{
    private sealed record Fila(string Codigo, decimal Total);

    private static void EnSta(Action accion)
    {
        Exception? error = null;
        var hilo = new Thread(() =>
        {
            try
            {
                accion();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        hilo.SetApartmentState(ApartmentState.STA);
        hilo.Start();
        hilo.Join();
        if (error is not null)
        {
            throw new Xunit.Sdk.XunitException(error.ToString());
        }
    }

    [Fact]
    public void R01_la_grilla_devuelve_la_fila_seleccionada_aunque_no_sea_la_fila_actual()
    {
        EnSta(() =>
        {
            using var formulario = new Form { ShowInTaskbar = false, Opacity = 0, StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000) };
            var grilla = new GrillaDatos().Columna("Código", nameof(Fila.Codigo)).Columna("Total", nameof(Fila.Total), FormatoColumna.Moneda);
            formulario.Controls.Add(grilla);
            formulario.Show();
            grilla.Mostrar(new[] { new Fila("PED-001", 59m), new Fila("PED-002", 102m), new Fila("PED-003", 90m) });

            // Selección por accesibilidad/código (UI Automation): marca la fila sin mover la celda actual.
            grilla.ClearSelection();
            grilla.Rows[2].Selected = true;

            grilla.CurrentRow!.Index.Should().Be(0, "la celda actual sigue en la primera fila");
            grilla.Seleccionado<Fila>()!.Codigo.Should().Be("PED-003");
        });
    }

    [Fact]
    public void R03_un_boton_primario_deshabilitado_cambia_de_aspecto_y_lo_recupera_al_habilitarse()
    {
        EnSta(() =>
        {
            using var boton = Tema.Boton("Confirmar cobro", (_, _) => { }, primario: true);
            var habilitado = (boton.BackColor, boton.ForeColor);

            boton.Enabled = false;
            boton.BackColor.Should().NotBe(Tema.Primario, "un botón deshabilitado no debe parecer accionable");
            boton.ForeColor.Should().Be(Tema.TextoSuave);

            boton.Enabled = true;
            (boton.BackColor, boton.ForeColor).Should().Be(habilitado);
        });
    }
}
