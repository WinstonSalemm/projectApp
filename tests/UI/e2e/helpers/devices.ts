import { devices, type BrowserContextOptions } from '@playwright/test';

export const WINDOWS_1440_LIGHT: BrowserContextOptions = {
  viewport: { width: 1440, height: 900 },
  colorScheme: 'light',
  deviceScaleFactor: 1,
  isMobile: false,
  ...devices['Desktop Chrome'],
};

export const WINDOWS_2560_DARK: BrowserContextOptions = {
  viewport: { width: 2560, height: 1440 },
  colorScheme: 'dark',
  deviceScaleFactor: 1,
  isMobile: false,
  ...devices['Desktop Chrome'],
};

export const TAB7_LANDSCAPE_LIGHT: BrowserContextOptions = {
  viewport: { width: 1280, height: 800 },
  colorScheme: 'light',
  deviceScaleFactor: 2,
  isMobile: true,
  hasTouch: true,
  userAgent: 'Mozilla/5.0 (Linux; Android 13; SM-T870) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36',
};

export const TAB7_PORTRAIT_DARK: BrowserContextOptions = {
  viewport: { width: 800, height: 1280 },
  colorScheme: 'dark',
  deviceScaleFactor: 2,
  isMobile: true,
  hasTouch: true,
  userAgent: 'Mozilla/5.0 (Linux; Android 13; SM-T870) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36',
};
