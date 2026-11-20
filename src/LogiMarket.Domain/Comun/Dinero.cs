using System.Globalization;

namespace LogiMarket.Domain.Comun;

/// <summary>
/// Valor monetario en USD. Redondeo comercial a 2 decimales con <see cref="MidpointRounding.AwayFromZero"/>.
/// </summary>
public readonly record struct Dinero : IComparable<Dinero>
{
    public static readonly Dinero Cero = new(0m);

    public Dinero(decimal valor)
    {
        Valor = valor;
    }

    public decimal Valor { get; }

    public static Dinero Usd(decimal valor) => new(valor);

    public Dinero Redondeado() => new(Math.Round(Valor, 2, MidpointRounding.AwayFromZero));

    public Dinero Por(decimal factor) => new Dinero(Valor * factor).Redondeado();

    public Dinero Por(int cantidad) => new(Valor * cantidad);

    public static Dinero operator +(Dinero a, Dinero b) => new(a.Valor + b.Valor);

    public static Dinero operator -(Dinero a, Dinero b) => new(a.Valor - b.Valor);

    public static bool operator >(Dinero a, Dinero b) => a.Valor > b.Valor;

    public static bool operator <(Dinero a, Dinero b) => a.Valor < b.Valor;

    public static bool operator >=(Dinero a, Dinero b) => a.Valor >= b.Valor;

    public static bool operator <=(Dinero a, Dinero b) => a.Valor <= b.Valor;

    public int CompareTo(Dinero other) => Valor.CompareTo(other.Valor);

    /// <summary>Formato de presentación: "USD 59.00".</summary>
    public override string ToString() =>
        "USD " + Math.Round(Valor, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);
}
