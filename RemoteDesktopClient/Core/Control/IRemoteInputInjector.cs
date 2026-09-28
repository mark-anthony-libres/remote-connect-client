namespace RemoteDesktopClient.Core.Control;

public interface IRemoteInputInjector
{
    void MoveMouse(double normalizedX, double normalizedY);

    void MouseButton(MouseButtonKind button, MouseButtonAction action, double normalizedX, double normalizedY);

    void MouseWheel(double normalizedX, double normalizedY, double deltaY);

    void KeyDown(RemoteKey key);

    void KeyUp(RemoteKey key);
}

public sealed class NullInputInjector : IRemoteInputInjector
{
    public void MoveMouse(double normalizedX, double normalizedY) { }
    public void MouseButton(MouseButtonKind button, MouseButtonAction action, double normalizedX, double normalizedY) { }
    public void MouseWheel(double normalizedX, double normalizedY, double deltaY) { }
    public void KeyDown(RemoteKey key) { }
    public void KeyUp(RemoteKey key) { }
}
