namespace RemoteDesktopClient.UI.Avalonia;

public sealed partial class MainWindow
{
    private static string FormatDeviceId(string digits)
    {
        int firstGroupLength = digits.Length % 3 == 0 ? 3 : digits.Length % 3;
        var groups = new List<string> { digits[..firstGroupLength] };
        for (int i = firstGroupLength; i < digits.Length; i += 3)
            groups.Add(digits.Substring(i, 3));
        return string.Join(' ', groups);
    }

    private const int RemoteDeviceIdDigitCount = 10;

    private void FormatRemoteIdAsDigitsAreTyped()
    {
        var typedDigits = new string(_remoteIdInput.Text.Where(char.IsDigit).ToArray());
        if (typedDigits.Length > RemoteDeviceIdDigitCount)
            typedDigits = typedDigits[..RemoteDeviceIdDigitCount];

        var formattedText = typedDigits.Length == 0 ? string.Empty : FormatDeviceId(typedDigits);
        if (formattedText == _remoteIdInput.Text)
            return;

        _remoteIdInput.Text = formattedText;
        _remoteIdInput.InnerTextBox.SelectionStart = formattedText.Length;
        _remoteIdInput.InnerTextBox.SelectionEnd = formattedText.Length;
        _remoteIdInput.InnerTextBox.CaretIndex = formattedText.Length;
    }
}
