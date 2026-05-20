const {
  app,
  BrowserWindow,
  Menu,
  ipcMain,
  shell,
} = require("electron");
const fs = require("node:fs");
const path = require("node:path");

const isDev = !app.isPackaged;
const DEV_URL = process.env.VITE_DEV_SERVER_URL || "http://127.0.0.1:5173";
const ICON_PATH = path.join(__dirname, "..", "build", "icon.png");
const STATE_PATH = path.join(app.getPath("userData"), "window-state.json");

const DEFAULT_BOUNDS = { width: 1520, height: 940 };
const MIN_SIZE = { minWidth: 1100, minHeight: 720 };

// ─── Window state persistence ────────────────────────
function loadState() {
  try {
    return JSON.parse(fs.readFileSync(STATE_PATH, "utf8"));
  } catch {
    return {};
  }
}

function saveState(win) {
  if (!win || win.isDestroyed()) return;
  try {
    const bounds = win.isMaximized() ? win.getNormalBounds() : win.getBounds();
    fs.writeFileSync(
      STATE_PATH,
      JSON.stringify({ ...bounds, isMaximized: win.isMaximized() })
    );
  } catch {
    /* best-effort; ignore */
  }
}

function debounce(fn, wait) {
  let t;
  return (...args) => {
    clearTimeout(t);
    t = setTimeout(() => fn(...args), wait);
  };
}

// ─── Window factory ──────────────────────────────────
function createWindow() {
  const state = loadState();
  const isWin = process.platform === "win32";

  const win = new BrowserWindow({
    width: state.width || DEFAULT_BOUNDS.width,
    height: state.height || DEFAULT_BOUNDS.height,
    x: state.x,
    y: state.y,
    ...MIN_SIZE,
    backgroundColor: "#1F1F1F",
    title: "Teams Call Simulator",
    icon: fs.existsSync(ICON_PATH) ? ICON_PATH : undefined,
    autoHideMenuBar: true,
    show: false,
    frame: !isWin,
    titleBarStyle: isWin ? "hidden" : "default",
    webPreferences: {
      preload: path.join(__dirname, "preload.cjs"),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
    },
  });

  if (state.isMaximized) win.maximize();

  win.once("ready-to-show", () => win.show());

  win.webContents.setWindowOpenHandler(({ url }) => {
    shell.openExternal(url);
    return { action: "deny" };
  });

  const persist = debounce(() => saveState(win), 400);
  ["resize", "move"].forEach((ev) => win.on(ev, persist));
  win.on("maximize", () => {
    saveState(win);
    win.webContents.send("window:maximized-changed", true);
  });
  win.on("unmaximize", () => {
    saveState(win);
    win.webContents.send("window:maximized-changed", false);
  });
  win.on("close", () => saveState(win));

  if (isDev) {
    win.loadURL(DEV_URL);
  } else {
    win.loadFile(path.join(__dirname, "..", "dist", "index.html"));
  }

  return win;
}

// ─── IPC for the frameless title bar ────────────────
ipcMain.on("window:minimize", (e) =>
  BrowserWindow.fromWebContents(e.sender)?.minimize()
);
ipcMain.on("window:toggle-maximize", (e) => {
  const w = BrowserWindow.fromWebContents(e.sender);
  if (!w) return;
  if (w.isMaximized()) w.unmaximize();
  else w.maximize();
});
ipcMain.on("window:close", (e) =>
  BrowserWindow.fromWebContents(e.sender)?.close()
);
ipcMain.handle("window:is-maximized", (e) =>
  Boolean(BrowserWindow.fromWebContents(e.sender)?.isMaximized())
);

// ─── App lifecycle ───────────────────────────────────
Menu.setApplicationMenu(null);

app.whenReady().then(() => {
  createWindow();
  app.on("activate", () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") app.quit();
});
