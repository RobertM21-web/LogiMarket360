using System.Reflection;
using System.Xml.Linq;
using FluentAssertions;

namespace LogiMarket.Tests.Arquitectura;

/// <summary>
/// P38 (parte): verifica la regla de dependencia de Clean Architecture leyendo los .csproj
/// y las referencias reales de los ensamblados compilados.
/// </summary>
public class ReglasDeDependenciaTests
{
    private static readonly string RaizSolucion = LocalizarRaiz();

    private static string LocalizarRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LogiMarket360.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("No se encontró LogiMarket360.sln");
    }

    private static XDocument Csproj(string proyecto) =>
        XDocument.Load(Path.Combine(RaizSolucion, "src", proyecto, proyecto + ".csproj"));

    private static IReadOnlyList<string> ReferenciasDeProyecto(string proyecto) =>
        Csproj(proyecto).Descendants("ProjectReference")
            .Select(e => Path.GetFileNameWithoutExtension(e.Attribute("Include")!.Value))
            .ToList();

    private static IReadOnlyList<string> Paquetes(string proyecto) =>
        Csproj(proyecto).Descendants("PackageReference")
            .Select(e => e.Attribute("Include")!.Value)
            .ToList();

    [Fact]
    public void Domain_no_referencia_ningun_proyecto_ni_paquete()
    {
        ReferenciasDeProyecto("LogiMarket.Domain").Should().BeEmpty();
        Paquetes("LogiMarket.Domain").Should().BeEmpty();
    }

    [Fact]
    public void Application_solo_referencia_Domain_y_no_usa_paquetes_de_infraestructura()
    {
        ReferenciasDeProyecto("LogiMarket.Application").Should().BeEquivalentTo("LogiMarket.Domain");
        Paquetes("LogiMarket.Application").Should()
            .NotContain(p => p.Contains("EntityFramework") || p.Contains("Sqlite") || p.Contains("Windows"));
    }

    [Fact]
    public void Infrastructure_solo_referencia_Application_y_Domain()
    {
        ReferenciasDeProyecto("LogiMarket.Infrastructure").Should()
            .BeEquivalentTo("LogiMarket.Application", "LogiMarket.Domain");
    }

    [Fact]
    public void Ningun_proyecto_interno_referencia_WinForms()
    {
        foreach (var p in new[] { "LogiMarket.Domain", "LogiMarket.Application", "LogiMarket.Infrastructure" })
        {
            ReferenciasDeProyecto(p).Should().NotContain("LogiMarket.WinForms", because: $"{p} es una capa interna");
        }
    }

    [Theory]
    [InlineData(typeof(Domain.AssemblyMarker), new[] { "LogiMarket.Application", "LogiMarket.Infrastructure", "LogiMarket.WinForms", "Microsoft.EntityFrameworkCore", "Microsoft.Data.Sqlite", "System.Windows.Forms" })]
    [InlineData(typeof(Application.AssemblyMarker), new[] { "LogiMarket.Infrastructure", "LogiMarket.WinForms", "Microsoft.EntityFrameworkCore", "Microsoft.Data.Sqlite", "System.Windows.Forms" })]
    [InlineData(typeof(Infrastructure.AssemblyMarker), new[] { "LogiMarket.WinForms", "System.Windows.Forms" })]
    public void Ensamblados_compilados_no_cargan_dependencias_prohibidas(Type marcador, string[] prohibidas)
    {
        var referencias = marcador.Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        referencias.Should().NotContain(r => prohibidas.Any(p => r.StartsWith(p, StringComparison.Ordinal)));
    }
}
