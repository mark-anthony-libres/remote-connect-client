namespace RemoteDesktopClient.Core.Control;

public interface IRemoteClipboard
{
    bool TryGetText(out string text);

    void SetText(string text);

    bool TryGetImagePng(out byte[] pngBytes);

    void SetImagePng(byte[] pngBytes);

    bool TryGetFilePaths(out IReadOnlyList<string> paths);

    void SetFilePaths(IReadOnlyList<string> paths);
}

public sealed class NullRemoteClipboard : IRemoteClipboard
{
    public bool TryGetText(out string text)
    {
        text = "";
        return false;
    }

    public void SetText(string text) { }

    public bool TryGetImagePng(out byte[] pngBytes)
    {
        pngBytes = [];
        return false;
    }

    public void SetImagePng(byte[] pngBytes) { }

    public bool TryGetFilePaths(out IReadOnlyList<string> paths)
    {
        paths = [];
        return false;
    }

    public void SetFilePaths(IReadOnlyList<string> paths) { }
}
