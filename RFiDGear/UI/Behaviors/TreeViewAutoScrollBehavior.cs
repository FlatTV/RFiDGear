using System;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Serilog;

namespace RFiDGear.UI.Behaviors
{
    /// <summary>
    /// Keeps the most recently added item of a TreeView visible by scrolling the
    /// TreeView's internal ScrollViewer to the bottom whenever a new item is
    /// appended to its (observable) ItemsSource.
    /// </summary>
    public static class TreeViewAutoScrollBehavior
    {
        public static readonly DependencyProperty AutoScrollToLastItemProperty = DependencyProperty.RegisterAttached(
            "AutoScrollToLastItem",
            typeof(bool),
            typeof(TreeViewAutoScrollBehavior),
            new PropertyMetadata(false, OnAutoScrollToLastItemChanged));

        private static readonly ILogger Logger = Log.ForContext(typeof(TreeViewAutoScrollBehavior));

        // Keeps the per-TreeView event handler alive so it can be detached again,
        // without leaking TreeView instances (ConditionalWeakTable holds weak refs to the key).
        private static readonly ConditionalWeakTable<TreeView, NotifyCollectionChangedEventHandler> Handlers = new();

        public static bool GetAutoScrollToLastItem(DependencyObject target)
        {
            return (bool)target.GetValue(AutoScrollToLastItemProperty);
        }

        public static void SetAutoScrollToLastItem(DependencyObject target, bool value)
        {
            target.SetValue(AutoScrollToLastItemProperty, value);
        }

        private static void OnAutoScrollToLastItemChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is not TreeView treeView)
            {
                return;
            }

            treeView.Loaded -= TreeViewOnLoaded;
            treeView.Unloaded -= TreeViewOnUnloaded;
            DetachCollectionChangedHandler(treeView);

            if (e.NewValue is bool enabled && enabled)
            {
                treeView.Loaded += TreeViewOnLoaded;
                treeView.Unloaded += TreeViewOnUnloaded;

                if (treeView.IsLoaded)
                {
                    AttachCollectionChangedHandler(treeView);
                }
            }
        }

        private static void TreeViewOnLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is TreeView treeView)
            {
                AttachCollectionChangedHandler(treeView);
            }
        }

        private static void TreeViewOnUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is TreeView treeView)
            {
                DetachCollectionChangedHandler(treeView);
            }
        }

        private static void AttachCollectionChangedHandler(TreeView treeView)
        {
            // treeView.Items is a live view over the bound ObservableCollection and
            // raises CollectionChanged itself whenever the source collection changes.
            if (treeView.Items is not INotifyCollectionChanged incc)
            {
                return;
            }

            DetachCollectionChangedHandler(treeView);

            NotifyCollectionChangedEventHandler handler = (s, args) =>
            {
                if (args.Action == NotifyCollectionChangedAction.Add ||
                    args.Action == NotifyCollectionChangedAction.Reset)
                {
                    ScrollToBottom(treeView);
                }
            };

            incc.CollectionChanged += handler;
            Handlers.AddOrUpdate(treeView, handler);
        }

        private static void DetachCollectionChangedHandler(TreeView treeView)
        {
            if (treeView.Items is INotifyCollectionChanged incc &&
                Handlers.TryGetValue(treeView, out var handler))
            {
                incc.CollectionChanged -= handler;
                Handlers.Remove(treeView);
            }
        }

        private static void ScrollToBottom(TreeView treeView)
        {
            // Defer until after the new TreeViewItem has been generated and measured.
            treeView.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                try
                {
                    var scrollViewer = FindVisualChild<ScrollViewer>(treeView);
                    scrollViewer?.ScrollToBottom();
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Unable to auto-scroll TreeView to the last item");
                }
            }));
        }

        private static T FindVisualChild<T>(DependencyObject root) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }

                var descendant = FindVisualChild<T>(child);
                if (descendant != null)
                {
                    return descendant;
                }
            }

            return null;
        }
    }
}
