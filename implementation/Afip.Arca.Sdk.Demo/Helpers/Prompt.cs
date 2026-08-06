using System;
using System.Globalization;

namespace Afip.Arca.Sdk.Demo.Helpers;

/// <summary>
/// Pequeño helper para leer datos desde consola con validación. Bloquea hasta recibir
/// un valor válido o respuesta vacía cuando se permite default.
/// </summary>
internal static class Prompt
{
    public static string AskString(string label, string? defaultValue = null, bool allowEmpty = false)
    {
        while (true)
        {
            Console.Write(defaultValue is null ? $"{label}: " : $"{label} [{defaultValue}]: ");
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input))
            {
                if (defaultValue is not null) return defaultValue;
                if (allowEmpty) return string.Empty;
                Console.WriteLine("  > El valor es obligatorio.");
                continue;
            }
            return input;
        }
    }

    public static int AskInt(string label, int? defaultValue = null, int min = int.MinValue, int max = int.MaxValue)
    {
        while (true)
        {
            Console.Write(defaultValue is null ? $"{label}: " : $"{label} [{defaultValue}]: ");
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input) && defaultValue is not null) return defaultValue.Value;
            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= min && n <= max)
            {
                return n;
            }
            Console.WriteLine($"  > Ingresá un entero entre {min} y {max}.");
        }
    }

    public static long AskLong(string label, long? defaultValue = null, long min = long.MinValue)
    {
        while (true)
        {
            Console.Write(defaultValue is null ? $"{label}: " : $"{label} [{defaultValue}]: ");
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input) && defaultValue is not null) return defaultValue.Value;
            if (long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= min)
            {
                return n;
            }
            Console.WriteLine($"  > Ingresá un número entero (≥ {min}).");
        }
    }

    public static decimal AskDecimal(string label, decimal? defaultValue = null, decimal min = decimal.MinValue)
    {
        while (true)
        {
            Console.Write(defaultValue is null ? $"{label}: " : $"{label} [{defaultValue}]: ");
            var input = Console.ReadLine()?.Trim()?.Replace(',', '.');
            if (string.IsNullOrEmpty(input) && defaultValue is not null) return defaultValue.Value;
            if (decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) && d >= min)
            {
                return d;
            }
            Console.WriteLine($"  > Ingresá un decimal válido (≥ {min}).");
        }
    }

    public static DateOnly AskDate(string label, DateOnly? defaultValue = null)
    {
        var def = defaultValue ?? DateOnly.FromDateTime(DateTime.Today);
        while (true)
        {
            Console.Write($"{label} (yyyy-mm-dd) [{def:yyyy-MM-dd}]: ");
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input)) return def;
            if (DateOnly.TryParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            {
                return d;
            }
            Console.WriteLine("  > Formato esperado: yyyy-mm-dd (ej. 2026-05-13).");
        }
    }

    public static bool AskYesNo(string label, bool defaultYes = true)
    {
        var hint = defaultYes ? "[S/n]" : "[s/N]";
        while (true)
        {
            Console.Write($"{label} {hint}: ");
            var input = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(input)) return defaultYes;
            if (input is "s" or "si" or "sí" or "y" or "yes") return true;
            if (input is "n" or "no") return false;
            Console.WriteLine("  > Respondé s/n.");
        }
    }

    public static TEnum AskEnum<TEnum>(string label, TEnum? defaultValue = null) where TEnum : struct, Enum
    {
        var values = Enum.GetValues<TEnum>();
        Console.WriteLine(label + ":");
        for (var i = 0; i < values.Length; i++)
        {
            var v = values[i];
            var marker = defaultValue is { } d && d.Equals(v) ? " (default)" : string.Empty;
            Console.WriteLine($"  [{i + 1}] {v} = {Convert.ToInt32(v, CultureInfo.InvariantCulture)}{marker}");
        }
        while (true)
        {
            Console.Write($"Elegí opción 1-{values.Length}: ");
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input) && defaultValue is not null) return defaultValue.Value;
            if (int.TryParse(input, out var idx) && idx >= 1 && idx <= values.Length)
            {
                return values[idx - 1];
            }
            Console.WriteLine("  > Opción inválida.");
        }
    }

    public static void Header(string title)
    {
        Console.WriteLine();
        Console.WriteLine(new string('═', 70));
        Console.WriteLine("  " + title);
        Console.WriteLine(new string('═', 70));
    }

    public static void Info(string message)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  ℹ " + message);
        Console.ForegroundColor = prev;
    }

    public static void Success(string message)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  ✔ " + message);
        Console.ForegroundColor = prev;
    }

    public static void Warning(string message)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  ⚠ " + message);
        Console.ForegroundColor = prev;
    }

    public static void Error(string message)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("  ✘ " + message);
        Console.ForegroundColor = prev;
    }

    public static void Pause()
    {
        Console.WriteLine();
        Console.Write("Presioná ENTER para continuar...");
        Console.ReadLine();
    }
}
