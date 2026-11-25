using System.Text;
using LogiMarket.Application.Puertos;

namespace LogiMarket.Infrastructure.Documentos;

/// <summary>Documento ya formateado para la vista previa.</summary>
public sealed record DocumentoVistaPrevia(string Titulo, string Contenido, DateTime GeneradoUtc);

/// <summary>
/// Implementador del Bridge: vista previa en pantalla. Da formato de página al contenido y lo publica
/// mediante un evento que la capa de presentación muestra.
/// </summary>
public sealed class SalidaVistaPrevia : ISalidaDocumento
{
    public event EventHandler<DocumentoVistaPrevia>? DocumentoGenerado;

    public string Nombre => "VISTA PREVIA";

    public DocumentoVistaPrevia? Ultimo { get; private set; }

    public string Emitir(string titulo, IReadOnlyList<string> lineas)
    {
        var ancho = Math.Max(titulo.Length, lineas.DefaultIfEmpty(string.Empty).Max(l => l.Length)) + 4;
        var sb = new StringBuilder();
        sb.AppendLine("╔" + new string('═', ancho) + "╗");
        sb.AppendLine("║  " + titulo.PadRight(ancho - 2) + "║");
        sb.AppendLine("╠" + new string('═', ancho) + "╣");
        foreach (var linea in lineas)
        {
            sb.AppendLine("║  " + linea.PadRight(ancho - 2) + "║");
        }

        sb.AppendLine("╚" + new string('═', ancho) + "╝");
        Ultimo = new DocumentoVistaPrevia(titulo, sb.ToString(), DateTime.UtcNow);
        DocumentoGenerado?.Invoke(this, Ultimo);
        return $"Vista previa generada ({lineas.Count} líneas)";
    }
}

/// <summary>Implementador del Bridge: archivo de texto UTF-8 en la carpeta de comprobantes.</summary>
public sealed class SalidaArchivoTxt : ISalidaDocumento
{
    private readonly string _carpeta;

    public SalidaArchivoTxt(string carpeta)
    {
        _carpeta = carpeta;
    }

    public string Nombre => "ARCHIVO TXT";

    public string? UltimaRuta { get; private set; }

    public string Emitir(string titulo, IReadOnlyList<string> lineas)
    {
        Directory.CreateDirectory(_carpeta);
        var nombre = string.Concat(titulo.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_'));
        var ruta = Path.Combine(_carpeta, $"{nombre}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        File.WriteAllLines(ruta, new[] { titulo, string.Empty }.Concat(lineas), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        UltimaRuta = ruta;
        return ruta;
    }
}
