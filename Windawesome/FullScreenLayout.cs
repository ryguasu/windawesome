﻿using System;
using System.Collections.Generic;
using System.Drawing;

namespace Windawesome
{
	public class FullScreenLayout : ILayout
	{
		private Workspace workspace;
		private Rectangle workingArea;

		private void MaximizeWindow(Window window)
		{
			var windowIsMaximized = NativeMethods.IsZoomed(window.hWnd);
			var ws = NativeMethods.GetWindowStyleLongPtr(window.hWnd);
			if (ws.HasFlag(NativeMethods.WS.WS_CAPTION | NativeMethods.WS.WS_MAXIMIZEBOX))
			{
				// if there is a caption, we can make the window maximized

				var screen = System.Windows.Forms.Screen.FromHandle(window.hWnd);
				if (!screen.Bounds.IntersectsWith(workingArea))
				{
					RestoreAndMaximizeArea(window, windowIsMaximized);
					windowIsMaximized = false;
				}

				if (!windowIsMaximized)
				{
					// TODO: this activates the window which is not desirable. Is there a way NOT to?
					NativeMethods.ShowWindowAsync(window.hWnd, NativeMethods.SW.SW_SHOWMAXIMIZED);
				}
			}
			else
			{
				// otherwise, Windows would make the window "truly" full-screen, i.e. on top of all shell
				// windows, which doesn't work for us. So we just set the window to take the maximum possible area

				RestoreAndMaximizeArea(window, windowIsMaximized);
			}
		}

		private void RestoreAndMaximizeArea(Window window, bool windowIsMaximized)
		{
			if (windowIsMaximized)
			{
				NativeMethods.ShowWindowAsync(window.hWnd, NativeMethods.SW.SW_SHOWNOACTIVATE); // should not use SW_RESTORE as it activates the window
				System.Threading.Thread.Sleep(Workspace.minimizeRestoreDelay);
			}
			NativeMethods.SetWindowPos(window.hWnd, IntPtr.Zero,
				workingArea.X, workingArea.Y, workingArea.Width, workingArea.Height,
				NativeMethods.SWP.SWP_ASYNCWINDOWPOS | NativeMethods.SWP.SWP_NOACTIVATE |
				NativeMethods.SWP.SWP_NOZORDER | NativeMethods.SWP.SWP_NOOWNERZORDER |
				NativeMethods.SWP.SWP_FRAMECHANGED | NativeMethods.SWP.SWP_NOCOPYBITS);
		}

		#region ILayout Members

		string ILayout.LayoutSymbol()
		{
			return workspace.GetWindowsCount() == 0 ? "[M]" : "[" + workspace.GetWindowsCount() + "]";
		}

		public string LayoutName()
		{
			return "Full Screen";
		}

		void ILayout.Initialize(Workspace workspace)
		{
			this.workspace = workspace;
			this.workingArea = workspace.Monitor.screen.WorkingArea;

			workspace.WindowTitlebarToggled += MaximizeWindow;
			workspace.WindowBorderToggled += MaximizeWindow;
		}

		bool ILayout.ShouldSaveAndRestoreSharedWindowsPosition()
		{
			return false;
		}

		void ILayout.Reposition()
		{
			this.workingArea = workspace.Monitor.screen.WorkingArea;
			workspace.GetWindows().ForEach(MaximizeWindow);
			Windawesome.DoLayoutUpdated();
		}

		void ILayout.WindowMinimized(Window window)
		{
		}

		void ILayout.WindowRestored(Window window)
		{
			MaximizeWindow(window);
		}

		void ILayout.WindowCreated(Window window)
		{
			if (workspace.IsWorkspaceVisible)
			{
				MaximizeWindow(window);
				Windawesome.DoLayoutUpdated();
			}
		}

		void ILayout.WindowDestroyed(Window window)
		{
			if (workspace.IsWorkspaceVisible)
			{
				Windawesome.DoLayoutUpdated();
			}
		}

		#endregion
	}
}
