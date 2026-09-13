namespace ScreenshotTool.Core.Annotation.UndoRedo;

public sealed class UndoRedoManager
{
    private readonly Stack<IEditCommand> _undo = new();
    private readonly Stack<IEditCommand> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Execute(IEditCommand command)
    {
        command.Execute();
        RecordExecuted(command);
    }

    public void RecordExecuted(IEditCommand command)
    {
        _undo.Push(command);
        _redo.Clear();
    }

    public void Undo()
    {
        if (!_undo.TryPop(out var command)) return;
        command.Undo();
        _redo.Push(command);
    }

    public void Redo()
    {
        if (!_redo.TryPop(out var command)) return;
        command.Execute();
        _undo.Push(command);
    }
}
