using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Microsoft.FSharp.Collections;
using Window = Kolonnade.Window<System.Windows.Media.Imaging.BitmapSource>;

namespace KolonnadeApp
{
    public interface ISelectable
    {
        void Reset();
        void OnSelected(object item);
        void UpdateSelectionItems(string searchText);
    }

    public class WindowSelectable : ISelectable
    {
        private readonly History _history = new History(16);
        private readonly Func<IEnumerable<Window>> _windowProvider;
        private readonly List<object> _selectionList;
        private List<Window> _windowList;

        public WindowSelectable(Func<IEnumerable<Window>> windowProvider, List<object> selectionList)
        {
            _windowProvider = windowProvider;
            _selectionList = selectionList;
            Reset();
        }

        public void Reset()
        {
            _windowList = _windowProvider().ToList();
        }

        public void OnSelected(object item)
        {
            var window = (item as Item).Window;
            _history.Append(window);
            window.ToForeground();
        }

        public void UpdateSelectionItems(string searchText)
        {
            _selectionList.Clear();
            _selectionList.AddRange(_windowList
                .Where(SearchFilter(searchText))
                .OrderBy(x => x, _history.Comparer)
                .Select((w, i) =>
                {
                    var shortCut = (i + 1).ToString();
                    return new Item(shortCut, w);
                })
            );
        }

        private Func<Window, bool> SearchFilter(string searchText)
        {
            return w => w.Title.ToLower().Contains(searchText)
                        || (w.Process != null && w.Process.ToLower().Contains(searchText));
        }

        class Item
        {
            public string ShortCut { get; }
            public Window Window { get; }

            public Item(string shortCut, Window window)
            {
                ShortCut = shortCut;
                Window = window;
            }
        }

        class History
        {
            public int MaxSize { get; }
            private readonly LinkedList<Kolonnade.Id> _history = new LinkedList<Kolonnade.Id>();

            public History(int maxSize)
            {
                MaxSize = maxSize;
            }

            public IComparer<Window> Comparer
            {
                get => new HistoryComparer(() => _history);
            }

            public void Append(Window value)
            {
                _history.Remove(value.Id);
                _history.AddFirst(value.Id);
                if (_history.Count > MaxSize)
                {
                    _history.RemoveLast();
                }
            }
        }

        class HistoryComparer : IComparer<Window>
        {
            private readonly Func<LinkedList<Kolonnade.Id>> _history;

            public HistoryComparer(Func<LinkedList<Kolonnade.Id>> history)
            {
                _history = history;
            }

            public int Compare(Window x, Window y)
            {
                if (x == null && y == null)
                {
                    return 0;
                }

                if (x == null)
                {
                    return -1;
                }

                if (y == null)
                {
                    return 1;
                }

                var yIndex = _history().TakeWhile(e => !e.Equals(y.Id)).Count();
                var xIndex = _history().TakeWhile(e => !e.Equals(x.Id)).Count();

                // It's rather unlikely that one wants to switch to the same window again,
                // hence swap first and second window
                if (xIndex == 0 && yIndex == 1)
                {
                    return 1;
                }

                if (yIndex == 0 && xIndex == 1)
                {
                    return -1;
                }

                return xIndex.CompareTo(yIndex);
            }
        }
    }

    public class LayoutSelectable : ISelectable
    {
        private readonly Kolonnade.WindowManager<System.Windows.Media.Imaging.BitmapSource> _windowManager;
        private readonly List<object> _selectionList;
        private List<LayoutItem> _layoutList;

        public LayoutSelectable(
            Kolonnade.WindowManager<System.Windows.Media.Imaging.BitmapSource> windowManager,
            List<object> selectionList)
        {
            _windowManager = windowManager;
            _selectionList = selectionList;
            Reset();
        }

        public void Reset()
        {
            _layoutList = _windowManager
                .EnumerateLayouts()
                .Select(layout =>
                {
                    var layoutItem = new LayoutItem(layout);
                    var down = ListModule.OfSeq(new List<int> { 2, 3 });
                    var stack = new Kolonnade.Stack<int>(1, FSharpList<int>.Empty, down);
                    var iconArea = new Rectangle(0, 0, 32, 32);
                    foreach (var (windowNumber, area) in layout.DoLayout(stack, iconArea))
                    {
                        if (windowNumber == 1)
                        {
                            layoutItem.MainWindow = area;
                        }
                        else if (windowNumber == 2)
                        {
                            layoutItem.SecondWindow = area;
                        }
                        else if (windowNumber == 3)
                        {
                            layoutItem.ThirdWindow = area;
                        }
                    }
                    return layoutItem;
                })
                .ToList();
        }

        public void OnSelected(object item)
        {
            var layout = ((LayoutItem) item).Layout;
            _windowManager.ModifyStackSet(stackSet => stackSet.WithCurrentLayout(layout));
        }

        public void UpdateSelectionItems(string searchText)
        {
            _selectionList.Clear();
            _selectionList.AddRange(_layoutList
                .Where(x => x.Layout.Description.ToLower().Contains(searchText)));
        }

        internal class LayoutItem
        {
            public Kolonnade.Layout Layout { get; }
            public Rectangle MainWindow { get; set; }
            public Rectangle SecondWindow { get; set; }
            public Rectangle ThirdWindow { get; set; }

            internal LayoutItem(Kolonnade.Layout layout)
            {
                Layout = layout;
            }
        }
    }
}