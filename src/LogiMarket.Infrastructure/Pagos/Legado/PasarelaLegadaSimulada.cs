namespace LogiMarket.Infrastructure.Pagos.Legado;

/// <summary>
/// Componente "legado" simulado con una interfaz incompatible (centavos y respuesta de texto).
/// NO se modifica para integrarlo: el <see cref="PasarelaLegadaAdapter"/> lo adapta a IPasarelaPago.
/// </summary>
public class PasarelaLegadaSimulada
{
    private readonly object _candado = new();
    private readonly Dictionary<string, string> _respuestasPorReferencia = new();
    private readonly HashSet<string> _anuladas = new();
    private int _secuencia;

    /// <summary>Límite de autorización por operación (en centavos), como en el sistema original.</summary>
    public long LimiteCentavos { get; set; } = 500_000;

    public int OperacionesProcesadas { get; private set; }

    /// <summary>
    /// Cobra un importe en centavos. Respuesta: "00|LEG-000001" (aprobado) o "05|motivo" (rechazado).
    /// La misma referencia de cliente devuelve la misma respuesta (deduplicación legada).
    /// </summary>
    public string CobrarCentavos(long centavos, string referenciaCliente)
    {
        lock (_candado)
        {
            if (_respuestasPorReferencia.TryGetValue(referenciaCliente, out var previa))
            {
                return previa;
            }

            OperacionesProcesadas++;
            string respuesta;
            if (centavos <= 0)
            {
                respuesta = "12|IMPORTE INVALIDO";
            }
            else if (centavos > LimiteCentavos)
            {
                respuesta = "05|EXCEDE LIMITE DE AUTORIZACION";
            }
            else
            {
                respuesta = $"00|LEG-{++_secuencia:000000}";
            }

            _respuestasPorReferencia[referenciaCliente] = respuesta;
            return respuesta;
        }
    }

    /// <summary>Reverso de una operación aprobada. Devuelve "00" o "25|NO ENCONTRADA".</summary>
    public string Reversar(string codigoAutorizacion)
    {
        lock (_candado)
        {
            var entrada = _respuestasPorReferencia.FirstOrDefault(e => e.Value == $"00|{codigoAutorizacion}");
            if (entrada.Key is null || !_anuladas.Add(codigoAutorizacion))
            {
                return "25|NO ENCONTRADA";
            }

            // Una autorización reversada no se reutiliza: un nuevo intento con la misma referencia se procesa de nuevo.
            _respuestasPorReferencia.Remove(entrada.Key);
            return "00";
        }
    }
}
