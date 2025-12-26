using System.Globalization;
using AIManager.Mobile.Models;

namespace AIManager.Mobile.Converters;

/// <summary>
/// Converts BankType to display color
/// </summary>
public class BankTypeToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is BankType bankType)
        {
            var info = BankInfo.GetDisplayInfo(bankType);
            return Color.FromArgb(info.PrimaryColor);
        }
        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts BankType to Thai name
/// </summary>
public class BankTypeToNameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is BankType bankType)
        {
            var info = BankInfo.GetDisplayInfo(bankType);
            return info.ShortName;
        }
        return "ไม่ทราบ";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts BankType to icon text (emoji or character)
/// </summary>
public class BankTypeToIconConverter : IMultiValueConverter
{
    public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length > 0 && values[0] is BankType bankType)
        {
            return bankType switch
            {
                BankType.PromptPay => "📱",
                BankType.TrueMoney => "💰",
                BankType.LinePay => "💚",
                BankType.ShopeePay => "🛒",
                _ => "🏦"
            };
        }
        return "🏦";
    }

    public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts BankType to placeholder text for account number
/// </summary>
public class BankTypeToPlaceholderConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is BankType bankType)
        {
            return bankType switch
            {
                BankType.PromptPay => "เบอร์โทร/เลขบัตรประชาชน",
                BankType.TrueMoney => "เบอร์โทรศัพท์",
                BankType.LinePay => "LINE ID หรือเบอร์โทร",
                BankType.ShopeePay => "เบอร์โทรศัพท์",
                _ => "เลขบัญชีธนาคาร"
            };
        }
        return "เลขบัญชี";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts bool to "เปิด"/"ปิด" text
/// </summary>
public class BoolToStatusTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isEnabled)
        {
            return isEnabled ? "เปิด" : "ปิด";
        }
        return "ปิด";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts bool to status color
/// </summary>
public class BoolToStatusColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isEnabled)
        {
            return isEnabled ? Colors.Green : Colors.Gray;
        }
        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts bool to "เพิ่มบัญชี"/"แก้ไขบัญชี" text
/// </summary>
public class BoolToAddEditBankConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isAddingNew)
        {
            return isAddingNew ? "เพิ่มบัญชีใหม่" : "แก้ไขบัญชี";
        }
        return "เพิ่มบัญชีใหม่";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts zero count to true (for empty state visibility)
/// </summary>
public class ZeroToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count == 0;
        }
        return true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts non-zero count to true
/// </summary>
public class NonZeroToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count > 0;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts enum to bool (true if not Unknown/default)
/// </summary>
public class EnumToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is BankType bankType)
        {
            return bankType != BankType.Unknown;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
