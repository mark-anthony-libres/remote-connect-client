using System.Text.Json.Serialization;

namespace RemoteDesktopClient.Core.Control;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(MouseMoveMessage), "mouse-move")]
[JsonDerivedType(typeof(MouseButtonMessage), "mouse-button")]
[JsonDerivedType(typeof(MouseWheelMessage), "mouse-wheel")]
[JsonDerivedType(typeof(KeyDownMessage), "keyboard-down")]
[JsonDerivedType(typeof(KeyUpMessage), "keyboard-up")]
[JsonDerivedType(typeof(ClipboardTextMessage), "clipboard-text")]
[JsonDerivedType(typeof(ClipboardImageChunkMessage), "clipboard-image-chunk")]
[JsonDerivedType(typeof(FileTransferStartMessage), "file-transfer-start")]
[JsonDerivedType(typeof(FileTransferChunkMessage), "file-transfer-chunk")]
[JsonDerivedType(typeof(FileTransferCompleteMessage), "file-transfer-complete")]
[JsonDerivedType(typeof(FileTransferCancelMessage), "file-transfer-cancel")]
[JsonDerivedType(typeof(FileTransferErrorMessage), "file-transfer-error")]
[JsonDerivedType(typeof(DebugLogStartMessage), "debug-log-start")]
[JsonDerivedType(typeof(DebugLogChunkMessage), "debug-log-chunk")]
[JsonDerivedType(typeof(DebugLogCompleteMessage), "debug-log-complete")]
[JsonDerivedType(typeof(AdjustVideoSettingsMessage), "adjust-video-settings")]
public abstract record ControlMessage;

public sealed record MouseMoveMessage(double NormalizedX, double NormalizedY) : ControlMessage;

public enum MouseButtonKind { Left, Middle, Right }

public enum MouseButtonAction { Down, Up }

public sealed record MouseButtonMessage(
    MouseButtonKind Button,
    MouseButtonAction Action,
    double NormalizedX,
    double NormalizedY) : ControlMessage;

public sealed record MouseWheelMessage(double NormalizedX, double NormalizedY, double DeltaY) : ControlMessage;

public enum RemoteKey
{
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,

    Enter, Escape, Backspace, Tab, Space,
    ShiftLeft, ShiftRight, ControlLeft, ControlRight, AltLeft, AltRight,
    WindowsLeft, WindowsRight,
    ArrowUp, ArrowDown, ArrowLeft, ArrowRight,
    Home, End, PageUp, PageDown, Insert, Delete,
}

public sealed record KeyDownMessage(RemoteKey Key) : ControlMessage;

public sealed record KeyUpMessage(RemoteKey Key) : ControlMessage;

public sealed record ClipboardTextMessage(string Text) : ControlMessage;

public sealed record ClipboardImageMessage(byte[] PngBytes) : ControlMessage;

public sealed record ClipboardImageChunkMessage(
    string TransferId,
    int ChunkIndex,
    int TotalChunks,
    byte[] Data) : ControlMessage;

public sealed record ClipboardFilesMessage(IReadOnlyList<string> Paths) : ControlMessage;

public sealed record FileTransferStartMessage(
    string TransferId,
    string BatchId,
    int FileIndex,
    int FileCount,
    string FileName,
    long FileSize) : ControlMessage;

public sealed record FileTransferChunkMessage(
    string TransferId,
    int ChunkIndex,
    byte[] Data) : ControlMessage;

public sealed record FileTransferCompleteMessage(
    string TransferId,
    int TotalChunks) : ControlMessage;

public sealed record FileTransferCancelMessage(
    string TransferId,
    string Reason) : ControlMessage;

public sealed record FileTransferErrorMessage(
    string TransferId,
    string Reason,
    string Direction,
    int LimitMb) : ControlMessage;

public sealed record DebugLogStartMessage(
    string TransferId,
    long FileSize) : ControlMessage;

public sealed record DebugLogChunkMessage(
    string TransferId,
    int ChunkIndex,
    byte[] Data) : ControlMessage;

public sealed record DebugLogCompleteMessage(
    string TransferId,
    int TotalChunks) : ControlMessage;

public sealed record AdjustVideoSettingsMessage(
    int Fps,
    int BitrateKbps) : ControlMessage;
