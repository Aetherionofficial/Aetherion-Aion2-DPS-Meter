const { app, BrowserWindow, desktopCapturer, ipcMain, session } = require('electron');
const path = require('node:path');

const isDev = process.argv.includes('--dev');

function createWindow() {
  const window = new BrowserWindow({
    width: 1240,
    height: 900,
    minWidth: 900,
    minHeight: 650,
    backgroundColor: '#080b18',
    title: 'Ludra Raid Helper',
    webPreferences: {
      preload: path.join(__dirname, 'preload.cjs'),
      contextIsolation: true,
      nodeIntegration: false,
    },
  });

  if (isDev) window.loadURL('http://localhost:5173');
  else window.loadFile(path.join(__dirname, '..', 'dist', 'index.html'));
}

ipcMain.handle('raid-helper:capture-screen', async () => {
  const sources = await desktopCapturer.getSources({ types: ['screen'], thumbnailSize: { width: 1920, height: 1080 } });
  const source = sources[0];
  return source ? source.thumbnail.toDataURL() : null;
});

app.whenReady().then(() => {
  session.defaultSession.setPermissionRequestHandler((_webContents, permission, callback) => callback(permission === 'display-capture'));
  createWindow();
  app.on('activate', () => { if (BrowserWindow.getAllWindows().length === 0) createWindow(); });
});

app.on('window-all-closed', () => { if (process.platform !== 'darwin') app.quit(); });
