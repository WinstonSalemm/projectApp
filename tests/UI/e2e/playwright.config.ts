import { defineConfig } from '@playwright/test';
import { WINDOWS_1440_LIGHT, WINDOWS_2560_DARK, TAB7_LANDSCAPE_LIGHT, TAB7_PORTRAIT_DARK } from './helpers/devices';

const baseURL = process.env.BASE_URL || 'http://localhost:5173';

export default defineConfig({
  testDir: './specs',
  timeout: 60000,
  use: {
    baseURL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'off',
  },
  reporter: [['list'], ['html', { outputFolder: '../../_reports/playwright' }]],
  projects: [
    { name: 'windows-1440-light', use: WINDOWS_1440_LIGHT },
    { name: 'windows-2560-dark', use: WINDOWS_2560_DARK },
    { name: 'tab7-landscape-light', use: TAB7_LANDSCAPE_LIGHT },
    { name: 'tab7-portrait-dark', use: TAB7_PORTRAIT_DARK },
  ],
});
