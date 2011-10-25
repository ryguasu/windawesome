from System.Linq import Enumerable
from Windawesome import Windawesome, ProgramRule, State, OnWindowShownAction, OnWindowCreatedOnCurrentWorkspaceAction, NativeMethods
from Windawesome.NativeMethods import WS, WS_EX

config.ProgramRules = [
	ProgramRule(
		className = "^cygwin/x X rl*$",
		windowCreatedDelay = 200,
		rules = [ProgramRule.Rule(workspace = 5)]
	),
	ProgramRule(
		className = "^TApplication$",
		displayName = "^Find and Run Robot$",
		isManaged = False
	),
	ProgramRule(
		className = "^TApplication$",
		rules = [ProgramRule.Rule(isFloating = True)]
	),
	ProgramRule(
		className = "^Vim$",
		windowCreatedDelay = 100,
		rules = [ProgramRule.Rule(workspace = 3, titlebar = State.HIDDEN, windowBorders = State.HIDDEN)]
	),
	ProgramRule(
		className = "^MozillaWindowClass$",
		rules = [ProgramRule.Rule(workspace = 2)]
	),
	ProgramRule(
		className = "^MozillaDialogClass$",
		rules = [ProgramRule.Rule(workspace = 2, isFloating = True)]
	),
	ProgramRule(
		className = "^CabinetWClass$", # Windows Explorer
		rules = [ProgramRule.Rule(workspace = 1)]
	),
	ProgramRule(
		className = "^ExploreWClass$", # Windows Explorer
		rules = [ProgramRule.Rule(workspace = 1)]
	),
	ProgramRule(
		displayName = ".*BitComet.*",
		onWindowCreatedAction = OnWindowShownAction.HideWindow,
		showMenu = False,
		rules = [ProgramRule.Rule(workspace = 9, titlebar = State.HIDDEN, windowBorders = State.HIDDEN)]
	),
	ProgramRule(
		displayName = ".*SA Dictionary 2010.*",
		rules = [ProgramRule.Rule(isFloating = True)]
	),
	ProgramRule(
		className = "^rctrl_renwnd32$", # Outlook
		rules = [ProgramRule.Rule(workspace = 8)]
	),
	ProgramRule(
		className = "^TLoginForm.*", # Skype
		rules = [ProgramRule.Rule(isFloating = True)]
	),
	ProgramRule(
		className = "^tSkMainForm.*", # Skype
		rules = [ProgramRule.Rule(workspace = 4, titlebar = State.HIDDEN, windowBorders = State.HIDDEN)]
	),
	ProgramRule(
		className = "^TConversationForm.*", # Skype
		onWindowCreatedAction = OnWindowShownAction.TemporarilyShowWindowOnCurrentWorkspace,
		rules = [ProgramRule.Rule(workspace = 4, titlebar = State.HIDDEN, windowBorders = State.HIDDEN)]
	),
	ProgramRule(
		className = "^MediaPlayerClassicW$",
		rules = [ProgramRule.Rule(workspace = 1)]
	),
	ProgramRule(
		className = "^mintty$",
		redrawDesktopOnWindowCreated = True,
		rules = [ProgramRule.Rule(workspace = 3, titlebar = State.HIDDEN, windowBorders = State.HIDDEN)]
	),
	ProgramRule(
		className = "^{97E27FAA-C0B3-4b8e-A693-ED7881E99FC1}$", # foobar2000
		rules = [ProgramRule.Rule(workspace = 7)]
	),
	ProgramRule(
		displayName = ".*Microsoft Visual Studio.*",
		onWindowCreatedAction = OnWindowShownAction.HideWindow,
		rules = [ProgramRule.Rule(workspace = 5, titlebar = State.HIDDEN, windowBorders = State.HIDDEN)]
	),
	ProgramRule(
		className = "^HwndWrapper\[DefaultDomain.*", # Visual Studio (Express)
		onWindowCreatedAction = OnWindowShownAction.HideWindow,
		rules = [ProgramRule.Rule(workspace = 5, titlebar = State.HIDDEN, windowBorders = State.HIDDEN)]
	),
	ProgramRule(
		className = "^XLMAIN$", # Excel
		rules = [ProgramRule.Rule(redrawOnShow = True)]
	),
	ProgramRule(
		className = "^QuickTimePlayerMain$",
		rules = [ProgramRule.Rule(workspace = 1, isFloating = True)]
	),
	ProgramRule(
		className = "^wxWindowClassNR$", # digsby Buddy List
		displayName = "^Buddy List$",
		processName = "^digsby-app$",
		customMatchingFunction = lambda hWnd: True,
		onWindowCreatedAction = OnWindowShownAction.TemporarilyShowWindowOnCurrentWorkspace,
		rules = [ProgramRule.Rule(workspace = 4, isFloating = True)]
	),
	ProgramRule(
		className = "^wxWindowClass$", # digsby chat window
		processName = "^digsby-app$",
		onWindowCreatedAction = OnWindowShownAction.HideWindow,
		rules = [ProgramRule.Rule(workspace = 4, titlebar = State.HIDDEN, windowBorders = State.HIDDEN)]
	),
	ProgramRule(
		className = "^OpusApp$",
		displayName = ".*Microsoft Word Viewer$",
		tryAgainAfter = 500
	),
	ProgramRule(
		className = "^ConsoleWindowClass$", # Interix terminal
		rules = [ProgramRule.Rule(isFloating = True)]
	),
	ProgramRule(
		className = "^Internet Explorer_Hidden$", # what the hell
		displayName = "",
		isManaged = False
	),
	ProgramRule(
		className = "^Edit$", # some stupid window of Adobe Reader
		displayName = "",
		isManaged = False
	),
	ProgramRule(
		className = "^ShockwaveFlashFullScreen$", # Adobe Flash Player when full screen
		displayName = "^Adobe Flash Player$",
		isManaged = False
	),
	ProgramRule(
		displayName = "^Windows 7 Manager - Junk File Cleaner$",
		isManaged = False
	),
	ProgramRule(
		displayName = "^Windows 7 Manager - Registry Cleaner$",
		isManaged = False
	),
	ProgramRule(
		displayName = ".*Windows 7 Manager.*",
		rules = [ProgramRule.Rule(workspace = 1)]
	),
	ProgramRule(
		className = "^MsiDialogCloseClass$",
		isManaged = False
	),
	ProgramRule(
		className = "^MsiDialogNoCloseClass$",
		isManaged = False
	),
	ProgramRule(
		className = "^#32770$", # all dialogs
		rules = [ProgramRule.Rule(isFloating = True)] # should be floating
	),
	ProgramRule(
		styleContains = WS.WS_POPUP,
		isManaged = False
	),
	ProgramRule(
		styleNotContains = WS.WS_MAXIMIZEBOX,
		rules = [ProgramRule.Rule(isFloating = True)]
	),
	ProgramRule() # an all-catching rule in the end to manage all other windows
]