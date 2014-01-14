using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Windawesome
{
	/// <summary>
	/// Represents a (logical) monitor. See also MonitorFactory.
	/// </summary>
	public abstract class Monitor
	{
		public readonly int monitorIndex;
		public Workspace CurrentVisibleWorkspace { get; internal set; }
		public IEnumerable<Workspace> Workspaces { get { return workspaces.Keys; } }
		public Rectangle Bounds { get; protected set; }
		public Rectangle WorkingArea { get; protected set; }
		public abstract bool Primary { get; }

		internal readonly HashSet<IntPtr> temporarilyShownWindows;
		private readonly Dictionary<Workspace, Tuple<int, AppBarNativeWindow, AppBarNativeWindow>> workspaces;

		private static bool isWindowsTaskbarShown;

		private static readonly NativeMethods.WinEventDelegate taskbarShownWinEventDelegate = TaskbarShownWinEventDelegate;
		private static readonly IntPtr taskbarShownWinEventHook;

		// TODO: when running under XP and a normal user account, but Windawesome is elevated, for example with SuRun,
		// the AppBars don't resize the desktop working area
		private sealed class AppBarNativeWindow : NativeWindow
		{
			public readonly int height;

			private Monitor monitor;
			private NativeMethods.RECT rect;
			private bool visible;
			private IEnumerable<IBar> bars;
			private readonly uint callbackMessageNum;
			private readonly NativeMethods.ABE edge;
			private bool isTopMost;

			private static uint count;

			public AppBarNativeWindow(int barHeight, bool topBar)
			{
				this.height = barHeight;
				visible = false;
				isTopMost = false;
				edge = topBar ? NativeMethods.ABE.ABE_TOP : NativeMethods.ABE.ABE_BOTTOM;

				this.CreateHandle(new CreateParams { Parent = NativeMethods.HWND_MESSAGE, ClassName = "Message" });

				callbackMessageNum = NativeMethods.WM_USER + count++;

				// register as AppBar
				var appBarData = new NativeMethods.APPBARDATA(this.Handle, callbackMessageNum);

				NativeMethods.SHAppBarMessage(NativeMethods.ABM.ABM_NEW, ref appBarData);
			}

			public void Destroy()
			{
				// unregister as AppBar
				var appBarData = new NativeMethods.APPBARDATA(this.Handle);

				NativeMethods.SHAppBarMessage(NativeMethods.ABM.ABM_REMOVE, ref appBarData);

				DestroyHandle();
			}

			public bool SetPosition(Monitor monitor)
			{
				this.monitor = monitor;

				var appBarData = new NativeMethods.APPBARDATA(this.Handle, uEdge: edge, rc: new NativeMethods.RECT { left = monitor.Bounds.Left, right = monitor.Bounds.Right });

				if (edge == NativeMethods.ABE.ABE_TOP)
				{
					appBarData.rc.top = monitor.Bounds.Top;
					appBarData.rc.bottom = appBarData.rc.top + this.height;
				}
				else
				{
					appBarData.rc.bottom = monitor.Bounds.Bottom;
					appBarData.rc.top = appBarData.rc.bottom - this.height;
				}

				NativeMethods.SHAppBarMessage(NativeMethods.ABM.ABM_QUERYPOS, ref appBarData);

				if (edge == NativeMethods.ABE.ABE_TOP)
				{
					appBarData.rc.bottom = appBarData.rc.top + this.height;
				}
				else
				{
					appBarData.rc.top = appBarData.rc.bottom - this.height;
				}

				NativeMethods.SHAppBarMessage(NativeMethods.ABM.ABM_SETPOS, ref appBarData);

				var changedPosition = appBarData.rc.bottom != rect.bottom || appBarData.rc.top != rect.top ||
					appBarData.rc.left != rect.left || appBarData.rc.right != rect.right;

				this.rect = appBarData.rc;

				this.visible = true;

				return changedPosition;
			}

			public void Hide()
			{
				var appBarData = new NativeMethods.APPBARDATA(this.Handle, uEdge: NativeMethods.ABE.ABE_TOP);

				NativeMethods.SHAppBarMessage(NativeMethods.ABM.ABM_QUERYPOS, ref appBarData);
				NativeMethods.SHAppBarMessage(NativeMethods.ABM.ABM_SETPOS, ref appBarData);

				this.visible = false;
			}

			// move the bars to their respective positions
			public IntPtr PositionBars(IntPtr winPosInfo, IEnumerable<IBar> bars)
			{
				this.bars = bars;

				var topBar = edge == NativeMethods.ABE.ABE_TOP;
				var currentY = topBar ? rect.top : rect.bottom;
				foreach (var bar in bars)
				{
					if (!topBar)
					{
						currentY -= bar.GetBarHeight();
					}
					var barRect = new NativeMethods.RECT
						{
							left = rect.left,
							top = currentY,
							right = rect.right,
							bottom = currentY + bar.GetBarHeight()
						};
					if (topBar)
					{
						currentY += bar.GetBarHeight();
					}

					bar.OnClientWidthChanging(barRect.right - barRect.left);

					NativeMethods.AdjustWindowRectEx(ref barRect, NativeMethods.GetWindowStyleLongPtr(bar.Handle),
						NativeMethods.GetMenu(bar.Handle) != IntPtr.Zero, NativeMethods.GetWindowExStyleLongPtr(bar.Handle));

					winPosInfo = NativeMethods.DeferWindowPos(winPosInfo, bar.Handle, NativeMethods.HWND_TOPMOST, barRect.left, barRect.top,
						barRect.right - barRect.left, barRect.bottom - barRect.top, NativeMethods.SWP.SWP_NOACTIVATE);
				}

				isTopMost = true;

				return winPosInfo;
			}

			protected override void WndProc(ref Message m)
			{
				if (m.Msg == callbackMessageNum)
				{
					if (visible)
					{
						switch ((NativeMethods.ABN) m.WParam)
						{
							case NativeMethods.ABN.ABN_FULLSCREENAPP:
								if (m.LParam == IntPtr.Zero)
								{
									// full-screen app is closing
									if (!isTopMost)
									{
										var winPosInfo = NativeMethods.BeginDeferWindowPos(bars.Count());
										winPosInfo = this.bars.Aggregate(winPosInfo, (current, bar) =>
											NativeMethods.DeferWindowPos(current, bar.Handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
												NativeMethods.SWP.SWP_NOACTIVATE | NativeMethods.SWP.SWP_NOMOVE | NativeMethods.SWP.SWP_NOSIZE));
										NativeMethods.EndDeferWindowPos(winPosInfo);

										isTopMost = true;
									}
								}
								else
								{
									// full-screen app is opening - check if that is the desktop window
									var foregroundWindow = NativeMethods.GetForegroundWindow();
									if (isTopMost && NativeMethods.GetWindowClassName(foregroundWindow) != "WorkerW")
									{
										var winPosInfo = NativeMethods.BeginDeferWindowPos(bars.Count());
										winPosInfo = this.bars.Aggregate(winPosInfo, (current, bar) =>
											NativeMethods.DeferWindowPos(current, bar.Handle, NativeMethods.HWND_BOTTOM, 0, 0, 0, 0,
												NativeMethods.SWP.SWP_NOACTIVATE | NativeMethods.SWP.SWP_NOMOVE | NativeMethods.SWP.SWP_NOSIZE));
										NativeMethods.EndDeferWindowPos(winPosInfo);

										isTopMost = false;
									}
								}
								break;
							case NativeMethods.ABN.ABN_POSCHANGED:
								// ABN_POSCHANGED could be sent before the Monitor is notified of the change
								// of the working area in Windawesome::OnDisplaySettingsChanged
								monitor.SetBoundsAndWorkingArea();
								if (SetPosition(monitor))
								{
									var winPosInfo = NativeMethods.BeginDeferWindowPos(bars.Count());
									NativeMethods.EndDeferWindowPos(PositionBars(winPosInfo, bars));
								}
								break;
						}
					}
				}
				else
				{
					base.WndProc(ref m);
				}
			}
		}

		static Monitor()
		{
			// this is because Windows shows the taskbar at random points when it is made to autohide
			taskbarShownWinEventHook = NativeMethods.SetWinEventHook(NativeMethods.EVENT.EVENT_OBJECT_SHOW, NativeMethods.EVENT.EVENT_OBJECT_SHOW,
				IntPtr.Zero, taskbarShownWinEventDelegate, 0,
				NativeMethods.GetWindowThreadProcessId(SystemAndProcessInformation.taskbarHandle, IntPtr.Zero),
				NativeMethods.WINEVENT.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT.WINEVENT_SKIPOWNTHREAD);
		}

		internal Monitor(int monitorIndex)
		{
			this.workspaces = new Dictionary<Workspace, Tuple<int, AppBarNativeWindow, AppBarNativeWindow>>(2);
			this.temporarilyShownWindows = new HashSet<IntPtr>();
			this.monitorIndex = monitorIndex;
		}

		internal void Dispose()
		{
			// this statement uses the laziness of Where
			workspaces.Values.Select(t => t.Item2).Concat(workspaces.Values.Select(t => t.Item3)).
				Where(ab => ab != null && ab.Handle != IntPtr.Zero).ForEach(ab => ab.Destroy());
		}

		internal static void StaticDispose()
		{
			NativeMethods.UnhookWinEvent(taskbarShownWinEventHook);

			if (!isWindowsTaskbarShown)
			{
				ShowHideWindowsTaskbar(true);
			}
		}

		internal abstract void SetBoundsAndWorkingArea();

		internal void SetStartingWorkspace(Workspace startingWorkspace)
		{
			CurrentVisibleWorkspace = startingWorkspace;
		}

		internal void Initialize()
		{
			SetBoundsAndWorkingArea();

			ShowHideAppBars(null, CurrentVisibleWorkspace);

			ShowBars(CurrentVisibleWorkspace);

			CurrentVisibleWorkspace.SwitchTo();
		}

		public override bool Equals(object obj)
		{
			var other = obj as Monitor;
			return other != null && other.monitorIndex == this.monitorIndex;
		}

		public override int GetHashCode()
		{
			return this.monitorIndex;
		}

		private static void TaskbarShownWinEventDelegate(IntPtr hWinEventHook, NativeMethods.EVENT eventType,
			IntPtr hwnd, NativeMethods.OBJID idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
		{
			if (NativeMethods.IsWindowVisible(SystemAndProcessInformation.taskbarHandle) != isWindowsTaskbarShown)
			{
				ShowHideWindowsTaskbar(isWindowsTaskbarShown);
			}
		}

		internal void SwitchToWorkspace(Workspace workspace)
		{
			CurrentVisibleWorkspace.Unswitch();

			HideBars(workspace, CurrentVisibleWorkspace);

			// hides or shows the Windows taskbar
			if (Primary && workspace.ShowWindowsTaskbar != isWindowsTaskbarShown)
			{
				ShowHideWindowsTaskbar(workspace.ShowWindowsTaskbar);
			}

			ShowHideAppBars(CurrentVisibleWorkspace, workspace);

			CurrentVisibleWorkspace = workspace;

			ShowBars(CurrentVisibleWorkspace);

			workspace.SwitchTo();
		}

		internal void HideBars(Workspace newWorkspace, Workspace oldWorkspace)
		{
			var oldBarsAtTop = oldWorkspace.barsAtTop[monitorIndex];
			var oldBarsAtBottom = oldWorkspace.barsAtBottom[monitorIndex];
			var newBarsAtTop = newWorkspace.barsAtTop[monitorIndex];
			var newBarsAtBottom = newWorkspace.barsAtBottom[monitorIndex];

			oldBarsAtTop.Concat(oldBarsAtBottom).Except(newBarsAtTop.Concat(newBarsAtBottom)).ForEach(b => b.Hide());
		}

		internal void ShowBars(Workspace workspace)
		{
			var newBarsAtTop = workspace.barsAtTop[monitorIndex];
			var newBarsAtBottom = workspace.barsAtBottom[monitorIndex];

			newBarsAtTop.Concat(newBarsAtBottom).ForEach(b => b.Show());
		}

		internal void ShowHideAppBars(Workspace oldWorkspace, Workspace newWorkspace)
		{
			var oldWorkspaceTuple = oldWorkspace == null ? null : workspaces[oldWorkspace];
			var newWorkspaceTuple = workspaces[newWorkspace];

			if (oldWorkspaceTuple == null || newWorkspaceTuple.Item1 != oldWorkspaceTuple.Item1)
			{
				ShowHideAppBarsAndRepositionBars(
					oldWorkspaceTuple == null ? null : oldWorkspaceTuple.Item2,
					oldWorkspaceTuple == null ? null : oldWorkspaceTuple.Item3,
					newWorkspaceTuple.Item2,
					newWorkspaceTuple.Item3,
					newWorkspace);
			}
		}

		internal static void ShowHideWindowsTaskbar(bool showWindowsTaskbar)
		{
			var appBarData = new NativeMethods.APPBARDATA(SystemAndProcessInformation.taskbarHandle);
			var state = (NativeMethods.ABS) (uint) NativeMethods.SHAppBarMessage(NativeMethods.ABM.ABM_GETSTATE, ref appBarData);

			appBarData.lParam = (IntPtr) (showWindowsTaskbar ? state & ~NativeMethods.ABS.ABS_AUTOHIDE : state | NativeMethods.ABS.ABS_AUTOHIDE);
			NativeMethods.SHAppBarMessage(NativeMethods.ABM.ABM_SETSTATE, ref appBarData);

			var showHide = showWindowsTaskbar ? NativeMethods.SW.SW_SHOWNA : NativeMethods.SW.SW_HIDE;

			NativeMethods.ShowWindow(SystemAndProcessInformation.taskbarHandle, showHide);
			if (SystemAndProcessInformation.isAtLeastVista)
			{
				NativeMethods.ShowWindow(SystemAndProcessInformation.startButtonHandle, showHide);
			}

			isWindowsTaskbarShown = showWindowsTaskbar;
		}

		internal void AddWorkspace(Workspace workspace)
		{
			var workspaceBarsAtTop = workspace.barsAtTop[monitorIndex];
			var workspaceBarsAtBottom = workspace.barsAtBottom[monitorIndex];

			var matchingBar = workspaces.Keys.FirstOrDefault(ws =>
				workspaceBarsAtTop.SequenceEqual(ws.barsAtTop[monitorIndex]) && workspaceBarsAtBottom.SequenceEqual(ws.barsAtBottom[monitorIndex]));
			if (matchingBar != null)
			{
				var matchingWorkspace = workspaces[matchingBar];
				this.workspaces[workspace] = Tuple.Create(matchingWorkspace.Item1, matchingWorkspace.Item2, matchingWorkspace.Item3);

				return ;
			}

			var workspaceBarsEquivalentClass = (this.workspaces.Count == 0 ? 0 : this.workspaces.Values.Max(t => t.Item1)) + 1;

			AppBarNativeWindow appBarTopWindow;
			var topBarsHeight = workspaceBarsAtTop.Sum(bar => bar.GetBarHeight());
			var matchingAppBar = workspaces.Values.Select(t => t.Item2).FirstOrDefault(ab =>
				(ab == null && topBarsHeight == 0) || (ab != null && topBarsHeight == ab.height));
			if (matchingAppBar != null || topBarsHeight == 0)
			{
				appBarTopWindow = matchingAppBar;
			}
			else
			{
				appBarTopWindow = new AppBarNativeWindow(topBarsHeight, true);
			}

			AppBarNativeWindow appBarBottomWindow;
			var bottomBarsHeight = workspaceBarsAtBottom.Sum(bar => bar.GetBarHeight());
			matchingAppBar = workspaces.Values.Select(t => t.Item3).FirstOrDefault(uniqueAppBar =>
				(uniqueAppBar == null && bottomBarsHeight == 0) || (uniqueAppBar != null && bottomBarsHeight == uniqueAppBar.height));
			if (matchingAppBar != null || bottomBarsHeight == 0)
			{
				appBarBottomWindow = matchingAppBar;
			}
			else
			{
				appBarBottomWindow = new AppBarNativeWindow(bottomBarsHeight, false);
			}

			this.workspaces[workspace] = Tuple.Create(workspaceBarsEquivalentClass, appBarTopWindow, appBarBottomWindow);
		}

		internal void RemoveWorkspace(Workspace workspace)
		{
			var workspaceTuple = workspaces[workspace];
			workspaces.Remove(workspace);
			if (workspaceTuple.Item2 != null && workspaces.All(kv => kv.Value.Item2 != workspaceTuple.Item2))
			{
				workspaceTuple.Item2.Destroy();
			}
			if (workspaceTuple.Item3 != null && workspaces.All(kv => kv.Value.Item3 != workspaceTuple.Item3))
			{
				workspaceTuple.Item3.Destroy();
			}
		}

		private void ShowHideAppBarsAndRepositionBars(AppBarNativeWindow previousAppBarTopWindow, AppBarNativeWindow previousAppBarBottomWindow,
			AppBarNativeWindow newAppBarTopWindow, AppBarNativeWindow newAppBarBottomWindow,
			Workspace newWorkspace)
		{
			ShowHideAppBarForms(previousAppBarTopWindow, newAppBarTopWindow);
			ShowHideAppBarForms(previousAppBarBottomWindow, newAppBarBottomWindow);

			var newBarsAtTop = newWorkspace.barsAtTop[monitorIndex];
			var newBarsAtBottom = newWorkspace.barsAtBottom[monitorIndex];

			var winPosInfo = NativeMethods.BeginDeferWindowPos(newBarsAtTop.Count + newBarsAtBottom.Count);
			if (newAppBarTopWindow != null)
			{
				winPosInfo = newAppBarTopWindow.PositionBars(winPosInfo, newBarsAtTop);
			}
			if (newAppBarBottomWindow != null)
			{
				winPosInfo = newAppBarBottomWindow.PositionBars(winPosInfo, newBarsAtBottom);
			}
			NativeMethods.EndDeferWindowPos(winPosInfo);
		}

		private void ShowHideAppBarForms(AppBarNativeWindow hideForm, AppBarNativeWindow showForm)
		{
			// this whole thing is so complicated as to avoid changing of the working area if the bars in the new workspace
			// take the same space as the one in the previous one

			// set the working area to a new one if needed
			if (hideForm != null)
			{
				if (showForm == null || hideForm != showForm)
				{
					hideForm.Hide();
					if (showForm != null)
					{
						showForm.SetPosition(this);
					}
				}
			}
			else if (showForm != null)
			{
				showForm.SetPosition(this);
			}
		}

		/// <summary>
		/// Return the number of physical monitors corresponding to this Monitor
		/// </summary>
		/// <returns></returns>
		internal abstract int PhysicalMonitorCount { get; }

		/// <summary>
		/// The # of physical displays, according to Windows
		/// </summary>
		public static int TotalMonitorsReportedByWindows
		{
			get { return Screen.AllScreens.Length; }
		}
	}

	/// <summary>
	/// A PhysicalMonitor represents an actual physical monitor. (At least it represents a single
	/// monitor according to Windows.)
	/// </summary>
	public class PhysicalMonitor : Monitor
	{
		private readonly IntPtr handle;
		private readonly Screen screen;

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="monitorIndex"></param>
		internal PhysicalMonitor(int monitorIndex)
			: this(monitorIndex, monitorIndex)
		{
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="logicalMonitorIndex">Monitor index as far as Windawesome is concerned</param>
		/// <param name="physicalMonitorIndex">Monitor index as far as Windows is concerned</param>
		internal PhysicalMonitor(int logicalMonitorIndex, int physicalMonitorIndex)
			: base(logicalMonitorIndex)
		{
			this.screen = Screen.AllScreens[physicalMonitorIndex];
			var rect = NativeMethods.RECT.FromRectangle(this.screen.Bounds);
			this.handle = NativeMethods.MonitorFromRect(ref rect, NativeMethods.MFRF.MONITOR_MONITOR_DEFAULTTONULL);
		}

		public override bool Primary
		{
			get { return screen.Primary; }
		}

		internal override void SetBoundsAndWorkingArea()
		{
			var monitorInfo = NativeMethods.MONITORINFO.Default;
			NativeMethods.GetMonitorInfo(this.handle, ref monitorInfo);
			Bounds = monitorInfo.rcMonitor.ToRectangle();
			WorkingArea = monitorInfo.rcWork.ToRectangle();
		}

		internal override int PhysicalMonitorCount
		{
			get { return 1; }
		}
	}

	/// <summary>
	/// <para>A CompositeMonitor represents a set of physical monitors that Windawesome will treat as if
	/// they were a single display. Some graphics cards can support such monitor spanning natively, of
	/// course, but many of them don't, and people don't always have a choice of graphics card. (e.g. At
	/// work, for example.) CompositeMonitor can fake it reasonably well if the graphics card can't
	/// do it.</para>
	/// <para>The use case that inspired this: Say you have two 1680x1050 displays. Not that big a deal, and
	/// perhaps you are to be pitied for not having bigger ones. But suppose you put both of the displays
	/// side-by-side in portrait orientation, and have Windawesome treat them (and lay out your windows) as
	/// if they were one big monitor, rather than two separate ones. Well now you have a poor man's
	/// 2100x3360 display; it's as if you now own, say, a 38in monitor. For some tastes, this may be more
	/// appealing than two smaller separate displays. Plus Windawesome makes it easier than normal to
	/// work with such large displays, because you don't have to waste time moving Windows all around.</para>
	/// </summary>
	public class CompositeMonitor : Monitor
	{
		Monitor[] submonitors;
		bool spanHorizontally; // false means vertically

		public CompositeMonitor(int monitorIndex, IEnumerable<Monitor> submonitors, bool spanHorizontaly = true)
			: base(monitorIndex)
		{
			this.submonitors = submonitors.ToArray();
			this.spanHorizontally = spanHorizontaly;
		}

		public override bool Primary
		{
			get
			{
				return submonitors.Any(m => m.Primary);
			}
		}

		internal override void SetBoundsAndWorkingArea()
		{
			submonitors.ForEach(m => m.SetBoundsAndWorkingArea());
			Bounds = MakeCompositeRectangle(submonitors.Select(m => m.Bounds));
			WorkingArea = MakeCompositeRectangle(submonitors.Select(m => m.WorkingArea));
		}

		Rectangle MakeCompositeRectangle(IEnumerable<Rectangle> submonitorRects)
		{
			int left, right, top, bottom;
			if (spanHorizontally)
			{
				// width: span as far as possible
				left = submonitorRects.Min(rect => rect.Left);
				right = submonitorRects.Max(rect => rect.Right);

				// height: limit to overlapping areas
				top = submonitorRects.Max(rect => rect.Top);
				bottom = submonitorRects.Min(rect => rect.Bottom);
			}
			else
			{
				// height: span as far as possible
				top = submonitorRects.Min(rect => rect.Top);
				bottom = submonitorRects.Max(rect => rect.Bottom);

				// width: limit to overlapping areas
				left = submonitorRects.Max(rect => rect.Left);
				right = submonitorRects.Min(rect => rect.Right);
			}

			int width = right - left;
			int height = bottom - top;

			return new Rectangle(left, top, width, height);
		}

		internal override int PhysicalMonitorCount
		{
			get { return submonitors.Length; }
		}
	}

    public class SplitScreenMonitor : Monitor
    {
        Monitor parentMonitor;
        bool leftSide;

        public SplitScreenMonitor(int logicalMonitorIndex, Monitor parentMonitor, bool leftSide)
            : base(logicalMonitorIndex)
        {
            this.parentMonitor = parentMonitor;
            this.leftSide = leftSide;
        }

        // Create with a parent monitor, and a percentage of the screen to use.
        // And horizontal vs vertical.
        //
        // This kind of screws up the counting of physical monitors, though.

        internal override int PhysicalMonitorCount
        {
            // Bad
            get { return 1; }
        }

        public override bool Primary
        {
            get { return parentMonitor.Primary; }
        }

        Rectangle TranslateParentRect(Rectangle parentRect)
        {
            int leftWidth = parentRect.Width / 2;
            int rightWidth = parentRect.Width - leftWidth;
            if (leftSide)
            {
                return new Rectangle(parentRect.Left, parentRect.Top, leftWidth, parentRect.Height);
            }
            else
            {
                return new Rectangle(leftWidth, parentRect.Top, rightWidth, parentRect.Height);
            }
        }

        // Huh...Even this horrible time hack doesn't work to keep the righthand bar from being placed
        // below the left.
        internal override void SetBoundsAndWorkingArea()
        {
            lock (parentMonitor)
            {
                if (DateTime.Now.Subtract(parentUpdate).TotalSeconds > 10)
                {
                    parentMonitor.SetBoundsAndWorkingArea();
                    cachedParentBounds = parentMonitor.Bounds;
                    cachedParentWorkingArea = parentMonitor.WorkingArea;
                    parentUpdate = DateTime.Now;
                }
            }

            Bounds = TranslateParentRect(cachedParentBounds);
            WorkingArea = TranslateParentRect(cachedParentWorkingArea);
        }

        static Rectangle cachedParentBounds = new Rectangle();
        static Rectangle cachedParentWorkingArea = new Rectangle();
        static DateTime parentUpdate = DateTime.MinValue;
    }

	public class MonitorFactory
	{
		/// <summary>
		/// Creates one Monitor for every physical display in the system
		/// (as reported by Windows)
		/// </summary>
		/// <returns></returns>
		public static Monitor[] CreateMonitors()
		{
			Monitor[] monitors = new Monitor[Monitor.TotalMonitorsReportedByWindows];
			for (int i = 0; i < Monitor.TotalMonitorsReportedByWindows; i++)
			{
				monitors[i] = new PhysicalMonitor(i);
			}

			monitors.ForEach(m => m.SetBoundsAndWorkingArea());

			return monitors;
		}

		/// <summary>
		/// Creates a set of monitors, some of which may be physical
		/// monitors, and some of which may be composites of several
		/// physical monitors
		/// </summary>
		/// <param name="compositeMonitorDefs"></param>
		/// <returns></returns>
		public static Monitor[] CreateMonitors(params IList<int>[] compositeMonitorDefs)
		{
			int logicalIndex = 0;

			Monitor[] logicalMonitors =
				compositeMonitorDefs.Select(compositeDef =>
				{
					if (compositeDef.Count == 1)
					{
						// There isn't really such a thing as a logicalMonitor
						// index for a submonitor, but we need one for the
						// constructor. Passing logicalMonitor == -1.
						//
						// It'd probably be better to make CompositeMonitor
						// directly use Screen objects??
						return new PhysicalMonitor(logicalIndex++, compositeDef[0]) as Monitor;
					}
					else
					{
						var submonitors =
							compositeDef.Select(physicalIndex => new PhysicalMonitor(-1, physicalIndex));
						return new CompositeMonitor(logicalIndex++, submonitors) as Monitor;
					}
				}).ToArray();

			logicalMonitors.ForEach(m => m.SetBoundsAndWorkingArea());

			return logicalMonitors;
		}
	}
}
