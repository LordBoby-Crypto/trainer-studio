using System.Buffers.Binary;
using System.Globalization;

namespace TrainerStudio.Core.Scanning;

public static class ValueCodec
{
    public static int SizeOf(ValueType type) => type switch
    {
        ValueType.Int32 => sizeof(int),
        ValueType.Float32 => sizeof(float),
        ValueType.Float64 => sizeof(double),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static bool TryEncode(string text, ValueType type, out byte[] bytes, out string error)
    {
        bytes = Array.Empty<byte>();
        error = string.Empty;

        switch (type)
        {
            case ValueType.Int32 when int.TryParse(text, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var integer):
                bytes = new byte[sizeof(int)];
                BinaryPrimitives.WriteInt32LittleEndian(bytes, integer);
                return true;

            case ValueType.Float32 when float.TryParse(text, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var single) && float.IsFinite(single):
                bytes = BitConverter.GetBytes(single);
                return true;

            case ValueType.Float64 when double.TryParse(text, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var number) && double.IsFinite(number):
                bytes = BitConverter.GetBytes(number);
                return true;

            default:
                error = $"Enter a valid {type} value using a period for decimals.";
                return false;
        }
    }

    public static string Decode(ReadOnlySpan<byte> bytes, ValueType type) => type switch
    {
        ValueType.Int32 => BinaryPrimitives.ReadInt32LittleEndian(bytes)
            .ToString(CultureInfo.InvariantCulture),
        ValueType.Float32 => BitConverter.ToSingle(bytes)
            .ToString("G9", CultureInfo.InvariantCulture),
        ValueType.Float64 => BitConverter.ToDouble(bytes)
            .ToString("G17", CultureInfo.InvariantCulture),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right, ValueType type)
        => type switch
        {
            ValueType.Int32 => BinaryPrimitives.ReadInt32LittleEndian(left)
                .CompareTo(BinaryPrimitives.ReadInt32LittleEndian(right)),
            ValueType.Float32 => BitConverter.ToSingle(left).CompareTo(BitConverter.ToSingle(right)),
            ValueType.Float64 => BitConverter.ToDouble(left).CompareTo(BitConverter.ToDouble(right)),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
}
