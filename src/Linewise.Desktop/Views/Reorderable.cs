using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Linewise.Desktop.ViewModels;
using Serilog;

namespace Linewise.Desktop.Views;

/// <summary>
/// Drag a row within an <see cref="ItemsControl"/> to reorder it.
/// </summary>
/// <remarks>
/// Attached rather than written into a code-behind, so the gesture is one thing in one place
/// and the list it reorders is chosen in the .axaml.
/// <para>
/// The behaviour knows nothing about what the list holds. It finds the row under the pointer,
/// asks it to move to the index it was dropped on, and the row — which already knows its own
/// owner — does the work. That keeps the ordering rule in the view model where it can be
/// tested without a window.
/// </para>
/// <para>
/// Dragging is never the only way to reorder. Each row also carries up and down buttons,
/// because a drag is a gesture somebody has to discover and cannot perform from a keyboard,
/// and both routes call the same method so they cannot produce different orders.
/// </para>
/// </remarks>
public static class Reorderable
{
    /// <summary>How far the pointer travels before a press becomes a drag.</summary>
    private const double DragThreshold = 4;

    /// <summary>
    /// What travels with the drag. A marker rather than the row itself: the transfer carries
    /// text or bytes so it can cross a process boundary, and this drag never leaves the
    /// window. The row is held here for the moment the drop needs it.
    /// </summary>
    private static readonly DataFormat<string> RowFormat =
        DataFormat.CreateStringApplicationFormat("linewise-row");

    private static IReorderableRow? _dragging;

    public static readonly AttachedProperty<bool> AllowDragProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("AllowDrag", typeof(Reorderable));

    /// <summary>
    /// Marks the one element a drag may begin on.
    /// </summary>
    /// <remarks>
    /// A drag that could start anywhere on the row starts by accident: on the way to a
    /// checkbox, on the way to the remove button, on any press that wanders four pixels. A
    /// drag holds a pointer grab for as long as it runs, so an accidental one is a good way
    /// to make the whole window feel stuck. It begins on the grip or it does not begin.
    /// </remarks>
    public static readonly AttachedProperty<bool> IsHandleProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsHandle", typeof(Reorderable));

    private static readonly AttachedProperty<Point?> OriginProperty =
        AvaloniaProperty.RegisterAttached<Control, Point?>("Origin", typeof(Reorderable));

    static Reorderable()
    {
        AllowDragProperty.Changed.AddClassHandler<Control>(OnAllowDragChanged);
    }

    public static void SetAllowDrag(Control control, bool value)
    {
        ArgumentNullException.ThrowIfNull(control);

        control.SetValue(AllowDragProperty, value);
    }

    public static bool GetAllowDrag(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        return control.GetValue(AllowDragProperty);
    }

    public static void SetIsHandle(Control control, bool value)
    {
        ArgumentNullException.ThrowIfNull(control);

        control.SetValue(IsHandleProperty, value);
    }

    public static bool GetIsHandle(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        return control.GetValue(IsHandleProperty);
    }

    private static void OnAllowDragChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.NewValue is not true)
        {
            return;
        }

        control.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        control.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        control.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);

        DragDrop.SetAllowDrop(control, true);
        control.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        control.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control
            && e.GetCurrentPoint(control).Properties.IsLeftButtonPressed
            && OnAHandle(e.Source as Visual))
        {
            control.SetValue(OriginProperty, e.GetPosition(control));
        }
    }

    /// <summary>Whether the press landed on the grip rather than anywhere else on the row.</summary>
    private static bool OnAHandle(Visual? source)
    {
        while (source is not null)
        {
            if (source is Control control && GetIsHandle(control))
            {
                return true;
            }

            source = source.GetVisualParent();
        }

        return false;
    }

    private static void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Control control)
        {
            control.SetValue(OriginProperty, null);
        }
    }

    /// <summary>
    /// A press only becomes a drag once the pointer has actually travelled. Without the
    /// threshold every click on a checkbox inside a row would start one.
    /// </summary>
    private static async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Control control
            || control.GetValue(OriginProperty) is not { } origin
            || !e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var travelled = e.GetPosition(control) - origin;

        if (Math.Abs(travelled.X) < DragThreshold && Math.Abs(travelled.Y) < DragThreshold)
        {
            return;
        }

        control.SetValue(OriginProperty, null);

        if (RowUnder(e.Source as Visual) is not { DataContext: IReorderableRow row })
        {
            return;
        }

        _dragging = row;

        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.Create(RowFormat, "row"));

        try
        {
            await DragDrop.DoDragDropAsync(e, transfer, DragDropEffects.Move).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            // A drag that the platform refuses is a gesture that did not happen, not a
            // reason to take the window down. The arrows still reorder the list.
            Log.Warning(exception, "A reorder drag could not be started.");
        }
        finally
        {
            _dragging = null;
        }
    }

    private static void OnDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = _dragging is not null && e.DataTransfer.Contains(RowFormat)
            ? DragDropEffects.Move
            : DragDropEffects.None;

    private static void OnDrop(object? sender, DragEventArgs e)
    {
        if (sender is not ItemsControl list
            || _dragging is not { } dragged
            || !e.DataTransfer.Contains(RowFormat)
            || RowUnder(e.Source as Visual) is not { } target)
        {
            return;
        }

        var index = list.IndexFromContainer(target);

        if (index >= 0)
        {
            dragged.MoveTo(index);
        }
    }

    /// <summary>
    /// The generated container for a row, found by walking up from whatever the pointer was
    /// actually over — which is usually a text block several levels inside it.
    /// </summary>
    private static ContentPresenter? RowUnder(Visual? source)
    {
        while (source is not null)
        {
            if (source is ContentPresenter presenter && presenter.DataContext is IReorderableRow)
            {
                return presenter;
            }

            source = source.GetVisualParent();
        }

        return null;
    }
}
