const { contextBridge, ipcRenderer } = require("electron");

contextBridge.exposeInMainWorld("electronApp", {
  platform: process.platform,
  isElectron: true,
});

if (process.platform === "win32") {
  contextBridge.exposeInMainWorld("windowControls", {
    platform: process.platform,
    minimize: () => ipcRenderer.send("window:minimize"),
    toggleMaximize: () => ipcRenderer.send("window:toggle-maximize"),
    close: () => ipcRenderer.send("window:close"),
    isMaximized: () => ipcRenderer.invoke("window:is-maximized"),
    onMaximizedChange: (callback) => {
      const handler = (_evt, isMax) => callback(Boolean(isMax));
      ipcRenderer.on("window:maximized-changed", handler);
      return () =>
        ipcRenderer.removeListener("window:maximized-changed", handler);
    },
  });
}
