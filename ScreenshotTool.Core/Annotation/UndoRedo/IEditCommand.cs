namespace ScreenshotTool.Core.Annotation.UndoRedo;

public interface IEditCommand
{
    void Execute();
    void Undo();
}
