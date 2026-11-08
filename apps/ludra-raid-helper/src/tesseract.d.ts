declare module 'tesseract.js' {
  export function createWorker(language?: string): Promise<{
    recognize: (image: string) => Promise<{ data: { text: string } }>;
    terminate: () => Promise<unknown>;
  }>;
}
