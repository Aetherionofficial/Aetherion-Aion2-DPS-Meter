interface Window {
  raidHelperDesktop?: {
    captureScreen: () => Promise<string | null>;
  };
}
