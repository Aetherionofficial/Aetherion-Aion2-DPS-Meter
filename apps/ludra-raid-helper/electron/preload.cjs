const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('raidHelperDesktop', {
  captureScreen: () => ipcRenderer.invoke('raid-helper:capture-screen'),
});
