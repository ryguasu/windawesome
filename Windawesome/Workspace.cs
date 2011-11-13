﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Windawesome
{
	public sealed class Workspace
	{
		public readonly int id;
		public Monitor Monitor { get; internal set; }
		public ILayout Layout { get; private set; }
		public readonly LinkedList<IBar>[] barsAtTop;
		public readonly LinkedList<IBar>[] barsAtBottom;
		public readonly string name;
		public readonly bool repositionOnSwitchedTo;
		public bool ShowWindowsTaskbar { get; private set; }
		public bool IsWorkspaceVisible { get; private set; }

		private bool isCurrentWorkspace;
		public bool IsCurrentWorkspace
		{
			get { return isCurrentWorkspace; }

			internal set
			{
				isCurrentWorkspace = value;
				if (isCurrentWorkspace)
				{
					DoWorkspaceActivated(this);
				}
				else
				{
					DoWorkspaceDeactivated(this);
				}
			}
		}

		public IEnumerable<IBar> BarsForCurrentMonitor
		{
			get { return barsAtTop[Monitor.monitorIndex].Concat(barsAtBottom[Monitor.monitorIndex]); }
		}

		internal int hideFromAltTabWhenOnInactiveWorkspaceCount;
		internal int sharedWindowsCount;
		internal readonly LinkedList<Window> windowsZOrder; // all windows, sorted in Z-order, topmost window first
		private readonly LinkedList<Window> windows; // all windows, sorted in tab-order, topmost window first

		private bool hasChanges;

		private static int count;

		#region Events

		public delegate void WorkspaceWindowAddedEventHandler(Workspace workspace, Window window);
		public static event WorkspaceWindowAddedEventHandler WorkspaceWindowAdded;

		public delegate void WorkspaceWindowRemovedEventHandler(Workspace workspace, Window window);
		public static event WorkspaceWindowRemovedEventHandler WorkspaceWindowRemoved;

		public delegate void WorkspaceWindowMinimizedEventHandler(Workspace workspace, Window window);
		public static event WorkspaceWindowMinimizedEventHandler WorkspaceWindowMinimized;

		public delegate void WorkspaceWindowRestoredEventHandler(Workspace workspace, Window window);
		public static event WorkspaceWindowRestoredEventHandler WorkspaceWindowRestored;

		public delegate void WorkspaceWindowOrderChangedEventHandler(Workspace workspace, Window window, int positions, bool backwards);
		public static event WorkspaceWindowOrderChangedEventHandler WorkspaceWindowOrderChanged;

		public delegate void WorkspaceHiddenEventHandler(Workspace workspace);
		public static event WorkspaceHiddenEventHandler WorkspaceHidden;

		public delegate void WorkspaceShownEventHandler(Workspace workspace);
		public static event WorkspaceShownEventHandler WorkspaceShown;

		public delegate void WorkspaceActivatedEventHandler(Workspace workspace);
		public static event WorkspaceActivatedEventHandler WorkspaceActivated;

		public delegate void WorkspaceDeactivatedEventHandler(Workspace workspace);
		public static event WorkspaceDeactivatedEventHandler WorkspaceDeactivated;

		public delegate void WorkspaceMonitorChangedEventHandler(Workspace workspace, Monitor oldMonitor, Monitor newMonitor);
		public static event WorkspaceMonitorChangedEventHandler WorkspaceMonitorChanged;

		public delegate void WorkspaceLayoutChangedEventHandler(Workspace workspace, ILayout oldLayout);
		public static event WorkspaceLayoutChangedEventHandler WorkspaceLayoutChanged;

		public delegate void WindowActivatedEventHandler(IntPtr hWnd);
		public static event WindowActivatedEventHandler WindowActivatedEvent;

		public delegate void WindowTitlebarToggledEventHandler(Window window);
		public event WindowTitlebarToggledEventHandler WindowTitlebarToggled;

		public delegate void WindowBorderToggledEventHandler(Window window);
		public event WindowBorderToggledEventHandler WindowBorderToggled;

		public delegate void LayoutUpdatedEventHandler();
		public static event LayoutUpdatedEventHandler LayoutUpdated; // TODO: this should be for a specific workspace. But how to call from Widgets then?

		private static void DoWorkspaceWindowAdded(Workspace workspace, Window window)
		{
			if (WorkspaceWindowAdded != null)
			{
				WorkspaceWindowAdded(workspace, window);
			}
		}

		private static void DoWorkspaceWindowRemoved(Workspace workspace, Window window)
		{
			if (WorkspaceWindowRemoved != null)
			{
				WorkspaceWindowRemoved(workspace, window);
			}
		}

		private static void DoWorkspaceWindowMinimized(Workspace workspace, Window window)
		{
			if (WorkspaceWindowMinimized != null)
			{
				WorkspaceWindowMinimized(workspace, window);
			}
		}

		private static void DoWorkspaceWindowRestored(Workspace workspace, Window window)
		{
			if (WorkspaceWindowRestored != null)
			{
				WorkspaceWindowRestored(workspace, window);
			}
		}

		private static void DoWorkspaceWindowOrderChanged(Workspace workspace, Window window, int positions, bool backwards)
		{
			if (WorkspaceWindowOrderChanged != null)
			{
				WorkspaceWindowOrderChanged(workspace, window, positions, backwards);
			}
		}

		private static void DoWorkspaceHidden(Workspace workspace)
		{
			if (WorkspaceHidden != null)
			{
				WorkspaceHidden(workspace);
			}
		}

		private static void DoWorkspaceShown(Workspace workspace)
		{
			if (WorkspaceShown != null)
			{
				WorkspaceShown(workspace);
			}
		}

		private static void DoWorkspaceActivated(Workspace workspace)
		{
			if (WorkspaceActivated != null)
			{
				WorkspaceActivated(workspace);
			}
		}

		private static void DoWorkspaceDeactivated(Workspace workspace)
		{
			if (WorkspaceDeactivated != null)
			{
				WorkspaceDeactivated(workspace);
			}
		}

		internal static void DoWorkspaceMonitorChanged(Workspace workspace, Monitor oldMonitor, Monitor newMonitor)
		{
			if (WorkspaceMonitorChanged != null)
			{
				WorkspaceMonitorChanged(workspace, oldMonitor, newMonitor);
			}
		}

		private static void DoWorkspaceLayoutChanged(Workspace workspace, ILayout oldLayout)
		{
			if (WorkspaceLayoutChanged != null)
			{
				WorkspaceLayoutChanged(workspace, oldLayout);
			}
		}

		private static void DoWindowActivated(IntPtr hWnd)
		{
			if (WindowActivatedEvent != null)
			{
				WindowActivatedEvent(hWnd);
			}
		}

		private void DoWindowTitlebarToggled(Window window)
		{
			if (WindowTitlebarToggled != null)
			{
				WindowTitlebarToggled(window);
			}
		}

		private void DoWindowBorderToggled(Window window)
		{
			if (WindowBorderToggled != null)
			{
				WindowBorderToggled(window);
			}
		}

		public static void DoLayoutUpdated()
		{
			if (LayoutUpdated != null)
			{
				LayoutUpdated();
			}
		}

		#endregion

		public Workspace(Monitor monitor, ILayout layout, IEnumerable<IBar> barsAtTop = null, IEnumerable<IBar> barsAtBottom = null,
			string name = "", bool showWindowsTaskbar = false, bool repositionOnSwitchedTo = false)
		{
			windowsZOrder = new LinkedList<Window>();
			windows = new LinkedList<Window>();

			this.id = ++count;
			this.Monitor = monitor;
			this.Layout = layout;
			this.barsAtTop = Screen.AllScreens.Select(_ => new LinkedList<IBar>()).ToArray();
			this.barsAtBottom = Screen.AllScreens.Select(_ => new LinkedList<IBar>()).ToArray();
			if (barsAtTop != null)
			{
				barsAtTop.ForEach(bar => this.barsAtTop[bar.Monitor.monitorIndex].AddLast(bar));
			}
			if (barsAtBottom != null)
			{
				barsAtBottom.ForEach(bar => this.barsAtBottom[bar.Monitor.monitorIndex].AddLast(bar));
			}
			this.name = name;
			this.ShowWindowsTaskbar = showWindowsTaskbar;
			this.repositionOnSwitchedTo = repositionOnSwitchedTo;
			layout.Initialize(this);
		}

		public override int GetHashCode()
		{
			return this.id;
		}

		public override bool Equals(object obj)
		{
			var workspace = obj as Workspace;
			return workspace != null && workspace.id == this.id;
		}

		internal void SwitchTo()
		{
			if (sharedWindowsCount > 0)
			{
				// sets the layout- and workspace-specific changes to the windows
				windowsZOrder.Where(w => w.WorkspacesCount > 1).ForEach(w => RestoreSharedWindowState(w, false));
			}

			IsWorkspaceVisible = true;

			if (NeedsToReposition())
			{
				// Repositions if there is/are new/deleted windows
				Reposition();
			}

			DoWorkspaceShown(this);
		}

		internal void Unswitch()
		{
			if (sharedWindowsCount > 0)
			{
				windowsZOrder.Where(w => w.WorkspacesCount > 1 && ShouldSaveAndRestoreSharedWindowsPosition(w)).
					ForEach(w => w.SavePosition());
			}

			IsWorkspaceVisible = false;
			DoWorkspaceHidden(this);
		}

		private void RestoreSharedWindowState(Window window, bool doNotShow)
		{
			window.Initialize();
			if (ShouldSaveAndRestoreSharedWindowsPosition(window))
			{
				window.RestorePosition(doNotShow);
			}
		}

		private bool ShouldSaveAndRestoreSharedWindowsPosition(Window window)
		{
			return !NeedsToReposition() || window.IsFloating || Layout.ShouldSaveAndRestoreSharedWindowsPosition();
		}

		public bool NeedsToReposition()
		{
			return hasChanges || repositionOnSwitchedTo;
		}

		public void Reposition()
		{
			hasChanges = !IsWorkspaceVisible;
			if (IsWorkspaceVisible)
			{
				Layout.Reposition();
			}
		}

		public void ChangeLayout(ILayout layout)
		{
			if (layout.LayoutName() != this.Layout.LayoutName())
			{
				this.Layout.Dispose();
				layout.Initialize(this);
				var oldLayout = this.Layout;
				this.Layout = layout;
				Reposition();
				DoWorkspaceLayoutChanged(this, oldLayout);
			}
		}

		internal void ToggleWindowsTaskbarVisibility()
		{
			if (Monitor.screen.Primary)
			{
				ShowWindowsTaskbar = !ShowWindowsTaskbar;
				Monitor.ShowHideWindowsTaskbar(ShowWindowsTaskbar);
				Reposition();
			}
		}

		//internal bool HideBar(int workspacesCount, IEnumerable<Workspace> workspaces, IBar hideBar)
		//{
		//    shownBars = shownBars.ToArray(); // because we need to hide the bar after that so we need to save the currently shown ones
		//    if (barsAtTop.Remove(hideBar) || barsAtBottom.Remove(hideBar))
		//    {
		//        FindWorkspaceBarsEquivalentClasses(workspacesCount, workspaces);
		//        ShowHideBars(null, null, appBarTopWindows[this.id - 1], appBarBottomWindows[this.id - 1]);

		//        Reposition();

		//        return true;
		//    }

		//    return false;
		//}

		//internal bool ShowBar(int workspacesCount, IEnumerable<Workspace> workspaces, IBar showBar, bool top, int position)
		//{
		//    if (!barsAtTop.Contains(showBar) && !barsAtBottom.Contains(showBar))
		//    {
		//        var bars = top ? barsAtTop : barsAtBottom;
		//        var bar = bars.First;
		//        while (bar != null && position-- > 0)
		//        {
		//            bar = bar.Next;
		//        }
		//        if (bar != null)
		//        {
		//            bars.AddBefore(bar, showBar);
		//        }
		//        else
		//        {
		//            bars.AddFirst(showBar);
		//        }

		//        FindWorkspaceBarsEquivalentClasses(workspacesCount, workspaces);
		//        ShowHideBars(null, null, appBarTopWindows[this.id - 1], appBarBottomWindows[this.id - 1]);

		//        Reposition();

		//        return true;
		//    }

		//    return false;
		//}

		internal void WindowMinimized(IntPtr hWnd)
		{
			var window = MoveWindowToBottom(hWnd);
			if (window != null && !window.IsMinimized)
			{
				window.IsMinimized = true;
				if (!window.IsFloating)
				{
					Layout.WindowMinimized(window);
				}

				DoWorkspaceWindowMinimized(this, window);
			}
		}

		internal void WindowRestored(IntPtr hWnd)
		{
			var window = MoveWindowToTop(hWnd);
			if (window != null && window.IsMinimized)
			{
				window.IsMinimized = false;
				if (!window.IsFloating)
				{
					Layout.WindowRestored(window);
				}

				DoWorkspaceWindowRestored(this, window);
			}
		}

		internal void WindowActivated(IntPtr hWnd)
		{
			MoveWindowToTop(hWnd);

			DoWindowActivated(hWnd);
		}

		internal void WindowCreated(Window window)
		{
			windows.AddFirst(window);
			windowsZOrder.AddFirst(window);
			if (window.hideFromAltTabAndTaskbarWhenOnInactiveWorkspace)
			{
				hideFromAltTabWhenOnInactiveWorkspaceCount++;
			}
			if (window.WorkspacesCount > 1)
			{
				sharedWindowsCount++;
			}
			if (IsWorkspaceVisible || window.WorkspacesCount == 1)
			{
				window.Initialize();
			}

			if (!window.IsMinimized && !window.IsFloating)
			{
				Layout.WindowCreated(window);

				hasChanges |= !IsWorkspaceVisible;
			}

			DoWorkspaceWindowAdded(this, window);
		}

		internal void WindowDestroyed(Window window)
		{
			windows.Remove(window);
			windowsZOrder.Remove(window);
			if (window.hideFromAltTabAndTaskbarWhenOnInactiveWorkspace)
			{
				hideFromAltTabWhenOnInactiveWorkspaceCount--;
			}
			if (window.WorkspacesCount > 1)
			{
				sharedWindowsCount--;
			}

			if (!window.IsMinimized && !window.IsFloating)
			{
				Layout.WindowDestroyed(window);

				hasChanges |= !IsWorkspaceVisible;
			}

			DoWorkspaceWindowRemoved(this, window);
		}

		public int GetWindowsCount()
		{
			return windowsZOrder.Count;
		}

		public bool ContainsWindow(IntPtr hWnd)
		{
			return windowsZOrder.Any(w => w.hWnd == hWnd);
		}

		public Window GetWindow(IntPtr hWnd)
		{
			return windowsZOrder.FirstOrDefault(w => w.hWnd == hWnd);
		}

		public IEnumerable<Window> GetManagedWindows()
		{
			return windows.Where(w => !w.IsFloating && !w.IsMinimized);
		}

		public IEnumerable<Window> GetWindows()
		{
			return windows;
		}

		public Window GetTopmostZOrderWindow()
		{
			var window = windowsZOrder.FirstOrDefault();
			return (window != null && !window.IsMinimized) ? window : null;
		}

		internal void ToggleWindowFloating(IntPtr hWnd)
		{
			var window = GetWindow(hWnd);
			if (window != null)
			{
				window.IsFloating = !window.IsFloating;
				if (!window.IsMinimized)
				{
					if (window.IsFloating)
					{
						Layout.WindowDestroyed(window);
					}
					else
					{
						Layout.WindowCreated(window);
					}
				}
			}
		}

		internal void ToggleShowHideWindowInTaskbar(IntPtr hWnd)
		{
			var window = GetWindow(hWnd);
			if (window != null)
			{
				window.ToggleShowHideInTaskbar();
			}
		}

		internal void ToggleShowHideWindowTitlebar(IntPtr hWnd)
		{
			var window = GetWindow(hWnd);
			if (window != null)
			{
				window.ToggleShowHideTitlebar();
				DoWindowTitlebarToggled(window);
			}
		}

		internal void ToggleShowHideWindowBorder(IntPtr hWnd)
		{
			var window = GetWindow(hWnd);
			if (window != null)
			{
				window.ToggleShowHideWindowBorder();
				DoWindowBorderToggled(window);
			}
		}

		internal void ToggleShowHideWindowMenu(IntPtr hWnd)
		{
			var window = GetWindow(hWnd);
			if (window != null)
			{
				window.ToggleShowHideWindowMenu();
			}
		}

		internal void Initialize()
		{
			// I'm adding to the front of the list in WindowCreated, however EnumWindows enums
			// from the top of the Z-order to the bottom, so I need to reverse the list
			if (windowsZOrder.Count > 0)
			{
				var newWindows = windowsZOrder.ToArray();
				windowsZOrder.Clear();
				newWindows.ForEach(w => windowsZOrder.AddFirst(w));
				newWindows.ForEach(this.ShiftWindowToMainPosition); // n ^ 2!
			}
		}

		private LinkedListNode<Window> GetWindowNode(IntPtr hWnd)
		{
			for (var node = windowsZOrder.First; node != null; node = node.Next)
			{
				if (node.Value.hWnd == hWnd)
				{
					return node;
				}
			}

			return null;
		}

		private Window MoveWindowToTop(IntPtr hWnd)
		{
			var node = GetWindowNode(hWnd);

			if (node != null)
			{
				if (node != windowsZOrder.First)
				{
					// adds the window to the front of the list, i.e. the top of the Z order
					windowsZOrder.Remove(node);
					windowsZOrder.AddFirst(node);
				}

				return node.Value;
			}

			return null;
		}

		private Window MoveWindowToBottom(IntPtr hWnd)
		{
			var node = GetWindowNode(hWnd);

			if (node != null)
			{
				if (node != windowsZOrder.First)
				{
					// adds the window to the back of the list, i.e. the bottom of the Z order
					windowsZOrder.Remove(node);
					windowsZOrder.AddLast(node);
				}

				return node.Value;
			}

			return null;
		}

		internal void RemoveFromSharedWindows(Window window)
		{
			RestoreSharedWindowState(window, !IsWorkspaceVisible);
			sharedWindowsCount--;
		}

		public Window GetNextWindow(Window window)
		{
			var node = windows.Find(window);
			if (node != null)
			{
				return node.Next != null ? node.Next.Value : null;
			}
			return null;
		}

		public Window GetPreviousWindow(Window window)
		{
			var node = windows.Find(window);
			if (node != null)
			{
				return node.Previous != null ? node.Previous.Value : null;
			}
			return null;
		}

		public void ShiftWindowForward(Window window, int positions = 1)
		{
			if (windows.Count > 1 && windows.Last.Value != window)
			{
				var node = windows.Find(window);
				if (node != null)
				{
					var nextNode = node.Next;
					windows.Remove(node);
					var i = 0;
					while (++i < positions && nextNode != null)
					{
						nextNode = nextNode.Next;
					}
					if (nextNode != null)
					{
						windows.AddAfter(nextNode, node);
					}
					else
					{
						windows.AddLast(node);
					}

					this.Reposition();
					DoWorkspaceWindowOrderChanged(this, window, i, false);
				}
			}
		}

		public void ShiftWindowBackwards(Window window, int positions = 1)
		{
			if (windows.Count > 1 && windows.First.Value != window)
			{
				var node = windows.Find(window);
				if (node != null)
				{
					var previousNode = node.Previous;
					windows.Remove(node);
					var i = 0;
					while (++i < positions && previousNode != null)
					{
						previousNode = previousNode.Previous;
					}
					if (previousNode != null)
					{
						windows.AddBefore(previousNode, node);
					}
					else
					{
						windows.AddFirst(node);
					}

					this.Reposition();
					DoWorkspaceWindowOrderChanged(this, window, i, true);
				}
			}
		}

		public void ShiftWindowToMainPosition(Window window)
		{
			if (windows.Count > 1 && windows.First.Value != window)
			{
				var node = windows.First;
				var i = 0;
				for ( ; node != null && node.Value != window; node = node.Next, i++)
				{
				}
				if (node != null)
				{
					windows.Remove(node);
					windows.AddFirst(node);

					this.Reposition();
					DoWorkspaceWindowOrderChanged(this, window, i, true);
				}
			}
		}
	}
}
