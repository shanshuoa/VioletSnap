using ScreenshotTool.Core.Annotation.Models;

namespace ScreenshotTool.Core.Annotation.UndoRedo;

public sealed class AddAnnotationCommand(IList<AnnotationItem> items, AnnotationItem item, int index) : IEditCommand
{
    public void Execute()
    {
        if (!items.Contains(item)) items.Insert(Math.Clamp(index, 0, items.Count), item);
    }
    public void Undo() => items.Remove(item);
}

public sealed class DeleteAnnotationCommand(IList<AnnotationItem> items, AnnotationItem item, int index) : IEditCommand
{
    public void Execute() => items.Remove(item);
    public void Undo()
    {
        if (!items.Contains(item)) items.Insert(Math.Clamp(index, 0, items.Count), item);
    }
}

public sealed class ChangeAnnotationCommand(Action applyBefore, Action applyAfter) : IEditCommand
{
    public void Execute() => applyAfter();
    public void Undo() => applyBefore();
}
